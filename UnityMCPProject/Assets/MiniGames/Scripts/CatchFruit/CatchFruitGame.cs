using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 接果子: move left and right, catch what falls.
    ///
    /// The fourth game, and the first with *continuous* input rather than a tap: it needs a held
    /// direction, which is why it also needed the touch layer's held-button support that the
    /// platformer taught us to use. Fruit falls, the basket catches it, three misses end the run.
    ///
    /// Same shape as the other three: <see cref="CatchRules"/> is pure arithmetic with the tests,
    /// this builds cones and spheres and moves them, and the HUD is IMGUI.
    /// </summary>
    public class CatchFruitGame : MonoBehaviour
    {
        public enum Phase { Ready, Playing, Dead }

        public Phase State { get; private set; } = Phase.Ready;
        public int Caught { get; private set; }
        public int Lives { get; private set; } = CatchRules.StartLives;
        public int Best { get; private set; }
        public int Missed { get; private set; }

        private CatchSettings _settings = CatchSettings.Default;
        private readonly System.Random _rng = new System.Random();
        private readonly List<Fruit> _fruits = new List<Fruit>();

        private class Fruit
        {
            public Transform Transform;
            public float X;
            public float Y;
            public float FallSpeed;
            public bool Counted;
            public bool IsBomb;
        }

        private Transform _basket;
        private Camera _camera;
        private float _lastAspect;
        private float _nextSpawn;
        private float _deathAt;
        private float _basketX;

        /// <summary>
        /// How many world units the basket moves per unit of finger drag. 1 means the basket tracks
        /// the thumb one-to-one; lower feels heavier, higher feels twitchier. Persisted so the choice
        /// survives across runs (feedback: 接果子 should let the player tune its sensitivity).
        /// </summary>
        public float Sensitivity { get; private set; } = 1f;

        private const string SensitivityKey = "dshpet.catch.sensitivity";

        public void SetSensitivity(float value)
        {
            Sensitivity = Mathf.Clamp(value, 0.5f, 2.0f);
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.Save();
        }

        /// <summary>Where the round-robin over the fruit kinds has got to.</summary>
        private int _fruitIndex;

        private static readonly Color AppleRed = new Color(0.90f, 0.24f, 0.24f);
        private static readonly Color OrangeSkin = new Color(0.98f, 0.62f, 0.16f);
        private static readonly Color PearGreen = new Color(0.74f, 0.82f, 0.32f);
        private static readonly Color BananaYellow = new Color(0.98f, 0.86f, 0.30f);
        private static readonly Color CarrotOrange = new Color(0.96f, 0.50f, 0.16f);
        private static readonly Color TomatoRed = new Color(0.94f, 0.30f, 0.22f);
        private static readonly Color LeafGreen = new Color(0.36f, 0.66f, 0.30f);
        private static readonly Color StemBrown = new Color(0.44f, 0.30f, 0.18f);

        /// <summary>
        /// Materials are shared per colour: a fruit is three or four primitives and there are at
        /// most a handful on screen, but a fresh Material per primitive is a real cost on a phone
        /// and these colours never change.
        /// </summary>
        private readonly Dictionary<int, Material> _materials = new Dictionary<int, Material>();

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), 0.5f, 2.0f);
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

            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -12f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.045f, 0.13f);   // dark DJ-club purple
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            _camera = camera;
            FitCamera();

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.72f, 0.70f, 1f);
                light.intensity = 0.8f;
                light.transform.rotation = Quaternion.Euler(46f, -30f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.28f, 0.44f);

            _basket = BuildBasket();
            BuildDiscoScene();
        }

        private Transform _discoBall;
        private Transform _discoBeams;
        private readonly List<DiscoDot> _discoDots = new List<DiscoDot>();

        private class DiscoDot
        {
            public Transform T;
            public float BaseScale;
            public float Phase;
            public float PulseSpeed;
        }

        /// <summary>
        /// 蹦迪魔球灯: a spinning silver mirror ball near the top of the play area, throwing
        /// rotating coloured light beams plus a dense cloud of glowing, additive-blended light dots
        /// across the dark club background. Beams + more dots + additive glow is what makes it read
        /// as a real disco ("再绚丽些") rather than a few pasted lights.
        /// </summary>
        private void BuildDiscoScene()
        {
            if (_camera == null) return;
            float top = _camera.orthographicSize;
            float span = _camera.orthographicSize * _camera.aspect;

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "DiscoBall";
            ball.transform.SetParent(transform, false);
            ball.transform.position = new Vector3(0f, top - 0.9f, 1f);
            ball.transform.localScale = Vector3.one * 1.6f;
            var ballRenderer = ball.GetComponent<Renderer>();
            var ballMat = new Material(Shader.Find("Standard"));
            ballMat.mainTexture = FacetTexture();
            ballMat.color = new Color(0.88f, 0.88f, 0.95f);
            ballMat.SetFloat("_Metallic", 1f);
            ballMat.SetFloat("_Glossiness", 0.95f);
            ballRenderer.material = ballMat;
            ballRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ballRenderer.receiveShadows = false;
            Destroy(ball.GetComponent<Collider>());
            _discoBall = ball.transform;

            var palette = new[]
            {
                new Color(1f, 0.30f, 0.75f),   // pink
                new Color(0.25f, 0.85f, 1f),   // cyan
                new Color(0.65f, 0.35f, 1f),   // purple
                new Color(1f, 0.45f, 0.30f),   // orange
                new Color(0.35f, 1f, 0.75f),   // mint
                new Color(0.30f, 0.55f, 1f),   // blue
                new Color(1f, 0.85f, 0.25f),   // yellow
                new Color(1f, 0.40f, 0.45f),   // red
            };

            // Rotating light beams radiating from the ball — the classic disco-bar rays.
            var beamRing = new GameObject("BeamRing").transform;
            beamRing.SetParent(transform, false);
            beamRing.position = new Vector3(0f, top - 0.9f, 1.4f);
            _discoBeams = beamRing;

            int beams = 14;
            for (int i = 0; i < beams; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Beam" + i;
                go.transform.SetParent(beamRing, false);

                float angle = i / (float)beams * Mathf.PI * 2f;
                float length = 4.2f + (i % 3) * 1.3f;
                go.transform.localPosition = new Vector3(Mathf.Cos(angle) * length * 0.5f, Mathf.Sin(angle) * length * 0.5f, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                go.transform.localScale = new Vector3(0.16f + 0.10f * (i % 2), length, 1f);

                var renderer = go.GetComponent<Renderer>();
                var mat = NewDiscoMaterial(palette[i % palette.Length]);
                renderer.material = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Destroy(go.GetComponent<Collider>());
            }

            // Denser, brighter dots: additive blending + more of them + a wider size spread.
            int count = 34;
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "DiscoDot" + i;
                go.transform.SetParent(transform, false);

                float t = i / (float)count;
                float angle = t * Mathf.PI * 2f;
                float radius = 2.0f + (i % 4) * 1.25f + 0.5f * Mathf.Sin(i * 1.7f);
                float x = Mathf.Cos(angle) * radius * (span / Mathf.Max(1f, top) * 0.80f + 0.20f);
                float y = top - 1.1f + Mathf.Sin(angle) * radius;
                go.transform.position = new Vector3(x, y, 1.5f);

                float size = 0.26f + 0.55f * (i % 5) / 4f;
                go.transform.localScale = Vector3.one * size;

                var renderer = go.GetComponent<Renderer>();
                var mat = NewDiscoMaterial(palette[i % palette.Length]);
                renderer.material = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Destroy(go.GetComponent<Collider>());
                _discoDots.Add(new DiscoDot
                {
                    T = go.transform,
                    BaseScale = size,
                    Phase = i * 0.53f,
                    PulseSpeed = 1.0f + (i % 4) * 0.6f
                });
            }
        }

        /// <summary>An additive-blended soft glow material, so the lights bloom instead of sitting flat.</summary>
        private static Material NewDiscoMaterial(Color color)
        {
            var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.mainTexture = DshMobile.SoftShadow.Gradient;
            mat.color = color;
            // Behind the fruit: the disco lights are part of the backdrop.
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            return mat;
        }

        /// <summary>Spins the mirror ball, rotates the light beams and pulses the dots.</summary>
        private void TickDisco()
        {
            if (_discoBall != null) _discoBall.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            if (_discoBeams != null) _discoBeams.Rotate(0f, 0f, 18f * Time.deltaTime, Space.Self);

            for (int i = 0; i < _discoDots.Count; i++)
            {
                var dot = _discoDots[i];
                if (dot == null || dot.T == null) continue;
                float pulse = 1f + 0.30f * Mathf.Sin(Time.time * dot.PulseSpeed + dot.Phase);
                dot.T.localScale = Vector3.one * (dot.BaseScale * pulse);
            }
        }

        /// <summary>A grid of small mirror facets, so the ball reads as a disco ball, not a grey moon.</summary>
        private Texture2D FacetTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            const int grid = 8;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int gx = x / (size / grid);
                    int gy = y / (size / grid);
                    float shade = 0.75f + 0.5f * Mathf.PerlinNoise(gx * 1.9f, gy * 1.9f);
                    // Thin darker seams between facets.
                    bool seam = (x % (size / grid) == 0) || (y % (size / grid) == 0);
                    float grey = seam ? 0.35f : shade;
                    tex.SetPixel(x, y, new Color(grey, grey, grey, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        private Transform BuildBasket()
        {
            var root = new GameObject("Basket").transform;
            root.SetParent(transform, false);

            // 编筐果篮：底板 + 左右侧板 + 顶部一圈收口边（Kenney 家具包里没有篮子，就用手工程序化做细一点）。
            Color weave = new Color(0.72f, 0.52f, 0.30f);
            Color weaveLight = new Color(0.86f, 0.66f, 0.40f);

            var floor = Shape("Floor", PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                new Vector3(_settings.BasketHalfWidth * 2f, 0.16f, 0.9f), weave);
            floor.SetParent(root, false);

            var left = Shape("Left", PrimitiveType.Cube, new Vector3(-_settings.BasketHalfWidth, 0.28f, 0f),
                new Vector3(0.13f, 0.60f, 0.9f), weave);
            left.SetParent(root, false);

            var right = Shape("Right", PrimitiveType.Cube, new Vector3(_settings.BasketHalfWidth, 0.28f, 0f),
                new Vector3(0.13f, 0.60f, 0.9f), weave);
            right.SetParent(root, false);

            var rim = Shape("Rim", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0f),
                new Vector3(_settings.BasketHalfWidth * 2f + 0.20f, 0.09f, 0.98f), weaveLight);
            rim.SetParent(root, false);

            return root;
        }

        private Transform Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.3f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        // ------------------------------------------------------------------ run control

        public void ResetRun()
        {
            for (int i = 0; i < _fruits.Count; i++)
            {
                if (_fruits[i].Transform != null) Destroy(_fruits[i].Transform.gameObject);
            }
            _fruits.Clear();

            Caught = 0;
            Missed = 0;
            Lives = CatchRules.StartLives;
            _nextSpawn = 0.7f;
            _basketX = 0f;
            _fruitIndex = 0;      // every run opens with an apple
            State = Phase.Ready;

            if (_basket != null) _basket.position = new Vector3(0f, -_settings.SpawnHeight + 1.4f, 0f);
            DshMobile.MobileHaptics.Light();
        }

        public void StartRun()
        {
            if (State == Phase.Playing) return;
            State = Phase.Playing;
        }

        /// <summary>Left/right input, pushed by the keyboard (the phone steers by dragging).</summary>
        public float MoveInput { get; set; }

        /// <summary>
        /// Moves the basket by a drag, in world units. This is the phone's control.
        ///
        /// It replaces the two hold-to-move pads, which were the wrong shape for this game: a pad
        /// is a *digital* control (left, right, or nothing) bolted onto an *analog* play area, so
        /// catching a fruit that landed 1.5 units away meant holding right, overshooting, tapping
        /// the other pad, and arriving late — and on a phone it was the right-hand pad that got
        /// missed, which is the report that produced this. Dragging paints the finger onto the
        /// world instead: the basket goes exactly where the thumb goes, and the whole screen is
        /// the control rather than two circles in the corners.
        /// </summary>
        public void DragBy(float worldDelta)
        {
            if (State == Phase.Dead) return;
            if (Mathf.Abs(worldDelta) <= 0.0001f) return;

            if (State == Phase.Ready) StartRun();

            _basketX = CatchRules.DragTo(_basketX, worldDelta * Sensitivity, _settings);
            ApplyBasket();
        }

        private void Update()
        {
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();
            TickDisco();

            float keyboard = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboard -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboard += 1f;

            float move = Mathf.Abs(keyboard) > 0.01f ? keyboard : MoveInput;
            if (Mathf.Abs(move) > 0.01f && State == Phase.Ready) StartRun();

            if (State == Phase.Dead)
            {
                if (Input.GetMouseButtonDown(0) && Time.time - _deathAt > 0.8f) ResetRun();
                return;
            }

            if (State == Phase.Playing) Tick(Time.deltaTime, move);
            ApplyBasket();
        }

        /// <summary>
        /// One frame of play. Public so a test (or an autopilot) can drive a whole run without
        /// waiting for real time.
        /// </summary>
        public void Tick(float dt, float move)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);
            _basketX = CatchRules.StepBasket(_basketX, move, step, _settings);

            _nextSpawn -= step;
            if (_nextSpawn <= 0f)
            {
                Spawn();
                _nextSpawn = CatchRules.SpawnSecondsFor(Caught, _settings);
            }

            float fall = CatchRules.FallSpeedFor(Caught, _settings);

            for (int i = _fruits.Count - 1; i >= 0; i--)
            {
                var fruit = _fruits[i];
                if (fruit.Transform == null)
                {
                    _fruits.RemoveAt(i);
                    continue;
                }

                fruit.Y -= fall * step;
                fruit.Transform.position = new Vector3(fruit.X, fruit.Y, 0f);

                if (!fruit.Counted && fruit.Y <= -_settings.SpawnHeight + 1.7f)
                {
                    fruit.Counted = true;

                    if (CatchRules.Catches(_basketX, fruit.X, _settings))
                    {
                        if (fruit.IsBomb)
                        {
                            // Catching a bomb ends the run immediately — that is the whole point of
                            // dodging it.
                            Die();
                            return;
                        }
                        Caught++;
                        if (Caught > Best) Best = Caught;
                        DshMobile.MobileHaptics.Light();
                    }
                    else if (!fruit.IsBomb)
                    {
                        Missed++;
                        Lives--;
                        DshMobile.MobileHaptics.Medium();
                        if (Lives <= 0)
                        {
                            Die();
                            return;
                        }
                    }

                    Destroy(fruit.Transform.gameObject);
                    _fruits.RemoveAt(i);
                    continue;
                }

                if (fruit.Y < -_settings.SpawnHeight - 1f)
                {
                    Destroy(fruit.Transform.gameObject);
                    _fruits.RemoveAt(i);
                }
            }
        }

        private void Spawn()
        {
            bool isBomb = CatchRules.RollBomb((float)_rng.NextDouble(), Caught);
            float x = CatchRules.SpawnX((float)_rng.NextDouble(), _basketX, _settings);

            if (isBomb)
            {
                // Keep the bomb away from the last falling thing so the two can't be confused.
                if (_fruits.Count > 0)
                {
                    float last = _fruits[_fruits.Count - 1].X;
                    int guard = 0;
                    while (Mathf.Abs(x - last) < CatchRules.MinBombFruitGap && guard++ < 8)
                        x = CatchRules.SpawnX((float)_rng.NextDouble(), _basketX, _settings);
                }

                var root = new GameObject("Bomb").transform;
                root.SetParent(transform, false);
                root.position = new Vector3(x, _settings.SpawnHeight, 0f);
                BuildBomb(root, _settings.FruitHalfWidth);
                _fruits.Add(new Fruit { Transform = root, X = x, Y = _settings.SpawnHeight, IsBomb = true });
                return;
            }

            var kind = CatchRules.FruitFor(_fruitIndex++);

            var fruitRoot = new GameObject("Fruit_" + CatchRules.FruitName(kind)).transform;
            fruitRoot.SetParent(transform, false);
            fruitRoot.position = new Vector3(x, _settings.SpawnHeight, 0f);

            BuildFruit(fruitRoot, kind, _settings.FruitHalfWidth);

            _fruits.Add(new Fruit
            {
                Transform = fruitRoot,
                X = x,
                Y = _settings.SpawnHeight
            });
        }

        /// <summary>A round black bomb with a lit fuse, so it reads as danger at a glance.</summary>
        private void BuildBomb(Transform root, float half)
        {
            Ball(root, "BombBody", Vector3.zero, new Vector3(1f, 1.05f, 1f) * half * 2f, new Color(0.13f, 0.12f, 0.15f));
            Stick(root, "Fuse", new Vector3(0f, half * 1.05f, 0f), new Vector3(0.06f, half * 0.55f, 0.06f), StemBrown, 16f);
            Ball(root, "Spark", new Vector3(0f, half * 1.42f, 0f),
                new Vector3(half * 0.5f, half * 0.4f, half * 0.5f), new Color(1f, 0.82f, 0.25f));
            Ball(root, "Warning", new Vector3(0f, 0f, -half * 0.9f),
                new Vector3(half * 1.1f, half * 0.5f, half * 0.2f), new Color(1f, 0.92f, 0.4f));
        }

        /// <summary>
        /// One fruit or vegetable, out of primitives.
        ///
        /// They used to be plain spheres in five colours, which the report from the phone described
        /// exactly: "怎么是小球". A ball is not a fruit — the shape *is* the information here, and a
        /// falling carrot reads as food at a glance where a purple sphere reads as a bullet.
        ///
        /// Every fruit is built around the same half-width, so the catch rule (which is arithmetic
        /// in <see cref="CatchRules"/>) keeps agreeing with what the player sees.
        /// </summary>
        private void BuildFruit(Transform root, FruitKind kind, float half)
        {
            switch (kind)
            {
                case FruitKind.Apple:
                    Ball(root, "Apple", new Vector3(0f, 0f, 0f), new Vector3(1f, 0.94f, 1f) * half * 2f, AppleRed);
                    Stick(root, "Stem", new Vector3(0f, half * 1.05f, 0f), new Vector3(0.06f, half * 0.5f, 0.06f), StemBrown, 12f);
                    Ball(root, "Leaf", new Vector3(half * 0.42f, half * 1.12f, 0f),
                        new Vector3(half * 0.9f, half * 0.16f, half * 0.5f), LeafGreen);
                    break;

                case FruitKind.Orange:
                    Ball(root, "Orange", Vector3.zero, Vector3.one * half * 2f, OrangeSkin);
                    Ball(root, "Navel", new Vector3(0f, -half * 0.92f, 0f),
                        new Vector3(half * 0.5f, half * 0.2f, half * 0.5f), new Color(0.86f, 0.48f, 0.10f));
                    Ball(root, "Leaf", new Vector3(half * 0.34f, half * 0.98f, 0f),
                        new Vector3(half * 0.7f, half * 0.14f, half * 0.4f), LeafGreen);
                    break;

                case FruitKind.Pear:
                    // A pear is two spheres: a small one sitting in a big one.
                    Ball(root, "PearBody", new Vector3(0f, -half * 0.28f, 0f),
                        new Vector3(half * 1.7f, half * 1.5f, half * 1.7f), PearGreen);
                    Ball(root, "PearTop", new Vector3(0f, half * 0.62f, 0f),
                        new Vector3(half * 1.1f, half * 1.1f, half * 1.1f), PearGreen);
                    Stick(root, "Stem", new Vector3(0f, half * 1.3f, 0f), new Vector3(0.05f, half * 0.4f, 0.05f), StemBrown, 8f);
                    break;

                case FruitKind.Banana:
                    // A banana is an arc of four overlapping spheres, which at this size is
                    // indistinguishable from a curved mesh and costs nothing to build.
                    for (int i = 0; i < 4; i++)
                    {
                        float t = i / 3f;
                        float bx = Mathf.Lerp(-half * 0.75f, half * 0.75f, t);
                        float by = Mathf.Sin(t * Mathf.PI) * half * 0.45f;
                        Ball(root, "Banana" + i, new Vector3(bx, by, 0f),
                            new Vector3(half * 0.9f, half * 0.8f, half * 0.8f),
                            Color.Lerp(BananaYellow, new Color(0.86f, 0.70f, 0.22f), t));
                    }
                    Ball(root, "Tip", new Vector3(half * 0.95f, half * 0.1f, 0f),
                        Vector3.one * half * 0.34f, StemBrown);
                    break;

                case FruitKind.Carrot:
                    // A cone pointing down: the one shape in this list that says "vegetable".
                    var body = Primitive(root, "Carrot", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f),
                        new Vector3(half * 0.95f, half * 1.5f, half * 0.95f), CarrotOrange);
                    body.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    Ball(root, "Tip", new Vector3(0f, -half * 1.5f, 0f), Vector3.one * half * 0.4f, CarrotOrange);
                    for (int i = 0; i < 3; i++)
                    {
                        var leaf = Primitive(root, "Leaf" + i, PrimitiveType.Cube,
                            new Vector3(0f, half * 1.75f, 0f),
                            new Vector3(half * 0.22f, half * 1.1f, half * 0.22f), LeafGreen);
                        leaf.localRotation = Quaternion.Euler(0f, 0f, -28f + i * 28f);
                    }
                    break;

                default: // Tomato: a squashed red ball with a green star on top.
                    Ball(root, "Tomato", Vector3.zero, new Vector3(half * 2f, half * 1.7f, half * 2f), TomatoRed);
                    for (int i = 0; i < 5; i++)
                    {
                        var calyx = Primitive(root, "Calyx" + i, PrimitiveType.Cube,
                            new Vector3(0f, half * 0.9f, 0f),
                            new Vector3(half * 0.5f, half * 0.08f, half * 0.2f), LeafGreen);
                        calyx.localRotation = Quaternion.Euler(0f, i * 72f, 0f);
                    }
                    break;
            }
        }

        private void Ball(Transform root, string name, Vector3 position, Vector3 scale, Color color)
            => Primitive(root, name, PrimitiveType.Sphere, position, scale, color);

        private void Stick(Transform root, string name, Vector3 position, Vector3 scale, Color color,
            float tilt)
        {
            var stick = Primitive(root, name, PrimitiveType.Cylinder, position, scale, color);
            stick.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private Transform Primitive(Transform root, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = MaterialFor(color);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        private Material MaterialFor(Color color)
        {
            int key = ((int)(color.r * 255f) << 16) | ((int)(color.g * 255f) << 8) | (int)(color.b * 255f);
            Material material;
            if (_materials.TryGetValue(key, out material) && material != null) return material;

            material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.45f);
            _materials[key] = material;
            return material;
        }

        private void Die()
        {
            State = Phase.Dead;
            _deathAt = Time.time;
            DshMobile.MobileHaptics.Heavy();

            int coins = CatchRules.CoinsFor(Caught);
            if (coins > 0) DshMobile.PetWallet.Add(coins);
        }

        public int RunCoins => CatchRules.CoinsFor(Caught);

        private void ApplyBasket()
        {
            if (_basket == null) return;
            _basket.position = new Vector3(_basketX, -_settings.SpawnHeight + 1.4f, 0f);
        }

        private void FitCamera()
        {
            if (_camera == null) return;
            _lastAspect = _camera.aspect;

            // The play area is wider than it is tall, so the constraint is the width — and a
            // portrait phone has to be given it, or half the fruit falls off the screen.
            float byWidth = (_settings.HalfWidth + 0.6f) / Mathf.Max(0.2f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(6.4f, byWidth);
        }

        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the catch game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
