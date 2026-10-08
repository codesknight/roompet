using System;
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

            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.94f, 0.82f);

            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = Color.white;

            _small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _small.normal.textColor = new Color(0.85f, 0.86f, 0.9f);

            _petLine = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _petLine.normal.textColor = new Color(1f, 0.88f, 0.72f);

            _userLine = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _userLine.normal.textColor = new Color(0.72f, 0.88f, 1f);

            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 12, 12) };

            _button = new GUIStyle(GUI.skin.button) { fontSize = 15, padding = new RectOffset(12, 12, 7, 7) };
            _buttonSmall = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(6, 6, 4, 4) };

            _thinking = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Italic };
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
        }

        private void OnGUI()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;

            EnsureStyles();

            var layout = ComputeLayout();

            // While a modal panel is up, the panels behind it must not react. GUI.enabled is
            // how IMGUI is told a control is inert, and it is order-independent — unlike the
            // full-rect invisible Button this used to draw, which claimed the click itself and
            // left the modal's own text fields unable to take focus.
            bool modal = ModalOpen;
            if (modal) GUI.enabled = false;
            DrawStatusPanel(gm, layout);
            DrawChat(gm, layout);
            DrawSpeciesSwitcher(gm, layout);
            DrawViewSwitcher(gm, layout);
            DrawThrowMeter(gm, layout);
            DrawOverlays(gm, layout);
            GUI.enabled = true;

            if (gm.DoorPromptOpen) DrawDoorPrompt(gm);
            if (_showSettings) DrawSettings(gm);
            if (_showPromptPreview) DrawPromptPreview(gm);
            if (_showJournal) DrawJournal(gm);
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
        }

        /// <summary>
        /// The HUD's geometry, in one place.
        ///
        /// Two rules are encoded here, both learned from real bugs:
        ///  1. the status panel and the chat panel must NEVER overlap. They used to be sized
        ///     independently (status up to 360px tall from the top, chat 250px tall pinned to
        ///     the bottom), so on a Game view shorter than ~630px the chat panel — drawn
        ///     second, therefore on top — covered the status panel's footer and swallowed the
        ///     clicks meant for 记事本 and 设置. That is why the buttons "did not work";
        ///  2. every floating panel is clamped to the viewport. A fixed 720x560 calendar is
        ///     centred, so on a short view its own title bar and month arrows render above
        ///     y=0 and the player sees nothing.
        /// </summary>
        public static HudLayout ComputeLayout()
            => ComputeLayout(Screen.width, Screen.height, PetSpecies.Count);

        public static HudLayout ComputeLayout(float w, float h, int speciesCount)
        {
            // The transcript is the main event in this app, so it takes a third of the view.
            float chatHeight = Mathf.Clamp(h * 0.34f, 160f, 260f);
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
                ChatTop = chat.y
            };
        }

        /// <summary>A floating panel's rect, centred but always fully inside the viewport.</summary>
        public static Rect OverlayRect(float preferredWidth, float preferredHeight)
            => OverlayRect(preferredWidth, preferredHeight, Screen.width, Screen.height);

        public static Rect OverlayRect(float preferredWidth, float preferredHeight,
            float screenWidth, float screenHeight)
        {
            float w = Mathf.Min(preferredWidth, screenWidth - 32f);
            float h = Mathf.Min(preferredHeight, screenHeight - 32f);
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
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

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

            GUILayout.Space(4f);
            GUILayout.Label(string.IsNullOrEmpty(gm.Needs.DominantNeed) ? "状态不错" : "想要：" + gm.Needs.DominantNeed, _small);

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

            if (GUILayout.Button("重置", _buttonSmall)) gm.ResetPet();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // ------------------------------------------------------------ throw / fetch

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
                        var dot = new Rect(screen.x - 9f, Screen.height - screen.y - 9f, 18f, 18f);
                        GUI.color = new Color(1f, 0.9f, 0.5f, 0.85f);
                        GUI.Box(dot, GUIContent.none);
                        GUI.color = Color.white;
                    }
                }

                var panel = new Rect(Screen.width * 0.5f - 170f, layout.ChatTop - 84f, 340f, 62f);
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
                var hint = new Rect(Screen.width * 0.5f - 190f, layout.ChatTop - 52f, 380f, 30f);
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

        // ----------------------------------------------------------------------- chat

        private void DrawChat(PetGameManager gm, HudLayout layout)
        {
            var rect = layout.Chat;
            GUI.Box(rect, GUIContent.none, _panel);

            var inner = new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, rect.height - 24f);
            GUILayout.BeginArea(inner);

            // --- transcript ---
            float logHeight = inner.height - 74f;
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(logHeight));

            var recent = gm.Memory.Recent;
            int start = Mathf.Max(0, recent.Count - MaxLogLines);
            for (int i = start; i < recent.Count; i++)
            {
                var line = recent[i];
                GUILayout.Label((line.IsUser ? "你：" : gm.PetName + "：") + line.Text,
                    line.IsUser ? _userLine : _petLine);
            }

            if (gm.IsThinking)
            {
                GUILayout.Label("……（它正在想）", _thinking);
            }

            GUILayout.EndScrollView();

            // --- input row ---
            GUILayout.Space(4f);

            if (!string.IsNullOrEmpty(gm.LastError))
            {
                GUI.color = new Color(1f, 0.6f, 0.55f);
                GUILayout.Label("接口出错，已用离线回复：" + gm.LastError, _small);
                GUI.color = Color.white;
            }

            GUILayout.BeginHorizontal();

            GUI.SetNextControlName("PetInput");
            bool enter = Event.current.type == EventType.KeyDown &&
                         (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                         GUI.GetNameOfFocusedControl() == "PetInput";

            _input = GUILayout.TextField(_input ?? "", 400, GUILayout.Height(30f));
            IsTextInputFocused = GUI.GetNameOfFocusedControl() == "PetInput";

            bool send = GUILayout.Button("发送", _button, GUILayout.Width(64f), GUILayout.Height(30f));
            if ((send || enter) && !gm.IsThinking)
            {
                gm.Talk(_input);
                _input = "";
                if (enter) Event.current.Use();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("摸摸它", _buttonSmall, GUILayout.Width(74f))) gm.QuickAction("pet");
            if (GUILayout.Button("去吃饭", _buttonSmall, GUILayout.Width(74f))) gm.QuickAction("feed");
            if (GUILayout.Button("去玩球", _buttonSmall, GUILayout.Width(74f))) gm.QuickAction("play");

            var audio = PetAudioDirector.Instance;
            if (audio != null)
            {
                if (GUILayout.Button(audio.Muted ? "🔇" : "🔊", _buttonSmall, GUILayout.Width(40f)))
                {
                    audio.ToggleMute();
                }
                audio.SetMasterVolume(GUILayout.HorizontalSlider(audio.MasterVolume, 0f, 1f, GUILayout.Width(80f)));
            }

            GUILayout.Label("WASD 走动　E 互动", _small);
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
                    ? gm.Player.Nearby.Label + "　按 E 出去"
                    : gm.Player.Nearby.Label + "　按 E 让宠物过来";

                var style = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                GUI.color = new Color(1f, 0.95f, 0.8f, 0.95f);
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, layout.ChatTop - 32f, 400f, 24f), label, style);
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(_cursorHint))
            {
                var pos = Event.current.mousePosition;
                GUI.Label(new Rect(pos.x + 16f, pos.y + 8f, 320f, 24f), _cursorHint, _small);
            }

            var cam = Camera.main;
            if (cam != null && gm.Avatar != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(gm.Avatar.transform.position + Vector3.up * 1.1f);
                if (screen.z > 0f)
                {
                    var rect = new Rect(screen.x - 60f, Screen.height - screen.y - 16f, 120f, 22f);
                    var centered = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
                    GUI.Label(rect, PetUtil.MoodLabel(gm.Needs.Mood), centered);
                }
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
