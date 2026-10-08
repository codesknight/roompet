using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Drawing for the touch controls.
    ///
    /// These deliberately do NOT use GUI.Button: IMGUI buttons cannot be pressed
    /// simultaneously (one pointer only), so the drawing here is inert and the input
    /// comes from <see cref="MobileTouch"/>, which reads every finger. Each draw call
    /// registers its rect so the next input pass knows where the control is.
    ///
    /// Two coordinate spaces are involved and mixing them up is the classic bug here:
    /// the HUD draws through <c>GUI.matrix</c> in <i>design pixels</i>, while the touch
    /// layer hit-tests against real <i>screen pixels</i>. So the callers hand these widgets
    /// design rects, the widgets draw them as-is, and <see cref="ToScreen"/> converts to
    /// screen pixels for registration. Feeding screen rects in would draw them scaled twice
    /// — invisible at a scale of 0.9, catastrophic at the 1.6-1.8 a phone actually uses.
    /// </summary>
    public static class MobileWidgets
    {
        private static GUIStyle _label;
        private static GUIStyle _small;
        private static Texture2D _white;

        /// <summary>Design→screen scale currently applied by the HUD's <c>GUI.matrix</c>.</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>Pixels the whole HUD is inset by to clear the notch and rounded corners.</summary>
        public static Vector2 SafeOffset { get; private set; }

        /// <summary>
        /// Publishes the HUD's transform for this frame. Call once per OnGUI, before any
        /// widget, so drawing and hit-testing agree about where a control is.
        /// </summary>
        public static void BeginFrame(float scale, Vector2 safeOffset)
        {
            Scale = scale > 0f ? scale : 1f;
            SafeOffset = safeOffset;
        }

        /// <summary>Design-space rect → real screen pixels, for touch hit-testing.</summary>
        public static Rect ToScreen(Rect design)
        {
            return new Rect(
                SafeOffset.x + design.x * Scale,
                SafeOffset.y + design.y * Scale,
                design.width * Scale,
                design.height * Scale);
        }

        /// <summary>Real screen point → the space these widgets draw in.</summary>
        public static Vector2 ToDesign(Vector2 screen) => (screen - SafeOffset) / Scale;

        private static void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static Texture2D White
        {
            get
            {
                // A 1x1 white texture used with GUI.color, so controls can be translucent
                // without shipping any art.
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        public static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = previous;
        }

        /// <summary>
        /// Draws an inert rounded panel (a filled rect plus a border), which is what the
        /// stick and the buttons are made of.
        /// </summary>
        public static void Panel(Rect rect, Color fill, Color border, float borderWidth = 2f)
        {
            Fill(rect, fill);
            Fill(new Rect(rect.x, rect.y, rect.width, borderWidth), border);
            Fill(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), border);
            Fill(new Rect(rect.x, rect.y, borderWidth, rect.height), border);
            Fill(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), border);
        }

        /// <summary>
        /// A tappable control: draws it, registers its rect, and highlights it while held.
        /// Returns true when it was pressed this frame (press = finger down on it).
        /// The rect is in the HUD's current (design) space; registration converts it.
        /// </summary>
        public static bool Button(string id, Rect rect, string label, Color tint, bool enabled = true)
        {
            EnsureStyles();
            MobileTouch.RegisterButton(id, ToScreen(rect), enabled, label);

            bool held = enabled && MobileTouch.Held(id);

            Color fill = enabled
                ? (held ? Color.Lerp(tint, Color.white, 0.45f) : new Color(tint.r, tint.g, tint.b, 0.55f))
                : new Color(0.2f, 0.2f, 0.24f, 0.4f);

            Panel(rect, fill, new Color(1f, 1f, 1f, enabled ? 0.55f : 0.2f));

            var previous = GUI.color;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(rect, label, _label);
            GUI.color = previous;

            return enabled && MobileTouch.Pressed(id);
        }

        /// <summary>
        /// Draws the movement stick: base at the touch point, knob on the thumb.
        ///
        /// The touch layer reports positions in real screen pixels, but this runs inside the
        /// HUD's <c>GUI.matrix</c> scaling block, where coordinates are design pixels. So the
        /// origin is converted in, and the radius is left in design pixels — the matrix
        /// re-applies the scale, which is what makes the stick the same physical size on
        /// every screen. Drawing the raw screen point here used to put the stick 10-45% away
        /// from the thumb that was holding it.
        /// </summary>
        public static void Joystick()
        {
            if (!MobileTouch.Stick.Active) return;

            float radius = MobileTouch.Stick.Radius;
            Vector2 origin = ToDesign(MobileTouch.Stick.Origin);

            var baseRect = new Rect(origin.x - radius, origin.y - radius, radius * 2f, radius * 2f);
            Panel(baseRect, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.35f));

            float knobRadius = radius * 0.45f;
            Vector2 knob = ToDesign(MobileTouch.Stick.Knob);
            var knobRect = new Rect(knob.x - knobRadius, knob.y - knobRadius, knobRadius * 2f, knobRadius * 2f);
            Panel(knobRect, new Color(1f, 0.93f, 0.72f, 0.75f), new Color(1f, 0.96f, 0.85f, 0.95f));
        }

        /// <summary>
        /// Hint shown where the stick will appear before the first touch. The zone arrives in
        /// design pixels, so the ghost is sized in design pixels too and the HUD's matrix
        /// scales it with everything else.
        /// </summary>
        public static void StickHint(Rect zone)
        {
            EnsureStyles();
            float radius = MobileTouch.Stick.Radius;
            var ghost = new Rect(zone.x + zone.width * 0.5f - radius, zone.yMax - radius * 1.35f,
                radius * 2f, radius * 2f);
            Panel(ghost, new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.16f));

            var labelRect = new Rect(ghost.x, ghost.center.y - 10f, ghost.width, 22f);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(labelRect, "这里拖动移动", _small);
            GUI.color = previous;
        }
    }
}
