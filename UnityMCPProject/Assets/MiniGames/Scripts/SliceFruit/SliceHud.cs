using UnityEngine;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// 切水果's interface — the score, the lives, the mode, the panels and the buttons — now in
    /// uGUI, migrated in round 41 like FlyBird. The blade is NOT here: it is a world-space ribbon
    /// owned by <see cref="SliceGame"/> (see its <c>BuildBlade</c>), which is why that part of the
    /// old IMGUI HUD simply disappears instead of being redrawn.
    ///
    /// Input note: unlike FlyBird there is no full-screen tap target to migrate, because the game's
    /// input is a *swipe* read directly by <see cref="SliceGame.ReadBlade"/>, not a button. The
    /// swipe needs movement, so a plain tap on any of these buttons never cuts a fruit; the game
    /// keeps reading its blade exactly as before and only the drawing moved.
    /// </summary>
    public class SliceHud : MonoBehaviour
    {
        private SliceGame _game;
        private RectTransform _root;

        private Text _score;
        private Text _livesMode;
        private Text _info;
        private Text _popup;

        private Button _back;
        private Button _pause;
        private Text _pauseLabel;
        private Button _mode;
        private Text _modeLabel;

        private GameObject _pausedPanel;
        private Text _pausedInfo;
        private Text _pausedRetryLabel;

        private GameObject _readyPanel;
        private Text _readyLine1;
        private Text _readyLine2;
        private Text _readyLine3;

        private GameObject _levelDonePanel;
        private Text _levelDoneTitle;
        private Text _levelDoneLine1;
        private Text _levelDoneLine2;
        private GameObject _againButton;    // "下一关" / "从头再来" / "再试一次" — label swaps
        private Text _againLabel;
        private GameObject _endlessButton;

        private GameObject _deadPanel;
        private Text _deadLine1;
        private Text _deadLine2;

        private bool _built;

        private static readonly Color Gold = new Color(1f, 0.97f, 0.82f);
        private static readonly Color Cream = new Color(1f, 0.98f, 0.9f);
        private static readonly Color Pale = new Color(0.96f, 0.97f, 1f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);
        private static readonly Color PanelFill = new Color(0.10f, 0.08f, 0.13f, 0.95f);
        private static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.16f);

        private void Awake()
        {
            _game = GetComponent<SliceGame>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("SliceHud");

            _score = DshMobile.Ugui.Text("Score", _root, "0", 52, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(440f, 60f));

            _livesMode = DshMobile.Ugui.Text("LivesMode", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_livesMode.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -76f), new Vector2(600f, 22f));

            _info = DshMobile.Ugui.Text("Info", _root, "", 16, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(600f, 22f));

            _popup = DshMobile.Ugui.Text("Popup", _root, "", 26, new Color(1f, 0.92f, 0.6f),
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_popup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -60f), new Vector2(560f, 44f));

            // Top-right: back, then pause below it.
            _back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(_back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            _back.onClick.AddListener(OnBack);

            _pause = DshMobile.Ugui.Button("Pause", _root, "暂停", 17, BackTint);
            DshMobile.Ugui.Place(_pause.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -68f), new Vector2(168f, 44f));
            _pauseLabel = _pause.GetComponentInChildren<Text>();
            _pause.onClick.AddListener(OnPause);

            // Top-left: mode toggle.
            _mode = DshMobile.Ugui.Button("Mode", _root, "无尽模式", 17, BackTint);
            DshMobile.Ugui.Place(_mode.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(150f, 44f));
            _modeLabel = _mode.GetComponentInChildren<Text>();
            _mode.onClick.AddListener(OnMode);

            BuildPausedPanel();
            BuildReadyPanel();
            BuildLevelDonePanel();
            BuildDeadPanel();

            _built = true;
        }

        private GameObject PanelContainer(string name, float w, float h, out Image panelImage)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), w, h);
            panelImage = DshMobile.Ugui.Panel("Bg", go.transform, 16f, PanelFill, PanelBorder, 1.5f);
            DshMobile.Ugui.Stretch(panelImage.rectTransform);
            return go;
        }

        private void BuildPausedPanel()
        {
            Image bg;
            var go = PanelContainer("PausedPanel", 420f, 210f, out bg);

            var title = DshMobile.Ugui.Text("Title", go.transform, "暂停", 26, Cream, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 16f, 380f, 36f);

            _pausedInfo = DshMobile.Ugui.Text("Info", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_pausedInfo.rectTransform, 20f, 58f, 380f, 24f);

            var resume = DshMobile.Ugui.Button("Resume", go.transform, "继续", 17, GreenTint);
            DshMobile.Ugui.SetRect(resume.GetComponent<RectTransform>(), 24f, 104f, 180f, 46f);
            resume.onClick.AddListener(OnResume);

            var retry = DshMobile.Ugui.Button("RetryPaused", go.transform, "重新开始", 17, BackTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 216f, 104f, 180f, 46f);
            _pausedRetryLabel = retry.GetComponentInChildren<Text>();
            retry.onClick.AddListener(OnRetryPaused);

            var home = DshMobile.Ugui.Button("HomePaused", go.transform, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.SetRect(home.GetComponent<RectTransform>(), 24f, 156f, 372f, 46f);
            home.onClick.AddListener(OnBack);

            _pausedPanel = go;
        }

        private void BuildReadyPanel()
        {
            Image bg;
            var go = PanelContainer("ReadyPanel", 480f, 168f, out bg);

            var title = DshMobile.Ugui.Text("Title", go.transform, "切水果", 26, Cream, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 14f, 440f, 34f);

            _readyLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_readyLine1.rectTransform, 20f, 54f, 440f, 24f);

            _readyLine2 = DshMobile.Ugui.Text("Line2", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_readyLine2.rectTransform, 20f, 80f, 440f, 24f);

            _readyLine3 = DshMobile.Ugui.Text("Line3", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_readyLine3.rectTransform, 20f, 106f, 440f, 24f);

            _readyPanel = go;
        }

        private void BuildLevelDonePanel()
        {
            Image bg;
            var go = PanelContainer("LevelDonePanel", 480f, 240f, out bg);

            _levelDoneTitle = DshMobile.Ugui.Text("Title", go.transform, "", 26, Cream, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_levelDoneTitle.rectTransform, 20f, 14f, 440f, 36f);

            _levelDoneLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_levelDoneLine1.rectTransform, 20f, 56f, 440f, 24f);

            _levelDoneLine2 = DshMobile.Ugui.Text("Line2", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_levelDoneLine2.rectTransform, 20f, 82f, 440f, 24f);

            _againButton = DshMobile.Ugui.Button("Next", go.transform, "下一关", 17, GreenTint).gameObject;
            DshMobile.Ugui.SetRect(_againButton.GetComponent<RectTransform>(), 24f, 132f, 200f, 48f);
            _againLabel = _againButton.GetComponentInChildren<Text>();
            _againButton.GetComponent<Button>().onClick.AddListener(OnNextOrRetry);

            _endlessButton = DshMobile.Ugui.Button("Endless", go.transform, "无尽模式", 17, BackTint).gameObject;
            DshMobile.Ugui.SetRect(_endlessButton.GetComponent<RectTransform>(), 256f, 132f, 200f, 48f);
            _endlessButton.GetComponent<Button>().onClick.AddListener(OnEndless);

            _levelDonePanel = go;
        }

        private void BuildDeadPanel()
        {
            Image bg;
            var go = PanelContainer("DeadPanel", 480f, 250f, out bg);

            var title = DshMobile.Ugui.Text("Title", go.transform, "切完了", 26, Cream, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 14f, 440f, 36f);

            _deadLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine1.rectTransform, 20f, 56f, 440f, 24f);

            _deadLine2 = DshMobile.Ugui.Text("Line2", go.transform, "", 15, Pale, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_deadLine2.rectTransform, 20f, 82f, 440f, 24f);

            var retry = DshMobile.Ugui.Button("RetryDead", go.transform, "再来一次", 17, GreenTint);
            DshMobile.Ugui.SetRect(retry.GetComponent<RectTransform>(), 24f, 136f, 200f, 48f);
            retry.onClick.AddListener(OnRetryDead);

            var home = DshMobile.Ugui.Button("HomeDead", go.transform, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.SetRect(home.GetComponent<RectTransform>(), 256f, 136f, 200f, 48f);
            home.onClick.AddListener(OnBack);

            _deadPanel = go;
        }

        private void Update()
        {
            if (_game == null || !_built) return;

            _score.text = _game.Score.ToString();

            string lives = "";
            for (int i = 0; i < SliceRules.StartLives; i++) lives += i < _game.Lives ? "● " : "○ ";

            string mode = _game.Mode == SliceMode.Endless
                ? "无尽模式"
                : _game.Plan.Name + "　第 " + Mathf.Min(_game.Wave, _game.Plan.Waves) + "/" + _game.Plan.Waves + " 波";
            _livesMode.text = lives + "　·　" + mode;

            if (_game.Mode == SliceMode.Levels)
            {
                _info.text = "目标 " + _game.Plan.TargetScore + " 分　·　漏 " + _game.Missed + "/" + _game.Plan.MaxMisses +
                    (_game.Plan.BombChance > 0f ? "　·　有炸弹" : "");
            }
            else
            {
                _info.text = "最高 " + _game.Best + "　·　宠物币 " + DshMobile.PetWallet.Coins.ToString("N0");
            }

            // Popup (+n / 炸弹!).
            if (_game.PopupVisible && !string.IsNullOrEmpty(_game.Popup))
            {
                _popup.text = _game.Popup;
                _popup.color = _game.Popup.StartsWith("炸弹") ? new Color(1f, 0.5f, 0.42f) : new Color(1f, 0.92f, 0.6f);
                _popup.gameObject.SetActive(true);
            }
            else
            {
                _popup.gameObject.SetActive(false);
            }

            // Always-visible controls.
            _pauseLabel.text = _game.Paused ? "继续" : "暂停";
            _modeLabel.text = _game.Mode == SliceMode.Endless ? "无尽模式" : "闯关模式";
            bool modeVisible = _game.State != SliceGame.Phase.Dead && _game.State != SliceGame.Phase.LevelDone;
            _mode.gameObject.SetActive(modeVisible);

            // Panels.
            _pausedPanel.SetActive(_game.Paused);
            _readyPanel.SetActive(_game.State == SliceGame.Phase.Ready);
            _levelDonePanel.SetActive(_game.State == SliceGame.Phase.LevelDone);
            _deadPanel.SetActive(_game.State == SliceGame.Phase.Dead);

            if (_game.Paused)
            {
                _pausedInfo.text = "得分 " + _game.Score + "　·　漏了 " + _game.Missed + " 个";
                _pausedRetryLabel.text = _game.Mode == SliceMode.Levels ? "重玩这一关" : "重新开始";
            }

            if (_game.State == SliceGame.Phase.Ready)
            {
                if (_game.Mode == SliceMode.Endless)
                {
                    _readyLine1.text = DshMobile.MobileUi.UseTouchControls
                        ? "在屏幕上滑动切水果，别切到炸弹"
                        : "按住鼠标划过水果，别碰到炸弹";
                    _readyLine2.text = "漏掉一个水果或切到炸弹，就少一条命（三条）。";
                    _readyLine3.text = "水果越切越多、越来越快，看你能切到多少分。";
                }
                else
                {
                    _readyLine1.text = _game.Plan.Name + "：" + _game.Plan.Waves + " 波，每波 " + _game.Plan.FruitsPerWave + " 个";
                    _readyLine2.text = "目标 " + _game.Plan.TargetScore + " 分　·　最多漏 " + _game.Plan.MaxMisses + " 个";
                    _readyLine3.text = SliceRules.DifficultyFor(_game.Plan);
                }
            }

            if (_game.State == SliceGame.Phase.LevelDone)
            {
                bool passed = _game.LevelPassed;
                _levelDoneTitle.text = passed ? "过关！" : "没到目标分";
                _levelDoneLine1.text = _game.Plan.Name + "　·　得分 " + _game.Score + " / 目标 " + _game.Plan.TargetScore;
                _levelDoneLine2.text = "漏了 " + _game.Missed + " 个";

                if (passed && _game.Level < SliceRules.LevelCount)
                {
                    _againLabel.text = "下一关";
                    _againButton.SetActive(true);
                }
                else if (passed)
                {
                    _againLabel.text = "从头再来";
                    _againButton.SetActive(true);
                }
                else
                {
                    _againLabel.text = "再试一次";
                    _againButton.SetActive(true);
                }
            }

            if (_game.State == SliceGame.Phase.Dead)
            {
                _deadLine1.text = "得分 " + _game.Score + "　·　漏了 " + _game.Missed + " 个";
                _deadLine2.text = SliceRules.RankFor(_game.Score) + "　·　赚了 " + _game.RunCoins + " 个宠物币";
            }
        }

        private void OnBack() => _game?.ReturnToRoom();
        private void OnPause() => _game?.SetPaused(!_game.Paused);
        private void OnMode() => _game?.SetMode(_game.Mode == SliceMode.Endless ? SliceMode.Levels : SliceMode.Endless);
        private void OnResume() => _game?.SetPaused(false);
        private void OnRetryPaused() => _game?.ResetRun(_game.Mode);
        private void OnNextOrRetry()
        {
            if (_game == null) return;
            if (_game.LevelPassed && _game.Level < SliceRules.LevelCount) _game.NextLevel();
            else if (_game.LevelPassed) _game.SetMode(SliceMode.Levels);
            else _game.ResetRun(SliceMode.Levels);
        }
        private void OnEndless() => _game?.SetMode(SliceMode.Endless);
        private void OnRetryDead() => _game?.ResetRun(_game.Mode);
    }
}
