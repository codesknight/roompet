using UnityEngine;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// 棱镜's interface, now in uGUI: a menu that picks difficulty and mode, a live score, and a
    /// result panel.
    ///
    /// The board pads are world objects (see <see cref="PrismPad"/>), not UI, so the only input
    /// change is that a pad tap is ignored while the pointer is over a uGUI element — the game's
    /// <c>PrismPad.OnMouseDown</c> now checks <c>IsPointerOverGameObject</c> instead of the old
    /// <c>PointerOverPanel</c> flag.
    /// </summary>
    public class PrismHud : MonoBehaviour
    {
        private PrismGame _game;
        private RectTransform _root;

        private PrismDifficulty _selectedDifficulty = PrismDifficulty.Rainbow;
        private PrismMode _selectedMode = PrismMode.Classic;

        private GameObject _menu;
        private Button[] _difficultyButtons = new Button[3];
        private Text[] _difficultyLabels = new Text[3];
        private Text[] _modeLabels = new Text[2];
        private Text _bestLine;

        private Text _score;
        private Text _progress;
        private Text _rushClock;

        private GameObject _resultPanel;
        private Text _resultScore;
        private Text _resultLine;

        private bool _built;

        private static readonly Color TitleColor = new Color(0.9f, 0.86f, 1f);
        private static readonly Color Gold = new Color(1f, 0.92f, 0.62f);
        private static readonly Color Pale = new Color(0.92f, 0.92f, 0.98f);
        private static readonly Color Selected = new Color(1f, 0.9f, 0.4f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);

        private void Awake()
        {
            _game = GetComponent<PrismGame>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("PrismHud");

            var back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            back.onClick.AddListener(OnBack);

            BuildMenu();
            BuildPlaying();
            BuildResult();

            _built = true;
        }

        private static Color DifficultyColor(PrismDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PrismDifficulty.Spectrum: return new Color(0.30f, 0.75f, 0.92f);
                case PrismDifficulty.Prism: return new Color(0.72f, 0.45f, 0.95f);
                default: return new Color(0.98f, 0.72f, 0.22f);
            }
        }

        private static Color ModeColor(PrismMode mode)
            => mode == PrismMode.Rush ? new Color(0.95f, 0.45f, 0.36f) : new Color(0.38f, 0.82f, 0.50f);

        private void BuildMenu()
        {
            var go = new GameObject("Menu", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 520f, 420f);
            _menu = go;
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "棱镜 · 序列记忆", 30, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 40f, 0f, 440f, 60f);

            var sub = DshMobile.Ugui.Text("Subtitle", m, "看、听、复现。点错一个就结束。", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(sub.rectTransform, 40f, 62f, 440f, 24f);

            var diffLabel = DshMobile.Ugui.Text("DiffLabel", m, "难度", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(diffLabel.rectTransform, 40f, 100f, 440f, 24f);

            string[] diffNames = { "彩虹", "光谱", "棱镜" };
            PrismDifficulty[] diffs = { PrismDifficulty.Rainbow, PrismDifficulty.Spectrum, PrismDifficulty.Prism };
            float[] xs = { 10f, 185f, 360f };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var btn = DshMobile.Ugui.Button("Diff" + i, m, diffNames[i], 17, DifficultyColor(diffs[i]));
                DshMobile.Ugui.SetRect(btn.GetComponent<RectTransform>(), xs[i], 130f, 150f, 46f);
                _difficultyButtons[i] = btn;
                _difficultyLabels[i] = btn.GetComponentInChildren<Text>();
                btn.onClick.AddListener(() => SelectDifficulty(diffs[index]));
            }

            var modeLabel = DshMobile.Ugui.Text("ModeLabel", m, "模式", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(modeLabel.rectTransform, 40f, 190f, 440f, 24f);

            var mode0 = DshMobile.Ugui.Button("Mode0", m, "经典（无时限）", 17, ModeColor(PrismMode.Classic));
            DshMobile.Ugui.SetRect(mode0.GetComponent<RectTransform>(), 80f, 220f, 160f, 44f);
            _modeLabels[0] = mode0.GetComponentInChildren<Text>();
            mode0.onClick.AddListener(() => SelectMode(PrismMode.Classic));

            var mode1 = DshMobile.Ugui.Button("Mode1", m, "竞速（限时）", 17, ModeColor(PrismMode.Rush));
            DshMobile.Ugui.SetRect(mode1.GetComponent<RectTransform>(), 280f, 220f, 160f, 44f);
            _modeLabels[1] = mode1.GetComponentInChildren<Text>();
            mode1.onClick.AddListener(() => SelectMode(PrismMode.Rush));

            var start = DshMobile.Ugui.Button("Start", m, "开始", 18, GreenTint);
            DshMobile.Ugui.SetRect(start.GetComponent<RectTransform>(), 130f, 284f, 260f, 54f);
            start.onClick.AddListener(OnStart);

            _bestLine = DshMobile.Ugui.Text("Best", m, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_bestLine.rectTransform, 40f, 352f, 440f, 24f);

            var unlock = DshMobile.Ugui.Text("Unlock", m, "光谱需最高 8 · 棱镜需最高 15（达到后解锁）", 16, Pale,
                TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(unlock.rectTransform, 40f, 380f, 440f, 24f);
        }

        private void BuildPlaying()
        {
            _score = DshMobile.Ugui.Text("Score", _root, "0", 52, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(400f, 54f));

            _progress = DshMobile.Ugui.Text("Progress", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_progress.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -66f), new Vector2(440f, 24f));

            _rushClock = DshMobile.Ugui.Text("RushClock", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_rushClock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(440f, 30f));
        }

        private void BuildResult()
        {
            var go = new GameObject("ResultPanel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), 440f, 320f);

            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);

            var title = DshMobile.Ugui.Text("Title", go.transform, "结束", 30, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 20f, 400f, 54f);

            _resultScore = DshMobile.Ugui.Text("Score", go.transform, "0", 52, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_resultScore.rectTransform, 20f, 78f, 400f, 90f);

            var sub = DshMobile.Ugui.Text("Sub", go.transform, "坚持到的序列长度", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(sub.rectTransform, 20f, 170f, 400f, 24f);

            _resultLine = DshMobile.Ugui.Text("Line", go.transform, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_resultLine.rectTransform, 20f, 200f, 400f, 24f);

            var retry = DshMobile.Ugui.Button("Retry", go.transform, "再来一次", 17, GreenTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 70f, 240f, 300f, 50f);
            retry.onClick.AddListener(OnStart);

            var back = DshMobile.Ugui.Button("BackToMenu", go.transform, "换难度 / 模式", 17, BackTint);
            DshMobile.Ugui.SetRect(back.GetComponent<RectTransform>(), 70f, 290f, 300f, 44f);
            back.onClick.AddListener(OnBackToMenu);

            _resultPanel = go;
        }

        private void Update()
        {
            if (_game == null || !_built) return;

            bool menu = _game.State == PrismGame.Phase.Menu;
            bool playing = _game.State == PrismGame.Phase.Playback || _game.State == PrismGame.Phase.Input ||
                           _game.State == PrismGame.Phase.Feedback;
            bool result = _game.State == PrismGame.Phase.Result;

            _menu.SetActive(menu);
            _score.gameObject.SetActive(playing);
            _progress.gameObject.SetActive(playing);
            _resultPanel.SetActive(result);

            if (menu)
            {
                _bestLine.text = $"最高 {_game.Best}　·　宠物币 {DshMobile.PetWallet.Coins:N0}";
                RefreshMenuSelection();
            }

            if (playing)
            {
                _score.text = _game.CurrentLength.ToString();
                string progress = _game.State == PrismGame.Phase.Input || _game.State == PrismGame.Phase.Feedback
                    ? "已对 " + _game.InputStep + " / " + _game.CurrentLength
                    : (_game.State == PrismGame.Phase.Playback ? "看它亮起的顺序……" : "");
                _progress.text = progress;

                bool rush = _game.Mode == PrismMode.Rush && _game.State == PrismGame.Phase.Input;
                _rushClock.gameObject.SetActive(rush);
                if (rush) _rushClock.text = "剩余 " + Mathf.CeilToInt(_game.RushClock) + " 秒";
            }

            if (result)
            {
                _resultScore.text = _game.Score.ToString();
                _resultLine.text = $"最高 {_game.Best}　·　本局赚了 {_game.RunCoins} 币";
            }
        }

        private void SelectDifficulty(PrismDifficulty difficulty) => _selectedDifficulty = difficulty;

        private void SelectMode(PrismMode mode) => _selectedMode = mode;

        private void RefreshMenuSelection()
        {
            int unlocked = (int)PrismRules.UnlockedFor(_game.Best);
            PrismDifficulty[] diffs = { PrismDifficulty.Rainbow, PrismDifficulty.Spectrum, PrismDifficulty.Prism };
            for (int i = 0; i < 3; i++)
            {
                bool locked = (int)diffs[i] > unlocked;
                _difficultyButtons[i].interactable = !locked;
                _difficultyLabels[i].text = locked ? DifficultyName(diffs[i]) + "（未解锁）" : DifficultyName(diffs[i]);
                _difficultyLabels[i].color = !locked && _selectedDifficulty == diffs[i] ? Selected : Color.white;
            }

            _modeLabels[0].color = _selectedMode == PrismMode.Classic ? Selected : Color.white;
            _modeLabels[1].color = _selectedMode == PrismMode.Rush ? Selected : Color.white;
        }

        private static string DifficultyName(PrismDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PrismDifficulty.Spectrum: return "光谱";
                case PrismDifficulty.Prism: return "棱镜";
                default: return "彩虹";
            }
        }

        private void OnBack() => _game?.ReturnToRoom();
        private void OnStart() { if (_game != null) _game.StartRun(_selectedDifficulty, _selectedMode); }
        private void OnBackToMenu() => _game?.BackToMenu();
    }
}
