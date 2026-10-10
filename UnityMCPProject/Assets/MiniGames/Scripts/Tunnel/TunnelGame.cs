using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 窒息隧道: fly a ship down a themed 3D tunnel with one virtual stick (up/down/left/right).
    /// Survival mode threads moving gate holes; mine mode dodges obstacles.
    /// </summary>
    public class TunnelGame : MonoBehaviour
    {
        public enum Phase { Menu, Running, Dead }

        public Phase State { get; private set; } = Phase.Menu;
        public TunnelMode Mode { get; private set; } = TunnelMode.Survival;
        public TunnelTheme Theme { get; private set; } = TunnelTheme.Neon;
        public int Score { get; private set; }
        public int RunCoins => TunnelRules.CoinsFor(Score);

        /// <summary>Best score of the session.</summary>
        public int Best { get; private set; }

        private const float ForwardSpeed = 6f;

        private Camera _camera;
        private Transform _ship;
        private Vector2 _shipPos;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private float _spawnTimer;
        private float _deathAt;

        private class Obstacle
        {
            public Transform Root;
            public bool IsGate;
            public Vector2 Hole;      // gate: the hole centre; mine: the obstacle centre
            public float Radius;
            public bool Scored;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            State = Phase.Menu;
        }

        // ------------------------------------------------------------------ world

        private void BuildWorld()
        {
            var existing = Camera.main;
            if (existing == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                existing = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            existing.orthographic = false;
            existing.fieldOfView = 60f;
            existing.transform.position = new Vector3(0f, 0f, -6.5f);
            existing.transform.rotation = Quaternion.identity;
            existing.clearFlags = CameraClearFlags.SolidColor;
            existing.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            existing.nearClipPlane = 0.1f;
            existing.farClipPlane = 60f;
            _camera = existing;

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.05f;
                light.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.48f);

            BuildShip();
            BuildTunnel();
        }

        private void BuildShip()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Ship";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // point down the tunnel
            go.transform.localScale = new Vector3(0.4f, 0.7f, 0.4f);
            var mat = new Material(Shader.Find("Standard")) { name = "Ship" };
            mat.color = new Color(1f, 0.9f, 0.4f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.2f) * 0.6f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().enabled = false;
            _ship = go.transform;
        }

        private void BuildTunnel()
        {
            var tunnel = new GameObject("Tunnel").transform;
            tunnel.SetParent(transform, false);
            var baseColor = TunnelRules.TunnelColor(TunnelTheme.Neon, false);
            var accent = TunnelRules.TunnelColor(TunnelTheme.Neon, true);

            for (int i = 0; i < 12; i++)
            {
                float z = -4f + i * 2.2f;
                var ring = new GameObject("Ring" + i).transform;
                ring.SetParent(tunnel, false);
                ring.localPosition = new Vector3(0f, 0f, z);

                for (int side = 0; side < 4; side++)
                {
                    float a = side * 90f;
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "Side";
                    box.transform.SetParent(ring, false);
                    box.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                    box.transform.localPosition = new Vector3(0f, 1.9f, 0f);
                    box.transform.localScale = new Vector3(3.8f, 0.12f, 0.12f);
                    var m = new Material(Shader.Find("Standard")) { name = "Ring" };
                    m.color = side % 2 == 0 ? accent : baseColor;
                    box.GetComponent<Renderer>().sharedMaterial = m;
                    box.GetComponent<Collider>().enabled = false;
                }
            }
        }

        private void ApplyTheme()
        {
            var tunnel = transform.Find("Tunnel");
            if (tunnel == null) return;
            var accent = TunnelRules.TunnelColor(Theme, true);
            var baseColor = TunnelRules.TunnelColor(Theme, false);
            for (int i = 0; i < tunnel.childCount; i++)
            {
                var ring = tunnel.GetChild(i);
                for (int side = 0; side < ring.childCount; side++)
                {
                    var r = ring.GetChild(side).GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial.color = side % 2 == 0 ? accent : baseColor;
                }
            }
            if (_camera != null) _camera.backgroundColor = baseColor * 0.15f;
        }

        // ------------------------------------------------------------------ run

        public void StartRun(TunnelMode mode, TunnelTheme theme)
        {
            Mode = mode;
            Theme = theme;
            Score = 0;
            _shipPos = Vector2.zero;
            _ship.localPosition = Vector3.zero;
            foreach (var o in _obstacles) if (o.Root != null) Destroy(o.Root.gameObject);
            _obstacles.Clear();
            _spawnTimer = 0f;
            ApplyTheme();
            State = Phase.Running;
        }

        private void Update()
        {
            if (State == Phase.Dead && Time.time - _deathAt > 0.9f) return;

            if (State == Phase.Running) TickRunning();
            else if (State == Phase.Dead) TickDeath();
        }

        private void TickRunning()
        {
            // Ship moves in the cross-section, driven by the HUD's single stick.
            Vector2 stick = TunnelHud.Stick;
            _shipPos += stick * TunnelRules.ShipSpeed * Time.deltaTime;
            _shipPos = TunnelRules.ClampToTunnel(_shipPos);
            _ship.localPosition = new Vector3(_shipPos.x, _shipPos.y, 0f);

            // Obstacles fly toward the ship.
            float dt = Time.deltaTime;
            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                var o = _obstacles[i];
                Vector3 p = o.Root.localPosition;
                p.z -= ForwardSpeed * dt;
                o.Root.localPosition = p;

                if (o.IsGate)
                {
                    if (p.z <= 0f && !o.Scored)
                    {
                        o.Scored = true;
                        if (TunnelRules.Collides(_shipPos, o.Hole, TunnelRules.GateHoleRadius))
                        {
                            Score++;
                            DshMobile.MobileHaptics.Light();
                        }
                        else
                        {
                            Die();
                            return;
                        }
                    }
                }
                else if (Mathf.Abs(p.z) < 0.4f &&
                         TunnelRules.Collides(_shipPos, o.Hole, TunnelRules.MineRadius + TunnelRules.ShipRadius))
                {
                    Die();
                    return;
                }

                if (p.z < -4f)
                {
                    Destroy(o.Root.gameObject);
                    _obstacles.RemoveAt(i);
                }
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                SpawnObstacle(TunnelRules.SpawnAhead);
                _spawnTimer = TunnelRules.Spacing / ForwardSpeed;
            }
        }

        private void SpawnObstacle(float z)
        {
            float r1 = Random.value;
            float r2 = Random.value;
            var root = new GameObject(Mode == TunnelMode.Survival ? "Gate" : "Mine").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, z);

            var o = new Obstacle { Root = root, IsGate = Mode == TunnelMode.Survival };

            if (o.IsGate)
            {
                o.Hole = TunnelRules.NextGateHole(_shipPos, r1, r2);
                o.Radius = TunnelRules.GateHoleRadius;
                BuildGate(root, o.Hole);
            }
            else
            {
                o.Hole = TunnelRules.NextMine(_shipPos, r1, r2);
                o.Radius = TunnelRules.MineRadius;
                BuildMine(root, o.Hole);
            }
            _obstacles.Add(o);
        }

        private void BuildGate(Transform root, Vector2 hole)
        {
            // A solid wall with a round hole: four boxes around the hole.
            var accent = TunnelRules.TunnelColor(Theme, true);
            float r = TunnelRules.GateHoleRadius;
            var panel = new GameObject("Panel").transform;
            panel.SetParent(root, false);
            panel.localPosition = new Vector3(hole.x, hole.y, 0f);

            for (int side = 0; side < 4; side++)
            {
                float a = side * 90f;
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(panel, false);
                box.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                box.transform.localPosition = new Vector3(0f, r + 1.4f, 0f);
                box.transform.localScale = new Vector3(4f, 2.8f, 0.14f);
                SetObstacleMaterial(box, accent, true);
            }
        }

        private void BuildMine(Transform root, Vector2 at)
        {
            var mine = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mine.transform.SetParent(root, false);
            mine.transform.localPosition = new Vector3(at.x, at.y, 0f);
            mine.transform.localScale = Vector3.one * (TunnelRules.MineRadius * 2f);
            SetObstacleMaterial(mine, new Color(0.95f, 0.35f, 0.3f), true);
        }

        private void SetObstacleMaterial(GameObject go, Color color, bool glow)
        {
            var m = new Material(Shader.Find("Standard")) { name = "Obstacle" };
            m.color = color;
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.5f);
            }
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Collider>().enabled = false;
        }

        private void TickDeath()
        {
            // Idle on the result screen.
        }

        private void Die()
        {
            if (State == Phase.Dead) return;
            State = Phase.Dead;
            _deathAt = Time.time;
            if (Score > Best) Best = Score;
            if (RunCoins > 0) DshMobile.PetWallet.Add(RunCoins);
            DshMobile.MobileHaptics.Heavy();
        }

        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the tunnel game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }

        /// <summary>Back to the menu, clearing the run.</summary>
        public void BackToMenu()
        {
            State = Phase.Menu;
            foreach (var o in _obstacles) if (o.Root != null) Destroy(o.Root.gameObject);
            _obstacles.Clear();
        }
    }
}
