using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// A side-scrolling run-and-jump level: platforms, coins, patrolling enemies, a flag.
    ///
    /// The third game, and the one the other two were quietly rehearsing. It is bigger than they
    /// are — it needs a world, a moving camera, enemies and lives — but the shape is identical:
    /// a pure rules class (<see cref="JumpQuestRules"/>) that decides everything that can be
    /// tested, a scene that only draws rectangles, and an IMGUI HUD. The level itself is
    /// generated and then *checked* for reachability, because a hand-built level in a scene file
    /// is the thing this project keeps avoiding and an unjumpable gap is a bug, not difficulty.
    /// </summary>
    public class JumpQuestGame : MonoBehaviour
    {
        public enum Phase { Ready, Running, Dead, Finished }

        public Phase State { get; private set; } = Phase.Ready;
        public int Coins { get; private set; }
        public int Lives { get; private set; } = 3;

        /// <summary>Best coin count of this session.</summary>
        public int Best { get; private set; }

        private JumpSettings _settings = JumpSettings.Default;
        private readonly List<Block> _blocks = new List<Block>();
        private readonly List<GameObject> _visuals = new List<GameObject>();
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<GameObject> _coinVisuals = new List<GameObject>();

        private class Enemy
        {
            public Block Block;
            public Transform Transform;
            public float Direction = 1f;
        }

        private Vector2 _position;
        private Vector2 _velocity;
        private float _heldFor;
        private float _spawnX;
        private bool _grounded;
        private float _deathAt;
        private Transform _player;
        private Camera _camera;
        private float _lastAspect;

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
            camera.transform.position = new Vector3(0f, 1.6f, -12f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.62f, 0.86f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            _camera = camera;
            FitCamera();

            if (FindObjectOfType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.05f;
                light.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.62f);

            _player = BuildPlayer();
            BuildLevelVisuals();
        }

        private Transform BuildPlayer()
        {
            var root = new GameObject("Player").transform;
            root.SetParent(transform, false);

            var body = Box("Body", Vector3.zero, new Vector3(0.6f, 0.8f, 0.6f),
                new Color(0.92f, 0.45f, 0.32f));
            body.SetParent(root, false);

            var head = Box("Head", Vector3.zero, new Vector3(0.44f, 0.4f, 0.44f),
                new Color(0.98f, 0.86f, 0.68f));
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, 0.56f, 0f);

            var eye = Box("Eye", Vector3.zero, new Vector3(0.08f, 0.1f, 0.1f),
                new Color(0.15f, 0.12f, 0.12f));
            eye.SetParent(root, false);
            eye.localPosition = new Vector3(0.14f, 0.6f, -0.22f);

            return root;
        }

        private void BuildLevelVisuals()
        {
            for (int i = 0; i < _visuals.Count; i++)
            {
                if (_visuals[i] != null) Destroy(_visuals[i]);
            }
            _visuals.Clear();
            _enemies.Clear();
            _coinVisuals.Clear();

            for (int i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                switch (block.Kind)
                {
                    case BlockKind.Ground:
                        _visuals.Add(Box("Ground", new Vector3(block.X, block.Y, 0f),
                            new Vector3(block.Width, block.Height, 1.2f),
                            new Color(0.36f, 0.60f, 0.34f)).gameObject);
                        break;

                    case BlockKind.Platform:
                        _visuals.Add(Box("Platform", new Vector3(block.X, block.Y, 0f),
                            new Vector3(block.Width, block.Height, 1f),
                            new Color(0.72f, 0.55f, 0.36f)).gameObject);
                        break;

                    case BlockKind.Coin:
                        var coin = Box("Coin", new Vector3(block.X, block.Y, 0f),
                            new Vector3(0.42f, 0.42f, 0.2f), new Color(1f, 0.84f, 0.28f));
                        coin.localRotation = Quaternion.Euler(0f, 0f, 45f);
                        _visuals.Add(coin.gameObject);
                        _coinVisuals.Add(coin.gameObject);
                        break;

                    case BlockKind.Enemy:
                        var enemy = Box("Enemy", new Vector3(block.X, block.Y, 0f),
                            new Vector3(block.Width, block.Height, 0.7f),
                            new Color(0.62f, 0.32f, 0.62f));
                        _visuals.Add(enemy.gameObject);
                        _enemies.Add(new Enemy { Block = block, Transform = enemy });
                        break;

                    case BlockKind.Goal:
                        var pole = Box("Goal", new Vector3(block.X, block.Y, 0f),
                            new Vector3(block.Width, block.Height, 0.3f),
                            new Color(0.95f, 0.95f, 0.95f));
                        _visuals.Add(pole.gameObject);
                        var flag = Box("Flag", Vector3.zero, new Vector3(1.1f, 0.7f, 0.12f),
                            new Color(0.9f, 0.28f, 0.32f));
                        flag.SetParent(pole, false);
                        flag.localPosition = new Vector3(0.62f, 0.9f, 0f);
                        break;
                }
            }
        }

        private Transform Box(string name, Vector3 position, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            box.transform.SetParent(transform, true);

            var renderer = box.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.2f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Destroy(box.GetComponent<Collider>());
            return box.transform;
        }

        // ------------------------------------------------------------------ run control

        private int _seed;

        public void ResetRun(bool newLevel = true)
        {
            Coins = 0;
            Lives = 3;
            _deathAt = 0f;

            if (newLevel || _blocks.Count == 0)
            {
                _seed = Random.Range(0, 1 << 28);
                _blocks.Clear();
                _blocks.AddRange(JumpQuestRules.BuildLevel(_seed, _settings));

                // A generated level that cannot be finished is a bug, not a difficulty setting —
                // so it is checked here and regenerated rather than shipped to the player.
                int attempts = 0;
                string complaint;
                while (!JumpQuestRules.IsPassable(_blocks, _settings, out complaint) && attempts < 12)
                {
                    attempts++;
                    Debug.LogWarning($"[DshMini] Regenerating level: {complaint}");
                    _seed = Random.Range(0, 1 << 28);
                    _blocks.Clear();
                    _blocks.AddRange(JumpQuestRules.BuildLevel(_seed, _settings));
                }

                BuildLevelVisuals();
            }

            _spawnX = -6f;
            _position = new Vector2(_spawnX, 1.6f);
            _velocity = Vector2.zero;
            _grounded = false;
            State = Phase.Ready;
            ApplyPlayer();
            DshMobile.MobileHaptics.Light();
        }

        public void StartRun()
        {
            if (State == Phase.Running) return;
            State = Phase.Running;
        }

        public void Jump()
        {
            if (State == Phase.Ready) StartRun();
            if (State != Phase.Running) return;

            _velocity.y = _settings.JumpSpeed;
            _heldFor = 0f;
            _grounded = false;
            DshMobile.MobileHaptics.Light();
        }

        private void Die()
        {
            if (State != Phase.Running) return;

            Lives--;
            DshMobile.MobileHaptics.Heavy();

            if (Lives > 0)
            {
                _position = new Vector2(Mathf.Max(_spawnX, _position.x - 3f), 2.2f);
                _velocity = Vector2.zero;
            }
            else
            {
                State = Phase.Dead;
                _deathAt = Time.time;
                PayOut(false);
            }
        }

        private void Finish()
        {
            if (State != Phase.Running) return;
            State = Phase.Finished;
            _deathAt = Time.time;
            DshMobile.MobileHaptics.Heavy();
            PayOut(true);
        }

        private void PayOut(bool finished)
        {
            int reward = JumpQuestRules.RewardFor(Coins, finished);
            if (reward > 0) DshMobile.PetWallet.Add(reward);
            if (Coins > Best) Best = Coins;
        }

        public int RunReward => JumpQuestRules.RewardFor(Coins, State == Phase.Finished);

        // ------------------------------------------------------------------ camera

        private void FitCamera()
        {
            if (_camera == null) return;
            _lastAspect = _camera.aspect;

            // Enough height to see the play area, and enough width that a phone in portrait does
            // not end up looking at a letterbox slot (the flappy game taught this one).
            float byHeight = 6.2f;
            float byWidth = 7.4f / Mathf.Max(0.2f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(byHeight, byWidth);
        }

        private void FollowPlayer()
        {
            if (_camera == null) return;

            var target = _camera.transform.position;
            float wantedX = _position.x + 2.6f;
            float wantedY = Mathf.Max(1.9f, _position.y + 0.9f);

            target.x = Mathf.Lerp(target.x, wantedX, 1f - Mathf.Exp(-6f * Time.deltaTime));
            target.y = Mathf.Lerp(target.y, wantedY, 1f - Mathf.Exp(-4f * Time.deltaTime));
            _camera.transform.position = target;
        }

        // ------------------------------------------------------------------ loop

        /// <summary>Held direction from the HUD's buttons or the keyboard, -1..1.</summary>
        public float MoveInput { get; set; }

        /// <summary>True while a jump is being held (button or key).</summary>
        public bool JumpHeld { get; set; }

        private void Update()
        {
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) ||
                Input.GetKeyDown(KeyCode.UpArrow))
            {
                Jump();
            }

            float keyboard = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboard -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboard += 1f;

            bool held = keyboard != 0f || Input.GetKey(KeyCode.Space) ||
                        Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);

            float move = Mathf.Abs(keyboard) > 0.01f ? keyboard : MoveInput;
            if (JumpHeld) held = true;

            if (State == Phase.Dead || State == Phase.Finished)
            {
                // Let the result panel breathe, then a tap restarts.
                if (Input.GetMouseButtonDown(0) && Time.time - _deathAt > 0.8f) ResetRun();
                return;
            }

            if (State == Phase.Ready)
            {
                if (Mathf.Abs(move) > 0.01f) StartRun();
            }

            if (State != Phase.Running)
            {
                ApplyPlayer();
                return;
            }

            bool jumpPressed = false;
            Tick(Time.deltaTime, move, held, jumpPressed);
            _heldFor = held ? _heldFor + Time.deltaTime : 0f;

            ApplyPlayer();
            FollowPlayer();
            TickEnemies(Time.deltaTime);
            TickPickups();
            TickGoal();
        }

        /// <summary>
        /// One frame of play. <paramref name="jumpPressed"/> is separate from
        /// <paramref name="held"/> because they are different things: a press starts a jump, and
        /// holding it stretches that same jump. <see cref="Jump"/> applies the press directly, so
        /// the live path passes false here and only a test (or an autopilot) passes true.
        /// </summary>
        public void Tick(float dt, float move, bool held, bool jumpPressed)
        {
            _grounded = JumpQuestRules.OnGround(_position, _blocks, _settings);
            if (_grounded && _velocity.y < 0f) _velocity.y = 0f;

            JumpQuestRules.Step(ref _position, ref _velocity, dt, move, held && _grounded,
                jumpPressed, _heldFor, _settings);
            JumpQuestRules.Resolve(ref _position, ref _velocity, _blocks, _settings);

            // A pit is a fall out of the world, which the level generator never leaves open but
            // the player can still find by jumping off the side of a platform.
            if (_position.y < -14f) Die();
        }

        private void TickEnemies(float dt)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy.Transform == null) continue;

                float x = enemy.Transform.position.x + enemy.Direction * 1.6f * dt;
                if (Mathf.Abs(x - enemy.Block.X) > enemy.Block.Patrol) enemy.Direction *= -1f;
                enemy.Transform.position = new Vector3(
                    Mathf.Clamp(x, enemy.Block.X - enemy.Block.Patrol, enemy.Block.X + enemy.Block.Patrol),
                    enemy.Block.Y, 0f);

                var rect = new Rect(enemy.Transform.position.x - enemy.Block.Width * 0.5f,
                    enemy.Block.Y - enemy.Block.Height * 0.5f, enemy.Block.Width, enemy.Block.Height);

                if (!JumpQuestRules.Overlaps(_position, _settings, rect)) continue;

                if (JumpQuestRules.IsStomp(_position, _velocity, rect, _settings))
                {
                    // Bounced off it: the payoff for jumping well is height, not just survival.
                    _velocity.y = _settings.JumpSpeed * 0.72f;
                    _position.y = rect.yMax + _settings.BodyHalfHeight;
                    DshMobile.MobileHaptics.Medium();
                    Destroy(enemy.Transform.gameObject);
                    enemy.Transform = null;
                }
                else
                {
                    Die();
                    return;
                }
            }
        }

        private void TickPickups()
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (_blocks[i].Kind != BlockKind.Coin) continue;

                // A little forgiveness on the pickup: the coin is a visual, and a player who ran
                // through one and got nothing would read it as a bug rather than as a near miss.
                var rect = _blocks[i].Rect;
                rect.xMin -= 0.12f;
                rect.xMax += 0.12f;
                rect.yMin -= 0.12f;
                rect.yMax += 0.12f;
                if (!JumpQuestRules.Overlaps(_position, _settings, rect)) continue;

                // Collected: the block becomes inert and the visual disappears.
                _blocks[i] = Block.Make(BlockKind.Platform, _blocks[i].X, _blocks[i].Y, 0f, 0f);
                Coins++;
                DshMobile.MobileHaptics.Light();

                for (int v = _coinVisuals.Count - 1; v >= 0; v--)
                {
                    var visual = _coinVisuals[v];
                    if (visual == null)
                    {
                        _coinVisuals.RemoveAt(v);
                        continue;
                    }

                    if (Mathf.Abs(visual.transform.position.x - rect.center.x) < 0.01f &&
                        Mathf.Abs(visual.transform.position.y - rect.center.y) < 0.01f)
                    {
                        Destroy(visual);
                        _coinVisuals.RemoveAt(v);
                        break;
                    }
                }
            }
        }

        private void TickGoal()
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (_blocks[i].Kind != BlockKind.Goal) continue;
                if (JumpQuestRules.Overlaps(_position, _settings, _blocks[i].Rect)) Finish();
                return;
            }
        }

        private void ApplyPlayer()
        {
            if (_player == null) return;
            _player.position = new Vector3(_position.x, _position.y, 0f);
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
