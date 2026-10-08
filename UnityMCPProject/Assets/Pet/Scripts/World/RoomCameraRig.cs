using UnityEngine;

namespace DshPet
{
    /// <summary>How the room camera behaves. Chosen from the HUD and remembered between runs.</summary>
    public enum CameraViewMode
    {
        /// <summary>The whole room in one frame, fixed. Best for watching the pet wander.</summary>
        Panorama = 0,

        /// <summary>Player-driven: right-drag to orbit, wheel to zoom, middle-drag to pan.</summary>
        Free = 1,

        /// <summary>Rides behind the player character. Best for walking around yourself.</summary>
        FollowPlayer = 2
    }

    /// <summary>
    /// Frames the room.
    ///
    /// The camera sits OUTSIDE the room and looks in over the near wall, which
    /// <see cref="PetRoom.CullFacesForCamera"/> hides. Two earlier setups failed here:
    /// a pure follow-camera lost the pet the moment the player walked off, and an
    /// inside-the-room rig had to stay within the walls, so the player could stand almost
    /// under the lens and slide off the bottom of the screen, right behind the chat panel.
    ///
    /// All three view modes share one guarantee: whatever the camera is asked to show is
    /// measured every frame against the band the HUD leaves free, and the pitch is corrected
    /// until it sits inside it. That is why the modes differ only in what they point at and
    /// how far away they sit, not in how they keep it visible.
    /// </summary>
    public class RoomCameraRig : MonoBehaviour
    {
        [Header("Wiring")]
        public Transform Player;
        public Transform Pet;

        [Header("View")]
        public CameraViewMode View = CameraViewMode.Panorama;

        [Tooltip("Remembered across runs.")]
        public string ViewPrefKey = "dshpet.view";

        [Header("Framing (screen-fit modes)")]
        [Tooltip("Height above the subjects' midpoint when they are standing together.")]
        public float BaseHeight = 7.5f;

        [Tooltip("Distance behind the midpoint when they are standing together.")]
        public float BaseBack = 10f;

        [Tooltip("Extra metres of height/distance per metre the two characters are apart. " +
                 "Without this the near character sinks behind the chat panel as they separate. " +
                 "Sized so a diagonal split still clears the panel, which is the worst case.")]
        public float SeparationGain = 1.15f;

        [Tooltip("In follow mode, frame the pet as well while it is this close to the player.")]
        public float FollowPairRange = 6f;

        public float MaxZoomOut = 16f;

        public Vector3 LookOffset = new Vector3(0f, 0.6f, 0f);

        /// <summary>0 frames the player, 1 frames the pet. The midpoint keeps both visible.</summary>
        [Range(0f, 1f)] public float PetBias = 0.5f;

        public float FollowSmoothing = 3.5f;
        public float LookSmoothing = 5f;

        [Header("Panorama")]
        [Tooltip("Camera placement for the whole-room view, relative to the room centre. " +
                 "Steep and far so a 14x14 room fits above the chat panel with margin.")]
        public float PanoramaHeight = 21f;
        public float PanoramaBack = 17f;

        [Header("Free camera")]
        public float FreeDistance = 14f;
        public float FreeMinDistance = 5f;
        public float FreeMaxDistance = 34f;
        public float FreePitch = 45f;
        public float FreeMinPitch = 12f;
        public float FreeMaxPitch = 82f;
        public float FreeOrbitSpeed = 4.5f;
        public float FreeZoomSpeed = 1.4f;
        public float FreePanSpeed = 0.02f;

        [Header("Boundaries")]
        [Tooltip("The camera may sit outside the room; the near wall is culled for it.")]
        public float RoomLimit = 24f;

        [Header("HUD framing")]
        [Tooltip("Centre the subjects in the free part of the screen instead of the screen " +
                 "centre, so the chat panel does not sit on top of the player character.")]
        public bool RespectHudSafeArea = true;

        [Tooltip("Where in the free band the subjects start out, 0 = top of the band, 1 = bottom. " +
                 "The closed-loop fit below finishes the job.")]
        [Range(0.3f, 0.9f)] public float FocusBandPosition = 0.55f;

        /// <summary>
        /// Extra downward pitch, in degrees, that lifts the subject above the chat panel.
        ///
        /// Pitching the camera down moves the world up on screen. The value is not solved
        /// analytically: the rig measures where the subject actually lands and corrects, which
        /// is the only way to hold the invariant for every view mode, separation and HUD height
        /// without a table of magic numbers. Exposed for the wiring report.
        /// </summary>
        public float HudPitchDegrees { get; private set; }

        /// <summary>Bottom/top margins inside the free band, as fractions of the band.</summary>
        public float BandBottomMargin = 0.06f;
        public float BandTopMargin = 0.94f;

        [Tooltip("Test/override seam: height in pixels of the HUD-free band. 0 uses the live HUD.")]
        public float BandHeightOverride;

        private Vector3 _lookPoint;
        private Vector3 _freeFocus = Vector3.zero;
        private float _freeYaw;
        private float _freePitch;
        private float _freeDistance;
        private bool _freeInitialised;
        private Camera _camera;

        /// <summary>
        /// The camera, resolved on demand.
        ///
        /// Not just cached in Awake: an EditMode test (or anything that adds this component
        /// outside play mode) never runs Awake, and a null camera silently disables the whole
        /// screen fit - which is a very quiet way for the framing to stop working.
        /// </summary>
        private Camera Cam
        {
            get
            {
                if (_camera == null) _camera = GetComponent<Camera>();
                if (_camera == null) _camera = gameObject.AddComponent<Camera>();
                return _camera;
            }
        }

        /// <summary>Pixel height the camera projects into; normally the screen height.</summary>
        private float ProjectionHeight
            => _camera != null && _camera.pixelHeight > 0 ? _camera.pixelHeight : Screen.height;

        /// <summary>
        /// The HUD's free band, expressed in the camera's own pixels.
        ///
        /// The band comes from the HUD in screen pixels while projection is in camera pixels;
        /// those are the same on screen but not necessarily anywhere else, and mixing them puts
        /// the fit loop into a space where it cannot converge.
        /// </summary>
        private float BandInProjectionPixels()
        {
            float band = HudBandHeight();
            if (band <= 0f) return 0f;
            return band * (ProjectionHeight / Mathf.Max(1f, Screen.height));
        }

        /// <summary>The points the fit keeps inside the band; reused to avoid per-frame garbage.</summary>
        private readonly Vector3[] _subjects = new Vector3[4];

        /// <summary>How many entries of <see cref="_subjects"/> the last collect filled in.</summary>
        private int _subjectCount;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
        }

        private void Start()
        {
            Resolve();
            LoadView();
            Snap();
        }

        // ------------------------------------------------------------------- view modes

        /// <summary>Switches mode, snapping so the change is immediate rather than a drift.</summary>
        public void SetView(CameraViewMode mode)
        {
            if (View == mode) return;
            View = mode;
            SaveView();

            if (mode == CameraViewMode.Free)
            {
                // Start the free camera where the room view already is, so switching does not
                // throw the player somewhere unrecognisable.
                _freeFocus = RoomCentre();
                _freeYaw = 0f;
                _freePitch = FreePitch;
                _freeDistance = FreeDistance;
                _freeInitialised = true;
            }

            Resolve();
            Snap();
        }

        public void LoadView()
        {
            if (!PlayerPrefs.HasKey(ViewPrefKey) && DshMobile.MobileUi.IsPortrait)
            {
                // First run on a phone held upright: start close to the pet rather than with the
                // whole room.
                //
                // "Whole room in one frame" cannot also mean "pet big enough to read" on a
                // 0.45-aspect screen — measured, the steeper portrait panorama still leaves the
                // pet around 40 screen pixels tall. Follow mode is the mode a phone actually
                // wants, and the player who prefers the doll house can still switch to 全景 at
                // any time; this only decides where they start.
                View = CameraViewMode.FollowPlayer;
                SaveView();
            }

            int saved = PlayerPrefs.GetInt(ViewPrefKey, (int)View);
            View = (CameraViewMode)Mathf.Clamp(saved, 0, 2);

            if (View == CameraViewMode.Free && !_freeInitialised)
            {
                _freeFocus = RoomCentre();
                _freeYaw = 0f;
                _freePitch = FreePitch;
                _freeDistance = FreeDistance;
                _freeInitialised = true;
            }
        }

        private void SaveView()
        {
            PlayerPrefs.SetInt(ViewPrefKey, (int)View);
            PlayerPrefs.Save();
        }

        /// <summary>Human-readable mode name for the HUD.</summary>
        public static string ViewLabel(CameraViewMode mode)
        {
            switch (mode)
            {
                case CameraViewMode.Free: return "自由";
                case CameraViewMode.FollowPlayer: return "跟随主角";
                default: return "全景";
            }
        }

        private Vector3 RoomCentre()
        {
            var gm = PetGameManager.Instance;
            var room = gm != null ? gm.Room : null;
            return room != null ? room.transform.position : Vector3.zero;
        }

        // ---------------------------------------------------------------------- framing

        private float HudBandHeight()
        {
            if (BandHeightOverride > 0f) return BandHeightOverride;
            if (PetHud.Exists) return PetHud.ComputeLayout().ChatTop;
            return 0f;
        }

        /// <summary>
        /// Width of the room's free band, in screen pixels; 0 means "the whole viewport".
        ///
        /// The camera used to assume the free area was "everything above the chat panel". That
        /// stopped being true the moment the transcript moved into a side column — the band is
        /// now wide and short in one layout and tall and narrow in the other, and only the HUD
        /// knows which. Feeding the real width into the horizontal fit is what keeps the room
        /// out from under the sidebar.
        /// </summary>
        private float HudBandWidth()
        {
            if (!PetHud.Exists) return 0f;
            var band = PetHud.FreeBandScreen();
            return band.width > 40f && band.width < Screen.width - 1f ? band.width : 0f;
        }

        /// <summary>Starting pitch for the current viewport and HUD, before the fit below.</summary>
        private void UpdateHudPitch()
        {
            // The free camera is aimed by the player; stacking an invisible offset on top of
            // their pitch would make the drag feel like it fights back.
            if (View == CameraViewMode.Free)
            {
                HudPitchDegrees = 0f;
                return;
            }

            float focusFraction = 0.5f;
            float band = HudBandHeight();

            if (RespectHudSafeArea && band > 40f)
            {
                focusFraction = Mathf.Clamp((band / Mathf.Max(1f, Screen.height)) * FocusBandPosition,
                    0.15f, 0.5f);
            }

            float fov = Cam.fieldOfView;
            HudPitchDegrees = (0.5f - focusFraction) * fov;
        }

        public void Resolve()
        {
            var gm = PetGameManager.Instance;

            if (Player == null)
            {
                var controller = FindObjectOfType<PlayerRoomController>();
                if (controller != null) Player = controller.transform;
            }
            if (Pet == null && gm != null && gm.Avatar != null) Pet = gm.Avatar.transform;
        }

        /// <summary>The point the camera looks at, per mode.</summary>
        public Vector3 FocusPoint()
        {
            switch (View)
            {
                case CameraViewMode.Free:
                    return _freeFocus;

                case CameraViewMode.FollowPlayer:
                    return Player != null ? Player.position : RoomCentre();

                default: // Panorama
                    return RoomCentre();
            }
        }

        /// <summary>How far apart the two characters are, on the floor plane.</summary>
        public float Separation()
        {
            if (Player == null || Pet == null) return 0f;
            Vector3 a = Player.position, b = Pet.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Camera offset for the current mode.
        ///
        /// The screen-fit modes pull further and higher the further apart the two characters
        /// are, which is what keeps BOTH of them inside the band above the chat panel: a fixed
        /// offset cannot, and at 12m apart the near character drops off the bottom.
        /// </summary>
        public Vector3 CameraOffset()
        {
            switch (View)
            {
                case CameraViewMode.Free:
                {
                    var rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
                    return rotation * (Vector3.back * _freeDistance);
                }

                case CameraViewMode.FollowPlayer:
                {
                    // When the pet is close enough to be framed too, pull further back and
                    // higher the further apart the pair is: with a fixed offset the near one
                    // drops off the bottom. With only the player framed there is no spread.
                    float spread = Separation() < FollowPairRange ? Separation() : 0f;
                    float extra = Mathf.Min(MaxZoomOut, spread * SeparationGain);
                    return new Vector3(0f, BaseHeight + extra * 0.95f, -(BaseBack + extra));
                }

                default: // Panorama
                {
                    // Pull back a little when the HUD eats more of the screen, so the whole
                    // room still fits the band.
                    float band = HudBandHeight();
                    float bandFraction = band > 40f ? band / Mathf.Max(1f, Screen.height) : 0.64f;
                    float scale = Mathf.Clamp(0.64f / Mathf.Max(0.2f, bandFraction), 1f, 1.7f);

                    // ...and pull back further on a narrow viewport. The room is 14 m wide, and
                    // a camera defined by a VERTICAL field of view loses horizontal coverage
                    // exactly as the viewport narrows, so in portrait the side walls get cut
                    // off no matter how the pitch is tuned.
                    scale *= Mathf.Max(1f, _widthFit);

                    // On a narrow screen, sweep the same doll-house view towards top-down.
                    //
                    // A square room seen from a shallow angle projects into a wide, SHORT strip
                    // — measured at 87% of a portrait phone's width but only 28% of its height,
                    // so three quarters of the screen was empty sky and floor. Looking down more
                    // projects the floor plan into the screen's height instead, which is the
                    // shape a tall viewport actually has. The width fit still applies on top, so
                    // the room keeps fitting the width either way.
                    float steepness = Mathf.InverseLerp(1.5f, 0.65f, Cam.aspect);
                    float height = PanoramaHeight * Mathf.Lerp(1f, PortraitHeightScale, steepness);
                    float back = PanoramaBack * Mathf.Lerp(1f, PortraitBackScale, steepness);

                    return new Vector3(0f, height * scale, -back * scale);
                }
            }
        }

        [Header("Portrait framing")]
        [Tooltip("How much taller the panorama camera sits on a narrow screen. Together with " +
                 "PortraitBackScale this tilts the view towards top-down so a tall viewport is " +
                 "filled by the floor plan instead of by empty sky.")]
        public float PortraitHeightScale = 1.55f;

        [Tooltip("How much closer in the panorama camera sits on a narrow screen.")]
        public float PortraitBackScale = 0.45f;

        /// <summary>
        /// Pull-back last computed by <see cref="CameraOffset"/>, used to relax the boundary.
        ///
        /// <see cref="RoomLimit"/> exists to stop the player panning the FREE camera off into the
        /// void. A screen-fit mode is not the player's doing — it is the framing solving for
        /// itself — and in portrait that solution wants to sit further back than the limit. Left
        /// clamped, the room's near corner ended up 22px off the left edge of a 1080px screen.
        /// </summary>
        private float _widthFit = 1f;

        /// <summary>Half the room's floor width, i.e. how far a corner sits from the centre.</summary>
        private float RoomHalfWidth()
        {
            var room = PetGameManager.Instance != null ? PetGameManager.Instance.Room : null;
            float size = room != null ? room.Size : 14f;
            // A little under half, because the corners are what is being kept in frame and
            // the margin in WidthFitScale is a fraction of the visible width, not a distance.
            return size * 0.5f - 0.6f;
        }

        public void Snap()
        {
            Resolve();
            UpdateHudPitch();
            _widthFit = SeedWidthFit();
            Vector3 focus = FocusPoint();
            transform.position = Clamp(focus + CameraOffset());
            _lookPoint = focus + LookOffset;
            FitSubjectsToBand();          // leaves the camera aimed with the fitted pitch
            FitSubjectsToWidth();         // ...and far enough back for a narrow viewport
            ApplyPitch();
            CullRoomFaces();
        }

        /// <summary>
        /// Starting guess for the horizontal fit, from the room's own size.
        ///
        /// The measurement loop below is what actually holds the invariant; this exists so the
        /// first frame after a snap is already close, instead of the camera visibly flying
        /// backwards over the first few frames.
        /// </summary>
        private float SeedWidthFit()
        {
            if (View != CameraViewMode.Panorama) return 1f;

            // The fit is measured against the HUD's free band, so the seed has to use the same
            // width — otherwise the first frame after a snap is framed for the whole viewport
            // and the room visibly jumps when the sidebar starts being accounted for.
            float bandWidth = HudBandWidth();
            float aspect = bandWidth > 40f
                ? bandWidth * Cam.aspect / Mathf.Max(1f, ProjectionWidth)
                : Cam.aspect;

            return DshMobile.MobileUi.WidthFitScale(
                Mathf.Sqrt(PanoramaBack * PanoramaBack + PanoramaHeight * PanoramaHeight),
                RoomHalfWidth(), Cam.fieldOfView, aspect);
        }

        /// <summary>
        /// Slides the camera back until everything it must show also fits the viewport WIDTH.
        ///
        /// Portrait is the case that needs it: the room is 14 m wide and the viewport is 0.45
        /// wide-to-tall, so the side walls fall off both edges however well the pitch is tuned.
        /// Solved by measurement like the vertical fit, because the near corners subtend more
        /// than the room centre does and an analytic distance lands a few percent short.
        /// </summary>
        private void FitSubjectsToWidth()
        {
            if (!RespectHudSafeArea || View != CameraViewMode.Panorama)
            {
                // The other modes frame characters near the middle of the screen, where a narrow
                // viewport cannot clip them; the free camera belongs to the player.
                _widthFit = 1f;
                return;
            }

            int count = _subjectCount;
            if (count == 0) return;

            // Fit into the band the HUD actually leaves free, not the whole viewport: with the
            // transcript in a side column the room would otherwise be centred under it.
            float bandWidth = HudBandWidth();
            float width = bandWidth > 40f ? bandWidth * (ProjectionWidth / Mathf.Max(1f, Screen.width)) : ProjectionWidth;
            if (width < 40f) return;

            float available = width * (1f - 2f * WidthMargin);

            for (int pass = 0; pass < 4; pass++)
            {
                float left = float.MaxValue, right = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    float x = ScreenXOf(_subjects[i]);
                    if (x < left) left = x;
                    if (x > right) right = x;
                }

                float used = right - left;
                if (used <= 1f) return;

                if (used > available)
                {
                    _widthFit = Mathf.Clamp(_widthFit * (used / available) * 1.02f, 1f,
                        DshMobile.MobileUi.MaxWidthFitPullback);
                }
                else if (used < available * 0.82f)
                {
                    // Come back in when there is room to spare, so turning the phone back to
                    // landscape (or a wider window) restores the original framing rather than
                    // staying zoomed out forever.
                    _widthFit = Mathf.Clamp(_widthFit * Mathf.Max(0.7f, used / available), 1f,
                        DshMobile.MobileUi.MaxWidthFitPullback);
                }
                else
                {
                    return;
                }

                transform.position = Clamp(FocusPoint() + CameraOffset());
            }
        }

        /// <summary>Fraction of the viewport width kept clear at each side of the subject.</summary>
        public float WidthMargin = 0.05f;

        private float ScreenXOf(Vector3 worldPoint) => Cam.WorldToScreenPoint(worldPoint).x;

        /// <summary>Viewport width in the camera's own pixels (see BandInProjectionPixels).</summary>
        private float ProjectionWidth
            => _camera != null && _camera.pixelWidth > 0 ? _camera.pixelWidth : Screen.width;

        /// <summary>
        /// Slides the frame until the subjects sit inside the band the HUD leaves free.
        ///
        /// Iterative rather than analytic because the pitch, the projection and the subject
        /// positions interact; a handful of corrections converges and it self-adjusts to any
        /// viewport, chat-panel height or separation. The camera is only slid, never scaled.
        /// </summary>
        private void FitSubjectsToBand()
        {
            if (!RespectHudSafeArea) return;

            float band = BandInProjectionPixels();
            if (band < 40f) return;

            int count = CollectSubjects();
            _subjectCount = count;
            if (count == 0) return;

            float lowerLimit = band * BandTopMargin;    // nearest subject must stay above this
            float upperLimit = band * BandBottomMargin; // furthest must stay below this

            for (int pass = 0; pass < 6; pass++)
            {
                ApplyPitch();

                float nearest = float.MinValue, furthest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    float y = ScreenY(_subjects[i]);
                    if (y > nearest) nearest = y;
                    if (y < furthest) furthest = y;
                }

                float error = 0f;
                if (nearest > lowerLimit) error = nearest - lowerLimit;
                else if (furthest < upperLimit) error = -(upperLimit - furthest);
                if (Mathf.Abs(error) < 0.75f) return;

                // A pixel of error is fov/height degrees of pitch.
                HudPitchDegrees = Mathf.Clamp(
                    HudPitchDegrees + error * (Cam.fieldOfView / Mathf.Max(1f, ProjectionHeight)) * 0.9f,
                    -12f, 55f);
            }
        }

        /// <summary>
        /// Fills <see cref="_subjects"/> with whatever this mode must keep visible, returning
        /// how many entries are valid.
        /// </summary>
        private int CollectSubjects() => Collect();

        private int Collect()
        {
            switch (View)
            {
                case CameraViewMode.Free:
                    return 0;   // the player is driving; do not fight them

                case CameraViewMode.FollowPlayer:
                {
                    // The player, plus the pet while it is close enough to be worth framing.
                    // Aim at chest height: a floor point reads about half a metre lower, which
                    // is the difference between clearing the chat panel and clipping into it.
                    if (Player == null) return 0;
                    _subjects[0] = Player.position + Vector3.up * 0.5f;
                    int n = 1;
                    if (Pet != null && Separation() < FollowPairRange)
                    {
                        _subjects[1] = Pet.position + Vector3.up * 0.5f;
                        n = 2;
                    }
                    return n;
                }

                default: // Panorama: the room's walkable corners, which are floor points
                {
                    var room = PetGameManager.Instance != null ? PetGameManager.Instance.Room : null;
                    float half = (room != null ? room.Size : 14f) * 0.5f - 0.7f;
                    Vector3 c = RoomCentre();
                    _subjects[0] = c + new Vector3(-half, 0f, -half);
                    _subjects[1] = c + new Vector3(half, 0f, -half);
                    _subjects[2] = c + new Vector3(-half, 0f, half);
                    _subjects[3] = c + new Vector3(half, 0f, half);
                    return 4;
                }
            }
        }

        /// <summary>
        /// Distance in pixels from the top of the screen to a subject point. Subjects carry
        /// their own height (chest for a character, floor for a room corner), because mixing
        /// the two is a half-metre error that shows up as the panel clipping the floor.
        /// </summary>
        private float ScreenY(Vector3 worldPoint)
        {
            var screen = Cam.WorldToScreenPoint(worldPoint);
            return ProjectionHeight - screen.y;
        }

        /// <summary>Applies the current look point and pitch. Shared by Snap and the fit loop.</summary>
        private void ApplyPitch()
        {
            Vector3 forward = _lookPoint - transform.position;
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Compose(forward);
        }

        /// <summary>Hide whatever wall stands between this camera and the room.</summary>
        private void CullRoomFaces()
        {
            var room = PetGameManager.Instance != null ? PetGameManager.Instance.Room : null;
            if (room != null) room.CullFacesForCamera(transform);
        }

        /// <summary>Look rotation with the HUD's framing pitch folded in.</summary>
        private Quaternion Compose(Vector3 forward)
        {
            var look = Quaternion.LookRotation(forward, Vector3.up);
            if (Mathf.Abs(HudPitchDegrees) < 0.01f) return look;
            return look * Quaternion.Euler(HudPitchDegrees, 0f, 0f);
        }

        private Vector3 Clamp(Vector3 position)
        {
            float limit = RoomLimit * Mathf.Max(1f, _widthFit);
            return new Vector3(
                Mathf.Clamp(position.x, -limit, limit),
                Mathf.Max(1.5f, position.y),
                Mathf.Clamp(position.z, -limit, limit));
        }

        // ------------------------------------------------------------------- free camera

        /// <summary>
        /// Orbit, zoom and pan.
        ///
        /// Desktop: right-drag orbits, wheel zooms, middle-drag pans — the left button
        /// belongs to throwing the ball and WASD belongs to the character.
        ///
        /// Touch: one finger on the play area orbits (it is the same free finger the
        /// gesture recogniser owns), two fingers pinch to zoom. The thumb on the movement
        /// stick never reaches here because that finger is owned by the UI.
        /// </summary>
        private void TickFreeInput()
        {
            if (View != CameraViewMode.Free) return;

            bool touchDriven = DshMobile.MobileUi.UseTouchControls && DshMobile.MobileTouch.UsingTouch;
            if (touchDriven) TickFreeTouch();
            else TickFreeMouse();

            // Keep the orbit centre over the room so the camera cannot be panned into the void.
            var room = PetGameManager.Instance != null ? PetGameManager.Instance.Room : null;
            float limit = (room != null ? room.Size : 14f) * 0.5f;
            _freeFocus = new Vector3(
                Mathf.Clamp(_freeFocus.x, -limit, limit),
                0f,
                Mathf.Clamp(_freeFocus.z, -limit, limit));
        }

        private void TickFreeMouse()
        {
            if (Input.GetMouseButton(1))
            {
                _freeYaw += Input.GetAxis("Mouse X") * FreeOrbitSpeed;
                _freePitch = Mathf.Clamp(_freePitch - Input.GetAxis("Mouse Y") * FreeOrbitSpeed,
                    FreeMinPitch, FreeMaxPitch);
            }

            if (Input.GetMouseButton(2))
            {
                var rotation = Quaternion.Euler(0f, _freeYaw, 0f);
                Vector3 move = rotation * new Vector3(-Input.GetAxis("Mouse X"), 0f, -Input.GetAxis("Mouse Y"));
                _freeFocus += move * FreePanSpeed * _freeDistance;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _freeDistance = Mathf.Clamp(_freeDistance - scroll * FreeZoomSpeed * _freeDistance,
                    FreeMinDistance, FreeMaxDistance);
            }
        }

        /// <summary>One finger orbits, two fingers pinch. UI fingers are ignored.</summary>
        private void TickFreeTouch()
        {
            int count = Input.touchCount;

            if (count >= 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (DshMobile.MobileTouch.IsUiFinger(Input.GetTouch(i).fingerId)) return;
                }

                Vector2 a = Input.GetTouch(0).position;
                Vector2 b = Input.GetTouch(1).position;
                float distance = Vector2.Distance(a, b);

                if (_lastPinchDistance > 1f)
                {
                    // Pinch out (fingers apart) pulls the camera in, like a map.
                    float ratio = distance / _lastPinchDistance;
                    _freeDistance = Mathf.Clamp(_freeDistance / Mathf.Max(0.5f, ratio),
                        FreeMinDistance, FreeMaxDistance);
                }
                _lastPinchDistance = distance;
                return;
            }

            _lastPinchDistance = 0f;

            // A single free finger: the gesture recogniser is tracking it, and its frame
            // delta is exactly the drag we want.
            var gesture = DshMobile.MobileTouch.Gesture;
            if (!gesture.IsActive) return;

            Vector2 delta = gesture.FrameDelta;
            if (delta.sqrMagnitude < 0.0001f) return;

            // Screen Y grows downwards, so dragging down tilts the camera up.
            float sensitivity = FreeOrbitSpeed * 0.35f;
            _freeYaw += delta.x * sensitivity;
            _freePitch = Mathf.Clamp(_freePitch + delta.y * sensitivity, FreeMinPitch, FreeMaxPitch);
        }

        private float _lastPinchDistance;

        private void LateUpdate()
        {
            Resolve();
            if (Player == null && Pet == null) return;

            float dt = Time.deltaTime;
            TickFreeInput();
            UpdateHudPitch();

            Vector3 focus = FocusPoint();
            Vector3 desired = Clamp(focus + CameraOffset());

            // The free camera is already user-damped; smoothing it as well feels laggy.
            float follow = View == CameraViewMode.Free ? 14f : FollowSmoothing;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-follow * dt));

            Vector3 lookTarget = focus + LookOffset;
            _lookPoint = Vector3.Lerp(_lookPoint, lookTarget, 1f - Mathf.Exp(-LookSmoothing * dt));

            Vector3 forward = _lookPoint - transform.position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Compose(forward), 1f - Mathf.Exp(-LookSmoothing * dt));
            }

            // Smoothing can leave the subjects adrift mid-move, so re-fit every frame.
            FitSubjectsToBand();
            FitSubjectsToWidth();
            ApplyPitch();
            CullRoomFaces();
        }
    }
}
