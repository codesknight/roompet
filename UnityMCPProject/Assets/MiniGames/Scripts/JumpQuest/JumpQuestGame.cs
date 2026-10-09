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
    /// The shape is the same as the other games, deliberately: <see cref="HopRules"/> is pure
    /// arithmetic with all the tests, this class only builds boxes and animates one arc, and the
    /// HUD is IMGUI. Nothing here decides anything the rules could have decided.
    ///
    /// Three things about this scene are the result of what the player reported and are worth not
    /// undoing:
    ///
    /// <list type="bullet">
    /// <item>The hop is a line <b>to the next box</b>, not along +X. The animal used to travel a fixed
    /// distance in +X while the boxes drifted sideways, so it visibly flew past boxes it had
    /// "landed" on.</item>
    /// <item>The camera is <b>computed from the animal and both boxes</b> every frame, and is
    /// smoothed but never allowed to lose the animal — see <see cref="UpdateView"/>.</item>
    /// <item>Boxes behind the animal are destroyed. The first version kept every box of the run
    /// alive for the whole run, which is two primitives and a material per hop.</item>
    /// </list>
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

        /// <summary>
        /// Where the animal is standing, on the plane the boxes are laid out on: x is the line, y is
        /// the sideways drift. A <c>Vector2</c> rather than a float because a hop is a line to a box
        /// and "where the animal is" has to be able to say "beside it".
        /// </summary>
        public Vector2 Position { get; private set; }

        /// <summary>How far the next box is from where the animal is standing — the number the game is about.</summary>
        public float NextDistance => NextBox == null ? 0f : (NextBox.Centre - Position).magnitude;

        /// <summary>Set briefly after a perfect landing, for the "+2" popup.</summary>
        public string LastPopup { get; private set; } = "";

        private float _popupUntil;

        private HopSettings _settings = HopSettings.Default;
        private readonly System.Random _rng = new System.Random();

        private class Box
        {
            public Transform Transform;
            public Vector2 Centre;   // middle of the box, on the hopping plane
            public float Size;       // half-extent
            public float Height;
        }

        private readonly List<Box> _boxes = new List<Box>();
        private Transform _player;
        private Transform _shadow;
        private Camera _camera;
        private float _lastAspect;

        private Vector2 _standFrom;
        private Vector2 _standTo;
        private float _hopT;
        private float _deathAt;
        private bool _wasHeld;
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

        // ---- the view ----
        // The camera is placed by arithmetic (see HopRules.Frame), so these are the only numbers
        // left to choose. The yaw is steep on purpose: the hop then runs *up* the screen, which is
        // the axis a phone in portrait has spare. The first version used a shallow yaw and a zoom
        // that took the *wider* of two demands, which on a portrait phone zoomed out until the toy
        // was a strip down the middle of the screen.
        private static readonly Vector3 ViewEuler = new Vector3(27f, 56f, 0f);
        private const float ViewPadding = 0.5f;
        private const float MinViewSize = 3.5f;
        private const float MaxViewSize = 11f;
        private const float EyeDistance = 26f;
        private const float BoxHeight = 1.1f;
        private const float AnimalHeight = 1.05f;

        private readonly List<Vector3> _viewPoints = new List<Vector3>(8);
        private Vector3 _viewFocus;
        private float _viewSize;
        private bool _viewReady;

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
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.19f, 0.28f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.transform.rotation = Quaternion.Euler(ViewEuler);
            _camera = camera;
            _lastAspect = camera.aspect;

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
            BuildAimLine();
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
            DshMobile.MiniAnimal.Build(root, _kind, AnimalHeight);

            // A soft blob under its feet: without it the animal is hard to place against a box of a
            // similar colour, and "am I on the box or beside it" is the whole game. (The first
            // version of this was a flat black cylinder — legible, but it read as a hole cut in the
            // box rather than as a shadow.)
            _shadow = DshMobile.SoftShadow.Attach(root, 0.30f, 0.30f, 0.45f);

            return root;
        }

        /// <summary>
        /// A thin line on the box tops from the animal to the box it is going to.
        ///
        /// It exists because the player could not tell where the animal was about to go: the hop used
        /// to be a fixed direction, and even now, with the hop aimed at the box, "aimed at what?" is
        /// worth answering on screen rather than in a comment. It is also the honest version of the
        /// promise — if the line points at a box, that hop is reachable.
        /// </summary>
        private void BuildAimLine()
        {
            var go = new GameObject("AimLine");
            go.transform.SetParent(transform, false);

            _aim = go.AddComponent<LineRenderer>();
            _aim.useWorldSpace = true;
            _aim.positionCount = 0;
            _aim.startWidth = 0.05f;
            _aim.endWidth = 0.05f;
            _aim.numCapVertices = 2;
            _aim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aim.receiveShadows = false;
            _aim.alignment = LineAlignment.View;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                // No shader to draw it with: the game is still complete without the hint line.
                Destroy(go);
                _aim = null;
                return;
            }

            var material = new Material(shader) { name = "Dsh_HopAim" };

            // Opaque white in the material, transparency in the vertex colours: the two multiply, so
            // a translucent material colour would have made an already-faint line invisible (which is
            // exactly what the first version of this drew — nothing).
            material.color = Color.white;
            _aim.material = material;
            _aim.startColor = new Color(1f, 0.97f, 0.82f, 0.20f);
            _aim.endColor = new Color(1f, 0.97f, 0.82f, 0.75f);
        }

        private LineRenderer _aim;

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
            // colour when one of them did. "Few" is now true — boxes behind the animal are recycled.
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.32f);
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ boxes

        private Box SpawnBox(Vector2 centre, float size, float height, Color tint)
        {
            var root = new GameObject("Box").transform;
            root.SetParent(transform, false);

            var cube = ShapeParented(root, "Block", new Vector3(size * 2f, height, size * 2f), tint);
            cube.localPosition = new Vector3(0f, height * 0.5f, 0f);

            // A lighter top face, so the box reads as a box from a three-quarter camera.
            var top = ShapeParented(root, "Top", new Vector3(size * 2f, 0.06f, size * 2f),
                Color.Lerp(tint, Color.white, 0.28f));
            top.localPosition = new Vector3(0f, height + 0.03f, 0f);

            root.position = new Vector3(centre.x, 0f, centre.y);
            return new Box { Transform = root, Centre = centre, Size = size, Height = height };
        }

        private void ClearBoxes()
        {
            for (int i = 0; i < _boxes.Count; i++)
            {
                if (_boxes[i].Transform != null) Destroy(_boxes[i].Transform.gameObject);
            }
            _boxes.Clear();
        }

        private Box CurrentBox => _boxes.Count == 0
            ? null
            : _boxes[Mathf.Clamp(_boxIndex, 0, _boxes.Count - 1)];

        private Box NextBox => _boxes.Count > _boxIndex + 1 ? _boxes[_boxIndex + 1] : null;

        /// <summary>
        /// Lays the next box.
        ///
        /// The gap is drawn from <see cref="HopRules.NextGap"/> including the sideways offset the new
        /// box will have, because the hop is a line to the box: a box 0.6 units to the side adds 0.6
        /// units to the length the animal has to travel, and the first version's reachability check
        /// never noticed.
        /// </summary>
        private void SpawnNext()
        {
            var previous = _boxes[_boxes.Count - 1];

            float drift = HopRules.NextDrift((float)_rng.NextDouble(), _settings);
            float sideways = Mathf.Abs(drift - previous.Centre.y);
            float gap = HopRules.NextGap((float)_rng.NextDouble(), Score, sideways, _settings);

            _colour = (_colour + 1) % Palette.Length;
            var centre = new Vector2(previous.Centre.x + gap, drift);

            _boxes.Add(SpawnBox(centre, _settings.BoxHalfSize, BoxHeight, Palette[_colour]));
        }

        /// <summary>
        /// Throws away the boxes the animal has already left behind.
        ///
        /// The first version grew the list forever: 40 hops meant 80 primitives, 40 materials and 40
        /// transforms, all still alive somewhere off to the left of a camera that had long since
        /// moved on. Three behind the animal is what the camera can ever see, plus the two that make
        /// the reasoning here easy to follow.
        /// </summary>
        private void PruneBehind()
        {
            while (_boxes.Count > 5 && _boxIndex >= 2)
            {
                var oldest = _boxes[0];
                if (oldest.Transform != null) Destroy(oldest.Transform.gameObject);

                _boxes.RemoveAt(0);
                _boxIndex--;
            }
        }

        // ------------------------------------------------------------------ run control

        public void ResetRun()
        {
            ClearBoxes();
            Score = 0;
            _boxIndex = 0;
            Held = 0f;
            Position = Vector2.zero;
            _standFrom = Vector2.zero;
            _standTo = Vector2.zero;
            _hopT = 0f;
            _colour = 0;
            _fallY = 0f;
            State = Phase.Ready;
            LastPopup = "";

            _boxes.Add(SpawnBox(Vector2.zero, _settings.BoxHalfSize, BoxHeight, Palette[0]));
            SpawnNext();

            ApplyPlayer(Vector3.zero, 0f);
            SnapView();
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

            var target = NextBox;
            float distance = HopRules.HopDistance(Held, _settings);

            _standFrom = Position;
            _standTo = target == null
                ? Position + new Vector2(distance, 0f)
                : HopRules.AimedLanding(Position, target.Centre, distance);
            _hopT = 0f;
            Held = 0f;
            State = Phase.Flying;

            DshMobile.MobileHaptics.Light();

            // The box under the player squashes as they leave it: the tiny piece of feedback that
            // makes a hop feel like it had weight.
            var from = CurrentBox;
            if (from != null && from.Transform != null) StartCoroutine(Squash(from.Transform));
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
            var next = NextBox;
            var landing = next == null
                ? HopRules.Landing.Missed
                : HopRules.JudgeAt(_standTo, next.Centre, _settings);

            if (landing == HopRules.Landing.Missed)
            {
                Die();
                return;
            }

            Position = _standTo;
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
            PruneBehind();
            State = Phase.Ready;

            // Standing where it came down, not in the middle of the box: the next hop is measured
            // from the animal, so moving it to the middle would be the game quietly helping. The
            // generator is what has to cope with a landing off-centre, and it does.
            ApplyPlayer(new Vector3(Position.x, 0f, Position.y), 0f);
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

        // ------------------------------------------------------------------ the animal
        //
        // Where the animal is standing, in the plane, versus where the eye is: the two are kept
        // apart on purpose. Position is a game fact (it decides the next hop); the eye only has to
        // keep it on screen.

        /// <summary>The direction the animal is facing: at the next box, or along the hop in flight.</summary>
        private Vector2 Facing
        {
            get
            {
                if (State == Phase.Flying)
                {
                    var travel = _standTo - _standFrom;
                    if (travel.sqrMagnitude > 0.0001f) return travel.normalized;
                }

                var next = NextBox;
                if (next != null)
                {
                    var towards = next.Centre - Position;
                    if (towards.sqrMagnitude > 0.0001f) return towards.normalized;
                }

                return Vector2.right;
            }
        }

        /// <summary>
        /// Places the animal. <paramref name="ground"/> is where its feet are on the hopping plane
        /// (x, z); its height above the box tops is the arc, so the animation cannot disagree with
        /// the landing the rules already judged.
        /// </summary>
        private void ApplyPlayer(Vector3 ground, float flightT)
        {
            if (_player == null) return;
            if (State != Phase.Dead) _fallY = 0f;

            var box = CurrentBox;
            float baseHeight = box != null ? box.Height : 0f;

            _player.position = new Vector3(ground.x, baseHeight + ground.y, ground.z);

            var facing = Facing;

            // MiniAnimal is built facing -Z, so facing a direction means looking along its negative.
            var look = Quaternion.LookRotation(new Vector3(-facing.x, 0f, -facing.y));

            // Leaning into the hop, about the axis across the direction of travel: it leaves tipped
            // back (a jump starts by pushing off) and arrives tipped forward.
            float lean = State == Phase.Flying ? Mathf.Lerp(-9f, 13f, Mathf.Clamp01(flightT)) : 0f;
            var axis = Vector3.Cross(Vector3.up, new Vector3(facing.x, 0f, facing.y));

            _player.rotation = axis.sqrMagnitude > 0.0001f
                ? Quaternion.AngleAxis(lean, axis.normalized) * look
                : look;

            KeepShadowDown(baseHeight);
        }

        /// <summary>
        /// Keeps the contact shadow on the box tops instead of riding up with the animal.
        ///
        /// A shadow that follows its character into the air is the classic tell that it is a sticker:
        /// it stops being a shadow and becomes a second body. It is a child of the animal, so its
        /// world position (and its flat rotation, which would otherwise tilt with the lean) is set
        /// back down every frame.
        /// </summary>
        private void KeepShadowDown(float baseHeight)
        {
            if (_shadow == null) return;

            _shadow.position = new Vector3(_player.position.x, baseHeight + 0.016f, _player.position.z);
            _shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ------------------------------------------------------------------ camera

        /// <summary>Frames the view immediately, with no smoothing: for a reset or a rotation.</summary>
        private void SnapView()
        {
            _viewReady = false;
            UpdateView(0f);
        }

        /// <summary>
        /// Puts the camera where it can see the animal and both boxes, and keeps it there.
        ///
        /// The wanted frame is computed by <see cref="HopRules.Frame"/> from three points — the
        /// animal, the box it is on or leaving, and the box it is going to — so the answer is right
        /// for any aspect ratio rather than for the one the editor happens to have. The camera then
        /// follows smoothly, *except* that the smoothing is abandoned the moment it would let the
        /// animal get near the edge: a camera that lags prettily and loses the character is the bug
        /// this code exists to fix.
        /// </summary>
        private void UpdateView(float deltaTime)
        {
            if (_camera == null) return;

            if (!Mathf.Approximately(_lastAspect, _camera.aspect))
            {
                _lastAspect = _camera.aspect;
                _viewReady = false;   // a rotated phone gets a correct frame, not a slow drift to one
            }

            var rotation = Quaternion.Euler(ViewEuler);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;

            var animal = _player != null
                ? _player.position
                : new Vector3(Position.x, BoxHeight, Position.y);

            // The feet and the head, so a tall animal is not framed by its ankles.
            _viewPoints.Clear();
            _viewPoints.Add(animal);
            _viewPoints.Add(animal + Vector3.up * AnimalHeight);

            var current = CurrentBox;
            if (current != null)
            {
                // The box under the animal is framed by its middle, not by its corners: it is behind
                // the action, and demanding its far corner on screen pushes the whole toy away from
                // the camera for nothing. (The first version framed both boxes by their middles and
                // cut the *next* box's corner off at the edge; the second framed both by their
                // corners and zoomed 45% further out. This is the middle of the two, which is where
                // the animal is still big enough to read on a phone.)
                _viewPoints.Add(new Vector3(current.Centre.x, 0f, current.Centre.y));
                _viewPoints.Add(new Vector3(current.Centre.x, current.Height, current.Centre.y));
            }

            var next = NextBox;
            if (next != null)
            {
                AddBoxCorners(next.Centre, next.Size, next.Height);

                // A point a third of the way to the next box. It is already inside the frame — it
                // lies between two points that are — so it cannot make the view bigger; all it does
                // is pull the middle of the frame forward, which is what keeps the animal low in the
                // picture with the box it is about to jump to in front of it.
                _viewPoints.Add(Vector3.Lerp(animal,
                    new Vector3(next.Centre.x, next.Height, next.Centre.y), 0.35f));
            }

            Vector3 wanted;
            float wantedSize;
            HopRules.Frame(_viewPoints, right, up, _camera.aspect, ViewPadding, MinViewSize,
                MaxViewSize, out wanted, out wantedSize);

            if (_viewReady)
            {
                float k = 1f - Mathf.Exp(-7f * Mathf.Max(0f, deltaTime));
                _viewFocus = Vector3.Lerp(_viewFocus, wanted, k);
                _viewSize = Mathf.Lerp(_viewSize, wantedSize, k);
            }
            else
            {
                _viewFocus = wanted;
                _viewSize = wantedSize;
                _viewReady = true;
            }

            var offset = HopRules.ViewOffset(animal, _viewFocus, right, up, _viewSize, _camera.aspect);
            if (Mathf.Abs(offset.x) > 0.85f || Mathf.Abs(offset.y) > 0.85f)
            {
                // The lag has run out of room: follow exactly, and grow immediately if the frame the
                // lag is still using is too small. Shrinking stays smooth, so the view never snaps
                // inwards on the animal.
                _viewFocus = wanted;
                _viewSize = Mathf.Max(_viewSize, wantedSize);
            }

            _camera.transform.rotation = rotation;
            _camera.transform.position = _viewFocus - forward * EyeDistance;
            _camera.orthographicSize = _viewSize;

            // The hint line: from the animal's feet to the middle of the box it is aimed at. Drawn
            // just above the box tops, so it reads as "this is the hop" rather than as a laser
            // across the room — and clear of the tops themselves, or it z-fights with them.
            if (_aim != null)
            {
                float height = BoxHeight + 0.16f;
                if (next != null && State != Phase.Dead)
                {
                    _aim.positionCount = 2;
                    _aim.SetPosition(0, new Vector3(Position.x, height, Position.y));
                    _aim.SetPosition(1, new Vector3(next.Centre.x, height, next.Centre.y));
                }
                else
                {
                    _aim.positionCount = 0;
                }
            }
        }

        /// <summary>
        /// Adds a box's eight corners to the points the camera must contain.
        ///
        /// The corners rather than the middle, because a box is a two-metre cube seen at an angle: the
        /// first version framed the box <i>centres</i>, and the corner of the next box was cut off by
        /// the edge of the screen — which reads as a sloppy camera rather than as a deliberate one.
        /// </summary>
        private void AddBoxCorners(Vector2 centre, float half, float height)
        {
            for (int i = 0; i < 4; i++)
            {
                float dx = (i == 0 || i == 3) ? -half : half;
                float dz = i < 2 ? -half : half;

                _viewPoints.Add(new Vector3(centre.x + dx, 0f, centre.y + dz));
                _viewPoints.Add(new Vector3(centre.x + dx, height, centre.y + dz));
            }
        }

        // ------------------------------------------------------------------ loop

        /// <summary>Held input, pushed by the HUD's touch pad.</summary>
        public bool HoldInput { get; set; }

        /// <summary>One-shot "the hold ended", pushed by the HUD when a finger lifts.</summary>
        public bool ReleaseInput { get; set; }

        private void Update()
        {
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
                    ApplyPlayer(HopRules.ArcPosition(_standFrom, _standTo, _hopT, _settings), _hopT);
                }
            }

            if (State == Phase.Dead) TickFall();

            if (!string.IsNullOrEmpty(LastPopup) && Time.time > _popupUntil) LastPopup = "";

            UpdateView(Time.deltaTime);
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

            var box = CurrentBox;
            float baseHeight = box != null ? box.Height : 0f;

            _player.position = new Vector3(_standTo.x, baseHeight + _fallY, _standTo.y);

            var facing = Facing;
            var look = Quaternion.LookRotation(new Vector3(-facing.x, 0f, -facing.y));
            var axis = Vector3.Cross(Vector3.up, new Vector3(facing.x, 0f, facing.y));
            float tip = Mathf.Lerp(0f, 65f, Mathf.Clamp01(-_fallY / 3.4f));

            _player.rotation = axis.sqrMagnitude > 0.0001f
                ? Quaternion.AngleAxis(tip, axis.normalized) * look
                : look;

            KeepShadowDown(baseHeight);
        }

        /// <summary>Goes home, the same way the other games do.</summary>
        public void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the hop game");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }
    }
}
