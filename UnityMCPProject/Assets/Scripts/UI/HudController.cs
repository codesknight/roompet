using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DshRunner
{
    /// <summary>
    /// The runner's HUD and menus, now in uGUI (migrated in round 41, the eighth screen).
    ///
    /// Movement is still gesture-only and read by the game through <see cref="DshMobile.MobileTouch"/>
    /// (the HUD just keeps <c>PlayInputEnabled</c> in step with the game state); this class only
    /// draws the score/progress/power-up HUD, the pause button, and the menu / level-select /
    /// pause / game-over / level-complete panels. Keyboard menu shortcuts moved from OnGUI's
    /// event loop to a plain <c>Input.GetKeyDown</c> check in Update.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private RectTransform _root;

        private GameObject _menu;
        private Text _menuBest;
        private Text _menuDistance;
        private Text _menuCoins;
        private GameObject _menuReturn;

        private GameObject _levelSelect;
        private readonly List<Button> _levelButtons = new List<Button>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private GameObject _levelSelectReturn;

        private GameObject _hud;
        private Text _hudScore;
        private Text _hudDistance;
        private Text _hudCoins;
        private Text _hudSpeed;
        private GameObject _progressBar;
        private RectTransform _progressFill;
        private Text _hint;
        private Button _pauseButton;
        private Button _returnButton;
        private readonly List<GameObject> _pills = new List<GameObject>();
        private readonly List<RectTransform> _pillFills = new List<RectTransform>();
        private readonly List<Text> _pillLabels = new List<Text>();

        private GameObject _paused;
        private GameObject _pausedReturn;

        private GameObject _gameOver;
        private Text _gameOverReason;
        private Text _gameOverScore;
        private Text _gameOverDetail;
        private Text _gameOverBest;
        private GameObject _gameOverReturn;

        private GameObject _levelComplete;
        private Text _levelCompleteScore;
        private Text _levelCompleteDetail;
        private GameObject _nextButton;
        private GameObject _levelCompleteReturn;

        private GameObject _gestureHint;

        private bool _built;

        private static readonly Color TitleColor = new Color(0.75f, 0.95f, 1f);
        private static readonly Color HudSmall = new Color(0.85f, 0.9f, 1f);
        private static readonly Color PanelFill = new Color(0.09f, 0.10f, 0.13f, 0.97f);
        private static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);

        private const string AwayFlagKey = "dshpet.away";
        private const string RoomSceneName = "PetRoom";

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("HudController");
            BuildMenu();
            BuildLevelSelect();
            BuildHud();
            BuildPaused();
            BuildGameOver();
            BuildLevelComplete();
            BuildGestureHint();
            _built = true;
        }

        private static bool ReturnToRoomVisible => PlayerPrefs.GetInt(AwayFlagKey, 0) == 1;

        private GameObject Panel(string name, float w, float h, float radius = 18f, bool shadow = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), w, h);
            var bg = DshMobile.Ugui.Panel("Bg", go.transform, radius, PanelFill, PanelBorder, 2f);
            DshMobile.Ugui.Stretch(bg.rectTransform);
            return go;
        }

        // ------------------------------------------------------------------- menu

        private void BuildMenu()
        {
            var go = Panel("Menu", 460f, 470f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "森 林 奔 跑", 44, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 24f, 20f, 412f, 52f);

            var sub = DshMobile.Ugui.Text("Sub", m, "FOREST RUNNER · 换道 / 跳跃 / 滑铲 / 道具", 18, Color.white, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(sub.rectTransform, 24f, 78f, 412f, 26f);

            _menuBest = DshMobile.Ugui.Text("Best", m, "", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_menuBest.rectTransform, 24f, 122f, 412f, 24f);
            _menuDistance = DshMobile.Ugui.Text("Distance", m, "", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_menuDistance.rectTransform, 24f, 148f, 412f, 24f);
            _menuCoins = DshMobile.Ugui.Text("Coins", m, "", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_menuCoins.rectTransform, 24f, 174f, 412f, 24f);

            var endless = DshMobile.Ugui.Button("Endless", m, "无尽模式  (Enter)", 20, GreenTint);
            DshMobile.Ugui.SetRect(endless.GetComponent<RectTransform>(), 24f, 214f, 412f, 48f);
            endless.onClick.AddListener(OnEndless);

            var levels = DshMobile.Ugui.Button("Levels", m, "关卡模式  (L)", 20, BackTint);
            DshMobile.Ugui.SetRect(levels.GetComponent<RectTransform>(), 24f, 274f, 412f, 48f);
            levels.onClick.AddListener(OnLevelSelect);

            var reset = DshMobile.Ugui.Button("Reset", m, "重置进度", 15, BackTint);
            DshMobile.Ugui.SetRect(reset.GetComponent<RectTransform>(), 24f, 334f, 412f, 34f);
            reset.onClick.AddListener(OnReset);

            _menuReturn = DshMobile.Ugui.Button("Return", m, "返回宠物小屋", 20, BackTint).gameObject;
            DshMobile.Ugui.SetRect(_menuReturn.GetComponent<RectTransform>(), 24f, 390f, 412f, 44f);
            _menuReturn.GetComponent<Button>().onClick.AddListener(OnReturnToRoom);

            _menu = go;
        }

        private void BuildLevelSelect()
        {
            var go = Panel("LevelSelect", 720f, 560f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "选择关卡", 44, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 24f, 18f, 672f, 52f);

            for (int i = 1; i < LevelLibrary.Count; i++)
            {
                var btn = DshMobile.Ugui.Button("Level" + i, m, "", 20, BackTint);
                DshMobile.Ugui.SetRect(btn.GetComponent<RectTransform>(), 24f, 78f + (i - 1) * 48f, 672f, 42f);
                int index = i;
                btn.onClick.AddListener(() => OnStartLevel(index));
                _levelButtons.Add(btn);
                _levelLabels.Add(btn.GetComponentInChildren<Text>());
            }

            var back = DshMobile.Ugui.Button("BackToMenu", m, "返回主菜单  (Esc)", 20, BackTint);
            DshMobile.Ugui.SetRect(back.GetComponent<RectTransform>(), 24f, 78f + (LevelLibrary.Count - 1) * 48f + 12f, 672f, 44f);
            back.onClick.AddListener(OnBackToMenu);

            _levelSelectReturn = DshMobile.Ugui.Button("Return", m, "返回宠物小屋", 20, BackTint).gameObject;
            DshMobile.Ugui.SetRect(_levelSelectReturn.GetComponent<RectTransform>(), 24f, 78f + (LevelLibrary.Count - 1) * 48f + 64f, 672f, 44f);
            _levelSelectReturn.GetComponent<Button>().onClick.AddListener(OnReturnToRoom);

            _levelSelect = go;
        }

        private void BuildHud()
        {
            var go = new GameObject("Hud", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Stretch(go.GetComponent<RectTransform>());
            Transform m = go.transform;

            _hudScore = DshMobile.Ugui.Text("Score", m, "0", 22, Color.white, TextAnchor.UpperLeft, true);
            DshMobile.Ugui.SetRect(_hudScore.rectTransform, 20f, 16f, 320f, 28f);

            _hudDistance = DshMobile.Ugui.Text("Distance", m, "", 16, HudSmall, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_hudDistance.rectTransform, 20f, 46f, 320f, 22f);

            _hudCoins = DshMobile.Ugui.Text("Coins", m, "", 16, HudSmall, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_hudCoins.rectTransform, 20f, 68f, 320f, 22f);

            _hudSpeed = DshMobile.Ugui.Text("Speed", m, "", 16, HudSmall, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_hudSpeed.rectTransform, 20f, 90f, 320f, 22f);

            // Level progress bar.
            _progressBar = new GameObject("ProgressBar", typeof(RectTransform));
            _progressBar.transform.SetParent(m, false);
            DshMobile.Ugui.Place(_progressBar.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(320f, 16f));
            var bg = DshMobile.Ugui.Image("Bg", _progressBar.transform, new Color(1f, 1f, 1f, 0.25f));
            DshMobile.Ugui.Stretch(bg.rectTransform);
            var fill = DshMobile.Ugui.Image("Fill", _progressBar.transform, new Color(0.4f, 0.95f, 1f, 0.95f));
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = new Vector2(0f, 0.5f);
            _progressFill.anchorMax = new Vector2(0f, 0.5f);
            _progressFill.pivot = new Vector2(0f, 0.5f);
            _progressFill.anchoredPosition = Vector2.zero;
            _progressFill.sizeDelta = new Vector2(0f, 16f);

            // Power-up pills, top-right.
            var kinds = new[] { PowerUpKind.Shield, PowerUpKind.Magnet, PowerUpKind.DoubleScore, PowerUpKind.SlowMotion };
            for (int i = 0; i < kinds.Length; i++)
            {
                var pill = new GameObject("Pill", typeof(RectTransform));
                pill.transform.SetParent(m, false);
                DshMobile.Ugui.Place(pill.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                    new Vector2(1f, 1f), new Vector2(-240f, -(16f + i * 32f)), new Vector2(220f, 26f));
                var pillBg = DshMobile.Ugui.Image("Bg", pill.transform, new Color(1f, 1f, 1f, 0.2f));
                DshMobile.Ugui.Stretch(pillBg.rectTransform);
                var pillFill = DshMobile.Ugui.Image("Fill", pill.transform, PowerUpTint(kinds[i]));
                var pillFillRt = pillFill.rectTransform;
                pillFillRt.anchorMin = new Vector2(0f, 0.5f);
                pillFillRt.anchorMax = new Vector2(0f, 0.5f);
                pillFillRt.pivot = new Vector2(0f, 0.5f);
                pillFillRt.anchoredPosition = Vector2.zero;
                pillFillRt.sizeDelta = new Vector2(0f, 26f);
                var pillLabel = DshMobile.Ugui.Text("Label", pill.transform, "", 15, Color.white, TextAnchor.MiddleLeft);
                DshMobile.Ugui.Stretch(pillLabel.rectTransform);
                pillLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
                pillLabel.rectTransform.offsetMax = Vector2.zero;
                _pills.Add(pill);
                _pillFills.Add(pillFillRt);
                _pillLabels.Add(pillLabel);
            }

            _hint = DshMobile.Ugui.Text("Hint", m, "", 16, HudSmall, TextAnchor.MiddleLeft);
            DshMobile.Ugui.Place(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 16f), new Vector2(700f, 24f));

            _pauseButton = DshMobile.Ugui.Button("Pause", m, "暂停", 17, BackTint);
            DshMobile.Ugui.Place(_pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(64f, 44f));
            _pauseButton.onClick.AddListener(OnPause);

            _returnButton = DshMobile.Ugui.Button("Return", m, "返回宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(_returnButton.GetComponent<RectTransform>(), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(168f, 44f));
            _returnButton.onClick.AddListener(OnReturnToRoom);

            _hud = go;
        }

        private void BuildPaused()
        {
            var go = Panel("Paused", 360f, 300f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "暂停", 44, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 24f, 24f, 312f, 52f);

            var resume = DshMobile.Ugui.Button("Resume", m, "继续  (Esc)", 20, GreenTint);
            DshMobile.Ugui.SetRect(resume.GetComponent<RectTransform>(), 24f, 92f, 312f, 44f);
            resume.onClick.AddListener(OnPause);

            var restart = DshMobile.Ugui.Button("Restart", m, "重新开始  (R)", 20, BackTint);
            DshMobile.Ugui.SetRect(restart.GetComponent<RectTransform>(), 24f, 144f, 312f, 44f);
            restart.onClick.AddListener(OnRestart);

            var menu = DshMobile.Ugui.Button("Menu", m, "返回主菜单", 20, BackTint);
            DshMobile.Ugui.SetRect(menu.GetComponent<RectTransform>(), 24f, 196f, 312f, 44f);
            menu.onClick.AddListener(OnBackToMenu);

            _pausedReturn = DshMobile.Ugui.Button("Return", m, "返回宠物小屋", 20, BackTint).gameObject;
            DshMobile.Ugui.SetRect(_pausedReturn.GetComponent<RectTransform>(), 24f, 248f, 312f, 44f);
            _pausedReturn.GetComponent<Button>().onClick.AddListener(OnReturnToRoom);

            _paused = go;
        }

        private void BuildGameOver()
        {
            var go = Panel("GameOver", 440f, 380f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "游戏结束", 44, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 24f, 22f, 392f, 52f);

            _gameOverReason = DshMobile.Ugui.Text("Reason", m, "", 18, Color.white, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_gameOverReason.rectTransform, 24f, 78f, 392f, 26f);

            _gameOverScore = DshMobile.Ugui.Text("Score", m, "", 22, Color.white, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_gameOverScore.rectTransform, 24f, 116f, 392f, 30f);

            _gameOverDetail = DshMobile.Ugui.Text("Detail", m, "", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_gameOverDetail.rectTransform, 24f, 150f, 392f, 24f);

            _gameOverBest = DshMobile.Ugui.Text("Best", m, "", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_gameOverBest.rectTransform, 24f, 176f, 392f, 24f);

            var retry = DshMobile.Ugui.Button("Retry", m, "再来一次  (R)", 20, GreenTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 24f, 214f, 392f, 46f);
            retry.onClick.AddListener(OnRestart);

            var menu = DshMobile.Ugui.Button("Menu", m, "返回主菜单", 20, BackTint);
            DshMobile.Ugui.SetRect(menu.GetComponent<RectTransform>(), 24f, 268f, 392f, 46f);
            menu.onClick.AddListener(OnBackToMenu);

            _gameOverReturn = DshMobile.Ugui.Button("Return", m, "返回宠物小屋", 20, BackTint).gameObject;
            DshMobile.Ugui.SetRect(_gameOverReturn.GetComponent<RectTransform>(), 24f, 322f, 392f, 46f);
            _gameOverReturn.GetComponent<Button>().onClick.AddListener(OnReturnToRoom);

            _gameOver = go;
        }

        private void BuildLevelComplete()
        {
            var go = Panel("LevelComplete", 460f, 400f);
            Transform m = go.transform;

            var title = DshMobile.Ugui.Text("Title", m, "通关！", 44, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 24f, 22f, 412f, 52f);

            _levelCompleteScore = DshMobile.Ugui.Text("Score", m, "", 22, Color.white, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_levelCompleteScore.rectTransform, 24f, 84f, 412f, 30f);

            _levelCompleteDetail = DshMobile.Ugui.Text("Detail", m, "", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_levelCompleteDetail.rectTransform, 24f, 118f, 412f, 24f);

            _nextButton = DshMobile.Ugui.Button("Next", m, "下一关  (Enter)", 20, GreenTint).gameObject;
            DshMobile.Ugui.SetRect(_nextButton.GetComponent<RectTransform>(), 24f, 156f, 412f, 46f);
            _nextButton.GetComponent<Button>().onClick.AddListener(OnNext);

            var replay = DshMobile.Ugui.Button("Replay", m, "重玩本关  (R)", 20, BackTint);
            DshMobile.Ugui.SetRect(replay.GetComponent<RectTransform>(), 24f, 210f, 412f, 46f);
            replay.onClick.AddListener(OnRestart);

            var menu = DshMobile.Ugui.Button("Menu", m, "返回主菜单", 20, BackTint);
            DshMobile.Ugui.SetRect(menu.GetComponent<RectTransform>(), 24f, 264f, 412f, 46f);
            menu.onClick.AddListener(OnBackToMenu);

            _levelCompleteReturn = DshMobile.Ugui.Button("Return", m, "返回宠物小屋", 20, BackTint).gameObject;
            DshMobile.Ugui.SetRect(_levelCompleteReturn.GetComponent<RectTransform>(), 24f, 318f, 412f, 46f);
            _levelCompleteReturn.GetComponent<Button>().onClick.AddListener(OnReturnToRoom);

            _levelComplete = go;
        }

        private void BuildGestureHint()
        {
            var go = Panel("GestureHint", 480f, 120f);
            Transform m = go.transform;

            var big = DshMobile.Ugui.Text("Big", m, "滑动屏幕来操作", 26, TitleColor, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(big.rectTransform, 0f, 14f, 480f, 30f);

            var line1 = DshMobile.Ugui.Text("Line1", m, "← → 换道　　↑ 或点一下 跳跃（按住更高）　　↓ 滑铲", 20, Color.white, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(line1.rectTransform, 0f, 52f, 480f, 26f);

            var line2 = DshMobile.Ugui.Text("Line2", m, "也可以直接点屏幕跳跃", 16, HudSmall, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(line2.rectTransform, 0f, 80f, 480f, 24f);

            _gestureHint = go;
        }

        private const float GestureHintSeconds = 5.5f;
        private static float _gestureHintUntil;
        private static bool _gestureHintShown;

        private void Update()
        {
            if (!_built) return;
            var gm = GameManager.Instance;
            if (gm == null)
            {
                _menu.SetActive(false);
                _levelSelect.SetActive(false);
                _hud.SetActive(false);
                _paused.SetActive(false);
                _gameOver.SetActive(false);
                _levelComplete.SetActive(false);
                _gestureHint.SetActive(false);
                return;
            }

            DshMobile.MobileTouch.PlayInputEnabled = gm.State == GameState.Playing;

            bool playing = gm.State == GameState.Playing;
            _menu.SetActive(gm.State == GameState.Menu);
            _levelSelect.SetActive(gm.State == GameState.LevelSelect);
            _hud.SetActive(playing || gm.State == GameState.Paused);
            _paused.SetActive(gm.State == GameState.Paused);
            _gameOver.SetActive(gm.State == GameState.GameOver);
            _levelComplete.SetActive(gm.State == GameState.LevelComplete);
            _gestureHint.SetActive(playing && GestureHintActive());

            bool returnVisible = ReturnToRoomVisible;
            _menuReturn.SetActive(returnVisible);
            _levelSelectReturn.SetActive(returnVisible);
            _pausedReturn.SetActive(returnVisible);
            _gameOverReturn.SetActive(returnVisible);
            _levelCompleteReturn.SetActive(returnVisible);

            // Touch pause button only on a phone.
            bool touch = DshMobile.MobileUi.UseTouchControls;
            _pauseButton.gameObject.SetActive(touch && playing);

            switch (gm.State)
            {
                case GameState.Menu: SyncMenu(gm); HandleMenuKeys(gm); break;
                case GameState.LevelSelect: SyncLevelSelect(gm); HandleLevelSelectKeys(gm); break;
                case GameState.Playing: SyncHud(gm); break;
                case GameState.Paused: SyncHud(gm); break;
                case GameState.GameOver: SyncGameOver(gm); HandleRetryKey(gm); break;
                case GameState.LevelComplete: SyncLevelComplete(gm); HandleLevelCompleteKeys(gm); break;
            }
        }

        private void SyncMenu(GameManager gm)
        {
            _menuBest.text = $"最高分  {ProgressStore.BestScore:N0}";
            _menuDistance.text = $"最远距离  {ProgressStore.BestDistance:F0} m";
            _menuCoins.text = $"累计果子  {ProgressStore.TotalCoins:N0}";
        }

        private void SyncLevelSelect(GameManager gm)
        {
            for (int i = 0; i < _levelButtons.Count; i++)
            {
                int index = i + 1;
                var level = LevelLibrary.Get(index);
                bool unlocked = index <= ProgressStore.UnlockedLevels;
                int best = ProgressStore.LevelBest(index);
                _levelButtons[i].interactable = unlocked;
                _levelLabels[i].text = unlocked
                    ? $"{level.Name}   ·   {level.Blurb}   ·   目标 {level.TargetDistance:F0} m   ·   最佳 {best:N0}"
                    : $"{level.Name}   ·   未解锁（先通关第 {index - 1} 关）";
            }
        }

        private void SyncHud(GameManager gm)
        {
            var score = gm.Score.Snapshot();
            _hudScore.text = $"{score.Score:N0}";
            _hudDistance.text = $"{score.Distance:F0} m   倍率 x{score.Multiplier:F2}";
            _hudCoins.text = $"果子 {score.Coins}";
            _hudSpeed.text = $"速度 {gm.CurrentSpeed:F1} m/s";

            bool hasProgress = gm.Level != null && !gm.Level.Endless;
            _progressBar.SetActive(hasProgress);
            if (hasProgress)
            {
                float progress = Mathf.Clamp01(Difficulty.Progress(gm.Level, score.Distance));
                _progressFill.sizeDelta = new Vector2(320f * progress, 16f);
            }

            bool touch = DshMobile.MobileUi.UseTouchControls;
            _hint.text = touch
                ? "滑动换道 · 上滑或点击跳跃 · 下滑滑铲"
                : "A/D 或 ←/→ 换道    W/↑/空格 跳跃    S/↓ 滑铲    P/Esc 暂停";
            _hint.gameObject.SetActive(true);
            _returnButton.gameObject.SetActive(ReturnToRoomVisible);

            SyncPowerUps(gm);
        }

        private void SyncPowerUps(GameManager gm)
        {
            var powerUps = gm.PowerUps;
            var kinds = new[] { PowerUpKind.Shield, PowerUpKind.Magnet, PowerUpKind.DoubleScore, PowerUpKind.SlowMotion };
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                if (powerUps == null)
                {
                    _pills[i].SetActive(false);
                    continue;
                }
                float remaining = powerUps.Remaining(kind);
                bool active = kind == PowerUpKind.Shield ? powerUps.ShieldActive : remaining > 0f;
                _pills[i].SetActive(active);
                if (!active) continue;
                _pillFills[i].sizeDelta = new Vector2(220f * powerUps.Normalised(kind), 26f);
                _pillLabels[i].text = $"{powerUps.Describe(kind)}  {remaining:F1}s";
            }
        }

        private void SyncGameOver(GameManager gm)
        {
            var score = gm.Score.Snapshot();
            _gameOverReason.gameObject.SetActive(!string.IsNullOrEmpty(gm.LastFailReason));
            _gameOverReason.text = gm.LastFailReason;
            _gameOverScore.text = $"本次得分  {score.Score:N0}";
            _gameOverDetail.text = $"距离  {score.Distance:F0} m      金币  {score.Coins}";
            _gameOverBest.text = $"最高分  {ProgressStore.BestScore:N0}";
        }

        private void SyncLevelComplete(GameManager gm)
        {
            var score = gm.Score.Snapshot();
            bool hasNext = gm.Level != null && !gm.Level.Endless && gm.Level.Index + 1 < LevelLibrary.Count;
            _levelCompleteScore.text = $"本次得分  {score.Score:N0}";
            _levelCompleteDetail.text = $"距离  {score.Distance:F0} m      金币  {score.Coins}";
            _nextButton.SetActive(hasNext);
        }

        private bool GestureHintActive()
        {
            if (!DshMobile.MobileUi.UseTouchControls) return false;
            if (!_gestureHintShown) return false;
            float remaining = _gestureHintUntil - Time.unscaledTime;
            return remaining > 0f;
        }

        private void ArmGestureHint()
        {
            if (!DshMobile.MobileUi.UseTouchControls) return;
            if (_gestureHintShown) return;
            _gestureHintShown = true;
            _gestureHintUntil = Time.unscaledTime + GestureHintSeconds;
        }

        private void HandleMenuKeys(GameManager gm)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) gm.StartRun(LevelLibrary.Endless);
            else if (Input.GetKeyDown(KeyCode.L)) gm.OpenLevelSelect();
        }

        private void HandleLevelSelectKeys(GameManager gm)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) gm.ReturnToMenu();
        }

        private void HandleRetryKey(GameManager gm)
        {
            if (Input.GetKeyDown(KeyCode.R)) gm.RestartRun();
            if (Input.GetKeyDown(KeyCode.Escape)) gm.ReturnToMenu();
        }

        private void HandleLevelCompleteKeys(GameManager gm)
        {
            if (Input.GetKeyDown(KeyCode.R)) gm.RestartRun();
            bool hasNext = gm.Level != null && !gm.Level.Endless && gm.Level.Index + 1 < LevelLibrary.Count;
            if (hasNext && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) gm.NextLevel();
        }

        private static Color PowerUpTint(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield: return new Color(0.35f, 0.85f, 1f, 0.85f);
                case PowerUpKind.Magnet: return new Color(1f, 0.55f, 0.2f, 0.85f);
                case PowerUpKind.DoubleScore: return new Color(1f, 0.9f, 0.3f, 0.85f);
                case PowerUpKind.SlowMotion: return new Color(0.6f, 0.6f, 1f, 0.85f);
                default: return new Color(1f, 1f, 1f, 0.85f);
            }
        }

        private void OnEndless() { var gm = GameManager.Instance; if (gm != null) { gm.StartRun(LevelLibrary.Endless); ArmGestureHint(); } }
        private void OnLevelSelect() { var gm = GameManager.Instance; if (gm != null) gm.OpenLevelSelect(); }
        private void OnReset() => ProgressStore.ResetAll();
        private void OnStartLevel(int index) { var gm = GameManager.Instance; if (gm != null) { gm.StartRun(LevelLibrary.Get(index)); ArmGestureHint(); } }
        private void OnBackToMenu() { var gm = GameManager.Instance; if (gm != null) gm.ReturnToMenu(); }
        private void OnPause() { var gm = GameManager.Instance; if (gm != null) gm.TogglePause(); }
        private void OnRestart() { var gm = GameManager.Instance; if (gm != null) { gm.RestartRun(); ArmGestureHint(); } }
        private void OnNext() { var gm = GameManager.Instance; if (gm != null) gm.NextLevel(); }

        /// <summary>
        /// Goes back to the pet room and clears the "away" flag, so the pet's HUD stops offering
        /// to bring the player home. Kept static: it is part of the runner's small public surface.
        /// </summary>
        public static void ReturnToRoom()
        {
            DshMobile.SceneClock.Restore("leaving the run");
            PlayerPrefs.SetInt(AwayFlagKey, 0);
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(RoomSceneName);
        }

        private void OnReturnToRoom() => ReturnToRoom();
    }
}
