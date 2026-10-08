using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Platform facts and the numbers that make an IMGUI layout survive on a phone.
    ///
    /// The whole UI in this project is IMGUI measured in raw pixels, which is fine on a
    /// 1280x720 desktop window and wrong on a 2340x1080 phone: a 300px panel becomes a
    /// third of the screen and a 26px button becomes untappable. Everything here exists
    /// to convert "looks right at 720p" into "looks right on the device".
    /// </summary>
    public static class MobileUi
    {
        /// <summary>Design height the desktop layout was tuned against.</summary>
        public const float ReferenceHeight = 720f;

        /// <summary>
        /// True on a phone/tablet build. Deliberately not "any touchscreen": a Windows
        /// tablet running the desktop build should keep mouse+keyboard behaviour, and
        /// the editor must not switch modes just because it can see a touch screen.
        /// </summary>
        public static bool IsMobile => Application.isMobilePlatform;

        /// <summary>
        /// True when touch controls should be shown: a mobile build, or a desktop build
        /// being driven by a touchscreen. Forced on with <see cref="ForceTouchControls"/>
        /// so the editor can preview the phone layout.
        /// </summary>
        public static bool UseTouchControls
        {
            get
            {
                if (ForceTouchControls) return true;
                if (IsMobile) return true;
                return Input.touchSupported && Input.touchCount > 0;
            }
        }

        /// <summary>Editor/testing switch: pretend we are on a phone.</summary>
        public static bool ForceTouchControls;

        /// <summary>
        /// Layout scale for the HUD.
        ///
        /// Driven by the screen's SHORT side — not by height — because the game runs in both
        /// orientations: on a 1080x2400 portrait phone the height is 2400, and scaling by it
        /// would slam the clamp at 1.8, leaving a 600px-wide design space to lay a whole HUD
        /// out in. The short side is the honest "how big is this screen", and in landscape it
        /// is exactly the number it always was, so no landscape layout moves.
        ///
        /// Clamped at both ends: below ~0.9 the text is unreadable, and above ~1.8 a single
        /// panel would swallow the play area. DPI is used as a gentle correction when the
        /// platform reports it (a 400-dpi phone needs slightly bigger targets than a
        /// 240-dpi tablet at the same pixel height).
        /// </summary>
        public static float UiScale
        {
            get
            {
                float dpi = Screen.dpi > 60f ? Screen.dpi : 0f;
                return ScaleFor(ShortSide, dpi);
            }
        }

        /// <summary>
        /// The scale calculation on its own, so it can be checked at phone sizes without
        /// resizing the editor's Game view. Pass the screen's short side.
        /// </summary>
        public static float ScaleFor(float shortSide, float dpi)
        {
            float byShortSide = shortSide / ReferenceHeight;

            // Screen.dpi is 0 on plenty of devices and in the editor; only trust it when it
            // looks plausible.
            if (dpi > 60f)
            {
                float dpiCorrection = Mathf.Lerp(0.9f, 1.15f, Mathf.InverseLerp(200f, 500f, dpi));
                byShortSide *= dpiCorrection;
            }

            return Mathf.Clamp(byShortSide, 0.9f, 1.8f);
        }

        /// <summary>Shortest side of the screen, in pixels — the honest "how big is this".</summary>
        public static float ShortSide => Mathf.Min(Screen.width, Screen.height);

        /// <summary>True when the device is held sideways (width ≥ height).</summary>
        public static bool IsLandscape => Screen.width >= Screen.height;

        /// <summary>True when the device is held upright. Both orientations are supported.</summary>
        public static bool IsPortrait => Screen.width < Screen.height;

        /// <summary>
        /// Width/height. Portrait phones land near 0.45, landscape near 2.2, and the cameras
        /// need to know which one they are looking into: a camera that fits a scene vertically
        /// can still slice the sides off horizontally.
        /// </summary>
        public static float Aspect => Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;

        /// <summary>
        /// How far the "fit this in view" correction may pull a camera back, as a multiplier on
        /// its base distance. Generous, because portrait is genuinely narrow, but capped so a
        /// bad measurement cannot turn the room into a postage stamp.
        /// </summary>
        public const float MaxWidthFitPullback = 4f;

        /// <summary>The aspect a layout or a camera is designed against.</summary>
        public const float ReferenceAspect = 16f / 9f;

        /// <summary>
        /// How far away a camera must sit for something <paramref name="halfWidth"/> metres to
        /// the side of what it is looking at to stay inside the frame.
        ///
        /// This is the piece that makes one camera serve both orientations. A camera is defined
        /// by its VERTICAL field of view, so as the viewport narrows the horizontal field
        /// collapses with it: a 3-lane track that fills a 16:9 phone is cut off at both edges in
        /// portrait. Pulling the camera back restores the horizontal coverage, and this is the
        /// honest distance to pull back to rather than a magic multiplier.
        ///
        /// Pure on purpose — the desktop case (16:9 and wider) must come out at "no change",
        /// and that is worth a test.
        /// </summary>
        public static float RequiredDistanceForWidth(float halfWidth, float verticalFovDegrees,
            float aspect, float sideMargin = 0.06f)
        {
            if (halfWidth <= 0f) return 0f;

            float halfTan = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(verticalFovDegrees, 1f, 170f) * 0.5f)
                            * Mathf.Max(0.05f, aspect);
            float usable = halfTan * Mathf.Max(0.1f, 1f - sideMargin);
            return usable <= 0.0001f ? float.MaxValue : halfWidth / usable;
        }

        /// <summary>
        /// The multiplier to apply to a camera's base distance so its subject fits the viewport's
        /// width. Exactly 1 whenever the base distance is already far enough — which is the case
        /// on every desktop window and on a landscape phone — so this only ever widens the shot
        /// on a narrow screen.
        /// </summary>
        public static float WidthFitScale(float baseDistance, float halfWidth, float verticalFovDegrees,
            float aspect, float sideMargin = 0.06f, float max = MaxWidthFitPullback)
        {
            if (baseDistance <= 0.01f) return 1f;

            float required = RequiredDistanceForWidth(halfWidth, verticalFovDegrees, aspect, sideMargin);
            if (required == float.MaxValue) return max;

            return Mathf.Clamp(required / baseDistance, 1f, max);
        }

        /// <summary>
        /// Screen.safeArea converted to IMGUI coordinates (origin top-left).
        ///
        /// Unity reports the safe area with the origin at the BOTTOM left while IMGUI
        /// measures from the top, so the two only agree by accident on symmetrical
        /// screens. Panels anchored with this stay clear of notches and rounded corners.
        /// </summary>
        public static Rect SafeArea
        {
            get
            {
                var area = Screen.safeArea;
                if (area.width <= 1f || area.height <= 1f) return new Rect(0f, 0f, Screen.width, Screen.height);
                return new Rect(area.x, Screen.height - (area.y + area.height), area.width, area.height);
            }
        }

        /// <summary>Minimum edge length of a tappable control, in design pixels.</summary>
        public const float MinTouchTarget = 44f;

        /// <summary>Grows a measurement to at least <see cref="MinTouchTarget"/>.</summary>
        public static float Touchable(float size) => Mathf.Max(size, MinTouchTarget);

        /// <summary>
        /// Scales a point from design space into screen space around a top-left origin,
        /// which is what every hand-written IMGUI rect in this project assumes.
        /// </summary>
        public static Vector2 ScalePoint(Vector2 point, float scale) => point * scale;

        /// <summary>Scales a size, never going below a comfortable touch target.</summary>
        public static Vector2 ScaleSize(Vector2 size, float scale)
            => new Vector2(Touchable(size.x * scale), Touchable(size.y * scale));
    }
}
