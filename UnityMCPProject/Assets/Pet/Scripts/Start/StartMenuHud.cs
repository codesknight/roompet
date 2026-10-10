using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DshPet
{
    /// <summary>
    /// The front door's interface, now in uGUI: a title, four buttons, and the settings / how-to /
    /// quit panels behind them. The settings switches are pill buttons that say 开/关 (a drawn switch
    /// rather than Unity's default toggle, for the same reason the IMGUI version drew them: a small
    /// grey checkbox is invisible at phone size), and the how-to text scrolls through a ScrollRect.
    /// </summary>
    public class StartMenuHud : MonoBehaviour
    {
        private StartMenu _menu;
        private RectTransform _root;

        private GameObject _menuPanel;
        private Text _menuSubtitle;
        private Text _menuVersion;

        private GameObject _settingsPanel;
        private readonly List<Text> _pillLabels = new List<Text>();
        private readonly List<System.Func<bool>> _toggleGetters = new List<System.Func<bool>>();
        private readonly List<System.Action<bool>> _toggleSetters = new List<System.Action<bool>>();
        private Text _volumeLabel;
        private Slider _volumeSlider;
        private Text _nowPlaying;

        private GameObject _howToPanel;

        private GameObject _starterPanel;
        private Text _starterStatus;

        private GameObject _quitPanel;

        private Image _fade;

        private bool _built;

        private static readonly Color TitleColor = new Color(1f, 0.96f, 0.86f);
        private static readonly Color SubtitleColor = new Color(1f, 0.92f, 0.82f, 0.92f);
        private static readonly Color BodyColor = new Color(0.96f, 0.96f, 0.99f);
        private static readonly Color SmallColor = new Color(0.90f, 0.92f, 0.97f, 0.9f);
        private static readonly Color PrimaryTint = new Color(0.80f, 0.58f, 0.22f);
        private static readonly Color SlateTint = new Color(0.30f, 0.32f, 0.44f);

        private void Awake()
        {
            _menu = GetComponent<StartMenu>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("StartMenuHud");

            _fade = DshMobile.Ugui.Image("Fade", _root, new Color(1f, 0.98f, 0.94f, 0f));
            DshMobile.Ugui.Stretch(_fade.rectTransform);
            _fade.raycastTarget = false;
            _fade.gameObject.SetActive(false);

            BuildMenu();
            BuildStarter();
            BuildSettings();
            BuildHowTo();
            BuildQuit();

            _built = true;
        }

        private GameObject Panel(string name, float w, float h, float radius = 18f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), w, h);
            var bg = DshMobile.Ugui.Panel("Bg", go.transform, radius,
                new Color(0.10f, 0.11f, 0.16f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);
            return go;
        }

        private void BuildMenu()
        {
            var go = new GameObject("Menu", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Stretch(go.GetComponent<RectTransform>());
            Transform m = go.transform;

            var header = DshMobile.Ugui.Panel("Header", m, 22f,
                new Color(0.12f, 0.10f, 0.14f, 0.44f), new Color(1f, 0.92f, 0.78f, 0.18f), 1.4f);
            DshMobile.Ugui.Place(header.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(560f, 116f));

            var title = DshMobile.Ugui.Text("Title", m, "宠物小屋", 46, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -72f), new Vector2(520f, 60f));

            _menuSubtitle = DshMobile.Ugui.Text("Subtitle", m, "", 18, SubtitleColor, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_menuSubtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(520f, 34f));

            float buttonWidth = 400f;
            float buttonHeight = 66f;
            float gap = 18f;
            float total = buttonHeight * 4f + gap * 3f;
            float y = total; // measured from the bottom edge upward

            var start = DshMobile.Ugui.Button("Start", m, "进入房间", 25, PrimaryTint);
            DshMobile.Ugui.Place(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(buttonWidth, buttonHeight));
            start.onClick.AddListener(() => _menu.Begin());

            var howTo = DshMobile.Ugui.Button("HowTo", m, "玩法介绍", 22, SlateTint);
            DshMobile.Ugui.Place(howTo.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, y - buttonHeight - gap), new Vector2(buttonWidth, buttonHeight));
            howTo.onClick.AddListener(() => _menu.OpenHowTo());

            var settings = DshMobile.Ugui.Button("Settings", m, "设置", 22, SlateTint);
            DshMobile.Ugui.Place(settings.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, y - (buttonHeight + gap) * 2f), new Vector2(buttonWidth, buttonHeight));
            settings.onClick.AddListener(() => _menu.OpenSettings());

            var quit = DshMobile.Ugui.Button("Quit", m, "离开房间", 22, SlateTint);
            DshMobile.Ugui.Place(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, y - (buttonHeight + gap) * 3f), new Vector2(buttonWidth, buttonHeight));
            quit.onClick.AddListener(() => _menu.AskToQuit());

            _menuVersion = DshMobile.Ugui.Text("Version", m, "", 14, SmallColor, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_menuVersion.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(520f, 24f));

            _menuPanel = go;
        }

        private void BuildStarter()
        {
            var go = Panel("Starter", 620f, 560f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "选择你的宠物", 40, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 22f, 20f, 576f, 46f);

            var hint = DshMobile.Ugui.Text("Hint", m,
                "三只里先带一只回家。它会是主要照顾的那只；另外两只以后能用宠物币领回家。", 15,
                SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(hint.rectTransform, 22f, 74f, 576f, 44f);

            float y = 128f;
            foreach (string id in PetOnboarding.StarterChoices)
            {
                var species = PetSpecies.Get(id);
                y = BuildStarterRow(m, y, species);
            }

            _starterStatus = DshMobile.Ugui.Text("Status", m, "选好后点「进入房间」，它会一直在房间里陪你。", 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_starterStatus.rectTransform, 22f, y + 4f, 576f, 30f);

            _starterPanel = go;
        }

        private float BuildStarterRow(Transform m, float y, PetSpecies species)
        {
            var row = new GameObject("StarterRow", typeof(RectTransform));
            row.transform.SetParent(m, false);
            DshMobile.Ugui.SetRect(row.GetComponent<RectTransform>(), 18f, y, 584f, 76f);

            var dot = DshMobile.Ugui.Image("Dot", row.transform, species.Fur);
            DshMobile.Ugui.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(2f, -2f), new Vector2(24f, 24f));

            var name = DshMobile.Ugui.Text("Name", row.transform,
                species.DisplayName + "　" + species.Blurb, 16, BodyColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(name.rectTransform, 34f, 2f, 430f, 44f);

            var pick = DshMobile.Ugui.Button("Pick", row.transform, "带它回家", 18, PrimaryTint);
            DshMobile.Ugui.SetRect(pick.GetComponent<RectTransform>(), 476f, 8f, 104f, 44f);

            string captured = species.Id;
            pick.onClick.AddListener(() =>
            {
                if (PetOnboarding.ChooseStarter(captured))
                {
                    DshMobile.MobileHaptics.Medium();
                }
            });

            return y + 82f;
        }

        private void BuildSettings()
        {
            var go = Panel("Settings", 500f, 600f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "设置", 46, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 22f, 16f, 456f, 36f);

            float y = 68f;
            BuildToggle(m, ref y, "背景音乐", "每个场景一首循环曲子；关掉它不影响音效和宠物的叫声。",
                () => DshMobile.MobileMusic.Enabled, v => DshMobile.MobileMusic.Enabled = v);
            BuildVolume(m, ref y);
            BuildToggle(m, ref y, "朗读宠物的话", "宠物说的话会用手机自带的语音念出来（只在安卓上有）。",
                () => DshMobile.MobileTts.Enabled, v =>
                {
                    DshMobile.MobileTts.Enabled = v;
                    if (v) DshMobile.MobileTts.WarmUp();
                    else DshMobile.MobileTts.Stop();
                });
            BuildToggle(m, ref y, "显示麦克风按钮", "聊天框旁边出现麦克风，按一下说话，识别到的字会填进输入框。",
                () => DshMobile.MobileStt.Enabled, v => DshMobile.MobileStt.Enabled = v);
            BuildToggle(m, ref y, "音效", "走路的脚步声、吃饭的声音、宠物的叫声。",
                () => !PetAudioDirector.MutedSetting, v => PetAudioDirector.MutedSetting = !v);
            BuildToggle(m, ref y, "震动反馈", "换道、跳跃、宠物把球叼回来时轻轻震一下。",
                () => DshMobile.MobileHaptics.Enabled, v => DshMobile.MobileHaptics.Enabled = v);

            var footnote = DshMobile.Ugui.Text("Footnote", m,
                "这些开关在房间里随时都能改（设置面板里也有）；这里只是让你在进门之前就能调好。", 14,
                SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(footnote.rectTransform, 22f, y + 4f, 456f, 60f);

            _nowPlaying = DshMobile.Ugui.Text("NowPlaying", m, "", 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_nowPlaying.rectTransform, 22f, y + 40f, 456f, 40f);

            var back = DshMobile.Ugui.Button("Back", m, "返回", 20, SlateTint);
            DshMobile.Ugui.SetRect(back.GetComponent<RectTransform>(), 24f, 540f, 180f, 44f);
            back.onClick.AddListener(() => _menu.Cancel());

            _settingsPanel = go;
        }

        private void BuildToggle(Transform m, ref float y, string label, string hint,
            System.Func<bool> getter, System.Action<bool> setter)
        {
            var row = new GameObject("Toggle", typeof(RectTransform));
            row.transform.SetParent(m, false);
            DshMobile.Ugui.SetRect(row.GetComponent<RectTransform>(), 18f, y, 464f, 60f);

            var labelText = DshMobile.Ugui.Text("Label", row.transform, label, 16, BodyColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(labelText.rectTransform, 4f, 2f, 352f, 26f);

            var hintText = DshMobile.Ugui.Text("Hint", row.transform, hint, 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(hintText.rectTransform, 4f, 26f, 352f, 36f);

            var pill = DshMobile.Ugui.Button("Pill", row.transform, "开", 19, new Color(0.34f, 0.60f, 0.33f));
            DshMobile.Ugui.SetRect(pill.GetComponent<RectTransform>(), 368f, 6f, 96f, 38f);
            var pillLabel = pill.GetComponentInChildren<Text>();
            _pillLabels.Add(pillLabel);
            _toggleGetters.Add(getter);
            _toggleSetters.Add(setter);

            int index = _pillLabels.Count - 1;
            pill.onClick.AddListener(() =>
            {
                var current = _toggleGetters[index]();
                _toggleSetters[index](!current);
            });

            y += 68f;
        }

        private void BuildVolume(Transform m, ref float y)
        {
            var label = DshMobile.Ugui.Text("VolumeLabel", m, "", 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(label.rectTransform, 26f, y, 140f, 26f);
            _volumeLabel = label;

            _volumeSlider = DshMobile.Ugui.Slider("Volume", m, 0f, 1f, DshMobile.MobileMusic.Volume,
                new Color(0.35f, 0.6f, 0.9f), Color.white);
            DshMobile.Ugui.SetRect(_volumeSlider.GetComponent<RectTransform>(), 170f, y + 4f, 300f, 24f);
            _volumeSlider.onValueChanged.AddListener(v => { if (_built) DshMobile.MobileMusic.Volume = v; });

            y += 40f;
        }

        private void BuildHowTo()
        {
            var go = Panel("HowTo", 520f, 620f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "玩法介绍", 46, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 22f, 14f, 476f, 34f);

            // Scrollable body.
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(m, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            DshMobile.Ugui.SetRect(viewportRt, 22f, 58f, 476f, 500f);
            viewport.AddComponent<RectMask2D>();
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(-22f, HowToHeight);

            var body = DshMobile.Ugui.Text("Text", content.transform, HowToText(), 16, BodyColor, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            DshMobile.Ugui.Stretch(body.rectTransform);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var back = DshMobile.Ugui.Button("Back", m, "返回", 20, SlateTint);
            DshMobile.Ugui.SetRect(back.GetComponent<RectTransform>(), 24f, 566f, 180f, 42f);
            back.onClick.AddListener(() => _menu.Cancel());

            _howToPanel = go;
        }

        private void BuildQuit()
        {
            var go = Panel("Quit", 440f, 230f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "离开房间？", 46, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 22f, 18f, 396f, 36f);

            var body = DshMobile.Ugui.Text("Body", m,
                "宠物会留在这里等你回来——它的需求、记忆和记事本都会保存。", 14, SmallColor, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(body.rectTransform, 22f, 66f, 396f, 60f);

            var leave = DshMobile.Ugui.Button("Leave", m, "离开", 20, PrimaryTint);
            DshMobile.Ugui.SetRect(leave.GetComponent<RectTransform>(), 24f, 158f, 180f, 48f);
            leave.onClick.AddListener(() => _menu.Quit());

            var stay = DshMobile.Ugui.Button("Stay", m, "再待一会儿", 20, SlateTint);
            DshMobile.Ugui.SetRect(stay.GetComponent<RectTransform>(), 236f, 158f, 180f, 48f);
            stay.onClick.AddListener(() => _menu.Cancel());

            _quitPanel = go;
        }

        private const float HowToHeight = 1180f;

        /// <summary>
        /// What a switch becomes when tapped: on → off, off → on, untapped → unchanged. Kept as a
        /// public pure function because the switch's whole logic is tested directly.
        /// </summary>
        public static bool Toggled(bool wasOn, bool tapped) => tapped ? !wasOn : wasOn;

        private void Update()
        {
            if (_menu == null || !_built) return;

            bool menu = _menu.Phase == StartPhase.Menu;
            bool settings = _menu.Phase == StartPhase.Settings;
            bool howTo = _menu.Phase == StartPhase.HowTo;
            bool quit = _menu.Phase == StartPhase.Quit;
            bool fading = _menu.Phase == StartPhase.Opening || _menu.Phase == StartPhase.Leaving;

            // The front door asks the starter question exactly once: a new player picks one of
            // three before the menu (and the room behind it) is reachable.
            bool starter = menu && !PetOnboarding.HasChosenStarter;

            _menuPanel.SetActive(menu && !starter);
            _starterPanel.SetActive(starter);
            _settingsPanel.SetActive(settings);
            _howToPanel.SetActive(howTo);
            _quitPanel.SetActive(quit);

            if (menu)
            {
                _menuSubtitle.text = "会记事、听得懂你说话的" + _menu.PetName + "，在门里面等你。";
                _menuVersion.text = "版本 " + _menu.Version + "　·　宠物币 " + _menu.Coins.ToString("N0");
            }

            if (settings)
            {
                for (int i = 0; i < _pillLabels.Count; i++)
                {
                    bool on = _toggleGetters[i]();
                    _pillLabels[i].text = on ? "开" : "关";
                }
                _volumeLabel.text = "音量 " + Mathf.RoundToInt(DshMobile.MobileMusic.Volume * 100f) + "%";
                _nowPlaying.text = DshMobile.MobileMusic.NowPlayingText;
            }

            // Opening / leaving fade.
            float alpha = StartSequence.FadeAlpha(_menu.OpeningSeconds);
            _fade.gameObject.SetActive(alpha > 0.001f);
            if (alpha > 0.001f)
            {
                var c = _fade.color;
                c.a = alpha;
                _fade.color = c;
            }
        }

        private static string HowToText()
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
    }
}
