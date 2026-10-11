using System.Collections.Generic;

namespace DshPet
{
    /// <summary>One vocabulary entry in the English corner.</summary>
    public class EnglishWord
    {
        public string Word;
        public string Meaning;
        public string Example;
        public string ExampleTranslation;
        public string Category;
    }

    /// <summary>How the word list is walked: in order, or shuffled.</summary>
    public enum EnglishStudyMode { Sequential, Random }

    /// <summary>
    /// The English corner's vocabulary and quiz logic — pure and testable, like every other rule
    /// layer in the project. The word list is data (in <c>EnglishCorner.Data.cs</c>); the quiz
    /// builder is arithmetic over that data.
    /// </summary>
    public static partial class EnglishCorner
    {
        public static int WordCount => Words.Length;

        /// <summary>
        /// The system-prompt instruction that turns the pet into a patient English teacher.
        /// Mounted while English chat mode is on, so the pet answers in English and actively
        /// teaches — words, phrases, example sentences and little dialogues — rather than just
        /// translating whatever the player wrote.
        /// </summary>
        public static readonly string EnglishTeacherPrompt =
            "现在你是主人的英语小老师，要陪主人面对面练英语：\n" +
            "1. 全程用英语回答，句子简短清楚，适合初学者。\n" +
            "2. 每轮主动教一点：一个新单词或短语，附上中文意思，再给一句例句。\n" +
            "3. 主人用英语开口时多鼓励；说错了就温柔纠正，再示范正确说法。\n" +
            "4. 可以顺势展开成小对话（打招呼、点餐、问路、聊宠物等）。\n" +
            "5. 中文只用来解释意思，你平时说的话仍然要用英语。";

        /// <summary>One situational dialogue scenario (AI 情景对话).</summary>
        public class EnglishScenario
        {
            public string Name;
            public string DefaultPrompt;
        }

        /// <summary>The scenarios the player can practise with the pet, each with a default prompt.</summary>
        public static readonly EnglishScenario[] Scenarios =
        {
            new EnglishScenario { Name = "餐厅点餐", DefaultPrompt =
                "现在和主人做「餐厅点餐」的情景对话，你是服务员，主人是顾客。全程用英语：先打招呼问主人想吃什么，介绍两三样菜和价格，帮主人点单并确认，最后祝用餐愉快。句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" },
            new EnglishScenario { Name = "问路", DefaultPrompt =
                "现在和主人做「问路」的情景对话，你是路人，主人来问路。全程用英语：先回应主人的招呼，听他说要去哪，用简单的方位词（left/right/straight）指路，必要时说大概走几分钟。句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" },
            new EnglishScenario { Name = "购物", DefaultPrompt =
                "现在和主人做「购物」的情景对话，你是店员，主人来买东西。全程用英语：问主人想买什么，介绍商品和价格，讨价还价一下，最后结账道别。句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" },
            new EnglishScenario { Name = "谈天气", DefaultPrompt =
                "现在和主人做「谈天气」的情景对话。全程用英语聊天气：问主人今天天气怎么样，说说你喜欢晴天还是雨天，聊聊季节。句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" },
            new EnglishScenario { Name = "谈爱好", DefaultPrompt =
                "现在和主人做「谈爱好」的情景对话。全程用英语聊兴趣爱好：问主人喜欢做什么，说说你自己的爱好（画画、唱歌、运动），再问问主人周末怎么过。句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" },
            new EnglishScenario { Name = "看病", DefaultPrompt =
                "现在和主人做「看病」的情景对话，你是医生，主人来看病。全程用英语：问主人哪里不舒服，问几句症状（头疼、发烧、咳嗽），给出建议（多喝水、多休息），最后叮嘱按时吃药。句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" },
            new EnglishScenario { Name = "自定义情景", DefaultPrompt =
                "现在和主人做一段英语情景对话，具体情景由主人来定（主人会在下面写清楚想练什么：面试、旅游、医院、机场、点外卖……以及各自扮演谁）。你按主人写的场景和角色来，全程用英语，句子简短适合初学者，每句英文后面附一句中文解释；主人说错了就温柔纠正再示范。" }
        };

        /// <summary>The index of the 自定义情景 in <see cref="Scenarios"/>.</summary>
        public static int CustomScenarioIndex => Scenarios.Length - 1;

        /// <summary>The default prompt for a scenario, or "" when the index is out of range.</summary>
        public static string ScenarioPrompt(int index)
            => (index >= 0 && index < Scenarios.Length) ? Scenarios[index].DefaultPrompt : "";

        /// <summary>Wraps an index into the word list (never throws on negatives).</summary>
        public static EnglishWord WordAt(int index)
            => Words[((index % WordCount) + WordCount) % WordCount];

        /// <summary>Looks a word up by its English spelling. Null when it is not in the list.</summary>
        public static EnglishWord FindWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            foreach (var w in Words)
            {
                if (w.Word == word) return w;
            }
            return null;
        }

        /// <summary>
        /// Builds a multiple-choice quiz for the word at <paramref name="answerIndex"/>: the answer
        /// plus <paramref name="count"/>-1 other meanings as distractors, shuffled. Distractors are
        /// distinct from the answer and from each other.
        /// </summary>
        public static EnglishWord[] QuizOptions(int answerIndex, System.Random rng, int count = 4)
            => QuizOptionsFor(WordAt(answerIndex), rng, count);

        /// <summary>The same quiz, given the answer word directly (for dictionary-scoped study).</summary>
        public static EnglishWord[] QuizOptionsFor(EnglishWord answer, System.Random rng, int count = 4)
        {
            if (answer == null) answer = Words[0];
            var result = new List<EnglishWord> { answer };

            var pool = new List<EnglishWord>();
            foreach (var w in Words)
            {
                if (w.Meaning == answer.Meaning) continue;
                pool.Add(w);
            }
            Shuffle(pool, rng);

            for (int i = 0; i < pool.Count && result.Count < count; i++)
            {
                bool dup = false;
                foreach (var r in result) if (r.Meaning == pool[i].Meaning) { dup = true; break; }
                if (!dup) result.Add(pool[i]);
            }

            Shuffle(result, rng);
            return result.ToArray();
        }

        /// <summary>Picks a random word index, for "next word" and quiz seeding.</summary>
        public static int RandomIndex(System.Random rng) => rng.Next(WordCount);

        // ------------------------------------------------------------------ dictionaries & search

        private static readonly Dictionary<string, EnglishWord[]> _categoryCache = new Dictionary<string, EnglishWord[]>();

        /// <summary>
        /// The words in one level's dictionary (Category == "初中" / "高中" / "大学"). Cached because
        /// the word list is a few thousand entries and the UI asks for it every frame.
        /// </summary>
        public static EnglishWord[] WordsForCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return Words;

            EnglishWord[] cached;
            if (_categoryCache.TryGetValue(category, out cached)) return cached;

            var list = new List<EnglishWord>();
            foreach (var w in Words)
            {
                if (w.Category == category) list.Add(w);
            }
            cached = list.ToArray();
            _categoryCache[category] = cached;
            return cached;
        }

        /// <summary>How many words a level's dictionary holds.</summary>
        public static int CategoryCount(string category) => WordsForCategory(category).Length;

        /// <summary>
        /// 词语检索: matches a word by its English spelling OR by a Chinese substring in its meaning.
        /// The English side is case-insensitive; results keep dictionary order (alphabetical) so a
        /// prefix like "cat" returns cat, catarrh, catch… in a stable list.
        /// </summary>
        public static EnglishWord[] Search(string query, int maxResults = 120)
        {
            if (string.IsNullOrWhiteSpace(query)) return new EnglishWord[0];
            string q = query.Trim();
            string lower = q.ToLowerInvariant();

            var list = new List<EnglishWord>();
            foreach (var w in Words)
            {
                if (w.Word.ToLowerInvariant().Contains(lower) || w.Meaning.Contains(q))
                {
                    list.Add(w);
                    if (list.Count >= maxResults) break;
                }
            }
            return list.ToArray();
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
