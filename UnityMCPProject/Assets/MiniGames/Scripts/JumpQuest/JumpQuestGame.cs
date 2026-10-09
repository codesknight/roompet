using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 跳一跳: hold to charge, release to hop from box to box.
    ///
    /// This replaced a side-scrolling platformer, which the player asked for by name — and the
    /// replacement is a better fit for where this game is played. A platformer wants two thumbs, a
    /// run-up and a level you learn; a hop wants one finger and thirty seconds, which is what "a
    /// small game you open from your pet's room" actually means.
    ///
    /// The shape is the same as the other two games, deliberately: <see cref="HopRules"/> is pure
    /// arithmetic with all the tests, this class only builds boxes and animates one arc, and the
    /// HUD is IMGUI. Nothing here decides anything the rules could have decided.
    /// </summary>
    public class JumpQuestGame : MonoBehaviour
    {
        public enum Phase { Ready, Charging, Flying, Dead }

        public Phase State { get; private set; } = Phase.Ready;

        /// <summary>Points scored: one per box, two for a perfect landing.</summary>
        public int Score { get; private set; }

        /// <summary>Best score of this session.</summary>
        public int Best { get; private set; }

        /// <summary>How long the current charge has been held, in seconds.</summary>
        public float Held { get; private set; }

        /// <summary>Charge as 0..1, for the bar.</summary>
        public float Charge => Mathf.Clamp01(Held / HopRules.FullChargeSeconds(_settings));

        /// <summary>Where the character is standing, along the line.</summary>
        public float PositionX { get; private set; }

        /// <summary>Set briefly after a perfect landing, for the "+2" popup.</summary>
        public string LastPopup { get; private set; } = "";

        private float _popupUntil;

        private HopSettings _settings = HopSettings.Default;
        private readonly System.Random _rng = new System.Random();

        private class Box
        {
            public Transform Transform;
            public float X;          // centre, along the line
            public float Size;       // half-extent
            public float Height;
            public float Drift;      // sideways offset, cosmetic
        }

        private readonly List<Box> _boxes = new List<Box>();
        private Transform _player;
        private Camera _camera;
        private float _lastAspect;

        private float _hopFrom;
        private float _hopTo;
        private float _hopT;
        private float _deathAt;
        private bool _wasHeld;
        private float _nextX;
        private int _colour;

        /// <summary>
        /// Which box the player is standing on. Separate from <see cref="Score"/> on purpose: a
        /// perfect landing scores two points but is still one box further along the line.
        /// </summary>
        private int _boxIndex;

        private static readonly Color[] Palette =
        {
            new Color(0.42f, 0.58f, 0.86f),   // blue
            new Color(0.52f, 0.76f, 0.55f),   // green
            new Color(0.88f, 0.62f, 0.42f),   // orange
            new Color(0.76f, 0.52f, 0.78f),   // purple
            new Color(0.90f, 0.80f, 0.48f),   // yellow
            new Color(0.46f, 0.72f, 0.76f)    // teal
        };

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            ResetRun();
        }

        private void BuildWorld()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            // A three-quarter view, so the boxes read as boxes rather than as bars.
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.19f, 0.28f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            _camera = camera;
            FitCamera();

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.9f);
                light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.48f, 0.56f);

            _player = BuildPlayer();
        }

        /// <summary>
        /// The character: the pet itself, as a little animal.
        ///
        /// It shipped as a bottle-shaped figure (a white cylinder with a sphere and a scarf), which
        /// is a fine placeholder and a strange thing to be jumping around in a game that lives
        /// behind a door in your pet's room. The species comes from the pet's own save, so the thing
        /// hopping is the animal the player has been looking after.
        /// </summary>
        private Transform BuildPlayer()
        {
            var root = new GameObject("Player").transform;
            root.SetParent(transform, false);

            _kind = DshMobile.MiniAnimal.Current;
            DshMobile.MiniAnimal.Build(root, _kind, 1.05f);

            // A soft blob under its feet: without it the animal is hard to place against a box of a
            // similar colour, and "am I on the box or beside it" is the whole game. (The first
            // version of this was a flat black cylinder — legible, but it read as a hole cut in the
            // box rather than as a shadow.)
            DshMobile.SoftShadow.Attach(root, 0.30f, 0.30f, 0.45f);

            return root;
        }

        /// <summary>Which animal the hero is, for the HUD to name it.</summary>
        private DshMobile.MiniAnimalKind _kind = DshMobile.MiniAnimalKind.Cat;

        /// <summary>The hero's animal name, e.g. 狐狸.</summary>
        public string HeroName => DshMobile.MiniAnimal.Name(_kind);

        private Transform Shape(string name, PrimitiveType type, Vector3 localPosition,
            Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;

            Tint(go, color);
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        private Transform ShapeParented(Transform parent, string name, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;

            Tint(go, color);
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        private static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            // One material per object: they are few, and a shared one would make every box change
            // colour when one of them did.
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.32f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ boxes

        private Box SpawnBox(float x, float size, float height, Color tint, float drift)
        {
            var root = new GameObject("Box").transform;
            root.SetParent(transform, false);

            var cube = ShapeParented(root, "Block", new Vector3(size * 2f, height, size * 2f), tint);
            cube.localPosition = new Vector3(0f, height * 0.5f, 0f);

            // A lighter top face, so the box reads as a box from a three-quarter camera.
            var top = ShapeParented(root, "Top", new Vector3(size * 2f, 0.06f, size * 2f),
                Color.Lerp(tint, Color.white, 0.28f));
            top.localPosition = new Vector3(0f, height + 0.03f, 0f);

            root.position = new Vector3(x, 0f, drift);
            return new Box { Transform = root, X = x, Size = size, Height = height, Drift = drift };
        }

        private void ClearBoxes()
        {
            for (int i = 0; i < _boxes.Count; i++)
            {
                if (_boxes[i].Transform != null) Destroy(_boxes[i].Transform.gameObject);
            }
            _boxes.Clear();
        }

        private void SpawnNext()
        {
            float gap = HopRules.NextGap((float)_rng.NextDouble(), Score, _settings);
            float drift = HopRules.NextDrift((float)_rng.NextDouble(), _settings);

            _colour = (_colour + 1) % Palette.Length;
            _nextX += gap;

            _boxes.Add(SpawnBox(_nextX, _settings.BoxHalfSize, 1.1f, Palette[_colour], drift));
        }

        // ------------------------------------------------------------------ run control

        public void ResetRun()
        {
            ClearBoxes();
            Score = 0;
            _boxIndex = 0;
            Held = 0f;
            PositionX = 0f;
            _nextX = 0f;
            _hopT = 0f;
            _hopFrom = 0f;
            _hopTo = 0f;
            State = Phase.Ready;
            LastPopup = "";

            _boxes.Add(SpawnBox(0f, _settings.BoxHalfSize, 1.1f, Palette[0], 0f));
            SpawnNext();

            ApplyPlayer(0f, 0f);
            SnapCamera();
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>Starts charging, while a finger or the space bar is down.</summary>
        public void BeginCharge()
        {
            if (State == Phase.Dead)
            {
                if (Time.time - _deathAt > 0.7f) ResetRun();
                return;
            }

            if (State == Phase.Ready) State = Phase.Charging;
        }

        /// <summary>Releases the charge and hops. Also the "tap to play again" path.</summary>
        public void Release()
        {
            if (State == Phase.Dead)
            {
                if (Time.time - _deathAt > 0.7f) ResetRun();
                return;
            }

            if (State == Phase.Ready) State = Phase.Charging;
            if (State != Phase.Charging) return;

            float distance = HopRules.HopDistance(Held, _settings);
            _hopFrom = PositionX;
            _hopTo = PositionX + distance;
            _hopT = 0f;
            Held = 0f;
            State = Phase.Flying;

            DshMobile.MobileHaptics.Light();

            // The box under the player squashes as they leave it: the tiny piece of feedback that
            // makes a hop feel like it had weight.
            var from = _boxes.Count > 0
                ? _boxes[Mathf.Clamp(_boxIndex, 0, _boxes.Count - 1)].Transform
                : null;
            if (from != null) StartCoroutine(Squash(from));
        }

        private System.Collections.IEnumerator Squash(Transform box)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                float k = 1f - Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.10f;
                if (box != null) box.localScale = new Vector3(1f, k, 1f);
                yield return null;
            }
            if (box != null) box.localScale = Vector3.one;
        }

        /// <summary>
        /// Resolves the landing. Public so a test (or an autopilot) can drive a whole run without
        /// waiting for an animation.
        /// </summary>
        public void Land()
        {
            // The box the player is standing on is counted in BOXES, not in points: a perfect
            // landing is worth two points but still advances one box, and conflating the two made
            // the game look up a box that did not exist yet the first time anyone landed dead
            // centre. Caught by driving a run from a script before it ever reached a phone.
            var next = _boxes.Count > _boxIndex + 1 ? _boxes[_boxIndex + 1] : null;
            var landing = next == null
                ? HopRules.Landing.Missed
                : HopRules.Judge(_hopFrom, _hopTo - _hopFrom, next.X, _settings);

            if (landing == HopRules.Landing.Missed)
            {
                Die();
                return;
            }

            PositionX = _hopTo;
            _boxIndex++;
            Score += HopRules.PointsFor(landing);
            if (Score > Best) Best = Score;

            if (landing == HopRules.Landing.Perfect)
            {
                LastPopup = "完美 +2";
                _popupUntil = Time.time + 1.1f;
                DshMobile.MobileHaptics.Medium();
            }
            else
            {
                DshMobile.MobileHaptics.Light();
            }

            SpawnNext();
            State = Phase.Ready;
            ApplyPlayer(PositionX, 0f);
        }

        private void Die()
        {
            State = Phase.Dead;
            _deathAt = Time.time;
            DshMobile.MobileHaptics.Heavy();

            // Paid immediately: the coins were earned on the way, and a crash should not take them.
            int coins = HopRules.CoinsFor(Score);
            if (coins > 0) DshMobile.PetWallet.Add(coins);
        }

        public int RunCoins => HopRules.CoinsFor(Score);

        // ------------------------------------------------------------------ camera

        private void FitCamera()
        {
            if (_camera == null) return;
            _lastAspect = _camera.aspect;

            // Two constraints, and the *smaller* one wins for framing: the player only needs to see
            // the box they are on and the next one or two, so demanding a wide view on a portrait
            // phone would shrink the whole toy into a strip down the middle of the screen. Five
            // units of half-width is enough for the next box, and the height covers the arc.
            float byHeight = 4.4f;
            float byWidth = 5f / Mathf.Max(0.2f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(byHeight, byWidth);
        }

        private void SnapCamera() => FollowCamera(1f);

        private void FollowCamera(float lerp)
        {
            if (_camera == null) return;

            var rotation = Quaternion.Euler(28f, -22f, 0f);
            var offset = rotation * new Vector3(0.9f, 3.4f, -13.5f);
            var wanted = new Vector3(PositionX + 1.2f, 0.4f, 0f) + offset;

            _camera.transform.position = Vector3.Lerp(_camera.transform.position, wanted, lerp);
            _camera.transform.rotation = rotation;
        }

        // ------------------------------------------------------------------ loop

        /// <summary>Held input, pushed by the HUD's touch pad.</summary>
        public bool HoldInput { get; set; }

        /// <summary>One-shot "the hold ended", pushed by the HUD when a finger lifts.</summary>
        public bool ReleaseInput { get; set; }

        private void Update()
        {
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();

            bool mouseHeld = Input.GetMouseButton(0) && !JumpQuestHud.PointerOverPanel;
            bool keyHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W);
            bool held = HoldInput || mouseHeld || keyHeld;

            if (held && !_wasHeld) BeginCharge();
            else if (!held && _wasHeld) Release();
            else if (ReleaseInput) Release();

            _wasHeld = held;
            ReleaseInput = false;

            if (State == Phase.Charging) Held += Time.deltaTime;

            if (State == Phase.Flying)
            {
                _hopT += Time.deltaTime / Mathf.Max(0.05f, _settings.HopSeconds);
                if (_hopT >= 1f)
                {
                    _hopT = 1f;
                    Land();
                }
                else
                {
                    var p = HopRules.ArcPosition(_hopFrom, _hopTo, _hopT, _settings);
                    ApplyPlayer(p.x, p.y * 0.35f);
                }
            }

            if (State == Phase.Dead) TickFall();

            if (!string.IsNullOrEmpty(LastPopup) && Time.time > _popupUntil) LastPopup = "";

            FollowCamera(1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        private float _fallY;

        /// <summary>
        /// The miss animation: the figure tips over the edge and drops. Short, and it stops — a
        /// character that disappears into the void reads as a bug rather than as a loss.
        /// </summary>
        private void TickFall()
        {
            _fallY -= Time.deltaTime * 5.5f;
            if (_fallY < -3.4f) _fallY = -3.4f;

            if (_player == null) return;
            _player.position = new Vector3(_hopTo, _fallY, 0f);
            _player.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -70f,
                Mathf.Clamp01(-_fallY / 3.4f)));
        }

        private void ApplyPlayer(float x, float y)
        {
            if (_player == null) return;
            if (State != Phase.Dead) _fallY = 0f;

            var box = _boxes.Count > 0
                ? _boxes[Mathf.Clamp(_boxIndex, 0, _boxes.Count - 1)]
                : null;
            float z = box != null ? box.Drift : 0f;
            float baseHeight = box != null ? box.Height : 0f;

            _player.position = new Vector3(x, baseHeight + y, z);
            _player.rotation = Quaternion.Euler(0f, 0f,
                State == Phase.Flying ? Mathf.Lerp(-12f, 12f, _hopT) : 0f);
        }

        /// <summary>Goes home, the same way the other games do.</summary>
        public void ReturnToRoom()
        {
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
