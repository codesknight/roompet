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
            BuildDogHead();
        }

        /// <summary>
        /// The roompet's dog head as a glowing backdrop, replacing the plain neon light beams.
        /// It is a flat, unlit silhouette drawn onto a transparent quad behind the play area, so it
        /// reads as a "background pattern" rather than a game object that could be mistaken for fruit.
        /// </summary>
        private void BuildDogHead()
        {
            if (_camera == null) return;
            float height = _camera.orthographicSize * 2f;
            float width = height * _camera.aspect;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DogHeadBackdrop";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 3f);
            quad.transform.localScale = new Vector3(width * 0.72f, height * 0.72f, 1f);

            var renderer = quad.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard"));
            material.mainTexture = DogHeadTexture();
            material.color = Color.white;
            // Background queue: it renders before the fruit so the fruit always occludes it.
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Destroy(quad.GetComponent<Collider>());
        }

        /// <summary>
        /// A small dog head (rounded head + two triangular ears + two eyes), drawn into a soft neon
        /// glow. The silhouette is rasterised to a mask and then box-blurred a few times, which gives
        /// the "炫光" halo without needing a second render pass.
        /// </summary>
        private Texture2D DogHeadTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            var mask = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;

                    bool head = InCircle(u, v, 0.5f, 0.44f, 0.24f);
                    bool earL = InTriangle(u, v, 0.34f, 0.52f, 0.20f, 0.78f, 0.52f, 0.60f);
                    bool earR = InTriangle(u, v, 0.66f, 0.52f, 0.80f, 0.78f, 0.48f, 0.60f);
                    if (head || earL || earR) mask[y * size + x] = 1f;
                }
            }

            Blur(mask, size, 3);
            Blur(mask, size, 3);

            var glow = new Color(0.30f, 0.86f, 1f, 1f);   // cyan neon
            var eye = new Color(0.05f, 0.08f, 0.12f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float a = Mathf.Clamp01(mask[y * size + x]);

                    // Darken the two eyes back in over the glow so the head still reads as a dog.
                    if (InCircle(u, v, 0.42f, 0.46f, 0.035f) || InCircle(u, v, 0.58f, 0.46f, 0.035f))
                    {
                        tex.SetPixel(x, y, new Color(eye.r, eye.g, eye.b, Mathf.Max(a, 0.9f)));
                        continue;
                    }

                    tex.SetPixel(x, y, new Color(glow.r, glow.g, glow.b, a * 0.75f));
                }
            }

            tex.Apply();
            return tex;
        }

        private static bool InCircle(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx, dy = v - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool InTriangle(float u, float v, float ax, float ay, float bx, float by,
            float cx, float cy)
        {
            // Barycentric sign test against a CCW triangle.
            float d1 = Sign(u, v, ax, ay, bx, by);
            float d2 = Sign(u, v, bx, by, cx, cy);
            float d3 = Sign(u, v, cx, cy, ax, ay);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Sign(float px, float py, float ax, float ay, float bx, float by)
            => (px - bx) * (ay - by) - (ax - bx) * (py - by);

        /// <summary>Box blur in place. A few passes of a 3x3 kernel turn a hard mask into a halo.</summary>
        private static void Blur(float[] m, int size, int passes)
        {
            var src = m;
            var dst = new float[size * size];
            for (int p = 0; p < passes; p++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float sum = 0f;
                        int count = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                                sum += src[ny * size + nx];
                                count++;
                            }
                        }
                        dst[y * size + x] = sum / count;
                    }
                }
                var tmp = src;
                src = dst;
                dst = tmp;
            }
            if (src != m) System.Array.Copy(src, m, m.Length);
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
