using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 窒息隧道: fly a ship down a neon 3D tunnel with one virtual stick. The tunnel is a run of
    /// glowing rings scrolling past a fixed ship, which is what sells the sense of forward motion;
    /// survival mode threads moving gate holes, mine mode dodges obstacles.
    /// </summary>
    public class TunnelGame : MonoBehaviour
    {
        public enum Phase { Menu, Running, Dead }

        public Phase State { get; private set; } = Phase.Menu;
        public TunnelMode Mode { get; private set; } = TunnelMode.Survival;
        public TunnelTheme Theme { get; private set; } = TunnelTheme.Neon;
        public int Score { get; private set; }
        public int RunCoins => TunnelRules.CoinsFor(Score);
        public int Best { get; private set; }

        /// <summary>How fast the ship answers the stick. Player-adjustable and persisted.</summary>
        public float Sensitivity
        {
            get => TunnelRules.ClampSensitivity(PlayerPrefs.GetFloat("dshtunnel.sensitivity", 1f));
            set => PlayerPrefs.SetFloat("dshtunnel.sensitivity", TunnelRules.ClampSensitivity(value));
        }

        private Camera _camera;
        private AudioSource _sfx;
        private Transform _ship;
        private Vector2 _shipPos;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private float _spawnTimer;
        private float _deathAt;

        private class Obstacle
        {
            public Transform Root;
            public Vector2 Hole;      // the debris's centre in the tunnel cross-section
            public float Radius;
            public bool Scored;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            BuildSource();
            State = Phase.Menu;
        }

        private void BuildSource()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
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
            existing.fieldOfView = 62f;
            // Close behind the ship for a more immersive, "in the tunnel" feel: the ship fills more
            // of the frame and the walls rush past the edges.
            existing.transform.position = new Vector3(0f, 0f, -6.0f);
            existing.transform.rotation = Quaternion.identity;
            existing.clearFlags = CameraClearFlags.SolidColor;
            existing.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
            existing.nearClipPlane = 0.05f;
            existing.farClipPlane = 120f;
            _camera = existing;

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 0.9f;
                light.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.42f);

            // A faint fog gives the tunnel depth: far rings fade into the vanishing point.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.06f);
            RenderSettings.fogDensity = 0.10f;

            BuildShip();
        }

        private void BuildShip()
        {
            var root = new GameObject("Ship").transform;
            root.SetParent(transform, false);

            // The ship is the pet's animal (Kenney chick) facing +Z into the tunnel, with a small
            // glow behind it so it still reads as "flying". Falls back to the rocket when missing.
            var model = DshMobile.KenneyModel.Load("animal-chick", root, 0.75f, centerVertically: true);
            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Prim("Glow", PrimitiveType.Sphere, root,
                    new Vector3(0f, 0f, -0.5f), new Vector3(0.4f, 0.4f, 0.4f),
                    Quaternion.identity, new Color(1f, 0.7f, 0.3f), 1.3f);
                _ship = root;
                return;
            }

            // A little rocket: a body, a nose, and a glowing engine cone.
            Prim("Body", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0f, 0.2f), new Vector3(0.34f, 0.62f, 0.34f),
                Quaternion.Euler(90f, 0f, 0f), new Color(0.85f, 0.9f, 1f), 0.25f);
            Prim("Nose", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0f, 0.62f), new Vector3(0.18f, 0.34f, 0.18f),
                Quaternion.Euler(90f, 0f, 0f), new Color(0.95f, 0.95f, 1f), 0.25f);
            Prim("Engine", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0f, -0.28f), new Vector3(0.26f, 0.16f, 0.26f),
                Quaternion.Euler(90f, 0f, 0f), new Color(1f, 0.55f, 0.2f), 0.9f);
            Prim("Glow", PrimitiveType.Sphere, root,
                new Vector3(0f, 0f, -0.4f), new Vector3(0.5f, 0.5f, 0.5f),
                Quaternion.identity, new Color(1f, 0.7f, 0.3f), 1.3f);

            _ship = root;
        }

        private void ApplyTheme()
        {
            // No more tunnel rings (feedback: 圆环太丑). Just the coloured void + fog, and the
            // scattered debris ahead reads against it.
            var baseColor = TunnelRules.TunnelColor(Theme, false);
            if (_camera != null) _camera.backgroundColor = baseColor * 0.12f;
            RenderSettings.fogColor = baseColor * 0.12f;
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
        }

        private void TickRunning()
        {
            float dt = Time.deltaTime;

            // The ship moves in the cross-section, driven by the HUD's single stick, scaled by the
            // player's sensitivity setting.
            _shipPos += TunnelHud.Stick * TunnelRules.ShipSpeed * Sensitivity * dt;
            _shipPos = TunnelRules.ClampToTunnel(_shipPos);
            _ship.localPosition = new Vector3(_shipPos.x, _shipPos.y, 0f);

            // Camera follows the ship with a dampened offset, so the whole view shifts as the ship
            // banks left/right/up/down — the parallax that sells "I'm flying this", not watching it.
            Vector3 camTarget = new Vector3(_shipPos.x * 0.5f, _shipPos.y * 0.5f, _camera.transform.position.z);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, camTarget,
                1f - Mathf.Exp(-7f * dt));

            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                var o = _obstacles[i];
                Vector3 p = o.Root.localPosition;
                p.z -= TunnelRules.ForwardSpeed * dt;
                o.Root.localPosition = p;

                // Debris you did NOT hit is debris you dodged — that is the score. Collision is a
                // plain circle-vs-circle against the debris's radius, so you dodge *around* it.
                if (Mathf.Abs(p.z) < 0.4f &&
                    TunnelRules.Collides(_shipPos, o.Hole, o.Radius + TunnelRules.ShipRadius))
                {
                    Die();
                    return;
                }
                if (!o.Scored && p.z < -0.6f)
                {
                    o.Scored = true;
                    Score++;
                    DshMobile.MobileHaptics.Light();
                }

                // Recycle once it is behind the ship.
                if (p.z < -2f)
                {
                    Destroy(o.Root.gameObject);
                    _obstacles.RemoveAt(i);
                }
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                SpawnObstacle(TunnelRules.SpawnAhead);
                _spawnTimer = TunnelRules.TimeBetweenObstacles;
            }
        }

        private static readonly string[] DebrisModels =
        {
            "Runner/Nature/rock_largeA",
            "Runner/Nature/log",
            "Runner/Nature/rock_smallA",
            "Runner/Nature/stump_round",
            "Runner/Nature/tree_blocks",
        };

        private void SpawnObstacle(float z)
        {
            // A cluster of scattered debris, like rubbish dumped in the tunnel — dodge *around* the
            // pieces (feedback: 障碍物随机摆，像丢进隧道里的垃圾，从旁边躲过去). Count grows a
            // little with score.
            int count = 3 + Mathf.Min(2, Score / 12);
            for (int i = 0; i < count; i++)
            {
                SpawnDebris(z + i * 1.1f);
            }
        }

        private void SpawnDebris(float z)
        {
            var root = new GameObject("Debris").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, z);

            float radius = 0.30f + Random.value * 0.22f;
            Vector2 pos = RandomDebrisPosition();

            var o = new Obstacle { Root = root, Hole = pos, Radius = radius };
            BuildDebris(root, pos, radius, Random.Range(0, DebrisModels.Length));

            _obstacles.Add(o);
        }

        /// <summary>A random spot in the cross-section, kept off the ship's current spot so a fresh
        /// cluster is never an instant death.</summary>
        private Vector2 RandomDebrisPosition()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(0.35f, TunnelRules.TunnelRadius - 0.55f, Mathf.Sqrt(Random.value));
                var pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if ((pos - _shipPos).sqrMagnitude > 0.5f) return TunnelRules.ClampToTunnel(pos);
            }
            return TunnelRules.ClampToTunnel(new Vector2(1.2f, 0f));
        }

        private void BuildDebris(Transform root, Vector2 at, float radius, int modelIndex)
        {
            var model = DshMobile.KenneyModel.LoadPath(DebrisModels[modelIndex], root, radius * 2.2f, true);
            if (model != null)
            {
                model.localPosition = new Vector3(at.x, at.y, 0f);
                model.localRotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
                return;
            }

            // Fallback: a grey boulder.
            Prim("Debris", PrimitiveType.Sphere, root,
                new Vector3(at.x, at.y, 0f), Vector3.one * (radius * 2f),
                Quaternion.identity, new Color(0.45f, 0.45f, 0.5f), 0.3f);
        }

        // ------------------------------------------------------------------ helpers

        private GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Quaternion localRot, Color color, float emission)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRot;

            var collider = go.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Paint(go.GetComponent<Renderer>(), color, emission);
            return go;
        }

        private void Paint(Renderer renderer, Color color, float emission)
        {
            if (renderer == null) return;
            var mat = new Material(Shader.Find("Standard")) { name = "Tunnel" };
            mat.color = color;
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
            }
            renderer.sharedMaterial = mat;
        }

        private void Die()
        {
            if (State == Phase.Dead) return;
            State = Phase.Dead;
            _deathAt = Time.time;
            if (Score > Best) Best = Score;
            if (RunCoins > 0) DshMobile.PetWallet.Add(RunCoins);
            PlaySfx(PrismAudio.Tone(150f, 0.4f, 0.5f, 0.18f));
            DshMobile.MobileHaptics.Heavy();
        }

        private void PlaySfx(float[] samples)
        {
            if (_sfx == null || samples == null) return;
            _sfx.PlayOneShot(PrismAudio.ToClip("tunnel", samples));
        }

        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the tunnel game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }

        public void BackToMenu()
        {
            State = Phase.Menu;
            foreach (var o in _obstacles) if (o.Root != null) Destroy(o.Root.gameObject);
            _obstacles.Clear();
        }
    }
}
