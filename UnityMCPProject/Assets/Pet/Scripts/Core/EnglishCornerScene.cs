using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DshPet
{
    /// <summary>
    /// The English corner as its own scene, unlocked with pet coins. A self-contained study UI:
    /// 背单词 with 会 / 不会 / 收藏, 顺序 / 随机, 初中 / 高中 / 大学 / 生词表 / 收藏 dictionaries, a quiz
    /// toggle, a speak button, a reset, 词语检索 (search by English or Chinese → word card), and an AI
    /// 情景对话 picker (six fixed scenarios + 自定义) with an editable prompt. Shares the pure layers
    /// (<see cref="EnglishCorner"/> data, <see cref="EnglishProgress"/> state) with the rest of the game.
    /// </summary>
    public class EnglishCornerScene : MonoBehaviour
    {
        private RectTransform _root;
        private Image _panel, _scenarioPanel, _searchPanel;
        private Text _word, _meaning, _example, _translation, _status;
        private Text _dictLabel, _orderLabel, _quizLabel, _knowLabel, _favLabel;
        private Text _scenarioName, _scenarioHint;
        private Text _searchCount;
        private Button _dictBtn, _orderBtn, _quizBtn, _knowBtn, _dontKnowBtn, _favBtn;
        private Button _resetBtn, _scenarioBtn, _searchBtn, _backBtn, _prevBtn, _nextBtn, _speakBtn;
        private Button _startBtn, _cancelBtn, _searchGoBtn, _searchCloseBtn;
        private InputField _promptField, _searchField;
        private readonly List<Button> _options = new List<Button>();
        private readonly List<Button> _scenarioBtns = new List<Button>();
        private readonly List<Button> _searchResultBtns = new List<Button>();
        private RectTransform _searchContent;
        private ScrollRect _searchScroll;

        private int _index;
        private int _mode;              // 0=顺序 1=随机
        private int _dict;              // 0=初中 1=高中 2=大学 3=生词表 4=收藏
        private bool _quiz;
        private bool _scenarioOpen;
        private bool _searchOpen;
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
        private static readonly Color RedTint = new Color(0.75f, 0.35f, 0.35f);
        private static readonly Color PurpleTint = new Color(0.55f, 0.40f, 0.78f);

        // The scenario/search dialogs get a distinct fill so they do NOT blend into the main panel
        // (feedback: 情景对话 UI 和背景重合了).
        private static readonly Color DialogFill = new Color(0.05f, 0.16f, 0.18f, 0.99f);
        private static readonly Color DialogBorder = new Color(0.42f, 0.90f, 1f, 0.35f);

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
            DshMobile.Ugui.SetRect(_dictBtn.GetComponent<RectTransform>(), 22f, 70f, 300f, 44f);
            _dictLabel = _dictBtn.GetComponentInChildren<Text>();
            _dictBtn.onClick.AddListener(() => { _dict = (_dict + 1) % 5; _shuffled = null; _index = 0; });

            _orderBtn = DshMobile.Ugui.Button("Order", p, "", 15, PurpleTint);
            DshMobile.Ugui.SetRect(_orderBtn.GetComponent<RectTransform>(), 332f, 70f, 130f, 44f);
            _orderLabel = _orderBtn.GetComponentInChildren<Text>();
            _orderBtn.onClick.AddListener(() => { _mode = 1 - _mode; _shuffled = null; _index = 0; });

            _quizBtn = DshMobile.Ugui.Button("Quiz", p, "测验", 15, OrangeTint);
            DshMobile.Ugui.SetRectRight(_quizBtn.GetComponent<RectTransform>(), 22f, 70f, 120f, 44f);
            _quizLabel = _quizBtn.GetComponentInChildren<Text>();
            _quizBtn.onClick.AddListener(() => { _quiz = !_quiz; if (_quiz) AskQuiz(); });

            _scenarioBtn = DshMobile.Ugui.Button("Scenario", p, "🎭 情景对话", 15, GreenTint);
            DshMobile.Ugui.SetRect(_scenarioBtn.GetComponent<RectTransform>(), 22f, 122f, 190f, 44f);
            _scenarioBtn.onClick.AddListener(() => { _scenarioOpen = true; _scenarioIndex = 0; RefreshPrompt(); });

            _searchBtn = DshMobile.Ugui.Button("Search", p, "🔍 检索", 15, SlateTint);
            DshMobile.Ugui.SetRect(_searchBtn.GetComponent<RectTransform>(), 222f, 122f, 110f, 44f);
            _searchBtn.onClick.AddListener(() => { _searchOpen = true; _searchField.text = ""; RunSearch(""); });

            _resetBtn = DshMobile.Ugui.Button("Reset", p, "重置", 15, RedTint);
            DshMobile.Ugui.SetRectRight(_resetBtn.GetComponent<RectTransform>(), 22f, 122f, 110f, 44f);
            _resetBtn.onClick.AddListener(OnReset);

            _word = DshMobile.Ugui.Text("Word", p, "", 40, Color.white, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_word.rectTransform, 22f, 186f, 636f, 64f);

            _meaning = DshMobile.Ugui.Text("Meaning", p, "", 20, new Color(0.86f, 0.87f, 0.91f), TextAnchor.UpperLeft);
            _meaning.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_meaning.rectTransform, 22f, 258f, 636f, 100f);

            _example = DshMobile.Ugui.Text("Example", p, "", 17, new Color(0.7f, 0.82f, 0.95f), TextAnchor.UpperLeft);
            _example.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_example.rectTransform, 22f, 366f, 636f, 108f);

            _translation = DshMobile.Ugui.Text("Translation", p, "", 16, new Color(0.90f, 0.88f, 0.80f), TextAnchor.UpperLeft);
            _translation.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_translation.rectTransform, 22f, 482f, 636f, 96f);

            _knowBtn = DshMobile.Ugui.Button("Know", p, "会", 16, GreenTint);
            DshMobile.Ugui.SetRect(_knowBtn.GetComponent<RectTransform>(), 22f, 588f, 120f, 48f);
            _knowLabel = _knowBtn.GetComponentInChildren<Text>();
            _knowBtn.onClick.AddListener(MarkLearned);

            _dontKnowBtn = DshMobile.Ugui.Button("DontKnow", p, "不会", 16, OrangeTint);
            DshMobile.Ugui.SetRect(_dontKnowBtn.GetComponent<RectTransform>(), 152f, 588f, 120f, 48f);
            _dontKnowBtn.onClick.AddListener(MarkUnlearned);

            _favBtn = DshMobile.Ugui.Button("Favorite", p, "☆ 收藏", 16, SlateTint);
            DshMobile.Ugui.SetRect(_favBtn.GetComponent<RectTransform>(), 282f, 588f, 180f, 48f);
            _favLabel = _favBtn.GetComponentInChildren<Text>();
            _favBtn.onClick.AddListener(OnFavorite);

            for (int i = 0; i < 4; i++)
            {
                var opt = DshMobile.Ugui.Button("Opt" + i, p, "", 15, SlateTint);
                DshMobile.Ugui.SetRect(opt.GetComponent<RectTransform>(), 22f, 258f + i * 96f, 636f, 88f);
                var optLabel = opt.GetComponentInChildren<Text>();
                optLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                optLabel.alignment = TextAnchor.MiddleLeft;
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

            // Status line sits ABOVE the bottom buttons, not on top of them (feedback: 绿色小字和按钮重叠).
            _status = DshMobile.Ugui.Text("Status", p, "", 14, new Color(0.7f, 0.95f, 0.75f), TextAnchor.MiddleLeft);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_status.rectTransform, 22f, 648f, 636f, 46f);

            BuildScenarioPanel();
            BuildSearchPanel();
        }

        private void BuildScenarioPanel()
        {
            _scenarioPanel = DshMobile.Ugui.Panel("ScenarioPanel", _root, 20f, DialogFill, DialogBorder, 2f);
            _scenarioPanel.raycastTarget = true;
            DshMobile.Ugui.Center(_scenarioPanel.rectTransform, 640f, 640f);
            _scenarioPanel.gameObject.SetActive(false);
            var s = _scenarioPanel.rectTransform;

            var t = DshMobile.Ugui.Text("Title", s, "情景对话（AI 陪练）", 26, TitleColor, TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(t.rectTransform, 22f, 14f, 440f, 36f);

            // 6 fixed + 1 custom scenario, laid out in a 2-column grid so nothing overflows.
            for (int i = 0; i < EnglishCorner.Scenarios.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                float x = 22f + col * 304f;
                float y = 58f + row * 54f;
                var b = DshMobile.Ugui.Button("Scen" + i, s, EnglishCorner.Scenarios[i].Name, 15,
                    i == EnglishCorner.CustomScenarioIndex ? OrangeTint : SlateTint);
                DshMobile.Ugui.SetRect(b.GetComponent<RectTransform>(), x, y, 292f, 46f);
                int captured = i;
                b.onClick.AddListener(() => { _scenarioIndex = captured; RefreshPrompt(); });
                _scenarioBtns.Add(b);
            }

            _scenarioName = DshMobile.Ugui.Text("ScenarioName", s, "", 15, new Color(1f, 0.92f, 0.7f), TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_scenarioName.rectTransform, 22f, 278f, 596f, 26f);

            _scenarioHint = DshMobile.Ugui.Text("ScenarioHint", s, "", 13, SmallColor, TextAnchor.UpperLeft);
            _scenarioHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_scenarioHint.rectTransform, 22f, 306f, 596f, 20f);

            _promptField = DshMobile.Ugui.InputField("Prompt", s);
            _promptField.lineType = InputField.LineType.MultiLineNewline;
            DshMobile.Ugui.SetRect(_promptField.GetComponent<RectTransform>(), 22f, 330f, 596f, 210f);

            _startBtn = DshMobile.Ugui.Button("Start", s, "开始对话", 18, GreenTint);
            DshMobile.Ugui.SetRect(_startBtn.GetComponent<RectTransform>(), 22f, 552f, 300f, 52f);
            _startBtn.onClick.AddListener(StartScenario);

            _cancelBtn = DshMobile.Ugui.Button("Cancel", s, "取消", 17, SlateTint);
            DshMobile.Ugui.SetRectRight(_cancelBtn.GetComponent<RectTransform>(), 22f, 552f, 160f, 52f);
            _cancelBtn.onClick.AddListener(() => _scenarioOpen = false);
        }

        private void BuildSearchPanel()
        {
            _searchPanel = DshMobile.Ugui.Panel("SearchPanel", _root, 20f, DialogFill, DialogBorder, 2f);
            _searchPanel.raycastTarget = true;
            DshMobile.Ugui.Center(_searchPanel.rectTransform, 640f, 960f);
            _searchPanel.gameObject.SetActive(false);
            var s = _searchPanel.rectTransform;

            var t = DshMobile.Ugui.Text("Title", s, "词语检索", 26, TitleColor, TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(t.rectTransform, 22f, 14f, 440f, 36f);

            var hint = DshMobile.Ugui.Text("Hint", s, "输入英文或中文，检索对应的词卡（点结果看词卡）", 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(hint.rectTransform, 22f, 56f, 596f, 22f);

            _searchField = DshMobile.Ugui.InputField("SearchField", s);
            DshMobile.Ugui.SetRect(_searchField.GetComponent<RectTransform>(), 22f, 84f, 460f, 50f);
            _searchField.onEndEdit.AddListener(RunSearch);

            _searchGoBtn = DshMobile.Ugui.Button("SearchGo", s, "检索", 17, GreenTint);
            DshMobile.Ugui.SetRect(_searchGoBtn.GetComponent<RectTransform>(), 494f, 84f, 124f, 50f);
            _searchGoBtn.onClick.AddListener(() => RunSearch(_searchField != null ? _searchField.text : ""));

            _searchCount = DshMobile.Ugui.Text("SearchCount", s, "", 14, SmallColor, TextAnchor.UpperLeft);
            DshMobile.Ugui.SetRect(_searchCount.rectTransform, 22f, 142f, 596f, 24f);

            // Drawer list: a vertical ScrollRect so a long result list never overflows the panel.
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(s, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            DshMobile.Ugui.SetRect(viewportRt, 22f, 172f, 596f, 716f);
            viewport.AddComponent<RectMask2D>();
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f);
            viewportImg.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _searchContent = content.GetComponent<RectTransform>();
            _searchContent.anchorMin = new Vector2(0f, 1f);
            _searchContent.anchorMax = new Vector2(1f, 1f);
            _searchContent.pivot = new Vector2(0.5f, 1f);
            _searchContent.sizeDelta = new Vector2(-4f, 0f);

            _searchScroll = _searchPanel.gameObject.AddComponent<ScrollRect>();
            _searchScroll.viewport = viewportRt;
            _searchScroll.content = _searchContent;
            _searchScroll.horizontal = false;
            _searchScroll.vertical = true;
            _searchScroll.movementType = ScrollRect.MovementType.Clamped;
            _searchScroll.scrollSensitivity = 40f;

            _searchCloseBtn = DshMobile.Ugui.Button("SearchClose", s, "关闭", 17, SlateTint);
            DshMobile.Ugui.SetRectRight(_searchCloseBtn.GetComponent<RectTransform>(), 22f, 900f, 160f, 50f);
            _searchCloseBtn.onClick.AddListener(() => _searchOpen = false);
        }

        private void RunSearch(string query)
        {
            EnglishWord[] results = EnglishCorner.Search(query ?? "", 120);
            RebuildSearchResults(results);
            _searchCount.text = results.Length == 0
                ? "没有找到，换个词试试"
                : "找到 " + results.Length + " 个结果" + (results.Length >= 120 ? "（仅显示前 120 个）" : "");
        }

        private void RebuildSearchResults(EnglishWord[] results)
        {
            for (int i = 0; i < _searchResultBtns.Count; i++)
            {
                if (_searchResultBtns[i] != null) Destroy(_searchResultBtns[i].gameObject);
            }
            _searchResultBtns.Clear();

            const float rowH = 64f;
            _searchContent.sizeDelta = new Vector2(-4f, results.Length * rowH);

            for (int i = 0; i < results.Length; i++)
            {
                var w = results[i];
                var btn = DshMobile.Ugui.Button("Result" + i, _searchContent, "", 15, SlateTint);
                DshMobile.Ugui.SetRect(btn.GetComponent<RectTransform>(), 0f, i * rowH, 588f, rowH - 4f);
                var label = btn.GetComponentInChildren<Text>();
                label.text = w.Word + "　" + Truncate(w.Meaning, 46);
                label.fontSize = 15;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                var captured = w;
                btn.onClick.AddListener(() => OpenSearchResult(captured));
                _searchResultBtns.Add(btn);
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private void OpenSearchResult(EnglishWord w)
        {
            if (w == null) return;
            _dict = CategoryToDict(w.Category);
            _shuffled = null;
            _quiz = false;
            _searchOpen = false;

            var words = BaseWords();
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Word == w.Word) { _index = i; break; }
            }
        }

        private static int CategoryToDict(string category)
        {
            switch (category)
            {
                case "高中": return 1;
                case "大学": return 2;
                default: return 0;   // 初中 (and anything unknown falls back to 初中)
            }
        }

        private void RefreshPrompt()
        {
            if (_scenarioName != null)
                _scenarioName.text = "情景：" + EnglishCorner.Scenarios[_scenarioIndex].Name;
            bool custom = _scenarioIndex == EnglishCorner.CustomScenarioIndex;
            if (_scenarioHint != null)
                _scenarioHint.text = custom
                    ? "自定义情景：在下面把想练的场景和角色写清楚，再开始对话。"
                    : "提示词可编辑，改完直接「开始对话」。";
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
            switch (_dict)
            {
                case 1: return UnlearnedOnly(EnglishCorner.WordsForCategory("高中"));
                case 2: return UnlearnedOnly(EnglishCorner.WordsForCategory("大学"));
                case 3: return WordsFrom(EnglishProgress.NewWordList());
                case 4: return WordsFrom(EnglishProgress.Favorites());
                default: return UnlearnedOnly(EnglishCorner.WordsForCategory("初中"));
            }
        }

        /// <summary>Filters out words the player has already marked 会 — learned words stop showing
        /// in the dictionary rotation and only come back after a reset (feedback: 已会词不再重复).</summary>
        private EnglishWord[] UnlearnedOnly(EnglishWord[] words)
        {
            var list = new List<EnglishWord>();
            foreach (var w in words)
            {
                if (!EnglishProgress.IsLearned(w.Word)) list.Add(w);
            }
            return list.ToArray();
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
            if (_dict == 4)
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
            _searchPanel.gameObject.SetActive(_searchOpen);

            var w = Current();
            bool empty = CurrentWords().Length == 0;
            _word.text = empty ? "🎉 都学会啦" : w.Word;
            _meaning.text = empty ? "这个词典的词你都标成「会」了。" : w.Meaning;
            _example.text = empty ? "点「重置」清零学习记录，它们就会重新出现。" : w.Example;
            _translation.text = empty ? "" : w.ExampleTranslation;

            _dictLabel.text = "词典：" + DictName(_dict) + "（" + CurrentWords().Length + " 词）";
            _orderLabel.text = _mode == 0 ? "顺序" : "随机";
            _quizLabel.text = _quiz ? "背单词" : "测验";

            _knowLabel.text = EnglishProgress.IsLearned(w.Word) ? "✓ 会" : "会";
            _favLabel.text = _dict == 4 ? "移除收藏" : (EnglishProgress.IsFavorite(w.Word) ? "★ 已收藏" : "☆ 收藏");

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
                case 1: return "高中";
                case 2: return "大学";
                case 3: return "生词表";
                case 4: return "收藏";
                default: return "初中";
            }
        }
    }
}
