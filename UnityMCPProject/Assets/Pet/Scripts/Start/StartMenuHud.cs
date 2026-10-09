using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The front door's interface: a title, four buttons, and the two panels behind them.
    ///
    /// IMGUI in design pixels with <c>GUI.matrix</c>, like every other screen in this project, so the
    /// phone layout and the desktop one are the same code. The one thing specific to this screen is that
    /// it is the *first* thing anybody sees: the buttons are big, the text says what the game is, and
    /// every panel can be dismissed by tapping the same corner it came from.
    /// </summary>
    public class StartMenuHud : MonoBehaviour
    {
        private StartMenu _menu;
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _button;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _centred;
        private Texture2D _white;
        private Vector2 _howToScroll;

        private void Awake() => _menu = GetComponent<StartMenu>();

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = new Color(1f, 0.96f, 0.86f);

            _subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _subtitle.normal.textColor = new Color(1f, 0.92f, 0.82f, 0.92f);

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(18, 18, 12, 12)
            };

            _body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _body.normal.textColor = new Color(0.96f, 0.96f, 0.99f);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _small.normal.textColor = new Color(0.90f, 0.92f, 0.97f, 0.9f);

            _centred = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };

            _pill = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _pill.normal.textColor = new Color(1f, 0.99f, 0.94f);

            _buttonPrimary = new GUIStyle(_button);
            _buttonPrimary.normal.textColor = new Color(0.20f, 0.12f, 0.04f);
            _buttonPrimary.fontSize = 25;

            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            _white.hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnGUI()
        {
            if (_menu == null) return;
            EnsureStyles();

            var safe = DshMobile.MobileUi.SafeArea;
            float scale = DshMobile.MobileUi.UseTouchControls ? DshMobile.MobileUi.UiScale : 1f;
            float width = Mathf.Max(320f, safe.width / scale);
            float height = Mathf.Max(240f, safe.height / scale);

            var previous = GUI.matrix;
            if (scale != 1f || safe.x != 0f || safe.y != 0f)
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, safe.y, 0f), Quaternion.identity,
                    new Vector3(scale, scale, 1f));
            }

            switch (_menu.Phase)
            {
                case StartPhase.Menu:
                    DrawMenu(width, height);
                    break;
                case StartPhase.Settings:
                    DrawSettings(width, height);
                    break;
                case StartPhase.HowTo:
                    DrawHowTo(width, height);
                    break;
                case StartPhase.Quit:
                    DrawQuit(width, height);
                    break;
                case StartPhase.Opening:
                case StartPhase.Leaving:
                    DrawFade(width, height);
                    break;
            }

            GUI.matrix = previous;
        }

        // ------------------------------------------------------------------ the menu

        private void DrawMenu(float width, float height)
        {
            // The title sits high and the buttons low, so a thumb on a phone is nowhere near the text.
            // Both are written on a translucent plate: the wall behind them is a bright cream, and cream
            // text on cream is invisible — the first capture of this screen had a title you could not read.
            float headerTop = height * 0.10f;
            var header = new Rect(width * 0.5f - 280f, headerTop - 14f, 560f, 116f);
            DshMobile.UiSkin.Panel(header, 22f, new Color(0.12f, 0.10f, 0.14f, 0.44f),
                new Color(1f, 0.92f, 0.78f, 0.18f), 1.4f);

            GUI.Label(new Rect(width * 0.5f - 260f, headerTop, 520f, 60f), "宠物小屋", _title);

            // One line, short. The first version ran the sentence out to two lines and left a full stop
            // alone on the second — which reads as a bug at the top of the very first screen of the game.
            GUI.Label(new Rect(width * 0.5f - 260f, headerTop + 60f, 520f, 34f),
                "会记事、听得懂你说话的" + _menu.PetName + "，在门里面等你。", _subtitle);

            float buttonWidth = Mathf.Min(400f, width - 72f);
            float buttonHeight = 66f;
            float gap = 18f;
            float total = buttonHeight * 4f + gap * 3f;
            float y = Mathf.Max(height * 0.44f, height - 92f - total);

            var start = new Rect((width - buttonWidth) * 0.5f, y, buttonWidth, buttonHeight);
            if (MenuButton(start, "进入房间", true))
            {
                _menu.Begin();
            }

            var howTo = new Rect(start.x, start.yMax + gap, buttonWidth, buttonHeight);
            if (MenuButton(howTo, "玩法介绍", false)) _menu.OpenHowTo();

            var settings = new Rect(start.x, howTo.yMax + gap, buttonWidth, buttonHeight);
            if (MenuButton(settings, "设置", false)) _menu.OpenSettings();

            var quit = new Rect(start.x, settings.yMax + gap, buttonWidth, buttonHeight);
            if (MenuButton(quit, "离开房间", false)) _menu.AskToQuit();

            GUI.Label(new Rect(width * 0.5f - 260f, height - 40f, 520f, 24f),
                "版本 " + _menu.Version + "　·　🐾 " + _menu.Coins.ToString("N0"), _centred);
        }

        /// <summary>
        /// A menu button: the project's rounded panel, a centred label, and an invisible button on top
        /// for the click.
        ///
        /// The rounded corners are <see cref="DshMobile.UiSkin"/>'s, like every other panel in the game —
        /// this screen is the first thing a player sees, and four default grey rectangles are a strange
        /// way to introduce a game whose whole look is generated from code.
        /// </summary>
        private bool MenuButton(Rect rect, string label, bool primary)
        {
            // Amber for 进入房间 and a dark slate for the rest: the room's palette is wood and lamplight,
            // and a purple "primary" button (the first version) looked like it belonged to another game.
            var fill = primary
                ? new Color(0.87f, 0.64f, 0.27f, 0.97f)
                : new Color(0.13f, 0.12f, 0.16f, 0.88f);
            var border = primary
                ? new Color(1f, 0.95f, 0.80f, 0.85f)
                : new Color(1f, 1f, 1f, 0.22f);
            var labelStyle = primary ? _buttonPrimary : _button;

            DshMobile.UiSkin.Panel(rect, 16f, fill, border, 1.7f);
            GUI.Label(rect, label, labelStyle);

            return GUI.Button(rect, GUIContent.none, Invisible);
        }

        private GUIStyle _buttonPrimary;
        private GUIStyle _invisible;
        private GUIStyle _pill;

        /// <summary>Draws nothing at all: the label and the panel behind it are the button.</summary>
        private static GUIStyle Invisible
            => new GUIStyle { normal = { background = null } };

        // ------------------------------------------------------------------ settings

        private void DrawSettings(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 250f, height * 0.12f, 500f, Mathf.Min(600f, height * 0.78f));
            DshMobile.UiSkin.Panel(panel, 18f, new Color(0.10f, 0.11f, 0.16f, 0.94f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 22f, panel.y + 16f, panel.width - 44f, 36f), "设置", _title);

            float y = panel.y + 68f;

            bool music = Toggle(panel, ref y, "背景音乐", DshMobile.MobileMusic.Enabled,
                "每个场景一首循环曲子；关掉它不影响音效和宠物的叫声。");
            if (music != DshMobile.MobileMusic.Enabled) DshMobile.MobileMusic.Enabled = music;
            MusicVolume(panel, ref y);

            bool speech = Toggle(panel, ref y, "朗读宠物的话", DshMobile.MobileTts.Enabled,
                "宠物说的话会用手机自带的语音念出来（只在安卓上有）。");
            if (speech != DshMobile.MobileTts.Enabled)
            {
                DshMobile.MobileTts.Enabled = speech;
                if (speech) DshMobile.MobileTts.WarmUp();
                else DshMobile.MobileTts.Stop();
            }

            bool voice = Toggle(panel, ref y, "显示麦克风按钮", DshMobile.MobileStt.Enabled,
                "聊天框旁边出现麦克风，按一下说话，识别到的字会填进输入框。");
            if (voice != DshMobile.MobileStt.Enabled) DshMobile.MobileStt.Enabled = voice;

            bool sound = Toggle(panel, ref y, "音效", !PetAudioDirector.MutedSetting,
                "走路的脚步声、吃饭的声音、宠物的叫声。");
            if (sound == PetAudioDirector.MutedSetting) PetAudioDirector.MutedSetting = !sound;

            bool haptics = Toggle(panel, ref y, "震动反馈", DshMobile.MobileHaptics.Enabled,
                "换道、跳跃、宠物把球叼回来时轻轻震一下。");
            if (haptics != DshMobile.MobileHaptics.Enabled) DshMobile.MobileHaptics.Enabled = haptics;

            GUI.Label(new Rect(panel.x + 22f, y + 4f, panel.width - 44f, 60f),
                "这些开关在房间里随时都能改（设置面板里也有）；这里只是让你在进门之前就能调好。",
                _small);

            GUI.Label(new Rect(panel.x + 22f, y + 40f, panel.width - 44f, 40f),
                DshMobile.MobileMusic.NowPlayingText, _small);

            if (GUI.Button(new Rect(panel.x + 24f, panel.yMax - 62f, 180f, 44f), "返回", _button))
            {
                _menu.Cancel();
            }
        }

        /// <summary>
        /// The music volume, under its switch.
        ///
        /// A slider rather than only on/off: the right level for music is a matter of taste and of
        /// where the phone is (a pocket, a quiet room, a train), so the setting people actually
        /// reach for is "quieter", not "off" — and a game that only offers off gets muted forever.
        /// </summary>
        private void MusicVolume(Rect panel, ref float y)
        {
            float volume = DshMobile.MobileMusic.Volume;

            GUI.Label(new Rect(panel.x + 26f, y, 140f, 26f),
                "音量 " + Mathf.RoundToInt(volume * 100f) + "%", _small);

            var slider = new Rect(panel.x + 170f, y + 4f, panel.width - 200f, 24f);
            float next = GUI.HorizontalSlider(slider, volume, 0f, 1f);
            if (!Mathf.Approximately(next, volume)) DshMobile.MobileMusic.Volume = next;

            y += 40f;
        }

        /// <summary>
        /// A settings row: the label, a line of explanation, and a switch on the right that says 开 or 关.
        ///
        /// Drawn rather than left to <c>GUI.Toggle</c>: the default tick box is a small grey square against a
        /// dark panel, and on a phone at this size it is not possible to tell whether a setting is on.
        /// </summary>
        private bool Toggle(Rect panel, ref float y, string label, bool value, string hint)
        {
            var row = new Rect(panel.x + 18f, y, panel.width - 36f, 60f);

            var pill = new Rect(row.xMax - 96f, row.y + 6f, 96f, 38f);
            DshMobile.UiSkin.Panel(pill, 19f,
                value ? new Color(0.34f, 0.60f, 0.33f, 0.96f) : new Color(0.22f, 0.22f, 0.27f, 0.92f),
                new Color(1f, 1f, 1f, value ? 0.45f : 0.16f), 1.5f);
            GUI.Label(pill, value ? "开" : "关", _pill);

            GUI.Label(new Rect(row.x + 4f, row.y + 2f, row.width - 112f, 26f), label, _body);

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(row.x + 4f, row.y + 26f, row.width - 112f, 36f), hint, _small);
            GUI.color = Color.white;

            y += 68f;
            return Toggled(value, GUI.Button(row, GUIContent.none, Invisible));
        }

        /// <summary>
        /// What a switch becomes when tapped: on → off, off → on, untapped → unchanged.
        ///
        /// A value rather than inline code because the first version returned the *tap* itself,
        /// and a switch that reads a tap as its new state flips to "off" and then cannot be turned
        /// back on — the tap is a transient, not a state. This is the whole of a switch's logic,
        /// so it is the whole of what needs to be right.
        /// </summary>
        public static bool Toggled(bool wasOn, bool tapped) => tapped ? !wasOn : wasOn;

        // ------------------------------------------------------------------ how to play

        private void DrawHowTo(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 260f, height * 0.10f, 520f, Mathf.Min(620f, height * 0.78f));
            DshMobile.UiSkin.Panel(panel, 18f, new Color(0.10f, 0.11f, 0.16f, 0.95f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 22f, panel.y + 14f, panel.width - 44f, 34f), "玩法介绍", _title);

            var view = new Rect(panel.x + 22f, panel.y + 58f, panel.width - 44f,
                panel.height - 58f - 62f);
            var content = new Rect(0f, 0f, view.width - 22f, HowToHeight);

            _howToScroll = GUI.BeginScrollView(view, _howToScroll, content);
            GUI.Label(content, HowToText(), _body);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(panel.x + 24f, panel.yMax - 54f, 180f, 42f), "返回", _button))
            {
                _menu.Cancel();
            }
        }

        /// <summary>Tall enough for the text; measured rather than guessed would need a layout pass.</summary>
        private const float HowToHeight = 1180f;

        private string HowToText()
        {
            return
                "【这是一只住在房间里的宠物】\n" +
                "它有饿、困、开心、干净和憋不住五个状态。你会看到它自己跑去吃饭、去睡觉、去猫砂盆，" +
                "也会在你很久没理它的时候凑过来。\n\n" +
                "【怎么照顾它】\n" +
                "· 左下的摇杆走路（手机上按屏幕左边拖动），走到东西旁边按「互动」；\n" +
                "· 点房间里任何一样东西，宠物会自己走过去用它；\n" +
                "· 点宠物本身可以摸它，它会按性格和心情做出反应；\n" +
                "· 聊天框在底部，打字就能和它说话；手机上有麦克风，说话也行。\n\n" +
                "【它听得懂短指令】\n" +
                "「过来」「别动」「跟着我」「拿球」「吃饭」「喝水」「陪我玩」「去睡觉」「上厕所」" +
                "「洗澡」「梳毛」「饭碗在哪」——打字或说话都行，本地识别，不花 token。\n" +
                "疑问句和陈述句不会被当成指令：「你吃饭了吗」「我今天吃了饭」它只会和你聊天。\n\n" +
                "【它看得见房间】\n" +
                "每一轮对话它都知道你和每样东西在哪个方位、大概几步远，所以问「饭碗在哪」它会真的回答。\n\n" +
                "【门是地图，也是游戏厅】\n" +
                "房间里的门可以换地方（小屋 / 花园 / 夜晚露台），也是去小游戏的入口：\n" +
                "· 小鸟飞行：点一下扇翅膀钻管子；\n" +
                "· 跳一跳：按住蓄力松手跳，主角就是你养的这只；\n" +
                "· 接果子：左右滑动接住水果；\n" +
                "· 切水果：滑动切水果，有炸弹，还有无尽模式和八关闯关；\n" +
                "· 弹弓小鸟：拉弓打猪，木块石块冰块都是有物理的，会倒会压死猪；\n" +
                "· 拼图与记忆配对：在房间里的面板上直接玩。\n\n" +
                "【宠物币】\n" +
                "小游戏和跑酷赚的币是同一份钱包，用来买新宠物、换地方、解锁东西。\n\n" +
                "【它记得你】\n" +
                "它有自己的记事本：聊过的重要事情会被记住，日历上能看到这些天发生了什么。\n\n" +
                "准备好了就点「进入房间」——门会自己开。";
        }

        // ------------------------------------------------------------------ quitting

        private void DrawQuit(float width, float height)
        {
            var panel = new Rect(width * 0.5f - 220f, height * 0.32f, 440f, 230f);
            DshMobile.UiSkin.Panel(panel, 18f, new Color(0.10f, 0.11f, 0.16f, 0.96f),
                new Color(1f, 1f, 1f, 0.16f), 1.5f);

            GUI.Label(new Rect(panel.x + 22f, panel.y + 18f, panel.width - 44f, 36f), "离开房间？", _title);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 66f, panel.width - 44f, 60f),
                "宠物会留在这里等你回来——它的需求、记忆和记事本都会保存。", _small);

            if (GUI.Button(new Rect(panel.x + 24f, panel.yMax - 72f, 180f, 48f), "离开", _button))
            {
                _menu.Quit();
            }

            if (GUI.Button(new Rect(panel.xMax - 204f, panel.yMax - 72f, 180f, 48f), "再待一会儿", _button))
            {
                _menu.Cancel();
            }

            if (Application.isEditor)
            {
                GUI.Label(new Rect(panel.x + 22f, panel.yMax - 100f, panel.width - 44f, 22f),
                    "（编辑器里不会真的退出，真机上会关掉应用）", _centred);
            }
        }

        // ------------------------------------------------------------------ the opening

        private void DrawFade(float width, float height)
        {
            float alpha = StartSequence.FadeAlpha(_menu.OpeningSeconds);
            if (alpha <= 0.001f) return;

            var was = GUI.color;
            GUI.color = new Color(1f, 0.98f, 0.94f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), _white);
            GUI.color = was;
        }
    }
}
