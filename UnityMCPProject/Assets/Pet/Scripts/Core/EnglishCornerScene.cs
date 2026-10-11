using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DshPet
{
    /// <summary>
    /// The English corner as its own scene, unlocked with pet coins. A self-contained study UI:
    /// 背单词 with 会 / 不会 / 收藏, 顺序 / 随机, 初中词库 / 生词表 / 收藏, a quiz toggle, a speak
    /// button, a reset, and an AI 情景对话 picker with an editable prompt. Shares the pure layers
    /// (<see cref="EnglishCorner"/> data, <see cref="EnglishProgress"/> state) with the rest of the game.
    /// </summary>
    public class EnglishCornerScene : MonoBehaviour
    {
        private RectTransform _root;
        private Image _panel, _scenarioPanel;
        private Text _word, _meaning, _example, _translation, _status;
        private Text _dictLabel, _orderLabel, _quizLabel, _knowLabel, _favLabel;
        private Text _scenarioName;
        private Button _dictBtn, _orderBtn, _quizBtn, _knowBtn, _dontKnowBtn, _favBtn;
        private Button _resetBtn, _scenarioBtn, _backBtn, _prevBtn, _nextBtn, _speakBtn;
        private Button _startBtn, _cancelBtn;
        private InputField _promptField;
        private readonly List<Button> _options = new List<Button>();
        private readonly List<Button> _scenarioBtns = new List<Button>();

        private int _index;
        private int _mode;              // 0=顺序 1=随机
        private int _dict;              // 0=初中词库 1=生词表 2=收藏
        private bool _quiz;
        private bool _scenarioOpen;
        private bool _armedReset;
        private int _scenarioIndex;
        private int _score, _total;
        private EnglishWord[] _optionWords;
        private int _answerIndex;
        private string _feedback = "";
        private readonly System.Random _rng = new System.Random();
        private EnglishWord[] _shuffled;

        private static readonly Color TitleColor = new Color(1f, 0.94f, 0.82f);
        private static readonly Color SmallColor = new Color(0.90f, 0.92f, 0.97f, 0.9f);
        private static readonly Color SlateTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);
        private static readonly Color OrangeTint = new Color(0.85f, 0.55f, 0.22f);

        private void Awake()
        {
            DshMobile.Ugui.SetReferenceResolution(720f, 1280f);
            Build();
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            _root = DshMobile.Ugui.Root("EnglishCornerHud");
            _panel = DshMobile.Ugui.Panel("Panel", _root, 22f,
                new Color(0.10f, 0.09f, 0.14f, 0.97f), new Color(1f, 1f, 1f, 0.16f), 2f);
            _panel.raycastTarget = true;
            DshMobile.Ugui.Center(_panel.rectTransform, 680f, 1180f);
            var p = _panel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "英语角", 30, TitleColor, TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 22f, 18f, 220f, 40f);

            _backBtn = DshMobile.Ugui.Button("Back", p, "返回房间", 17, SlateTint);
            DshMobile.Ugui.SetRectRight(_backBtn.GetComponent<RectTransform>(), 22f, 18f, 130f, 44f);
            _backBtn.onClick.AddListener(BackToRoom);

            _dictBtn = DshMobile.Ugui.Button("Dict", p, "", 15, SlateTint);
            DshMobile.Ugui.SetRect(_dictBtn.GetComponent<RectTransform>(), 22f, 72f, 300f, 44f);
            _dictLabel = _dictBtn.GetComponentInChildren<Text>();
            _dictBtn.onClick.AddListener(() => { _dict = (_dict + 1) % 3; _shuffled = null; _index = 0; });

            _orderBtn = DshMobile.Ugui.Button("Order", p, "", 15, new Color(0.55f, 0.40f, 0.78f));
            DshMobile.Ugui.SetRect(_orderBtn.GetComponent<RectTransform>(), 332f, 72f, 130f, 44f);
            _orderLabel = _orderBtn.GetComponentInChildren<Text>();
            _orderBtn.onClick.AddListener(() => { _mode = 1 - _mode; _shuffled = null; _index = 0; });

            _quizBtn = DshMobile.Ugui.Button("Quiz", p, "测验", 15, OrangeTint);
            DshMobile.Ugui.SetRectRight(_quizBtn.GetComponent<RectTransform>(), 22f, 72f, 120f, 44f);
            _quizLabel = _quizBtn.GetComponentInChildren<Text>();
            _quizBtn.onClick.AddListener(() => { _quiz = !_quiz; if (_quiz) AskQuiz(); });

            _scenarioBtn = DshMobile.Ugui.Button("Scenario", p, "🎭 情景对话", 15, GreenTint);
            DshMobile.Ugui.SetRect(_scenarioBtn.GetComponent<RectTransform>(), 22f, 126f, 200f, 44f);
            _scenarioBtn.onClick.AddListener(() => { _scenarioOpen = true; _scenarioIndex = 0; RefreshPrompt(); });

            _resetBtn = DshMobile.Ugui.Button("Reset", p, "重置", 15, new Color(0.75f, 0.35f, 0.35f));
            DshMobile.Ugui.SetRect(_resetBtn.GetComponent<RectTransform>(), 232f, 126f, 130f, 44f);
            _resetBtn.onClick.AddListener(OnReset);

            _word = DshMobile.Ugui.Text("Word", p, "", 40, Color.white, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_word.rectTransform, 22f, 196f, 636f, 60f);

            _meaning = DshMobile.Ugui.Text("Meaning", p, "", 20, new Color(0.86f, 0.87f, 0.91f), TextAnchor.UpperLeft);
            _meaning.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_meaning.rectTransform, 22f, 268f, 636f, 80f);

            _example = DshMobile.Ugui.Text("Example", p, "", 17, new Color(0.7f, 0.82f, 0.95f), TextAnchor.UpperLeft);
            _example.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_example.rectTransform, 22f, 356f, 636f, 90f);

            _translation = DshMobile.Ugui.Text("Translation", p, "", 16, new Color(0.90f, 0.88f, 0.80f), TextAnchor.UpperLeft);
            _translation.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_translation.rectTransform, 22f, 452f, 636f, 80f);

            _knowBtn = DshMobile.Ugui.Button("Know", p, "会", 16, GreenTint);
            DshMobile.Ugui.SetRect(_knowBtn.GetComponent<RectTransform>(), 22f, 548f, 120f, 46f);
            _knowLabel = _knowBtn.GetComponentInChildren<Text>();
            _knowBtn.onClick.AddListener(MarkLearned);

            _dontKnowBtn = DshMobile.Ugui.Button("DontKnow", p, "不会", 16, OrangeTint);
            DshMobile.Ugui.SetRect(_dontKnowBtn.GetComponent<RectTransform>(), 152f, 548f, 120f, 46f);
            _dontKnowBtn.onClick.AddListener(MarkUnlearned);

            _favBtn = DshMobile.Ugui.Button("Favorite", p, "☆ 收藏", 16, SlateTint);
            DshMobile.Ugui.SetRect(_favBtn.GetComponent<RectTransform>(), 282f, 548f, 180f, 46f);
            _favLabel = _favBtn.GetComponentInChildren<Text>();
            _favBtn.onClick.AddListener(OnFavorite);

            for (int i = 0; i < 4; i++)
            {
                var opt = DshMobile.Ugui.Button("Opt" + i, p, "", 15, SlateTint);
                DshMobile.Ugui.SetRect(opt.GetComponent<RectTransform>(), 22f, 356f + i * 64f, 636f, 56f);
                int captured = i;
                opt.onClick.AddListener(() => OnOption(captured));
                _options.Add(opt);
            }

            _prevBtn = DshMobile.Ugui.Button("Prev", p, "上一个", 16, SlateTint);
            DshMobile.Ugui.SetRectBottomLeft(_prevBtn.GetComponent<RectTransform>(), 22f, 16f, 120f, 44f);
            _prevBtn.onClick.AddListener(() => Step(-1));

            _nextBtn = DshMobile.Ugui.Button("Next", p, "下一个", 16, GreenTint);
            DshMobile.Ugui.SetRectBottomLeft(_nextBtn.GetComponent<RectTransform>(), 152f, 16f, 120f, 44f);
            _nextBtn.onClick.AddListener(() => Step(1));

            _speakBtn = DshMobile.Ugui.Button("Speak", p, "🔊 朗读", 16, GreenTint);
            DshMobile.Ugui.SetRectBottomLeft(_speakBtn.GetComponent<RectTransform>(), 282f, 16f, 140f, 44f);
            _speakBtn.onClick.AddListener(Speak);

            _status = DshMobile.Ugui.Text("Status", p, "", 14, new Color(0.7f, 0.95f, 0.75f), TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_status.rectTransform, 22f, 1100f, 636f, 56f);

            BuildScenarioPanel();
        }

        private void BuildScenarioPanel()
        {
            _scenarioPanel = DshMobile.Ugui.Panel("ScenarioPanel", _root, 20f,
                new Color(0.10f, 0.09f, 0.14f, 0.98f), new Color(1f, 1f, 1f, 0.18f), 2f);
            _scenarioPanel.raycastTarget = true;
            DshMobile.Ugui.Center(_scenarioPanel.rectTransform, 620f, 720f);
            _scenarioPanel.gameObject.SetActive(false);
            var s = _scenarioPanel.rectTransform;

            var t = DshMobile.Ugui.Text("Title", s, "情景对话（AI 陪练）", 26, TitleColor, TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(t.rectTransform, 22f, 16f, 400f, 36f);

            for (int i = 0; i < EnglishCorner.Scenarios.Length; i++)
            {
                var b = DshMobile.Ugui.Button("Scen" + i, s, EnglishCorner.Scenarios[i].Name, 15, SlateTint);
                DshMobile.Ugui.SetRect(b.GetComponent<RectTransform>(), 22f, 60f + i * 54f, 200f, 46f);
                int captured = i;
                b.onClick.AddListener(() => { _scenarioIndex = captured; RefreshPrompt(); });
                _scenarioBtns.Add(b);
            }

            _scenarioName = DshMobile.Ugui.Text("ScenarioName", s, "", 15, new Color(1f, 0.92f, 0.7f), TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_scenarioName.rectTransform, 240f, 60f, 358f, 26f);

            var promptLabel = DshMobile.Ugui.Text("PromptLabel", s, "提示词（可编辑，发给 AI）", 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(promptLabel.rectTransform, 240f, 92f, 358f, 22f);

            _promptField = DshMobile.Ugui.InputField("Prompt", s);
            _promptField.lineType = InputField.LineType.MultiLineNewline;
            DshMobile.Ugui.SetRect(_promptField.GetComponent<RectTransform>(), 22f, 396f, 576f, 220f);

            _startBtn = DshMobile.Ugui.Button("Start", s, "开始对话", 18, GreenTint);
            DshMobile.Ugui.SetRect(_startBtn.GetComponent<RectTransform>(), 22f, 640f, 280f, 52f);
            _startBtn.onClick.AddListener(StartScenario);

            _cancelBtn = DshMobile.Ugui.Button("Cancel", s, "取消", 17, SlateTint);
            DshMobile.Ugui.SetRectRight(_cancelBtn.GetComponent<RectTransform>(), 22f, 640f, 160f, 52f);
            _cancelBtn.onClick.AddListener(() => _scenarioOpen = false);
        }

        private void RefreshPrompt()
        {
            if (_scenarioName != null) _scenarioName.text = EnglishCorner.Scenarios[_scenarioIndex].Name;
            if (_promptField != null) _promptField.text = EnglishCorner.Scenarios[_scenarioIndex].DefaultPrompt;
        }

        private void StartScenario()
        {
            string prompt = _promptField != null ? _promptField.text.Trim() : "";
            if (string.IsNullOrEmpty(prompt)) prompt = EnglishCorner.Scenarios[_scenarioIndex].DefaultPrompt;
            PlayerPrefs.SetString(PetGameManager.ScenarioChatKey, prompt);
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }

        // ------------------------------------------------------------------ dictionary / mode

        private EnglishWord[] BaseWords()
        {
            if (_dict == 1) return WordsFrom(EnglishProgress.NewWordList());
            if (_dict == 2) return WordsFrom(EnglishProgress.Favorites());
            return EnglishCorner.Words;
        }

        private EnglishWord[] WordsFrom(List<string> wordStrings)
        {
            var list = new List<EnglishWord>();
            foreach (string w in wordStrings)
            {
                var found = EnglishCorner.FindWord(w);
                if (found != null) list.Add(found);
            }
            return list.ToArray();
        }

        private EnglishWord[] CurrentWords()
        {
            var baseWords = BaseWords();
            if (_mode == 0) return baseWords;

            if (_shuffled == null || _shuffled.Length != baseWords.Length)
            {
                _shuffled = new List<EnglishWord>(baseWords).ToArray();
                for (int i = _shuffled.Length - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    var t = _shuffled[i]; _shuffled[i] = _shuffled[j]; _shuffled[j] = t;
                }
            }
            return _shuffled;
        }

        private EnglishWord Current()
        {
            var words = CurrentWords();
            if (words.Length == 0) return EnglishCorner.Words[0];
            int i = ((_index % words.Length) + words.Length) % words.Length;
            return words[i];
        }

        private void Step(int delta)
        {
            var words = CurrentWords();
            if (words.Length == 0) return;
            _index = ((_index + delta) % words.Length + words.Length) % words.Length;
            if (_quiz) AskQuiz();
        }

        // ------------------------------------------------------------------ 百词斩 marking

        private void MarkLearned()
        {
            var w = Current();
            _feedback = EnglishProgress.MarkLearned(w.Word)
                ? $"学会了「{w.Word}」，+{EnglishProgress.CoinPerWord} 宠物币"
                : $"「{w.Word}」已经会了";
            Step(1);
        }

        private void MarkUnlearned()
        {
            var w = Current();
            EnglishProgress.MarkUnlearned(w.Word);
            _feedback = $"「{w.Word}」加进了生词表，多复习几遍";
            Step(1);
        }

        private void OnFavorite()
        {
            var w = Current();
            if (_dict == 2)
            {
                // 在收藏词表里点「收藏」= 移除收藏。
                EnglishProgress.RemoveFavorite(w.Word);
                _feedback = $"移除了「{w.Word}」的收藏";
                _shuffled = null;
                Step(0);
            }
            else
            {
                bool added = EnglishProgress.ToggleFavorite(w.Word);
                _feedback = added ? $"已收藏「{w.Word}」" : $"取消了「{w.Word}」的收藏";
            }
        }

        private void OnReset()
        {
            if (!_armedReset)
            {
                _armedReset = true;
                _feedback = "再点一次「重置」就清零所有学习记录（学会/生词表/收藏）";
                return;
            }
            _armedReset = false;
            EnglishProgress.ResetAll();
            _index = 0;
            _shuffled = null;
            _feedback = "学习记录已清零，可以重新开始";
        }

        private void Speak()
        {
            DshMobile.MobileTts.Speak(Current().Example, 1f, 1f);
        }

        private void AskQuiz()
        {
            var answer = Current();
            _optionWords = EnglishCorner.QuizOptionsFor(answer, _rng, 4);
            _answerIndex = -1;
            for (int i = 0; i < _optionWords.Length; i++)
                if (_optionWords[i].Meaning == answer.Meaning) _answerIndex = i;
            _feedback = "";
        }

        private void OnOption(int i)
        {
            if (!_quiz || _optionWords == null || i >= _optionWords.Length) return;
            _total++;
            if (i == _answerIndex)
            {
                _score++;
                _feedback = "✓ 答对了！";
            }
            else
            {
                _feedback = "✗ 答案是「" + Current().Meaning + "」";
            }
            Step(1);
        }

        private void BackToRoom()
        {
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("PetRoom");
        }

        // ------------------------------------------------------------------ sync

        private void Update()
        {
            if (_panel == null) return;

            _scenarioPanel.gameObject.SetActive(_scenarioOpen);

            var w = Current();
            _word.text = w.Word;
            _meaning.text = w.Meaning;
            _example.text = w.Example;
            _translation.text = w.ExampleTranslation;

            _dictLabel.text = "词典：" + DictName(_dict) + "（" + CurrentWords().Length + " 词）";
            _orderLabel.text = _mode == 0 ? "顺序" : "随机";
            _quizLabel.text = _quiz ? "背单词" : "测验";

            _knowLabel.text = EnglishProgress.IsLearned(w.Word) ? "✓ 会" : "会";
            _favLabel.text = _dict == 2 ? "移除收藏" : (EnglishProgress.IsFavorite(w.Word) ? "★ 已收藏" : "☆ 收藏");

            string resetHint = _armedReset ? "　再点一次重置清零" : "";
            _status.text = $"已会 {EnglishProgress.LearnedCount} · 生词 {EnglishProgress.NewWordCount} · 收藏 {EnglishProgress.FavoriteCount}　{_feedback}{resetHint}";

            bool learn = !_quiz;
            _meaning.gameObject.SetActive(learn);
            _example.gameObject.SetActive(learn);
            _translation.gameObject.SetActive(learn);
            _knowBtn.gameObject.SetActive(learn);
            _dontKnowBtn.gameObject.SetActive(learn);
            _favBtn.gameObject.SetActive(learn);
            _prevBtn.gameObject.SetActive(learn);
            _nextBtn.gameObject.SetActive(learn);
            _speakBtn.gameObject.SetActive(learn);

            for (int i = 0; i < _options.Count; i++)
            {
                bool show = _quiz && _optionWords != null && i < _optionWords.Length;
                _options[i].gameObject.SetActive(show);
                if (show) _options[i].GetComponentInChildren<Text>().text = _optionWords[i].Meaning;
            }
        }

        private static string DictName(int dict)
        {
            switch (dict)
            {
                case 1: return "生词表";
                case 2: return "收藏";
                default: return "初中词库";
            }
        }
    }
}
