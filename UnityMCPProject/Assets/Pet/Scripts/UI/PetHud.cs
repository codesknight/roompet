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
        private bool _showFurnish;
        private bool _placeMode;
        private int _furnishTab;
        private Vector2 _furnishScroll;
        private string _furnishMessage = "";
        private string _toast = "";
        private float _toastUntil;
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

        // uGUI overlays (toast + cursor hint), the first slice of PetHud migrated off IMGUI.
        private RectTransform _root;
        private UnityEngine.UI.Image _toastPanel;
        private UnityEngine.UI.Text _toastText;
        private UnityEngine.UI.Text _cursorHintText;
        private bool _overlaysBuilt;

        // uGUI chat (second slice).
        private UnityEngine.UI.Image _chatPanel;
        private UnityEngine.UI.Image _chatAvatar;
        private UnityEngine.UI.Text _chatName;
        private UnityEngine.UI.Text _chatMood;
        private UnityEngine.UI.Text _chatThinking;
        private UnityEngine.UI.ScrollRect _chatScroll;
        private RectTransform _chatContent;
        private UnityEngine.UI.Button _closeButton;
        private UnityEngine.UI.InputField _chatInput;
        private UnityEngine.UI.Button _chatSend;
        private UnityEngine.UI.Text _chatSendLabel;
        private UnityEngine.UI.Button _micButton;
        private UnityEngine.UI.Text _micLabel;
        private UnityEngine.UI.Button _ttsButton;
        private UnityEngine.UI.Text _ttsLabel;
        private UnityEngine.UI.Button _soundButton;
        private UnityEngine.UI.Text _soundLabel;
        private UnityEngine.UI.Text _voiceStatus;
        private readonly List<GameObject> _bubbles = new List<GameObject>();
        private int _builtMessageCount = -1;

        // uGUI pet card (third slice).
        private UnityEngine.UI.Image _cardPanel;
        private readonly List<UnityEngine.UI.Button> _chipButtons = new List<UnityEngine.UI.Button>();
        private readonly List<UnityEngine.UI.Text> _chipLabels = new List<UnityEngine.UI.Text>();
        private UnityEngine.UI.InputField _renameField;
        private UnityEngine.UI.Text _headerSummary;
        private UnityEngine.UI.Text _detailText;
        private UnityEngine.UI.ScrollRect _detailScroll;
        private readonly List<RectTransform> _needFills = new List<RectTransform>();
        private readonly List<UnityEngine.UI.Image> _needFillImages = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.Text> _needLabels = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Button> _footerButtons = new List<UnityEngine.UI.Button>();
        private int _builtPetIndex = -1;
        private bool _builtCardExpanded;

        // uGUI placement bar (fourth slice, first piece).
        private UnityEngine.UI.Image _placementBar;
        private UnityEngine.UI.Text _placementText;

        // uGUI map panel (fourth slice, second piece).
        private UnityEngine.UI.Image _modalScrim;
        private UnityEngine.UI.Image _mapPanel;
        private UnityEngine.UI.Text _mapBalance;
        private UnityEngine.UI.Text _mapHere;
        private UnityEngine.UI.Text _mapMessageText;
        private RectTransform _mapContent;
        private bool _mapBuilt;

        // uGUI settings panel (fourth slice, third piece).
        private UnityEngine.UI.Image _settingsPanel;
        private RectTransform _settingsContent;
        private UnityEngine.UI.InputField _baseUrlField;
        private UnityEngine.UI.InputField _modelField;
        private UnityEngine.UI.InputField _keyField;
        private UnityEngine.UI.Text _settingsStatus;
        private readonly List<UnityEngine.UI.Text> _toggleLabels = new List<UnityEngine.UI.Text>();
        private readonly List<string> _toggleTexts = new List<string>();
        private readonly List<System.Func<bool>> _toggleGetters = new List<System.Func<bool>>();
        private readonly List<System.Action<bool>> _toggleSetters = new List<System.Action<bool>>();
        private UnityEngine.UI.Slider _musicSlider;
        private UnityEngine.UI.Text _musicVolumeLabel;

        // uGUI journal (calendar) panel (fourth slice, fourth piece).
        private UnityEngine.UI.Image _journalPanel;
        private UnityEngine.UI.Text _journalMonthLabel;
        private UnityEngine.UI.Text _journalStorageLabel;
        private UnityEngine.UI.Text _journalStatusText;
        private RectTransform _journalGrid;
        private RectTransform _journalDayContent;
        private UnityEngine.UI.Text _journalDayLabel;
        private UnityEngine.UI.Button _deleteDayButton;
        private UnityEngine.UI.Text _deleteDayLabel;
        private DateTime _builtJournalMonth;
        private DateTime _builtJournalDay;

        // uGUI collection panel (fourth slice, fifth piece).
        private UnityEngine.UI.Image _collectionPanel;
        private UnityEngine.UI.Text _collectionBalance;
        private UnityEngine.UI.Text _collectionMessageText;
        private RectTransform _collectionContent;
        private readonly List<UnityEngine.UI.Text> _collectionTabLabels = new List<UnityEngine.UI.Text>();
        private int _builtCollectionTab = -1;

        // uGUI furnish panel (fourth slice, sixth piece).
        private UnityEngine.UI.Image _furnishPanel;
        private UnityEngine.UI.Text _furnishInfo;
        private UnityEngine.UI.Text _furnishMsg;
        private RectTransform _furnishContent;
        private readonly List<UnityEngine.UI.Text> _furnishTabLabels = new List<UnityEngine.UI.Text>();
        private int _builtFurnishTab = -1;

        // uGUI prompt preview panel (fourth slice, seventh piece).
        private UnityEngine.UI.Image _promptPanel;
        private UnityEngine.UI.Text _promptText;
        private UnityEngine.UI.InputField _promptEdit;
        private UnityEngine.UI.Text _promptSaveLabel;

        // uGUI memory match panel (fourth slice, eighth piece).
        private UnityEngine.UI.Image _memoryPanel;
        private UnityEngine.UI.Text _memoryStats;
        private UnityEngine.UI.Text _memoryMsg;
        private RectTransform _memoryBoard;
        private readonly List<UnityEngine.UI.Button> _memoryCards = new List<UnityEngine.UI.Button>();
        private readonly List<UnityEngine.UI.Image> _memoryCardBgs = new List<UnityEngine.UI.Image>();
        private readonly List<UnityEngine.UI.RawImage> _memoryCardFaces = new List<UnityEngine.UI.RawImage>();
        private readonly List<UnityEngine.UI.Text> _memoryLevelLabels = new List<UnityEngine.UI.Text>();
        private UnityEngine.UI.Text _memoryFooter;
        private int _builtMemoryBoardCount = -1;

        // uGUI puzzle panel (fourth slice, ninth piece).
        private UnityEngine.UI.Image _puzzlePanel;
        private UnityEngine.UI.Text _puzzleStatus;
        private UnityEngine.UI.Text _puzzleMsg;
        private RectTransform _puzzleBoard;
        private readonly List<UnityEngine.UI.RawImage> _puzzleTiles = new List<UnityEngine.UI.RawImage>();
        private readonly List<UnityEngine.UI.Text> _puzzleNumbers = new List<UnityEngine.UI.Text>();
        private readonly List<UnityEngine.UI.Button> _puzzleButtons = new List<UnityEngine.UI.Button>();
        private UnityEngine.UI.RawImage _puzzlePreview;
        private UnityEngine.UI.Text _puzzleFooter;
        private UnityEngine.UI.Text _puzzleToggleLabel;

        /// <summary>Set by hoverable world objects; shown near the cursor.</summary>
        public static void SetCursorHint(string hint) => _cursorHint = hint;

        /// <summary>
        /// A short message over the top of the room, for the in-world interactions that are not a
        /// panel: "摘了 3 个苹果", "钓上了一条鱼", "需要装备铲子". Fades after a few seconds.
        /// </summary>
        public static void SetToast(string message)
        {
            var hud = _instance;
            if (hud == null) return;
            hud._toast = message;
            hud._toastUntil = Time.realtimeSinceStartup + 3.2f;
        }

        /// <summary>
        /// Opens the furniture panel straight onto the warehouse tab. Used by the cabin's
        /// warehouse cabinet — clicking a piece of furniture that IS the warehouse should take
        /// you to the warehouse, not make the pet walk over to it.
        /// </summary>
        public static void OpenFurnishWarehouse()
        {
            var hud = _instance;
            if (hud == null) return;
            hud._showFurnish = true;
            hud._furnishTab = 2;
            hud._furnishMessage = "";
        }

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

                // The stick's claim area is the SAME rect the hint is drawn in, and that rect stops
                // above the collapsed chat bar. It used to be a second, larger rectangle (the left
                // 46% of the whole lower 60% of the screen) which swallowed the bottom strip — so a
                // tap on 「和它说说话」 started the joystick instead of opening the chat, and the
                // button read as dead. Two definitions of one area is one definition too many.
                MobileTouch.StickZone = ToScreen(
                    ComputeMobileControls(layout).StickZone);
            }

            // While a modal panel is up, the panels behind it must not react. GUI.enabled is
            // how IMGUI is told a control is inert, and it is order-independent — unlike the
            // full-rect invisible Button this used to draw, which claimed the click itself and
            // left the modal's own text fields unable to take focus.
            bool modal = ModalOpen;
            if (modal) GUI.enabled = false;
            // The pet card is now uGUI — see SyncPetCard.
            if (Mobile) DrawMobileChat(gm, layout);
            // The expanded chat (and the whole desktop chat) is now uGUI — see SyncChat.
            DrawViewSwitcher(gm, layout);
            if (!Mobile) DrawThrowMeter(gm, layout);
            else ShowMobileAimGuide(gm);
            DrawOverlays(gm, layout);
            GUI.enabled = true;

            // Controls sit above the room but below any modal panel.
            if (Mobile) DrawMobileControls(gm, layout);

            // The map panel (door prompt) is now uGUI — see SyncMapPanel.
            // The puzzle panel is now uGUI — see SyncPuzzlePanel.
            // The memory match panel is now uGUI — see SyncMemoryPanel.
            // The settings panel is now uGUI — see SyncSettingsPanel.
            // The prompt preview is now uGUI — see SyncPromptPanel.
            // The journal (calendar) is now uGUI — see SyncJournalPanel.
            // The collection panel is now uGUI — see SyncCollectionPanel.
            // The furnish panel is now uGUI — see SyncFurnishPanel.
            // The placement bar is now uGUI — see SyncPlacementBar.

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

        private void Awake() { if (_instance == null) _instance = this; BuildOverlays(); }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>
        /// The uGUI toast and cursor hint, the first slice of this HUD migrated off IMGUI. The
        /// canvas survives scene loads, so the container is destroyed in OnDestroy like every other
        /// HUD. The rest of the HUD is still IMGUI and draws over these overlays until the whole
        /// file moves, so the toast is positioned to clear the IMGUI top panels.
        /// </summary>
        private void BuildOverlays()
        {
            if (_overlaysBuilt) return;
            _overlaysBuilt = true;

            _root = DshMobile.Ugui.Root("PetHudOverlays");

            _toastPanel = DshMobile.Ugui.Panel("Toast", _root, 12f,
                new Color(0.08f, 0.09f, 0.13f, 0.92f), new Color(1f, 0.85f, 0.4f, 0.5f), 1.4f);
            DshMobile.Ugui.Place(_toastPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(420f, 40f));
            _toastText = DshMobile.Ugui.Text("ToastText", _toastPanel.rectTransform, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleCenter);
            DshMobile.Ugui.Stretch(_toastText.rectTransform);
            _toastPanel.gameObject.SetActive(false);

            _cursorHintText = DshMobile.Ugui.Text("CursorHint", _root, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.Place(_cursorHintText.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), Vector2.zero, new Vector2(320f, 24f));
            _cursorHintText.gameObject.SetActive(false);

            BuildChat();
            BuildPetCard();
            BuildPlacementBar();
            BuildMapPanel();
            BuildSettingsPanel();
            BuildJournalPanel();
            BuildCollectionPanel();
            BuildFurnishPanel();
            BuildPromptPanel();
            BuildMemoryPanel();
            BuildPuzzlePanel();
        }

        private void Update()
        {
            if (!_overlaysBuilt) return;

            // Toast: show while fresh, then clear.
            if (!string.IsNullOrEmpty(_toast) && Time.realtimeSinceStartup > _toastUntil) _toast = "";
            _toastPanel.gameObject.SetActive(!string.IsNullOrEmpty(_toast));
            if (_toastPanel.gameObject.activeSelf) _toastText.text = _toast;

            // Cursor hint follows the pointer.
            _cursorHintText.gameObject.SetActive(!string.IsNullOrEmpty(_cursorHint));
            if (_cursorHintText.gameObject.activeSelf)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, Input.mousePosition, null, out local))
                {
                    _cursorHintText.rectTransform.anchoredPosition = local + new Vector2(16f, -8f);
                }
                _cursorHintText.text = _cursorHint;
            }

            SyncChat();
            SyncPetCard();
            SyncPlacementBar();
            SyncMapPanel();
            SyncSettingsPanel();
            SyncJournalPanel();
            SyncCollectionPanel();
            SyncFurnishPanel();
            SyncPromptPanel();
            SyncMemoryPanel();
            SyncPuzzlePanel();
        }

        /// <summary>
        /// Positions a uGUI rect from a PetHud design-space <see cref="Rect"/>, as fractions of the
        /// viewport. This is how the chat panel — laid out by <see cref="ComputeLayout"/> in the
        /// HUD's dynamic design pixels — lands on the shared canvas, whose reference resolution is
        /// fixed: anchors are fractions, so the mapping is resolution-independent.
        /// </summary>
        private void ApplyDesignRect(RectTransform rt, Rect design)
        {
            float w = DesignWidth > 0f ? DesignWidth : 1f;
            float h = DesignHeight > 0f ? DesignHeight : 1f;
            rt.anchorMin = new Vector2(design.xMin / w, 1f - design.yMax / h);
            rt.anchorMax = new Vector2(design.xMax / w, 1f - design.yMin / h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void BuildChat()
        {
            _chatPanel = DshMobile.Ugui.Panel("ChatPanel", _root, 16f,
                new Color(0.10f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.10f), 2f);
            _chatPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _chatPanel.gameObject.SetActive(false);
            var p = _chatPanel.rectTransform;

            // Header: avatar disc, name, mood, thinking indicator.
            _chatAvatar = DshMobile.Ugui.Image("Avatar", p, Color.white);
            _chatAvatar.sprite = DshMobile.UguiRounded.Circle(18f, Color.white);
            _chatName = DshMobile.Ugui.Text("Name", p, "", 20, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            _chatMood = DshMobile.Ugui.Text("Mood", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            _chatThinking = DshMobile.Ugui.Text("Thinking", p, "正在想…", 15, new Color(1f, 0.85f, 0.4f), UnityEngine.TextAnchor.MiddleRight);

            _closeButton = DshMobile.Ugui.Button("Close", p, "收起", 16, new Color(0.75f, 0.35f, 0.35f));
            _closeButton.onClick.AddListener(OnCloseChat);

            // Transcript scroll.
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            _chatScroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            _chatScroll.viewport = viewport.GetComponent<RectTransform>();
            _chatScroll.horizontal = false;
            _chatScroll.vertical = true;
            _chatScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            _chatScroll.scrollSensitivity = 40f;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _chatContent = content.GetComponent<RectTransform>();
            _chatContent.anchorMin = new Vector2(0f, 1f);
            _chatContent.anchorMax = new Vector2(1f, 1f);
            _chatContent.pivot = new Vector2(0.5f, 1f);
            _chatScroll.content = _chatContent;

            // Footer: input + send + mic + TTS + sound (all in one row for desktop; the mobile
            // footer is a taller version of the same row, positioned by SyncChat).
            _chatInput = DshMobile.Ugui.InputField("Input", p);
            _chatSend = DshMobile.Ugui.Button("Send", p, "发送", 17, new Color(0.30f, 0.55f, 0.35f));
            _chatSendLabel = _chatSend.GetComponentInChildren<UnityEngine.UI.Text>();
            _chatSend.onClick.AddListener(OnSendChat);

            _micButton = DshMobile.Ugui.Button("Mic", p, "说", 16, new Color(0.34f, 0.60f, 0.86f));
            _micLabel = _micButton.GetComponentInChildren<UnityEngine.UI.Text>();
            _micButton.onClick.AddListener(OnMic);

            _ttsButton = DshMobile.Ugui.Button("Tts", p, "朗读：开", 14, new Color(0.30f, 0.40f, 0.58f));
            _ttsLabel = _ttsButton.GetComponentInChildren<UnityEngine.UI.Text>();
            _ttsButton.onClick.AddListener(OnTts);

            _soundButton = DshMobile.Ugui.Button("Sound", p, "音效：开", 14, new Color(0.30f, 0.40f, 0.58f));
            _soundLabel = _soundButton.GetComponentInChildren<UnityEngine.UI.Text>();
            _soundButton.onClick.AddListener(OnSound);

            _voiceStatus = DshMobile.Ugui.Text("VoiceStatus", p, "", 14,
                new Color(0.72f, 0.82f, 0.95f), UnityEngine.TextAnchor.MiddleLeft);
        }

        private void SyncChat()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;

            var layout = ComputeLayout(DesignWidth, DesignHeight, PetSpecies.Count, TranscriptVisible);
            bool show = !Mobile || _chatExpanded;

            // The collapsed mobile bar stays IMGUI for now; the expanded panel is uGUI.
            _chatPanel.gameObject.SetActive(show);

            if (!show)
            {
                IsTextInputFocused = false;
                return;
            }

            var rect = layout.Chat;
            if (Mobile) rect = new Rect(rect.x, rect.yMax - Mathf.Max(rect.height, 200f), rect.width, rect.height);
            ApplyDesignRect(_chatPanel.rectTransform, rect);

            // Header.
            _chatAvatar.color = gm.Species != null ? gm.Species.Fur : Color.white;
            _chatAvatar.gameObject.SetActive(true);
            _chatAvatar.rectTransform.anchorMin = _chatAvatar.rectTransform.anchorMax = new Vector2(0f, 1f);
            _chatAvatar.rectTransform.pivot = new Vector2(0f, 1f);
            _chatAvatar.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            _chatAvatar.rectTransform.sizeDelta = new Vector2(36f, 36f);

            _chatName.text = gm.PetName;
            _chatName.rectTransform.anchorMin = _chatName.rectTransform.anchorMax = new Vector2(0f, 1f);
            _chatName.rectTransform.pivot = new Vector2(0f, 1f);
            _chatName.rectTransform.anchoredPosition = new Vector2(58f, -12f);
            _chatName.rectTransform.sizeDelta = new Vector2(rect.width * 0.5f, 24f);

            _chatMood.text = PetUtil.MoodLabel(gm.Needs.Mood) + "　·　" + (gm.Personality != null ? gm.Personality.Archetype : "");
            _chatMood.rectTransform.anchorMin = _chatMood.rectTransform.anchorMax = new Vector2(0f, 1f);
            _chatMood.rectTransform.pivot = new Vector2(0f, 1f);
            _chatMood.rectTransform.anchoredPosition = new Vector2(58f, -36f);
            _chatMood.rectTransform.sizeDelta = new Vector2(rect.width * 0.6f, 18f);

            _chatThinking.gameObject.SetActive(gm.IsThinking);
            _chatThinking.rectTransform.anchorMin = _chatThinking.rectTransform.anchorMax = new Vector2(1f, 1f);
            _chatThinking.rectTransform.pivot = new Vector2(1f, 1f);
            _chatThinking.rectTransform.anchoredPosition = new Vector2(-12f, -12f);
            _chatThinking.rectTransform.sizeDelta = new Vector2(90f, 22f);

            _closeButton.gameObject.SetActive(Mobile);
            _closeButton.GetComponent<RectTransform>().anchorMin = _closeButton.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            _closeButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            _closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-12f, -12f);
            _closeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(88f, 34f);

            // Transcript.
            float footerH = Mobile ? 150f : 100f;
            var viewportRt = _chatScroll.viewport;
            viewportRt.anchorMin = new Vector2(0f, 0f);
            viewportRt.anchorMax = new Vector2(1f, 1f);
            viewportRt.offsetMin = new Vector2(8f, footerH);
            viewportRt.offsetMax = new Vector2(-8f, -56f);

            RebuildTranscript(gm, rect.width - 16f);

            // Footer.
            float fy = footerH - 8f;
            _chatInput.GetComponent<RectTransform>().anchorMin = _chatInput.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
            _chatInput.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
            _chatInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, fy - 40f);
            _chatInput.GetComponent<RectTransform>().sizeDelta = new Vector2(rect.width - 200f, 38f);

            _chatSend.GetComponent<RectTransform>().anchorMin = _chatSend.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
            _chatSend.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
            _chatSend.GetComponent<RectTransform>().anchoredPosition = new Vector2(rect.width - 176f, fy - 40f);
            _chatSend.GetComponent<RectTransform>().sizeDelta = new Vector2(76f, 38f);
            _chatSendLabel.text = gm.IsThinking ? "…" : "发送";

            // Mic + TTS + sound (mobile footer row).
            bool micHere = DshMobile.MobileStt.Offered;
            _micButton.gameObject.SetActive(micHere);
            if (micHere)
            {
                _micButton.GetComponent<RectTransform>().anchorMin = _micButton.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
                _micButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
                _micButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, fy - 86f);
                _micButton.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 40f);
                bool listening = DshMobile.MobileStt.Listening;
                _micLabel.text = listening ? "停" : (DshMobile.MobileStt.PendingPermission ? "等" : "说");
            }

            _ttsButton.gameObject.SetActive(DshMobile.MobileTts.Available);
            if (DshMobile.MobileTts.Available)
            {
                _ttsButton.GetComponent<RectTransform>().anchorMin = _ttsButton.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
                _ttsButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
                _ttsButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(rect.width - 300f, fy - 86f);
                _ttsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 40f);
                _ttsLabel.text = DshMobile.MobileTts.Enabled ? "朗读：开" : "朗读：关";
            }

            var audio = PetAudioDirector.Instance;
            _soundButton.gameObject.SetActive(audio != null);
            if (audio != null)
            {
                _soundButton.GetComponent<RectTransform>().anchorMin = _soundButton.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
                _soundButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
                _soundButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(rect.width - 200f, fy - 86f);
                _soundButton.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 40f);
                _soundLabel.text = audio.Muted ? "音效：关" : "音效：开";
            }

            string status = VoiceStatusLine();
            _voiceStatus.gameObject.SetActive(!string.IsNullOrEmpty(status));
            if (!string.IsNullOrEmpty(status))
            {
                _voiceStatus.text = status;
                _voiceStatus.color = VoiceStatusIsProblem() ? new Color(1f, 0.68f, 0.58f) : new Color(0.72f, 0.82f, 0.95f);
                _voiceStatus.rectTransform.anchorMin = _voiceStatus.rectTransform.anchorMax = new Vector2(0f, 0f);
                _voiceStatus.rectTransform.pivot = new Vector2(0f, 0f);
                _voiceStatus.rectTransform.anchoredPosition = new Vector2(18f, 4f);
                _voiceStatus.rectTransform.sizeDelta = new Vector2(rect.width - 36f, 20f);
            }

            // Keep the input's text in sync with the IMGUI-era field.
            if (!_chatInput.isFocused && _chatInput.text != _input) _chatInput.text = _input;
            if (_chatInput.isFocused) _input = _chatInput.text;
            IsTextInputFocused = _chatInput.isFocused;
        }

        private void RebuildTranscript(PetGameManager gm, float width)
        {
            var recent = gm.Memory.Recent;
            int start = Mathf.Max(0, recent.Count - MaxLogLines);
            int count = recent.Count - start;

            if (count == _builtMessageCount) return;
            _builtMessageCount = count;

            foreach (var b in _bubbles) if (b != null) Destroy(b);
            _bubbles.Clear();

            _chatContent.sizeDelta = new Vector2(-20f, Mathf.Max(100f, count * 62f + 12f));
            float y = 6f;

            for (int i = start; i < recent.Count; i++)
            {
                var line = recent[i];
                string text = line.Text ?? "";
                if (line.IsSystem)
                {
                    var t = DshMobile.Ugui.Text("System", _chatContent, text, 14,
                        new Color(0.78f, 0.80f, 0.88f, 0.9f), UnityEngine.TextAnchor.MiddleCenter);
                    t.fontStyle = FontStyle.Italic;
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.rectTransform.anchorMin = new Vector2(0f, 1f);
                    t.rectTransform.anchorMax = new Vector2(1f, 1f);
                    t.rectTransform.pivot = new Vector2(0.5f, 1f);
                    t.rectTransform.anchoredPosition = new Vector2(0f, -y);
                    t.rectTransform.sizeDelta = new Vector2(0f, 34f);
                    _bubbles.Add(t.gameObject);
                    y += 44f;
                    continue;
                }

                bool isUser = line.IsUser;
                var tint = isUser ? new Color(0.30f, 0.52f, 0.78f, 0.95f) : new Color(0.98f, 0.93f, 0.84f, 0.96f);
                var bubble = DshMobile.Ugui.Panel("Bubble", _chatContent, 12f, tint,
                    new Color(1f, 1f, 1f, 0.12f), 1.5f);
                bubble.rectTransform.pivot = new Vector2(0f, 1f);

                var label = DshMobile.Ugui.Text("Text", bubble.rectTransform, text, 16,
                    isUser ? Color.white : new Color(0.20f, 0.15f, 0.12f), UnityEngine.TextAnchor.UpperLeft);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                DshMobile.Ugui.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(12f, 7f);
                label.rectTransform.offsetMax = new Vector2(-12f, -7f);

                float maxW = width * 0.78f;
                label.rectTransform.sizeDelta = new Vector2(maxW - 24f, 0f);
                float textH = Mathf.Max(24f, label.preferredHeight + 14f);
                float textW = Mathf.Min(maxW, label.preferredWidth + 24f);
                textW = Mathf.Max(textW, 62f);
                label.rectTransform.sizeDelta = new Vector2(textW - 24f, textH - 14f);

                bubble.rectTransform.anchorMin = new Vector2(0f, 1f);
                bubble.rectTransform.anchorMax = new Vector2(0f, 1f);
                bubble.rectTransform.anchoredPosition = new Vector2(isUser ? width - 24f - textW : 12f, -y);
                bubble.rectTransform.sizeDelta = new Vector2(textW, textH);

                _bubbles.Add(bubble.gameObject);
                y += textH + 8f;
            }

            _chatContent.sizeDelta = new Vector2(-20f, y + 12f);
            _chatScroll.normalizedPosition = new Vector2(0f, 0f);
        }

        private void OnCloseChat() { _chatExpanded = false; }
        private void OnSendChat()
        {
            var gm = PetGameManager.Instance;
            if (gm == null || gm.IsThinking) return;
            gm.Talk(_input);
            _input = "";
            _chatInput.text = "";
        }
        private void OnMic()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            if (DshMobile.MobileStt.Listening) DshMobile.MobileStt.StopListening();
            else StartListeningAndSay(gm);
        }
        private void OnTts()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            SetSpeechEnabled(gm, !DshMobile.MobileTts.Enabled);
        }
        private void OnSound()
        {
            var audio = PetAudioDirector.Instance;
            if (audio != null) audio.ToggleMute();
        }

        // ------------------------------------------------------------------- pet card (uGUI)

        private void BuildPetCard()
        {
            _cardPanel = DshMobile.Ugui.Panel("CardPanel", _root, 16f,
                new Color(0.11f, 0.10f, 0.14f, 0.95f), new Color(1f, 1f, 1f, 0.10f), 2f);
            _cardPanel.gameObject.SetActive(false);
            var p = _cardPanel.rectTransform;

            // Needs bars: four fixed rows; the bladder is appended by SyncPetCard when it matters.
            for (int i = 0; i < 5; i++)
            {
                var row = new GameObject("Need" + i, typeof(RectTransform));
                row.transform.SetParent(p, false);
                var label = DshMobile.Ugui.Text("Label", row.transform, "", 14,
                    new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(40f, 20f);

                var bg = DshMobile.Ugui.Image("Bg", row.transform, new Color(1f, 1f, 1f, 0.18f));
                bg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                bg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                bg.rectTransform.pivot = new Vector2(0f, 0.5f);
                bg.rectTransform.anchoredPosition = new Vector2(46f, 0f);
                bg.rectTransform.sizeDelta = new Vector2(-120f, 10f);

                var fill = DshMobile.Ugui.Image("Fill", row.transform, Color.white);
                fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.anchoredPosition = new Vector2(46f, 0f);
                fill.rectTransform.sizeDelta = new Vector2(0f, 10f);

                var value = DshMobile.Ugui.Text("Value", row.transform, "", 13,
                    new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
                value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                value.rectTransform.pivot = new Vector2(1f, 0.5f);
                value.rectTransform.anchoredPosition = new Vector2(-6f, 0f);
                value.rectTransform.sizeDelta = new Vector2(44f, 20f);

                _needLabels.Add(label);
                _needFills.Add(fill.rectTransform);
                _needFillImages.Add(fill);
            }

            // Header summary (name is a chip; the summary sits under it).
            _headerSummary = DshMobile.Ugui.Text("HeaderSummary", p, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            _headerSummary.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Rename field for the primary pet.
            _renameField = DshMobile.Ugui.InputField("Rename", p);

            // Detail scroll.
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            _detailScroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            _detailScroll.viewport = viewport.GetComponent<RectTransform>();
            _detailScroll.horizontal = false;
            _detailScroll.vertical = true;
            _detailScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            _detailScroll.scrollSensitivity = 30f;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            _detailScroll.content = contentRt;

            _detailText = DshMobile.Ugui.Text("Detail", content.transform, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.Stretch(_detailText.rectTransform);
            _detailText.rectTransform.offsetMin = new Vector2(2f, 0f);
            _detailText.rectTransform.offsetMax = new Vector2(-2f, 0f);
        }

        private void SyncPetCard()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;

            var cards = gm.Cards();
            bool show = cards.Count > 0;
            _cardPanel.gameObject.SetActive(show);
            if (!show) return;

            var layout = ComputeLayout(DesignWidth, DesignHeight, PetSpecies.Count, TranscriptVisible);
            int selected = Mathf.Clamp(gm.SelectedPetIndex, 0, cards.Count - 1);
            var card = cards[selected];
            bool expanded = gm.CardExpanded;

            var names = new string[cards.Count];
            float chipBudget = Mathf.Max(40f, (layout.Status.width - 28f) * 0.45f);
            for (int i = 0; i < cards.Count; i++) names[i] = ShortenChip(ChipLabel(cards[i], cards[i].Name), chipBudget, 14);

            var plan = ComputeCardLayout(layout.Status, names, expanded, card.Primary, 14, 14);
            ApplyDesignRect(_cardPanel.rectTransform, plan.Panel);
            var p = _cardPanel.rectTransform;

            // Chips (rebuilt when the set of pets changes).
            if (_builtPetIndex != selected || _chipButtons.Count != cards.Count)
            {
                RebuildChips(gm, cards, names, plan.ChipRows, p, plan.Panel.width);
                _builtPetIndex = selected;
            }

            float innerW = plan.Panel.width - 28f;

            // Header summary.
            string mood = card.Needs != null ? PetUtil.MoodLabel(card.Needs.Mood) : "—";
            string need = card.Needs != null ? card.Needs.DominantNeed : "";
            string who = card.Species != null ? card.Species.DisplayName : "";
            string summary = $"{card.Name}　·　{mood}　·　{(string.IsNullOrEmpty(need) ? "状态不错" : "想要：" + need)}";
            _headerSummary.text = summary;
            _headerSummary.rectTransform.anchorMin = _headerSummary.rectTransform.anchorMax = new Vector2(0f, 1f);
            _headerSummary.rectTransform.pivot = new Vector2(0f, 1f);
            _headerSummary.rectTransform.anchoredPosition = new Vector2(14f, -(12f + plan.ChipsHeight + 4f));
            _headerSummary.rectTransform.sizeDelta = new Vector2(innerW, plan.HeaderHeight - 8f);

            // Rename field (primary, expanded only).
            _renameField.gameObject.SetActive(expanded && card.Primary);
            if (expanded && card.Primary)
            {
                if (_editPetName == null || _editPetName != gm.PetName) _editPetName = gm.PetName;
                if (!_renameField.isFocused) _renameField.text = _editPetName;
                if (_renameField.isFocused) _editPetName = _renameField.text;
                _renameField.GetComponent<RectTransform>().anchorMin = _renameField.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
                _renameField.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
                _renameField.GetComponent<RectTransform>().anchoredPosition = new Vector2(14f, -(12f + plan.ChipsHeight + 2f));
                _renameField.GetComponent<RectTransform>().sizeDelta = new Vector2(innerW - 60f, 30f);
            }
            IsTextInputFocused = IsTextInputFocused || _renameField.isFocused;

            // Detail (needs bars + text), rebuilt when the selected pet/expansion changes.
            float detailY = 12f + plan.ChipsHeight + plan.HeaderHeight;
            float footerH = plan.FooterHeight + 12f;

            _detailScroll.viewport.anchorMin = new Vector2(0f, 0f);
            _detailScroll.viewport.anchorMax = new Vector2(1f, 1f);
            _detailScroll.viewport.offsetMin = new Vector2(12f, footerH);
            _detailScroll.viewport.offsetMax = new Vector2(-12f, -detailY);

            if (_builtCardExpanded != expanded)
            {
                RebuildDetail(gm, card, expanded);
                _builtCardExpanded = expanded;
            }
            SyncNeedsBars(card.Needs, plan.Panel.width);

            // Footer buttons (rebuilt when the plan changes).
            RebuildFooter(gm, plan, p, plan.Panel.width);
        }

        private void RebuildChips(PetGameManager gm, List<PetGameManager.PetCard> cards, string[] names, int maxRows, RectTransform p, float designWidth)
        {
            foreach (var b in _chipButtons) if (b != null) Destroy(b.gameObject);
            _chipButtons.Clear();
            _chipLabels.Clear();

            var packed = PackRows(names, Mathf.Max(60f, designWidth - 28f - RowPackingSlack), 14, buttonPadding: 20f, spacing: ChipSpacing);
            int rowCount = Mathf.Clamp(maxRows, 0, packed.Length);
            int index = 0;

            for (int r = 0; r < rowCount; r++)
            {
                float x = 14f;
                foreach (string label in packed[r])
                {
                    if (index >= cards.Count) break;
                    float chipWidth = Mathf.Min(designWidth - 28f, EstimatedLabelWidth(label, 14) + 20f);
                    var chip = DshMobile.Ugui.Button("Chip", p, label, 14, new Color(0.30f, 0.40f, 0.58f));
                    chip.GetComponent<RectTransform>().anchorMin = chip.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
                    chip.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
                    chip.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, -(12f + r * (ChipHeight + ChipSpacing)));
                    chip.GetComponent<RectTransform>().sizeDelta = new Vector2(chipWidth, ChipHeight);
                    int ci = index;
                    chip.onClick.AddListener(() => gm.SelectPet(ci, toggleIfSame: true));
                    _chipButtons.Add(chip);
                    _chipLabels.Add(chip.GetComponentInChildren<UnityEngine.UI.Text>());
                    x += chipWidth + ChipSpacing;
                    index++;
                }
            }

            for (int i = 0; i < _chipLabels.Count; i++)
                _chipLabels[i].color = i == gm.SelectedPetIndex ? new Color(1f, 0.94f, 0.75f) : Color.white;
        }

        private void RebuildDetail(PetGameManager gm, PetGameManager.PetCard card, bool expanded)
        {
            var sb = new System.Text.StringBuilder();
            if (expanded)
            {
                if (card.Primary && !gm.BrainConfig.CanUseNetwork)
                    sb.AppendLine("⚙ 配置大脑连接（当前离线）");

                if (card.Personality != null)
                {
                    sb.AppendLine("性格：" + gm.Personality.Archetype);
                    if (!Mobile || _statusDetail) sb.AppendLine(card.Personality.Summary);
                }

                sb.AppendLine("音色：" + PetVoice.Describe(card.Species, card.Personality));

                if (!card.Primary)
                {
                    sb.AppendLine("同伴：自己走动、有自己的状态与音色，摸它有反应；它不接大脑，所以不花 token。");
                    sb.AppendLine("想换主要照顾的那只，去「宠物」面板的背包页。");
                }
                else if (!Mobile || _statusDetail)
                {
                    string mode = gm.Controller != null ? gm.Controller.CurrentMode.ToString() : "-";
                    string action = string.IsNullOrEmpty(gm.LastActionLabel) ? "休息中" : gm.LastActionLabel;
                    sb.AppendLine($"行为：{mode}　动作：{action}");
                    if (!string.IsNullOrEmpty(gm.LastBehaviorLabel)) sb.AppendLine($"刚才：{gm.LastBehaviorLabel}");
                    if (!string.IsNullOrEmpty(gm.LastNudgeReason)) sb.AppendLine($"主动开口：{gm.LastNudgeReason}");
                    sb.AppendLine("大脑：" + gm.BrainConfig.Describe());
                    if (!gm.BrainConfig.CanUseNetwork) sb.AppendLine("　" + gm.BrainConfig.StatusDetail());
                }

                if (card.Primary) sb.AppendLine($"{gm.Journal.Count} 条记忆　·　{card.Needs.MoodScore:P0} 状态分");
                else sb.AppendLine($"状态分 {card.Needs.MoodScore:P0}　·　亲密度 {card.Needs.Affection:P0}");
            }

            _detailText.text = sb.ToString();
            _detailText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(60f, _detailText.preferredHeight + 10f));
            _detailText.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        }

        private void SyncNeedsBars(PetNeeds needs, float designWidth)
        {
            var bars = new[] {
                new { Label = "饱食", Value = needs != null ? needs.Hunger : 0f, Color = new Color(0.95f, 0.62f, 0.30f) },
                new { Label = "精力", Value = needs != null ? needs.Energy : 0f, Color = new Color(0.45f, 0.80f, 0.95f) },
                new { Label = "开心", Value = needs != null ? needs.Joy : 0f, Color = new Color(0.98f, 0.80f, 0.35f) },
                new { Label = "清洁", Value = needs != null ? needs.Cleanliness : 0f, Color = new Color(0.60f, 0.90f, 0.65f) },
                new { Label = "便意", Value = needs != null ? needs.Bladder : 0f, Color = new Color(0.85f, 0.72f, 0.45f) }
            };

            var p = _cardPanel.rectTransform;
            bool showBladder = needs != null && needs.Bladder < 0.6f;
            float y = 12f + 0f;

            for (int i = 0; i < 5; i++)
            {
                if (i == 4 && !showBladder) { _needLabels[i].transform.parent.gameObject.SetActive(false); continue; }
                _needLabels[i].transform.parent.gameObject.SetActive(true);
                var rowRt = _needLabels[i].transform.parent.GetComponent<RectTransform>();
                rowRt.anchorMin = rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(14f, -y);
                rowRt.sizeDelta = new Vector2(designWidth - 28f, 22f);

                var b = bars[i];
                _needLabels[i].text = b.Label;
                float v = Mathf.Clamp01(b.Value);
                _needFills[i].sizeDelta = new Vector2((designWidth - 148f) * v, 10f);
                _needFillImages[i].color = v < 0.25f ? Color.Lerp(b.Color, Color.red, 0.55f) : b.Color;
                _needLabels[i].transform.parent.Find("Value").GetComponent<UnityEngine.UI.Text>().text = $"{b.Value:P0}";
                y += 24f;
            }
        }

        private void RebuildFooter(PetGameManager gm, PetCardLayout plan, RectTransform p, float designWidth)
        {
            foreach (var b in _footerButtons) if (b != null) Destroy(b.gameObject);
            _footerButtons.Clear();

            float y = plan.FooterHeight + 6f;
            foreach (var row in plan.FooterRows)
            {
                var packed = row;
                float total = 0f;
                foreach (var label in packed) total += EstimatedLabelWidth(label, 14) + 18f + 6f;
                float x = 14f + Mathf.Max(0f, (designWidth - 28f - total) * 0.5f);
                foreach (string label in packed)
                {
                    float w = EstimatedLabelWidth(label, 14) + 18f;
                    var btn = DshMobile.Ugui.Button("Footer", p, label, 14, new Color(0.30f, 0.40f, 0.58f));
                    btn.GetComponent<RectTransform>().anchorMin = btn.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
                    btn.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
                    btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y - FooterRowHeight);
                    btn.GetComponent<RectTransform>().sizeDelta = new Vector2(w, FooterRowHeight);
                    string lbl = label;
                    btn.onClick.AddListener(() => HandleFooterButton(lbl, gm));
                    _footerButtons.Add(btn);
                    x += w + 6f;
                }
                y -= FooterRowHeight + 4f;
            }
        }

        private void BuildPlacementBar()
        {
            _placementBar = DshMobile.Ugui.Panel("PlacementBar", _root, 14f,
                new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(0.6f, 0.9f, 1f, 0.5f), 1.5f);
            _placementBar.gameObject.SetActive(false);
            _placementText = DshMobile.Ugui.Text("PlacementText", _placementBar.rectTransform,
                "自由摆放中：按住家具拖到新位置", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            _placementText.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.Stretch(_placementText.rectTransform);
            _placementText.rectTransform.offsetMin = new Vector2(14f, 8f);
            _placementText.rectTransform.offsetMax = new Vector2(-110f, -8f);

            var done = DshMobile.Ugui.Button("Done", _placementBar.rectTransform, "完成", 16,
                new Color(0.30f, 0.55f, 0.35f));
            done.GetComponent<RectTransform>().anchorMin = done.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            done.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            done.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10f, 0f);
            done.GetComponent<RectTransform>().sizeDelta = new Vector2(84f, 36f);
            done.onClick.AddListener(OnDonePlacement);
        }

        private void SyncPlacementBar()
        {
            _placementBar.gameObject.SetActive(_placeMode);
            if (_placeMode)
            {
                _placementBar.rectTransform.anchorMin = _placementBar.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                _placementBar.rectTransform.pivot = new Vector2(0.5f, 1f);
                _placementBar.rectTransform.anchoredPosition = new Vector2(0f, -12f);
                _placementBar.rectTransform.sizeDelta = new Vector2(440f, 54f);
            }
        }

        private void OnDonePlacement()
        {
            _placeMode = false;
            PlacementDragger.SetActive(false);
            DshMobile.MobileHaptics.Light();
        }

        private void BuildMapPanel()
        {
            _modalScrim = DshMobile.Ugui.Image("ModalScrim", _root, new Color(0f, 0f, 0f, 0.55f));
            DshMobile.Ugui.Stretch(_modalScrim.rectTransform);
            _modalScrim.gameObject.SetActive(false);

            _mapPanel = DshMobile.Ugui.Panel("MapPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _mapPanel.gameObject.SetActive(false);
            var p = _mapPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "地图", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 200f, 32f);

            _mapBalance = DshMobile.Ugui.Text("Balance", p, "", 20, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleRight, true);
            DshMobile.Ugui.SetRect(_mapBalance.rectTransform, -260f, 14f, 180f, 32f);

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -90f, 16f, 76f, 30f);
            close.onClick.AddListener(OnCloseMap);

            _mapHere = DshMobile.Ugui.Text("Here", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_mapHere.rectTransform, 18f, 48f, 500f, 22f);

            _mapMessageText = DshMobile.Ugui.Text("Message", p, "", 14, new Color(0.7f, 0.95f, 0.75f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_mapMessageText.rectTransform, 18f, 72f, 500f, 22f);

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            var scroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport.anchorMin = new Vector2(0f, 0f);
            scroll.viewport.anchorMax = new Vector2(1f, 1f);
            scroll.viewport.offsetMin = new Vector2(18f, 14f);
            scroll.viewport.offsetMax = new Vector2(-18f, -96f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _mapContent = content.GetComponent<RectTransform>();
            _mapContent.anchorMin = new Vector2(0f, 1f);
            _mapContent.anchorMax = new Vector2(1f, 1f);
            _mapContent.pivot = new Vector2(0.5f, 1f);
            scroll.content = _mapContent;
        }

        private void OnCloseMap()
        {
            var gm = PetGameManager.Instance;
            if (gm != null) gm.CloseDoorPrompt();
        }

        private void SyncMapPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            bool open = gm.DoorPromptOpen;
            _modalScrim.gameObject.SetActive(open);
            _mapPanel.gameObject.SetActive(open);
            if (!open) { _mapBuilt = false; return; }

            var w = Mathf.Min(680f, DesignWidth - 32f);
            var h = Mathf.Min(620f, DesignHeight - 32f);
            _mapPanel.rectTransform.anchorMin = _mapPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _mapPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _mapPanel.rectTransform.anchoredPosition = Vector2.zero;
            _mapPanel.rectTransform.sizeDelta = new Vector2(w, h);

            _mapBalance.text = "宠物币 " + DshMobile.PetWallet.Coins.ToString("N0");
            var here = RoomThemeInfo.Get(PetWorldMap.Current);
            _mapHere.text = $"现在住在 {here.DisplayName}　·　{here.Effects()}";
            _mapMessageText.text = _mapMessage;
            _mapMessageText.color = _mapError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);

            if (!_mapBuilt) { RebuildMapContent(gm); _mapBuilt = true; }
        }

        private void RebuildMapContent(PetGameManager gm)
        {
            for (int i = _mapContent.childCount - 1; i >= 0; i--)
                Destroy(_mapContent.GetChild(i).gameObject);

            float y = 0f;
            foreach (var info in RoomThemeInfo.All)
            {
                y = BuildPlaceRow(gm, info, _mapContent, y);
                y += 8f;
            }

            y += 4f;
            y = BuildMapLabel(_mapContent, y, "出去走走（赚宠物币）");

            var games = gm.AvailableMiniGames();
            if (games.Count == 0)
            {
                y = BuildMapLabel(_mapContent, y, "暂时没有可以去的活动。");
            }
            else
            {
                foreach (var game in games)
                {
                    y = BuildMapGame(_mapContent, y, game.Icon + "  " + game.DisplayName, game.Blurb, () => gm.LaunchMiniGame(game.Id));
                }
            }

            y += 4f;
            y = BuildMapLabel(_mapContent, y, "在屋里玩（也赚宠物币）");
            y = BuildMapGame(_mapContent, y, "🧩 拼图", "把这间屋子的画拼回去，步数越少宠物币越多。",
                () => { gm.CloseDoorPrompt(); OpenPuzzle(gm); });
            y = BuildMapGame(_mapContent, y, "🃏 记忆配对", "翻开两张一样的卡片就留下，全配完给宠物币。",
                () => { gm.CloseDoorPrompt(); OpenMemoryMatch(gm); });

            _mapContent.sizeDelta = new Vector2(-8f, y + 12f);
        }

        private static float BuildMapLabel(RectTransform content, float y, string text)
        {
            var label = DshMobile.Ugui.Text("Label", content, text, 16, Color.white, UnityEngine.TextAnchor.UpperLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(2f, -y);
            label.rectTransform.sizeDelta = new Vector2(0f, 26f);
            return y + 30f;
        }

        private static float BuildMapGame(RectTransform content, float y, string name, string blurb, UnityEngine.Events.UnityAction onClick)
        {
            var btn = DshMobile.Ugui.Button("Game", content, name, 16, new Color(0.30f, 0.40f, 0.58f));
            btn.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
            btn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            btn.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);
            btn.onClick.AddListener(onClick);
            y += 42f;

            var hint = DshMobile.Ugui.Text("Blurb", content, "　　" + blurb, 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.rectTransform.anchorMin = new Vector2(0f, 1f);
            hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(0f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -y);
            hint.rectTransform.sizeDelta = new Vector2(0f, 22f);
            return y + 28f;
        }

        private float BuildPlaceRow(PetGameManager gm, RoomThemeInfo info, RectTransform content, float y)
        {
            bool unlocked = PetWorldMap.IsUnlocked(info.Theme);
            bool current = PetWorldMap.Current == info.Theme;

            var row = new GameObject("Place", typeof(RectTransform));
            row.transform.SetParent(content, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 88f);

            // Swatch: three colour bands.
            var bands = new[] { info.Floor, info.Wall, info.Rug };
            for (int i = 0; i < 3; i++)
            {
                var band = DshMobile.Ugui.Image("Band", row.transform, bands[i]);
                band.rectTransform.anchorMin = new Vector2(0f, 1f);
                band.rectTransform.anchorMax = new Vector2(0f, 1f);
                band.rectTransform.pivot = new Vector2(0f, 1f);
                band.rectTransform.anchoredPosition = new Vector2(2f + i * 14f, -8f);
                band.rectTransform.sizeDelta = new Vector2(12f, 22f);
            }

            string title = current ? $"{info.DisplayName}　·　现在在这里" : info.DisplayName;
            var name = DshMobile.Ugui.Text("Name", row.transform, title, 16, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 50f, 0f, 400f, 24f);

            var blurb = DshMobile.Ugui.Text("Blurb", row.transform, info.Blurb, 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(blurb.rectTransform, 50f, 24f, 400f, 34f);

            var fx = DshMobile.Ugui.Text("Fx", row.transform, "效果：" + info.Effects(), 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(fx.rectTransform, 50f, 58f, 400f, 24f);

            var button = DshMobile.Ugui.Button("Action", row.transform, "", 16, new Color(0.30f, 0.40f, 0.58f));
            button.GetComponent<RectTransform>().anchorMin = button.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            button.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 40f);
            var buttonLabel = button.GetComponentInChildren<UnityEngine.UI.Text>();

            if (current)
            {
                button.interactable = false;
                buttonLabel.text = "住在这里";
            }
            else if (unlocked)
            {
                buttonLabel.text = "前往";
                button.onClick.AddListener(() =>
                {
                    string message;
                    gm.TravelTo(info.Theme, out message);
                    SetMapMessage(message);
                    DshMobile.MobileHaptics.Light();
                });
            }
            else
            {
                bool afford = DshMobile.PetWallet.CanAfford(info.Price);
                button.interactable = afford;
                buttonLabel.text = afford ? "解锁并搬入" : $"还差 {info.Price - DshMobile.PetWallet.Coins}";
                button.onClick.AddListener(() =>
                {
                    string message;
                    if (PetWorldMap.TryUnlock(info.Theme, out message))
                    {
                        string moved;
                        gm.TravelTo(info.Theme, out moved);
                        SetMapMessage(message + "，" + moved);
                    }
                    else SetMapMessage(message, true);
                    DshMobile.MobileHaptics.Light();
                });
            }

            return y + 92f;
        }

        // ----------------------------------------------------------------- settings (uGUI)

        private void BuildSettingsPanel()
        {
            _settingsPanel = DshMobile.Ugui.Panel("SettingsPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _settingsPanel.gameObject.SetActive(false);
            var p = _settingsPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "宠物大脑设置", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 300f, 30f);

            var sub = DshMobile.Ugui.Text("Sub", p, "默认指向 DeepSeek 官方接口；任何 OpenAI 兼容端点都可以填在这里。", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(sub.rectTransform, 18f, 44f, 460f, 20f);

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            var scroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport.anchorMin = new Vector2(0f, 0f);
            scroll.viewport.anchorMax = new Vector2(1f, 1f);
            scroll.viewport.offsetMin = new Vector2(16f, 56f);
            scroll.viewport.offsetMax = new Vector2(-16f, -70f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _settingsContent = content.GetComponent<RectTransform>();
            _settingsContent.anchorMin = new Vector2(0f, 1f);
            _settingsContent.anchorMax = new Vector2(1f, 1f);
            _settingsContent.pivot = new Vector2(0.5f, 1f);
            scroll.content = _settingsContent;

            float y = 4f;
            y = BuildSettingsField(_settingsContent, y, "Base URL", 300, out _baseUrlField);
            y = BuildSettingsField(_settingsContent, y, "模型", 120, out _modelField);
            y = BuildSettingsField(_settingsContent, y, "API Key（留空则使用离线大脑）", 200, out _keyField, password: true);
            y += 4f;

            y = BuildSettingsToggle(_settingsContent, y, "允许无鉴权（本地网关）", () => _editAnonymous, v => _editAnonymous = v);
            y = BuildSettingsToggle(_settingsContent, y, "强制离线模式", () => _editOffline, v => _editOffline = v);
            y = BuildSettingsToggle(_settingsContent, y, "振动反馈（轻/中/重）", () => DshMobile.MobileHaptics.Enabled, v => DshMobile.MobileHaptics.Enabled = v);
            y = BuildSettingsToggle(_settingsContent, y, "背景音乐", () => DshMobile.MobileMusic.Enabled, v => DshMobile.MobileMusic.Enabled = v);
            y = BuildSettingsToggle(_settingsContent, y, "宠物叫声", () => PetVoice.Enabled, v => { PetVoice.Enabled = v; });
            y = BuildSettingsToggle(_settingsContent, y, "朗读宠物的话（语音输出）", () => DshMobile.MobileTts.Enabled,
                v => { DshMobile.MobileTts.Enabled = v; if (v) DshMobile.MobileTts.WarmUp(); else DshMobile.MobileTts.Stop(); });
            y = BuildSettingsToggle(_settingsContent, y, "显示麦克风按钮", () => DshMobile.MobileStt.Enabled,
                v => { DshMobile.MobileStt.Enabled = v; if (!v) DshMobile.MobileStt.Cancel(); });

            // Music volume slider.
            var volLabel = DshMobile.Ugui.Text("VolLabel", _settingsContent, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            volLabel.rectTransform.anchorMin = volLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            volLabel.rectTransform.pivot = new Vector2(0f, 1f);
            volLabel.rectTransform.anchoredPosition = new Vector2(2f, -y);
            volLabel.rectTransform.sizeDelta = new Vector2(120f, 24f);
            _musicVolumeLabel = volLabel;

            _musicSlider = DshMobile.Ugui.Slider("MusicVol", _settingsContent, 0f, 1f, DshMobile.MobileMusic.Volume,
                new Color(0.35f, 0.6f, 0.9f), Color.white);
            _musicSlider.GetComponent<RectTransform>().anchorMin = _musicSlider.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            _musicSlider.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            _musicSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(130f, -y);
            _musicSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 22f);
            _musicSlider.onValueChanged.AddListener(v => { if (_overlaysBuilt) DshMobile.MobileMusic.Volume = v; });
            y += 30f;

            // Status / result line.
            _settingsStatus = DshMobile.Ugui.Text("Status", _settingsContent, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            _settingsStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _settingsStatus.rectTransform.anchorMin = new Vector2(0f, 1f);
            _settingsStatus.rectTransform.anchorMax = new Vector2(1f, 1f);
            _settingsStatus.rectTransform.pivot = new Vector2(0f, 1f);
            _settingsStatus.rectTransform.anchoredPosition = new Vector2(2f, -y);
            _settingsStatus.rectTransform.sizeDelta = new Vector2(-4f, 90f);
            y += 96f;

            // Personality + brain config buttons.
            y = BuildSettingsAction(_settingsContent, y, "换一个性格", () => { var g = PetGameManager.Instance; if (g != null) g.RerollPersonality(); });
            y = BuildSettingsAction(_settingsContent, y, "打开系统语音设置（装中文语音）", () =>
            {
                bool opened = DshMobile.MobileTts.OpenSystemSettings();
                SetVoiceMessage(opened ? "已经打开系统的「文字转语音」设置。" : "这台设备打不开系统语音设置。", !opened);
            });
            y = BuildSettingsAction(_settingsContent, y, "申请麦克风权限", () => DshMobile.MobileStt.RequestPermission());
            y = BuildSettingsAction(_settingsContent, y, "重置识别器（识别不动时点一下）", () =>
            {
                DshMobile.MobileStt.ResetRecognizer();
                SetVoiceMessage("识别器已经重建，再点一次麦克风试试。", false);
            });
            y += 10f;

            _settingsContent.sizeDelta = new Vector2(-4f, y);

            // Footer (pinned).
            var save = DshMobile.Ugui.Button("Save", p, "保存并应用", 16, new Color(0.30f, 0.55f, 0.35f));
            DshMobile.Ugui.SetRect(save.GetComponent<RectTransform>(), 16f, -46f, 150f, 40f);
            save.onClick.AddListener(OnSaveSettings);

            var test = DshMobile.Ugui.Button("Test", p, "测试连接", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(test.GetComponent<RectTransform>(), 176f, -46f, 130f, 40f);
            test.onClick.AddListener(() => TestConnection());

            var env = DshMobile.Ugui.Button("Env", p, "读环境变量", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(env.GetComponent<RectTransform>(), 316f, -46f, 130f, 40f);
            env.onClick.AddListener(OnReadEnv);

            var cancel = DshMobile.Ugui.Button("Cancel", p, "取消", 16, new Color(0.75f, 0.35f, 0.35f));
            DshMobile.Ugui.SetRect(cancel.GetComponent<RectTransform>(), -162f, -46f, 130f, 40f);
            cancel.onClick.AddListener(() => _showSettings = false);
        }

        private static float BuildSettingsField(RectTransform content, float y, string label, int charLimit,
            out UnityEngine.UI.InputField field, bool password = false)
        {
            var lab = DshMobile.Ugui.Text("Label", content, label, 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            lab.rectTransform.anchorMin = lab.rectTransform.anchorMax = new Vector2(0f, 1f);
            lab.rectTransform.pivot = new Vector2(0f, 1f);
            lab.rectTransform.anchoredPosition = new Vector2(2f, -y);
            lab.rectTransform.sizeDelta = new Vector2(300f, 20f);
            y += 20f;

            field = DshMobile.Ugui.InputField("Field", content);
            if (password) field.contentType = UnityEngine.UI.InputField.ContentType.Password;
            field.characterLimit = charLimit;
            field.GetComponent<RectTransform>().anchorMin = field.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            field.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            field.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f, -y);
            field.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Min(charLimit * 1.2f + 40f, 360f), 30f);
            return y + 34f;
        }

        private float BuildSettingsToggle(RectTransform content, float y, string label,
            System.Func<bool> getter, System.Action<bool> setter)
        {
            var btn = DshMobile.Ugui.Button("Toggle", content, "", 14, new Color(0.30f, 0.40f, 0.58f));
            btn.GetComponent<RectTransform>().anchorMin = btn.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            btn.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f, -y);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 28f);
            var lab = btn.GetComponentInChildren<UnityEngine.UI.Text>();
            lab.alignment = UnityEngine.TextAnchor.MiddleLeft;
            _toggleLabels.Add(lab);
            _toggleTexts.Add(label);
            _toggleGetters.Add(getter);
            _toggleSetters.Add(setter);
            int index = _toggleLabels.Count - 1;
            btn.onClick.AddListener(() => _toggleSetters[index](!_toggleGetters[index]()));
            return y + 32f;
        }

        private static float BuildSettingsAction(RectTransform content, float y, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btn = DshMobile.Ugui.Button("Action", content, label, 14, new Color(0.30f, 0.40f, 0.58f));
            btn.GetComponent<RectTransform>().anchorMin = btn.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            btn.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f, -y);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 28f);
            btn.onClick.AddListener(onClick);
            return y + 32f;
        }

        private void SyncSettingsPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            bool open = _showSettings;
            _settingsPanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || (gm.DoorPromptOpen));
            if (!open) return;

            var w = Mathf.Min(520f, DesignWidth - 32f);
            var h = Mathf.Min(660f, DesignHeight - 40f);
            _settingsPanel.rectTransform.anchorMin = _settingsPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _settingsPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _settingsPanel.rectTransform.anchoredPosition = Vector2.zero;
            _settingsPanel.rectTransform.sizeDelta = new Vector2(w, h);

            if (_baseUrlField.text != (_editBaseUrl ?? "") && !_baseUrlField.isFocused) _baseUrlField.text = _editBaseUrl ?? "";
            if (_baseUrlField.isFocused) _editBaseUrl = _baseUrlField.text;
            if (_modelField.text != (_editModel ?? "") && !_modelField.isFocused) _modelField.text = _editModel ?? "";
            if (_modelField.isFocused) _editModel = _modelField.text;
            if (_keyField.text != (_editKey ?? "") && !_keyField.isFocused) _keyField.text = _editKey ?? "";
            if (_keyField.isFocused) _editKey = _keyField.text;

            for (int i = 0; i < _toggleLabels.Count; i++)
            {
                bool on = _toggleGetters[i]();
                _toggleLabels[i].text = (on ? "☑ " : "☐ ") + _toggleTexts[i];
            }
            _musicVolumeLabel.text = "音量 " + Mathf.RoundToInt(DshMobile.MobileMusic.Volume * 100f) + "%";
            _settingsStatus.text = _testResult + (string.IsNullOrEmpty(_voiceMessage) ? "" : "\n" + _voiceMessage);
        }

        private void OnSaveSettings()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            ApplyEditConfig(gm);
            gm.BrainConfig.Save();
            gm.RebuildBrain();
            _showSettings = false;
        }

        private void OnReadEnv()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            var resolved = PetBrainConfig.FromEnvironment();
            gm.BrainConfig = resolved;
            gm.RebuildBrain();
            FillEditConfig(gm);
            _testResult = "已从环境变量重新读取：" + resolved.Describe();
        }

        // ----------------------------------------------------------------- journal (uGUI)

        private void BuildJournalPanel()
        {
            _journalPanel = DshMobile.Ugui.Panel("JournalPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _journalPanel.gameObject.SetActive(false);
            var p = _journalPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "记事本", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 10f, 200f, 30f);

            var prev = DshMobile.Ugui.Button("Prev", p, "◀", 14, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(prev.GetComponent<RectTransform>(), -300f, 14f, 34f, 26f);
            prev.onClick.AddListener(() => { _journalMonth = _journalMonth.AddMonths(-1); });

            _journalMonthLabel = DshMobile.Ugui.Text("Month", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_journalMonthLabel.rectTransform, -262f, 16f, 150f, 24f);

            var next = DshMobile.Ugui.Button("Next", p, "▶", 14, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(next.GetComponent<RectTransform>(), -120f, 14f, 34f, 26f);
            next.onClick.AddListener(() => { _journalMonth = _journalMonth.AddMonths(1); });

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 14, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -76f, 14f, 58f, 26f);
            close.onClick.AddListener(() => _showJournal = false);

            _journalStorageLabel = DshMobile.Ugui.Text("Storage", p, "", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_journalStorageLabel.rectTransform, 18f, 42f, 400f, 26f);

            var clearAll = DshMobile.Ugui.Button("ClearAll", p, "清空全部", 14, new Color(0.75f, 0.35f, 0.35f));
            DshMobile.Ugui.SetRect(clearAll.GetComponent<RectTransform>(), -104f, 42f, 96f, 26f);
            clearAll.onClick.AddListener(OnClearAllJournal);

            var clearMonth = DshMobile.Ugui.Button("ClearMonth", p, "清理本月", 14, new Color(0.75f, 0.35f, 0.35f));
            DshMobile.Ugui.SetRect(clearMonth.GetComponent<RectTransform>(), -208f, 42f, 96f, 26f);
            clearMonth.onClick.AddListener(OnClearMonthJournal);

            _journalStatusText = DshMobile.Ugui.Text("Status", p, "", 14, new Color(1f, 0.9f, 0.6f), UnityEngine.TextAnchor.MiddleRight);
            DshMobile.Ugui.SetRect(_journalStatusText.rectTransform, -250f, 68f, 250f, 20f);

            _journalGrid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            _journalGrid.SetParent(p, false);
            DshMobile.Ugui.SetRect(_journalGrid, 18f, 96f, 0f, 0f);

            _journalDayLabel = DshMobile.Ugui.Text("DayLabel", p, "", 16, Color.white, UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_journalDayLabel.rectTransform, 18f, 0f, 400f, 22f);

            _deleteDayButton = DshMobile.Ugui.Button("DeleteDay", p, "删除这一天", 14, new Color(0.75f, 0.35f, 0.35f));
            DshMobile.Ugui.SetRect(_deleteDayButton.GetComponent<RectTransform>(), -120f, 0f, 120f, 26f);
            _deleteDayLabel = _deleteDayButton.GetComponentInChildren<UnityEngine.UI.Text>();
            _deleteDayButton.onClick.AddListener(OnDeleteDay);

            _journalDayContent = new GameObject("DayContent", typeof(RectTransform)).GetComponent<RectTransform>();
            _journalDayContent.SetParent(p, false);
        }

        private void SyncJournalPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            bool open = _showJournal;
            _journalPanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || _showSettings || gm.DoorPromptOpen);
            if (!open) return;

            var w = Mathf.Min(720f, DesignWidth - 32f);
            var h = Mathf.Min(560f, DesignHeight - 32f);
            _journalPanel.rectTransform.anchorMin = _journalPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _journalPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _journalPanel.rectTransform.anchoredPosition = Vector2.zero;
            _journalPanel.rectTransform.sizeDelta = new Vector2(w, h);

            _journalMonthLabel.text = $"{_journalMonth:yyyy 年 M 月}";
            var journal = gm.Journal;
            _journalStorageLabel.text = $"{journal.Count} 条记忆（{journal.PinnedCount} 条钉住）　占用 {PetJournal.FormatBytes(journal.StorageBytes)}";
            _journalStatusText.text = _journalStatus;

            if (_builtJournalMonth != _journalMonth || _builtJournalDay != _selectedDay.Date)
            {
                RebuildJournalGrid(gm, w);
                RebuildJournalDay(gm, w, h);
                _builtJournalMonth = _journalMonth;
                _builtJournalDay = _selectedDay.Date;
            }
        }

        private void RebuildJournalGrid(PetGameManager gm, float w)
        {
            for (int i = _journalGrid.childCount - 1; i >= 0; i--) Destroy(_journalGrid.GetChild(i).gameObject);

            string[] weekdays = { "一", "二", "三", "四", "五", "六", "日" };
            float cellW = (w - 36f) / 7f;
            float cellH = Mathf.Clamp((Mathf.Min(560f, DesignHeight - 32f) - 78f - 20f - 116f) / 6f, 24f, 46f);
            for (int i = 0; i < 7; i++)
            {
                var wd = DshMobile.Ugui.Text("Wd", _journalGrid, weekdays[i], 14,
                    new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleCenter);
                DshMobile.Ugui.Place(wd.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(i * cellW, -18f), new Vector2(cellW, 18f));
            }

            var first = new DateTime(_journalMonth.Year, _journalMonth.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(_journalMonth.Year, _journalMonth.Month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_journalMonth.Year, _journalMonth.Month, day);
                int slot = offset + day - 1;
                int col = slot % 7;
                int row = slot / 7;

                bool selected = date.Date == _selectedDay.Date;
                bool today = date.Date == DateTime.Now.Date;

                var cell = DshMobile.Ugui.Button("Cell", _journalGrid, day.ToString(), 12,
                    selected ? new Color(1f, 0.92f, 0.7f, 0.95f) : (today ? new Color(0.75f, 0.85f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.16f)));
                DshMobile.Ugui.Place(cell.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(col * cellW + 2f, -(row * cellH + 2f)), new Vector2(cellW - 4f, cellH - 4f));
                var dayLabel = cell.GetComponentInChildren<UnityEngine.UI.Text>();
                dayLabel.alignment = UnityEngine.TextAnchor.UpperLeft;
                DateTime captured = date;
                cell.onClick.AddListener(() => _selectedDay = captured);

                var kinds = gm.Journal.KindsOn(date);
                for (int k = 0; k < kinds.Count && k < 4; k++)
                {
                    var dot = DshMobile.Ugui.Image("Dot", cell.transform, KindColor(kinds[k]));
                    DshMobile.Ugui.Place(dot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(5f + k * 9f, 5f), new Vector2(7f, 7f));
                }
            }
        }

        private void RebuildJournalDay(PetGameManager gm, float w, float h)
        {
            for (int i = _journalDayContent.childCount - 1; i >= 0; i--) Destroy(_journalDayContent.GetChild(i).gameObject);

            var journal = gm.Journal;
            var entries = journal.ForDay(_selectedDay);
            _journalDayLabel.text = $"{_selectedDay:yyyy-MM-dd}　{entries.Count} 条";
            _journalDayLabel.rectTransform.anchorMin = _journalDayLabel.rectTransform.anchorMax = new Vector2(0f, 0f);
            _journalDayLabel.rectTransform.pivot = new Vector2(0f, 0f);
            _journalDayLabel.rectTransform.anchoredPosition = new Vector2(18f, 150f);
            _journalDayLabel.rectTransform.sizeDelta = new Vector2(400f, 22f);

            _deleteDayButton.gameObject.SetActive(entries.Count > 0);
            if (entries.Count > 0)
            {
                bool armed = _journalConfirmDay == _selectedDay.Date;
                _deleteDayLabel.text = armed ? $"真的删掉 {entries.Count} 条？" : "删除这一天";
                _deleteDayButton.GetComponent<RectTransform>().anchorMin = _deleteDayButton.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0f);
                _deleteDayButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
                _deleteDayButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-18f, 148f);
                _deleteDayButton.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 26f);
            }

            DshMobile.Ugui.SetRect(_journalDayContent, 18f, 180f, w - 36f, h - 200f);

            float y = 4f;
            if (entries.Count == 0)
            {
                var empty = DshMobile.Ugui.Text("Empty", _journalDayContent, "这一天什么也没发生。", 14,
                    new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
                empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(0f, 1f);
                empty.rectTransform.pivot = new Vector2(0f, 1f);
                empty.rectTransform.anchoredPosition = new Vector2(2f, -y);
                empty.rectTransform.sizeDelta = new Vector2(300f, 22f);
                return;
            }

            foreach (var entry in entries)
            {
                var row = DshMobile.Ugui.Text("Entry", _journalDayContent,
                    $"[{KindLabel(entry.Kind)}] {entry.When:HH:mm}  {entry.Title}" + (entry.Pinned ? " 📌" : ""), 16,
                    Color.white, UnityEngine.TextAnchor.UpperLeft);
                row.horizontalOverflow = HorizontalWrapMode.Wrap;
                row.rectTransform.anchorMin = row.rectTransform.anchorMax = new Vector2(0f, 1f);
                row.rectTransform.pivot = new Vector2(0f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(2f, -y);
                row.rectTransform.sizeDelta = new Vector2(w - 76f, 24f);

                var del = DshMobile.Ugui.Button("Del", _journalDayContent, "✕", 14, new Color(0.75f, 0.35f, 0.35f));
                del.GetComponent<RectTransform>().anchorMin = del.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
                del.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
                del.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4f, -y);
                del.GetComponent<RectTransform>().sizeDelta = new Vector2(34f, 24f);
                JournalEntry capturedEntry = entry;
                del.onClick.AddListener(() => { journal.Delete(capturedEntry); journal.Save(); _journalStatus = $"已删除 1 条（{PetJournal.FormatBytes(journal.StorageBytes)}）"; RebuildJournalDay(gm, w, h); });

                y += 26f;

                if (!string.IsNullOrEmpty(entry.Detail))
                {
                    var detail = DshMobile.Ugui.Text("Detail", _journalDayContent, "    " + entry.Detail, 14,
                        new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
                    detail.horizontalOverflow = HorizontalWrapMode.Wrap;
                    detail.rectTransform.anchorMin = detail.rectTransform.anchorMax = new Vector2(0f, 1f);
                    detail.rectTransform.pivot = new Vector2(0f, 1f);
                    detail.rectTransform.anchoredPosition = new Vector2(2f, -y);
                    detail.rectTransform.sizeDelta = new Vector2(w - 76f, 22f);
                    y += 24f;
                }
            }
        }

        private void OnDeleteDay()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            var journal = gm.Journal;
            var entries = journal.ForDay(_selectedDay);
            bool armed = _journalConfirmDay == _selectedDay.Date;
            if (armed)
            {
                int removed = journal.DeleteDay(_selectedDay);
                journal.Save();
                _journalConfirmDay = DateTime.MinValue;
                _journalStatus = $"已删除 {removed} 条（{PetJournal.FormatBytes(journal.StorageBytes)}）";
                _builtJournalDay = DateTime.MinValue;
            }
            else
            {
                _journalConfirmDay = _selectedDay.Date;
                _journalStatus = "再点一次确认删除这一天的记录";
            }
        }

        private void OnClearAllJournal()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            if (_journalConfirmAll)
            {
                gm.Journal.Clear();
                gm.Journal.Save();
                _journalConfirmAll = false;
                _journalStatus = "记事本已清空";
            }
            else
            {
                _journalConfirmAll = true;
                _journalConfirmMonth = false;
                _journalStatus = "再点一次确认清空全部记忆";
            }
            _builtJournalDay = DateTime.MinValue;
        }

        private void OnClearMonthJournal()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            if (_journalConfirmMonth)
            {
                int removed = gm.Journal.DeleteMonth(_journalMonth.Year, _journalMonth.Month);
                gm.Journal.Save();
                _journalConfirmMonth = false;
                _journalStatus = $"已清理 {removed} 条（{PetJournal.FormatBytes(gm.Journal.StorageBytes)}）";
            }
            else
            {
                _journalConfirmMonth = true;
                _journalConfirmAll = false;
                _journalStatus = $"再点一次确认清理 {_journalMonth:yyyy 年 M 月} 的记录";
            }
            _builtJournalDay = DateTime.MinValue;
        }

        // --------------------------------------------------------------- collection (uGUI)

        private void BuildCollectionPanel()
        {
            _collectionPanel = DshMobile.Ugui.Panel("CollectionPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _collectionPanel.gameObject.SetActive(false);
            var p = _collectionPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "宠物图鉴", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 200f, 32f);

            _collectionBalance = DshMobile.Ugui.Text("Balance", p, "", 20, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleRight, true);
            DshMobile.Ugui.SetRect(_collectionBalance.rectTransform, -280f, 14f, 200f, 32f);

            var tabs = new[] { CollectionTab.Shop, CollectionTab.Warehouse, CollectionTab.Backpack };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = DshMobile.Ugui.Button("Tab", p, PetCollectionPanel.TabLabel(tabs[i]), 16, new Color(0.30f, 0.40f, 0.58f));
                DshMobile.Ugui.SetRect(tab.GetComponent<RectTransform>(), 18f + i * 146f, 48f, 140f, 34f);
                _collectionTabLabels.Add(tab.GetComponentInChildren<UnityEngine.UI.Text>());
                CollectionTab captured = tabs[i];
                tab.onClick.AddListener(() => { PetCollectionPanel.Tab = captured; _builtCollectionTab = -1; });
            }

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -94f, 48f, 76f, 34f);
            close.onClick.AddListener(() => _showCollection = false);

            _collectionMessageText = DshMobile.Ugui.Text("Message", p, "", 14, new Color(0.7f, 0.95f, 0.75f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_collectionMessageText.rectTransform, 18f, 88f, 500f, 22f);

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            var scroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport.anchorMin = new Vector2(0f, 0f);
            scroll.viewport.anchorMax = new Vector2(1f, 1f);
            scroll.viewport.offsetMin = new Vector2(18f, 14f);
            scroll.viewport.offsetMax = new Vector2(-18f, -116f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _collectionContent = content.GetComponent<RectTransform>();
            _collectionContent.anchorMin = new Vector2(0f, 1f);
            _collectionContent.anchorMax = new Vector2(1f, 1f);
            _collectionContent.pivot = new Vector2(0.5f, 1f);
            scroll.content = _collectionContent;
        }

        private void SyncCollectionPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            bool open = _showCollection;
            _collectionPanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || _showSettings || _showJournal || gm.DoorPromptOpen);
            if (!open) return;

            var w = Mathf.Min(760f, DesignWidth - 48f);
            var h = Mathf.Min(560f, DesignHeight - 48f);
            _collectionPanel.rectTransform.anchorMin = _collectionPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _collectionPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _collectionPanel.rectTransform.anchoredPosition = Vector2.zero;
            _collectionPanel.rectTransform.sizeDelta = new Vector2(w, h);

            _collectionBalance.text = "宠物币 " + DshMobile.PetWallet.Coins.ToString("N0");
            _collectionMessageText.text = _collectionMessage;
            _collectionMessageText.color = _collectionError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);

            var tabs = new[] { CollectionTab.Shop, CollectionTab.Warehouse, CollectionTab.Backpack };
            for (int i = 0; i < _collectionTabLabels.Count; i++)
                _collectionTabLabels[i].color = PetCollectionPanel.Tab == tabs[i] ? new Color(1f, 0.92f, 0.7f) : Color.white;

            if (_builtCollectionTab != (int)PetCollectionPanel.Tab)
            {
                RebuildCollectionContent(gm, w);
                _builtCollectionTab = (int)PetCollectionPanel.Tab;
            }
        }

        private void RebuildCollectionContent(PetGameManager gm, float w)
        {
            for (int i = _collectionContent.childCount - 1; i >= 0; i--) Destroy(_collectionContent.GetChild(i).gameObject);
            float y = 4f;

            if (PetCollectionPanel.Tab == CollectionTab.Shop)
            {
                y = BuildGacha(_collectionContent, y, w);
                y += 12f;
                y = BuildCollectionLabel(_collectionContent, y, "直接领养");
                foreach (var species in PetCollectionPanel.ShopOrder())
                {
                    y = BuildShopSpecies(gm, _collectionContent, y, w, species);
                }
            }
            else
            {
                bool inBackpack = PetCollectionPanel.Tab == CollectionTab.Backpack;
                y = BuildCollectionLabel(_collectionContent, y, inBackpack
                    ? $"背包 {PetCollection.Backpack.Count}/{PetCollection.BackpackSlots}　在背包里的宠物会一起在房间里走动。"
                    : $"仓库里一共有 {PetCollection.Warehouse.Count} 只宠物，容量不限。");

                var pets = inBackpack ? RecordsIn(PetCollection.Backpack) : new List<PetRecord>(PetCollection.Warehouse);
                if (pets.Count == 0) y = BuildCollectionLabel(_collectionContent, y, "这里还没有宠物，去商城领一只吧。");
                foreach (var record in pets)
                {
                    y = BuildPetRecord(gm, _collectionContent, y, w, record, inBackpack);
                }
            }

            _collectionContent.sizeDelta = new Vector2(-4f, y + 12f);
        }

        private static float BuildCollectionLabel(RectTransform content, float y, string text)
        {
            var label = DshMobile.Ugui.Text("Label", content, text, 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(2f, -y);
            label.rectTransform.sizeDelta = new Vector2(-4f, 24f);
            return y + 26f;
        }

        private float BuildGacha(RectTransform content, float y, float w)
        {
            var title = DshMobile.Ugui.Text("GachaTitle", content, "🎰 扭蛋机", 20, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.UpperLeft, true);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(2f, -y);
            title.rectTransform.sizeDelta = new Vector2(200f, 28f);
            y += 30f;

            y = BuildCollectionLabel(content, y, "投 " + PetGacha.Cost + " 币，随机转出一只扭蛋专属宠物（不进直售）。");
            y = BuildCollectionLabel(content, y, "奖池：" + PetGacha.OddsText());

            bool afford = DshMobile.PetWallet.CanAfford(PetGacha.Cost);
            var roll = DshMobile.Ugui.Button("Roll", content,
                afford ? $"🎲 扭一次（¥{PetGacha.Cost}）" : $"还差 {PetGacha.Cost - DshMobile.PetWallet.Coins} 币",
                16, new Color(0.98f, 0.72f, 0.22f));
            roll.interactable = afford;
            roll.GetComponent<RectTransform>().anchorMin = roll.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            roll.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            roll.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f, -y);
            roll.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Min(w - 60f, 400f), 46f);
            roll.onClick.AddListener(() =>
            {
                string message;
                PetCollection.RollGacha(out message);
                SetCollectionMessage(message);
                DshMobile.MobileHaptics.Medium();
                _builtCollectionTab = -1;
            });
            return y + 54f;
        }

        private float BuildShopSpecies(PetGameManager gm, RectTransform content, float y, float w, PetSpecies species)
        {
            bool owned = PetCollection.IsSpeciesUnlocked(species.Id);
            int price = PetCollection.PriceFor(species.Id);
            bool afford = DshMobile.PetWallet.CanAfford(price);

            var row = new GameObject("Species", typeof(RectTransform));
            row.transform.SetParent(content, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 48f);

            var dot = DshMobile.Ugui.Image("Dot", row.transform, species.Fur);
            DshMobile.Ugui.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -2f), new Vector2(20f, 20f));

            var name = DshMobile.Ugui.Text("Name", row.transform, $"{species.DisplayName}　{species.Blurb}", 16, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 30f, 2f, w - 230f, 24f);

            var sub = DshMobile.Ugui.Text("Sub", row.transform, owned
                ? $"已拥有 {PetCollection.OwnedCount(species.Id)} 只　再买一只 {price} 币"
                : $"未解锁　{price} 币", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(sub.rectTransform, 30f, 26f, w - 230f, 20f);

            var buy = DshMobile.Ugui.Button("Buy", row.transform, afford ? $"领回家 {price}" : $"还差 {price - DshMobile.PetWallet.Coins}", 16,
                new Color(0.30f, 0.55f, 0.35f));
            buy.interactable = afford;
            buy.GetComponent<RectTransform>().anchorMin = buy.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            buy.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            buy.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            buy.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 38f);
            PetSpecies captured = species;
            buy.onClick.AddListener(() => { var r = PetCollectionPanel.Buy(captured.Id); SetCollectionMessage(r.Message, r.Error); _builtCollectionTab = -1; });

            return y + 54f;
        }

        private float BuildPetRecord(PetGameManager gm, RectTransform content, float y, float w, PetRecord record, bool inBackpack)
        {
            var species = PetSpecies.Get(record.SpeciesId);
            bool isPrimary = PetCollection.Primary != null && PetCollection.Primary.Id == record.Id;

            var row = new GameObject("Pet", typeof(RectTransform));
            row.transform.SetParent(content, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 84f);

            var dot = DshMobile.Ugui.Image("Dot", row.transform, species.Fur);
            DshMobile.Ugui.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -2f), new Vector2(20f, 20f));

            var name = DshMobile.Ugui.Text("Name", row.transform, $"{record.Name}　{species.DisplayName}", 16, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 30f, 2f, w - 340f, 24f);

            var role = DshMobile.Ugui.Text("Role", row.transform, $"{record.Personality.Archetype}　·　{PetCollectionPanel.RoleLabel(inBackpack, isPrimary)}", 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(role.rectTransform, 30f, 26f, w - 340f, 20f);

            var voice = DshMobile.Ugui.Text("Voice", row.transform, "音色：" + PetVoice.Describe(species, record.Personality), 14,
                new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(voice.rectTransform, 30f, 46f, w - 340f, 20f);

            var sell = DshMobile.Ugui.Button("Sell", row.transform, _armedSell == "sell:" + record.Id ? "真的卖掉？" : "卖掉", 14,
                new Color(0.75f, 0.35f, 0.35f));
            sell.interactable = !isPrimary;
            sell.GetComponent<RectTransform>().anchorMin = sell.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            sell.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            sell.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -2f);
            sell.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 30f);
            PetRecord captured = record;
            sell.onClick.AddListener(() =>
            {
                if (_armedSell == "sell:" + captured.Id)
                {
                    _armedSell = null;
                    var r = PetCollectionPanel.Sell(captured.Id);
                    SetCollectionMessage(r.Message, r.Error);
                    _builtCollectionTab = -1;
                }
                else
                {
                    _armedSell = "sell:" + captured.Id;
                    SetCollectionMessage($"再点一次就把{captured.Name}卖掉，能得到 {PetCollection.SellPriceFor(captured.SpeciesId)} 个宠物币（没法撤销）");
                }
            });

            var action = DshMobile.Ugui.Button("Action", row.transform, "", 14, new Color(0.30f, 0.40f, 0.58f));
            action.GetComponent<RectTransform>().anchorMin = action.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            action.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            action.GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, -2f);
            action.GetComponent<RectTransform>().sizeDelta = new Vector2(112f, 30f);
            var actionLabel = action.GetComponentInChildren<UnityEngine.UI.Text>();

            if (inBackpack)
            {
                if (isPrimary) { action.gameObject.SetActive(false); }
                else { actionLabel.text = "主要照顾"; action.onClick.AddListener(() => { var r = PetCollectionPanel.MakePrimary(captured.Id); SetCollectionMessage(r.Message, r.Error); _builtCollectionTab = -1; }); }
            }
            else
            {
                actionLabel.text = "放进背包";
                action.onClick.AddListener(() => { var r = PetCollectionPanel.PutInBackpack(captured.Id); SetCollectionMessage(r.Message, r.Error); _builtCollectionTab = -1; });
            }

            return y + 90f;
        }

        // ---------------------------------------------------------------- furnish (uGUI)

        private void BuildFurnishPanel()
        {
            _furnishPanel = DshMobile.Ugui.Panel("FurnishPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _furnishPanel.gameObject.SetActive(false);
            var p = _furnishPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "商城 · 仓库 · 背包", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 300f, 32f);

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -94f, 16f, 76f, 30f);
            close.onClick.AddListener(() => _showFurnish = false);

            string[] tabNames = { "商城", "背包", "仓库" };
            for (int i = 0; i < 3; i++)
            {
                var tab = DshMobile.Ugui.Button("Tab", p, tabNames[i], 16, new Color(0.30f, 0.40f, 0.58f));
                DshMobile.Ugui.SetRect(tab.GetComponent<RectTransform>(), 18f + i * 80f, 48f, 72f, 34f);
                _furnishTabLabels.Add(tab.GetComponentInChildren<UnityEngine.UI.Text>());
                int captured = i;
                tab.onClick.AddListener(() => { _furnishTab = captured; _builtFurnishTab = -1; });
            }

            _furnishInfo = DshMobile.Ugui.Text("Info", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_furnishInfo.rectTransform, 260f, 52f, 300f, 26f);

            _furnishMsg = DshMobile.Ugui.Text("Msg", p, "", 14, new Color(0.62f, 0.95f, 0.70f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_furnishMsg.rectTransform, 18f, 88f, 500f, 22f);

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            var scroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport.anchorMin = new Vector2(0f, 0f);
            scroll.viewport.anchorMax = new Vector2(1f, 1f);
            scroll.viewport.offsetMin = new Vector2(18f, 14f);
            scroll.viewport.offsetMax = new Vector2(-18f, -116f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _furnishContent = content.GetComponent<RectTransform>();
            _furnishContent.anchorMin = new Vector2(0f, 1f);
            _furnishContent.anchorMax = new Vector2(1f, 1f);
            _furnishContent.pivot = new Vector2(0.5f, 1f);
            scroll.content = _furnishContent;
        }

        private void SyncFurnishPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            bool open = _showFurnish;
            _furnishPanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || _showSettings || _showJournal || _showCollection || gm.DoorPromptOpen);
            if (!open) return;

            var w = Mathf.Min(560f, DesignWidth - 32f);
            var h = Mathf.Min(620f, DesignHeight - 32f);
            _furnishPanel.rectTransform.anchorMin = _furnishPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _furnishPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _furnishPanel.rectTransform.anchoredPosition = Vector2.zero;
            _furnishPanel.rectTransform.sizeDelta = new Vector2(w, h);

            _furnishInfo.text = "粮 " + PetInventory.Food + " · 背包 " + PetBackpack.Equipped().Count + "/" + PetBackpack.ToolSlots;
            _furnishMsg.text = _furnishMessage;
            _furnishMsg.color = (_furnishMessage.StartsWith("还差") || _furnishMessage.Contains("没有"))
                ? new Color(1f, 0.72f, 0.60f) : new Color(0.62f, 0.95f, 0.70f);

            for (int i = 0; i < _furnishTabLabels.Count; i++)
                _furnishTabLabels[i].color = _furnishTab == i ? new Color(1f, 0.92f, 0.7f) : Color.white;

            if (_builtFurnishTab != _furnishTab)
            {
                RebuildFurnishContent(gm, w);
                _builtFurnishTab = _furnishTab;
            }
        }

        private void RebuildFurnishContent(PetGameManager gm, float w)
        {
            for (int i = _furnishContent.childCount - 1; i >= 0; i--) Destroy(_furnishContent.GetChild(i).gameObject);
            float y = 4f;

            if (_furnishTab == 0) RebuildShopTab(w, ref y);
            else if (_furnishTab == 1) RebuildBackpackTab(gm, w, ref y);
            else RebuildWarehouseTab(gm, w, ref y);

            _furnishContent.sizeDelta = new Vector2(-4f, y + 12f);
        }

        private void RebuildShopTab(float w, ref float y)
        {
            y = BuildFurnishLabel(_furnishContent, y, "🎰 扭蛋机");
            y = BuildFurnishLabel(_furnishContent, y, "　投 " + PetGacha.Cost + " 币随机抽一只扭蛋专属宠物（小仓鼠 50% / 小熊猫 35% / 小企鹅 15%），不进直售。");

            bool afford = DshMobile.PetWallet.CanAfford(PetGacha.Cost);
            var roll = DshMobile.Ugui.Button("Roll", _furnishContent,
                afford ? $"🎲 扭一次（¥{PetGacha.Cost}）" : $"还差 {PetGacha.Cost - DshMobile.PetWallet.Coins} 币",
                16, new Color(0.98f, 0.72f, 0.22f));
            roll.interactable = afford;
            roll.GetComponent<RectTransform>().anchorMin = roll.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            roll.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            roll.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f, -y);
            roll.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Min(w - 60f, 400f), 44f);
            roll.onClick.AddListener(() => { string m; PetCollection.RollGacha(out m); _furnishMessage = m; DshMobile.MobileHaptics.Medium(); _builtFurnishTab = -1; });
            y += 52f;

            var sections = new[] { new { H = "🍖 食品", C = ShopCategory.Food }, new { H = "🧰 道具", C = ShopCategory.Tool }, new { H = "🛋️ 家具", C = ShopCategory.Furniture } };
            foreach (var section in sections)
            {
                y = BuildFurnishLabel(_furnishContent, y, section.H);
                foreach (var item in PetShop.All)
                {
                    if (item.Category != section.C) continue;
                    y = BuildShopItem(w, item, ref y);
                }
            }
        }

        private float BuildShopItem(float w, ShopItem item, ref float y)
        {
            if (item.Produced)
            {
                y = BuildFurnishLabel(_furnishContent, y, $"　{item.Emoji} {item.Name} —— 世界里获得（能卖 ¥{item.SellPrice}）");
                y = BuildFurnishLabel(_furnishContent, y, "　" + item.Blurb);
                return y;
            }

            var row = new GameObject("Item", typeof(RectTransform));
            row.transform.SetParent(_furnishContent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 36f);

            var name = DshMobile.Ugui.Text("Name", row.transform, $"{item.Emoji} {item.Name}", 16, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 2f, 4f, 150f, 28f);

            var price = DshMobile.Ugui.Text("Price", row.transform, "¥" + item.Price, 16, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(price.rectTransform, 158f, 4f, 70f, 28f);

            bool owned = item.IsFood ? false : PetInventory.IsOwned(item.Id);
            string buyLabel = item.IsFood ? (item.FoodUnits > 1 ? $"购买（+{item.FoodUnits}）" : "购买") : (owned ? "已拥有" : "购买");
            var buy = DshMobile.Ugui.Button("Buy", row.transform, buyLabel, 15, new Color(0.30f, 0.55f, 0.35f));
            buy.interactable = !owned;
            buy.GetComponent<RectTransform>().anchorMin = buy.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            buy.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            buy.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            buy.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 34f);
            ShopItem captured = item;
            buy.onClick.AddListener(() => { _furnishMessage = PetInventory.Buy(captured); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; });

            y += 38f;
            y = BuildFurnishLabel(_furnishContent, y, "　" + item.Blurb);
            if (item.IsFurniture && item.Scene != ItemScene.Anywhere)
            {
                bool here = item.AllowedIn(PetWorldMap.Current);
                y = BuildFurnishLabel(_furnishContent, y, here ? "　（这里可以摆放）" : "　（只能摆在" + PetInventory.PlaceName(item.Scene) + "）");
            }
            return y;
        }

        private void RebuildBackpackTab(PetGameManager gm, float w, ref float y)
        {
            y = BuildFurnishLabel(_furnishContent, y, "道具栏（" + PetBackpack.Equipped().Count + "/" + PetBackpack.ToolSlots + "）");
            var equipped = PetBackpack.Equipped();
            for (int i = 0; i < PetBackpack.ToolSlots; i++)
            {
                string id = i < equipped.Count ? equipped[i] : "";
                var item = string.IsNullOrEmpty(id) ? null : PetShop.Get(id);
                y = BuildEquipRow(w, i, item, ref y);
            }

            if (PetBackpack.BucketFull) y = BuildFurnishLabel(_furnishContent, y, "水桶里装满了水，去浇苹果树吧。");

            y = BuildFurnishLabel(_furnishContent, y, "没装备上的道具");
            bool anyTool = false;
            foreach (var item in PetShop.All)
            {
                if (!item.IsTool || !PetInventory.IsOwned(item.Id) || PetBackpack.IsEquipped(item.Id)) continue;
                anyTool = true;
                y = BuildSimpleActionRow(w, $"{item.Emoji} {item.Name}", "装备", () => { _furnishMessage = PetBackpack.Equip(item.Id); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; }, ref y);
            }
            if (!anyTool) y = BuildFurnishLabel(_furnishContent, y, "还没有没装备的道具。商城买水桶、铲子，再来这里装备。");

            y = BuildFurnishLabel(_furnishContent, y, "随身食物（点「投喂」直接喂给宠物）");
            bool anyFood = false;
            foreach (var item in PetShop.All)
            {
                if (!item.IsFood) continue;
                int n = PetInventory.Count(item.Id);
                if (n <= 0) continue;
                anyFood = true;
                y = BuildSimpleActionRow(w, $"{item.Emoji} {item.Name} ×{n}", "投喂", () => { if (gm != null) gm.FeedFromBackpack(item.Id); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; }, ref y);
            }
            if (!anyFood) y = BuildFurnishLabel(_furnishContent, y, "背包里还没有食物。商城买粮食、水、肉，或去花园摘苹果、钓鱼。");
            y = BuildFurnishLabel(_furnishContent, y, "宠物栏（随身 " + PetCollection.Backpack.Count + "/" + PetCollection.BackpackSlots + "）——宠物在「宠物」面板的背包页管理。");
            y = BuildFurnishLabel(_furnishContent, y, "仓库容量：" + PetInventory.WarehouseSlots + " 格。");
        }

        private float BuildEquipRow(float w, int slot, ShopItem item, ref float y)
        {
            var row = new GameObject("Slot", typeof(RectTransform));
            row.transform.SetParent(_furnishContent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 30f);

            var label = DshMobile.Ugui.Text("Label", row.transform, "第 " + (slot + 1) + " 格：" + (item != null ? $"{item.Emoji} {item.Name}" : "空"), 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(label.rectTransform, 2f, 2f, 250f, 26f);

            if (item != null)
            {
                var unequip = DshMobile.Ugui.Button("Unequip", row.transform, "取下", 14, new Color(0.75f, 0.35f, 0.35f));
                unequip.GetComponent<RectTransform>().anchorMin = unequip.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
                unequip.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
                unequip.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
                unequip.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 28f);
                ShopItem captured = item;
                unequip.onClick.AddListener(() => { _furnishMessage = PetBackpack.Unequip(captured.Id); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; });
            }
            return y + 34f;
        }

        private void RebuildWarehouseTab(PetGameManager gm, float w, ref float y)
        {
            var currentPlace = PetWorldMap.Current;
            y = BuildFurnishLabel(_furnishContent, y, "粮食 " + PetInventory.Food + " 顿　·　苹果 " + PetInventory.Count("apple") + " 个　·　鱼 " +
                PetInventory.Count("fish") + " 条　·　水 " + PetInventory.Count("water") + " 瓶　·　肉 " + PetInventory.Count("meat") + " 份");

            foreach (var item in PetShop.All)
            {
                if (!item.IsFood) continue;
                int n = PetInventory.Count(item.Id);
                if (n <= 0) continue;
                y = BuildFeedSellRow(w, $"{item.Emoji} {item.Name} ×{n}", item, gm, ref y);
            }

            y = BuildFurnishLabel(_furnishContent, y, "摆在这里（" + RoomThemeInfo.Get(currentPlace).DisplayName + "）");
            var placed = PetInventory.Placed(currentPlace);
            bool anyHere = false;
            foreach (var id in placed.Keys)
            {
                var item = PetShop.Get(id);
                if (item == null) continue;
                anyHere = true;
                if (!PetShop.IsStarter(id))
                {
                    y = BuildSimpleActionRow(w, $"{item.Emoji} {item.Name}", "收回仓库", () => { _furnishMessage = PetInventory.Store(id, currentPlace); DshMobile.MobileHaptics.Light(); gm.RebuildRoom(); _builtFurnishTab = -1; }, ref y);
                }
                else
                {
                    y = BuildFurnishLabel(_furnishContent, y, $"{item.Emoji} {item.Name}");
                }
            }
            if (!anyHere) y = BuildFurnishLabel(_furnishContent, y, "这里还空着，去仓库把家具摆出来。");

            y = BuildFurnishLabel(_furnishContent, y, "在仓库里（还没摆进这个场景）");
            bool any = false;
            foreach (var item in PetShop.All)
            {
                if (!item.IsFurniture || !PetInventory.IsOwned(item.Id) || placed.ContainsKey(item.Id)) continue;
                any = true;
                bool allowed = item.AllowedIn(currentPlace);
                y = BuildFurnishPlaceSellRow(w, item, allowed, gm, ref y);
                if (!allowed) y = BuildFurnishLabel(_furnishContent, y, "　只能摆在" + PetInventory.PlaceName(item.Scene) + "。");
            }
            if (!any) y = BuildFurnishLabel(_furnishContent, y, "仓库空空的。去商城看看，家具买回来才能摆进房间。");
        }

        private float BuildFeedSellRow(float w, string label, ShopItem item, PetGameManager gm, ref float y)
        {
            var row = new GameObject("Food", typeof(RectTransform));
            row.transform.SetParent(_furnishContent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 30f);

            var name = DshMobile.Ugui.Text("Name", row.transform, label, 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 2f, 2f, 170f, 26f);

            var feed = DshMobile.Ugui.Button("Feed", row.transform, "投喂", 14, new Color(0.30f, 0.55f, 0.35f));
            feed.GetComponent<RectTransform>().anchorMin = feed.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            feed.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            feed.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110f, 0f);
            feed.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 28f);
            ShopItem captured = item;
            feed.onClick.AddListener(() => { gm.FeedFromBackpack(captured.Id); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; });

            var sell = DshMobile.Ugui.Button("Sell", row.transform, "卖 ¥" + item.SellPrice, 14, new Color(0.75f, 0.35f, 0.35f));
            sell.GetComponent<RectTransform>().anchorMin = sell.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            sell.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            sell.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            sell.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 28f);
            sell.onClick.AddListener(() => { _furnishMessage = PetInventory.Sell(captured.Id); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; });
            return y + 34f;
        }

        private float BuildFurnishPlaceSellRow(float w, ShopItem item, bool allowed, PetGameManager gm, ref float y)
        {
            var row = new GameObject("Furniture", typeof(RectTransform));
            row.transform.SetParent(_furnishContent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 30f);

            var name = DshMobile.Ugui.Text("Name", row.transform, $"{item.Emoji} {item.Name}", 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 2f, 2f, 150f, 26f);

            var place = DshMobile.Ugui.Button("Place", row.transform, "摆放", 14, new Color(0.30f, 0.55f, 0.35f));
            place.interactable = allowed;
            place.GetComponent<RectTransform>().anchorMin = place.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            place.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            place.GetComponent<RectTransform>().anchoredPosition = new Vector2(-120f, 0f);
            place.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 30f);
            ShopItem captured = item;
            place.onClick.AddListener(() => { _furnishMessage = PetInventory.Place(captured.Id, PetWorldMap.Current); DshMobile.MobileHaptics.Light(); gm.RebuildRoom(); _builtFurnishTab = -1; });

            var sell = DshMobile.Ugui.Button("Sell", row.transform, "卖掉 ¥" + item.SellPrice, 14, new Color(0.75f, 0.35f, 0.35f));
            sell.GetComponent<RectTransform>().anchorMin = sell.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            sell.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            sell.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            sell.GetComponent<RectTransform>().sizeDelta = new Vector2(108f, 30f);
            sell.onClick.AddListener(() => { _furnishMessage = PetInventory.Sell(captured.Id); DshMobile.MobileHaptics.Light(); _builtFurnishTab = -1; });
            return y + 34f;
        }

        private float BuildSimpleActionRow(float w, string label, string action, UnityEngine.Events.UnityAction onClick, ref float y)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(_furnishContent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -y);
            rowRt.sizeDelta = new Vector2(0f, 30f);

            var name = DshMobile.Ugui.Text("Name", row.transform, label, 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 2f, 2f, 250f, 26f);

            var btn = DshMobile.Ugui.Button("Action", row.transform, action, 14, new Color(0.30f, 0.40f, 0.58f));
            btn.GetComponent<RectTransform>().anchorMin = btn.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            btn.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 28f);
            btn.onClick.AddListener(onClick);
            return y + 34f;
        }

        private static float BuildFurnishLabel(RectTransform content, float y, string text)
        {
            var label = DshMobile.Ugui.Text("Label", content, text, 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(2f, -y);
            label.rectTransform.sizeDelta = new Vector2(-4f, 24f);
            return y + 26f;
        }

        // ------------------------------------------------------------ prompt preview (uGUI)

        private void BuildPromptPanel()
        {
            _promptPanel = DshMobile.Ugui.Panel("PromptPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _promptPanel.gameObject.SetActive(false);
            var p = _promptPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "发给模型的实际提示词", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 400f, 28f);

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(p, false);
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();
            var viewportImg = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;
            var scroll = p.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport.anchorMin = new Vector2(0f, 0f);
            scroll.viewport.anchorMax = new Vector2(1f, 1f);
            scroll.viewport.offsetMin = new Vector2(16f, 150f);
            scroll.viewport.offsetMax = new Vector2(-16f, -52f);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            scroll.content = contentRt;

            _promptText = DshMobile.Ugui.Text("Preview", content.transform, "", 12, Color.white, UnityEngine.TextAnchor.UpperLeft);
            _promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _promptText.verticalOverflow = VerticalWrapMode.Overflow;
            DshMobile.Ugui.Stretch(_promptText.rectTransform);
            _promptText.rectTransform.offsetMin = new Vector2(2f, 0f);
            _promptText.rectTransform.offsetMax = new Vector2(-2f, 0f);

            var editLabel = DshMobile.Ugui.Text("EditLabel", p, "额外要求（追加在系统提示后面，会保存）", 15, Color.white, UnityEngine.TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(editLabel.rectTransform, 16f, -120f, 500f, 22f);

            _promptEdit = DshMobile.Ugui.InputField("Edit", p);
            _promptEdit.lineType = UnityEngine.UI.InputField.LineType.MultiLineNewline;
            _promptEdit.GetComponent<RectTransform>().anchorMin = _promptEdit.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            _promptEdit.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            _promptEdit.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, -96f);
            _promptEdit.GetComponent<RectTransform>().sizeDelta = new Vector2(-32f, 60f);

            var save = DshMobile.Ugui.Button("Save", p, "保存额外要求", 16, new Color(0.30f, 0.55f, 0.35f));
            DshMobile.Ugui.SetRect(save.GetComponent<RectTransform>(), 16f, -44f, 150f, 36f);
            _promptSaveLabel = save.GetComponentInChildren<UnityEngine.UI.Text>();
            save.onClick.AddListener(OnSavePrompt);

            var copy = DshMobile.Ugui.Button("Copy", p, "复制全部提示词", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(copy.GetComponent<RectTransform>(), 176f, -44f, 150f, 36f);
            copy.onClick.AddListener(OnCopyPrompt);

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 16, new Color(0.75f, 0.35f, 0.35f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -166f, -44f, 150f, 36f);
            close.onClick.AddListener(() => _showPromptPreview = false);
        }

        private void SyncPromptPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            bool open = _showPromptPreview;
            _promptPanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || _showSettings || _showJournal || _showCollection || _showFurnish || gm.DoorPromptOpen);
            if (!open) return;

            var w = Mathf.Min(700f, DesignWidth - 32f);
            var h = Mathf.Min(540f, DesignHeight - 32f);
            _promptPanel.rectTransform.anchorMin = _promptPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _promptPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _promptPanel.rectTransform.anchoredPosition = Vector2.zero;
            _promptPanel.rectTransform.sizeDelta = new Vector2(w, h);

            _promptText.text = gm.PreviewSystemPrompt();
            _promptText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(200f, _promptText.preferredHeight + 12f));

            if (!_promptEdit.isFocused) _promptEdit.text = _editExtraInstructions ?? "";
            if (_promptEdit.isFocused) _editExtraInstructions = _promptEdit.text;

            bool dirty = (gm.BrainConfig.ExtraInstructions ?? "") != (_editExtraInstructions ?? "");
            _promptSaveLabel.text = dirty ? "保存额外要求 *" : "保存额外要求";
            _promptSaveLabel.color = dirty ? new Color(1f, 0.9f, 0.6f) : Color.white;
        }

        private void OnSavePrompt()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            gm.BrainConfig.ExtraInstructions = _editExtraInstructions ?? "";
            gm.BrainConfig.Save();
            gm.RebuildBrain();
        }

        private void OnCopyPrompt()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            string preview = gm.PreviewSystemPrompt();
            GUIUtility.systemCopyBuffer = preview +
                (string.IsNullOrWhiteSpace(_editExtraInstructions) ? "" : "\n\n【主人的额外要求】\n" + _editExtraInstructions.Trim());
        }

        // ------------------------------------------------------------- memory match (uGUI)

        private void BuildMemoryPanel()
        {
            _memoryPanel = DshMobile.Ugui.Panel("MemoryPanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _memoryPanel.gameObject.SetActive(false);
            var p = _memoryPanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "记忆配对", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 200f, 32f);

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -94f, 16f, 76f, 30f);
            close.onClick.AddListener(() => { _showMemory = false; SyncMusic(); });

            _memoryStats = DshMobile.Ugui.Text("Stats", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_memoryStats.rectTransform, 18f, 50f, 600f, 22f);

            for (int i = 0; i < 3; i++)
            {
                var level = PetMemoryMatch.ClampDifficulty(i);
                var btn = DshMobile.Ugui.Button("Level", p, "", 14, new Color(0.24f, 0.26f, 0.34f));
                DshMobile.Ugui.SetRect(btn.GetComponent<RectTransform>(), 18f + i * 170f, 76f, 162f, 34f);
                _memoryLevelLabels.Add(btn.GetComponentInChildren<UnityEngine.UI.Text>());
                MemoryDifficulty captured = level;
                btn.onClick.AddListener(() => { if (_memoryLevel != captured) { SetMemoryLevel(PetGameManager.Instance, captured); PetAudioDirector.Instance?.Play(SfxId.UiClick); _builtMemoryBoardCount = -1; } });
            }

            _memoryMsg = DshMobile.Ugui.Text("Msg", p, "", 14, new Color(0.85f, 0.88f, 0.95f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_memoryMsg.rectTransform, 18f, 116f, 600f, 22f);

            _memoryBoard = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
            _memoryBoard.SetParent(p, false);

            var restart = DshMobile.Ugui.Button("Restart", p, "重开一局", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(restart.GetComponent<RectTransform>(), 18f, -44f, 150f, 34f);
            restart.onClick.AddListener(() => { DealMemoryBoard(PetGameManager.Instance, $"重新洗牌了。{PetMemoryMatch.Describe(_memoryLevel)}"); _builtMemoryBoardCount = -1; });

            _memoryFooter = DshMobile.Ugui.Text("Footer", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleRight);
            DshMobile.Ugui.SetRect(_memoryFooter.rectTransform, -170f, -40f, 160f, 22f);
        }

        private void SyncMemoryPanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            if (_memory == null && _showMemory) OpenMemoryMatch(gm);
            bool open = _showMemory && _memory != null;
            _memoryPanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || _showSettings || _showJournal || _showCollection || _showFurnish || _showPromptPreview || gm.DoorPromptOpen);
            if (!open) return;

            var w = Mathf.Min(640f, DesignWidth - 32f);
            var h = Mathf.Min(700f, DesignHeight - 32f);
            _memoryPanel.rectTransform.anchorMin = _memoryPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _memoryPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _memoryPanel.rectTransform.anchoredPosition = Vector2.zero;
            _memoryPanel.rectTransform.sizeDelta = new Vector2(w, h);

            _memoryStats.text = $"{(int)(Time.realtimeSinceStartup - _memoryOpenedAt)} 秒　·　{_memory.Moves} 步　·　消掉 {_memory.Matched}/{_memory.Pairs} 对　·　{PetMemoryMatch.NameOf(_memory.Difficulty)}";
            _memoryMsg.text = _memoryMessage;
            _memoryMsg.color = _memory.IsSolved ? new Color(0.7f, 0.95f, 0.75f) : new Color(0.85f, 0.88f, 0.95f);
            _memoryFooter.text = $"玩成 {_memoryWins} 次　·　最少 {(_memoryBestMoves > 0 ? _memoryBestMoves.ToString() : "-")} 步";

            for (int i = 0; i < _memoryLevelLabels.Count; i++)
            {
                var level = PetMemoryMatch.ClampDifficulty(i);
                bool active = level == _memoryLevel;
                _memoryLevelLabels[i].text = $"{PetMemoryMatch.NameOf(level)}　{PetMemoryMatch.BaseRewardFor(level)} 币";
                _memoryLevelLabels[i].color = active ? new Color(1f, 0.92f, 0.7f) : Color.white;
                _memoryLevelLabels[i].transform.parent.GetComponent<UnityEngine.UI.Image>().color = active
                    ? new Color(0.36f, 0.66f, 0.44f, 0.6f) : new Color(0.24f, 0.26f, 0.34f, 0.6f);
            }

            if (_memory.WaitingToHide && Time.realtimeSinceStartup - _memoryFlipAt > 0.85f) _memory.HideMismatch();

            if (_builtMemoryBoardCount != _memory.Count)
            {
                RebuildMemoryBoard(w);
                _builtMemoryBoardCount = _memory.Count;
            }
            SyncMemoryCards(w);
        }

        private void RebuildMemoryBoard(float w)
        {
            for (int i = _memoryBoard.childCount - 1; i >= 0; i--) Destroy(_memoryBoard.GetChild(i).gameObject);
            _memoryCards.Clear();
            _memoryCardBgs.Clear();
            _memoryCardFaces.Clear();

            int columns = PetMemoryMatch.Columns;
            int rows = Mathf.CeilToInt(_memory.Count / (float)columns);
            float cell = Mathf.Max(52f, Mathf.Min((w - 36f - (columns - 1) * 8f) / columns, (380f - (rows - 1) * 8f) / Mathf.Max(1, rows)));

            for (int i = 0; i < _memory.Count; i++)
            {
                int row = i / columns, col = i % columns;
                var card = DshMobile.Ugui.Button("Card", _memoryBoard, "", 14, new Color(0.28f, 0.36f, 0.55f));
                float x = 18f + col * (cell + 8f) + cell * 0.5f;
                float y = 150f + row * (cell + 8f) + cell * 0.5f;
                DshMobile.Ugui.Place(card.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, -y), new Vector2(cell, cell));
                var bg = card.GetComponent<UnityEngine.UI.Image>();
                bg.color = new Color(0.28f, 0.36f, 0.55f, 1f);

                var face = DshMobile.Ugui.RawImage("Face", card.transform, null, Color.white);
                DshMobile.Ugui.Stretch(face.rectTransform);
                face.rectTransform.offsetMin = new Vector2(cell * 0.12f, cell * 0.12f);
                face.rectTransform.offsetMax = new Vector2(-cell * 0.12f, -cell * 0.12f);

                int index = i;
                card.onClick.AddListener(() => OnFlipCard(index));
                _memoryCards.Add(card);
                _memoryCardBgs.Add(bg);
                _memoryCardFaces.Add(face);
            }
        }

        private void OnFlipCard(int index)
        {
            var gm = PetGameManager.Instance;
            if (gm == null || _memory == null) return;
            if (_memory.IsSolved || !_memory.CanFlip(index)) return;
            if (_memory.Flip(index))
            {
                _memoryFlipAt = Time.realtimeSinceStartup;
                PetAudioDirector.Instance?.Play(SfxId.PickUp);
                DshMobile.MobileHaptics.Light();
                if (_memory.LastFlipMatched) FinishMemoryPair(gm);
            }
        }

        private void SyncMemoryCards(float w)
        {
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _memoryCards.Count; i++)
            {
                bool taken = _memory.IsTaken(i);
                var bg = _memoryCardBgs[i];
                var face = _memoryCardFaces[i];
                var btn = _memoryCards[i];

                if (taken && !_memoryClearedSeen[i] && i < _memoryClearedSeen.Length)
                {
                    _memoryClearedSeen[i] = true;
                    _memoryClearedAt[i] = now;
                }

                if (taken)
                {
                    float age = i < _memoryClearedAt.Length ? now - _memoryClearedAt[i] : 1f;
                    float fade = Mathf.Clamp01(age / MemoryClearSeconds);
                    btn.interactable = false;
                    if (fade >= 1f)
                    {
                        bg.color = new Color(1f, 1f, 1f, 0.035f);
                        face.gameObject.SetActive(false);
                        continue;
                    }
                    bg.color = new Color(0.42f, 0.78f, 0.48f, 0.75f * (1f - fade));
                    face.texture = PetAvatarArt.TextureFor(_memory.FaceAt(i));
                    face.gameObject.SetActive(true);
                    face.color = new Color(1f, 1f, 1f, 1f - fade);
                    continue;
                }

                btn.interactable = !_memory.IsSolved && _memory.CanFlip(i);
                if (_memory.IsFaceUp(i))
                {
                    bg.color = new Color(0.98f, 0.94f, 0.86f, 1f);
                    face.texture = PetAvatarArt.TextureFor(_memory.FaceAt(i));
                    face.gameObject.SetActive(true);
                    face.color = Color.white;
                }
                else
                {
                    bg.color = new Color(0.28f, 0.36f, 0.55f, 1f);
                    face.gameObject.SetActive(false);
                }
            }
        }

        // ------------------------------------------------------------------ puzzle (uGUI)

        private void BuildPuzzlePanel()
        {
            _puzzlePanel = DshMobile.Ugui.Panel("PuzzlePanel", _root, 18f,
                new Color(0.11f, 0.10f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.14f), 2f);
            _puzzlePanel.gameObject.SetActive(false);
            var p = _puzzlePanel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "拼图", 24, new Color(1f, 0.94f, 0.82f), UnityEngine.TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 14f, 200f, 32f);

            var close = DshMobile.Ugui.Button("Close", p, "关闭", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(close.GetComponent<RectTransform>(), -94f, 16f, 76f, 30f);
            close.onClick.AddListener(() => { _showPuzzle = false; SyncMusic(); });

            _puzzleStatus = DshMobile.Ugui.Text("Status", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_puzzleStatus.rectTransform, 18f, 50f, 600f, 22f);

            _puzzleMsg = DshMobile.Ugui.Text("Msg", p, "", 14, new Color(0.7f, 0.95f, 0.75f), UnityEngine.TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_puzzleMsg.rectTransform, 18f, 74f, 600f, 22f);

            _puzzleBoard = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
            _puzzleBoard.SetParent(p, false);

            for (int i = 0; i < PetPuzzle.TileCount; i++)
            {
                var tile = DshMobile.Ugui.RawImage("Tile", _puzzleBoard, null, Color.white);
                tile.gameObject.AddComponent<UnityEngine.UI.Button>();
                var btn = tile.GetComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = tile;
                var number = DshMobile.Ugui.Text("Num", tile.transform, "", 14, new Color(1f, 1f, 1f, 0.72f), UnityEngine.TextAnchor.UpperLeft);
                DshMobile.Ugui.Stretch(number.rectTransform);
                number.rectTransform.offsetMin = new Vector2(5f, 5f);
                number.rectTransform.offsetMax = new Vector2(-5f, -5f);
                int index = i;
                btn.onClick.AddListener(() => OnSlideTile(index));
                _puzzleTiles.Add(tile);
                _puzzleNumbers.Add(number);
                _puzzleButtons.Add(btn);
            }

            _puzzlePreview = DshMobile.Ugui.RawImage("Preview", p, null, Color.white);
            _puzzlePreview.rectTransform.anchorMin = _puzzlePreview.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _puzzlePreview.rectTransform.pivot = new Vector2(1f, 0.5f);
            _puzzlePreview.rectTransform.anchoredPosition = new Vector2(-12f, -20f);
            _puzzlePreview.rectTransform.sizeDelta = new Vector2(72f, 72f);
            _puzzlePreview.gameObject.SetActive(false);

            var restart = DshMobile.Ugui.Button("Restart", p, "打乱重来", 16, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(restart.GetComponent<RectTransform>(), 18f, -44f, 150f, 34f);
            restart.onClick.AddListener(OnShufflePuzzle);

            var toggle = DshMobile.Ugui.Button("Toggle", p, "看原图", 14, new Color(0.30f, 0.40f, 0.58f));
            DshMobile.Ugui.SetRect(toggle.GetComponent<RectTransform>(), 178f, -44f, 90f, 34f);
            _puzzleToggleLabel = toggle.GetComponentInChildren<UnityEngine.UI.Text>();
            toggle.onClick.AddListener(() => _puzzleShowFull = !_puzzleShowFull);

            _puzzleFooter = DshMobile.Ugui.Text("Footer", p, "", 14, new Color(0.86f, 0.87f, 0.91f), UnityEngine.TextAnchor.MiddleRight);
            DshMobile.Ugui.SetRect(_puzzleFooter.rectTransform, -170f, -40f, 160f, 22f);
        }

        private void SyncPuzzlePanel()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;
            if ((_puzzle == null || _puzzleArt == null) && _showPuzzle) OpenPuzzle(gm);
            bool open = _showPuzzle && _puzzle != null && _puzzleArt != null;
            _puzzlePanel.gameObject.SetActive(open);
            _modalScrim.gameObject.SetActive(open || _showSettings || _showJournal || _showCollection || _showFurnish || _showPromptPreview || _showMemory || gm.DoorPromptOpen);
            if (!open) return;

            var w = Mathf.Min(680f, DesignWidth - 32f);
            var h = Mathf.Min(700f, DesignHeight - 32f);
            _puzzlePanel.rectTransform.anchorMin = _puzzlePanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _puzzlePanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _puzzlePanel.rectTransform.anchoredPosition = Vector2.zero;
            _puzzlePanel.rectTransform.sizeDelta = new Vector2(w, h);

            float par = PetPuzzle.Par;
            _puzzleStatus.text = _puzzle.IsSolved
                ? $"拼好了！用了 {_puzzle.Moves} 步（参考 {par:F0} 步）"
                : $"{_puzzle.Moves} 步　·　参考 {par:F0} 步　·　{(int)(Time.realtimeSinceStartup - _puzzleStartedAt)} 秒";
            _puzzleMsg.text = _puzzleMessage;
            _puzzleMsg.color = _puzzleError ? new Color(1f, 0.65f, 0.55f) : new Color(0.7f, 0.95f, 0.75f);
            _puzzleFooter.text = $"拼好 {_puzzleWins} 次　·　最少 {(_puzzleBestMoves > 0 ? _puzzleBestMoves.ToString() : "-")} 步";
            _puzzleToggleLabel.text = _puzzleShowFull ? "关原图" : "看原图";
            _puzzlePreview.gameObject.SetActive(_puzzleShowFull);
            if (_puzzleShowFull) _puzzlePreview.texture = _puzzleArt;

            float side = Mathf.Min((w - 36f) * 0.72f, 400f);
            float cell = side / PetPuzzle.Size;
            for (int i = 0; i < _puzzleTiles.Count; i++)
            {
                int row = i / PetPuzzle.Size, col = i % PetPuzzle.Size;
                var tile = _puzzleTiles[i];
                tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = new Vector2(0f, 1f);
                tile.rectTransform.pivot = new Vector2(0f, 1f);
                tile.rectTransform.anchoredPosition = new Vector2(18f + (w - 36f - side) * 0.5f + col * cell + 1f, -(104f + row * cell + 1f));
                tile.rectTransform.sizeDelta = new Vector2(cell - 2f, cell - 2f);

                int value = _puzzle[i];
                if (value == PetPuzzle.Empty)
                {
                    tile.color = new Color(0f, 0f, 0f, 0.45f);
                    tile.texture = null;
                    _puzzleNumbers[i].text = "";
                    _puzzleButtons[i].interactable = false;
                    continue;
                }

                tile.texture = _puzzleArt;
                tile.color = Color.white;
                tile.uvRect = PuzzleArt.TileUv(value - 1);
                _puzzleNumbers[i].text = value.ToString();
                _puzzleButtons[i].interactable = !_puzzle.IsSolved && _puzzle.CanSlide(i);
            }
        }

        private void OnSlideTile(int index)
        {
            var gm = PetGameManager.Instance;
            if (gm == null || _puzzle == null) return;
            if (_puzzle.IsSolved || !_puzzle.CanSlide(index)) return;
            if (_puzzle.TrySlide(index))
            {
                PetAudioDirector.Instance?.Play(SfxId.PickUp);
                DshMobile.MobileHaptics.Light();
                if (_puzzle.IsSolved) FinishPuzzle(gm);
            }
        }

        private void OnShufflePuzzle()
        {
            _puzzle = PetPuzzle.Start(UnityEngine.Random.Range(0, 1 << 30));
            _puzzlePaid = false;
            _puzzleStartedAt = Time.realtimeSinceStartup;
            SetPuzzleMessage("重新打乱了，慢慢来。");
        }

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
                    hud._showCollection || hud._showPuzzle || hud._showMemory ||
                    hud._showFurnish || hud._placeMode) return true;
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
            else if (label == "家具")
            {
                _showFurnish = !_showFurnish;
                if (_showFurnish) _furnishMessage = "";
            }
            else if (label.Contains("展开") || label.Contains("收起")) gm.SelectPet(gm.SelectedPetIndex, toggleIfSame: true);
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
                    ? new[] { "收起", "本子", "宠物", "家具", "地图", "设置", "提示词", detailLabel, "重置" }
                    : new[] { "收起", "记事本", "宠物", "家具", "地图", "设置", "提示词", "重置" });

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

                // The whole bar is the target, not only the pill inside it. Registered before the
                // pill and the microphone so those keep their own, smaller presses — overlapping
                // targets resolve to the smallest area — while a thumb that lands on the pet's
                // line or in the gap beside it is asking the same question and now gets an answer
                // instead of a dead strip. This is registered, not an Input check, so it also
                // works when the raw-pointer fallback in MobileWidgets is the only path alive.
                DshMobile.MobileTouch.RegisterButton(MobileButtonIds.PetChatBar,
                    MobileWidgets.ToScreen(inner), true, "和它说说话");

                // The microphone belongs here, in the bar, where the thumb already is: a separate
                // corner button would be a second way to do the same thing, and the first version
                // put it to the left of the input *outside* the panel, where it was simply not on
                // screen. Space is reserved for it instead of hoped for.
                bool micHere = DshMobile.MobileStt.Offered;
                DshMobile.MobileStt.Tick();
                float micSize = MobileUi.Touchable(64f);
                var micRect = new Rect(inner.x, inner.y + (inner.height - micSize) * 0.5f, micSize, micSize);

                if (micHere)
                {
                    bool listening = DshMobile.MobileStt.Listening;
                    string label = listening ? "停" : (DshMobile.MobileStt.PendingPermission ? "等" : "说");
                    var tint = listening
                        ? new Color(0.90f, 0.42f, 0.36f)
                        : (DshMobile.MobileStt.PendingPermission
                            ? new Color(0.82f, 0.66f, 0.30f)
                            : new Color(0.34f, 0.60f, 0.86f));

                    if (MobileWidgets.CircleButton(MobileButtonIds.PetMic, micRect, label, tint))
                    {
                        // Speaking is a conversation, so tapping the microphone opens the
                        // transcript as well: the recognised words have to land somewhere visible,
                        // and so does the reason when nothing is recognised at all.
                        _chatExpanded = true;
                        FillEditConfig(gm);
                        if (listening) DshMobile.MobileStt.StopListening();
                        else StartListeningAndSay(gm);
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

                bool chatting = MobileWidgets.Button(MobileButtonIds.PetChat, sayRect, "和它说说话",
                    new Color(0.30f, 0.60f, 0.86f));

                if (!chatting && (DshMobile.MobileTouch.Pressed(MobileButtonIds.PetChatBar)
                    || MobileWidgets.PointerPressedInside(inner)))
                {
                    chatting = true;
                }

                if (chatting)
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

            // Expanded: the real transcript is now uGUI (see SyncChat). Just mark the unread
            // messages as read so the collapsed bar's dot clears.
            _unreadMark = gm.Memory.Recent.Count;
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

        /// <summary>
        /// Height of the collapsed chat bar on a phone.
        ///
        /// This is the one control the whole game hangs on — the room, the pet and the conversation
        /// are the game — so it is sized to be a target rather than a strip: 80 design pixels is
        /// about 40dp on a phone, which is the smallest a thumb reliably hits without looking, and
        /// it is tall enough to hold the microphone at the same size as the round action buttons.
        /// </summary>
        public const float MobileChatBarHeight = 80f;

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

            // Sit above the collapsed chat bar, not on it — in BOTH orientations.
            //
            // This used to ask `ChatOnSide`, on the theory that a side panel cannot be in the way of
            // the buttons. It can: the transcript only moves to the side once it is *expanded*, and
            // while it is collapsed the bar spans the full width of the bottom edge — so on a phone
            // held sideways the action button was drawn straight on top of the bar, and the bar
            // (a much bigger target) took the tap. The bar is the one thing that is always there.
            float bottom = layout.Chat.yMax - MobileChatBarHeight - 10f;

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

            // The 收起 button is laid out HERE, before the header, so the header can be narrowed by
            // exactly its width. It used to be drawn last at the panel's top-right corner, on top of
            // whatever the header happened to put there — the pet's name, its mood, and the
            // "正在想…" indicator all ran underneath it.
            var close = new Rect(rect.xMax - MobileUi.Touchable(88f) - 14f, rect.y + 14f,
                MobileUi.Touchable(88f), MobileUi.Touchable(34f));

            float headerHeight = 46f;
            var header = new Rect(rect.x + 18f, rect.y + 12f,
                Mathf.Max(80f, rect.width - 36f - close.width - 10f), headerHeight);
            DrawChatHeader(gm, header);
            UiSkin.Fill(new Rect(rect.x + 14f, header.yMax + 4f, rect.width - 28f, 1f),
                new Color(1f, 1f, 1f, 0.09f));

            // --- transcript ---
            // The phone footer is two rows plus a status line: a thumb needs bigger targets than a
            // mouse, and the voice/TTS state has to be readable *here* rather than only in settings.
            float footerHeight = Mobile ? 138f : 96f;
            var logRect = new Rect(rect.x + 8f, header.yMax + 10f, rect.width - 16f,
                Mathf.Max(60f, rect.yMax - footerHeight - header.yMax - 16f));

            DrawTranscript(gm, logRect);

            // --- input row + quick actions ---
            var footer = new Rect(rect.x + 16f, rect.yMax - footerHeight + 8f, rect.width - 32f,
                footerHeight - 8f);
            if (Mobile) DrawMobileChatFooter(gm, footer);
            else DrawChatFooter(gm, footer);

            if (MobileWidgets.Button("pet.chatclose", close, "收起", new Color(0.75f, 0.35f, 0.35f)))
            {
                _chatExpanded = false;
                GUI.FocusControl(null);
            }
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

        /// <summary>
        /// The phone's chat footer: speak, type, send — and the two switches that used to be
        /// findable only in the settings panel.
        ///
        /// Three things are different from the desktop row, and each is a fix rather than a taste:
        ///
        ///  · <b>The voice state is shown here.</b> The error from a failed recognition used to be
        ///    written into <c>_voiceMessage</c>, which is only *rendered* by the settings panel — so
        ///    on a phone, tapping the microphone and getting nothing produced literally no feedback.
        ///    "The button does nothing" was the correct reading of the screen.
        ///  · <b>Reading aloud has a button.</b> The pet talks now, and the only way to shut it up
        ///    was a checkbox in a panel most players never open. A switch the player cannot find is
        ///    a switch that does not work.
        ///  · <b>Two rows instead of one crammed line.</b> The desktop footer packs three quick
        ///    actions, a mute button, a volume slider and two hint labels into one 30px strip; on a
        ///    637px-wide panel that row ran off the bottom edge of the screen.
        /// </summary>
        private void DrawMobileChatFooter(PetGameManager gm, Rect rect)
        {
            MobileWidgets.BeginFrame(Scale, SafeOffset);
            DshMobile.MobileStt.Tick();
            ReportVoiceFailure(gm);

            // ---------------------------------------------------------------- row 1: talk
            float rowHeight = 46f;
            bool micHere = DshMobile.MobileStt.Offered;
            float micSize = micHere ? rowHeight : 0f;

            var field = new Rect(rect.x + micSize + (micHere ? 8f : 0f), rect.y + 14f,
                rect.width - 84f - micSize - (micHere ? 8f : 0f), rowHeight);
            UiSkin.Panel(field, 12f, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.22f), 1.5f);

            if (micHere)
            {
                var mic = new Rect(rect.x, field.y, micSize, micSize);
                bool listening = DshMobile.MobileStt.Listening;

                // The label is the state, because the button is the only thing on screen while the
                // player waits: 说 → tap to speak, 听 → it is listening, 等 → the system dialog is up.
                string label = listening ? "停" : (DshMobile.MobileStt.PendingPermission ? "等" : "说");
                var tint = listening
                    ? new Color(0.90f, 0.42f, 0.36f)
                    : (DshMobile.MobileStt.PendingPermission
                        ? new Color(0.82f, 0.66f, 0.30f)
                        : new Color(0.34f, 0.60f, 0.86f));

                if (MobileWidgets.CircleButton(MobileButtonIds.PetMic, mic, label, tint))
                {
                    if (listening) DshMobile.MobileStt.StopListening();
                    else StartListeningAndSay(gm);
                }
            }

            GUI.SetNextControlName("PetInput");
            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                         GUI.GetNameOfFocusedControl() == "PetInput";

            var previous = GUI.skin.textField.fontSize;
            GUI.skin.textField.fontSize = _inputFontSize;
            _input = GUI.TextField(new Rect(field.x + 12f, field.y + 10f, field.width - 24f, 26f),
                _input ?? "", 400);
            GUI.skin.textField.fontSize = previous;
            IsTextInputFocused = GUI.GetNameOfFocusedControl() == "PetInput";

            var sendRect = new Rect(field.xMax + 8f, field.y, 76f, rowHeight);
            bool send = GUI.Button(sendRect, gm.IsThinking ? "…" : "发送", _sendButton);
            if ((send || enter) && !gm.IsThinking)
            {
                gm.Talk(_input);
                _input = "";
                if (enter) Event.current.Use();
            }

            // ------------------------------------------------------- row 2: the two switches
            var actions = new Rect(rect.x, field.yMax + 8f, rect.width, 34f);
            GUILayout.BeginArea(actions);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("摸摸它", _buttonSmall, GUILayout.Height(30f))) gm.QuickAction("pet");
            if (GUILayout.Button("去吃饭", _buttonSmall, GUILayout.Height(30f))) gm.QuickAction("feed");
            if (GUILayout.Button("去玩球", _buttonSmall, GUILayout.Height(30f))) gm.QuickAction("play");

            GUILayout.FlexibleSpace();

            if (DshMobile.MobileTts.Available)
            {
                bool speaking = DshMobile.MobileTts.Enabled;
                if (GUILayout.Button(speaking ? "朗读：开" : "朗读：关", _buttonSmall,
                        GUILayout.Height(30f), GUILayout.Width(96f)))
                {
                    SetSpeechEnabled(gm, !speaking);
                }
            }

            var audio = PetAudioDirector.Instance;
            if (audio != null)
            {
                // No emoji for the speaker: Unity's built-in font has no glyph for 🔊 and this
                // project has shipped that bug four times (DEVLOG 坑 77/80).
                if (GUILayout.Button(audio.Muted ? "音效：关" : "音效：开", _buttonSmall,
                        GUILayout.Height(30f), GUILayout.Width(96f)))
                {
                    audio.ToggleMute();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // ---------------------------------------------------------------- the status line
            string status = VoiceStatusLine();
            if (!string.IsNullOrEmpty(status))
            {
                GUI.color = VoiceStatusIsProblem() ? new Color(1f, 0.68f, 0.58f)
                                                   : new Color(0.72f, 0.82f, 0.95f);
                GUI.Label(new Rect(rect.x + 2f, actions.yMax + 2f, rect.width - 4f, 22f),
                    status, _small);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Starts listening, and — when it cannot — says so in the conversation.
        ///
        /// The error goes into the transcript as a system note, not into a label that a repaint can
        /// wipe: a player who tapped the microphone and got nothing has to be able to read *why*,
        /// and to still be able to read it after the next frame.
        ///
        /// Since recognition runs in a block posted to Android's UI thread, a failure can also
        /// arrive *after* this returns; those are picked up by <see cref="ReportVoiceFailure"/>.
        /// </summary>
        private void StartListeningAndSay(PetGameManager gm)
        {
            if (DshMobile.MobileStt.StartListening()) return;
            ReportVoiceFailure(gm);
        }

        /// <summary>
        /// Writes a late voice failure into the conversation, once per distinct message.
        ///
        /// "Once" matters: this runs every frame, and a failure that repeats itself would fill the
        /// transcript with the same sentence until the player gives up on reading it.
        /// </summary>
        private static void ReportVoiceFailure(PetGameManager gm)
        {
            string why = DshMobile.MobileStt.TakeErrorReport();
            if (string.IsNullOrEmpty(why) || gm == null) return;

            // The reason already reads as a sentence ("这台手机没有语音识别服务"), so it goes in
            // brackets rather than behind another prefix — "语音输入：语音输入…" is what a naive
            // concatenation produces, and it makes a diagnostic look like a bug.
            gm.Memory.AddSystem("（语音输入：" + why + "）");
            gm.AnnounceChat();
        }

        /// <summary>Turns reading aloud on or off, from anywhere, and notes it in the conversation.</summary>
        private void SetSpeechEnabled(PetGameManager gm, bool on)
        {
            DshMobile.MobileTts.Enabled = on;
            if (on) DshMobile.MobileTts.WarmUp();
            else DshMobile.MobileTts.Stop();

            gm.Memory.AddSystem(on
                ? "（朗读打开了：宠物说的话会念出来。想安静再点一次「朗读：开」）"
                : "（朗读关掉了：宠物只叫不说。想听再点一次「朗读：关」）");
            gm.AnnounceChat();
        }

        /// <summary>
        /// One line about the voice features, for the phone's chat panel.
        ///
        /// Empty when there is nothing worth saying — a status that is always on is wallpaper.
        /// </summary>
        private static string VoiceStatusLine()
        {
            if (DshMobile.MobileStt.Listening) return "正在听……说完会自动停，识别到的字会填进输入框。";
            if (DshMobile.MobileStt.PendingPermission) return "在等麦克风权限：同意系统弹窗后会自动开始听。";
            if (DshMobile.MobileStt.Enabled && !DshMobile.MobileStt.AvailableNow)
                return "这台设备没有语音识别，打字也可以。";
            if (!string.IsNullOrEmpty(DshMobile.MobileStt.LastError))
                return DshMobile.MobileStt.LastError;

            if (DshMobile.MobileTts.Available && !DshMobile.MobileTts.Enabled)
                return "朗读已关闭（点「朗读：关」可以打开）。";
            return "";
        }

        /// <summary>Whether <see cref="VoiceStatusLine"/> is reporting a problem rather than a state.</summary>
        private static bool VoiceStatusIsProblem()
        {
            if (DshMobile.MobileStt.Listening || DshMobile.MobileStt.PendingPermission) return false;
            if (DshMobile.MobileStt.Enabled && !DshMobile.MobileStt.AvailableNow) return true;
            return !string.IsNullOrEmpty(DshMobile.MobileStt.LastError);
        }

        /// <summary>Input field, send button and the quick actions.</summary>
        private void DrawChatFooter(PetGameManager gm, Rect rect)
        {
            ReportVoiceFailure(gm);

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
                // Words, not 🔇/🔊: the built-in font has no glyph for either, so the desktop
                // footer had two invisible buttons sitting next to the volume slider (DEVLOG 坑 77).
                if (GUILayout.Button(audio.Muted ? "音效：关" : "音效：开", _buttonSmall,
                        GUILayout.Width(78f), GUILayout.Height(26f)))
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

        /// <summary>Which board the player is on. Kept between sessions, because it is a preference.</summary>
        private MemoryDifficulty _memoryLevel = MemoryDifficulty.Normal;

        private bool _memoryLevelLoaded;

        /// <summary>When each slot's pair was cleared, so it can be seen to disappear.</summary>
        private readonly float[] _memoryClearedAt = new float[PetMemoryMatch.MaxSlots];

        /// <summary>Which slots we have already seen go from "in play" to "cleared".</summary>
        private readonly bool[] _memoryClearedSeen = new bool[PetMemoryMatch.MaxSlots];

        /// <summary>How long a matched pair takes to shrink and fade away.</summary>
        public const float MemoryClearSeconds = 0.45f;

        /// <summary>Where the difficulty preference lives.</summary>
        public const string MemoryLevelKey = "dshpet.memory.level";

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
            SyncMusic();
            if (!_showMemory) return;

            if (!_memoryLevelLoaded)
            {
                _memoryLevel = PetMemoryMatch.ParseDifficulty(
                    PlayerPrefs.GetString(MemoryLevelKey, ""), MemoryDifficulty.Normal);
                _memoryLevelLoaded = true;
            }

            DealMemoryBoard(gm, $"翻开两张一样的就消掉，不一样会自己盖回去。{PetMemoryMatch.Describe(_memoryLevel)}");
        }

        /// <summary>Deals a fresh board at the current difficulty, and resets the bookkeeping.</summary>
        private void DealMemoryBoard(PetGameManager gm, string message)
        {
            _memory = PetMemoryMatch.Start(_memoryLevel, UnityEngine.Random.Range(0, 1 << 28));
            _memoryPaid = false;
            _memoryOpenedAt = Time.realtimeSinceStartup;

            for (int i = 0; i < _memoryClearedSeen.Length; i++)
            {
                _memoryClearedSeen[i] = false;
                _memoryClearedAt[i] = 0f;
            }

            if (!string.IsNullOrEmpty(message)) SetMemoryMessage(message);
        }

        /// <summary>Switches difficulty, saves it and deals again. The reward changes with it.</summary>
        private void SetMemoryLevel(PetGameManager gm, MemoryDifficulty level)
        {
            if (_memoryLevel == level && _memory != null) return;

            _memoryLevel = level;
            _memoryLevelLoaded = true;
            PlayerPrefs.SetString(MemoryLevelKey, PetMemoryMatch.KeyOf(level));
            PlayerPrefs.Save();

            DealMemoryBoard(gm,
                $"换成{PetMemoryMatch.NameOf(level)}了：{PetMemoryMatch.Describe(level)}。");
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
                SyncMusic();
                return;
            }

            GUI.Label(new Rect(inner.x, inner.y + 36f, inner.width, 22f),
                $"{(int)(Time.realtimeSinceStartup - _memoryOpenedAt)} 秒　·　{_memory.Moves} 步　·　" +
                $"消掉 {_memory.Matched}/{_memory.Pairs} 对　·　{PetMemoryMatch.NameOf(_memory.Difficulty)}", _small);

            // Difficulty row. Three buttons rather than a cycle: which board you are on is the first
            // thing a player wants to change, and a control that has to be pressed twice to get back
            // where you were is a control that gets pressed wrong.
            float levelTop = inner.y + 62f;
            float levelWidth = (inner.width - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var level = PetMemoryMatch.ClampDifficulty(i);
                bool active = level == _memoryLevel;

                var tint = active
                    ? new Color(0.36f, 0.66f, 0.44f)
                    : new Color(0.24f, 0.26f, 0.34f);

                var box = new Rect(inner.x + i * (levelWidth + 8f), levelTop, levelWidth, 34f);
                UiSkin.Panel(box, 10f, tint, new Color(1f, 1f, 1f, active ? 0.55f : 0.18f), 1.5f);

                GUI.Label(box,
                    $"{PetMemoryMatch.NameOf(level)}　{PetMemoryMatch.BaseRewardFor(level)} 币", _small);

                if (GUI.Button(box, GUIContent.none, GUIStyle.none) && !active)
                {
                    SetMemoryLevel(gm, level);
                    PetAudioDirector.Instance?.Play(SfxId.UiClick);

                    // The board under this row has just been replaced; stop drawing this frame rather
                    // than mixing the new grid with the old loop's idea of it.
                    return;
                }
            }

            if (!string.IsNullOrEmpty(_memoryMessage))
            {
                GUI.color = _memory.IsSolved ? new Color(0.7f, 0.95f, 0.75f) : new Color(0.85f, 0.88f, 0.95f);
                GUI.Label(new Rect(inner.x, levelTop + 40f, inner.width, 22f), _memoryMessage, _small);
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
            float boardTop = levelTop + 70f;
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

            float now = Time.realtimeSinceStartup;

            for (int i = 0; i < _memory.Count; i++)
            {
                int row = i / columns, column = i % columns;
                var card = new Rect(boardX + column * (cell + 8f), boardY + row * (cell + 8f), cell, cell);

                bool taken = _memory.IsTaken(i);

                // A pair that has just matched is watched going away, not teleported: the model says
                // "banked", the screen says "gone", and the half second between them is the reward.
                if (taken && !_memoryClearedSeen[i] && i < _memoryClearedSeen.Length)
                {
                    _memoryClearedSeen[i] = true;
                    _memoryClearedAt[i] = now;
                }

                if (taken)
                {
                    float age = i < _memoryClearedAt.Length ? now - _memoryClearedAt[i] : 1f;
                    float fade = Mathf.Clamp01(age / MemoryClearSeconds);

                    if (fade >= 1f)
                    {
                        // The slot is empty and stays empty: the board is *smaller* as you win, which
                        // is how a matched pair is supposed to read. No button, nothing to flip.
                        UiSkin.Panel(card, 12f, new Color(1f, 1f, 1f, 0.035f),
                            new Color(1f, 1f, 1f, 0.10f), 1.5f);
                        continue;
                    }

                    // Green flash first, then the animal shrinks into it.
                    UiSkin.Panel(card, 12f,
                        new Color(0.42f, 0.78f, 0.48f, 0.75f * (1f - fade)),
                        new Color(0.72f, 0.95f, 0.76f, 0.9f * (1f - fade)), 2f);

                    float shrink = 1f - fade * 0.45f;
                    var shrinking = new Rect(card.center.x - card.width * shrink * 0.5f,
                        card.center.y - card.height * shrink * 0.5f,
                        card.width * shrink, card.height * shrink);

                    DrawMemoryFace(shrinking, _memory.FaceAt(i), 1f - fade);
                    continue;
                }

                bool faceUp = _memory.IsFaceUp(i);
                if (faceUp)
                {
                    UiSkin.Panel(card, 12f, new Color(0.98f, 0.94f, 0.86f, 1f),
                        new Color(1f, 1f, 1f, 0.25f), 2f);
                    DrawMemoryFace(card, _memory.FaceAt(i), 1f);
                }
                else
                {
                    // Face down: a card back, not a hole. The disc in the middle is what says
                    // "there is something under here" without printing a character at it.
                    UiSkin.Panel(card, 12f, new Color(0.28f, 0.36f, 0.55f, 1f),
                        new Color(1f, 1f, 1f, 0.20f), 2f);
                    float back = cell * 0.26f;
                    UiSkin.Panel(new Rect(card.center.x - back, card.center.y - back, back * 2f, back * 2f),
                        back, new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.10f), 2f);

                    if (!_memory.IsSolved && _memory.CanFlip(i)
                        && GUI.Button(card, GUIContent.none, GUIStyle.none))
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
                DealMemoryBoard(gm, $"重新洗牌了。{PetMemoryMatch.Describe(_memoryLevel)}");
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"玩成 {_memoryWins} 次　·　最少 {(_memoryBestMoves > 0 ? _memoryBestMoves.ToString() : "-")} 步", _small);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawModalEscape();
        }

        /// <summary>The animal on a card, filling the middle of it.</summary>
        private static void DrawMemoryFace(Rect card, int face, float alpha)
        {
            float inset = Mathf.Min(card.width, card.height) * 0.12f;
            var art = new Rect(card.x + inset, card.y + inset,
                card.width - inset * 2f, card.height - inset * 2f);

            var texture = PetAvatarArt.TextureFor(face);
            if (texture == null) return;

            var was = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(art, texture, ScaleMode.ScaleToFit, true);
            GUI.color = was;
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

            string level = PetMemoryMatch.NameOf(_memory.Difficulty);
            gm.Memory.AddPet($"（和你玩{level}的记忆配对，{_memory.Moves} 步就全找齐了）");
            gm.Journal.Add(MemoryKind.Play, "玩记忆配对",
                $"{level}：{_memory.Moves} 步配完 {_memory.Pairs} 对，赚了 {coins} 个宠物币", 0.45f);
            gm.AnnounceChat();

            SetMemoryMessage(
                $"全消掉了！{level}拿 {coins} 个宠物币（{PetMemoryMatch.RankFor(_memory.Pairs, _memory.Moves)}）" +
                "——想多赚就换更难的。");
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
        /// <summary>
        /// Keeps the music in step with what is on screen.
        ///
        /// The two in-room games are panels rather than scenes, so the scene-name wiring that
        /// gives every other activity its track cannot see them: opening the puzzle would leave
        /// the garden's music playing over a quiet thinking game. Called from the four places the
        /// panels open or close rather than every frame — a per-frame "make sure the track is
        /// right" is the kind of call that quietly becomes a bug when two of them disagree.
        /// </summary>
        private void SyncMusic()
        {
            if (_showPuzzle || _showMemory)
            {
                DshMobile.MobileMusic.Play(DshMobile.MusicId.Puzzle);
                return;
            }

            DshMobile.MobileMusic.PlayForTheme(PetWorldMap.Current);
        }

        public void OpenPuzzle(PetGameManager gm)
        {
            _showPuzzle = !_showPuzzle;
            SyncMusic();
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
                SyncMusic();
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

        // ------------------------------------------------------------------ shop & warehouse

        /// <summary>
        /// The furniture panel: 商城 (buy), 仓库 (place / store / sell), and the door into free
        /// placement mode. One panel rather than three, because "buy it → where did it go →
        /// put it in the room" is one thought and it should be one screen.
        /// </summary>
        private void DrawFurnish(PetGameManager gm)
        {
            float w = Mathf.Min(560f, DesignWidth - 32f);
            float h = Mathf.Min(620f, DesignHeight - 32f);
            var rect = OverlayRect(w, h);
            ModalBackdrop(rect);

            var inner = new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width * 0.5f, 32f), "商城 · 仓库 · 背包", _title);

            var balance = new GUIStyle(_title) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(inner.x + inner.width * 0.4f, inner.y, inner.width * 0.6f - 84f, 32f),
                $"🐾 {DshMobile.PetWallet.Coins:N0}", balance);

            if (GUI.Button(new Rect(inner.xMax - 76f, inner.y + 2f, 76f, 30f), "关闭", _button))
            {
                _showFurnish = false;
                return;
            }

            // Tabs: 商城 / 背包 / 仓库.
            float tabWidth = 72f;
            GUI.enabled = _furnishTab != 0;
            if (GUI.Button(new Rect(inner.x, inner.y + 34f, tabWidth, 34f), "商城", _button)) _furnishTab = 0;
            GUI.enabled = _furnishTab != 1;
            if (GUI.Button(new Rect(inner.x + tabWidth + 8f, inner.y + 34f, tabWidth, 34f), "背包", _button)) _furnishTab = 1;
            GUI.enabled = _furnishTab != 2;
            if (GUI.Button(new Rect(inner.x + tabWidth * 2f + 16f, inner.y + 34f, tabWidth, 34f), "仓库", _button)) _furnishTab = 2;
            GUI.enabled = true;

            GUI.Label(new Rect(inner.x + tabWidth * 3f + 28f, inner.y + 38f, inner.width - tabWidth * 3f - 28f, 26f),
                "粮 " + PetInventory.Food + " · 背包 " + PetBackpack.Equipped().Count + "/" + PetBackpack.ToolSlots, _small);

            var messageColor = _furnishMessage.StartsWith("还差") || _furnishMessage.Contains("没有")
                ? new Color(1f, 0.72f, 0.60f)
                : new Color(0.62f, 0.95f, 0.70f);
            GUI.color = messageColor;
            GUI.Label(new Rect(inner.x, inner.y + 70f, inner.width, 22f), _furnishMessage, _small);
            GUI.color = Color.white;

            var body = new Rect(inner.x, inner.y + 94f, inner.width, Mathf.Max(60f, inner.yMax - inner.y - 94f));
            GUILayout.BeginArea(body);
            _furnishScroll = GUILayout.BeginScrollView(_furnishScroll);

            if (_furnishTab == 0) DrawShopTab();
            else if (_furnishTab == 1) DrawBackpackTab(gm);
            else DrawWarehouseTab(gm);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawModalEscape();
        }

        private void DrawShopTab()
        {
            var currentPlace = PetWorldMap.Current;
            DrawGachaSection();
            DrawShopSection("🍖 食品", ShopCategory.Food, currentPlace);
            DrawShopSection("🧰 道具", ShopCategory.Tool, currentPlace);
            DrawShopSection("🛋️ 家具", ShopCategory.Furniture, currentPlace);
        }

        /// <summary>
        /// The 扭蛋机 lives at the top of the 商城 so it cannot be missed again: it was originally
        /// tucked inside the pet collection and players reported not finding it at all.
        /// </summary>
        private void DrawGachaSection()
        {
            GUILayout.Label("🎰 扭蛋机", _label);
            GUILayout.Label("　投 " + PetGacha.Cost + " 币随机抽一只扭蛋专属宠物（小仓鼠 50% / 小熊猫 35% / 小企鹅 15%），不进直售。", _small);
            GUILayout.Space(2f);

            bool afford = DshMobile.PetWallet.CanAfford(PetGacha.Cost);
            GUI.enabled = afford;
            if (GUILayout.Button(afford ? $"🎲 扭一次（¥{PetGacha.Cost}）" : $"还差 {PetGacha.Cost - DshMobile.PetWallet.Coins} 币",
                    _button, GUILayout.Height(44f)))
            {
                string message;
                PetCollection.RollGacha(out message);
                _furnishMessage = message;
                DshMobile.MobileHaptics.Medium();
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_furnishMessage) && _furnishMessage.Contains("扭蛋"))
            {
                GUILayout.Label("　" + _furnishMessage, _small);
            }
            GUILayout.Space(8f);
        }

        private void DrawShopSection(string header, ShopCategory category, RoomTheme place)
        {
            GUILayout.Label(header, _label);
            GUILayout.Space(2f);

            for (int i = 0; i < PetShop.All.Length; i++)
            {
                var item = PetShop.All[i];
                if (item.Category != category) continue;

                // Produced food (apple, fish) is not bought; it comes from the garden. The shop
                // shows it greyed so the player knows it *can* be sold later.
                if (item.Produced)
                {
                    GUILayout.Label($"　{item.Emoji} {item.Name} —— 世界里获得（能卖 ¥{item.SellPrice}）", _small);
                    GUILayout.Label("　" + item.Blurb, _small);
                    GUILayout.Space(6f);
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{item.Emoji} {item.Name}", _label, GUILayout.Width(150f));
                GUILayout.Label($"¥{item.Price}", _label, GUILayout.Width(70f));
                GUILayout.FlexibleSpace();

                bool owned = item.IsFood ? false : PetInventory.IsOwned(item.Id);
                GUI.enabled = !owned;
                string buyLabel = item.IsFood
                    ? (item.FoodUnits > 1 ? $"购买（+{item.FoodUnits}）" : "购买")
                    : (owned ? "已拥有" : "购买");
                if (GUILayout.Button(buyLabel, _button, GUILayout.Width(110f), GUILayout.Height(34f)))
                {
                    _furnishMessage = PetInventory.Buy(item);
                    DshMobile.MobileHaptics.Light();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.Label("　" + item.Blurb, _small);

                if (item.IsFurniture && item.Scene != ItemScene.Anywhere)
                {
                    bool here = item.AllowedIn(place);
                    GUILayout.Label(here
                        ? "　（这里可以摆放）"
                        : "　（只能摆在" + PetInventory.PlaceName(item.Scene) + "）", _small);
                }
                GUILayout.Space(8f);
            }
            GUILayout.Space(6f);
        }

        /// <summary>The backpack: three tool slots, and the pet slots it shares a screen with.</summary>
        private void DrawBackpackTab(PetGameManager gm)
        {
            GUILayout.Label("道具栏（" + PetBackpack.Equipped().Count + "/" + PetBackpack.ToolSlots + "）", _label);

            var equipped = PetBackpack.Equipped();
            for (int i = 0; i < PetBackpack.ToolSlots; i++)
            {
                string id = i < equipped.Count ? equipped[i] : "";
                var item = string.IsNullOrEmpty(id) ? null : PetShop.Get(id);

                GUILayout.BeginHorizontal();
                GUILayout.Label("第 " + (i + 1) + " 格：", _small, GUILayout.Width(60f));
                GUILayout.Label(item != null ? $"{item.Emoji} {item.Name}" : "空", _label, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();

                if (item != null && GUILayout.Button("取下", _buttonSmall, GUILayout.Height(28f)))
                {
                    _furnishMessage = PetBackpack.Unequip(item.Id);
                    DshMobile.MobileHaptics.Light();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            if (PetBackpack.BucketFull) GUILayout.Label("水桶里装满了水，去浇苹果树吧。", _small);

            GUILayout.Space(8f);
            GUILayout.Label("没装备上的道具", _label);
            bool anyTool = false;
            foreach (var item in PetShop.All)
            {
                if (!item.IsTool || !PetInventory.IsOwned(item.Id)) continue;
                if (PetBackpack.IsEquipped(item.Id)) continue;
                anyTool = true;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{item.Emoji} {item.Name}", _label, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("装备", _buttonSmall, GUILayout.Height(28f)))
                {
                    _furnishMessage = PetBackpack.Equip(item.Id);
                    DshMobile.MobileHaptics.Light();
                }
                GUILayout.EndHorizontal();
            }
            if (!anyTool) GUILayout.Label("还没有没装备的道具。商城买水桶、铲子，再来这里装备。", _small);

            GUILayout.Space(10f);
            GUILayout.Label("随身食物（点「投喂」直接喂给宠物）", _label);
            bool anyFood = false;
            foreach (var item in PetShop.All)
            {
                if (!item.IsFood) continue;
                int n = PetInventory.Count(item.Id);
                if (n <= 0) continue;
                anyFood = true;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{item.Emoji} {item.Name} ×{n}", _label, GUILayout.Width(170f));
                GUILayout.FlexibleSpace();
                if (gm != null && GUILayout.Button("投喂", _buttonSmall, GUILayout.Height(28f)))
                {
                    gm.FeedFromBackpack(item.Id);
                    DshMobile.MobileHaptics.Light();
                }
                GUILayout.EndHorizontal();
            }
            if (!anyFood) GUILayout.Label("背包里还没有食物。商城买粮食、水、肉，或去花园摘苹果、钓鱼。", _small);

            GUILayout.Space(10f);
            GUILayout.Label("宠物栏（随身 " + PetCollection.Backpack.Count + "/" + PetCollection.BackpackSlots + "）", _label);
            GUILayout.Label("　宠物在「宠物」面板的背包页管理；道具和食物在这里管理。", _small);
            GUILayout.Label("仓库容量：" + PetInventory.WarehouseSlots + " 格（多余的道具、家具和宠物都放这里）。", _small);
        }

        private void DrawWarehouseTab(PetGameManager gm)
        {
            var currentPlace = PetWorldMap.Current;
            GUILayout.Label("粮食 " + PetInventory.Food + " 顿　·　苹果 " + PetInventory.Count("apple") + " 个　·　鱼 " +
                            PetInventory.Count("fish") + " 条　·　水 " + PetInventory.Count("water") + " 瓶　·　肉 " +
                            PetInventory.Count("meat") + " 份", _small);
            GUILayout.Space(4f);

            // Edible / sellable stock, one line each.
            foreach (var item in PetShop.All)
            {
                if (!item.IsFood) continue;
                int n = PetInventory.Count(item.Id);
                if (n <= 0) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{item.Emoji} {item.Name} ×{n}", _label, GUILayout.Width(170f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("投喂", _buttonSmall, GUILayout.Height(28f)))
                {
                    gm.FeedFromBackpack(item.Id);
                    DshMobile.MobileHaptics.Light();
                }
                if (GUILayout.Button($"卖 ¥{item.SellPrice}", _buttonSmall, GUILayout.Height(28f)))
                {
                    _furnishMessage = PetInventory.Sell(item.Id);
                    DshMobile.MobileHaptics.Light();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            var placed = PetInventory.Placed(currentPlace);

            GUILayout.Label("摆在这里（" + RoomThemeInfo.Get(currentPlace).DisplayName + "）", _label);
            bool anyHere = false;
            foreach (var id in placed.Keys)
            {
                var item = PetShop.Get(id);
                if (item == null) continue;
                anyHere = true;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{item.Emoji} {item.Name}", _label, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();

                if (!PetShop.IsStarter(id))
                {
                    if (GUILayout.Button("收回仓库", _buttonSmall, GUILayout.Height(30f)))
                    {
                        _furnishMessage = PetInventory.Store(id, currentPlace);
                        DshMobile.MobileHaptics.Light();
                        gm.RebuildRoom();
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (!anyHere) GUILayout.Label("这里还空着，去仓库把家具摆出来。", _small);

            GUILayout.Space(10f);
            GUILayout.Label("在仓库里（还没摆进这个场景）", _label);
            bool any = false;
            foreach (var item in PetShop.All)
            {
                if (!item.IsFurniture) continue;
                if (!PetInventory.IsOwned(item.Id) || placed.ContainsKey(item.Id)) continue;
                any = true;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{item.Emoji} {item.Name}", _label, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();

                bool allowed = item.AllowedIn(currentPlace);
                GUI.enabled = allowed;
                if (GUILayout.Button("摆放", _buttonSmall, GUILayout.Height(30f)))
                {
                    _furnishMessage = PetInventory.Place(item.Id, currentPlace);
                    DshMobile.MobileHaptics.Light();
                    gm.RebuildRoom();
                }
                GUI.enabled = true;

                if (GUILayout.Button($"卖掉 ¥{item.SellPrice}", _buttonSmall, GUILayout.Height(30f)))
                {
                    _furnishMessage = PetInventory.Sell(item.Id);
                    DshMobile.MobileHaptics.Light();
                }
                GUILayout.EndHorizontal();

                if (!allowed)
                {
                    GUILayout.Label("　只能摆在" + PetInventory.PlaceName(item.Scene) + "。", _small);
                }
                else
                {
                    // Where else this owned furniture is already set down.
                    var elsewhere = PetInventory.ScenesWherePlaced(item.Id);
                    var names = new System.Collections.Generic.List<string>();
                    for (int s = 0; s < elsewhere.Count; s++)
                    {
                        if (elsewhere[s] == currentPlace) continue;
                        names.Add(RoomThemeInfo.Get(elsewhere[s]).DisplayName);
                    }
                    if (names.Count > 0) GUILayout.Label("　也摆在：" + string.Join("、", names.ToArray()), _small);
                }
            }
            if (!any) GUILayout.Label("仓库空空的。去商城看看，家具买回来才能摆进房间。", _small);

            GUILayout.Space(14f);
            GUILayout.Label("想给家具换个位置？", _label);
            if (GUILayout.Button("🧭 进入自由摆放模式（拖动家具）", _button, GUILayout.Height(40f)))
            {
                _showFurnish = false;
                EnterPlacementMode();
            }
            GUILayout.Label("　摆放模式下，按住家具拖到新位置，松手就定下来。", _small);
        }

        private void EnterPlacementMode()
        {
            _placeMode = true;
            PlacementDragger.SetActive(true);
        }

        private void DrawPlacementHud(PetGameManager gm)
        {
            // A slim bar at the top: the mode is on, the exit is always reachable.
            var bar = new Rect(DesignWidth * 0.5f - 220f, 12f, 440f, 54f);
            DshMobile.UiSkin.Panel(bar, 14f, new Color(0.08f, 0.09f, 0.13f, 0.94f),
                new Color(0.6f, 0.9f, 1f, 0.5f), 1.5f);

            GUI.Label(new Rect(bar.x + 14f, bar.y + 6f, bar.width - 120f, 24f),
                "自由摆放中：按住家具拖到新位置", _small);
            GUI.Label(new Rect(bar.x + 14f, bar.y + 28f, bar.width - 120f, 20f),
                "松手即保存；猫砂盆、小床都可以重新摆", _small);

            if (GUI.Button(new Rect(bar.xMax - 96f, bar.y + 10f, 84f, 36f), "完成", _button))
            {
                _placeMode = false;
                PlacementDragger.SetActive(false);
                DshMobile.MobileHaptics.Light();
            }
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

            // The cursor hint is now a uGUI element (see BuildOverlays/Update); it was drawn here
            // before the migration and now lives on top of the room as a canvas label.

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

            // Music gets its own switch rather than riding on the sound-effect mute: the player
            // who wants footsteps and chirps without a soundtrack is exactly who asks for this,
            // and "all audio off" is the one answer that never helps them. The slider is here
            // because "quieter" is the request people actually have.
            GUILayout.BeginHorizontal();
            bool musicOn = GUILayout.Toggle(DshMobile.MobileMusic.Enabled, " 背景音乐", _small);
            if (musicOn != DshMobile.MobileMusic.Enabled) DshMobile.MobileMusic.Enabled = musicOn;
            GUILayout.FlexibleSpace();
            GUILayout.Label(Mathf.RoundToInt(DshMobile.MobileMusic.Volume * 100f) + "%", _small,
                GUILayout.Width(44f));
            GUILayout.EndHorizontal();

            float musicVolume = GUILayout.HorizontalSlider(DshMobile.MobileMusic.Volume, 0f, 1f);
            if (!Mathf.Approximately(musicVolume, DshMobile.MobileMusic.Volume))
            {
                DshMobile.MobileMusic.Volume = musicVolume;
            }

            GUILayout.Label(DshMobile.MobileMusic.NowPlayingText, _small);

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
                    //
                    // But pressing it also means "I want to hear this", so the test switches
                    // speech on. That is the whole complaint it answers: the phone could talk and
                    // the pet still said nothing, because the checkbox above was off and nobody
                    // had a reason to open this panel at all.
                    if (!DshMobile.MobileTts.Enabled) DshMobile.MobileTts.Enabled = true;
                    DshMobile.MobileTts.WarmUp();

                    float testPitch, testRate;
                    PetVoice.SpeechParams(gm.Species, gm.Personality, out testPitch, out testRate);
                    bool spoke = DshMobile.MobileTts.Test(testPitch, testRate);
                    SetVoiceMessage(spoke
                        ? "已经念了一句，朗读也打开了：宠物之后说的话都会念出来。听不到就把手机音量调大一点（用的是媒体音量）。"
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

            // The commands are recognised on the device, so they work through the microphone as well
            // as through the text box — which is worth saying out loud somewhere a player can find.
            GUILayout.Label("它听得懂短指令（打字或说话都行，本地识别，不花 token）：「过来」「别动」「跟着我」" +
                            "「吃饭」「喝水」「拿球」「陪我玩」「去睡觉」「上厕所」「洗澡」「梳毛」" +
                            "「饭碗在哪」。", _small);

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

                // The facts of the last attempt, on screen. "点麦克风没反应" cannot be diagnosed
                // from a phone without this: there is no console, and every failure mode (no
                // permission, no recogniser, refused request, engine never ready) looks identical
                // from the outside.
                GUILayout.Space(4f);
                GUILayout.Label("语音诊断（点一次麦克风之后看这里）：", _small);
                GUI.color = new Color(0.80f, 0.84f, 0.92f);
                GUILayout.Label(DshMobile.MobileStt.Diagnostics(), _small);
                GUI.color = Color.white;

                if (GUILayout.Button("重置识别器（识别不动时点一下）", _buttonSmall, GUILayout.Height(26f)))
                {
                    DshMobile.MobileStt.ResetRecognizer();
                    SetVoiceMessage("识别器已经重建，再点一次麦克风试试。", false);
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
            _showFurnish = false;
            _placeMode = false;
            PlacementDragger.SetActive(false);
            SyncMusic();
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

            DrawGachaMachine();

            GUILayout.Space(10f);
            GUILayout.Label("直接领养", _label);
            GUILayout.Space(4f);

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

        /// <summary>
        /// The 扭蛋机: one big pull button, the price, the odds, and the last result. Gacha-only
        /// species never appear in the direct-adoption list, so this is their only door.
        /// </summary>
        private void DrawGachaMachine()
        {
            GUILayout.Label("🎰 扭蛋机", _title);
            GUILayout.Label("投 " + PetGacha.Cost + " 币，随机转出一只扭蛋专属宠物（不进直售）。", _small);
            GUILayout.Label("奖池：" + PetGacha.OddsText(), _small);
            GUILayout.Space(4f);

            bool afford = DshMobile.PetWallet.CanAfford(PetGacha.Cost);
            GUI.enabled = afford;
            string label = afford ? $"🎲 扭一次（¥{PetGacha.Cost}）" : $"还差 {PetGacha.Cost - DshMobile.PetWallet.Coins} 币";
            if (GUILayout.Button(label, _button, GUILayout.Height(46f)))
            {
                string message;
                PetCollection.RollGacha(out message);
                SetCollectionMessage(message);
                DshMobile.MobileHaptics.Medium();
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_collectionMessage) && _collectionMessage.Contains("扭蛋"))
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUILayout.Label(_collectionMessage, _small);
                GUI.color = Color.white;
            }
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
