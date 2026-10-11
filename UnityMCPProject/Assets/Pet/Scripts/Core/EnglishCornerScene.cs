using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DshPet
{
    /// <summary>
    /// The English corner as its own scene, unlocked with pet coins. A self-contained study UI:
    /// 背单词 with 会 / 不会 / 收藏, 顺序 / 随机, 初中词库 / 生词表, a quiz toggle, and a speak
    /// button. It shares the pure layers (<see cref="EnglishCorner"/> data, <see cref="EnglishProgress"/>
    /// state) with the rest of the game, so progress carries over between visits.
    /// </summary>
    public class EnglishCornerScene : MonoBehaviour
    {
        private RectTransform _root;
        private Image _panel;

        private Text _word, _meaning, _example, _translation, _status;
        private Text _dictLabel, _orderLabel, _knowLabel, _favLabel, _quizLabel;
        private Button _dictBtn, _orderBtn, _knowBtn, _dontKnowBtn, _favBtn, _quizBtn;
        private Button _prevBtn, _nextBtn, _speakBtn, _backBtn;
        private readonly List<Button> _options = new List<Button>();

        private int _index;                 // index into the current dictionary list
        private int _mode;                  // 0=顺序 1=随机
        private int _dict;                  // 0=初中词库 1=生词表
        private bool _quiz;                 // quiz mode on/off
        private int _score, _total;
        private EnglishWord[] _optionWords;
        private int _answerIndex;
        private string _feedback = "";
        private readonly System.Random _rng = new System.Random();
        private EnglishWord[] _shuffled;

        private static readonly Color TitleColor = new Color(1f, 0.94f, 0.82f);
        private static readonly Color BodyColor = new Color(0.96f, 0.96f, 0.99f);
        private static readonly Color SmallColor = new Color(0.90f, 0.92f, 0.97f, 0.9f);
        private static readonly Color SlateTint = new Color(0.30f, 0.40f, 0.58f);
        private static readonly Color GreenTint = new Color(0.30f, 0.55f, 0.35f);
        private static readonly Color OrangeTint = new Color(0.85f, 0.55f, 0.22f);

        private void Awake() => Build();

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
            DshMobile.Ugui.Center(_panel.rectTransform, 720f, 640f);
            var p = _panel.rectTransform;

            var title = DshMobile.Ugui.Text("Title", p, "英语角", 30, TitleColor, TextAnchor.MiddleLeft, true);
            DshMobile.Ugui.SetRect(title.rectTransform, 22f, 16f, 200f, 36f);

            _backBtn = DshMobile.Ugui.Button("Back", p, "返回房间", 17, SlateTint);
            DshMobile.Ugui.SetRectRight(_backBtn.GetComponent<RectTransform>(), 22f, 18f, 120f, 36f);
            _backBtn.onClick.AddListener(BackToRoom);

            _dictBtn = DshMobile.Ugui.Button("Dict", p, "", 15, SlateTint);
            DshMobile.Ugui.SetRect(_dictBtn.GetComponent<RectTransform>(), 22f, 64f, 340f, 36f);
            _dictLabel = _dictBtn.GetComponentInChildren<Text>();
            _dictBtn.onClick.AddListener(() => { _dict = (_dict + 1) % 2; _shuffled = null; _index = 0; });

            _orderBtn = DshMobile.Ugui.Button("Order", p, "", 15, new Color(0.55f, 0.40f, 0.78f));
            DshMobile.Ugui.SetRectRight(_orderBtn.GetComponent<RectTransform>(), 22f, 64f, 140f, 36f);
            _orderLabel = _orderBtn.GetComponentInChildren<Text>();
            _orderBtn.onClick.AddListener(() => { _mode = 1 - _mode; _shuffled = null; _index = 0; });

            _quizBtn = DshMobile.Ugui.Button("Quiz", p, "测验", 15, OrangeTint);
            DshMobile.Ugui.SetRectRight(_quizBtn.GetComponent<RectTransform>(), 170f, 64f, 100f, 36f);
            _quizLabel = _quizBtn.GetComponentInChildren<Text>();
            _quizBtn.onClick.AddListener(() => { _quiz = !_quiz; if (_quiz) AskQuiz(); });

            _word = DshMobile.Ugui.Text("Word", p, "", 40, Color.white, TextAnchor.MiddleCenter, true);
            DshMobile.Ugui.SetRect(_word.rectTransform, 22f, 116f, 676f, 50f);

            _meaning = DshMobile.Ugui.Text("Meaning", p, "", 20, new Color(0.86f, 0.87f, 0.91f), TextAnchor.MiddleCenter);
            DshMobile.Ugui.SetRect(_meaning.rectTransform, 22f, 172f, 676f, 60f);

            _example = DshMobile.Ugui.Text("Example", p, "", 17, new Color(0.7f, 0.82f, 0.95f), TextAnchor.UpperLeft);
            _example.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_example.rectTransform, 22f, 240f, 676f, 56f);

            _translation = DshMobile.Ugui.Text("Translation", p, "", 16, new Color(0.90f, 0.88f, 0.80f), TextAnchor.UpperLeft);
            _translation.horizontalOverflow = HorizontalWrapMode.Wrap;
            DshMobile.Ugui.SetRect(_translation.rectTransform, 22f, 300f, 676f, 56f);

            _knowBtn = DshMobile.Ugui.Button("Know", p, "会", 16, GreenTint);
            DshMobile.Ugui.SetRect(_knowBtn.GetComponent<RectTransform>(), 22f, 368f, 110f, 40f);
            _knowLabel = _knowBtn.GetComponentInChildren<Text>();
            _knowBtn.onClick.AddListener(MarkLearned);

            _dontKnowBtn = DshMobile.Ugui.Button("DontKnow", p, "不会", 16, OrangeTint);
            DshMobile.Ugui.SetRect(_dontKnowBtn.GetComponent<RectTransform>(), 142f, 368f, 110f, 40f);
            _dontKnowBtn.onClick.AddListener(MarkUnlearned);

            _favBtn = DshMobile.Ugui.Button("Favorite", p, "☆ 收藏", 16, SlateTint);
            DshMobile.Ugui.SetRect(_favBtn.GetComponent<RectTransform>(), 262f, 368f, 140f, 40f);
            _favLabel = _favBtn.GetComponentInChildren<Text>();
            _favBtn.onClick.AddListener(ToggleFavorite);

            for (int i = 0; i < 4; i++)
            {
                var opt = DshMobile.Ugui.Button("Opt" + i, p, "", 15, SlateTint);
                DshMobile.Ugui.SetRect(opt.GetComponent<RectTransform>(), 22f, 240f + i * 50f, 676f, 44f);
                int captured = i;
                opt.onClick.AddListener(() => OnOption(captured));
                _options.Add(opt);
            }

            _prevBtn = DshMobile.Ugui.Button("Prev", p, "上一个", 16, SlateTint);
            DshMobile.Ugui.SetRectBottomLeft(_prevBtn.GetComponent<RectTransform>(), 22f, 16f, 120f, 40f);
            _prevBtn.onClick.AddListener(() => Step(-1));

            _nextBtn = DshMobile.Ugui.Button("Next", p, "下一个", 16, GreenTint);
            DshMobile.Ugui.SetRectBottomLeft(_nextBtn.GetComponent<RectTransform>(), 152f, 16f, 120f, 40f);
            _nextBtn.onClick.AddListener(() => Step(1));

            _speakBtn = DshMobile.Ugui.Button("Speak", p, "🔊 朗读", 16, GreenTint);
            DshMobile.Ugui.SetRectBottomLeft(_speakBtn.GetComponent<RectTransform>(), 282f, 16f, 120f, 40f);
            _speakBtn.onClick.AddListener(Speak);

            _status = DshMobile.Ugui.Text("Status", p, "", 15, new Color(0.7f, 0.95f, 0.75f), TextAnchor.MiddleLeft);
            DshMobile.Ugui.SetRect(_status.rectTransform, 22f, 596f, 676f, 26f);
        }

        private void Update() => Sync();

        // ------------------------------------------------------------------ dictionary / mode

        private EnglishWord[] BaseWords()
        {
            if (_dict == 1)
            {
                var list = new List<EnglishWord>();
                foreach (string w in EnglishProgress.NewWordList())
                {
                    var found = EnglishCorner.FindWord(w);
                    if (found != null) list.Add(found);
                }
                return list.ToArray();
            }
            return EnglishCorner.Words;
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
            if (EnglishProgress.MarkLearned(w.Word))
                _feedback = $"学会了「{w.Word}」，+{EnglishProgress.CoinPerWord} 宠物币";
            else
                _feedback = $"「{w.Word}」已经会了";
            Step(1);
        }

        private void MarkUnlearned()
        {
            var w = Current();
            EnglishProgress.MarkUnlearned(w.Word);
            _feedback = $"「{w.Word}」加进了生词表，多复习几遍";
            Step(1);
        }

        private void ToggleFavorite()
        {
            var w = Current();
            bool added = EnglishProgress.ToggleFavorite(w.Word);
            _feedback = added ? $"已收藏「{w.Word}」" : $"取消了「{w.Word}」的收藏";
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

        private void Sync()
        {
            var w = Current();

            _word.text = w.Word;
            _meaning.text = w.Meaning;
            _example.text = w.Example;
            _translation.text = w.ExampleTranslation;

            _dictLabel.text = "词典：" + (_dict == 1 ? "生词表" : "初中词库") + "（" + CurrentWords().Length + " 词）";
            _orderLabel.text = _mode == 0 ? "顺序" : "随机";
            _quizLabel.text = _quiz ? "背单词" : "测验";

            _knowLabel.text = EnglishProgress.IsLearned(w.Word) ? "✓ 会" : "会";
            _favLabel.text = EnglishProgress.IsFavorite(w.Word) ? "★ 已收藏" : "☆ 收藏";

            _status.text = $"已会 {EnglishProgress.LearnedCount} · 生词 {EnglishProgress.NewWordCount} · 收藏 {EnglishProgress.FavoriteCount}　{_feedback}";

            bool showLearn = !_quiz;
            _example.gameObject.SetActive(showLearn);
            _translation.gameObject.SetActive(showLearn);
            _knowBtn.gameObject.SetActive(showLearn);
            _dontKnowBtn.gameObject.SetActive(showLearn);
            _favBtn.gameObject.SetActive(showLearn);
            _speakBtn.gameObject.SetActive(showLearn);
            _prevBtn.gameObject.SetActive(showLearn);
            _nextBtn.gameObject.SetActive(showLearn);

            for (int i = 0; i < _options.Count; i++)
            {
                bool show = _quiz && _optionWords != null && i < _optionWords.Length;
                _options[i].gameObject.SetActive(show);
                if (show) _options[i].GetComponentInChildren<Text>().text = _optionWords[i].Meaning;
            }
        }
    }
}
