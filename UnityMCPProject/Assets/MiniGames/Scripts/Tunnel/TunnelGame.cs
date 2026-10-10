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

        private const float RingSpacing = 2.2f;
        private const int RingCount = 24;
        private const float RingRadius = 1.9f;
        private const int RingSegments = 18;

        private Camera _camera;
        private AudioSource _sfx;
        private Transform _ship;
        private Vector2 _shipPos;
        private Vector2 _lastHole;
        private int _gateIndex;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private readonly List<Transform> _rings = new List<Transform>();
        private float _spawnTimer;
        private float _deathAt;

        private class Obstacle
        {
            public Transform Root;
            public Transform Visual;
            public bool IsGate;
            public Vector2 Hole;       // gate: hole centre; mine: current (drifting) centre
            public Vector2 BaseHole;   // mine: centre before drift
            public float Radius;
            public bool Scored;
            public GateShape Shape;
            public float DriftPhase;
            public float DriftRadius;
            public float DriftSpeed;
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
            BuildTunnel();
            BuildVanishingPoint();
        }

        private void BuildShip()
        {
            var root = new GameObject("Ship").transform;
            root.SetParent(transform, false);

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

        private void BuildTunnel()
        {
            for (int i = 0; i < RingCount; i++)
            {
                var ring = BuildRing();
                ring.SetParent(transform, false);
                ring.localPosition = new Vector3(0f, 0f, -7f + i * RingSpacing);
                _rings.Add(ring);
            }
            ApplyRingTheme();
        }

        private Transform BuildRing()
        {
            var ring = new GameObject("TunnelRing").transform;
            for (int s = 0; s < RingSegments; s++)
            {
                float angle = s * Mathf.PI * 2f / RingSegments;
                Prim("Seg", PrimitiveType.Cube, ring,
                    new Vector3(Mathf.Cos(angle) * RingRadius, Mathf.Sin(angle) * RingRadius, 0f),
                    new Vector3(0.42f, 0.10f, 0.10f),
                    Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg),
                    Color.white, 0.3f);
            }
            return ring;
        }

        /// <summary>A distant bright dot at the tunnel's vanishing point.</summary>
        private void BuildVanishingPoint()
        {
            Prim("VanishingPoint", PrimitiveType.Sphere, transform,
                new Vector3(0f, 0f, 40f), new Vector3(2.5f, 2.5f, 0.2f),
                Quaternion.identity, new Color(1f, 0.9f, 0.8f), 2.5f);
        }

        private void ApplyRingTheme()
        {
            var accent = TunnelRules.TunnelColor(Theme, true);
            var baseColor = TunnelRules.TunnelColor(Theme, false);
            if (_camera != null) _camera.backgroundColor = baseColor * 0.12f;
            RenderSettings.fogColor = baseColor * 0.12f;

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring == null) continue;
                for (int s = 0; s < ring.childCount; s++)
                {
                    var r = ring.GetChild(s).GetComponent<Renderer>();
                    if (r != null) Paint(r, s % 2 == 0 ? accent : baseColor, s % 2 == 0 ? 0.9f : 0.35f);
                }
            }
        }

        // ------------------------------------------------------------------ run

        public void StartRun(TunnelMode mode, TunnelTheme theme)
        {
            Mode = mode;
            Theme = theme;
            Score = 0;
            _shipPos = Vector2.zero;
            _lastHole = Vector2.zero;
            _gateIndex = 0;
            _ship.localPosition = Vector3.zero;
            foreach (var o in _obstacles) if (o.Root != null) Destroy(o.Root.gameObject);
            _obstacles.Clear();
            _spawnTimer = 0f;
            ApplyRingTheme();
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

            // Rings scroll past the ship and wrap, which is the forward-motion illusion.
            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring == null) continue;
                var p = ring.localPosition;
                p.z -= TunnelRules.ForwardSpeed * dt;
                if (p.z < -7f) p.z += RingCount * RingSpacing;
                ring.localPosition = p;
            }

            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                var o = _obstacles[i];
                Vector3 p = o.Root.localPosition;
                p.z -= TunnelRules.ForwardSpeed * dt;
                o.Root.localPosition = p;

                // Floating mines drift in a small circle, so the obstacle is never sitting still.
                if (!o.IsGate && o.Visual != null)
                {
                    o.DriftPhase += dt * o.DriftSpeed;
                    Vector2 drift = new Vector2(Mathf.Cos(o.DriftPhase), Mathf.Sin(o.DriftPhase)) * o.DriftRadius;
                    o.Hole = TunnelRules.ClampToTunnel(o.BaseHole + drift);
                    o.Visual.localPosition = new Vector3(o.Hole.x, o.Hole.y, 0f);
                }

                if (o.IsGate)
                {
                    if (p.z <= 0f && !o.Scored)
                    {
                        o.Scored = true;
                        if (TunnelRules.IsInsideHole(_shipPos, o.Hole, o.Shape, o.Radius))
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
                         TunnelRules.Collides(_shipPos, o.Hole, o.Radius + TunnelRules.ShipRadius))
                {
                    Die();
                    return;
                }

                // Recycle as soon as it is behind the ship, so a passed gate never lingers
                // between the camera and the ship to block the view of what is coming.
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

        private void SpawnObstacle(float z)
        {
            var root = new GameObject(Mode == TunnelMode.Survival ? "Gate" : "Mine").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, z);

            var o = new Obstacle { Root = root, IsGate = Mode == TunnelMode.Survival };
            if (o.IsGate)
            {
                // Chain each hole from the previous one, so two consecutive gates are never an
                // impossible pair no matter how the ship wanders between them. The hole starts
                // wide and shrinks as the score rises, and its shape widens from circles to
                // squares / triangles / semicircles as the run goes on.
                float r = TunnelRules.GateHoleRadiusFor(Score);
                o.Shape = TunnelRules.GateShapeFor(Score, _gateIndex++);
                o.Hole = TunnelRules.NextGateHole(_lastHole, Random.value, Random.value, r);
                _lastHole = o.Hole;
                o.Radius = r;
                BuildGate(root, o.Hole, o.Shape, r);
            }
            else
            {
                float r = TunnelRules.MineRadiusFor(Score);
                o.BaseHole = TunnelRules.NextMine(_shipPos, Random.value, Random.value);
                o.Hole = o.BaseHole;
                o.Radius = r;
                o.DriftPhase = Random.value * Mathf.PI * 2f;
                o.DriftRadius = 0.28f;
                o.DriftSpeed = 1.2f + Random.value * 0.8f;
                o.Visual = BuildMine(root, o.Hole, r);
            }
            _obstacles.Add(o);
        }

        /// <summary>
        /// A wall with a shaped hole: a glowing outline of the hole plus radial spokes from the
        /// outline out to the tunnel wall, so it reads as a solid wall with that shape cut out.
        /// </summary>
        private void BuildGate(Transform root, Vector2 hole, GateShape shape, float radius)
        {
            var accent = TunnelRules.TunnelColor(Theme, true);
            var points = TunnelRules.GateShapePoints(shape, radius, 16);

            // Spokes: from each outline point, outward to the tunnel wall along its radial direction.
            for (int s = 0; s < points.Length; s++)
            {
                Vector2 dir = points[s].normalized;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                float inner = points[s].magnitude;
                float midR = (inner + RingRadius) * 0.5f;
                float length = RingRadius - inner;
                Prim("Spoke", PrimitiveType.Cube, root,
                    new Vector3(hole.x + dir.x * midR, hole.y + dir.y * midR, 0f),
                    new Vector3(length, 0.12f, 0.12f),
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg),
                    accent, 0.8f);
            }

            // The glowing outline of the hole shape itself.
            for (int s = 0; s < points.Length; s++)
            {
                Prim("Rim", PrimitiveType.Cube, root,
                    new Vector3(hole.x + points[s].x, hole.y + points[s].y, 0f),
                    new Vector3(0.26f, 0.16f, 0.14f),
                    Quaternion.identity,
                    new Color(1f, 0.95f, 0.6f), 1.2f);
            }
        }

        private Transform BuildMine(Transform root, Vector2 at, float radius)
        {
            var body = Prim("MineBody", PrimitiveType.Sphere, root,
                new Vector3(at.x, at.y, 0f), Vector3.one * (radius * 2f),
                Quaternion.identity, new Color(0.95f, 0.32f, 0.28f), 1.2f);
            return body.transform;
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
