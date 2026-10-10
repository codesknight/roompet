using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshMiniGames
{
    /// <summary>
    /// 愤怒的小鸟: pull the slingshot back, let go, and knock the pigs out of their tower.
    ///
    /// The important structural decision is that <b>this class does not decide what happens</b>. The
    /// shot is handed to <see cref="BirdRules.Simulate"/>, which runs the rigid-body simulation and
    /// returns a recorded replay; this class then plays that recording back — moving the bird, every
    /// block and every pig to where the simulation says they were. The generator proved the level
    /// clearable with that same function, so what the player sees is what was verified.
    ///
    /// Round 21 replaced the old "blocks drop straight down" approximation with real bodies (see
    /// <see cref="BirdRules"/>), which is why the view is a *replay* rather than a hand-written
    /// animation: a tower that topples has a shape no animation code could predict, and the point of
    /// the generator's promise is that the picture and the verdict are the same run.
    ///
    /// Input is one drag: touch (or hold the mouse) anywhere and pull away from the slingshot. The
    /// rubber band follows the bird, and the dotted arc is what the shot would do in empty air — off
    /// with one button for players who would rather judge it themselves.
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

        /// <summary>
        /// Whether the dotted arc is drawn while aiming.
        ///
        /// The player asked for it as a toggle ("可开启或关闭"), and that is the right shape for it: the
        /// arc is a teaching aid, and a level you can beat by tracing a line is a different game from
        /// one you have to read.
        /// </summary>
        public bool ShowTrajectory
        {
            get => _showTrajectory;
            set
            {
                _showTrajectory = value;
                PlayerPrefs.SetInt(TrajectoryKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public const string TrajectoryKey = "dshgames.birdtraj";
        private bool _showTrajectory = true;

        /// <summary>True while the player has the game held.</summary>
        public bool Paused { get; private set; }

        private BirdSettings _settings = BirdSettings.Default;
        private BirdLevel _level;
        private Transform _world;
        private Transform _bird;
        private Transform _slingRoot;
        private LineRenderer _band;
        private Camera _camera;
        private float _lastAspect;

        private readonly List<Transform> _blockViews = new List<Transform>();
        private readonly List<Transform> _pigViews = new List<Transform>();
        private readonly List<float> _pigDiedAt = new List<float>();

        private class Debris
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Age;
        }
        private readonly List<Debris> _debris = new List<Debris>();

        private Vector2 _dragWorld;
        private bool _dragging;
        private bool _hasDrag;

        // Playback of the last shot.
        private BirdShotResult _shot;
        private float _playTime;
        private bool _playing;
        private int _nextEvent;
        private bool _levelScored;

        /// <summary>The dotted arc, in world units, for the HUD to draw.</summary>
        private readonly List<Vector2> _preview = new List<Vector2>(48);

        /// <summary>The level, for the HUD.</summary>
        public BirdLevel Level => _level;

        /// <summary>The arc to draw this frame. Empty when not aiming, or when the aid is switched off.</summary>
        public IList<Vector2> Preview => _preview;

        /// <summary>Whether a hint is available for this level.</summary>
        public bool HasHint => _level != null && _level.Solution != null && _level.Solution.Count > 0;

        public string HintText => HasHint
            ? "这一关验证过的第一发：角度 " + Mathf.RoundToInt(Mathf.Atan2(_level.Solution[0].y, _level.Solution[0].x) * Mathf.Rad2Deg) +
              "°，力度 " + Mathf.RoundToInt(_level.Solution[0].magnitude / _settings.MaxLaunchSpeed * 100f) + "%"
            : "";

        public bool HintVisible { get; private set; }
        private float _hintUntil;

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _showTrajectory = PlayerPrefs.GetInt(TrajectoryKey, 1) != 0;
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
            BuildArcDots();
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

        /// <summary>
        /// The slingshot: a post, two prongs leaning apart, and the rubber band between them.
        ///
        /// The first version was a post with a horizontal bar on top — which is a *signpost*, and it
        /// read as one: nothing about it said "pull this". The prongs give the band somewhere to
        /// attach, and the band is what makes the drag legible, because it is the thing that visibly
        /// stretches and visibly snaps.
        /// </summary>
        private void BuildSling()
        {
            _slingRoot = new GameObject("Sling").transform;
            _slingRoot.SetParent(transform, false);

            var wood = new Color(0.52f, 0.36f, 0.22f);
            float baseY = _settings.GroundY;
            float forkY = baseY + _settings.SlingHeight;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(_slingRoot, false);
            post.transform.localScale = new Vector3(0.17f, _settings.SlingHeight * 0.5f, 0.17f);
            post.transform.localPosition = new Vector3(_settings.SlingX, baseY + _settings.SlingHeight * 0.5f, 0.3f);
            Paint(post, wood);
            Destroy(post.GetComponent<Collider>());

            for (int side = -1; side <= 1; side += 2)
            {
                var prong = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prong.name = side < 0 ? "ProngL" : "ProngR";
                prong.transform.SetParent(_slingRoot, false);
                prong.transform.localScale = new Vector3(0.11f, 0.42f, 0.11f);
                prong.transform.localPosition = new Vector3(
                    _settings.SlingX + side * 0.20f, forkY - 0.30f, 0.3f);
                prong.transform.localRotation = Quaternion.Euler(0f, 0f, side * -16f);
                Paint(prong, wood);
                Destroy(prong.GetComponent<Collider>());
            }

            var go = new GameObject("Band");
            go.transform.SetParent(_slingRoot, false);

            _band = go.AddComponent<LineRenderer>();
            _band.useWorldSpace = true;
            _band.positionCount = 3;
            _band.startWidth = 0.075f;
            _band.endWidth = 0.075f;
            _band.numCapVertices = 2;
            _band.alignment = LineAlignment.View;
            _band.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _band.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Destroy(go);
                _band = null;
                return;
            }

            var material = new Material(shader) { name = "Dsh_Band" };
            material.color = Color.white;
            _band.material = material;
            _band.startColor = new Color(0.36f, 0.13f, 0.16f, 1f);
            _band.endColor = new Color(0.36f, 0.13f, 0.16f, 1f);

            UpdateBand();
        }

        /// <summary>The two prong tips, which is where the band is tied.</summary>
        private Vector2 ForkTip(int side)
            => new Vector2(_settings.SlingX + side * 0.26f, _settings.GroundY + _settings.SlingHeight - 0.06f);

        /// <summary>
        /// Redraws the band: prong, bird, prong.
        ///
        /// Three points in one line rather than two lines, because that is exactly what a rubber band
        /// around a pocket looks like — and because it cannot develop a seam in the middle of the pull,
        /// which is where the player's eye is.
        /// </summary>
        private void UpdateBand()
        {
            if (_band == null) return;

            Vector2 pocket = _bird != null
                ? new Vector2(_bird.position.x, _bird.position.y)
                : new Vector2(_level != null ? _level.SlingX : _settings.SlingX,
                    _settings.GroundY + _settings.SlingHeight);

            // Once the bird has gone the band hangs slack between the prongs.
            if (State == Phase.Flying || State == Phase.Cleared || State == Phase.Failed)
            {
                pocket = (ForkTip(-1) + ForkTip(1)) * 0.5f + new Vector2(0f, -0.12f);
            }

            _band.SetPosition(0, new Vector3(ForkTip(-1).x, ForkTip(-1).y, -0.1f));
            _band.SetPosition(1, new Vector3(pocket.x, pocket.y, -0.1f));
            _band.SetPosition(2, new Vector3(ForkTip(1).x, ForkTip(1).y, -0.1f));
        }

        /// <summary>
        /// The dotted arc, spawned in the world.
        ///
        /// It was drawn in the interface for a round, and on a phone it was not merely ugly — it was in
        /// the wrong place: the HUD turned world points into screen pixels and then drew them through
        /// <c>GUI.matrix</c>, which scales by the design factor and insets by the notch, so the whole
        /// parabola came out shifted and stretched. "抛物线完全错位" was exactly that. In the world there
        /// is no conversion to get wrong: the arc is the path the simulation would take, in the
        /// coordinates the simulation uses.
        /// </summary>
        private void BuildArcDots()
        {
            var shader = Shader.Find("Sprites/Default");
            var material = shader == null ? null : new Material(shader) { name = "Dsh_ArcDot" };
            if (material != null)
            {
                material.mainTexture = DshMobile.SoftShadow.Gradient;
                material.color = new Color(1f, 0.98f, 0.9f, 0.85f);
            }

            for (int i = 0; i < ArcDotCount; i++)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                dot.name = "ArcDot" + i;
                dot.transform.SetParent(transform, false);
                dot.transform.localScale = Vector3.one * 0.24f;
                Destroy(dot.GetComponent<Collider>());

                var renderer = dot.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (material != null) renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                dot.SetActive(false);
                _arcDots.Add(dot.transform);
            }
        }

        /// <summary>Thirty-odd dots is more than an arc needs, and fewer than a pool of them costs.</summary>
        private const int ArcDotCount = 26;

        private readonly List<Transform> _arcDots = new List<Transform>();

        /// <summary>
        /// Places the dots along the arc for the current pull.
        ///
        /// The preview is computed by the rules — the same integrator the shot uses — so the dots *are*
        /// the trajectory, not a decoration that resembles one.
        /// </summary>
        private void UpdateArcDots()
        {
            if (_arcDots.Count == 0) return;

            bool show = _showTrajectory && State == Phase.Aiming && !Paused && _preview.Count >= 2;
            if (!show)
            {
                for (int i = 0; i < _arcDots.Count; i++)
                {
                    if (_arcDots[i].gameObject.activeSelf) _arcDots[i].gameObject.SetActive(false);
                }
                return;
            }

            int count = _arcDots.Count;

            // Spaced along the *length* of the arc rather than by sample index. The samples are coarse
            // (one every 0.05 s), a short shot is only six of them, and stepping by index stacked all
            // twenty-six dots onto those six points and left the rest of the curve bare.
            float total = 0f;
            for (int i = 1; i < _preview.Count; i++) total += Vector2.Distance(_preview[i - 1], _preview[i]);

            for (int i = 0; i < count; i++)
            {
                float along = count > 1 ? i / (float)(count - 1) : 0f;
                var point = AlongPreview(total * along, total);

                var dot = _arcDots[i];
                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                dot.position = new Vector3(point.x, point.y, -0.9f);

                // Smaller and fainter the further along it is: a direction, not a wall to hit.
                dot.localScale = Vector3.one * Mathf.Lerp(0.26f, 0.12f, along);
            }
        }

        /// <summary>The point this far along the previewed arc, interpolated between its samples.</summary>
        private Vector2 AlongPreview(float distance, float total)
        {
            if (_preview.Count == 0) return Vector2.zero;
            if (total <= 0.0001f || _preview.Count == 1) return _preview[0];

            float walked = 0f;
            for (int i = 1; i < _preview.Count; i++)
            {
                var from = _preview[i - 1];
                var to = _preview[i];
                float segment = Vector2.Distance(from, to);

                if (walked + segment >= distance || i == _preview.Count - 1)
                {
                    float t = segment <= 0.0001f ? 0f : Mathf.Clamp01((distance - walked) / segment);
                    return Vector2.Lerp(from, to, t);
                }

                walked += segment;
            }

            return _preview[_preview.Count - 1];
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

            float left = slingX - 1.6f;
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
            _viewLeft = left;
            _viewRight = right;
            _viewCentre = new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
            _viewSize = Mathf.Max(4.6f, Mathf.Max(byWidth, halfHeight));

            _camera.orthographicSize = _viewSize;
            _camera.transform.position = new Vector3(_viewCentre.x, _viewCentre.y, -14f);
        }

        private float _viewLeft;
        private float _viewRight;
        private Vector2 _viewCentre;
        private float _viewSize;

        /// <summary>
        /// Keeps the camera on the action.
        ///
        /// Fitting the whole level is what makes it readable on a wide screen and what makes it *tiny* on
        /// a portrait one: an eighteen-unit field on a 0.45 aspect screen needs an orthographic size of
        /// twenty, and the towers become specks in the middle of a lot of sky. So the level is framed as
        /// a whole only while aiming, and while the bird is in the air the camera follows it and pulls in
        /// a little — which is also what the game this is modelled on does, for the same reason.
        /// </summary>
        private void UpdateCamera()
        {
            if (_camera == null) return;

            float zoom = _viewSize;
            float centreX = _viewCentre.x;

            if (State == Phase.Flying && _bird != null)
            {
                centreX = Mathf.Clamp(_bird.position.x,
                    _viewLeft + _viewSize * _camera.aspect * 0.5f,
                    _viewRight - _viewSize * _camera.aspect * 0.5f);
                zoom = Mathf.Max(4.6f, _viewSize * 0.72f);
            }

            // A bird crosses the field in under a second: a leisurely follow would spend the interesting
            // part of the shot with the bird off the edge of the screen, which is exactly what the first
            // version of this did.
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            if (Time.deltaTime <= 0f) k = 1f;

            var wanted = new Vector3(centreX, _viewCentre.y, -14f);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, wanted, k);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, zoom, k);
        }

        // ------------------------------------------------------------------ stages

        /// <summary>Builds a fresh, verified level for a stage, across frames.</summary>
        public void LoadStage(int stage)
        {
            Stage = Mathf.Clamp(stage, 1, BirdLevels.StageCount);
            int seed = 20250607 + Stage * 977;

            // A verified level is worth keeping: the seed is fixed per stage, so the generator would
            // produce this exact tower again — after a second or two of simulation. Read it back if the
            // last visit already paid for it.
            _level = ReadCachedLevel(Stage);
            if (_level != null)
            {
                _generator = null;
                Generating = false;
                BirdsLeft = _level.Birds;
                _levelScored = false;
                _shot = null;
                _playing = false;
                _preview.Clear();
                State = Phase.Ready;
                Paused = false;
                Rebuild();
                FitCamera();
                return;
            }

            _generator = new BirdLevels.Generator(Stage, seed, _settings);
            _level = null;
            Generating = true;
            BirdsLeft = 0;
            _levelScored = false;
            _shot = null;
            _playing = false;
            _preview.Clear();
            State = Phase.Ready;
            Paused = false;
        }

        private BirdLevel ReadCachedLevel(int stage)
        {
            string key = BirdLevels.CacheKey(stage, _settings);
            string text = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(text)) return null;

            var level = BirdLevels.Decode(text);
            if (level == null)
            {
                // A cache that cannot be read is not an error worth showing: the generator runs again.
                PlayerPrefs.DeleteKey(key);
                return null;
            }

            // And a cached level is only worth keeping if its recorded shots still clear it under this
            // build's physics. A fraction of a millimetre of rounding is enough to turn a shot that
            // cleared the level into one that misses, and "unwinnable level" is the one failure this
            // game must not ship — so the claim is checked rather than trusted.
            if (!BirdLevels.SolutionStillClears(level, _settings))
            {
                PlayerPrefs.DeleteKey(key);
                return null;
            }

            return level;
        }

        private void WriteCachedLevel(BirdLevel level)
        {
            if (level == null || level.Solution == null || level.Solution.Count == 0) return;

            PlayerPrefs.SetString(BirdLevels.CacheKey(level.Stage, _settings), BirdLevels.Encode(level));
            PlayerPrefs.Save();
        }

        /// <summary>True while the level is being built and verified, so the HUD can say so.</summary>
        public bool Generating { get; private set; }

        /// <summary>Which attempt the verifier is on, for the loading line.</summary>
        public int GenerationAttempt => _generator != null ? _generator.Attempts : 0;

        private BirdLevels.Generator _generator;

        /// <summary>
        /// One generation attempt per frame.
        ///
        /// A rigid-body level has to be settled and then played several times over before it can be
        /// handed to the player, which is a second or two of work — and doing it inside `LoadStage` froze
        /// the game on a static screen with no explanation. One attempt per frame keeps the scene alive
        /// and lets the HUD count the attempts out loud.
        /// </summary>
        private void TickGeneration()
        {
            if (_generator == null)
            {
                Generating = false;
                return;
            }

            if (!_generator.Step()) return;

            _level = _generator.Level;
            _generator = null;
            Generating = false;
            BirdsLeft = _level.Birds;
            WriteCachedLevel(_level);
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
                pig.VX = 0f;
                pig.VY = 0f;
                pig.Awake = true;
                pig.RestTimer = 0f;
                pig.SquashTimer = 0f;
                _level.Pigs[i] = pig;
            }

            for (int i = 0; i < _level.Blocks.Count; i++)
            {
                var block = _level.Blocks[i];
                block.Alive = true;
                block.Health = BirdRules.HealthFor(block.Kind);
                block.Angle = 0f;
                block.VX = 0f;
                block.VY = 0f;
                block.Spin = 0f;
                block.Awake = true;
                block.RestTimer = 0f;
                _level.Blocks[i] = block;
            }

            _levelScored = false;
            _shot = null;
            _playing = false;
            _preview.Clear();
            State = Phase.Ready;
            Paused = false;
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
            _pigDiedAt.Clear();

            for (int i = 0; i < _level.Blocks.Count; i++)
            {
                var block = _level.Blocks[i];
                var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                view.name = "Block" + i;
                view.transform.SetParent(_world, false);
                view.transform.localScale = new Vector3(block.HalfW * 2f, block.HalfH * 2f, 1.1f);
                view.transform.position = new Vector3(block.X, block.Y, 0f);
                view.transform.localRotation = Quaternion.Euler(0f, 0f, block.Angle * Mathf.Rad2Deg);
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
                _pigViews.Add(view.transform);
                _pigDiedAt.Add(-1f);
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
            // Prefer the Kenney pig over the green sphere. +90° Y turns its -Z face toward -X (the
            // sling, where the bird is coming from); the death pop still scales the whole view.
            var model = DshMobile.KenneyModel.Load("animal-pig", root, radius * 2f, centerVertically: true);
            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0f, 90f, 0f);
                return;
            }

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

            // Prefer the Kenney parrot over the red sphere. -90° Y turns the model's -Z face
            // toward +X, the launch direction the sphere bird's beak already pointed at; the
            // flight replay keeps setting the bird's Z-rotation, which then tilts the model
            // along the trajectory just like it did the sphere.
            var model = DshMobile.KenneyModel.Load("animal-parrot", _bird,
                _settings.BirdRadius * 2f, centerVertically: true);
            if (model != null)
            {
                model.localRotation = Quaternion.Euler(0f, -90f, 0f);
                UpdateBand();
                return;
            }

            AddPart(_bird, PrimitiveType.Sphere, Vector3.zero, Vector3.one * _settings.BirdRadius * 2f,
                new Color(0.92f, 0.28f, 0.24f));
            AddPart(_bird, PrimitiveType.Cube, new Vector3(_settings.BirdRadius * 1.05f, 0f, 0f),
                new Vector3(_settings.BirdRadius * 0.7f, _settings.BirdRadius * 0.4f, _settings.BirdRadius * 0.5f),
                new Color(0.98f, 0.78f, 0.24f), "Beak");
            AddPart(_bird, PrimitiveType.Sphere, new Vector3(_settings.BirdRadius * 0.35f,
                    _settings.BirdRadius * 0.35f, -_settings.BirdRadius * 0.8f),
                Vector3.one * _settings.BirdRadius * 0.34f, Color.white, "Eye");

            UpdateBand();
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

        /// <summary>Pause from the HUD. Nothing is frozen globally: the shot's own clock stops.</summary>
        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused)
            {
                _dragging = false;
                _hasDrag = false;
                _preview.Clear();
                if (State == Phase.Aiming) State = Phase.Ready;
                UpdateBand();
            }
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (_camera != null && !Mathf.Approximately(_lastAspect, _camera.aspect)) FitCamera();

            if (Generating)
            {
                TickGeneration();
                return;
            }

            if (_level == null) return;

            AnimatePigs();
            TickDebris(Time.deltaTime);

            if (Paused)
            {
                UpdateBand();
                return;
            }

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

                default:
                    Aim();
                    break;
            }

            UpdateBand();
            UpdateCamera();
            UpdateArcDots();
        }

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
            }

            _dragWorld = pointer.Value;
            _dragging = true;
            State = Phase.Aiming;

            // The bird follows the pull, up to the sling's reach.
            Vector2 pull = _dragWorld - SlingPosition;
            if (pull.magnitude > _settings.MaxPull) pull = pull.normalized * _settings.MaxPull;
            _bird.position = SlingPosition + pull;

            UpdatePreview();
        }

        /// <summary>
        /// The dotted arc: where the bird would go if the sky were empty.
        ///
        /// Computed with the rules' own integrator, so the preview cannot promise an arc the bird will
        /// not fly. It deliberately stops at the ground and knows nothing about blocks — this is an aid
        /// for reading the pull, not a solver.
        /// </summary>
        private void UpdatePreview()
        {
            _preview.Clear();
            if (!_showTrajectory) return;
            if (!BirdRules.CanLaunch(SlingPosition, _dragWorld, _settings)) return;

            var velocity = BirdRules.LaunchVelocity(SlingPosition, _dragWorld, _settings);
            BirdRules.PreviewArc(SlingPosition, velocity, _settings, _preview);
        }

        private void ResetBird()
        {
            State = Phase.Ready;
            _preview.Clear();
            if (_bird != null) _bird.position = SlingPosition;
        }

        private Vector2? PointerWorld()
        {
            // A pointer over a uGUI button belongs to the button, not the sling: without this,
            // tapping 暂停 / 重开这关 / 看提示 would also yank the sling and fire a wasted shot
            // (the old PointerOverPanel flag was set but never read, which was exactly that bug).
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return null;

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

            // The whole shot is decided here, by the rules, and then only replayed — with the replay
            // recorded, because a toppling tower is not something an animation can predict.
            _shot = BirdRules.Simulate(_level, velocity, _settings, true, true);
            _playTime = 0f;
            _playing = true;
            _nextEvent = 0;
            _dragging = false;
            _hasDrag = false;
            _preview.Clear();
            BirdsLeft--;

            State = Phase.Flying;
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>Plays the recorded replay back, firing the recorded events on time.</summary>
        private void Playback()
        {
            if (_shot == null || !_playing)
            {
                State = Phase.Ready;
                return;
            }

            _playTime += Time.deltaTime;
            ApplyFrames(_playTime);

            while (_nextEvent < _shot.Events.Count && _shot.Events[_nextEvent].Time <= _playTime)
            {
                Apply(_shot.Events[_nextEvent]);
                _nextEvent++;
            }

            if (_playTime < _shot.EndTime) return;

            _playing = false;
            _shot = null;

            if (_level.Cleared)
            {
                FinishCleared();
                return;
            }

            if (BirdsLeft <= 0)
            {
                State = Phase.Failed;
                DshMobile.MobileHaptics.Heavy();
                return;
            }

            ResetBird();
        }

        /// <summary>
        /// Moves every view to where the recording says it was, interpolating between the 60 Hz samples
        /// so the picture is smooth on a faster screen than the one it was recorded on.
        /// </summary>
        private void ApplyFrames(float time)
        {
            var frames = _shot.Frames;
            if (frames.Count == 0) return;

            int index = 0;
            while (index < frames.Count - 1 && frames[index + 1].Time <= time) index++;

            var frame = frames[index];
            var next = frames[Mathf.Min(index + 1, frames.Count - 1)];
            float span = next.Time - frame.Time;
            float t = span > 0.0001f ? Mathf.Clamp01((time - frame.Time) / span) : 0f;

            if (_bird != null)
            {
                var position = Vector2.Lerp(frame.Bird, next.Bird, t);
                var previous = _bird.position;
                _bird.position = new Vector3(position.x, position.y, 0f);

                var direction = position - new Vector2(previous.x, previous.y);
                if (direction.sqrMagnitude > 0.0002f)
                {
                    _bird.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }
            }

            int blocks = Mathf.Min(_blockViews.Count, frame.Blocks.Length);
            for (int i = 0; i < blocks; i++)
            {
                var view = _blockViews[i];
                if (view == null) continue;

                bool alive = frame.Alive[i];
                if (view.gameObject.activeSelf != alive) view.gameObject.SetActive(alive);
                if (!alive) continue;

                var position = Vector2.Lerp(frame.Blocks[i], next.Blocks[i], t);
                view.position = new Vector3(position.x, position.y, 0f);

                float angle = Mathf.LerpAngle(frame.Angles[i] * Mathf.Rad2Deg,
                    next.Angles[i] * Mathf.Rad2Deg, t);
                view.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            int pigs = Mathf.Min(_pigViews.Count, frame.Pigs.Length);
            for (int i = 0; i < pigs; i++)
            {
                var view = _pigViews[i];
                if (view == null || !view.gameObject.activeSelf) continue;

                var position = Vector2.Lerp(frame.Pigs[i], next.Pigs[i], t);
                view.position = new Vector3(position.x, position.y, 0f);
            }
        }

        private void Apply(BirdEvent e)
        {
            switch (e.Kind)
            {
                case BirdEventKind.BlockBroken:
                    if (e.Index >= 0 && e.Index < _level.Blocks.Count)
                    {
                        BurstBlock(e.Index);
                        Score += BirdRules.ScoreFor(_level.Blocks[e.Index].Kind);
                        DshMobile.MobileHaptics.Medium();
                    }
                    break;

                case BirdEventKind.PigKilled:
                    if (e.Index >= 0 && e.Index < _pigViews.Count)
                    {
                        _pigDiedAt[e.Index] = Time.time;
                        Score += BirdRules.PigScore;
                        DshMobile.MobileHaptics.Medium();
                    }
                    break;

                case BirdEventKind.BirdStopped:
                    break;
            }
        }

        /// <summary>
        /// Pigs shrink and vanish when they die.
        ///
        /// A pig that simply disappears reads as a bug; a pig that pops reads as a hit. It also hides
        /// the moment the replay desynchronises, which it never should — but if it ever does, a puff is
        /// the wrong place to notice.
        /// </summary>
        private void AnimatePigs()
        {
            for (int i = 0; i < _pigViews.Count && i < _pigDiedAt.Count; i++)
            {
                if (_pigDiedAt[i] < 0f) continue;
                var view = _pigViews[i];
                if (view == null) continue;

                float age = Time.time - _pigDiedAt[i];
                float scale = Mathf.Clamp01(1f - age / 0.28f);
                view.localScale = Vector3.one * (0.6f + scale * 0.4f);

                if (scale > 0f) continue;
                view.gameObject.SetActive(false);
                _pigDiedAt[i] = -1f;
            }
        }

        private static void HideView(List<Transform> views, int index)
        {
            if (index < 0 || index >= views.Count) return;
            var view = views[index];
            if (view == null) return;
            view.gameObject.SetActive(false);
        }

        /// <summary>Bursts a block into little fragments that fly out and fade — the "破坏感" the
        /// plain vanish never had. The fragments are driven by <see cref="TickDebris"/>.</summary>
        private void BurstBlock(int index)
        {
            var view = index >= 0 && index < _blockViews.Count ? _blockViews[index] : null;
            Vector3 pos = view != null ? view.position : new Vector3(_level.Blocks[index].X, _level.Blocks[index].Y, 0f);
            Color color = ColourFor(_level.Blocks[index].Kind);
            HideView(_blockViews, index);

            int pieces = 7;
            for (int i = 0; i < pieces; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Debris";
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.16f;
                Paint(go, color);
                Destroy(go.GetComponent<Collider>());

                float angle = (float)i / pieces * Mathf.PI * 2f + Random.value * 0.6f;
                float speed = 2.6f + Random.value * 3.4f;
                _debris.Add(new Debris
                {
                    Transform = go.transform,
                    Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    Age = 0f
                });
            }
        }

        private void TickDebris(float dt)
        {
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var d = _debris[i];
                if (d == null || d.Transform == null) { _debris.RemoveAt(i); continue; }
                d.Age += dt;
                d.Velocity += new Vector3(0f, -14f * dt, 0f);   // gravity
                d.Transform.position += d.Velocity * dt;

                const float life = 0.6f;
                if (d.Age >= life) { Destroy(d.Transform.gameObject); _debris.RemoveAt(i); continue; }
                float fade = 1f - d.Age / life;
                d.Transform.localScale = Vector3.one * 0.16f * fade;
            }
        }

        private void FinishCleared()
        {
            State = Phase.Cleared;

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
