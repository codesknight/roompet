using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 切水果: fruit is thrown up, and the player swipes through it.
    ///
    /// Same shape as the other games here: <see cref="SliceRules"/> is pure arithmetic with all the
    /// tests, this class owns the world and the input, and the HUD is IMGUI. Nothing in this file
    /// decides anything the rules could have decided — including how fast to throw a fruit, which is
    /// computed from the game's fairness rule (every fruit must be in the air long enough to cut).
    ///
    /// The blade is a *line*, not a point: the swipe from last frame's position to this frame's is
    /// tested against every fruit. A hit test that only looks at where the finger is right now lets
    /// fruit slip between two frames, which the player experiences as "my swipe went straight
    /// through it" — the single most annoying bug a game like this can have.
    /// </summary>
    public class SliceGame : MonoBehaviour
    {
        public enum Phase { Ready, Playing, LevelDone, Dead }

        private class Piece
        {
            public Transform Transform;
            public Vector2 Position;
            public Vector2 Velocity;
            public FruitKind Kind;
            public bool IsBomb;
            public bool Sliced;
            public float Spin;
            public bool Counted;      // has this piece already cost or earned anything
        }

        private class Shard
        {
            public Transform Transform;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
        }

        public Phase State { get; private set; } = Phase.Ready;
        public SliceMode Mode { get; private set; } = SliceMode.Endless;
        public int Score { get; private set; }
        public int Best { get; private set; }
        public int Lives { get; private set; } = SliceRules.StartLives;
        public int Missed { get; private set; }
        public int Level { get; private set; } = 1;
        public int Wave { get; private set; }
        public int LastCombo { get; private set; }

        /// <summary>Set briefly after a good swipe, for the "+6" popup.</summary>
        public string Popup { get; private set; } = "";
        private float _popupUntil;
        private float _comboUntil;
        private int _comboCount;

        private SliceSettings _settings = SliceSettings.Default;
        private SliceLevelPlan _plan;

        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly List<Shard> _shards = new List<Shard>();
        private readonly System.Random _rng = new System.Random();

        private Transform _slicedRoot;
        private Camera _camera;
        private float _lastAspect;
        private float _nextWave;
        private float _deathAt;
        private bool _levelPassed;

        /// <summary>The swipe, in world units, for the HUD to draw and the rules to test.</summary>
        public Vector2 BladeFrom { get; private set; }
        public Vector2 BladeTo { get; private set; }
        public bool BladeActive { get; private set; }
        public float BladeFade { get; private set; }

        private Vector2 _pointerWorld;
        private bool _pointerDown;
        private bool _hasPointer;

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
            _plan = SliceRules.LevelPlan(Level, _settings);
            ResetRun(Mode);
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
            camera.backgroundColor = new Color(0.16f, 0.12f, 0.22f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            _camera = camera;
            FitCamera();

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.9f);
                light.intensity = 1.05f;
                light.transform.rotation = Quaternion.Euler(42f, -26f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.5f, 0.62f);

            _slicedRoot = new GameObject("Sliced").transform;
            _slicedRoot.SetParent(transform, false);
        }

        /// <summary>
        /// Frames the play area. The width is the constraint on a phone, exactly as in the catch
        /// game: portrait would otherwise put a third of the fruit off the sides.
        /// </summary>
        private void FitCamera()
        {
            if (_camera == null) return;
            _lastAspect = _camera.aspect;

            float byWidth = (_settings.HalfWidth + 0.8f) / Mathf.Max(0.2f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(6.6f, byWidth);
        }

        // ------------------------------------------------------------------ run control

        public void SetMode(SliceMode mode)
        {
            Mode = mode;
            Level = 1;
            ResetRun(mode);
        }

        public void ResetRun(SliceMode mode)
        {
            Mode = mode;
            ClearPieces();

            Score = 0;
            Lives = SliceRules.StartLives;
            Missed = 0;
            Wave = 0;
            _comboCount = 0;
            LastCombo = 0;
            Popup = "";
            _levelPassed = false;
            _nextWave = 0.9f;
            State = Phase.Ready;
            _plan = SliceRules.LevelPlan(Level, _settings);
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>Starts the next level of 闯关模式 (and keeps the score, which is a run total).</summary>
        public void NextLevel()
        {
            Level = Mathf.Min(Level + 1, SliceRules.LevelCount);
            Lives = SliceRules.StartLives;
            Missed = 0;
            Wave = 0;
            _levelPassed = false;
            _nextWave = 0.9f;
            State = Phase.Ready;
            _plan = SliceRules.LevelPlan(Level, _settings);
        }

        private void ClearPieces()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].Transform != null) Destroy(_pieces[i].Transform.gameObject);
            }
            _pieces.Clear();

            for (int i = 0; i < _shards.Count; i++)
            {
                if (_shards[i].Transform != null) Destroy(_shards[i].Transform.gameObject);
            }
            _shards.Clear();
        }

        /// <summary>True when the player has run out of levels, so the run is over for good.</summary>
        public bool CampaignFinished => Mode == SliceMode.Levels && Level >= SliceRules.LevelCount && _levelPassed;

        public SliceLevelPlan Plan => _plan;

        public int RunCoins => SliceRules.CoinsFor(Score);

        public bool LevelPassed => _levelPassed;

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();

            ReadBlade();

            if (State == Phase.Dead)
            {
                if (Input.GetMouseButtonDown(0) && Time.time - _deathAt > 0.8f) ResetRun(Mode);
                StepShards(Time.deltaTime);
                return;
            }

            if (State == Phase.LevelDone)
            {
                StepShards(Time.deltaTime);
                return;
            }

            if (State == Phase.Ready && BladeActive) StartRun();

            if (State == Phase.Playing)
            {
                SpawnIfDue(Time.deltaTime);
                Tick(Time.deltaTime);
            }

            StepShards(Time.deltaTime);
            ApplyBladeLook();
        }

        public void StartRun()
        {
            if (State != Phase.Ready) return;
            State = Phase.Playing;
        }

        /// <summary>
        /// One frame of play: fruit flies, gets sliced, or is missed. Public so a test can drive a
        /// whole run without waiting for real time.
        /// </summary>
        public void Tick(float dt)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);

            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                var piece = _pieces[i];
                if (piece.Transform == null)
                {
                    _pieces.RemoveAt(i);
                    continue;
                }

                piece.Velocity.y -= _settings.Gravity * step;
                piece.Position += piece.Velocity * step;
                piece.Spin += step * (piece.Velocity.x > 0f ? -140f : 140f);
                piece.Transform.position = new Vector3(piece.Position.x, piece.Position.y, 0f);
                piece.Transform.localRotation = Quaternion.Euler(0f, 0f, piece.Spin);

                if (BladeActive && !piece.Sliced && !piece.Counted &&
                    SliceRules.Slices(BladeFrom, BladeTo, piece.Position, _settings.FruitRadius, _settings))
                {
                    Cut(piece);
                }

                // Off the bottom: a fruit costs a life, a bomb is simply gone.
                if (piece.Position.y < _settings.BottomY - 1.5f)
                {
                    if (!piece.Counted && !piece.IsBomb)
                    {
                        piece.Counted = true;
                        Missed++;
                        Lives--;
                        DshMobile.MobileHaptics.Medium();
                        if (Lives <= 0)
                        {
                            Die();
                            return;
                        }
                    }

                    Destroy(piece.Transform.gameObject);
                    _pieces.RemoveAt(i);
                }
            }

            if (_comboCount > 0 && Time.time > _comboUntil)
            {
                _comboCount = 0;
                LastCombo = 0;
            }

            if (Mode == SliceMode.Levels && State == Phase.Playing && _nextWave <= 0f && _pieces.Count == 0)
            {
                // Every wave has been thrown and the sky is empty: the level is judged on its score.
                FinishLevel();
            }
        }

        private void Die()
        {
            State = Phase.Dead;
            _deathAt = Time.time;
            DshMobile.MobileHaptics.Heavy();

            int coins = RunCoins;
            if (coins > 0) DshMobile.PetWallet.Add(coins);
        }

        private void FinishLevel()
        {
            _levelPassed = Score >= _plan.TargetScore;
            State = Phase.LevelDone;

            if (_levelPassed)
            {
                DshMobile.MobileHaptics.Light();
            }
            else
            {
                DshMobile.MobileHaptics.Heavy();
            }
        }

        // ------------------------------------------------------------------ spawning

        private void SpawnIfDue(float dt)
        {
            _nextWave -= dt;
            if (_nextWave > 0f) return;

            if (Mode == SliceMode.Levels && Wave >= _plan.Waves)
            {
                return;      // the level is thrown; Tick decides when it is over
            }

            int size = Mode == SliceMode.Endless
                ? SliceRules.WaveSizeFor(Score, _settings)
                : _plan.FruitsPerWave;

            float bombChance = Mode == SliceMode.Endless
                ? SliceRules.BombChanceFor(Score, _settings)
                : _plan.BombChance;

            for (int i = 0; i < size; i++)
            {
                // Staggered a little so a wave reads as a burst rather than a single blob.
                SpawnPiece((float)_rng.NextDouble(), (float)_rng.NextDouble(),
                    (float)_rng.NextDouble() < bombChance, i * 0.09f);
            }

            Wave++;
            _nextWave = Mode == SliceMode.Endless
                ? SliceRules.WaveSecondsFor(Score, _settings)
                : SliceRules.LevelWaveSeconds(Wave);
        }

        private void SpawnPiece(float speedRoll, float acrossRoll, bool isBomb, float delay)
        {
            Vector2 position, velocity;
            SliceRules.Launch(speedRoll, acrossRoll, _settings, out position, out velocity);

            // The stagger is applied by pushing the piece forward in time, so every fruit still gets
            // its full flight — a delay that shortened the flight would break the fairness rule.
            position = SliceRules.PositionAt(position, velocity, delay, _settings);
            velocity.y -= _settings.Gravity * delay;

            var root = new GameObject(isBomb ? "Bomb" : "Fruit").transform;
            root.SetParent(transform, false);
            root.position = new Vector3(position.x, position.y, 0f);

            var kind = CatchRules.FruitFor(_rng.Next(CatchRules.FruitKindCount));
            if (isBomb) FruitArt.BuildBomb(root, _settings.FruitRadius);
            else FruitArt.Build(root, kind, _settings.FruitRadius);

            _pieces.Add(new Piece
            {
                Transform = root,
                Position = position,
                Velocity = velocity,
                Kind = kind,
                IsBomb = isBomb
            });
        }

        // ------------------------------------------------------------------ the blade

        /// <summary>
        /// Reads the swipe.
        ///
        /// Two sources, because the game ships to a phone and is developed in the editor: the shared
        /// touch layer's gesture (a finger, or the mouse when the phone layout is forced on) and the
        /// raw mouse for a desktop build that is not using touch controls at all. Both end up as the
        /// same world-space segment.
        /// </summary>
        private void ReadBlade()
        {
            BladeFade = Mathf.Max(0f, BladeFade - Time.deltaTime * 3.2f);

            Vector2? topDown = null;
            bool down = false;

            var gesture = DshMobile.MobileTouch.Gesture;
            if (gesture.IsActive && DshMobile.MobileTouch.PlayInputEnabled)
            {
                topDown = gesture.Position;
                down = true;
            }
            else if (Input.GetMouseButton(0) && DshMobile.MobileTouch.PlayInputEnabled)
            {
                topDown = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                down = true;
            }

            if (topDown == null || !down)
            {
                _pointerDown = false;
                _hasPointer = false;
                BladeActive = false;
                return;
            }

            Vector2 world = WorldPoint(topDown.Value);

            if (_pointerDown && _hasPointer)
            {
                BladeFrom = _pointerWorld;
                BladeTo = world;
                BladeActive = Vector2.Distance(BladeFrom, BladeTo) > 0.001f;
                if (BladeActive) BladeFade = 1f;
            }
            else
            {
                BladeFrom = world;
                BladeTo = world;
                BladeActive = false;
            }

            _pointerWorld = world;
            _pointerDown = true;
            _hasPointer = true;
        }

        /// <summary>Screen (top-down, as the touch layer reports it) → world.</summary>
        public Vector2 WorldPoint(Vector2 topDownScreen)
        {
            if (_camera == null) return topDownScreen;
            var world = _camera.ScreenToWorldPoint(
                new Vector3(topDownScreen.x, Screen.height - topDownScreen.y, -_camera.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        private void ApplyBladeLook()
        {
            // The blade is drawn by the HUD from BladeFrom/BladeTo, so there is nothing to build
            // here — one less thing that can disagree with the hit test.
        }

        // ------------------------------------------------------------------ cutting

        private void Cut(Piece piece)
        {
            piece.Counted = true;

            if (piece.IsBomb)
            {
                Explode(piece);
                return;
            }

            piece.Sliced = true;
            Score += SliceRules.ScoreFor(piece.Kind);
            SlicePieces(piece);

            _comboCount++;
            _comboUntil = Time.time + 0.45f;
            LastCombo = _comboCount;

            if (_comboCount >= 2)
            {
                int bonus = SliceRules.ComboBonus(_comboCount);
                Score += bonus;
                Popup = "+" + (SliceRules.ScoreFor(piece.Kind) + bonus) + "　连切 " + _comboCount;
                _popupUntil = Time.time + 1.1f;
            }
            else
            {
                Popup = "+" + SliceRules.ScoreFor(piece.Kind);
                _popupUntil = Time.time + 0.7f;
            }

            DshMobile.MobileHaptics.Light();

            if (piece.Transform != null) Destroy(piece.Transform.gameObject);
            _pieces.Remove(piece);
        }

        private void Explode(Piece bomb)
        {
            DshMobile.MobileHaptics.Heavy();
            Popup = "炸弹！";
            _popupUntil = Time.time + 1.2f;

            for (int i = 0; i < 10; i++)
            {
                SpawnShard(bomb.Position, FruitArt.BombDark, 0.22f, 6f);
            }

            if (bomb.Transform != null) Destroy(bomb.Transform.gameObject);
            _pieces.Remove(bomb);

            if (Mode == SliceMode.Levels)
            {
                // In a level a bomb is the end of the attempt: the level is about precision, and a
                // run that can absorb three bombs is a run that teaches nothing.
                Lives = 0;
                Die();
                return;
            }

            Lives--;
            if (Lives <= 0) Die();
        }

        private void SlicePieces(Piece piece)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var root = new GameObject("Half").transform;
                root.SetParent(_slicedRoot, false);
                root.position = new Vector3(piece.Position.x, piece.Position.y, 0f);
                FruitArt.BuildHalf(root, piece.Kind, _settings.FruitRadius * 0.85f, side);

                _shards.Add(new Shard
                {
                    Transform = root,
                    Position = piece.Position,
                    Velocity = new Vector2(side * 2.6f, piece.Velocity.y * 0.35f + 1.4f),
                    Spin = side * 260f
                });
            }

            for (int i = 0; i < 5; i++)
            {
                SpawnShard(piece.Position, FruitArt.Skin(piece.Kind), 0.14f, 4.5f);
            }
        }

        private void SpawnShard(Vector2 at, Color color, float size, float speed)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Juice";
            go.transform.SetParent(_slicedRoot, false);
            go.transform.localScale = Vector3.one * size * 2f;
            go.transform.position = new Vector3(at.x, at.y, 0.4f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard")) { color = color };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Destroy(go.GetComponent<Collider>());

            float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
            _shards.Add(new Shard
            {
                Transform = go.transform,
                Position = at,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + new Vector2(0f, 1.5f)
            });
        }

        private void StepShards(float dt)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);

            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                var shard = _shards[i];
                if (shard.Transform == null)
                {
                    _shards.RemoveAt(i);
                    continue;
                }

                shard.Age += step;
                shard.Velocity.y -= _settings.Gravity * step;
                shard.Position += shard.Velocity * step;
                shard.Transform.position = new Vector3(shard.Position.x, shard.Position.y, 0.4f);
                if (!Mathf.Approximately(shard.Spin, 0f))
                {
                    shard.Transform.localRotation = Quaternion.Euler(0f, 0f, shard.Spin * shard.Age);
                }

                if (shard.Age > 1.4f || shard.Position.y < _settings.BottomY - 2f)
                {
                    Destroy(shard.Transform.gameObject);
                    _shards.RemoveAt(i);
                }
            }
        }

        /// <summary>True while the "+n" popup should be on screen.</summary>
        public bool PopupVisible => Time.time < _popupUntil;

        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the slicing game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
