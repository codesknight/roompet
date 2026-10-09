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
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
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
        /// Whether a pointer press landed inside a design-space rect, read straight from the
        /// pointer instead of from the touch layer.
        ///
        /// <see cref="MobileTouch"/> is the good path — it tracks fingers by id, so two controls
        /// can be held at once — but it is also a lot of machinery between a finger and a button,
        /// and when anything in that machinery is wrong the symptom is "the button does not
        /// respond", with nothing on screen to explain it. This asks the same question the dumb
        /// way: did a click this frame land in this rect. The two answers are OR'd, so the good
        /// path still owns multi-touch and this can only ever add a press that would be lost.
        /// A click is what Unity synthesises from a single touch, so this is exactly one finger.
        /// </summary>
        public static bool PointerPressedInside(Rect designRect)
        {
            if (!MobileTouch.PlayInputEnabled) return false;
            if (!Input.GetMouseButtonDown(0)) return false;

            var screen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return designRect.Contains(ToDesign(screen));
        }

        /// <summary>Like <see cref="PointerPressedInside"/>, but for as long as the pointer is down.</summary>
        public static bool PointerHeldInside(Rect designRect)
        {
            if (!MobileTouch.PlayInputEnabled) return false;
            if (!Input.GetMouseButton(0)) return false;

            var screen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return designRect.Contains(ToDesign(screen));
        }

        /// <summary>
        /// A tappable control: draws it, registers its rect, and highlights it while held.
        /// Returns true when it was pressed this frame (finger down on it, on either input path).
        /// The rect is in the HUD's current (design) space; registration converts it.
        /// </summary>
        public static bool Button(string id, Rect rect, string label, Color tint, bool enabled = true)
        {
            EnsureStyles();
            MobileTouch.RegisterButton(id, ToScreen(rect), enabled, label);

            bool held = enabled && (MobileTouch.Held(id) || PointerHeldInside(rect));

            Color fill = enabled
                ? (held ? Color.Lerp(tint, Color.white, 0.45f) : new Color(tint.r, tint.g, tint.b, 0.55f))
                : new Color(0.2f, 0.2f, 0.24f, 0.4f);

            Panel(rect, fill, new Color(1f, 1f, 1f, enabled ? 0.55f : 0.2f));

            var previous = GUI.color;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(rect, label, _label);
            GUI.color = previous;

            return enabled && (MobileTouch.Pressed(id) || PointerPressedInside(rect));
        }

        /// <summary>
        /// A round action button: the shape phones use for "the thing your thumb does".
        ///
        /// Round because it is the only shape that reads as a button without a border on a small
        /// screen, and because a circle of a given diameter is a bigger target than a square of the
        /// same width where thumbs actually land.
        ///
        /// The word goes <b>on</b> the button, not under it with a glyph above. The first version
        /// drew an icon with a caption, which failed for the reason this project keeps re-learning:
        /// Unity's built-in IMGUI font has no emoji, so every button came out as an empty coloured
        /// disc with a tiny label underneath. Two characters of Chinese are an icon here.
        /// </summary>
        public static bool CircleButton(string id, Rect rect, string label, Color tint,
            bool enabled = true)
        {
            EnsureStyles();
            MobileTouch.RegisterButton(id, ToScreen(rect), enabled, label);

            bool held = enabled && (MobileTouch.Held(id) || PointerHeldInside(rect));
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var circle = new Rect(rect.center.x - radius, rect.center.y - radius, radius * 2f, radius * 2f);

            Color fill = enabled
                ? (held
                    ? Color.Lerp(tint, Color.white, 0.35f)
                    : new Color(tint.r, tint.g, tint.b, 0.86f))
                : new Color(0.22f, 0.22f, 0.26f, 0.45f);

            // A real circle (the shared skin draws rounded panels), a dark rim so it separates from
            // a busy room, and a soft highlight along the top so it does not read as a hole.
            UiSkin.Shadow(circle, radius, 4f, 0.45f);
            UiSkin.Panel(circle, radius, fill, new Color(0f, 0f, 0f, 0.35f), 3f);
            UiSkin.Panel(new Rect(circle.x + radius * 0.42f, circle.y + radius * 0.24f,
                    radius * 1.16f, radius * 0.42f),
                radius * 0.34f, new Color(1f, 1f, 1f, held ? 0.26f : 0.14f),
                new Color(1f, 1f, 1f, 0f), 0f);

            var previous = GUI.color;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(circle, label, _label);
            GUI.color = previous;

            return enabled && (MobileTouch.Pressed(id) || PointerPressedInside(rect));
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

        /// <summary>How long the stick hint stays at full strength after the room loads.</summary>
        public const float StickHintSeconds = 9f;

        private static float _stickHintBornAt = -1f;
        private static bool _stickHintUsed;

        /// <summary>Fades the stick hint out once the player has moved, or after a few seconds.</summary>
        public static void ResetStickHint()
        {
            _stickHintBornAt = Time.realtimeSinceStartup;
            _stickHintUsed = false;
        }

        /// <summary>
        /// Hint shown where the stick will appear, before the player has used it.
        ///
        /// Two rules, both from a phone: it must stay <b>inside</b> the zone it describes (the
        /// first version hung 30px below it and printed "这里拖动移动" across the chat bar's own
        /// button, which is exactly the "the button does not work" report it earned), and it must
        /// go away once the player has clearly understood — a permanent ghost stick in the corner
        /// of the screen is clutter forever after the first ten seconds.
        /// </summary>
        public static void StickHint(Rect zone)
        {
            if (_stickHintBornAt < 0f) _stickHintBornAt = Time.realtimeSinceStartup;
            if (MobileTouch.Stick.Active) _stickHintUsed = true;
            if (_stickHintUsed) return;

            float age = Time.realtimeSinceStartup - _stickHintBornAt;
            if (age > StickHintSeconds) return;

            float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(StickHintSeconds * 0.6f, StickHintSeconds, age));
            if (alpha <= 0.01f) return;

            EnsureStyles();
            float radius = MobileTouch.Stick.Radius;

            // Fully inside the zone: the ghost's bottom edge is the zone's bottom edge. A circle,
            // not a box — this is the shape the real stick appears in.
            float diameter = radius * 1.7f;
            var ghost = new Rect(zone.x + zone.width * 0.5f - diameter * 0.5f,
                Mathf.Max(zone.y, zone.yMax - diameter), diameter, diameter);

            UiSkin.Panel(ghost, diameter * 0.5f, new Color(1f, 1f, 1f, 0.05f * alpha),
                new Color(1f, 1f, 1f, 0.22f * alpha), 2f);

            var labelRect = new Rect(ghost.center.x - 70f, ghost.center.y - 11f, 140f, 22f);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.45f * alpha);
            GUI.Label(labelRect, "这里拖动移动", _small);
            GUI.color = previous;
        }
    }
}
