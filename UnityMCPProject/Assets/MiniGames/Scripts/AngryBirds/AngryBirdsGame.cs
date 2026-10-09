using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 愤怒的小鸟: pull the slingshot back, let go, and knock the pigs out of their tower.
    ///
    /// The important structural decision is that <b>this class does not decide what happens</b>. The
    /// shot is handed to <see cref="BirdRules.Simulate"/>, which returns the bird's path and the
    /// ordered list of what broke; this class then *replays* that list — moving the bird along the
    /// recorded path, popping blocks and pigs at their recorded times. The generator proved the level
    /// clearable with that same function, so what the player sees is what was verified. There is no
    /// physics engine, no rigid body, and no way for the animation to disagree with the outcome.
    ///
    /// Input is one drag: touch (or hold the mouse) anywhere and pull away from the slingshot.
    /// </summary>
    public class AngryBirdsGame : MonoBehaviour
    {
        public enum Phase { Ready, Aiming, Flying, Cleared, Failed }

        public Phase State { get; private set; } = Phase.Ready;
        public int Stage { get; private set; } = 1;
        public int Score { get; private set; }
        public int Best { get; private set; }
        public int BirdsLeft { get; private set; }

        /// <summary>Set by the HUD when the player asks for a hint: the next proven shot.</summary>
        public bool HintRequested { get; set; }

        private BirdSettings _settings = BirdSettings.Default;
        private BirdLevel _level;
        private Transform _world;
        private Transform _bird;
        private Transform _slingRoot;
        private Camera _camera;
        private float _lastAspect;

        private readonly List<Transform> _blockViews = new List<Transform>();
        private readonly List<Transform> _pigViews = new List<Transform>();

        private Vector2 _dragWorld;
        private bool _dragging;

        // Playback of the last shot.
        private BirdShotResult _shot;
        private float _shotStarted;
        private int _nextEvent;
        private bool _levelScored;
        private float _settleAt;

        /// <summary>The full level, for the HUD.</summary>
        public BirdLevel Level => _level;

        /// <summary>Whether a hint is available for this level.</summary>
        public bool HasHint => _level != null && _level.Solution != null && _level.Solution.Count > 0;

        public string HintText => HasHint
            ? "这一关的验证过的一发：角度 " + Mathf.RoundToInt(Mathf.Atan2(_level.Solution[0].y, _level.Solution[0].x) * Mathf.Rad2Deg) +
              "°，力度 " + Mathf.RoundToInt(_level.Solution[0].magnitude / _settings.MaxLaunchSpeed * 100f) + "%"
            : "";

        public bool HintVisible { get; private set; }
        private float _hintUntil;

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            LoadStage(Stage);
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
            camera.transform.position = new Vector3(0f, 0f, -14f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.68f, 0.86f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            _camera = camera;
            FitCamera();

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.86f);
                light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.62f);

            _world = new GameObject("Level").transform;
            _world.SetParent(transform, false);
            BuildGround();
            BuildSling();
        }

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(40f, 1f, 4f);
            ground.transform.position = new Vector3(2f, _settings.GroundY - 0.5f, 0f);
            Paint(ground, new Color(0.42f, 0.68f, 0.34f));
            Destroy(ground.GetComponent<Collider>());
        }

        private void BuildSling()
        {
            _slingRoot = new GameObject("Sling").transform;
            _slingRoot.SetParent(transform, false);

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(_slingRoot, false);
            post.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
            post.transform.localPosition = new Vector3(_settings.SlingX, _settings.GroundY + 0.9f, 0.2f);
            Paint(post, new Color(0.52f, 0.36f, 0.22f));
            Destroy(post.GetComponent<Collider>());

            var fork = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fork.name = "Fork";
            fork.transform.SetParent(_slingRoot, false);
            fork.transform.localScale = new Vector3(0.5f, 0.14f, 0.2f);
            fork.transform.localPosition = new Vector3(_settings.SlingX, _settings.GroundY + 1.8f, 0.2f);
            Paint(fork, new Color(0.52f, 0.36f, 0.22f));
            Destroy(fork.GetComponent<Collider>());
        }

        /// <summary>
        /// Frames the *level*, not a fixed width.
        ///
        /// The first version asked for a fixed 7.4 units of half-width, which on a portrait phone
        /// (aspect 0.45) needed an orthographic size of 16 and put the slingshot — the thing the player
        /// aims with — off the left edge of the screen. Fitting the scene's own extent keeps the sling,
        /// the structure and the ground in view whatever shape the screen is.
        /// </summary>
        private void FitCamera()
        {
            if (_camera == null) return;
            _lastAspect = _camera.aspect;

            float ground = _level != null ? _level.GroundY : _settings.GroundY;
            float slingX = _level != null ? _level.SlingX : _settings.SlingX;

            float left = slingX - 1.4f;
            float right = slingX + 3f;

            if (_level != null)
            {
                for (int i = 0; i < _level.Blocks.Count; i++)
                {
                    right = Mathf.Max(right, _level.Blocks[i].Right + 2.2f);
                }
            }

            float top = ground + 2f;
            if (_level != null)
            {
                for (int i = 0; i < _level.Blocks.Count; i++)
                {
                    top = Mathf.Max(top, _level.Blocks[i].Top + 2.2f);
                }
                for (int i = 0; i < _level.Pigs.Count; i++)
                {
                    top = Mathf.Max(top, _level.Pigs[i].Y + 2f);
                }
            }

            float bottom = ground - 2f;
            float halfWidth = (right - left) * 0.5f;
            float halfHeight = (top - bottom) * 0.5f;

            float byWidth = halfWidth / Mathf.Max(0.25f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(4.6f, Mathf.Max(byWidth, halfHeight));
            _camera.transform.position = new Vector3((left + right) * 0.5f, (top + bottom) * 0.5f, -14f);
        }

        // ------------------------------------------------------------------ stages

        /// <summary>Builds a fresh, verified level for a stage.</summary>
        public void LoadStage(int stage)
        {
            Stage = Mathf.Clamp(stage, 1, BirdLevels.StageCount);
            int seed = 20250607 + Stage * 977;

            _level = BirdLevels.Generate(Stage, seed, _settings);
            BirdsLeft = _level.Birds;
            _levelScored = false;
            _shot = null;
            State = Phase.Ready;
            Rebuild();
            FitCamera();
        }

        public void RestartStage()
        {
            BirdsLeft = _level.Birds;
            for (int i = 0; i < _level.Pigs.Count; i++)
            {
                var pig = _level.Pigs[i];
                pig.Alive = true;
                _level.Pigs[i] = pig;
            }

            for (int i = 0; i < _level.Blocks.Count; i++)
            {
                var block = _level.Blocks[i];
                block.Alive = true;
                block.Health = BirdRules.HealthFor(block.Kind);
                block.Falling = false;
                _level.Blocks[i] = block;
            }

            _levelScored = false;
            _shot = null;
            State = Phase.Ready;
            Rebuild();
        }

        /// <summary>Rebuilds the view from the level data. The data is the only source of truth.</summary>
        private void Rebuild()
        {
            for (int i = _world.childCount - 1; i >= 0; i--)
            {
                var child = _world.GetChild(i);
                if (child.name == "Ground" || child.name == "Sling") continue;
                Destroy(child.gameObject);
            }

            _blockViews.Clear();
            _pigViews.Clear();

            for (int i = 0; i < _level.Blocks.Count; i++)
            {
                var block = _level.Blocks[i];
                var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                view.name = "Block" + i;
                view.transform.SetParent(_world, false);
                view.transform.localScale = new Vector3(block.HalfW * 2f, block.HalfH * 2f, 1.1f);
                view.transform.position = new Vector3(block.X, block.Y, 0f);
                Paint(view, ColourFor(block.Kind));
                Destroy(view.GetComponent<Collider>());
                _blockViews.Add(view.transform);
            }

            for (int i = 0; i < _level.Pigs.Count; i++)
            {
                var pig = _level.Pigs[i];
                var view = new GameObject("Pig" + i).transform;
                view.SetParent(_world, false);
                view.position = new Vector3(pig.X, pig.Y, 0f);
                BuildPig(view, pig.Radius);
                _pigViews.Add(view);
            }

            BuildBird();
            FitCamera();
        }

        private static Color ColourFor(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Ice: return new Color(0.62f, 0.86f, 0.95f);
                case BlockKind.Stone: return new Color(0.56f, 0.56f, 0.58f);
                default: return new Color(0.76f, 0.56f, 0.32f);
            }
        }

        private void BuildPig(Transform root, float radius)
        {
            AddPart(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * radius * 2f,
                new Color(0.52f, 0.78f, 0.42f));
            AddPart(root, PrimitiveType.Sphere, new Vector3(0f, -radius * 0.1f, -radius * 0.8f),
                new Vector3(radius * 0.8f, radius * 0.6f, radius * 0.5f), new Color(0.62f, 0.86f, 0.5f), "Snout");
            AddPart(root, PrimitiveType.Sphere, new Vector3(-radius * 0.34f, radius * 0.3f, -radius * 0.8f),
                Vector3.one * radius * 0.26f, Color.white, "EyeL");
            AddPart(root, PrimitiveType.Sphere, new Vector3(radius * 0.34f, radius * 0.3f, -radius * 0.8f),
                Vector3.one * radius * 0.26f, Color.white, "EyeR");
            AddPart(root, PrimitiveType.Sphere, new Vector3(-radius * 0.6f, radius * 0.75f, 0f),
                Vector3.one * radius * 0.4f, new Color(0.44f, 0.7f, 0.36f), "EarL");
            AddPart(root, PrimitiveType.Sphere, new Vector3(radius * 0.6f, radius * 0.75f, 0f),
                Vector3.one * radius * 0.4f, new Color(0.44f, 0.7f, 0.36f), "EarR");
        }

        private void BuildBird()
        {
            if (_bird != null) Destroy(_bird.gameObject);

            _bird = new GameObject("Bird").transform;
            _bird.SetParent(transform, false);
            _bird.position = SlingPosition;

            AddPart(_bird, PrimitiveType.Sphere, Vector3.zero, Vector3.one * _settings.BirdRadius * 2f,
                new Color(0.92f, 0.28f, 0.24f));
            AddPart(_bird, PrimitiveType.Cube, new Vector3(_settings.BirdRadius * 1.05f, 0f, 0f),
                new Vector3(_settings.BirdRadius * 0.7f, _settings.BirdRadius * 0.4f, _settings.BirdRadius * 0.5f),
                new Color(0.98f, 0.78f, 0.24f), "Beak");
            AddPart(_bird, PrimitiveType.Sphere, new Vector3(_settings.BirdRadius * 0.35f,
                    _settings.BirdRadius * 0.35f, -_settings.BirdRadius * 0.8f),
                Vector3.one * _settings.BirdRadius * 0.34f, Color.white, "Eye");
        }

        private void AddPart(Transform root, PrimitiveType type, Vector3 position, Vector3 scale, Color colour,
            string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name ?? "Part";
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Paint(go, colour);
            Destroy(go.GetComponent<Collider>());
        }

        private static void Paint(GameObject go, Color colour)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var material = new Material(Shader.Find("Standard"));
            material.color = colour;
            material.SetFloat("_Glossiness", 0.3f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Vector2 SlingPosition => new Vector2(_level.SlingX, _level.SlingY);

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();

            // The hint is a real, verified shot: showing it is showing the proof the level can be
            // cleared, which is a better tutorial than any text.
            if (HintRequested)
            {
                HintRequested = false;
                HintVisible = HasHint;
                _hintUntil = Time.time + 4f;
            }
            if (HintVisible && Time.time > _hintUntil) HintVisible = false;

            switch (State)
            {
                case Phase.Flying:
                    Playback();
                    break;

                case Phase.Cleared:
                case Phase.Failed:
                    if (Time.time > _settleAt + 0.6f && Input.GetMouseButtonDown(0))
                    {
                        // A tap after the result moves on, but only once the result is readable.
                        _settleAt = float.MaxValue;
                    }
                    break;

                default:
                    Aim();
                    break;
            }
        }

        private Vector2 _dragStart;
        private bool _hasDrag;

        /// <summary>
        /// One drag to aim. The pull is clamped by the rules, so a wild drag cannot produce a shot
        /// the solver never considered.
        /// </summary>
        private void Aim()
        {
            if (State != Phase.Ready && State != Phase.Aiming) return;
            if (_bird == null) return;
            if (!DshMobile.MobileTouch.PlayInputEnabled) return;

            Vector2? pointer = PointerWorld();
            if (pointer == null)
            {
                if (_dragging)
                {
                    _dragging = false;
                    if (BirdRules.CanLaunch(SlingPosition, _dragWorld, _settings)) Launch();
                    else ResetBird();
                }
                return;
            }

            if (!_hasDrag)
            {
                _hasDrag = true;
                _dragStart = pointer.Value;
            }

            _dragWorld = pointer.Value;
            _dragging = true;
            State = Phase.Aiming;

            // The bird follows the pull, up to the sling's reach.
            Vector2 pull = _dragWorld - SlingPosition;
            if (pull.magnitude > _settings.MaxPull) pull = pull.normalized * _settings.MaxPull;
            _bird.position = SlingPosition + pull;
        }

        private void ResetBird()
        {
            State = Phase.Ready;
            if (_bird != null) _bird.position = SlingPosition;
        }

        private Vector2? PointerWorld()
        {
            var gesture = DshMobile.MobileTouch.Gesture;
            if (gesture.IsActive) return WorldPoint(gesture.Position);
            if (Input.GetMouseButton(0))
                return WorldPoint(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            return null;
        }

        /// <summary>Screen (top-down, as the touch layer reports it) → world.</summary>
        public Vector2 WorldPoint(Vector2 topDownScreen)
        {
            if (_camera == null) return topDownScreen;
            var world = _camera.ScreenToWorldPoint(
                new Vector3(topDownScreen.x, Screen.height - topDownScreen.y, -_camera.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        private void Launch()
        {
            Vector2 velocity = BirdRules.LaunchVelocity(SlingPosition, _dragWorld, _settings);

            // The whole shot is decided here, by the rules, and then only replayed.
            _shot = BirdRules.Simulate(_level, velocity, _settings);
            _shotStarted = Time.time;
            _nextEvent = 0;
            _dragging = false;
            _hasDrag = false;
            BirdsLeft--;

            State = Phase.Flying;
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>Moves the view along the recorded path and fires the recorded events on time.</summary>
        private void Playback()
        {
            if (_shot == null)
            {
                State = Phase.Ready;
                return;
            }

            float elapsed = Time.time - _shotStarted;

            // The path is sampled at the rules' fixed step, so the index is exact rather than guessed.
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / _settings.Step), 0, _shot.Path.Count - 1);
            if (_bird != null && _shot.Path.Count > 0)
            {
                var point = _shot.Path[index];
                var previous = _shot.Path[Mathf.Max(0, index - 1)];
                _bird.position = new Vector3(point.x, point.y, 0f);
                var direction = point - previous;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    _bird.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }
            }

            while (_nextEvent < _shot.Events.Count && _shot.Events[_nextEvent].Time <= elapsed)
            {
                Apply(_shot.Events[_nextEvent]);
                _nextEvent++;
            }

            if (elapsed < _shot.EndTime) return;

            _shot = null;

            if (_level.Cleared)
            {
                FinishCleared();
                return;
            }

            if (BirdsLeft <= 0)
            {
                State = Phase.Failed;
                _settleAt = Time.time;
                DshMobile.MobileHaptics.Heavy();
                return;
            }

            ResetBird();
        }

        private void Apply(BirdEvent e)
        {
            switch (e.Kind)
            {
                case BirdEventKind.BlockBroken:
                    HideView(_blockViews, e.Index);
                    Score += BirdRules.ScoreFor(_level.Blocks[e.Index].Kind);
                    DshMobile.MobileHaptics.Light();
                    break;

                case BirdEventKind.PigKilled:
                    HideView(_pigViews, e.Index);
                    Score += BirdRules.PigScore;
                    DshMobile.MobileHaptics.Medium();
                    break;

                case BirdEventKind.BlockFell:
                    // The rules already moved the block; the view only has to follow it.
                    Follow(_blockViews, e.Index);
                    break;

                case BirdEventKind.BirdStopped:
                    break;
            }
        }

        private static void HideView(List<Transform> views, int index)
        {
            if (index < 0 || index >= views.Count) return;
            var view = views[index];
            if (view == null) return;
            view.gameObject.SetActive(false);
        }

        private void Follow(List<Transform> views, int index)
        {
            if (index < 0 || index >= views.Count) return;
            var view = views[index];
            if (view == null) return;
            var block = _level.Blocks[index];
            view.position = new Vector3(block.X, block.Y, 0f);
        }

        /// <summary>
        /// Keeps falling blocks glued to the data every frame.
        ///
        /// The falling positions are part of the simulated path, not of the events, so the view reads
        /// them from the level each frame rather than interpolating — the level *is* the animation.
        /// </summary>
        private void LateUpdate()
        {
            if (_shot == null) return;

            for (int i = 0; i < _blockViews.Count; i++)
            {
                if (i >= _level.Blocks.Count) break;
                var block = _level.Blocks[i];
                if (!block.Falling || !block.Alive) continue;
                Follow(_blockViews, i);
            }
        }

        private void FinishCleared()
        {
            State = Phase.Cleared;
            _settleAt = Time.time;

            if (!_levelScored)
            {
                _levelScored = true;
                int coins = BirdRules.CoinsFor(_level, BirdsLeft, Score);
                if (coins > 0) DshMobile.PetWallet.Add(coins);
                if (Score > Best) Best = Score;
            }

            DshMobile.MobileHaptics.Light();
        }

        public int RunCoins => BirdRules.CoinsFor(_level, BirdsLeft, Score);

        public bool HasNextStage => Stage < BirdLevels.StageCount;

        public void NextStage()
        {
            if (!HasNextStage) return;
            LoadStage(Stage + 1);
        }

        public string Rank => BirdRules.RankFor(BirdsLeft, Score);

        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the slingshot game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
