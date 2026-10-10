using UnityEngine;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// 窒息隧道's interface, now in uGUI: a menu (mode + theme + sensitivity + start), the single
    /// floating virtual stick, a score, and a result panel.
    ///
    /// The stick's *input* stays exactly where it was — the HUD's Update reads the mouse/touch and
    /// publishes <c>Stick</c> for the game. Only the *drawing* moved: the base and knob are two
    /// circle Images placed under the finger each frame via
    /// <c>RectTransformUtility.ScreenPointToLocalPointInRectangle</c>.
    /// </summary>
    public class TunnelHud : MonoBehaviour
    {
        private TunnelGame _game;
        private RectTransform _root;

        private TunnelMode _selectedMode = TunnelMode.Survival;
        private TunnelTheme _selectedTheme = TunnelTheme.Neon;

        /// <summary>The single stick input, -1..1 in each axis. Read by the game.</summary>
        public static Vector2 Stick { get; private set; }

        private static bool _dragging;
        private static bool _stickVisible;
        private static Vector2 _stickCentre;
        private const float StickRadius = 78f;
        private const float KnobHalf = 28f;

        private Image _stickBase;
        private Image _stickKnob;

        private GameObject _menu;
        private Text _sensitivityLabel;
        private Text _bestLine;
        private Text[] _modeLabels = new Text[2];
        private Text[] _themeLabels = new Text[4];

        private Text _score;
        private Text _hint;

        private GameObject _resultPanel;
        private Text _resultScore;
        private Text _resultLine;

        private bool _built;

        private static readonly Color TitleColor = new Color(0.8f, 0.95f, 1f);
        private static readonly Color Gold = new Color(1f, 0.92f, 0.62f);
        private static readonly Color Pale = new Color(0.92f, 0.92f, 0.98f);
        private static readonly Color Selected = new Color(1f, 0.9f, 0.4f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);

        private void Awake()
        {
            _game = GetComponent<TunnelGame>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("TunnelHud");

            // Stick visuals, drawn under the finger (children of the full-screen root).
            _stickBase = DshMobile.Ugui.Image("StickBase", _root, new Color(1f, 1f, 1f, 0.28f));
            _stickBase.sprite = DshMobile.UguiRounded.Circle(StickRadius, Color.white);
            _stickBase.rectTransform.anchorMin = _stickBase.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _stickBase.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _stickBase.rectTransform.sizeDelta = new Vector2(StickRadius * 2f, StickRadius * 2f);

            _stickKnob = DshMobile.Ugui.Image("StickKnob", _root, new Color(1f, 0.9f, 0.4f, 0.9f));
            _stickKnob.sprite = DshMobile.UguiRounded.Circle(KnobHalf, Color.white);
            _stickKnob.rectTransform.anchorMin = _stickKnob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _stickKnob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _stickKnob.rectTransform.sizeDelta = new Vector2(KnobHalf * 2f, KnobHalf * 2f);

            _stickBase.gameObject.SetActive(false);
            _stickKnob.gameObject.SetActive(false);

            var back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            back.onClick.AddListener(OnBack);

            BuildMenu();
            BuildRunning();
            BuildResult();

            _built = true;
        }

        private void BuildMenu()
        {
            var go = new GameObject("Menu", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 560f, 480f);
            _menu = go;
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "窒息隧道", 30, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 60f, 0f, 440f, 56f);

            var sub = DshMobile.Ugui.Text("Subtitle", m, "一根摇杆上下左右，穿过门洞、躲开障碍。", 16, Pale,
                TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(sub.rectTransform, 60f, 56f, 440f, 24f);

            var modeLabel = DshMobile.Ugui.Text("ModeLabel", m, "玩法", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(modeLabel.rectTransform, 60f, 92f, 440f, 24f);

            var mode0 = DshMobile.Ugui.Button("Mode0", m, "生存 · 穿门洞", 17, BackTint);
            DshMobile.Ugui.SetRect(mode0.GetComponent<RectTransform>(), 100f, 122f, 160f, 44f);
            _modeLabels[0] = mode0.GetComponentInChildren<Text>();
            mode0.onClick.AddListener(() => SelectMode(TunnelMode.Survival));

            var mode1 = DshMobile.Ugui.Button("Mode1", m, "矿洞 · 躲障碍", 17, BackTint);
            DshMobile.Ugui.SetRect(mode1.GetComponent<RectTransform>(), 300f, 122f, 160f, 44f);
            _modeLabels[1] = mode1.GetComponentInChildren<Text>();
            mode1.onClick.AddListener(() => SelectMode(TunnelMode.Mine));

            var themeLabel = DshMobile.Ugui.Text("ThemeLabel", m, "隧道主题", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(themeLabel.rectTransform, 60f, 180f, 440f, 24f);

            string[] themeNames = { "霓虹", "冰窟", "熔岩", "绿林" };
            TunnelTheme[] themes = { TunnelTheme.Neon, TunnelTheme.Ice, TunnelTheme.Lava, TunnelTheme.Forest };
            float[] xs = { 20f, 150f, 280f, 410f };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var tb = DshMobile.Ugui.Button("Theme" + i, m, themeNames[i], 17, BackTint);
                DshMobile.Ugui.SetRect(tb.GetComponent<RectTransform>(), xs[i], 210f, 120f, 40f);
                _themeLabels[i] = tb.GetComponentInChildren<Text>();
                tb.onClick.AddListener(() => SelectTheme(themes[index]));
            }

            _sensitivityLabel = DshMobile.Ugui.Text("SensLabel", m, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_sensitivityLabel.rectTransform, 60f, 266f, 440f, 24f);

            var slider = DshMobile.Ugui.Slider("Sensitivity", m, 0.5f, 2f, _game.Sensitivity,
                new Color(0.35f, 0.6f, 0.9f), Color.white);
            DshMobile.Ugui.SetRect(slider.GetComponent<RectTransform>(), 100f, 294f, 360f, 24f);
            slider.onValueChanged.AddListener(v => { if (_game != null) _game.Sensitivity = v; });

            var start = DshMobile.Ugui.Button("Start", m, "开始", 18, GreenTint);
            DshMobile.Ugui.SetRect(start.GetComponent<RectTransform>(), 150f, 332f, 260f, 54f);
            start.onClick.AddListener(OnStart);

            _bestLine = DshMobile.Ugui.Text("Best", m, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_bestLine.rectTransform, 60f, 402f, 440f, 24f);
        }

        private void BuildRunning()
        {
            _score = DshMobile.Ugui.Text("Score", _root, "0", 52, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(400f, 52f));

            _hint = DshMobile.Ugui.Text("Hint", _root, "按住屏幕任意处拖动，控制飞船上下左右", 16, Pale,
                TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -62f), new Vector2(560f, 24f));
        }

        private void BuildResult()
        {
            var go = new GameObject("ResultPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 440f, 360f);

            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);

            var title = DshMobile.Ugui.Text("Title", go.transform, "撞上了", 30, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 20f, 400f, 54f);

            _resultScore = DshMobile.Ugui.Text("Score", go.transform, "0", 52, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_resultScore.rectTransform, 20f, 84f, 400f, 90f);

            _resultLine = DshMobile.Ugui.Text("Line", go.transform, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_resultLine.rectTransform, 20f, 176f, 400f, 24f);

            var retry = DshMobile.Ugui.Button("Retry", go.transform, "再来一次", 17, GreenTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 70f, 220f, 300f, 50f);
            retry.onClick.AddListener(OnStart);

            var back = DshMobile.Ugui.Button("BackToMenu", go.transform, "换玩法 / 主题", 17, BackTint);
            DshMobile.Ugui.SetRect(back.GetComponent<RectTransform>(), 70f, 280f, 300f, 50f);
            back.onClick.AddListener(OnBackToMenu);

            _resultPanel = go;
        }

        private void Update()
        {
            if (_game == null || !_built) return;
            ReadStick();
            SyncDisplay();
        }

        private void ReadStick()
        {
            if (_game.State != TunnelGame.Phase.Running)
            {
                _dragging = false;
                _stickVisible = false;
                Stick = Vector2.zero;
                return;
            }

            Vector2 mouse = Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && mouse.y < Screen.height - 90f)
            {
                _dragging = true;
                _stickVisible = true;
                _stickCentre = mouse;
                Stick = Vector2.zero;
            }

            if (_dragging && Input.GetMouseButton(0))
            {
                Vector2 delta = (Vector2)Input.mousePosition - _stickCentre;
                Stick = Vector2.ClampMagnitude(delta / StickRadius, 1f);
                if (Stick.magnitude < 0.08f) Stick = Vector2.zero;
            }

            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
                _stickVisible = false;
                Stick = Vector2.zero;
            }

            // Draw the stick under the finger.
            _stickBase.gameObject.SetActive(_stickVisible);
            _stickKnob.gameObject.SetActive(_stickVisible);
            if (_stickVisible)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, _stickCentre, null, out local))
                {
                    _stickBase.rectTransform.anchoredPosition = local;
                    _stickKnob.rectTransform.anchoredPosition =
                        local + new Vector2(Stick.x * StickRadius, Stick.y * StickRadius);
                }
            }
        }

        private void SyncDisplay()
        {
            bool menu = _game.State == TunnelGame.Phase.Menu;
            bool running = _game.State == TunnelGame.Phase.Running;
            bool dead = _game.State == TunnelGame.Phase.Dead;

            _menu.SetActive(menu);
            _score.gameObject.SetActive(running);
            _hint.gameObject.SetActive(running);
            _resultPanel.SetActive(dead);

            if (menu)
            {
                _sensitivityLabel.text = "灵敏度　" + Mathf.RoundToInt(_game.Sensitivity * 100f) + "%";
                _bestLine.text = $"最高 {_game.Best}　·　宠物币 {DshMobile.PetWallet.Coins:N0}";
            }

            if (running)
            {
                _score.text = _game.Score.ToString();
            }

            if (dead)
            {
                _resultScore.text = _game.Score.ToString();
                _resultLine.text = $"最高 {_game.Best}　·　本局赚了 {_game.RunCoins} 币";
            }

            RefreshSelection();
        }

        private void SelectMode(TunnelMode mode)
        {
            _selectedMode = mode;
            RefreshSelection();
        }

        private void SelectTheme(TunnelTheme theme)
        {
            _selectedTheme = theme;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            _modeLabels[0].color = _selectedMode == TunnelMode.Survival ? Selected : Color.white;
            _modeLabels[1].color = _selectedMode == TunnelMode.Mine ? Selected : Color.white;
            TunnelTheme[] themes = { TunnelTheme.Neon, TunnelTheme.Ice, TunnelTheme.Lava, TunnelTheme.Forest };
            for (int i = 0; i < 4; i++)
                _themeLabels[i].color = _selectedTheme == themes[i] ? Selected : Color.white;
        }

        private void OnBack() => _game?.ReturnToRoom();
        private void OnStart() { if (_game != null) _game.StartRun(_selectedMode, _selectedTheme); }
        private void OnBackToMenu() => _game?.BackToMenu();
    }
}
