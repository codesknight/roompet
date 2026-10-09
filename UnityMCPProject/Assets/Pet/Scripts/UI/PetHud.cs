using System;
using System.Collections.Generic;
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
        private GUIStyle _chipActive;
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
        private bool _showCollection;
        private bool _showPuzzle;
        private bool _showMemory;
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

            // The pet chip that is currently selected. Coloured rather than merely bold, because
            // "which animal am I looking at" is the one thing the card must never be vague about.
            _chipActive = new GUIStyle(_buttonSmall) { fontStyle = FontStyle.Bold };
            _chipActive.normal.textColor = new Color(1f, 0.94f, 0.75f);
            _chipActive.hover.textColor = new Color(1f, 0.97f, 0.85f);

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
            DrawPetCards(gm, layout);
            if (Mobile) DrawMobileChat(gm, layout);
            else DrawChat(gm, layout);
            DrawViewSwitcher(gm, layout);
            if (!Mobile) DrawThrowMeter(gm, layout);
            else ShowMobileAimGuide(gm);
            DrawOverlays(gm, layout);
            GUI.enabled = true;

            // Controls sit above the room but below any modal panel.
            if (Mobile) DrawMobileControls(gm, layout);

            if (gm.DoorPromptOpen) DrawMapPanel(gm);
            if (_showPuzzle) DrawPuzzle(gm);
            if (_showMemory) DrawMemoryMatch(gm);
            if (_showSettings) DrawSettings(gm);
            if (_showPromptPreview) DrawPromptPreview(gm);
            if (_showJournal) DrawJournal(gm);
            if (_showCollection) DrawCollection(gm);

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
            _ = speciesCount;   // the card is sized by how many pets are in the room, not by species

            float margin = 14f;
            float chatWidth = Mathf.Clamp(w * 0.30f, 340f, 520f);

            var chat = new Rect(w - chatWidth - margin, margin, chatWidth, h - margin * 2f);

            // The left column is as wide as the space the sidebar leaves, capped so the status
            // panel does not stretch into a wall of text.
            float columnWidth = Mathf.Clamp(chat.x - margin * 2f - 8f, 220f, 340f);

            float left = margin;
            var status = new Rect(left, margin, columnWidth, Mathf.Clamp(h * 0.44f, 190f, 400f));
            var switcher = new Rect(left, status.yMax + 10f, columnWidth,
                Mathf.Min(ViewCardHeight, Mathf.Max(60f, h - status.yMax - 20f)));

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
            _ = speciesCount;

            // The transcript is the main event in this app, so it takes a third of the view.
            float chatHeight = Mathf.Clamp(h * 0.34f, 200f, 340f);
            var chat = new Rect(14f, h - chatHeight - 14f, Mathf.Max(260f, w - 28f), chatHeight);

            // Right-aligned view card, and a pet card narrow enough that the two can never
            // collide even on a small Game view.
            float switchWidth = Mathf.Clamp(w * 0.22f, 150f, 176f);
            var switcher = new Rect(w - switchWidth - 14f, 14f, switchWidth, ViewCardHeight);

            float statusWidth = Mathf.Clamp(w - switchWidth - 40f, 232f, 300f);

            // The pet card gets whatever room is left above the chat panel, never less than
            // enough for its chips, its header and some detail.
            float available = chat.y - 14f - 10f;
            var status = new Rect(14f, 14f, statusWidth, Mathf.Clamp(available, 168f, 440f));

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

        /// <summary>
        /// Height of the view card (视角). The species card that used to share this column is
        /// gone — the backpack decides who lives here — so the view buttons now own it outright.
        /// </summary>
        public const float ViewCardHeight = 34f + 3f * 26f;

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
                if (hud._showSettings || hud._showJournal || hud._showPromptPreview ||
                    hud._showCollection || hud._showPuzzle || hud._showMemory) return true;
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

        // ------------------------------------------------------------------ pet cards

        /// <summary>Height of one row of pet chips.</summary>
        private const float ChipHeight = 30f;

        private const float ChipSpacing = 6f;

        /// <summary>One card per pet, with the pets themselves as the tabs.</summary>
        public struct PetCardLayout
        {
            public Rect Panel;

            /// <summary>Whether the detail is drawn. False when the player collapsed it — or
            /// when the card would not fit and fell back to the short form.</summary>
            public bool Expanded;

            public float ChipsHeight;
            public float HeaderHeight;
            public float FooterHeight;
            public float ScrollHeight;
            public string[][] FooterRows;
            public int ChipRows;
        }

        /// <summary>
        /// How many rows the pet chips need, and how tall that is.
        ///
        /// The chips carry the pets' names, which the player chooses, so their width is not
        /// knowable in advance — the same packing rule the footer buttons use applies here, and
        /// for the same reason: a chip pushed outside its area renders and cannot be tapped.
        /// </summary>
        public static int ChipRowsFor(string[] names, float availableWidth, int fontSize)
            => PackRows(names, Mathf.Max(60f, availableWidth - RowPackingSlack), fontSize,
                buttonPadding: 20f, spacing: ChipSpacing).Length;

        /// <summary>Name-row height in the expanded card: the editable name plus the mood line.</summary>
        private const float CardHeaderHeight = 70f;

        /// <summary>Name-row height in the collapsed card: one line only.</summary>
        private const float CardHeaderCompactHeight = 28f;

        /// <summary>Below this there is no room for a need bar worth reading.</summary>
        private const float MinDetailHeight = 30f;

        /// <summary>
        /// Where everything in the pet card goes.
        ///
        /// Split out and static so the geometry — which is what "no overlapping UI" actually
        /// means — can be tested at every viewport without a running game, exactly like the
        /// footer packing above it. Three rules are encoded here:
        ///  1. the card is only as tall as its content, so a collapsed card gives the room back;
        ///  2. the detail is the only elastic part, and the pinned buttons are never pushed out
        ///     of the card — the failure this whole file has a history of;
        ///  3. when even the short form's buttons do not fit the space, the card falls back to
        ///     the collapsed shape rather than drawing controls past its own edge.
        /// </summary>
        public static PetCardLayout ComputeCardLayout(Rect status, string[] chipNames,
            bool expanded, bool primary, int chipFontSize, int buttonFontSize)
        {
            var inner = new Rect(status.x + 14f, status.y + 12f, status.width - 28f, status.height - 24f);

            int chipRows = ChipRowsFor(chipNames, inner.width, chipFontSize);
            float chips = chipRows * ChipHeight + Mathf.Max(0, chipRows - 1) * ChipSpacing;

            var layout = new PetCardLayout { ChipRows = chipRows, ChipsHeight = chips };

            if (expanded)
            {
                var rows = primary
                    ? StatusFooterRows(Mobile, false, inner.width, buttonFontSize, collapsed: false)
                    : new[] { CompanionFooterRow(inner.width, buttonFontSize, collapsed: false) };
                float footer = FooterHeight(rows.Length);
                float room = inner.height - chips - CardHeaderHeight - footer;

                if (room >= MinDetailHeight)
                {
                    layout.Expanded = true;
                    layout.HeaderHeight = CardHeaderHeight;
                    layout.FooterHeight = footer;
                    layout.FooterRows = rows;
                    layout.ScrollHeight = room;
                    layout.Panel = new Rect(status.x, status.y, status.width,
                        Mathf.Min(status.height, 12f + chips + CardHeaderHeight + room + footer + 12f));
                    return layout;
                }
            }

            // Collapsed — either because the player asked for it, or because the space can only
            // hold the short form. The chips and the navigation buttons stay: a collapsed card
            // that hides the way to the notebook and the settings would be a trap.
            var shortRows = primary
                ? StatusFooterRows(Mobile, false, inner.width, buttonFontSize, collapsed: true)
                : new[] { CompanionFooterRow(inner.width, buttonFontSize, collapsed: true) };
            float shortFooter = FooterHeight(shortRows.Length);

            // Last resort: trim the chip rows so the pinned buttons still fit. This only ever
            // bites below the game's own limit of three pets in a room — the assertion that it
            // never bites in practice is a test, not a hope — and trimming is the least bad of
            // the three options, the others being buttons outside the panel (this file's oldest
            // bug) and a card taller than the viewport (its second oldest).
            float chipRoom = inner.height - CardHeaderCompactHeight - shortFooter;
            int maxChipRows = Mathf.Max(1,
                Mathf.FloorToInt((chipRoom + ChipSpacing) / (ChipHeight + ChipSpacing)));
            if (chipRows > maxChipRows)
            {
                chipRows = maxChipRows;
                chips = chipRows * ChipHeight + Mathf.Max(0, chipRows - 1) * ChipSpacing;
                layout.ChipRows = chipRows;
                layout.ChipsHeight = chips;
            }

            layout.Expanded = false;
            layout.HeaderHeight = CardHeaderCompactHeight;
            layout.FooterHeight = shortFooter;
            layout.FooterRows = shortRows;
            layout.ScrollHeight = 0f;
            layout.Panel = new Rect(status.x, status.y, status.width,
                Mathf.Min(status.height,
                    12f + chips + CardHeaderCompactHeight + shortFooter + 12f));
            return layout;
        }

        /// <summary>
        /// Shortens a name so a chip cannot eat the whole row.
        ///
        /// Names are the player's to choose, and "一只名字很长的宠物" three times over would push
        /// the chips into a stack taller than the card — at which point the pets stop being tabs
        /// and become the panel. The full name is always in the card's header and in the 宠物
        /// panel; the chip only has to be recognisable.
        /// </summary>
        public static string ShortenChip(string name, float maxWidth, int fontSize)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (EstimatedLabelWidth(name, fontSize) <= maxWidth) return name;

            string result = "";
            foreach (char c in name)
            {
                if (EstimatedLabelWidth(result + c + "…", fontSize) > maxWidth) break;
                result += c;
            }
            return result.Length == 0 ? name.Substring(0, 1) : result + "…";
        }

        /// <summary>The one row of buttons a companion's card gets.</summary>
        private static string[] CompanionFooterRow(float availableWidth, int buttonFontSize,
            bool collapsed)
        {
            var labels = collapsed
                ? new[] { "展开", "在背包里管理" }
                : new[] { "在背包里管理", "收起" };
            var rows = PackRows(labels, Mathf.Max(60f, availableWidth - RowPackingSlack),
                buttonFontSize);
            return rows.Length > 0 ? rows[0] : labels;
        }

        /// <summary>
        /// The status card: one pet, or one chip per pet when several are in the room.
        ///
        /// This used to be a single panel hard-wired to the primary pet, with a separate "换一只"
        /// card listing the four species — which was both redundant (the backpack already decides
        /// who lives here) and wrong the moment the backpack could hold three: the room showed
        /// three animals and the panel described one of them with no way to tell which.
        ///
        /// The chips are the answer. They are the pets themselves, in the order they exist, and
        /// tapping a pet in the room selects its chip — so "which animal is this card about" is
        /// always the one the player just touched.
        /// </summary>
        private void DrawPetCards(PetGameManager gm, HudLayout layout)
        {
            var cards = gm.Cards();
            if (cards.Count == 0) return;

            int selected = Mathf.Clamp(gm.SelectedPetIndex, 0, cards.Count - 1);
            var card = cards[selected];
            bool expanded = gm.CardExpanded;

            var names = new string[cards.Count];
            float chipBudget = Mathf.Max(40f, (layout.Status.width - 28f) * 0.45f);
            for (int i = 0; i < cards.Count; i++)
            {
                names[i] = ShortenChip(ChipLabel(cards[i], cards[i].Name), chipBudget,
                    _buttonSmall.fontSize);
            }

            var plan = ComputeCardLayout(layout.Status, names, expanded, card.Primary,
                _buttonSmall.fontSize, _buttonSmall.fontSize);

            var rect = plan.Panel;
            GUI.Box(rect, GUIContent.none, _panel);

            var inner = new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f);

            // ---- the pets, as chips ----
            float y = inner.y;
            DrawPetChips(gm, cards, names, inner, plan.ChipRows, ref y);

            // ---- whose card this is ----
            var headerRect = new Rect(inner.x, y, inner.width, plan.HeaderHeight);
            GUILayout.BeginArea(headerRect);

            string mood = card.Needs != null ? PetUtil.MoodLabel(card.Needs.Mood) : "—";
            string need = card.Needs != null ? card.Needs.DominantNeed : "";
            string who = card.Species != null ? card.Species.DisplayName : "";
            string summary = $"{mood}　·　{(string.IsNullOrEmpty(need) ? "状态不错" : "想要：" + need)}";

            if (!plan.Expanded)
            {
                // Collapsed: one line, nothing interactive but the chips and the footer buttons.
                GUILayout.Label($"{card.Name}　·　{summary}", _label);
            }
            else if (card.Primary)
            {
                DrawNameRow(gm);
                GUILayout.Label($"{summary}　·　{who}", _small);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUI.color = card.Species != null ? card.Species.Fur : Color.white;
                GUILayout.Label("●", _label, GUILayout.Width(18f));
                GUI.color = Color.white;
                GUILayout.Label(card.Name + "（同伴）", _label);
                GUILayout.EndHorizontal();
                GUILayout.Label($"{summary}　·　{who}", _small);
            }

            GUILayout.EndArea();
            y = headerRect.yMax;

            if (!plan.Expanded)
            {
                DrawCardFooter(gm, inner, rect, plan);
                return;
            }

            // ---- detail ----
            var scrollRect = new Rect(inner.x, y, inner.width, plan.ScrollHeight);
            GUILayout.BeginArea(scrollRect);
            _statusScroll = GUILayout.BeginScrollView(_statusScroll, GUILayout.ExpandHeight(true));

            if (card.Primary && !gm.BrainConfig.CanUseNetwork)
            {
                // The "no brain configured" button lives in the scroll area rather than in the
                // pinned footer. It is a one-off setup step, and a pinned footer that has to hold
                // it plus the button rows is a footer that no longer fits a short panel.
                GUI.color = new Color(1f, 0.86f, 0.55f);
                if (GUILayout.Button("⚙ 配置大脑连接（当前离线）", _buttonSmall, GUILayout.Height(FooterRowHeight)))
                {
                    OpenSettings(gm);
                }
                GUI.color = Color.white;
            }

            DrawNeedsBars(card.Needs);

            if (card.Personality != null)
            {
                GUILayout.Label("性格：" + gm.Personality.Archetype, _small);
                if (!Mobile || _statusDetail) GUILayout.Label(card.Personality.Summary, _small);
            }

            GUILayout.Label("音色：" + PetVoice.Describe(card.Species, card.Personality), _small);

            if (!card.Primary)
            {
                GUI.color = new Color(0.78f, 0.88f, 1f);
                GUILayout.Label("同伴：自己走动、有自己的状态与音色，摸它有反应；它不接大脑，所以不花 token。", _small);
                GUILayout.Label("想换主要照顾的那只，去「宠物」面板的背包页。", _small);
                GUI.color = Color.white;
            }
            else if (!Mobile || _statusDetail)
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
            if (card.Primary)
            {
                GUILayout.Label($"{gm.Journal.Count} 条记忆　·　{card.Needs.MoodScore:P0} 状态分", _small);
            }
            else
            {
                GUILayout.Label($"状态分 {card.Needs.MoodScore:P0}　·　亲密度 {card.Needs.Affection:P0}", _small);
            }

            // A little air at the end of the scroll content, so the last line does not come to
            // rest flush against the pinned buttons and read as if it were cut off by them.
            GUILayout.Space(10f);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawCardFooter(gm, inner, rect, plan);
        }

        /// <summary>
        /// The card's pinned buttons. Pinned, not part of the scroll: the notebook, the pets,
        /// the map and the settings are how the player leaves this panel, and a navigation button
        /// that can be scrolled out of reach is a trap.
        /// </summary>
        private void DrawCardFooter(PetGameManager gm, Rect inner, Rect rect, PetCardLayout plan)
        {
            var footer = new Rect(inner.x, rect.yMax - 12f - plan.FooterHeight, inner.width,
                plan.FooterHeight);
            GUILayout.BeginArea(footer);

            foreach (var row in plan.FooterRows)
            {
                GUILayout.BeginHorizontal();
                foreach (string label in row)
                {
                    if (GUILayout.Button(label, _buttonSmall, GUILayout.Height(FooterRowHeight)))
                    {
                        HandleFooterButton(label, gm);
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        /// <summary>The row (or rows) of pet chips, and the select/collapse behaviour.</summary>
        private void DrawPetChips(PetGameManager gm, List<PetGameManager.PetCard> cards,
            string[] names, Rect area, int maxRows, ref float y)
        {
            var packed = PackRows(names, Mathf.Max(60f, area.width - RowPackingSlack),
                _buttonSmall.fontSize, buttonPadding: 20f, spacing: ChipSpacing);

            int rowCount = Mathf.Clamp(maxRows, 0, packed.Length);
            int index = 0;
            float rowY = y;

            for (int r = 0; r < rowCount; r++)
            {
                // Positioned by hand rather than by GUILayout: the wrap was decided against the
                // estimated width, and GUILayout would re-measure every label with the real font
                // and wrap again — which is how a chip ends up in a different row than the one it
                // was measured for.
                float x = area.x;
                foreach (string label in packed[r])
                {
                    if (index >= cards.Count) break;

                    float chipWidth = Mathf.Min(area.width,
                        EstimatedLabelWidth(label, _buttonSmall.fontSize) + 20f);
                    var chipRect = new Rect(x, rowY, chipWidth, ChipHeight);

                    bool isSelected = index == gm.SelectedPetIndex;
                    if (GUI.Button(chipRect, label, isSelected ? _chipActive : _buttonSmall))
                    {
                        gm.SelectPet(index, toggleIfSame: true);
                    }

                    x += chipWidth + ChipSpacing;
                    index++;
                }

                rowY += ChipHeight + ChipSpacing;
            }

            y = rowCount > 0 ? rowY - ChipSpacing : y;
        }

        private static string ChipLabel(PetGameManager.PetCard card, string name)
        {
            if (card.Needs == null) return name;

            // One mark per pet, so the row reads as three animals rather than three buttons.
            //
            // ASCII on purpose: "★" and "●" look right in a design tool and come out of Unity's
            // built-in IMGUI font as a blank box, which is how the first version of this row ended
            // up with three identical-looking chips and no way to tell which pet was which. The
            // selected chip is coloured by its style, so the mark only has to carry "this one has
            // something urgent" and "this one is the pet you are looking after".
            if (!string.IsNullOrEmpty(card.Needs.DominantNeed)) return "! " + name;
            return card.Primary ? "* " + name : name;
        }

        /// <summary>The four need bars plus the bladder when it matters, for one pet.</summary>
        private void DrawNeedsBars(PetNeeds needs)
        {
            if (needs == null) return;

            Bar("饱食", needs.Hunger, new Color(0.95f, 0.62f, 0.30f));
            Bar("精力", needs.Energy, new Color(0.45f, 0.80f, 0.95f));
            Bar("开心", needs.Joy, new Color(0.98f, 0.80f, 0.35f));
            Bar("清洁", needs.Cleanliness, new Color(0.60f, 0.90f, 0.65f));

            // The bladder only appears once it matters: a permanent bar that spends most of its
            // life full is noise, while a bar that appears when the pet starts fidgeting is a
            // warning the player actually reads.
            if (needs.Bladder < 0.6f)
            {
                Bar("便意", needs.Bladder, new Color(0.85f, 0.72f, 0.45f));
            }
        }

        /// <summary>Height of one packed footer row. Fixed, so the footer can be sized for it.</summary>
        private const float FooterRowHeight = 26f;

        private const float FooterRowSpacing = 6f;

        /// <summary>
        /// How tall the status panel's pinned footer has to be for the rows it will hold.
        ///
        /// Split out and public so the vertical half of the "buttons must fit" contract can be
        /// tested next to the horizontal one.
        /// </summary>
        public static float FooterHeight(int rowCount,
            float rowHeight = FooterRowHeight, float spacing = FooterRowSpacing)
        {
            if (rowCount <= 0) return 0f;
            return rowCount * rowHeight + (rowCount - 1) * spacing;
        }

        /// <summary>Routes a footer button by its label, so the row layout stays declarative.</summary>
        private void HandleFooterButton(string label, PetGameManager gm)
        {
            if (label.EndsWith("本子") || label.EndsWith("记事本")) _showJournal = !_showJournal;
            else if (label.EndsWith("设置")) OpenSettings(gm);
            else if (label == "提示词") OpenPromptPreview(gm);
            else if (label == "详情" || label == "简略") _statusDetail = !_statusDetail;
            else if (label.Contains("宠物") || label.Contains("背包")) _showCollection = !_showCollection;
            else if (label.Contains("收起")) gm.SelectPet(gm.SelectedPetIndex, toggleIfSame: true);
            else if (label.Contains("地图"))
            {
                if (gm.DoorPromptOpen) gm.CloseDoorPrompt();
                else gm.OpenDoorPrompt();
            }
            else if (label == "重置") gm.ResetPet();
        }

        /// <summary>
        /// Pixels held back when packing the status footer, so an estimate error cannot push a
        /// button out of its area.
        ///
        /// <see cref="EstimatedLabelWidth"/> counts CJK as one em and emoji as one em and does
        /// not measure the font at all, so a row packed to within a few pixels of the panel is a
        /// row where the real text can be the thing that overflows — and an overflowing footer
        /// button is silently unclickable, which is the failure this whole mechanism exists for.
        /// </summary>
        public const float RowPackingSlack = 24f;

        /// <summary>
        /// The status panel's footer buttons, packed into rows that fit the width available.
        ///
        /// Split out so the fit can be tested. A GUILayout area silently stops delivering input
        /// past its own rect, so a row one button too wide produces a button that looks fine and
        /// does nothing — and the test that checks this found exactly that on a 640x400 window,
        /// where four desktop buttons wanted 282px of a 272px panel.
        ///
        /// Packing rather than a fixed split by platform, because the available width depends on
        /// the viewport, not on whether the device has a touchscreen.
        ///
        /// No emoji on these labels, unlike the rest of the UI. Each one is a surrogate pair, so
        /// the width estimate charges it two ems — a row of icons costs a whole extra row at every
        /// panel width this layout can produce, and that extra row is exactly what put 重置 outside
        /// the area that delivers clicks. A button that cannot be tapped is worse than a missing
        /// decoration.
        /// </summary>
        public static string[][] StatusFooterRows(bool mobile, bool detailOn, float availableWidth,
            int fontSize, bool collapsed = false)
        {
            string detailLabel = detailOn ? "简略" : "详情";

            // Collapsed, the card keeps the way out and nothing else. 展开 first, because it is
            // the button that undoes the state the player is in.
            var labels = collapsed
                ? new[] { "展开", "地图", "设置" }
                : (mobile
                    ? new[] { "收起", "本子", "宠物", "地图", "设置", "提示词", detailLabel, "重置" }
                    : new[] { "收起", "记事本", "宠物", "地图", "设置", "提示词", "重置" });

            float budget = Mathf.Max(96f, availableWidth - RowPackingSlack);
            return PackRows(labels, budget, fontSize);
        }

        /// <summary>Greedily packs labels into rows that each fit <paramref name="available"/>.</summary>
        public static string[][] PackRows(string[] labels, float available, int fontSize,
            float buttonPadding = 18f, float spacing = 6f)
        {
            var rows = new System.Collections.Generic.List<string[]>();
            var current = new System.Collections.Generic.List<string>();
            float width = 0f;

            foreach (string label in labels)
            {
                float item = EstimatedLabelWidth(label, fontSize) + buttonPadding;
                float withSpacing = item + (current.Count > 0 ? spacing : 0f);

                if (current.Count > 0 && width + withSpacing > available)
                {
                    rows.Add(current.ToArray());
                    current.Clear();
                    width = 0f;
                    withSpacing = item;
                }

                current.Add(label);
                width += withSpacing;
            }

            if (current.Count > 0) rows.Add(current.ToArray());
            return rows.ToArray();
        }

        /// <summary>
        /// Rough width of a label at a given font size.
        ///
        /// CJK glyphs are square (one em each) and Latin ones about half that, which is close
        /// enough to catch "this row cannot fit" without a font metrics dependency. Emoji count
        /// as wide, since they render at roughly a full em.
        /// </summary>
        public static float EstimatedLabelWidth(string label, int fontSize)
        {
            if (string.IsNullOrEmpty(label)) return 0f;

            float em = fontSize;
            float width = 0f;
            foreach (char c in label)
            {
                if (c < 0x2E80) width += em * 0.56f;          // Latin, digits, punctuation
                else width += em;                             // CJK, kana, emoji blocks
            }
            return width;
        }

        /// <summary>Width one row of footer buttons needs, including the skin's padding.</summary>
        public static float EstimatedRowWidth(string[] labels, int fontSize, float buttonPadding = 18f,
            float spacing = 6f)
        {
            if (labels == null || labels.Length == 0) return 0f;

            float width = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                width += EstimatedLabelWidth(labels[i], fontSize) + buttonPadding;
                if (i > 0) width += spacing;
            }
            return width;
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
            var rect = new Rect(switcher.x, switcher.y, switcher.width, ViewCardHeight);
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
                MobileWidgets.BeginFrame(Scale, SafeOffset);

                var inner = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f);

                // The microphone belongs here, in the bar, where the thumb already is: a separate
                // corner button would be a second way to do the same thing, and the first version
                // put it to the left of the input *outside* the panel, where it was simply not on
                // screen. Space is reserved for it instead of hoped for.
                bool micHere = DshMobile.MobileStt.Offered;
                DshMobile.MobileStt.Tick();
                float micSize = MobileUi.Touchable(52f);
                var micRect = new Rect(inner.x, inner.y + (inner.height - micSize) * 0.5f, micSize, micSize);

                if (micHere)
                {
                    bool listening = DshMobile.MobileStt.Listening;
                    if (MobileWidgets.CircleButton(MobileButtonIds.PetMic, micRect,
                            listening ? "停" : "说",
                            listening ? new Color(0.90f, 0.42f, 0.36f) : new Color(0.34f, 0.60f, 0.86f)))
                    {
                        // Speaking is a conversation, so tapping the microphone opens the
                        // transcript as well: the recognised words have to land somewhere visible,
                        // and a player who has to tap twice to be heard will not bother.
                        _chatExpanded = true;
                        FillEditConfig(gm);
                        DshMobile.MobileStt.StartListening();
                        _unreadMark = gm.Memory.Recent.Count;
                        return;
                    }

                    string heard = DshMobile.MobileStt.TakeResult();
                    if (!string.IsNullOrEmpty(heard))
                    {
                        _chatExpanded = true;
                        _input = heard;
                    }
                }

                float sayX = micHere ? micRect.xMax + 8f : inner.x;
                float sayWidth = MobileUi.Touchable(124f);
                var sayRect = new Rect(sayX, inner.y, sayWidth, inner.height);

                if (MobileWidgets.Button(MobileButtonIds.PetChat, sayRect, "和它说说话",
                        new Color(0.30f, 0.60f, 0.86f)))
                {
                    _chatExpanded = true;
                    FillEditConfig(gm);
                }

                // The pet's last line gets a bubble of its own, at transcript size: this is the
                // thing that has to catch the eye from across the room.
                var lineRect = new Rect(sayRect.xMax + 10f, inner.y,
                    Mathf.Max(0f, inner.xMax - sayRect.xMax - 10f), inner.height);
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

            // A system note is not the pet talking: the unread dot means "it said something to
            // you", and flashing it for a settings hint would be a lie.
            for (int i = recent.Count - 1; i >= _unreadMark && i >= 0; i--)
            {
                if (recent[i].IsSystem) continue;
                return !recent[i].IsUser;
            }
            return false;
        }

        private static string LastLine(PetGameManager gm)
        {
            var recent = gm.Memory.Recent;
            if (recent.Count == 0) return "（还没聊过）";

            for (int i = recent.Count - 1; i >= 0; i--)
            {
                if (recent[i].IsUser || recent[i].IsSystem) continue;
                string text = recent[i].Text ?? "";
                    return text.Length > 42 ? text.Substring(0, 42) + "…" : text;
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
            public Rect Mic;
        }

        /// <summary>Height of the collapsed chat bar on a phone.</summary>
        public const float MobileChatBarHeight = 68f;

        /// <summary>
        /// Geometry for the touch controls.
        ///
        /// The rules encoded here: everything is at least <see cref="MobileUi.MinTouchTarget"/>
        /// across, the action buttons are a stack in the bottom-right where a right thumb rests,
        /// the stick zone is the bottom-left, and none of them overlap the collapsed chat bar or
        /// each other — overlapping touch targets is how a UI ends up feeling broken on a phone.
        ///
        /// The shape is the one phone games converged on: one big primary button, secondary
        /// buttons stacked above it (not beside it — a row of equal squares to the left of the
        /// primary is a desktop toolbar, and it crowds the middle of the screen where the room
        /// is), and the chat pill above the stack, out of the way of both thumbs.
        /// </summary>
        public static MobileControls ComputeMobileControls(HudLayout layout)
        {
            var view = layout.Viewport;
            float primary = MobileUi.Touchable(104f);
            float secondary = MobileUi.Touchable(84f);
            float gap = 12f;
            float margin = 18f;

            // The right thumb's resting place: the bottom-right of the room, which is the
            // viewport's corner or the edge of the sidebar when there is one.
            float right = (layout.ChatOnSide ? Mathf.Min(layout.FreeBand.xMax, view.xMax) : view.xMax) - margin;

            // Sit above the collapsed chat bar, not on it — but only where the bar really does
            // own that strip.
            float bottom = layout.ChatOnSide
                ? view.yMax - margin
                : layout.Chat.yMax - MobileChatBarHeight - 10f;

            var action = new Rect(right - primary, bottom - primary, primary, primary);
            var throwRect = new Rect(right - secondary, action.y - gap - secondary, secondary, secondary);

            // The chat pill above the stack, wide enough to read as "say something" rather than as
            // another action button.
            var chat = new Rect(right - MobileUi.Touchable(124f),
                throwRect.y - gap - MobileUi.Touchable(50f),
                MobileUi.Touchable(124f), MobileUi.Touchable(50f));

            // The mic lives in the chat bar itself (see DrawMobileChat): a thumb reaching for
            // "talk to the pet" is already there, and a separate button in the corner would be a
            // second way to do the same thing.
            var mic = new Rect(0f, 0f, 0f, 0f);

            // The stick owns the lower-left quadrant, stopping just above the chat bar so a
            // thumb resting near the middle does not grab it by accident.
            float stickTop = bottom - primary * 1.8f;
            var stick = new Rect(view.x, stickTop, view.width * 0.42f, Mathf.Max(primary, bottom - stickTop));

            return new MobileControls
            {
                StickZone = stick,
                Action = action,
                Throw = throwRect,
                Chat = chat,
                Mic = mic
            };
        }

        /// <summary>
        /// The touch controls: a floating movement stick on the left, a stack of round action
        /// buttons on the right. Drawn through <see cref="MobileWidgets"/>, which registers each
        /// rect with the touch layer so several fingers can be used at once.
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

            // Registered in bottom-up draw order, and the touch layer prefers the smallest rect
            // under the finger — so the chat pill never swallows the action button below it.
            if (MobileWidgets.CircleButton(MobileButtonIds.PetChat, controls.Chat, "聊天",
                    new Color(0.28f, 0.54f, 0.84f)))
            {
                _chatExpanded = true;
                FillEditConfig(gm);
            }

            // The throw button keeps its slot whether or not there is a ball in hand. It used to be
            // drawn only while holding one, which made the whole stack jump up the moment the player
            // picked the ball up — a control that moves under the thumb is a control that gets
            // mis-tapped.
            bool charging = holdingBall && MobileTouch.Held(MobileButtonIds.PetThrow);
            MobileWidgets.CircleButton(MobileButtonIds.PetThrow, controls.Throw,
                charging ? "扔出" : "蓄力",
                charging ? new Color(0.94f, 0.52f, 0.24f) : new Color(0.80f, 0.42f, 0.22f),
                enabled: holdingBall);

            MobileWidgets.CircleButton(MobileButtonIds.PetAction, controls.Action,
                ActionLabel(gm), ActionTint(gm));
        }

        /// <summary>Colour for the primary action, so the button says what it will do.</summary>
        private static Color ActionTint(PetGameManager gm)
        {
            var player = gm.Player;
            if (player == null) return new Color(0.35f, 0.68f, 0.46f);

            if (player.Ball != null && player.Ball.IsAtRest && player.IsNear(player.Ball.transform.position))
            {
                return new Color(0.90f, 0.62f, 0.28f);   // 拿球
            }

            if (player.Nearby != null && player.Nearby.Kind == InteractableKind.Bed)
            {
                return new Color(0.48f, 0.46f, 0.78f);   // 睡觉
            }

            return new Color(0.35f, 0.68f, 0.46f);
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

                // A note from the game itself: centred, grey, no bubble on either side. It has to
                // be readable as "the game is telling you something", not as the pet speaking.
                if (line.IsSystem)
                {
                    var noteStyle = new GUIStyle(_small)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        fontStyle = FontStyle.Italic
                    };
                    noteStyle.normal.textColor = new Color(0.78f, 0.80f, 0.88f, 0.9f);

                    float noteHeight = Mathf.Max(20f,
                        noteStyle.CalcHeight(new GUIContent(text), rect.width - 40f));
                    GUI.Label(new Rect(20f, y, rect.width - 40f, noteHeight), text, noteStyle);
                    y += noteHeight + 10f;
                    continue;
                }

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

            // The microphone takes its space OUT of the input row rather than being drawn beside
            // it: the first version put it at `field.x - 46`, which on a phone is off the left edge
            // of the panel — a button that was never on screen, while the settings happily reported
            // that voice input was ready.
            DshMobile.MobileStt.Tick();
            bool micHere = DshMobile.MobileStt.Offered;
            float micWidth = micHere ? 46f : 0f;

            var field = new Rect(rect.x + micWidth, rect.y + 20f, rect.width - 92f - micWidth, 38f);
            UiSkin.Panel(field, 10f, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.22f), 1.5f);

            if (micHere)
            {
                var mic = new Rect(rect.x, field.y, 40f, 38f);
                bool listening = DshMobile.MobileStt.Listening;
                MobileWidgets.BeginFrame(Scale, SafeOffset);
                if (MobileWidgets.CircleButton(MobileButtonIds.PetMic, mic,
                        listening ? "停" : "说",
                        listening ? new Color(0.90f, 0.42f, 0.36f) : new Color(0.34f, 0.60f, 0.86f)))
                {
                    if (listening) DshMobile.MobileStt.StopListening();
                    else DshMobile.MobileStt.StartListening();
                }

                string heard = DshMobile.MobileStt.TakeResult();
                if (!string.IsNullOrEmpty(heard))
                {
                    _input = heard;
                    SetVoiceMessage("听到了：" + heard + "（可以改，再点发送）", false);
                }
                else if (!string.IsNullOrEmpty(DshMobile.MobileStt.LastError) &&
                         !DshMobile.MobileStt.Listening)
                {
                    SetVoiceMessage(DshMobile.MobileStt.LastError, true);
                }
            }

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

            // The microphone sits inside the input row (see above), so there is nothing to draw
            // beside the field — the duplicate button that used to live here was the one that fell
            // off the left edge of the panel.

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

            // The control hint is dropped when the panel is too narrow to hold it. It is the
            // least important thing in the row, and letting it push the audio slider out of the
            // footer would leave a control that is drawn but unreachable.
            float chips = EstimatedRowWidth(new[] { "摸摸它", "去吃饭", "去玩球" }, _buttonSmall.fontSize);
            if (rect.width > chips + 150f)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(Mobile ? "摇杆走动　按钮互动" : "WASD 走动　E 互动", _small);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // -------------------------------------------------------------- memory match

        private PetMemoryMatch _memory;
        private bool _memoryPaid;
        private float _memoryOpenedAt;
        private float _memoryFlipAt;
        private string _memoryMessage = "";
        private int _memoryWins;
        private int _memoryBestMoves;

        /// <summary>
        /// Opens 记忆配对 — the second game that lives indoors, and the one that is pure UI.
        ///
        /// A card game needs no scene, no physics and no camera: it is a grid of buttons and a rule
        /// about two of them. Building it as a panel means it can be opened from the map while the
        /// pet is walking around behind it, which is exactly where a quiet game belongs.
        /// </summary>
        public void OpenMemoryMatch(PetGameManager gm)
        {
            _showMemory = !_showMemory;
            if (!_showMemory) return;

            // Six pairs: enough to need attention, few enough to finish in a minute or two.
            _memory = PetMemoryMatch.Start(6, UnityEngine.Random.Range(0, 1 << 28));
            _memoryPaid = false;
            _memoryOpenedAt = Time.realtimeSinceStartup;
            SetMemoryMessage("翻开两张一样的就留下，不一样会自己盖回去。");
        }

        private void DrawMemoryMatch(PetGameManager gm)
        {
            if (_memory == null) OpenMemoryMatch(gm);
            if (_memory == null) return;

            float w = Mathf.Min(640f, DesignWidth - 32f);
            float h = Mathf.Min(700f, DesignHeight - 32f);
            var rect = OverlayRect(w, h);
            ModalBackdrop(rect);

            var inner = new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width * 0.5f, 32f), "记忆配对", _title);

            var balance = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight };
            GUI.color = new Color(1f, 0.9f, 0.6f);
            GUI.Label(new Rect(inner.x + inner.width * 0.5f, inner.y, inner.width * 0.5f - 84f, 32f),
                $"🐾 {DshMobile.PetWallet.Coins:N0}", balance);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(inner.xMax - 76f, inner.y + 2f, 76f, 30f), "关闭", _button))
            {
                _showMemory = false;
                return;
            }

            GUI.Label(new Rect(inner.x, inner.y + 36f, inner.width, 22f),
                $"配成 {_memory.Matched}/{_memory.Pairs} 对　·　{_memory.Moves} 步　·　" +
                $"{(int)(Time.realtimeSinceStartup - _memoryOpenedAt)} 秒", _small);

            if (!string.IsNullOrEmpty(_memoryMessage))
            {
                GUI.color = _memory.IsSolved ? new Color(0.7f, 0.95f, 0.75f) : new Color(0.85f, 0.88f, 0.95f);
                GUI.Label(new Rect(inner.x, inner.y + 58f, inner.width, 22f), _memoryMessage, _small);
                GUI.color = Color.white;
            }

            // A mismatched pair stays visible for a moment before it goes back: the pause is the
            // whole memory part of the game.
            if (_memory.WaitingToHide && Time.realtimeSinceStartup - _memoryFlipAt > 0.85f)
            {
                _memory.HideMismatch();
            }

            // ---- the board ----
            const float footer = 78f;
            float boardTop = inner.y + 86f;
            float boardRoom = inner.yMax - footer - boardTop;

            int columns = PetMemoryMatch.Columns;
            int rows = Mathf.CeilToInt(_memory.Count / (float)columns);
            float cell = Mathf.Min((inner.width - (columns - 1) * 8f) / columns,
                (boardRoom - (rows - 1) * 8f) / Mathf.Max(1, rows));
            cell = Mathf.Max(52f, cell);

            float boardWidth = columns * cell + (columns - 1) * 8f;
            float boardHeight = rows * cell + (rows - 1) * 8f;
            float boardX = inner.x + (inner.width - boardWidth) * 0.5f;
            float boardY = boardTop + Mathf.Max(0f, (boardRoom - boardHeight) * 0.5f);

            for (int i = 0; i < _memory.Count; i++)
            {
                int row = i / columns, column = i % columns;
                var card = new Rect(boardX + column * (cell + 8f), boardY + row * (cell + 8f), cell, cell);

                bool faceUp = _memory.IsFaceUp(i) || _memory.IsTaken(i);
                var tint = faceUp
                    ? new Color(0.98f, 0.94f, 0.86f, 1f)
                    : new Color(0.28f, 0.36f, 0.55f, 1f);

                UiSkin.Panel(card, 12f, tint,
                    _memory.IsTaken(i)
                        ? new Color(0.55f, 0.85f, 0.6f, 0.9f)
                        : new Color(1f, 1f, 1f, 0.2f), 2f);

                if (faceUp)
                {
                    var face = new GUIStyle(_title)
                    {
                        fontSize = Mathf.RoundToInt(cell * 0.42f),
                        alignment = TextAnchor.MiddleCenter
                    };
                    face.normal.textColor = new Color(0.16f, 0.14f, 0.18f);
                    GUI.Label(card, PetMemoryMatch.Label(_memory.FaceAt(i)), face);
                }
                else if (!_memory.IsSolved && _memory.CanFlip(i))
                {
                    if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                    {
                        if (_memory.Flip(i))
                        {
                            _memoryFlipAt = Time.realtimeSinceStartup;
                            PetAudioDirector.Instance?.Play(SfxId.PickUp);
                            DshMobile.MobileHaptics.Light();
                            if (_memory.LastFlipMatched) FinishMemoryPair(gm);
                        }
                    }
                }
            }

            // ---- footer ----
            var footerArea = new Rect(inner.x, inner.yMax - footer + 8f, inner.width, footer - 8f);
            GUILayout.BeginArea(footerArea);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重开一局", _button, GUILayout.Height(34f)))
            {
                _memory = PetMemoryMatch.Start(6, UnityEngine.Random.Range(0, 1 << 28));
                _memoryPaid = false;
                _memoryOpenedAt = Time.realtimeSinceStartup;
                SetMemoryMessage("重新洗牌了。");
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"玩成 {_memoryWins} 次　·　最少 {(_memoryBestMoves > 0 ? _memoryBestMoves.ToString() : "-")} 步", _small);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawModalEscape();
        }

        private void SetMemoryMessage(string message) => _memoryMessage = message;

        /// <summary>Pays out when the last pair is found, once per board.</summary>
        private void FinishMemoryPair(PetGameManager gm)
        {
            if (!_memory.IsSolved) return;

            _memoryWins++;
            if (_memoryBestMoves == 0 || _memory.Moves < _memoryBestMoves) _memoryBestMoves = _memory.Moves;

            if (_memoryPaid)
            {
                SetMemoryMessage("又配对完了。");
                return;
            }

            _memoryPaid = true;
            int coins = _memory.PendingReward;
            DshMobile.PetWallet.Add(coins);
            DshMobile.MobileHaptics.Medium();

            gm.Memory.AddPet($"（和你玩记忆配对，{_memory.Moves} 步就全找齐了）");
            gm.Journal.Add(MemoryKind.Play, "玩记忆配对",
                $"{_memory.Moves} 步配完 {_memory.Pairs} 对，赚了 {coins} 个宠物币", 0.45f);
            gm.AnnounceChat();

            SetMemoryMessage($"全配上了！赚了 {coins} 个宠物币（{PetMemoryMatch.RankFor(_memory.Pairs, _memory.Moves)}）");
        }


        private PetPuzzle _puzzle;
        private Texture2D _puzzleArt;
        private bool _puzzlePaid;
        private bool _puzzleShowFull;
        private float _puzzleStartedAt;
        private int _puzzleBestMoves;
        private int _puzzleWins;

        /// <summary>
        /// Opens the picture puzzle — the mini game that lives indoors.
        ///
        /// The runner and this are deliberately different kinds of game. The runner is a reflex
        /// game that needs its own scene, its own camera and a controller; this is a puzzle that
        /// needs a board and a picture, and both of those are things the room can already draw.
        /// Making it a scene would have meant a third .unity file for something that fits in a
        /// panel.
        /// </summary>
        public void OpenPuzzle(PetGameManager gm)
        {
            _showPuzzle = !_showPuzzle;
            if (!_showPuzzle) return;

            // The picture is painted for this pet and this room, so the puzzle is always about
            // somewhere the player has actually been.
            if (_puzzleArt != null) Destroy(_puzzleArt);
            var info = DshPet.RoomThemeInfo.Get(DshPet.PetWorldMap.Current);
            _puzzleArt = PuzzleArt.Paint(gm.Species, info, UnityEngine.Random.Range(0, 1 << 30));

            _puzzle = PetPuzzle.Start(UnityEngine.Random.Range(0, 1 << 30));
            _puzzlePaid = false;
            _puzzleShowFull = false;
            _puzzleStartedAt = Time.realtimeSinceStartup;
        }

        private void DrawPuzzle(PetGameManager gm)
        {
            if (_puzzle == null || _puzzleArt == null) OpenPuzzle(gm);

            float w = Mathf.Min(680f, DesignWidth - 32f);
            float h = Mathf.Min(700f, DesignHeight - 32f);
            var rect = OverlayRect(w, h);
            ModalBackdrop(rect);

            var inner = new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width * 0.5f, 32f), "拼图", _title);

            var reward = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight };
            GUI.color = new Color(1f, 0.9f, 0.6f);
            GUI.Label(new Rect(inner.x + inner.width * 0.5f, inner.y, inner.width * 0.5f - 84f, 32f),
                $"🐾 {DshMobile.PetWallet.Coins:N0}", reward);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(inner.xMax - 76f, inner.y + 2f, 76f, 30f), "关闭", _button))
            {
                _showPuzzle = false;
                return;
            }

            float par = PetPuzzle.Par;
            string status = _puzzle.IsSolved
                ? $"拼好了！用了 {_puzzle.Moves} 步（参考 {par:F0} 步）"
                : $"{_puzzle.Moves} 步　·　参考 {par:F0} 步　·　" +
                  $"{(int)(Time.realtimeSinceStartup - _puzzleStartedAt)} 秒";
            GUI.Label(new Rect(inner.x, inner.y + 36f, inner.width, 22f), status, _small);

            if (!string.IsNullOrEmpty(_puzzleMessage))
            {
                var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft };
                GUI.color = _puzzleError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);
                GUI.Label(new Rect(inner.x, inner.y + 58f, inner.width, 22f), _puzzleMessage, style);
                GUI.color = Color.white;
            }

            // ---- the board ----
            const float footer = 78f;
            float boardTop = inner.y + 84f;
            float boardRoom = inner.yMax - footer - boardTop;
            float side = Mathf.Min(inner.width * 0.72f, boardRoom);
            side = Mathf.Max(side, 120f);

            var board = new Rect(inner.x + (inner.width - side) * 0.5f, boardTop, side, side);
            UiSkin.Panel(new Rect(board.x - 6f, board.y - 6f, board.width + 12f, board.height + 12f),
                12f, new Color(0f, 0f, 0f, 0.35f), new Color(1f, 1f, 1f, 0.18f), 1.5f);

            float cell = side / PetPuzzle.Size;
            for (int i = 0; i < PetPuzzle.TileCount; i++)
            {
                int row = i / PetPuzzle.Size, column = i % PetPuzzle.Size;
                var cellRect = new Rect(board.x + column * cell + 1f, board.y + row * cell + 1f,
                    cell - 2f, cell - 2f);
                int value = _puzzle[i];

                if (value == PetPuzzle.Empty)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.45f);
                    GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    continue;
                }

                // The tile shows the slice of the picture that belongs in this cell when solved...
                GUI.DrawTextureWithTexCoords(cellRect, _puzzleArt, PuzzleArt.TileUv(value - 1));

                // ...and its number, because a painted picture has large flat areas and two
                // pieces of sky are genuinely hard to tell apart. The number is what makes the
                // puzzle solvable by thinking instead of by squinting.
                var numberStyle = new GUIStyle(_small) { alignment = TextAnchor.UpperLeft };
                numberStyle.normal.textColor = new Color(1f, 1f, 1f, 0.72f);
                GUI.Label(new Rect(cellRect.x + 5f, cellRect.y + 3f, cellRect.width - 8f, 18f),
                    value.ToString(), numberStyle);

                if (_puzzle.IsSolved) continue;

                bool movable = _puzzle.CanSlide(i);
                if (movable && GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                {
                    if (_puzzle.TrySlide(i))
                    {
                        PetAudioDirector.Instance?.Play(SfxId.PickUp);
                        DshMobile.MobileHaptics.Light();
                        if (_puzzle.IsSolved) FinishPuzzle(gm);
                    }
                }
            }

            // ---- footer ----
            var footerArea = new Rect(inner.x, inner.yMax - footer + 8f, inner.width, footer - 8f);
            GUILayout.BeginArea(footerArea);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("打乱重来", _button, GUILayout.Height(34f)))
            {
                _puzzle = PetPuzzle.Start(UnityEngine.Random.Range(0, 1 << 30));
                _puzzlePaid = false;
                _puzzleStartedAt = Time.realtimeSinceStartup;
                SetPuzzleMessage("重新打乱了，慢慢来。");
            }

            _puzzleShowFull = GUILayout.Toggle(_puzzleShowFull, " 看原图", _small, GUILayout.Width(90f));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"拼好 {_puzzleWins} 次　·　最少 {(_puzzleBestMoves > 0 ? _puzzleBestMoves.ToString() : "-")} 步", _small);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // The reference picture, drawn small in the corner while the toggle is on.
            if (_puzzleShowFull)
            {
                var preview = new Rect(inner.xMax - 84f, board.yMax + 6f, 72f, 72f);
                if (preview.yMax < inner.yMax - footer)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.9f);
                    GUI.DrawTexture(preview, _puzzleArt, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
            }

            DrawModalEscape();
        }

        private string _puzzleMessage = "";
        private bool _puzzleError;

        private void SetPuzzleMessage(string message, bool error = false)
        {
            _puzzleMessage = message;
            _puzzleError = error;
        }

        /// <summary>Pays out once, records it, and says so in the conversation.</summary>
        private void FinishPuzzle(PetGameManager gm)
        {
            _puzzleWins++;
            if (_puzzleBestMoves == 0 || _puzzle.Moves < _puzzleBestMoves) _puzzleBestMoves = _puzzle.Moves;

            if (_puzzlePaid)
            {
                SetPuzzleMessage("又拼好了一次。");
                return;
            }

            _puzzlePaid = true;
            int coins = PetPuzzle.Reward(_puzzle.Moves);
            DshMobile.PetWallet.Add(coins);
            DshMobile.MobileHaptics.Medium();

            gm.Memory.AddPet($"（和你一起把拼图拼好了，{_puzzle.Moves} 步）");
            gm.Journal.Add(MemoryKind.Play, "一起拼好了拼图",
                $"{_puzzle.Moves} 步，赚了 {coins} 个宠物币", 0.5f);
            gm.AnnounceChat();

            SetPuzzleMessage($"拼好了！赚了 {coins} 个宠物币（宠物币可以在地图里换新房间）。");
        }

        // The "换一只" card used to live here: four buttons, one per species, that swapped the
        // pet outright. It is gone, and deliberately. Once the backpack could hold three pets and
        // the shop could sell a fourth, that card was a second, worse way to do what the
        // collection panel already does — and it silently *replaced* the animal you had been
        // looking after, memory and all, from a button that looked like a tab. Which pet lives in
        // this room is a decision for the 宠物 panel; the card is for reading the pets that are
        // already here.

        // ------------------------------------------------------------------- overlays

        private string _mapMessage = "";
        private bool _mapError;
        private Vector2 _mapScroll;

        private void SetMapMessage(string message, bool error = false)
        {
            _mapMessage = message;
            _mapError = error;
        }

        /// <summary>
        /// The map: where the pet lives, what is still locked, and the way out to a mini game.
        ///
        /// This is the door panel grown up. It used to be a list of mini games and nothing else,
        /// because a door could only mean "leave". Now the door is also the way to move house,
        /// and the places are the thing the runner's coins are actually for — which is what
        /// gives the mini game a reason to exist beyond its own scoreboard.
        ///
        /// Everything is inside one scroll view. Three places plus a game is more than a phone
        /// in landscape can show at once, and a panel whose last row is laid out past its own
        /// area is a row that renders and cannot be tapped.
        /// </summary>
        private void DrawMapPanel(PetGameManager gm)
        {
            float w = Mathf.Min(680f, DesignWidth - 32f);
            float h = Mathf.Min(620f, DesignHeight - 32f);
            var rect = OverlayRect(w, h);
            ModalBackdrop(rect);

            var inner = new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width * 0.5f, 32f), "地图", _title);

            var balance = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(inner.x + inner.width * 0.4f, inner.y, inner.width * 0.6f - 84f, 32f),
                $"🐾 {DshMobile.PetWallet.Coins:N0}", balance);

            if (GUI.Button(new Rect(inner.xMax - 76f, inner.y + 2f, 76f, 30f), "关闭", _button))
            {
                gm.CloseDoorPrompt();
                return;
            }

            var here = RoomThemeInfo.Get(PetWorldMap.Current);
            DrawThemeSwatch(new Rect(inner.x, inner.y + 34f, 46f, 26f), here);
            GUI.Label(new Rect(inner.x + 50f, inner.y + 36f, inner.width - 50f, 22f),
                $"现在住在 {here.DisplayName}　·　{here.Effects()}", _small);

            var status = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft };
            GUI.color = _mapError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);
            GUI.Label(new Rect(inner.x, inner.y + 58f, inner.width, 22f), _mapMessage ?? "", status);
            GUI.color = Color.white;

            var body = new Rect(inner.x, inner.y + 84f, inner.width,
                Mathf.Max(60f, inner.yMax - inner.y - 84f));
            GUILayout.BeginArea(body);
            _mapScroll = GUILayout.BeginScrollView(_mapScroll);

            foreach (var info in RoomThemeInfo.All)
            {
                DrawPlaceRow(gm, info);
                GUILayout.Space(8f);
            }

            GUILayout.Space(4f);
            GUILayout.Label("出去走走（赚宠物币）", _label);

            var games = gm.AvailableMiniGames();
            if (games.Count == 0)
            {
                GUILayout.Label("暂时没有可以去的活动。", _small);
            }
            else
            {
                for (int i = 0; i < games.Count; i++)
                {
                    var game = games[i];
                    if (GUILayout.Button($"{game.Icon}  {game.DisplayName}", _button, GUILayout.Height(38f)))
                    {
                        gm.LaunchMiniGame(game.Id);
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.Label("　　" + game.Blurb, _small);
                    GUILayout.Space(6f);
                }
            }

            GUILayout.Space(4f);
            GUILayout.Label("在屋里玩（也赚宠物币）", _label);
            if (GUILayout.Button("🧩  拼图", _button, GUILayout.Height(38f)))
            {
                gm.CloseDoorPrompt();
                OpenPuzzle(gm);
                GUIUtility.ExitGUI();
            }
            GUILayout.Label("　　把这间屋子的画拼回去，步数越少宠物币越多。", _small);

            if (GUILayout.Button("🃏  记忆配对", _button, GUILayout.Height(38f)))
            {
                gm.CloseDoorPrompt();
                OpenMemoryMatch(gm);
                GUIUtility.ExitGUI();
            }
            GUILayout.Label("　　翻开两张一样的卡片就留下，全配完给宠物币。", _small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawModalEscape();
        }

        /// <summary>One place on the map: what it does to the pet, and how to get there.</summary>
        private void DrawPlaceRow(PetGameManager gm, RoomThemeInfo info)
        {
            bool unlocked = PetWorldMap.IsUnlocked(info.Theme);
            bool current = PetWorldMap.Current == info.Theme;

            GUILayout.BeginHorizontal();

            // A swatch of the place's own palette rather than an emoji: the emoji font renders
            // as nothing in IMGUI, and "which of these looks like somewhere I want to live" is
            // most of what a map is for. Three bands — floor, wall, rug.
            var swatch = GUILayoutUtility.GetRect(46f, 40f, GUILayout.Width(46f), GUILayout.Height(40f));
            DrawThemeSwatch(swatch, info);

            GUILayout.BeginVertical();
            GUILayout.Label(current ? $"{info.DisplayName}　·　现在在这里" : info.DisplayName, _label);
            GUILayout.Label(info.Blurb, _small);
            GUILayout.Label("效果：" + info.Effects(), _small);

            // The price gets its own line. Sharing one with the effects made the line long enough
            // to wrap on a phone, and the wrapped half landed underneath the button.
            if (!unlocked) GUILayout.Label($"{info.Price} 宠物币解锁", _small);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            float buttonWidth = Mobile ? MobileUi.Touchable(150f) : 150f;

            if (current)
            {
                GUI.enabled = false;
                GUILayout.Button("住在这里", _button, GUILayout.Width(buttonWidth), GUILayout.Height(40f));
                GUI.enabled = true;
            }
            else if (unlocked)
            {
                if (GUILayout.Button("前往", _button, GUILayout.Width(buttonWidth), GUILayout.Height(40f)))
                {
                    string message;
                    gm.TravelTo(info.Theme, out message);
                    SetMapMessage(message);
                    DshMobile.MobileHaptics.Light();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                bool afford = DshMobile.PetWallet.CanAfford(info.Price);
                GUI.enabled = afford;
                string label = afford ? "解锁并搬入"
                    : $"还差 {info.Price - DshMobile.PetWallet.Coins}";
                if (GUILayout.Button(label, _button, GUILayout.Width(buttonWidth), GUILayout.Height(40f)))
                {
                    string message;
                    if (PetWorldMap.TryUnlock(info.Theme, out message))
                    {
                        // Unlocking and then having to tap again to actually go there is a
                        // button that looks broken. Buy the place, move in.
                        string moved;
                        gm.TravelTo(info.Theme, out moved);
                        SetMapMessage(message + "，" + moved);
                    }
                    else
                    {
                        SetMapMessage(message, true);
                    }

                    DshMobile.MobileHaptics.Light();
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>Three bands of the place's palette: floor, walls, rug.</summary>
        private static void DrawThemeSwatch(Rect rect, RoomThemeInfo info)
        {
            var bands = new[] { info.Floor, info.Wall, info.Rug };
            var inner = new Rect(rect.x + 2f, rect.center.y - 11f, 40f, 22f);

            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);

            float width = (inner.width - 6f) / bands.Length;
            for (int i = 0; i < bands.Length; i++)
            {
                var cell = new Rect(inner.x + 3f + i * width, inner.y + 3f, width - 1f, inner.height - 6f);
                GUI.color = bands[i];
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
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
            // Tall enough for the labels, the fields, the toggles and the status lines. The
            // earlier 330px panel was shorter than its own content, so GUILayout — which does
            // not clip, but does stop a group from receiving input past its rect — pushed the
            // action buttons out of the panel and out of reach.
            //
            // Sized to the viewport rather than to a fixed number now: on a phone there is room
            // for the voice switches without scrolling, and on a short Game view OverlayRect
            // clamps it and the body scrolls, which is the behaviour that already existed.
            var rect = OverlayRect(520f, Mathf.Min(660f, DesignHeight - 40f));
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

            // The pet's own voice. One switch for the whole layer — the chirp it makes when
            // spoken to, when poked, and when it reacts on its own — because a setting that
            // leaves half the sounds on is worse than either extreme.
            GUILayout.Space(6f);
            bool voiceOn = GUILayout.Toggle(PetVoice.Enabled, " 宠物叫声", _small);
            if (voiceOn != PetVoice.Enabled)
            {
                PetVoice.Enabled = voiceOn;
                if (voiceOn) PetAudioDirector.Instance?.Speak(gm.Species, gm.Personality, gm.Needs.Mood);
            }
            GUILayout.Label("现在的音色：" + PetVoice.Describe(gm.Species, gm.Personality), _small);

            // Reading the pet's lines out loud. Kept separate from 宠物叫声 on purpose: the chirps
            // are part of the character, the speech is an accessibility-and-convenience switch,
            // and a player who wants a quiet room wants both off while one who wants to hear the
            // sentences may well want the chirps too.
            //
            // This block is deliberately chatty about what the engine is doing. The first version
            // shipped a toggle, a status line that said "准备中" forever on a real phone, and no
            // way to tell whether the problem was the phone, the engine or the setting.
            GUILayout.Space(6f);
            if (DshMobile.MobileTts.Available)
            {
                GUILayout.BeginHorizontal();
                bool speechOn = GUILayout.Toggle(DshMobile.MobileTts.Enabled, " 朗读宠物的话（语音输出）", _small);
                if (speechOn != DshMobile.MobileTts.Enabled)
                {
                    DshMobile.MobileTts.Enabled = speechOn;
                    // Warm the engine up on the switch, so the next reply is not swallowed by the
                    // several hundred milliseconds the platform takes to come up.
                    if (speechOn) DshMobile.MobileTts.WarmUp();
                    else DshMobile.MobileTts.Stop();
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▶ 测试朗读", _buttonSmall, GUILayout.Height(26f)))
                {
                    // The switch is a preference, the test is a diagnostic: it speaks even with
                    // the switch off, because "I hear nothing" has to be answerable from here.
                    float testPitch, testRate;
                    PetVoice.SpeechParams(gm.Species, gm.Personality, out testPitch, out testRate);
                    bool spoke = DshMobile.MobileTts.Test(testPitch, testRate);
                    SetVoiceMessage(spoke
                        ? "已经念了一句，听到了吗？听不到就把手机音量调大一点（用的是媒体音量）。"
                        : "没能念出来：" + DshMobile.MobileTts.LastError, spoke);
                }
                GUILayout.EndHorizontal();

                GUI.color = DshMobile.MobileTts.Ready
                    ? new Color(0.7f, 0.95f, 0.75f)
                    : new Color(1f, 0.85f, 0.6f);
                GUILayout.Label("语音引擎：" + DshMobile.MobileTts.StatusText(
                    DshMobile.MobileTts.Enabled, DshMobile.MobileTts.Ready, true,
                    DshMobile.MobileTts.InitStatus, DshMobile.MobileTts.LanguageStatus,
                    DshMobile.MobileTts.LastError), _small);
                GUI.color = Color.white;

                if (!string.IsNullOrEmpty(DshMobile.MobileTts.EngineName))
                {
                    GUILayout.Label("引擎：" + DshMobile.MobileTts.EngineName +
                                    (string.IsNullOrEmpty(DshMobile.MobileTts.TuningStatus)
                                        ? ""
                                        : "　·　" + DshMobile.MobileTts.TuningStatus), _small);
                }

                if (!string.IsNullOrEmpty(_voiceMessage))
                {
                    GUI.color = _voiceError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);
                    GUILayout.Label(_voiceMessage, _small);
                    GUI.color = Color.white;
                }

                // "Install a Chinese voice" is a scavenger hunt through the settings app, so the
                // one thing the player needs is a button that lands them on that screen.
                if (GUILayout.Button("打开系统语音设置（装中文语音）", _buttonSmall, GUILayout.Height(26f)))
                {
                    bool opened = DshMobile.MobileTts.OpenSystemSettings();
                    SetVoiceMessage(opened
                        ? "已经打开系统的「文字转语音」设置：装一个中文语音包，回来再按「▶ 测试朗读」。"
                        : "这台设备打不开系统语音设置。", !opened);
                }

                GUILayout.Label("只朗读说的话，括号里的动作不会念；引擎没起来时会每隔几秒重试一次。", _small);
            }
            else
            {
                GUILayout.Label("这台设备没有系统语音（语音输出只在安卓上可用）。", _small);
            }

            GUILayout.Label("语音输入（对着麦克风说）在手机上有麦克风按钮：按一下说话，识别到的字会填进输入框。", _small);

            if (DshMobile.MobileStt.AvailableNow)
            {
                bool sttOn = GUILayout.Toggle(DshMobile.MobileStt.Enabled, " 显示麦克风按钮", _small);
                if (sttOn != DshMobile.MobileStt.Enabled)
                {
                    DshMobile.MobileStt.Enabled = sttOn;
                    if (!sttOn) DshMobile.MobileStt.Cancel();
                }

                GUI.color = DshMobile.MobileStt.HasPermission
                    ? new Color(0.7f, 0.95f, 0.75f)
                    : new Color(1f, 0.85f, 0.6f);
                GUILayout.Label("语音输入：" + DshMobile.MobileStt.StatusText(
                    DshMobile.MobileStt.Enabled, true, DshMobile.MobileStt.HasPermission,
                    DshMobile.MobileStt.Listening, DshMobile.MobileStt.LastErrorCode,
                    DshMobile.MobileStt.LastError), _small);
                GUI.color = Color.white;

                if (!DshMobile.MobileStt.HasPermission &&
                    GUILayout.Button("申请麦克风权限", _buttonSmall, GUILayout.Height(26f)))
                {
                    DshMobile.MobileStt.RequestPermission();
                }
            }

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
            _testResult = "";

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
            _showCollection = false;
            _showPuzzle = false;
            _showMemory = false;
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

            // --- storage row ---
            // What the notebook costs, and the two ways to shrink it. Deleting has to be
            // reachable from the panel that shows the memories: "free up space" is a thought
            // you have while looking at the thing taking up the space.
            DrawJournalStorageRow(gm, new Rect(rect.x + 18f, rect.y + 42f, rect.width - 36f, 26f));

            // --- month grid ---
            // The cell height is derived from the room actually available, so a six-row month
            // still leaves space for the day's entries on a short Game view. Fixed 46px cells
            // used to push the detail list off the bottom of the panel.
            float gridX = rect.x + 18f;
            float gridY = rect.y + 78f;
            float cellW = (w - 36f) / 7f;

            const float detailReserve = 116f;   // the "N 条" line plus a usable list
            float cellH = Mathf.Clamp((rect.height - 78f - 20f - detailReserve) / 6f, 24f, 46f);

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

            // "Delete this day" sits with the day it deletes, and the confirmation is a second
            // press on the same button rather than a modal on top of a modal.
            if (entries.Count > 0)
            {
                float buttonWidth = Mobile ? MobileUi.Touchable(120f) : 108f;
                var deleteDay = new Rect(gridX + w - 36f - buttonWidth, detailY - 2f, buttonWidth, 26f);
                bool armed = _journalConfirmDay == _selectedDay.Date;
                if (GUI.Button(deleteDay, armed ? $"真的删掉 {entries.Count} 条？" : "删除这一天",
                        _buttonSmall))
                {
                    if (armed)
                    {
                        int removed = journal.DeleteDay(_selectedDay);
                        journal.Save();
                        _journalConfirmDay = DateTime.MinValue;
                        _journalStatus = $"已删除 {removed} 条（{PetJournal.FormatBytes(journal.StorageBytes)}）";
                    }
                    else
                    {
                        _journalConfirmDay = _selectedDay.Date;
                        _journalStatus = "再点一次确认删除这一天的记录";
                    }
                }
            }

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

                    // The delete button is pinned to the right of the row and sized for a
                    // thumb on a phone: the whole point of this panel is being able to get rid
                    // of one memory without clearing the day.
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("✕", _buttonSmall, GUILayout.Width(34f), GUILayout.Height(24f)))
                    {
                        journal.Delete(entry);
                        journal.Save();
                        _journalStatus = $"已删除 1 条（{PetJournal.FormatBytes(journal.StorageBytes)}）";
                        GUIUtility.ExitGUI();   // the list we are iterating just changed
                    }

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

        /// <summary>
        /// The notebook's storage line and its two bulk deletions.
        ///
        /// Freeing space is a thought you have while looking at the thing that is taking up the
        /// space, so the controls live in the notebook rather than in a settings pane nobody
        /// visits. Both bulk actions confirm on a second press — there is no undo, and a
        /// mis-tap that erases months of the pet's history would be unrecoverable.
        /// </summary>
        private void DrawJournalStorageRow(PetGameManager gm, Rect rect)
        {
            var journal = gm.Journal;

            var label = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height),
                $"{journal.Count} 条记忆（{journal.PinnedCount} 条钉住）　占用 {PetJournal.FormatBytes(journal.StorageBytes)}",
                label);

            float right = rect.xMax;
            float buttonWidth = Mobile ? MobileUi.Touchable(104f) : 96f;
            float gap = 8f;

            bool armedAll = _journalConfirmAll;
            var clearAll = new Rect(right - buttonWidth, rect.y, buttonWidth, rect.height);
            if (GUI.Button(clearAll, armedAll ? "真的全部清空？" : "清空全部", _buttonSmall))
            {
                if (armedAll)
                {
                    journal.Clear();
                    journal.Save();
                    _journalConfirmAll = false;
                    _journalStatus = "记事本已清空";
                }
                else
                {
                    _journalConfirmAll = true;
                    _journalConfirmMonth = false;
                    _journalStatus = "再点一次确认清空全部记忆";
                }
            }

            right -= buttonWidth + gap;
            bool armedMonth = _journalConfirmMonth;
            var clearMonth = new Rect(right - buttonWidth, rect.y, buttonWidth, rect.height);
            if (GUI.Button(clearMonth, armedMonth ? "确认清理本月？" : "清理本月", _buttonSmall))
            {
                if (armedMonth)
                {
                    int removed = journal.DeleteMonth(_journalMonth.Year, _journalMonth.Month);
                    journal.Save();
                    _journalConfirmMonth = false;
                    _journalStatus = $"已清理 {removed} 条（{PetJournal.FormatBytes(journal.StorageBytes)}）";
                }
                else
                {
                    _journalConfirmMonth = true;
                    _journalConfirmAll = false;
                    _journalStatus = $"再点一次确认清理 {_journalMonth:yyyy 年 M 月} 的记录";
                }
            }

            if (!string.IsNullOrEmpty(_journalStatus))
            {
                var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
                GUI.color = new Color(1f, 0.9f, 0.6f);
                GUI.Label(new Rect(rect.x, rect.yMax + 4f, rect.width, 20f), _journalStatus, style);
                GUI.color = Color.white;
            }
        }

        private DateTime _journalConfirmDay = DateTime.MinValue;
        private bool _journalConfirmMonth;
        private bool _journalConfirmAll;
        private string _journalStatus = "";

        // ---------------------------------------------------------------- collection

        /// <summary>
        /// Shop, warehouse and backpack in one panel.
        ///
        /// One screen rather than three because the actions run into each other: you buy a pet,
        /// it lands in the warehouse, you put it in the backpack, and it appears in the room.
        /// The coin balance sits in the header because every decision here is about coins.
        /// </summary>
        private void DrawCollection(PetGameManager gm)
        {
            float w = Mathf.Min(760f, DesignWidth - 48f);
            float h = Mathf.Min(560f, DesignHeight - 48f);
            var rect = OverlayRect(w, h);
            ModalBackdrop(rect);

            var inner = new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width * 0.5f, 32f), "宠物图鉴", _title);

            var balance = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(inner.x + inner.width * 0.5f, inner.y, inner.width * 0.5f, 32f),
                $"🐾 {DshMobile.PetWallet.Coins:N0}", balance);

            var tabs = new[] { CollectionTab.Shop, CollectionTab.Warehouse, CollectionTab.Backpack };
            float tabWidth = Mathf.Min(140f, inner.width / 4f);
            var tabRect = new Rect(inner.x, inner.y + 38f, inner.width, 34f);
            for (int i = 0; i < tabs.Length; i++)
            {
                var cell = new Rect(tabRect.x + i * (tabWidth + 6f), tabRect.y, tabWidth, tabRect.height);
                GUI.color = PetCollectionPanel.Tab == tabs[i] ? new Color(1f, 0.92f, 0.7f) : Color.white;
                if (GUI.Button(cell, PetCollectionPanel.TabLabel(tabs[i]), _button))
                {
                    PetCollectionPanel.Tab = tabs[i];
                }
                GUI.color = Color.white;
            }

            if (GUI.Button(new Rect(inner.xMax - 76f, tabRect.y, 76f, tabRect.height), "关闭", _button))
            {
                _showCollection = false;
                return;
            }

            if (!string.IsNullOrEmpty(_collectionMessage))
            {
                var style = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft };
                GUI.color = _collectionError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);
                GUI.Label(new Rect(inner.x, tabRect.yMax + 4f, inner.width, 22f), _collectionMessage, style);
                GUI.color = Color.white;
            }

            var body = new Rect(inner.x, tabRect.yMax + 30f, inner.width, inner.yMax - tabRect.yMax - 30f);
            switch (PetCollectionPanel.Tab)
            {
                case CollectionTab.Shop: DrawShopTab(body); break;
                case CollectionTab.Warehouse: DrawPetsTab(body, inBackpack: false); break;
                default: DrawPetsTab(body, inBackpack: true); break;
            }

            DrawModalEscape();
        }

        private string _collectionMessage = "";
        private bool _collectionError;
        private Vector2 _collectionScroll;

        /// <summary>Which pet's sell button is armed, so the second tap really sells it.</summary>
        private string _armedSell;

        private string _voiceMessage = "";
        private bool _voiceError;

        private void SetVoiceMessage(string message, bool error = false)
        {
            _voiceMessage = message;
            _voiceError = error;
        }

        private void SetCollectionMessage(string message, bool error = false)
        {
            _collectionMessage = message;
            _collectionError = error;
        }

        private void DrawShopTab(Rect body)
        {
            var hint = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(body.x, body.y, body.width, 22f),
                "用宠物币带新伙伴回家（跑酷赚币）。同一物种可以再买一只，性格会重新生成。", hint);

            var list = new Rect(body.x, body.y + 26f, body.width, body.height - 26f);
            GUILayout.BeginArea(list);
            _collectionScroll = GUILayout.BeginScrollView(_collectionScroll);

            foreach (var species in PetCollectionPanel.ShopOrder())
            {
                bool owned = PetCollection.IsSpeciesUnlocked(species.Id);
                int price = PetCollection.PriceFor(species.Id);

                GUILayout.BeginHorizontal();
                GUI.color = species.Fur;
                GUILayout.Label("●", _title, GUILayout.Width(26f));
                GUI.color = Color.white;

                GUILayout.BeginVertical();
                GUILayout.Label($"{species.DisplayName}　{species.Blurb}", _label);
                GUILayout.Label(owned
                    ? $"已拥有 {PetCollection.OwnedCount(species.Id)} 只　再买一只 {price} 币"
                    : $"未解锁　{price} 币", _small);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                bool afford = DshMobile.PetWallet.CanAfford(price);
                GUI.enabled = afford;
                float buttonWidth = Mobile ? MobileUi.Touchable(150f) : 150f;
                if (GUILayout.Button(afford ? $"领回家 {price}" : $"还差 {price - DshMobile.PetWallet.Coins}",
                        _button, GUILayout.Width(buttonWidth), GUILayout.Height(38f)))
                {
                    var result = PetCollectionPanel.Buy(species.Id);
                    SetCollectionMessage(result.Message, result.Error);
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPetsTab(Rect body, bool inBackpack)
        {
            var hint = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft };

            if (inBackpack)
            {
                GUI.Label(new Rect(body.x, body.y, body.width, 22f),
                    $"背包 {PetCollection.Backpack.Count}/{PetCollection.BackpackSlots}　" +
                    "在背包里的宠物会一起在房间里走动，点它可以摸它。", hint);
                GUI.color = new Color(1f, 0.88f, 0.6f);
                GUI.Label(new Rect(body.x, body.y + 22f, body.width, 22f),
                    PetCollectionPanel.TokenAdvice(PetCollection.Backpack.Count), hint);
                GUI.color = Color.white;
            }
            else
            {
                GUI.Label(new Rect(body.x, body.y, body.width, 22f),
                    $"仓库里一共有 {PetCollection.Warehouse.Count} 只宠物，容量不限。", hint);
            }

            var list = new Rect(body.x, body.y + 50f, body.width, body.height - 50f);
            GUILayout.BeginArea(list);
            _collectionScroll = GUILayout.BeginScrollView(_collectionScroll);

            var pets = inBackpack
                ? RecordsIn(PetCollection.Backpack)
                : new List<PetRecord>(PetCollection.Warehouse);

            if (pets.Count == 0) GUILayout.Label("这里还没有宠物，去商城领一只吧。", _small);

            foreach (var record in pets)
            {
                var species = PetSpecies.Get(record.SpeciesId);
                bool isPrimary = PetCollection.Primary != null && PetCollection.Primary.Id == record.Id;
                bool inBag = PetCollection.IsInBackpack(record.Id);

                GUILayout.BeginHorizontal();
                GUI.color = species.Fur;
                GUILayout.Label("●", _title, GUILayout.Width(26f));
                GUI.color = Color.white;

                GUILayout.BeginVertical();
                GUILayout.Label($"{record.Name}　{species.DisplayName}", _label);
                GUILayout.Label($"{record.Personality.Archetype}　·　" +
                                PetCollectionPanel.RoleLabel(inBag, isPrimary), _small);
                GUILayout.Label("音色：" + PetVoice.Describe(species, record.Personality), _small);

                // Where a pet came from is worth a line: one that was born in the room reads
                // differently from one that came out of the shop.
                if (!string.IsNullOrEmpty(record.Parents))
                {
                    GUI.color = new Color(0.82f, 0.9f, 1f);
                    GUILayout.Label("在屋里出生：" + record.Parents, _small);
                    GUI.color = Color.white;
                }
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // Selling is two taps, like the journal's bulk deletes: there is no undo, and a
                // pet is not a thing to lose to a mis-tap.
                string sellId = "sell:" + record.Id;
                bool armed = _armedSell == sellId;
                float sellWidth = Mobile ? MobileUi.Touchable(96f) : 96f;

                GUI.enabled = !isPrimary;
                if (GUILayout.Button(armed ? "真的卖掉？" : "卖掉", _buttonSmall,
                        GUILayout.Width(sellWidth), GUILayout.Height(30f)))
                {
                    if (armed)
                    {
                        _armedSell = null;
                        var result = PetCollectionPanel.Sell(record.Id);
                        SetCollectionMessage(result.Message, result.Error);
                        GUIUtility.ExitGUI();
                    }
                    else
                    {
                        _armedSell = sellId;
                        SetCollectionMessage(
                            $"再点一次就把{record.Name}卖掉，能得到 " +
                            $"{PetCollection.SellPriceFor(record.SpeciesId)} 个宠物币（没法撤销）");
                    }
                }
                GUI.enabled = true;
                float buttonWidth = Mobile ? MobileUi.Touchable(112f) : 112f;

                if (inBag)
                {
                    if (!isPrimary && GUILayout.Button("主要照顾", _buttonSmall,
                            GUILayout.Width(buttonWidth), GUILayout.Height(30f)))
                    {
                        var result = PetCollectionPanel.MakePrimary(record.Id);
                        SetCollectionMessage(result.Message, result.Error);
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("放回仓库", _buttonSmall,
                            GUILayout.Width(buttonWidth), GUILayout.Height(30f)))
                    {
                        var result = PetCollectionPanel.TakeOutOfBackpack(record.Id);
                        SetCollectionMessage(result.Message, result.Error);
                        GUIUtility.ExitGUI();
                    }
                }
                else if (GUILayout.Button("放进背包", _buttonSmall,
                             GUILayout.Width(buttonWidth), GUILayout.Height(30f)))
                {
                    var result = PetCollectionPanel.PutInBackpack(record.Id);
                    SetCollectionMessage(result.Message, result.Error);
                    GUIUtility.ExitGUI();
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static List<PetRecord> RecordsIn(IReadOnlyList<string> ids)
        {
            var list = new List<PetRecord>();
            for (int i = 0; i < ids.Count; i++)
            {
                var record = PetCollection.Find(ids[i]);
                if (record != null) list.Add(record);
            }
            return list;
        }
    }
}
