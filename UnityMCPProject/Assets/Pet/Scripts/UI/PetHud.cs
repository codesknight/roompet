using System;
using DshMobile;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// All of the pet's interface, in IMGUI. Same choice as the runner: no Canvas, no font
    /// asset, no extra package, so the scene opens and works in any project state.
    /// </summary>
    public class PetHud : MonoBehaviour
    {
        private const int MaxLogLines = 200;

        /// <summary>IMGUI control name of the pet-name field, shared with the tests.</summary>
        public const string NameControl = "PetNameField";

        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _petLine;
        private GUIStyle _userLine;
        private GUIStyle _bubble;
        private GUIStyle _bubblePet;
        private GUIStyle _bubbleUser;
        private GUIStyle _avatar;
        private GUIStyle _sendButton;
        private int _inputFontSize = 17;
        private GUIStyle _panel;
        private GUIStyle _button;
        private GUIStyle _buttonSmall;
        private GUIStyle _thinking;
        private GUIStyle _nameField;
        private bool _nameFocused;
        private string _editPetName;
        private bool _stylesReady;

        private string _input = "";
        private Vector2 _logScroll;
        private Vector2 _statusScroll;
        private Vector2 _settingsScroll;
        private Vector2 _promptScroll;
        private bool _showSettings;
        private bool _focusFirstField;
        private bool _showPromptPreview;
        private bool _showJournal;
        private DateTime _journalMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        private DateTime _selectedDay = DateTime.Now.Date;
        private Vector2 _dayScroll;

        private string _editBaseUrl;
        private string _editModel;
        private string _editKey;
        private string _editExtraInstructions = "";
        private bool _editAnonymous;
        private bool _editOffline;
        private string _testResult = "";
        private bool _testing;

        private static string _cursorHint;

        /// <summary>Set by hoverable world objects; shown near the cursor.</summary>
        public static void SetCursorHint(string hint) => _cursorHint = hint;

        /// <summary>True while the chat box has keyboard focus, so WASD can be typed instead
        /// of walking the character around.</summary>
        public static bool IsTextInputFocused { get; private set; }

        /// <summary>Opens or closes the notebook. Exposed so the editor menu and automated
        /// checks can show it without clicking.</summary>
        public void ToggleJournal() => _showJournal = !_showJournal;

        public bool JournalOpen => _showJournal;

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            // Everything is a couple of points bigger than it was. The old sizes were tuned to
            // fit as much as possible on a 720p window, with the result that the conversation —
            // the entire point of the game — was the smallest text on screen, and on a phone it
            // was unreadable. A HUD that has to be squinted at is not a dense HUD, it is a
            // broken one.
            _title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.94f, 0.82f);

            _label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _label.normal.textColor = Color.white;

            _small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _small.normal.textColor = new Color(0.86f, 0.87f, 0.91f);

            _petLine = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _petLine.normal.textColor = new Color(0.22f, 0.17f, 0.13f);

            _userLine = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _userLine.normal.textColor = Color.white;

            // Bubble text, named separately from the transcript colours so the panel can use a
            // light bubble for the pet and a dark one for the player.
            _bubblePet = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _bubblePet.normal.textColor = new Color(0.20f, 0.15f, 0.12f);

            _bubbleUser = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _bubbleUser.normal.textColor = Color.white;

            _bubble = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };

            _avatar = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _avatar.normal.textColor = new Color(0.14f, 0.11f, 0.10f);

            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 12, 12) };

            _button = new GUIStyle(GUI.skin.button) { fontSize = 16, padding = new RectOffset(12, 12, 7, 7) };
            _buttonSmall = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(8, 8, 5, 5) };

            _sendButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _thinking = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic };
            _thinking.normal.textColor = new Color(1f, 0.85f, 0.4f);

            // The name field doubles as the panel title, so it is sized like one.
            _nameField = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(6, 6, 4, 4)
            };
            _nameField.normal.textColor = new Color(1f, 0.94f, 0.82f);
            _nameField.focused.textColor = Color.white;

            _inputFontSize = 17;
        }

        private void OnGUI()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;

            EnsureStyles();

            // ------------------------------------------------------------- scaling
            // Everything below is laid out in "design pixels" and then scaled as a whole
            // with GUI.matrix, which scales fonts, spacing and IMGUI's own hit-testing
            // together. That is the only way a hand-written pixel layout survives on a
            // 2400x1080 phone: without it a 300px panel eats a third of the screen and a
            // 26px button is too small to hit.
            //
            // The layout is also inset by the safe area so nothing hides under a notch or a
            // rounded corner.
            var safe = MobileUi.SafeArea;
            float scale = MobileUi.UseTouchControls ? MobileUi.UiScale : 1f;
            float designWidth = Mathf.Max(320f, safe.width / scale);
            float designHeight = Mathf.Max(240f, safe.height / scale);

            var previousMatrix = GUI.matrix;
            if (scale != 1f || safe.x != 0f || safe.y != 0f)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, safe.y, 0f), Quaternion.identity,
                    new Vector3(scale, scale, 1f));
            }

            Mobile = MobileUi.UseTouchControls;
            Scale = scale;
            SafeOffset = new Vector2(safe.x, safe.y);
            DesignWidth = designWidth;
            DesignHeight = designHeight;

            // The sidebar is worth its width only while the transcript is showing; with the
            // chat collapsed on a phone the room should get the whole screen back.
            TranscriptVisible = !Mobile || _chatExpanded;

            var layout = ComputeLayout(designWidth, designHeight, PetSpecies.Count, TranscriptVisible);

            // The touch layer has to know whether the play area is interactive at all.
            if (Mobile)
            {
                MobileTouch.PlayInputEnabled = !ModalOpen;
                MobileTouch.StickEnabled = !ModalOpen && !_chatExpanded;
                MobileTouch.StickZone = ToScreen(new Rect(
                    0f, designHeight * 0.40f, designWidth * 0.46f, designHeight * 0.60f));
            }

            // While a modal panel is up, the panels behind it must not react. GUI.enabled is
            // how IMGUI is told a control is inert, and it is order-independent — unlike the
            // full-rect invisible Button this used to draw, which claimed the click itself and
            // left the modal's own text fields unable to take focus.
            bool modal = ModalOpen;
            if (modal) GUI.enabled = false;
            DrawStatusPanel(gm, layout);
            if (Mobile) DrawMobileChat(gm, layout);
            else DrawChat(gm, layout);
            DrawSpeciesSwitcher(gm, layout);
            DrawViewSwitcher(gm, layout);
            if (!Mobile) DrawThrowMeter(gm, layout);
            else ShowMobileAimGuide(gm);
            DrawOverlays(gm, layout);
            GUI.enabled = true;

            // Controls sit above the room but below any modal panel.
            if (Mobile) DrawMobileControls(gm, layout);

            if (gm.DoorPromptOpen) DrawDoorPrompt(gm);
            if (_showSettings) DrawSettings(gm);
            if (_showPromptPreview) DrawPromptPreview(gm);
            if (_showJournal) DrawJournal(gm);

            GUI.matrix = previousMatrix;
        }

        // ------------------------------------------------------------------- mobile

        /// <summary>True while the touch layout is in use (see <see cref="MobileUi"/>).</summary>
        public static bool Mobile { get; private set; }

        /// <summary>Design→screen scale factor applied through GUI.matrix.</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>Pixels the whole HUD is inset by, to clear notches and rounded corners.</summary>
        public static Vector2 SafeOffset { get; private set; }

        /// <summary>
        /// Viewport size in DESIGN pixels — what the HUD is actually laid out in.
        ///
        /// Everything drawn between the <c>GUI.matrix</c> push and pop is in these units, which
        /// is NOT what <c>Screen.width</c> reports on a phone. Mixing the two is the bug that
        /// put every modal panel off-centre and half off the bottom of the screen: a rect
        /// centred against 1080x2400 screen pixels and then drawn through a 1.7x matrix lands
        /// at 1.7x the intended position.
        /// </summary>
        public static float DesignWidth { get; private set; } = 1280f;

        /// <inheritdoc cref="DesignWidth"/>
        public static float DesignHeight { get; private set; } = 720f;

        /// <summary>Converts real screen pixels (e.g. WorldToScreenPoint) into design pixels.</summary>
        public static Vector2 ScreenToDesign(Vector2 screen)
        {
            float scale = Scale > 0f ? Scale : 1f;
            return new Vector2((screen.x - SafeOffset.x) / scale, (screen.y - SafeOffset.y) / scale);
        }

        /// <summary>
        /// A world point as an IMGUI rect in design space, y measured from the top.
        ///
        /// <c>WorldToScreenPoint</c> answers in real screen pixels with the origin at the
        /// BOTTOM left, so using it directly inside the scaled block puts a label in the wrong
        /// place twice over. Only valid outside OnGUI's matrix block if Mobile is false, in
        /// which case the conversion is the identity.
        /// </summary>
        public static Rect WorldLabelRect(Camera camera, Vector3 world, float width, float height,
            float yOffset = -16f)
        {
            var screen = camera.WorldToScreenPoint(world);
            float scale = Scale > 0f ? Scale : 1f;
            var design = ScreenToDesign(new Vector2(screen.x, Screen.height - screen.y));
            return new Rect(design.x - width * 0.5f,
                design.y + yOffset / scale,
                width, height);
        }

        /// <summary>Chat is collapsed by default on a phone: the thumbs live down there.</summary>
        private bool _chatExpanded;

        /// <summary>Whether the status panel shows the tuning lines (phone layout).</summary>
        private bool _statusDetail;

        /// <summary>Converts a design-space rect into real screen pixels for touch hit-testing.</summary>
        private static Rect ToScreen(Rect design)
        {
            return new Rect(
                SafeOffset.x + design.x * Scale,
                SafeOffset.y + design.y * Scale,
                design.width * Scale,
                design.height * Scale);
        }

        // -------------------------------------------------------------------- layout

        /// <summary>Where the always-on panels go, derived from the actual viewport.</summary>
        public struct HudLayout
        {
            public Rect Status;
            public Rect Chat;
            public Rect Switcher;

            /// <summary>Top edge of the chat panel: the floor for anything floating above it.</summary>
            public float ChatTop;

            /// <summary>
            /// True when the transcript sits in a right-hand sidebar rather than under the room.
            ///
            /// The conversation is the point of this game, and a 260px strip along the bottom
            /// made it the least noticeable thing on screen. On a wide viewport it moves to a
            /// proper column beside the room; on a phone in portrait there is no room for a
            /// column, so it stays a (much taller, much louder) bottom sheet.
            /// </summary>
            public bool ChatOnSide;

            /// <summary>
            /// The part of the viewport the room should be framed in, in design pixels.
            ///
            /// With the transcript on the side this is a wide, short band; with it at the
            /// bottom it is a tall, narrow one. The camera reads this instead of assuming
            /// "everything above the chat panel", which is what lets both layouts work.
            /// </summary>
            public Rect FreeBand;

            /// <summary>
            /// The whole usable viewport. The touch controls anchor to THIS rather than to the
            /// chat panel: thumbs live at the corners of the screen, and with the transcript in
            /// a sidebar the chat panel is no longer anywhere near them.
            /// </summary>
            public Rect Viewport;
        }

        /// <summary>Minimum viewport width for the sidebar layout. Below this, the column would
        /// squeeze the room into a slot, so the transcript goes back under it.</summary>
        public const float SidebarMinWidth = 1024f;

        /// <summary>
        /// The HUD's geometry, in one place.
        ///
        /// Three rules are encoded here, all learned from real bugs:
        ///  1. the status panel and the chat panel must NEVER overlap. They used to be sized
        ///     independently (status up to 360px tall from the top, chat 250px tall pinned to
        ///     the bottom), so on a Game view shorter than ~630px the chat panel — drawn
        ///     second, therefore on top — covered the status panel's footer and swallowed the
        ///     clicks meant for 记事本 and 设置. That is why the buttons "did not work";
        ///  2. every floating panel is clamped to the viewport. A fixed 720x560 calendar is
        ///     centred, so on a short view its own title bar and month arrows render above
        ///     y=0 and the player sees nothing;
        ///  3. the layout is responsive. Wide viewports get a transcript sidebar with the
        ///     status and switcher stacked in a left column; narrow ones keep the original
        ///     top-and-bottom arrangement, which is the only one that fits a phone.
        /// </summary>
        public static HudLayout ComputeLayout()
            => ComputeLayout(Screen.width, Screen.height, PetSpecies.Count, TranscriptVisible);

        /// <summary>
        /// Whether the transcript is on screen right now.
        ///
        /// On a phone the chat is collapsed by default, and a sidebar that reserves a quarter
        /// of the screen for a bar nobody has opened yet is worse than no sidebar at all — the
        /// room shrinks for nothing. So the column only exists while the transcript does, which
        /// also means the room visibly widens when the chat is put away.
        /// </summary>
        public static bool TranscriptVisible { get; private set; } = true;

        /// <summary>
        /// The room's free band in real screen pixels, for the camera.
        ///
        /// The camera used to assume "everything above the chat panel", which stopped being
        /// true the moment the transcript moved into a side column: the band is now wide and
        /// short in one layout and tall and narrow in the other, and only the HUD knows which.
        /// </summary>
        public static Rect FreeBandScreen() => ComputeLayout().FreeBand;

        /// <summary>Layout for a fully visible transcript. Kept for tests and reports.</summary>
        public static HudLayout ComputeLayout(float w, float h, int speciesCount)
            => ComputeLayout(w, h, speciesCount, true);

        public static HudLayout ComputeLayout(float w, float h, int speciesCount, bool transcriptVisible)
        {
            bool sidebar = transcriptVisible
                           && w >= SidebarMinWidth
                           && w * 0.34f >= 340f          // the column has to be worth having
                           && w - Mathf.Clamp(w * 0.30f, 340f, 520f) >= 520f;  // ...and leave a room

            return sidebar ? ComputeSidebarLayout(w, h, speciesCount)
                           : ComputeStackedLayout(w, h, speciesCount);
        }

        /// <summary>Wide viewport: transcript on the right, one left column for everything else.</summary>
        private static HudLayout ComputeSidebarLayout(float w, float h, int speciesCount)
        {
            float margin = 14f;
            float chatWidth = Mathf.Clamp(w * 0.30f, 340f, 520f);

            var chat = new Rect(w - chatWidth - margin, margin, chatWidth, h - margin * 2f);

            // The left column is as wide as the space the sidebar leaves, capped so the status
            // panel does not stretch into a wall of text.
            float columnWidth = Mathf.Clamp(chat.x - margin * 2f - 8f, 220f, 340f);
            float switchHeight = 34f + speciesCount * 30f;

            float left = margin;
            var status = new Rect(left, margin, columnWidth, Mathf.Clamp(h * 0.44f, 190f, 380f));
            var switcher = new Rect(left, status.yMax + 10f, columnWidth,
                Mathf.Min(switchHeight, Mathf.Max(60f, h - status.yMax - 20f)));

            return new HudLayout
            {
                Status = status,
                Chat = chat,
                Switcher = switcher,
                ChatTop = h - margin,
                ChatOnSide = true,
                FreeBand = new Rect(margin + columnWidth + 8f, margin,
                    Mathf.Max(120f, chat.x - columnWidth - margin * 2f - 8f), h - margin * 2f),
                Viewport = new Rect(0f, 0f, w, h)
            };
        }

        /// <summary>Narrow viewport: the original top panels with the transcript underneath.</summary>
        private static HudLayout ComputeStackedLayout(float w, float h, int speciesCount)
        {
            // The transcript is the main event in this app, so it takes a third of the view.
            float chatHeight = Mathf.Clamp(h * 0.34f, 200f, 340f);
            var chat = new Rect(14f, h - chatHeight - 14f, Mathf.Max(260f, w - 28f), chatHeight);

            // Right-aligned switcher, and a status panel narrow enough that the two can never
            // collide even on a small Game view.
            float switchWidth = Mathf.Clamp(w * 0.22f, 150f, 176f);
            float switchHeight = 30f + speciesCount * 30f;
            var switcher = new Rect(w - switchWidth - 14f, 14f, switchWidth, switchHeight);

            float statusWidth = Mathf.Clamp(w - switchWidth - 40f, 232f, 300f);

            // The status panel gets whatever room is left above the chat panel, never less
            // than enough for its pinned footer (62) plus the header (46) plus some detail.
            float available = chat.y - 14f - 10f;
            var status = new Rect(14f, 14f, statusWidth, Mathf.Clamp(available, 168f, 360f));

            return new HudLayout
            {
                Status = status,
                Chat = chat,
                Switcher = switcher,
                ChatTop = chat.y,
                ChatOnSide = false,
                FreeBand = new Rect(0f, 0f, w, chat.y),
                Viewport = new Rect(0f, 0f, w, h)
            };
        }

        /// <summary>A floating panel's rect, centred but always fully inside the viewport.</summary>
        public static Rect OverlayRect(float preferredWidth, float preferredHeight)
            => OverlayRect(preferredWidth, preferredHeight, DesignWidth, DesignHeight, Mobile ? 24f : 16f);

        /// <summary>
        /// Centres a panel in the viewport, clamped so it never hangs off an edge.
        ///
        /// The sizes are in DESIGN pixels — the space the HUD actually draws in. Using
        /// <c>Screen.width</c> here is what put every dialog off-centre and half off the bottom
        /// of a phone screen: a rect centred against 1080x2400 screen pixels, then drawn
        /// through a 1.6x matrix, lands at 1.6x the intended offset.
        ///
        /// The margin grows a little on a phone: a panel that touches the screen edge looks
        /// like a bug next to rounded corners, and it leaves no room to tap beside it.
        /// </summary>
        public static Rect OverlayRect(float preferredWidth, float preferredHeight,
            float screenWidth, float screenHeight, float margin = 16f)
        {
            float w = Mathf.Min(preferredWidth, Mathf.Max(200f, screenWidth - margin * 2f));
            float h = Mathf.Min(preferredHeight, Mathf.Max(160f, screenHeight - margin * 2f));
            return new Rect((screenWidth - w) * 0.5f, (screenHeight - h) * 0.5f, w, h);
        }

        /// <summary>
        /// Dims the room and fills a modal panel opaquely.
        ///
        /// GUI.skin.box is translucent, so a panel drawn with it let the whole scene through:
        /// the pet, the shelves, the "按 E" prompt and both HUD panels were all readable
        /// underneath the notebook, which is why the calendar looked like it had not opened
        /// at all. A scrim plus a solid fill is what makes a modal read as a modal.
        ///
        /// Nothing is drawn to swallow clicks here. The panels underneath are disabled while a
        /// modal is open (see OnGUI) instead: an invisible Button covering the panel looks
        /// equivalent but beats the panel's own controls to the click, so every text field
        /// inside it became impossible to focus.
        /// </summary>
        private void ModalBackdrop(Rect rect)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            // Design pixels, not Screen.width: the scrim is drawn through the HUD's matrix, so
            // a screen-sized rect overshoots the viewport by the scale factor.
            GUI.DrawTexture(new Rect(0f, 0f, DesignWidth, DesignHeight), Texture2D.whiteTexture);

            GUI.color = new Color(0.11f, 0.10f, 0.14f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Keep the skin's frame on top of the flat fill.
            GUI.Box(rect, GUIContent.none, _panel);
        }

        private static PetHud _instance;

        /// <summary>
        /// Last-seen geometry and IMGUI focus state, for automated UI checks.
        ///
        /// Text input cannot be verified from a screenshot, and the interesting failures here
        /// (a field that never takes focus, a field whose rect has been pushed off the panel)
        /// are invisible in a render. These are written every OnGUI pass so a test can read
        /// what the GUI actually did.
        /// </summary>
        public static class Diagnostics
        {
            public static Rect PanelRect;
            public static Rect BaseUrlRect;
            public static Rect KeyRect;
            public static string FocusedControl = "";
            public static int KeyboardControl;
            public static string EditBaseUrl = "";

            /// <summary>The pet's committed name, for automated checks of the rename flow.</summary>
            public static string PetName = "";
        }

        /// <summary>True when a pet HUD is live, so other systems can frame around its panels.</summary>
        public static bool Exists => _instance != null;

        private void Awake() { if (_instance == null) _instance = this; }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>
        /// True while a panel is open over the room. World clicks, WASD and E are ignored
        /// while this holds: without it, clicking a calendar cell also fired the
        /// <c>OnMouseDown</c> of whatever interactable happened to sit behind the panel —
        /// physics messages are not blocked by IMGUI — and the pet walked off mid-notebook.
        /// </summary>
        public static bool ModalOpen
        {
            get
            {
                var hud = _instance;
                if (hud == null) return false;
                if (hud._showSettings || hud._showJournal || hud._showPromptPreview) return true;
                var gm = PetGameManager.Instance;
                return gm != null && gm.DoorPromptOpen;
            }
        }

        /// <summary>Human-readable geometry, for the wiring report and for diagnosing layout.</summary>
        public string DescribeLayout()
        {
            var l = ComputeLayout();
            return string.Format(
                "screen={0}x{1} status={2} chat={3} overlap={4} switcher={5} chatTop={6:F0}",
                Screen.width, Screen.height,
                Fmt(l.Status), Fmt(l.Chat),
                l.Status.yMax > l.Chat.y ? "YES(" + (l.Status.yMax - l.Chat.y).ToString("F0") + "px)" : "no",
                Fmt(l.Switcher), l.ChatTop);
        }

        private static string Fmt(Rect r)
            => string.Format("({0:F0},{1:F0},{2:F0}x{3:F0})", r.x, r.y, r.width, r.height);

        /// <summary>
        /// The pet's name, editable in place.
        ///
        /// Committing on Enter as well as on the button, because a name field that demands a
        /// button press after typing reads as broken. The field is re-seeded whenever the
        /// manager's name differs from the draft, so swiping species or hitting 重置 cannot
        /// leave a stale name sitting in the box.
        /// </summary>
        private void DrawNameRow(PetGameManager gm)
        {
            if (_editPetName == null || (!_nameFocused && _editPetName != gm.PetName))
            {
                _editPetName = gm.PetName;
            }

            GUILayout.BeginHorizontal();

            GUI.SetNextControlName(NameControl);
            _editPetName = GUILayout.TextField(_editPetName ?? "", PetGameManager.MaxPetNameLength,
                _nameField, GUILayout.Height(30f));

            bool focused = GUI.GetNameOfFocusedControl() == NameControl;
            bool enter = focused && Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            bool renamed = GUILayout.Button("改名", _buttonSmall, GUILayout.Width(52f), GUILayout.Height(30f));
            GUILayout.EndHorizontal();

            if (renamed || enter)
            {
                if (gm.RenamePet(_editPetName))
                {
                    _editPetName = gm.PetName;
                    // The chat line the pet answers with should not also contain the leftovers.
                    GUI.FocusControl(null);
                }
                else
                {
                    _editPetName = gm.PetName;
                }

                if (enter) Event.current.Use();
            }

            _nameFocused = GUI.GetNameOfFocusedControl() == NameControl;
            Diagnostics.PetName = gm.PetName;
        }

        // ------------------------------------------------------------------ left panel

        /// <summary>
        /// Status panel. The detail lines scroll; the buttons are pinned below the scroll
        /// area and always visible. An earlier version grew the content past the fixed
        /// panel height, which pushed the "设置" and "记事本" buttons out of the clickable
        /// region — the buttons must never depend on scrolling.
        /// </summary>
        private void DrawStatusPanel(PetGameManager gm, HudLayout layout)
        {
            const float footerHeight = 62f;

            // The header is the editable name row plus a 13pt subtitle. At 46px the subtitle
            // spilled out of its area and printed straight over the first need bar ("饱食" got a
            // "开心 亲密度" caption through it). GUILayout areas do not clip their children.
            const float headerHeight = 86f;

            var rect = layout.Status;
            GUI.Box(rect, GUIContent.none, _panel);

            var inner = new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f);

            // ---- header (fixed) ----
            GUILayout.BeginArea(new Rect(inner.x, inner.y, inner.width, headerHeight));
            DrawNameRow(gm);
            GUILayout.Label($"{PetUtil.MoodLabel(gm.Needs.Mood)}　·　亲密度 {gm.Needs.Affection:P0}" +
                            $"　·　{gm.Species.DisplayName}", _small);
            GUILayout.EndArea();

            // ---- scrollable detail ----
            float scrollHeight = inner.height - headerHeight - footerHeight;
            var scrollRect = new Rect(inner.x, inner.y + headerHeight, inner.width, scrollHeight);
            GUILayout.BeginArea(scrollRect);
            _statusScroll = GUILayout.BeginScrollView(_statusScroll, GUILayout.ExpandHeight(true));

            Bar("饱食", gm.Needs.Hunger, new Color(0.95f, 0.62f, 0.30f));
            Bar("精力", gm.Needs.Energy, new Color(0.45f, 0.80f, 0.95f));
            Bar("开心", gm.Needs.Joy, new Color(0.98f, 0.80f, 0.35f));
            Bar("清洁", gm.Needs.Cleanliness, new Color(0.60f, 0.90f, 0.65f));

            // The bladder only appears once it matters: a permanent bar that spends most of its
            // life full is noise, while a bar that appears when the pet starts fidgeting is a
            // warning the player actually reads.
            if (gm.Needs.Bladder < 0.6f)
            {
                Bar("便意", gm.Needs.Bladder, new Color(0.85f, 0.72f, 0.45f));
            }

            GUILayout.Space(4f);
            GUILayout.Label(string.IsNullOrEmpty(gm.Needs.DominantNeed) ? "状态不错" : "想要：" + gm.Needs.DominantNeed, _small);

            if (gm.Personality != null)
            {
                GUILayout.Label("性格：" + gm.Personality.Archetype, _small);
                if (!Mobile || _statusDetail) GUILayout.Label(gm.Personality.Summary, _small);
            }

            // On a phone the panel is small and the debug lines are the first thing to go:
            // they are for tuning, not for playing. The toggle in the footer brings them back.
            if (!Mobile || _statusDetail)
            {
                string mode = gm.Controller != null ? gm.Controller.CurrentMode.ToString() : "-";
                string action = string.IsNullOrEmpty(gm.LastActionLabel) ? "休息中" : gm.LastActionLabel;
                GUILayout.Label($"行为：{mode}　动作：{action}", _small);
                if (!string.IsNullOrEmpty(gm.LastBehaviorLabel)) GUILayout.Label($"刚才：{gm.LastBehaviorLabel}", _small);
                if (!string.IsNullOrEmpty(gm.LastNudgeReason)) GUILayout.Label($"主动开口：{gm.LastNudgeReason}", _small);

                GUILayout.Space(2f);
                GUILayout.Label("大脑：" + gm.BrainConfig.Describe(), _small);
                if (!gm.BrainConfig.CanUseNetwork)
                {
                    GUI.color = new Color(1f, 0.8f, 0.5f);
                    GUILayout.Label("　" + gm.BrainConfig.StatusDetail(), _small);
                    GUI.color = Color.white;
                }
            }

            GUILayout.Space(2f);
            GUILayout.Label($"{gm.Journal.Count} 条记忆　·　{gm.Needs.MoodScore:P0} 状态分", _small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // ---- footer buttons (pinned) ----
            var footer = new Rect(inner.x, inner.yMax - footerHeight, inner.width, footerHeight);
            GUILayout.BeginArea(footer);

            if (!gm.BrainConfig.CanUseNetwork)
            {
                // Loud when it matters: the whole point of the status line is that the
                // player can act on it, so give them the button right here.
                GUI.color = new Color(1f, 0.86f, 0.55f);
                if (GUILayout.Button("⚙ 配置大脑连接（当前离线）", _buttonSmall, GUILayout.Height(24f)))
                {
                    OpenSettings(gm);
                }
                GUI.color = Color.white;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📖 记事本", _buttonSmall)) _showJournal = !_showJournal;
            if (GUILayout.Button("⚙ 设置", _buttonSmall)) OpenSettings(gm);
            if (GUILayout.Button("提示词", _buttonSmall)) OpenPromptPreview(gm);

            if (Mobile && GUILayout.Button(_statusDetail ? "简略" : "详情", _buttonSmall))
            {
                _statusDetail = !_statusDetail;
            }

            if (GUILayout.Button("重置", _buttonSmall)) gm.ResetPet();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // ------------------------------------------------------------ throw / fetch

        /// <summary>
        /// The aim guide on a phone.
        ///
        /// The desktop throw meter is skipped on touch (its panel would sit under the thumbs),
        /// but the guide is exactly what a thumb needs: without it, aiming with the stick and
        /// throwing with a button is guesswork.
        /// </summary>
        private void ShowMobileAimGuide(PetGameManager gm)
        {
            var player = gm.Player;
            var ball = player != null ? player.Ball : (gm.Room != null ? gm.Room.Ball : null);
            if (ball == null || ball.State != BallState.Held) return;

            DrawAimGuide(gm, ball);
        }

        /// <summary>
        /// Where a thrown ball would land, and the arc it would take to get there.
        ///
        /// Throwing used to be a charge bar and nothing else: you held the button, a bar filled,
        /// the ball disappeared over the horizon and you found out afterwards where it went.
        /// Showing the landing spot turns it into an aimed action — and because the simulation
        /// is a plain ballistic arc, the preview can be the same maths the ball will run rather
        /// than an approximation that drifts.
        /// </summary>
        private void DrawAimGuide(PetGameManager gm, PetBall ball)
        {
            var player = gm.Player;
            var camera = Camera.main;
            if (player == null || ball == null || camera == null) return;

            Vector3 origin = player.transform.position + Vector3.up * 0.55f;
            Vector3 direction = player.AimPoint - player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = player.transform.forward;
            direction.Normalize();

            float speed = Mathf.Lerp(ball.MinThrowSpeed, ball.MaxThrowSpeed, ball.Charge);
            float elevation = ball.ThrowElevationDegrees * Mathf.Deg2Rad;
            Vector3 velocity = direction * (speed * Mathf.Cos(elevation)) + Vector3.up * (speed * Mathf.Sin(elevation));

            // March the same integrator the ball uses, and stop at the floor.
            const float step = 0.05f;
            Vector3 point = origin;
            Vector3 stepVelocity = velocity;
            float groundY = 0.26f;   // the ball's own radius, which is where it comes to rest

            for (int i = 0; i < 80; i++)
            {
                stepVelocity += Vector3.down * (ball.Gravity * step);
                point += stepVelocity * step;
                if (point.y <= groundY) break;

                // Every third sample, so the arc is dotted rather than a solid line.
                if (i % 3 != 0) continue;

                var screen = camera.WorldToScreenPoint(point);
                if (screen.z <= 0f) continue;

                var design = ScreenToDesign(new Vector2(screen.x, Screen.height - screen.y));
                float size = Mathf.Lerp(5f, 9f, ball.Charge);
                GUI.color = new Color(1f, 0.92f, 0.62f, 0.55f);
                GUI.DrawTexture(new Rect(design.x - size * 0.5f, design.y - size * 0.5f, size, size),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // The landing marker: a ring that tightens as the charge fills, so the player can
            // feel the throw firming up without reading the bar.
            Vector3 landing = new Vector3(point.x, 0.02f, point.z);
            var landingScreen = camera.WorldToScreenPoint(landing);
            if (landingScreen.z <= 0f) return;

            var centre = ScreenToDesign(new Vector2(landingScreen.x, Screen.height - landingScreen.y));
            float radius = Mathf.Lerp(34f, 16f, ball.Charge);
            var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            var previous = GUI.color;

            GUI.color = new Color(1f, 0.9f, 0.55f, 0.9f);
            UiSkin.Panel(new Rect(centre.x - radius, centre.y - radius * 0.45f, radius * 2f, radius * 0.9f),
                radius * 0.45f, new Color(1f, 0.9f, 0.55f, 0.18f), new Color(1f, 0.92f, 0.6f, 0.85f), 2f);
            GUI.Label(new Rect(centre.x - 60f, centre.y - radius * 0.45f + 4f, 120f, 20f),
                ball.Charge > 0.85f ? "用力扔！" : "会落在这里", style);
            GUI.color = previous;
        }

        /// <summary>
        /// The throwing loop's only UI. Without it "press E, then hold the left button" is
        /// invisible, and the fetch behaviour — the payoff — never gets discovered.
        /// </summary>
        private void DrawThrowMeter(PetGameManager gm, HudLayout layout)
        {
            var player = gm.Player;
            var ball = player != null ? player.Ball : (gm.Room != null ? gm.Room.Ball : null);
            if (ball == null) return;

            var camera = Camera.main;

            if (ball.State == BallState.Held)
            {
                // Reticle on the floor where the mouse is pointing.
                if (camera != null)
                {
                    Vector3 screen = camera.WorldToScreenPoint(player.AimPoint);
                    if (screen.z > 0f)
                    {
                        var centre = ScreenToDesign(new Vector2(screen.x, Screen.height - screen.y));
                        var dot = new Rect(centre.x - 9f, centre.y - 9f, 18f, 18f);
                        GUI.color = new Color(1f, 0.9f, 0.5f, 0.85f);
                        GUI.Box(dot, GUIContent.none);
                        GUI.color = Color.white;
                    }
                }

                DrawAimGuide(gm, ball);

                var panel = new Rect(DesignWidth * 0.5f - 170f, layout.ChatTop - 84f, 340f, 62f);
                GUI.Box(panel, GUIContent.none, _panel);
                GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, panel.height - 16f));

                GUILayout.Label("按住鼠标左键蓄力，松开扔出去", _small);

                var bar = GUILayoutUtility.GetRect(panel.width - 24f, 16f);
                bar.y += 2f;
                bar.height = 12f;
                GUI.color = new Color(1f, 1f, 1f, 0.20f);
                GUI.Box(bar, GUIContent.none);
                GUI.color = Color.Lerp(new Color(0.6f, 0.9f, 0.6f), new Color(1f, 0.45f, 0.35f), ball.Charge);
                GUI.Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(ball.Charge), bar.height), GUIContent.none);
                GUI.color = Color.white;

                GUILayout.EndArea();
                return;
            }

            // Standing next to a ball that is just lying there: tell the player what to do.
            if (ball.IsAtRest && player != null && player.IsNear(ball.transform.position))
            {
                var hint = new Rect(DesignWidth * 0.5f - 190f, layout.ChatTop - 52f, 380f, 30f);
                GUI.Box(hint, GUIContent.none, _panel);
                GUI.Label(new Rect(hint.x + 12f, hint.y + 6f, hint.width - 24f, 22f),
                    "按 E 拿起小球，蓄力扔出去让它捡", _label);
            }
        }

        private void Bar(string label, float value, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(34f));

            var rect = GUILayoutUtility.GetRect(120f, 14f, GUILayout.ExpandWidth(true));
            rect.y += 3f;
            rect.height = 10f;

            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = value < 0.25f ? Color.Lerp(color, Color.red, 0.55f) : color;
            GUI.Box(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), GUIContent.none);
            GUI.color = Color.white;

            GUILayout.Label($"{value:P0}", _small, GUILayout.Width(38f));
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------- view mode

        /// <summary>
        /// Camera mode buttons, stacked under the species switcher.
        ///
        /// The rig is asked for its current mode rather than the HUD keeping its own copy, so
        /// the highlight cannot drift out of sync with what the camera is actually doing.
        /// </summary>
        private void DrawViewSwitcher(PetGameManager gm, HudLayout layout)
        {
            var rig = gm.CameraRig;
            if (rig == null) return;

            var switcher = layout.Switcher;
            var rect = new Rect(switcher.x, switcher.yMax + 8f, switcher.width, 34f + 3f * 26f);
            GUI.Box(rect, GUIContent.none, _panel);

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, rect.height - 16f));
            GUILayout.Label("视角", _label);

            foreach (CameraViewMode mode in new[]
                     { CameraViewMode.Panorama, CameraViewMode.Free, CameraViewMode.FollowPlayer })
            {
                bool active = rig.View == mode;
                if (GUILayout.Button((active ? "● " : "○ ") + RoomCameraRig.ViewLabel(mode),
                        _buttonSmall, GUILayout.Height(22f)) && !active)
                {
                    rig.SetView(mode);
                }
            }

            GUILayout.EndArea();

            // Free mode is not discoverable without telling the player which buttons it uses.
            if (rig.View == CameraViewMode.Free)
            {
                var hint = new Rect(rect.x - 200f, rect.yMax + 6f, rect.width + 200f, 24f);
                var style = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
                GUI.Label(hint, "右键拖动转视角　滚轮缩放　中键平移", style);
            }
        }

        /// <summary>
        /// Chat on a phone: a prominent one-line bubble by default, expanding into the real
        /// transcript (which also brings up the keyboard).
        ///
        /// The desktop panel owns the bottom third of the screen, which is exactly where
        /// both thumbs live — leaving it open would put the movement stick and the action
        /// buttons underneath a text box. Collapsed it is deliberately loud rather than
        /// discreet: a thin dark strip with small grey text is a chat nobody notices, and the
        /// whole game is the chat.
        /// </summary>
        private void DrawMobileChat(PetGameManager gm, HudLayout layout)
        {
            float height = _chatExpanded ? layout.Chat.height : MobileChatBarHeight;
            var rect = new Rect(layout.Chat.x, layout.Chat.yMax - height, layout.Chat.width, height);

            if (!_chatExpanded)
            {
                UiSkin.Panel(rect, 16f, new Color(0.11f, 0.10f, 0.15f, 0.95f),
                    new Color(1f, 1f, 1f, 0.12f), 2f, shadow: true);

                // Drawn with absolute rects, NOT inside a GUILayout.BeginArea. The widgets are
                // hand-drawn and register themselves in design space, and GUI coordinates
                // inside an area are relative to that area — nesting the two offset every
                // rect by another bar height and pushed the whole bar off the bottom of the
                // screen, which is why the collapsed bar rendered as an empty strip.
                var inner = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f);
                float buttonWidth = MobileUi.Touchable(132f);

                if (MobileWidgets.Button(MobileButtonIds.PetChat,
                        new Rect(inner.x, inner.y, buttonWidth, inner.height),
                        "和它说说话", new Color(0.30f, 0.60f, 0.86f)))
                {
                    _chatExpanded = true;
                    FillEditConfig(gm);
                }

                // The pet's last line gets a bubble of its own, at transcript size: this is the
                // thing that has to catch the eye from across the room.
                var lineRect = new Rect(inner.x + buttonWidth + 10f, inner.y,
                    Mathf.Max(0f, inner.xMax - inner.x - buttonWidth - 10f), inner.height);
                if (lineRect.width > 60f)
                {
                    UiSkin.Panel(lineRect, 12f, new Color(0.98f, 0.93f, 0.84f, 0.95f),
                        new Color(1f, 1f, 1f, 0.10f), 1.5f);
                    GUI.Label(new Rect(lineRect.x + 12f, lineRect.y + 8f,
                        lineRect.width - 24f, lineRect.height - 16f), LastLine(gm), _bubblePet);
                }

                if (UnreadFromPet(gm))
                {
                    // A small pulsing dot: the pet said something and the player has not looked.
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
                    var dot = new Rect(inner.xMax - 14f, inner.y - 2f, 12f, 12f);
                    UiSkin.Panel(dot, 6f, new Color(1f, 0.55f, 0.35f, pulse),
                        new Color(1f, 1f, 1f, 0.5f), 1f);
                }
                return;
            }

            // Expanded: the real transcript, with a close button in its corner.
            DrawChat(gm, layout);
            _unreadMark = gm.Memory.Recent.Count;

            var close = new Rect(rect.xMax - MobileUi.Touchable(88f) - 14f, rect.y + 14f,
                MobileUi.Touchable(88f), MobileUi.Touchable(34f));
            if (MobileWidgets.Button("pet.chatclose", close, "收起", new Color(0.75f, 0.35f, 0.35f)))
            {
                _chatExpanded = false;
                GUI.FocusControl(null);
            }
        }

        private int _unreadMark;

        /// <summary>True when the pet has said something since the transcript was last open.</summary>
        private bool UnreadFromPet(PetGameManager gm)
        {
            var recent = gm.Memory.Recent;
            if (recent.Count == 0) return false;
            if (recent.Count <= _unreadMark) return false;
            return !recent[recent.Count - 1].IsUser;
        }

        private static string LastLine(PetGameManager gm)
        {
            var recent = gm.Memory.Recent;
            if (recent.Count == 0) return "（还没聊过）";

            for (int i = recent.Count - 1; i >= 0; i--)
            {
                if (!recent[i].IsUser)
                {
                    string text = recent[i].Text ?? "";
                    return text.Length > 42 ? text.Substring(0, 42) + "…" : text;
                }
            }

            return "（还没聊过）";
        }

        /// <summary>Where the touch controls sit, in design pixels. Pure, so it is testable.</summary>
        public struct MobileControls
        {
            public Rect StickZone;
            public Rect Action;
            public Rect Throw;
            public Rect Chat;
        }

        /// <summary>Height of the collapsed chat bar on a phone.</summary>
        public const float MobileChatBarHeight = 68f;

        /// <summary>
        /// Geometry for the touch controls.
        ///
        /// The rules encoded here: everything is at least <see cref="MobileUi.MinTouchTarget"/>
        /// across, the action buttons are bottom-right where a right thumb rests, the stick
        /// zone is the bottom-left, and none of them overlap the collapsed chat bar or each
        /// other — overlapping touch targets is how a UI ends up feeling broken on a phone.
        /// (An earlier version put the buttons at the panel's very bottom edge and landed them
        /// right on top of the chat bar; the layout test caught it.)
        ///
        /// They anchor to the viewport, not to the chat panel. With the transcript in a side
        /// column the chat panel is nowhere near the thumbs, and anchoring to it put the
        /// movement stick under the right hand.
        /// </summary>
        public static MobileControls ComputeMobileControls(HudLayout layout)
        {
            var view = layout.Viewport;
            float size = MobileUi.Touchable(96f);
            float gap = 14f;

            // The right thumb's resting place: the bottom-right of the room, which is the
            // viewport's corner or the edge of the sidebar when there is one.
            float right = (layout.ChatOnSide ? Mathf.Min(layout.FreeBand.xMax, view.xMax) : view.xMax) - 16f;

            // Sit above the collapsed chat bar, not on it — but only where the bar really does
            // own that strip.
            float bottom = layout.ChatOnSide
                ? view.yMax - 20f
                : layout.Chat.yMax - MobileChatBarHeight - 14f;

            var action = new Rect(right - size, bottom - size, size, size);
            var throwRect = new Rect(action.x - size - gap, bottom - size, size, size);
            var chat = new Rect(right - MobileUi.Touchable(132f),
                action.y - gap - MobileUi.Touchable(56f),
                MobileUi.Touchable(132f), MobileUi.Touchable(56f));

            // The stick owns the lower-left quadrant, stopping just above the chat bar so a
            // thumb resting near the middle does not grab it by accident.
            float stickTop = bottom - size * 2f;
            var stick = new Rect(view.x, stickTop, view.width * 0.42f, Mathf.Max(size, bottom - stickTop));

            return new MobileControls
            {
                StickZone = stick,
                Action = action,
                Throw = throwRect,
                Chat = chat
            };
        }

        /// <summary>
        /// The touch controls: a floating movement stick on the left, action buttons on the
        /// right. Drawn through <see cref="MobileWidgets"/>, which registers each rect with
        /// the touch layer so several fingers can be used at once.
        /// </summary>
        private void DrawMobileControls(PetGameManager gm, HudLayout layout)
        {
            if (ModalOpen || _chatExpanded) return;

            var controls = ComputeMobileControls(layout);

            // Publish the HUD's design→screen transform so the widgets draw in design space
            // and register their hit areas in screen space.
            MobileWidgets.BeginFrame(Scale, SafeOffset);

            MobileWidgets.StickHint(controls.StickZone);
            MobileWidgets.Joystick();

            var player = gm.Player;
            bool holdingBall = player != null && player.Ball != null && player.Ball.State == BallState.Held;

            MobileWidgets.Button(MobileButtonIds.PetAction, controls.Action,
                ActionLabel(gm), new Color(0.35f, 0.70f, 0.45f));

            if (holdingBall)
            {
                bool charging = MobileTouch.Held(MobileButtonIds.PetThrow);
                MobileWidgets.Button(MobileButtonIds.PetThrow, controls.Throw,
                    charging ? "松手扔出" : "按住蓄力",
                    charging ? new Color(0.95f, 0.55f, 0.25f) : new Color(0.85f, 0.45f, 0.25f));
            }

            if (MobileWidgets.Button(MobileButtonIds.PetChat, controls.Chat,
                    "聊天", new Color(0.35f, 0.62f, 0.85f)))
            {
                _chatExpanded = true;
                FillEditConfig(gm);
            }
        }

        /// <summary>Label for the context action button, so the player knows what it will do.</summary>
        private static string ActionLabel(PetGameManager gm)
        {
            var player = gm.Player;
            if (player == null) return "互动";

            if (player.Ball != null && player.Ball.IsAtRest && player.IsNear(player.Ball.transform.position))
            {
                return "拿球";
            }

            if (player.Nearby != null)
            {
                switch (player.Nearby.Kind)
                {
                    case InteractableKind.Door: return "开门";
                    case InteractableKind.Bed: return "睡觉";
                    default: return "互动";
                }
            }

            return "互动";
        }


        private void DrawChat(PetGameManager gm, HudLayout layout)
        {
            var rect = layout.Chat;

            // A rounded, shadowed panel instead of a flat box: this is the interface the player
            // looks at most, and the stock IMGUI box is what made a working screen look like a
            // debug overlay.
            UiSkin.Panel(rect, 16f, new Color(0.10f, 0.09f, 0.13f, 0.94f),
                new Color(1f, 1f, 1f, 0.10f), 2f, shadow: true);

            float headerHeight = 46f;
            var header = new Rect(rect.x + 18f, rect.y + 12f, rect.width - 36f, headerHeight);
            DrawChatHeader(gm, header);
            UiSkin.Fill(new Rect(rect.x + 14f, header.yMax + 4f, rect.width - 28f, 1f),
                new Color(1f, 1f, 1f, 0.09f));

            // --- transcript ---
            float footerHeight = 96f;
            var logRect = new Rect(rect.x + 8f, header.yMax + 10f, rect.width - 16f,
                Mathf.Max(60f, rect.yMax - footerHeight - header.yMax - 16f));

            DrawTranscript(gm, logRect);

            // --- input row + quick actions ---
            var footer = new Rect(rect.x + 16f, rect.yMax - footerHeight + 8f, rect.width - 32f, footerHeight - 8f);
            DrawChatFooter(gm, footer);
        }

        /// <summary>
        /// The panel's head: who is talking, how they feel, and their temperament.
        ///
        /// The name and the mood used to be nowhere near the transcript — you had to read the
        /// status panel across the screen to know the pet was starving while it chatted happily.
        /// </summary>
        private void DrawChatHeader(PetGameManager gm, Rect rect)
        {
            // Avatar: a soft disc in the species' fur colour, with the initial letter. No art,
            // but it anchors the column and makes the pet feel present in the conversation.
            float disc = Mathf.Min(36f, rect.height);
            var avatarRect = new Rect(rect.x, rect.y + (rect.height - disc) * 0.5f, disc, disc);
            UiSkin.Panel(avatarRect, disc * 0.5f, gm.Species.Fur, new Color(1f, 1f, 1f, 0.35f), 2f);
            GUI.Label(avatarRect, Initial(gm.PetName), _avatar);

            var nameRect = new Rect(avatarRect.xMax + 10f, rect.y, rect.width - disc - 10f, 26f);
            GUI.Label(nameRect, gm.PetName, _title);

            var moodRect = new Rect(avatarRect.xMax + 10f, rect.y + 24f, rect.width - disc - 10f, 20f);
            string mood = PetUtil.MoodLabel(gm.Needs.Mood);
            string temperament = gm.Personality != null ? gm.Personality.Archetype : "";
            GUI.Label(moodRect, $"{mood}　·　{temperament}", _small);

            if (gm.IsThinking)
            {
                var thinking = new Rect(rect.xMax - 92f, rect.y + 12f, 92f, 22f);
                GUI.Label(thinking, "正在想…", _thinking);
            }
        }

        private static string Initial(string name)
        {
            if (string.IsNullOrEmpty(name)) return "·";
            // Chinese names read better as their first character than as a Latin initial.
            return name.Substring(0, 1);
        }

        /// <summary>
        /// The conversation as speech bubbles: the pet on the left in warm cream, the player on
        /// the right in blue.
        ///
        /// Alignment plus colour is what makes a transcript scannable at a glance — the old
        /// "你：…" / "小狐狸：…" prefix lines all looked alike, which is most of why the panel
        /// was easy to ignore.
        /// </summary>
        private void DrawTranscript(PetGameManager gm, Rect rect)
        {
            var recent = gm.Memory.Recent;
            int start = Mathf.Max(0, recent.Count - MaxLogLines);

            _logScroll = GUI.BeginScrollView(rect, _logScroll,
                new Rect(0f, 0f, rect.width - 20f, Mathf.Max(rect.height, (recent.Count - start) * 62f + 12f)));

            var bubble = new GUIStyle(_bubble) { wordWrap = true };

            float y = 6f;
            for (int i = start; i < recent.Count; i++)
            {
                var line = recent[i];
                string text = line.Text ?? "";

                float maxWidth = rect.width * 0.78f;
                float width = Mathf.Min(maxWidth, bubble.CalcSize(new GUIContent(text)).x + 24f);
                width = Mathf.Max(width, 62f);
                float height = Mathf.Max(34f, bubble.CalcHeight(new GUIContent(text), width - 24f) + 16f);

                var bubbleRect = line.IsUser
                    ? new Rect(rect.width - 24f - width, y, width, height)
                    : new Rect(12f, y, width, height);

                var tint = line.IsUser
                    ? new Color(0.30f, 0.52f, 0.78f, 0.95f)
                    : new Color(0.98f, 0.93f, 0.84f, 0.96f);

                UiSkin.Panel(bubbleRect, 12f, tint, new Color(1f, 1f, 1f, 0.12f), 1.5f);

                var textRect = new Rect(bubbleRect.x + 12f, bubbleRect.y + 7f,
                    bubbleRect.width - 24f, bubbleRect.height - 14f);
                GUI.Label(textRect, text, line.IsUser ? _bubbleUser : _bubblePet);

                y += height + 8f;
            }

            if (gm.IsThinking)
            {
                y += 6f;
                GUI.Label(new Rect(16f, y, rect.width - 32f, 24f), "……（它正在想）", _thinking);
            }

            GUI.EndScrollView();

            // Keep the newest line in view: a chat that opens halfway up the history is a chat
            // the player has to fight before they can read the reply they just waited for.
            if (Event.current.type == EventType.Repaint && recent.Count > start)
            {
                float content = y + 12f;
                if (content > _lastChatContent + 1f)
                {
                    _lastChatContent = content;
                    _logScroll.y = Mathf.Max(0f, content - rect.height);
                }
            }
        }

        private float _lastChatContent;

        /// <summary>Input field, send button and the quick actions.</summary>
        private void DrawChatFooter(PetGameManager gm, Rect rect)
        {
            if (!string.IsNullOrEmpty(gm.LastError))
            {
                GUI.color = new Color(1f, 0.62f, 0.56f);
                GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, 20f),
                    "接口出错，已用离线回复：" + gm.LastError, _small);
                GUI.color = Color.white;
            }

            var field = new Rect(rect.x, rect.y + 20f, rect.width - 92f, 38f);
            UiSkin.Panel(field, 10f, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.22f), 1.5f);

            GUI.SetNextControlName("PetInput");
            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                         GUI.GetNameOfFocusedControl() == "PetInput";

            var previous = GUI.skin.textField.fontSize;
            GUI.skin.textField.fontSize = _inputFontSize;
            _input = GUI.TextField(new Rect(field.x + 10f, field.y + 7f, field.width - 20f, 24f),
                _input ?? "", 400);
            GUI.skin.textField.fontSize = previous;
            IsTextInputFocused = GUI.GetNameOfFocusedControl() == "PetInput";

            var sendRect = new Rect(field.xMax + 8f, field.y, 84f, 38f);
            bool send = GUI.Button(sendRect, gm.IsThinking ? "…" : "发送", _sendButton);
            if ((send || enter) && !gm.IsThinking)
            {
                gm.Talk(_input);
                _input = "";
                if (enter) Event.current.Use();
            }

            var actions = new Rect(rect.x, field.yMax + 8f, rect.width, 30f);
            GUILayout.BeginArea(actions);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("摸摸它", _buttonSmall, GUILayout.Height(26f))) gm.QuickAction("pet");
            if (GUILayout.Button("去吃饭", _buttonSmall, GUILayout.Height(26f))) gm.QuickAction("feed");
            if (GUILayout.Button("去玩球", _buttonSmall, GUILayout.Height(26f))) gm.QuickAction("play");

            var audio = PetAudioDirector.Instance;
            if (audio != null)
            {
                if (GUILayout.Button(audio.Muted ? "🔇" : "🔊", _buttonSmall, GUILayout.Width(40f),
                        GUILayout.Height(26f)))
                {
                    audio.ToggleMute();
                }
                audio.SetMasterVolume(GUILayout.HorizontalSlider(audio.MasterVolume, 0f, 1f, GUILayout.Width(70f)));
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(Mobile ? "摇杆走动　按钮互动" : "WASD 走动　E 互动", _small);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------ species switch

        private void DrawSpeciesSwitcher(PetGameManager gm, HudLayout layout)
        {
            var rect = layout.Switcher;
            GUI.Box(rect, GUIContent.none, _panel);

            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f));
            GUILayout.Label("换一只", _label);

            for (int i = 0; i < PetSpecies.Count; i++)
            {
                var species = PetSpecies.Get(i);
                bool active = species.Id == gm.Species.Id;
                string label = (active ? "● " : "○ ") + species.DisplayName;

                if (GUILayout.Button(label, _buttonSmall, GUILayout.Height(24f)) && !active)
                {
                    gm.SwitchSpecies(i);
                }
            }

            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------- overlays

        private void DrawDoorPrompt(PetGameManager gm)
        {
            var games = gm.AvailableMiniGames();
            float h = 150f + games.Count * 84f;
            var rect = OverlayRect(520f, h);
            ModalBackdrop(rect);

            GUILayout.BeginArea(new Rect(rect.x + 18f, rect.y + 16f, rect.width - 36f, rect.height - 32f));

            GUILayout.Label("要出去走走吗？", _title);
            GUILayout.Label("宠物会自己留在家里，回来时它还记得你。", _small);
            GUILayout.Space(10f);

            if (games.Count == 0)
            {
                GUILayout.Label("暂时没有可以去的活动。", _small);
            }
            else
            {
                for (int i = 0; i < games.Count; i++)
                {
                    var game = games[i];
                    if (GUILayout.Button($"{game.Icon}  {game.DisplayName}", _button, GUILayout.Height(36f)))
                    {
                        gm.LaunchMiniGame(game.Id);
                        return;
                    }
                    GUILayout.Label("     " + game.Blurb, _small);
                    GUILayout.Space(6f);
                }
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("还是留在家里", _button, GUILayout.Height(32f))) gm.CloseDoorPrompt();

            GUILayout.EndArea();
        }

        private void DrawOverlays(PetGameManager gm, HudLayout layout)
        {
            // "press E" prompt for whatever the character is standing next to. Anchored to the
            // chat panel rather than to the bottom of the screen, so it can never land on top
            // of the input box on a short view.
            if (gm.Player != null && gm.Player.Nearby != null && !gm.DoorPromptOpen)
            {
                string label = gm.Player.Nearby.Kind == InteractableKind.Door
                    ? gm.Player.Nearby.Label + (Mobile ? "　点互动出去" : "　按 E 出去")
                    : gm.Player.Nearby.Label + (Mobile ? "　点互动让宠物过来" : "　按 E 让宠物过来");

                // On a phone the collapsed chat bar only occupies the bottom strip, so the
                // prompt hangs just above it instead of floating in the middle of the room.
                float promptBottom = Mobile
                    ? layout.Chat.yMax - MobileChatBarHeight - 26f
                    : layout.ChatTop - 32f;

                var style = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1f, 0.95f, 0.8f, 0.95f);
                GUI.Label(new Rect(DesignWidth * 0.5f - 200f, promptBottom, 400f, 24f), label, style);
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(_cursorHint))
            {
                // Event.current.mousePosition is already in the current GUI space, so it needs
                // no conversion — unlike WorldToScreenPoint just below.
                var pos = Event.current.mousePosition;
                GUI.Label(new Rect(pos.x + 16f, pos.y + 8f, 320f, 24f), _cursorHint, _small);
            }

            var cam = Camera.main;
            if (cam != null && gm.Avatar != null)
            {
                var moodLabel = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(WorldLabelRect(cam, gm.Avatar.transform.position + Vector3.up * 1.1f, 120f, 22f),
                    PetUtil.MoodLabel(gm.Needs.Mood), moodLabel);
            }
        }

        // ------------------------------------------------------------------- settings

        private void OpenSettings(PetGameManager gm)
        {
            _showSettings = !_showSettings;
            if (!_showSettings) return;

            FillEditConfig(gm);
            _testResult = "";

            // Put the caret in the first field so the player can type immediately instead of
            // having to click it — and so a stray chat focus cannot swallow the first keystroke.
            _focusFirstField = true;
        }

        private void DrawSettings(PetGameManager gm)
        {
            // Tall enough for the labels, three fields, both toggles and the status lines. The
            // earlier 330px panel was shorter than its own content, so GUILayout — which does
            // not clip, but does stop a group from receiving input past its rect — pushed the
            // action buttons out of the panel and out of reach.
            var rect = OverlayRect(520f, 430f);
            ModalBackdrop(rect);

            const float footerHeight = 52f;
            var inner = new Rect(rect.x + 16f, rect.y + 14f, rect.width - 32f, rect.height - 28f);

            // Fields scroll if the viewport is short; the buttons stay pinned and clickable.
            var body = new Rect(inner.x, inner.y, inner.width, inner.height - footerHeight);
            GUILayout.BeginArea(body);
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll);

            GUILayout.Label("宠物大脑设置", _title);
            GUILayout.Label("默认指向 DeepSeek 官方接口；任何 OpenAI 兼容端点都可以填在这里。", _small);
            GUILayout.Space(8f);

            GUILayout.Label("Base URL", _small);
            GUI.SetNextControlName("PetBaseUrl");
            _editBaseUrl = GUILayout.TextField(_editBaseUrl ?? "", 300, GUILayout.Height(26f));
            Diagnostics.BaseUrlRect = GUILayoutUtility.GetLastRect();

            if (_focusFirstField)
            {
                // Must be requested from inside OnGUI, after the control has been drawn: the
                // name lookup is built during the pass.
                _focusFirstField = false;
                GUI.FocusControl("PetBaseUrl");
            }

            GUILayout.Label("模型", _small);
            GUI.SetNextControlName("PetModel");
            _editModel = GUILayout.TextField(_editModel ?? "", 120, GUILayout.Height(26f));

            GUILayout.Label("API Key（留空则使用离线大脑）", _small);
            GUI.SetNextControlName("PetApiKey");
            _editKey = GUILayout.PasswordField(_editKey ?? "", '*', 200, GUILayout.Height(26f));
            Diagnostics.KeyRect = GUILayoutUtility.GetLastRect();

            GUILayout.Space(6f);
            _editAnonymous = GUILayout.Toggle(_editAnonymous, " 允许无鉴权（本地网关）", _small);
            _editOffline = GUILayout.Toggle(_editOffline, " 强制离线模式", _small);

            // The haptics switch lives here rather than in its own panel: it is one bit, and a
            // player who wants it off looks in the settings they already know about. Applied
            // immediately, because a feedback setting you have to "save" is a feedback setting
            // you cannot feel your way to.
            if (Mobile)
            {
                GUILayout.Space(6f);
                bool vibrationOn = GUILayout.Toggle(MobileHaptics.Enabled, " 振动反馈（轻/中/重）", _small);
                if (vibrationOn != MobileHaptics.Enabled)
                {
                    MobileHaptics.Enabled = vibrationOn;
                    if (vibrationOn) MobileHaptics.Light();   // confirm it can be felt
                }
            }

            // A pet's temperament is the most personal thing about it, so the player gets to
            // reroll it — but it is buried here rather than offered at every launch, because
            // "who is this animal" is not a decision to make every time you open the game.
            GUILayout.Space(8f);
            GUILayout.Label("性格：" + gm.Personality.Archetype, _label);
            GUILayout.Label(gm.Personality.Summary, _small);
            if (GUILayout.Button("换一个性格", _buttonSmall, GUILayout.Height(28f)))
            {
                gm.RerollPersonality();
            }

            GUILayout.Space(8f);
            GUILayout.Label("当前：" + gm.BrainConfig.Describe(), _small);
            GUILayout.Label("状态：" + gm.BrainConfig.StatusDetail(), _small);
            if (!string.IsNullOrEmpty(_testResult))
            {
                GUI.color = _testResult.StartsWith("成功") ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.75f, 0.6f);
                GUILayout.Label(_testResult, _small);
                GUI.color = Color.white;
            }

            Diagnostics.FocusedControl = GUI.GetNameOfFocusedControl();
            Diagnostics.KeyboardControl = GUIUtility.keyboardControl;
            Diagnostics.EditBaseUrl = _editBaseUrl;
            Diagnostics.PanelRect = rect;

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            var footer = new Rect(inner.x, inner.yMax - footerHeight + 6f, inner.width, footerHeight);
            GUILayout.BeginArea(footer);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("保存并应用", _button, GUILayout.Height(34f)))
            {
                ApplyEditConfig(gm);
                gm.BrainConfig.Save();
                gm.RebuildBrain();
                _showSettings = false;
            }

            if (GUILayout.Button("测试连接", _button, GUILayout.Height(34f))) TestConnection();

            if (GUILayout.Button("读环境变量", _button, GUILayout.Height(34f)))
            {
                // Re-resolve from DEEPSEEK_*/OPENAI_* and drop the saved overrides, which is
                // the escape hatch when a saved blank key is hiding a working environment.
                var resolved = PetBrainConfig.FromEnvironment();
                gm.BrainConfig = resolved;
                gm.RebuildBrain();
                FillEditConfig(gm);
                _testResult = "已从环境变量重新读取：" + resolved.Describe();
            }

            if (GUILayout.Button("取消", _button, GUILayout.Height(34f))) _showSettings = false;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void FillEditConfig(PetGameManager gm)
        {
            _editBaseUrl = gm.BrainConfig.BaseUrl;
            _editModel = gm.BrainConfig.Model;
            _editKey = gm.BrainConfig.ApiKey;
            _editAnonymous = gm.BrainConfig.AllowAnonymous;
            _editOffline = gm.BrainConfig.ForceOffline;
            _editExtraInstructions = gm.BrainConfig.ExtraInstructions ?? "";
        }

        private PetBrainConfig BuildEditConfig()
        {
            return new PetBrainConfig
            {
                BaseUrl = _editBaseUrl,
                Model = _editModel,
                ApiKey = _editKey,
                AllowAnonymous = _editAnonymous,
                ForceOffline = _editOffline,
                Temperature = 1.25f,
                MaxTokens = 64,
                TimeoutSeconds = 25
            };
        }

        private void ApplyEditConfig(PetGameManager gm)
        {
            gm.BrainConfig.BaseUrl = _editBaseUrl;
            gm.BrainConfig.Model = _editModel;
            gm.BrainConfig.ApiKey = _editKey;
            gm.BrainConfig.AllowAnonymous = _editAnonymous;
            gm.BrainConfig.ForceOffline = _editOffline;
        }

        /// <summary>Fires one tiny request so the player can tell a bad key from a bad URL.</summary>
        private void TestConnection()
        {
            if (_testing) return;

            var config = BuildEditConfig();
            if (!config.CanUseNetwork)
            {
                _testResult = "配置不完整：" + config.StatusDetail();
                return;
            }

            _testing = true;
            _testResult = "测试中……";

            StartCoroutine(OpenAiClient.Chat(config, "你是一只宠物，只回答很短的一句。", null,
                "在吗？",
                (text, error) =>
                {
                    _testing = false;
                    _testResult = string.IsNullOrEmpty(error)
                        ? "成功：" + (text ?? "").Trim().Replace("\n", " ")
                        : "失败：" + error;
                }));
        }

        /// <summary>
        /// Opens the prompt panel, seeded from the saved settings.
        ///
        /// The seed matters: without it the editable box starts empty while the saved value is
        /// something else, which reads as "my text was lost" and makes the dirty marker lie.
        /// </summary>
        private void OpenPromptPreview(PetGameManager gm)
        {
            _showPromptPreview = !_showPromptPreview;
            if (!_showPromptPreview) return;

            FillEditConfig(gm);
        }

        private void DrawPromptPreview(PetGameManager gm)
        {
            var rect = OverlayRect(700f, 540f);
            ModalBackdrop(rect);

            const float footerHeight = 52f;
            var inner = new Rect(rect.x + 16f, rect.y + 14f, rect.width - 32f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28f), "发给模型的实际提示词", _title);

            var body = new Rect(inner.x, inner.y + 34f, inner.width, inner.height - 34f - footerHeight);
            var textStyle = new GUIStyle(GUI.skin.textArea) { wordWrap = true, fontSize = 12 };

            // The generated prompt is a live preview: it is rebuilt from the pet's state every
            // frame, so it cannot be edited in place. It is shown in a TextArea purely so it can
            // be scrolled and Ctrl+C'd — the returned string is deliberately discarded.
            // (Editing it used to look possible but every keystroke was reverted, and the
            // ExpandHeight text area below pushed the 关闭 button out of the panel entirely,
            // so the panel could not be dismissed at all.)
            string preview = gm.PreviewSystemPrompt();

            float extraHeight = 30f + 4f * textStyle.lineHeight + 14f;
            float previewHeight = body.height - extraHeight;

            if (previewHeight > 90f)
            {
                GUILayout.BeginArea(new Rect(body.x, body.y, body.width, previewHeight));
                _promptScroll = GUILayout.BeginScrollView(_promptScroll);
                GUILayout.Label(preview, textStyle);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            // Editable, and the only part that persists.
            var editTop = body.yMax - extraHeight + 8f;
            GUI.Label(new Rect(body.x, editTop, body.width, 22f),
                "额外要求（追加在系统提示后面，会保存）", _label);

            var editRect = new Rect(body.x, editTop + 24f, body.width, extraHeight - 32f);
            _editExtraInstructions = GUI.TextArea(editRect, _editExtraInstructions ?? "", 1200, textStyle);
            Diagnostics.FocusedControl = GUI.GetNameOfFocusedControl();
            Diagnostics.KeyboardControl = GUIUtility.keyboardControl;

            var footer = new Rect(inner.x, inner.yMax - footerHeight + 8f, inner.width, footerHeight);
            GUILayout.BeginArea(footer);
            GUILayout.BeginHorizontal();

            bool dirty = (gm.BrainConfig.ExtraInstructions ?? "") != (_editExtraInstructions ?? "");
            GUI.color = dirty ? new Color(1f, 0.9f, 0.6f) : Color.white;
            string saveLabel = dirty ? "保存额外要求 *" : "保存额外要求";
            if (GUILayout.Button(saveLabel, _button, GUILayout.Height(34f)))
            {
                gm.BrainConfig.ExtraInstructions = _editExtraInstructions ?? "";
                gm.BrainConfig.Save();
                gm.RebuildBrain();
            }
            GUI.color = Color.white;

            if (GUILayout.Button("复制全部提示词", _button, GUILayout.Height(34f)))
            {
                GUIUtility.systemCopyBuffer = preview +
                    (string.IsNullOrWhiteSpace(_editExtraInstructions)
                        ? ""
                        : "\n\n【主人的额外要求】\n" + _editExtraInstructions.Trim());
            }

            if (GUILayout.Button("关闭", _button, GUILayout.Height(34f))) _showPromptPreview = false;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawModalEscape();
        }

        /// <summary>
        /// Esc (or a right-click) closes whatever panel is open. Every modal gets this, because
        /// a panel whose only exit is a button that might not be reachable is a trap.
        /// </summary>
        private void DrawModalEscape()
        {
            var e = Event.current;
            if (e == null) return;
            bool escape = e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape;
            if (!escape) return;

            _showPromptPreview = false;
            _showSettings = false;
            _showJournal = false;
            var gm = PetGameManager.Instance;
            if (gm != null && gm.DoorPromptOpen) gm.CloseDoorPrompt();
            e.Use();
        }

        // -------------------------------------------------------------------- journal

        private static Color KindColor(MemoryKind kind)
        {
            switch (kind)
            {
                case MemoryKind.Chat: return new Color(0.55f, 0.78f, 1f);
                case MemoryKind.Care: return new Color(0.55f, 0.92f, 0.62f);
                case MemoryKind.Play: return new Color(0.98f, 0.82f, 0.38f);
                case MemoryKind.Rest: return new Color(0.72f, 0.66f, 0.98f);
                case MemoryKind.Mood: return new Color(0.98f, 0.62f, 0.75f);
                case MemoryKind.Promise: return new Color(1f, 0.66f, 0.35f);
                case MemoryKind.Preference: return new Color(0.45f, 0.92f, 0.92f);
                case MemoryKind.Milestone: return new Color(1f, 0.88f, 0.45f);
                default: return Color.white;
            }
        }

        private static string KindLabel(MemoryKind kind)
        {
            switch (kind)
            {
                case MemoryKind.Chat: return "聊天";
                case MemoryKind.Care: return "照顾";
                case MemoryKind.Play: return "玩耍";
                case MemoryKind.Rest: return "休息";
                case MemoryKind.Mood: return "心情";
                case MemoryKind.Promise: return "约定";
                case MemoryKind.Preference: return "记下";
                case MemoryKind.Milestone: return "里程碑";
                default: return kind.ToString();
            }
        }

        private void DrawJournal(PetGameManager gm)
        {
            var rect = OverlayRect(720f, 560f);
            float w = rect.width;
            ModalBackdrop(rect);

            var journal = gm.Journal;
            var header = new GUIStyle(_title) { alignment = TextAnchor.MiddleLeft };
            var dayStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(4, 4, 3, 3) };

            // --- title row ---
            GUI.Label(new Rect(rect.x + 18f, rect.y + 10f, 300f, 30f), "记事本", header);

            if (GUI.Button(new Rect(rect.xMax - 300f, rect.y + 14f, 34f, 26f), "◀", _buttonSmall))
            {
                _journalMonth = _journalMonth.AddMonths(-1);
            }
            GUI.Label(new Rect(rect.xMax - 262f, rect.y + 16f, 150f, 24f),
                $"{_journalMonth:yyyy 年 M 月}", _small);
            if (GUI.Button(new Rect(rect.xMax - 120f, rect.y + 14f, 34f, 26f), "▶", _buttonSmall))
            {
                _journalMonth = _journalMonth.AddMonths(1);
            }
            if (GUI.Button(new Rect(rect.xMax - 76f, rect.y + 14f, 58f, 26f), "关闭", _buttonSmall))
            {
                _showJournal = false;
                return;
            }

            // --- month grid ---
            // The cell height is derived from the room actually available, so a six-row month
            // still leaves space for the day's entries on a short Game view. Fixed 46px cells
            // used to push the detail list off the bottom of the panel.
            float gridX = rect.x + 18f;
            float gridY = rect.y + 52f;
            float cellW = (w - 36f) / 7f;

            const float detailReserve = 116f;   // the "N 条" line plus a usable list
            float cellH = Mathf.Clamp((rect.height - 52f - 20f - detailReserve) / 6f, 24f, 46f);

            string[] weekdays = { "一", "二", "三", "四", "五", "六", "日" };
            for (int i = 0; i < 7; i++)
            {
                var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(gridX + i * cellW, gridY - 20f, cellW, 18f), weekdays[i], style);
            }

            var first = new DateTime(_journalMonth.Year, _journalMonth.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7; // Monday-first
            int daysInMonth = DateTime.DaysInMonth(_journalMonth.Year, _journalMonth.Month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_journalMonth.Year, _journalMonth.Month, day);
                int slot = offset + day - 1;
                int col = slot % 7;
                int row = slot / 7;

                var cell = new Rect(gridX + col * cellW + 2f, gridY + row * cellH + 2f, cellW - 4f, cellH - 4f);
                bool selected = date.Date == _selectedDay.Date;
                bool today = date.Date == DateTime.Now.Date;

                GUI.color = selected ? new Color(1f, 0.92f, 0.7f, 0.95f)
                    : (today ? new Color(0.75f, 0.85f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.16f));
                if (GUI.Button(cell, GUIContent.none, dayStyle))
                {
                    _selectedDay = date;
                }
                GUI.color = Color.white;
                GUI.Label(new Rect(cell.x + 4f, cell.y + 2f, cell.width, 16f), day.ToString(), _small);

                // Activity dots: one per kind recorded that day.
                var kinds = journal.KindsOn(date);
                for (int k = 0; k < kinds.Count && k < 4; k++)
                {
                    GUI.color = KindColor(kinds[k]);
                    GUI.Box(new Rect(cell.x + 5f + k * 9f, cell.y + cell.height - 12f, 7f, 7f), GUIContent.none);
                }
                GUI.color = Color.white;
            }

            // --- selected day detail ---
            float detailY = gridY + 6f * cellH + 12f;
            var entries = journal.ForDay(_selectedDay);

            GUI.Label(new Rect(gridX, detailY, 400f, 22f),
                $"{_selectedDay:yyyy-MM-dd}　{entries.Count} 条", _label);

            var listRect = new Rect(gridX, detailY + 26f, w - 36f, rect.yMax - detailY - 46f);
            if (listRect.height < 32f)
            {
                GUI.Label(new Rect(gridX, listRect.y, w - 36f, 22f),
                    "窗口太矮，把 Game 视图拉高就能看到当天记录。", _small);
                return;
            }

            GUI.Box(listRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(listRect.x + 10f, listRect.y + 8f, listRect.width - 20f, listRect.height - 16f));
            _dayScroll = GUILayout.BeginScrollView(_dayScroll);

            if (entries.Count == 0)
            {
                GUILayout.Label("这一天什么也没发生。", _small);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    GUILayout.BeginHorizontal();
                    GUI.color = KindColor(entry.Kind);
                    GUILayout.Label($"[{KindLabel(entry.Kind)}]", _small, GUILayout.Width(58f));
                    GUI.color = Color.white;
                    GUILayout.Label(entry.When.ToString("HH:mm"), _small, GUILayout.Width(46f));
                    GUILayout.Label(entry.Title + (entry.Pinned ? "　📌" : ""), _label);
                    GUILayout.EndHorizontal();

                    if (!string.IsNullOrEmpty(entry.Detail))
                    {
                        GUILayout.Label("    " + entry.Detail, _small);
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
