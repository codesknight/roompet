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
        }

        private Transform _basket;
        private Camera _camera;
        private float _lastAspect;
        private float _nextSpawn;
        private float _deathAt;
        private float _basketX;

        private static readonly Color[] FruitColours =
        {
            new Color(0.92f, 0.34f, 0.34f),   // apple
            new Color(0.98f, 0.72f, 0.26f),   // orange
            new Color(0.72f, 0.52f, 0.86f),   // plum
            new Color(0.44f, 0.78f, 0.46f),   // pear
            new Color(0.95f, 0.52f, 0.68f)    // peach
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

            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -12f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.62f, 0.42f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            _camera = camera;
            FitCamera();

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.05f;
                light.transform.rotation = Quaternion.Euler(46f, -30f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.54f, 0.58f, 0.6f);

            _basket = BuildBasket();
        }

        private Transform BuildBasket()
        {
            var root = new GameObject("Basket").transform;
            root.SetParent(transform, false);

            // A woven basket: a box floor, two sloping sides, and a rim. Primitives only.
            var floor = Shape("Floor", PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                new Vector3(_settings.BasketHalfWidth * 2f, 0.18f, 0.9f), new Color(0.72f, 0.52f, 0.30f));
            floor.SetParent(root, false);

            var left = Shape("Left", PrimitiveType.Cube, new Vector3(-_settings.BasketHalfWidth, 0.30f, 0f),
                new Vector3(0.14f, 0.7f, 0.9f), new Color(0.82f, 0.62f, 0.36f));
            left.SetParent(root, false);

            var right = Shape("Right", PrimitiveType.Cube, new Vector3(_settings.BasketHalfWidth, 0.30f, 0f),
                new Vector3(0.14f, 0.7f, 0.9f), new Color(0.82f, 0.62f, 0.36f));
            right.SetParent(root, false);

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
            State = Phase.Ready;

            if (_basket != null) _basket.position = new Vector3(0f, -_settings.SpawnHeight + 1.4f, 0f);
            DshMobile.MobileHaptics.Light();
        }

        public void StartRun()
        {
            if (State == Phase.Playing) return;
            State = Phase.Playing;
        }

        /// <summary>Left/right input, pushed by the HUD's pads or the keyboard.</summary>
        public float MoveInput { get; set; }

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
                        Caught++;
                        if (Caught > Best) Best = Caught;
                        DshMobile.MobileHaptics.Light();
                    }
                    else
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
            float x = CatchRules.SpawnX((float)_rng.NextDouble(), _basketX, _settings);
            var colour = FruitColours[_rng.Next(FruitColours.Length)];

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fruit";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (_settings.FruitHalfWidth * 2f);
            go.transform.position = new Vector3(x, _settings.SpawnHeight, 0f);

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = colour;
            material.SetFloat("_Glossiness", 0.5f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Destroy(go.GetComponent<Collider>());

            _fruits.Add(new Fruit
            {
                Transform = go.transform,
                X = x,
                Y = _settings.SpawnHeight
            });
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
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
