using UnityEngine;
using UnityEngine.UI;

namespace DshMiniGames
{
    /// <summary>
    /// 愤怒的小鸟's interface, now in uGUI: pigs left, birds left, the score, the aim-guide hint,
    /// and the level/result/pause/generating panels.
    ///
    /// The sling drag is NOT migrated (like Slice's blade it stays in the game); the only input
    /// change is that the game's <c>PointerWorld</c> now ignores the pointer while it is over a
    /// uGUI button, so tapping 暂停 / 重开这关 / 看提示 no longer also yanks the sling — the old
    /// IMGUI <c>PointerOverPanel</c> flag was set but never read, which is exactly that bug.
    /// </summary>
    public class AngryBirdsHud : MonoBehaviour
    {
        private AngryBirdsGame _game;
        private RectTransform _root;

        private Text _score;
        private Text _status;
        private Text _controlHint;

        private Button _pause;
        private Text _pauseLabel;
        private Button _hint;
        private Button _arc;
        private Text _arcLabel;

        private Text _hintText;

        private GameObject _generatingPanel;
        private Text _generatingLine;

        private GameObject _readyPanel;
        private Text _readyLine1;

        private GameObject _resultPanel;
        private Text _resultTitle;
        private Text _resultLine1;
        private Text _resultLine2;
        private Text _resultLine3;
        private GameObject _againButton;
        private Text _againLabel;

        private GameObject _pausedPanel;
        private Text _pausedLine1;

        private bool _built;
        private float _aimStartedAt = -1f;

        private static readonly Color Gold = new Color(1f, 0.97f, 0.82f);
        private static readonly Color Cream = new Color(1f, 0.98f, 0.9f);
        private static readonly Color White = new Color(1f, 1f, 1f);
        private static readonly Color HintYellow = new Color(1f, 0.94f, 0.6f);
        private static readonly Color BackTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);

        private void Awake()
        {
            _game = GetComponent<AngryBirdsGame>();
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("AngryBirdsHud");

            _score = DshMobile.Ugui.Text("Score", _root, "0", 46, Gold, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(280f, 46f));

            _status = DshMobile.Ugui.Text("Status", _root, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(560f, 22f));

            _controlHint = DshMobile.Ugui.Text("ControlHint", _root, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.Place(_controlHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -82f), new Vector2(560f, 22f));

            // Top-right: back, pause.
            var back = DshMobile.Ugui.Button("Back", _root, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.Place(back.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(168f, 44f));
            back.onClick.AddListener(OnBack);

            _pause = DshMobile.Ugui.Button("Pause", _root, "暂停", 17, BackTint);
            DshMobile.Ugui.Place(_pause.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -68f), new Vector2(168f, 44f));
            _pauseLabel = _pause.GetComponentInChildren<Text>();
            _pause.onClick.AddListener(OnPause);

            // Top-left: hint, restart, arc.
            _hint = DshMobile.Ugui.Button("Hint", _root, "看提示", 17, BackTint);
            DshMobile.Ugui.Place(_hint.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(130f, 44f));
            _hint.onClick.AddListener(OnHint);

            var restart = DshMobile.Ugui.Button("Restart", _root, "重开这关", 17, BackTint);
            DshMobile.Ugui.Place(restart.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -68f), new Vector2(130f, 44f));
            restart.onClick.AddListener(OnRestart);

            _arc = DshMobile.Ugui.Button("Arc", _root, "虚线：开", 17, BackTint);
            DshMobile.Ugui.Place(_arc.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -120f), new Vector2(130f, 44f));
            _arcLabel = _arc.GetComponentInChildren<Text>();
            _arc.onClick.AddListener(OnArc);

            _hintText = DshMobile.Ugui.Text("HintText", _root, "", 16, HintYellow,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.Place(_hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(560f, 44f));

            BuildGeneratingPanel();
            BuildReadyPanel();
            BuildResultPanel();
            BuildPausedPanel();

            _built = true;
        }

        private void BuildGeneratingPanel()
        {
            var go = Panel("GeneratingPanel", 460f, 168f);

            var title = DshMobile.Ugui.Text("Title", go.transform, "关卡生成中", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 16f, 420f, 34f);

            _generatingLine = DshMobile.Ugui.Text("Line", go.transform, "", 15, White,
                TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_generatingLine.rectTransform, 20f, 58f, 420f, 24f);

            var line2 = DshMobile.Ugui.Text("Line2", go.transform,
                "每一关都是随机搭的，但这一关必须先被机器打通过，才会交给你。", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(line2.rectTransform, 20f, 88f, 420f, 46f);

            _generatingPanel = go;
        }

        private void BuildReadyPanel()
        {
            var go = Panel("ReadyPanel", 500f, 116f);

            var title = DshMobile.Ugui.Text("Title", go.transform, "", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 18f, 10f, 464f, 30f);

            _readyLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_readyLine1.rectTransform, 18f, 44f, 464f, 24f);

            var line2 = DshMobile.Ugui.Text("Line2", go.transform,
                "每一关都是随机搭的，但都验证过一定打得通。", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(line2.rectTransform, 18f, 70f, 464f, 24f);

            _readyPanel = go;
        }

        private void BuildResultPanel()
        {
            var go = Panel("ResultPanel", 480f, 250f);

            _resultTitle = DshMobile.Ugui.Text("Title", go.transform, "", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_resultTitle.rectTransform, 20f, 14f, 440f, 36f);

            _resultLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_resultLine1.rectTransform, 20f, 56f, 440f, 24f);

            _resultLine2 = DshMobile.Ugui.Text("Line2", go.transform, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_resultLine2.rectTransform, 20f, 82f, 440f, 24f);

            _resultLine3 = DshMobile.Ugui.Text("Line3", go.transform, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_resultLine3.rectTransform, 20f, 112f, 440f, 24f);

            _againButton = DshMobile.Ugui.Button("Next", go.transform, "下一关", 17, GreenTint).gameObject;
            DshMobile.Ugui.SetRect(_againButton.GetComponent<RectTransform>(), 24f, 136f, 200f, 48f);
            _againLabel = _againButton.GetComponentInChildren<Text>();
            _againButton.GetComponent<Button>().onClick.AddListener(OnNextOrRetry);

            var home = DshMobile.Ugui.Button("Home", go.transform, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.SetRect(home.GetComponent<RectTransform>(), 256f, 136f, 200f, 48f);
            home.onClick.AddListener(OnBack);

            _resultPanel = go;
        }

        private void BuildPausedPanel()
        {
            var go = Panel("PausedPanel", 420f, 216f);

            var title = DshMobile.Ugui.Text("Title", go.transform, "暂停", 26, Cream,
                TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 20f, 16f, 380f, 36f);

            _pausedLine1 = DshMobile.Ugui.Text("Line1", go.transform, "", 15, White, TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_pausedLine1.rectTransform, 20f, 58f, 380f, 24f);

            var resume = DshMobile.Ugui.Button("Resume", go.transform, "继续", 17, GreenTint);
            DshMobile.Ugui.SetRect(resume.GetComponent<RectTransform>(), 24f, 106f, 180f, 46f);
            resume.onClick.AddListener(OnResume);

            var restart = DshMobile.Ugui.Button("RestartPaused", go.transform, "重开这关", 17, BackTint);
            DshMobile.Ugui.SetRect(restart.GetComponent<RectTransform>(), 216f, 106f, 180f, 46f);
            restart.onClick.AddListener(OnRestart);

            var home = DshMobile.Ugui.Button("HomePaused", go.transform, "回到宠物小屋", 17, BackTint);
            DshMobile.Ugui.SetRect(home.GetComponent<RectTransform>(), 24f, 158f, 372f, 46f);
            home.onClick.AddListener(OnBack);

            _pausedPanel = go;
        }

        private GameObject Panel(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            DshMobile.Ugui.Center(go.GetComponent<RectTransform>(), w, h);
            var bg = DshMobile.Ugui.Panel("Bg", go.transform, 16f,
                new Color(0.09f, 0.10f, 0.14f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);
            DshMobile.Ugui.Stretch(bg.rectTransform);
            return go;
        }

        private void Update()
        {
            if (_game == null || !_built) return;

            _score.text = _game.Score.ToString();

            var level = _game.Level;
            int pigsLeft = level == null ? 0 : level.PigsAlive;
            string birds = "";
            for (int i = 0; i < (level == null ? 0 : level.Birds); i++) birds += i < _game.BirdsLeft ? "● " : "○ ";
            _status.text = "第 " + _game.Stage + " 关　·　剩 " + pigsLeft + " 只猪　·　" + birds;

            _controlHint.text = DshMobile.MobileUi.UseTouchControls
                ? "按住小鸟往后拉，松手发射"
                : "按住鼠标往后拉，松手发射";

            _pauseLabel.text = _game.Paused ? "继续" : "暂停";
            _hint.gameObject.SetActive(_game.HasHint);
            _arcLabel.text = _game.ShowTrajectory ? "虚线：开" : "虚线：关";

            bool hintVisible = _game.HintVisible && !string.IsNullOrEmpty(_game.HintText);
            _hintText.gameObject.SetActive(hintVisible);
            if (hintVisible) _hintText.text = _game.HintText;

            bool generating = _game.Generating;
            bool paused = _game.Paused;
            // The level intro auto-dismisses: it stays through the Ready phase, then for ~2.5s of
            // Aiming, so the player gets a look at the level instead of having the panel sit there
            // until launch.
            if (_game.State == AngryBirdsGame.Phase.Aiming && _aimStartedAt < 0f) _aimStartedAt = Time.unscaledTime;
            if (_game.State != AngryBirdsGame.Phase.Aiming) _aimStartedAt = -1f;
            bool introActive = _game.State == AngryBirdsGame.Phase.Ready ||
                (_game.State == AngryBirdsGame.Phase.Aiming && Time.unscaledTime - _aimStartedAt < 2.5f);
            bool ready = !generating && !paused && introActive;
            bool result = !generating && !paused && (_game.State == AngryBirdsGame.Phase.Cleared || _game.State == AngryBirdsGame.Phase.Failed);

            _generatingPanel.SetActive(generating);
            _readyPanel.SetActive(ready);
            _resultPanel.SetActive(result);
            _pausedPanel.SetActive(paused);

            if (generating)
            {
                _generatingLine.text = "正在搭关卡并验证…（第 " + Mathf.Max(1, _game.GenerationAttempt) + " 次尝试）";
            }

            if (ready && level != null)
            {
                var title = _readyPanel.transform.Find("Title").GetComponent<Text>();
                title.text = "第 " + _game.Stage + " 关";
                _readyLine1.text = BirdLevels.Blurb(level);
            }

            if (result)
            {
                bool cleared = _game.State == AngryBirdsGame.Phase.Cleared;
                _resultTitle.text = cleared ? "过关！" : "鸟用完了";
                _resultLine1.text = "得分 " + _game.Score + "　·　剩 " + _game.BirdsLeft + " 只鸟";
                if (cleared)
                {
                    _resultLine2.text = _game.Rank + "　·　赚了 " + _game.RunCoins + " 个宠物币";
                    if (_game.HasNextStage)
                    {
                        _resultLine3.gameObject.SetActive(false);
                        _againButton.SetActive(true);
                        _againLabel.text = "下一关";
                    }
                    else
                    {
                        _resultLine3.gameObject.SetActive(true);
                        _resultLine3.text = "十二关都过了，厉害。";
                        _againButton.SetActive(true);
                        _againLabel.text = "从头再来";
                    }
                }
                else
                {
                    _resultLine2.text = "再试一次，或者按「看提示」看验证过的角度。";
                    _resultLine3.gameObject.SetActive(false);
                    _againButton.SetActive(true);
                    _againLabel.text = "再来一次";
                }
            }

            if (paused)
            {
                _pausedLine1.text = "第 " + _game.Stage + " 关　·　得分 " + _game.Score;
            }
        }

        private void OnBack() => _game?.ReturnToRoom();
        private void OnPause() => _game?.SetPaused(!_game.Paused);
        private void OnHint() { if (_game != null) _game.HintRequested = true; }
        private void OnRestart() => _game?.RestartStage();
        private void OnArc() { if (_game != null) _game.ShowTrajectory = !_game.ShowTrajectory; }
        private void OnResume() => _game?.SetPaused(false);
        private void OnNextOrRetry()
        {
            if (_game == null) return;
            if (_game.State == AngryBirdsGame.Phase.Cleared && _game.HasNextStage) _game.NextStage();
            else if (_game.State == AngryBirdsGame.Phase.Cleared) _game.LoadStage(1);
            else _game.RestartStage();
        }
    }
}
