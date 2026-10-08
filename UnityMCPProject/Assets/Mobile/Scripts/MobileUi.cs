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
        /// Driven by screen height so a taller phone gets proportionally bigger controls,
        /// but clamped at both ends: below ~0.9 the text is unreadable, and above ~1.8 a
        /// single panel would swallow the play area. DPI is used as a gentle correction
        /// when the platform reports it (a 400-dpi phone needs slightly bigger targets
        /// than a 240-dpi tablet at the same pixel height).
        /// </summary>
        public static float UiScale
        {
            get
            {
                float dpi = Screen.dpi > 60f ? Screen.dpi : 0f;
                return ScaleFor(Screen.height, dpi);
            }
        }

        /// <summary>
        /// The scale calculation on its own, so it can be checked at phone sizes without
        /// resizing the editor's Game view.
        /// </summary>
        public static float ScaleFor(float screenHeight, float dpi)
        {
            float byHeight = screenHeight / ReferenceHeight;

            // Screen.dpi is 0 on plenty of devices and in the editor; only trust it when it
            // looks plausible.
            if (dpi > 60f)
            {
                float dpiCorrection = Mathf.Lerp(0.9f, 1.15f, Mathf.InverseLerp(200f, 500f, dpi));
                byHeight *= dpiCorrection;
            }

            return Mathf.Clamp(byHeight, 0.9f, 1.8f);
        }

        /// <summary>Shortest side of the screen, in pixels — the honest "how big is this".</summary>
        public static float ShortSide => Mathf.Min(Screen.width, Screen.height);

        /// <summary>True when the device is held sideways (width ≥ height).</summary>
        public static bool IsLandscape => Screen.width >= Screen.height;

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
