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
    /// layer in the project. The word list is data; the quiz builder is arithmetic over that data.
    /// </summary>
    public static class EnglishCorner
    {
        /// <summary>The vocabulary, grouped by category. English word → Chinese meaning → an
        /// example sentence the TTS can read out → its Chinese translation.</summary>
        public static readonly EnglishWord[] Words =
        {
            new EnglishWord { Word = "cat", Meaning = "猫", Example = "The cat is sleeping on the bed.", ExampleTranslation = "猫正在床上睡觉。", Category = "动物" },
            new EnglishWord { Word = "dog", Meaning = "狗", Example = "The dog runs in the park.", ExampleTranslation = "狗在公园里跑。", Category = "动物" },
            new EnglishWord { Word = "rabbit", Meaning = "兔子", Example = "The rabbit eats a carrot.", ExampleTranslation = "兔子在吃胡萝卜。", Category = "动物" },
            new EnglishWord { Word = "bear", Meaning = "熊", Example = "The bear likes honey.", ExampleTranslation = "熊喜欢蜂蜜。", Category = "动物" },
            new EnglishWord { Word = "fox", Meaning = "狐狸", Example = "The fox has a big tail.", ExampleTranslation = "狐狸有一条大尾巴。", Category = "动物" },
            new EnglishWord { Word = "panda", Meaning = "熊猫", Example = "The panda eats bamboo.", ExampleTranslation = "熊猫在吃竹子。", Category = "动物" },
            new EnglishWord { Word = "bird", Meaning = "鸟", Example = "The bird can fly high.", ExampleTranslation = "小鸟能飞得很高。", Category = "动物" },
            new EnglishWord { Word = "fish", Meaning = "鱼", Example = "The fish swims in the water.", ExampleTranslation = "鱼在水里游。", Category = "动物" },
            new EnglishWord { Word = "pig", Meaning = "猪", Example = "The pig is pink.", ExampleTranslation = "猪是粉红色的。", Category = "动物" },
            new EnglishWord { Word = "frog", Meaning = "青蛙", Example = "The frog jumps on the leaf.", ExampleTranslation = "青蛙在叶子上跳。", Category = "动物" },

            new EnglishWord { Word = "apple", Meaning = "苹果", Example = "I eat an apple every day.", ExampleTranslation = "我每天吃一个苹果。", Category = "食物" },
            new EnglishWord { Word = "orange", Meaning = "橘子", Example = "The orange is sweet.", ExampleTranslation = "这个橘子很甜。", Category = "食物" },
            new EnglishWord { Word = "banana", Meaning = "香蕉", Example = "The monkey likes bananas.", ExampleTranslation = "猴子喜欢香蕉。", Category = "食物" },
            new EnglishWord { Word = "carrot", Meaning = "胡萝卜", Example = "Rabbits love carrots.", ExampleTranslation = "兔子爱吃胡萝卜。", Category = "食物" },
            new EnglishWord { Word = "bread", Meaning = "面包", Example = "I have bread for breakfast.", ExampleTranslation = "我早餐吃面包。", Category = "食物" },
            new EnglishWord { Word = "milk", Meaning = "牛奶", Example = "I drink a glass of milk.", ExampleTranslation = "我喝一杯牛奶。", Category = "食物" },
            new EnglishWord { Word = "water", Meaning = "水", Example = "Please drink more water.", ExampleTranslation = "请多喝水。", Category = "食物" },
            new EnglishWord { Word = "egg", Meaning = "鸡蛋", Example = "I cook an egg for lunch.", ExampleTranslation = "我午饭煮了一个鸡蛋。", Category = "食物" },
            new EnglishWord { Word = "rice", Meaning = "米饭", Example = "We eat rice for dinner.", ExampleTranslation = "我们晚饭吃米饭。", Category = "食物" },
            new EnglishWord { Word = "cake", Meaning = "蛋糕", Example = "The cake is very sweet.", ExampleTranslation = "这个蛋糕非常甜。", Category = "食物" },

            new EnglishWord { Word = "run", Meaning = "跑", Example = "I run in the morning.", ExampleTranslation = "我早上跑步。", Category = "动作" },
            new EnglishWord { Word = "jump", Meaning = "跳", Example = "The frog can jump high.", ExampleTranslation = "青蛙能跳得很高。", Category = "动作" },
            new EnglishWord { Word = "sleep", Meaning = "睡觉", Example = "I sleep at ten o'clock.", ExampleTranslation = "我十点睡觉。", Category = "动作" },
            new EnglishWord { Word = "eat", Meaning = "吃", Example = "We eat dinner together.", ExampleTranslation = "我们一起吃晚饭。", Category = "动作" },
            new EnglishWord { Word = "drink", Meaning = "喝", Example = "I drink tea in the afternoon.", ExampleTranslation = "我下午喝茶。", Category = "动作" },
            new EnglishWord { Word = "play", Meaning = "玩", Example = "Let's play a game.", ExampleTranslation = "我们玩个游戏吧。", Category = "动作" },
            new EnglishWord { Word = "walk", Meaning = "走", Example = "We walk to school.", ExampleTranslation = "我们走路去上学。", Category = "动作" },
            new EnglishWord { Word = "talk", Meaning = "说话", Example = "They talk for an hour.", ExampleTranslation = "他们聊了一个小时。", Category = "动作" },
            new EnglishWord { Word = "read", Meaning = "读", Example = "I read a book at night.", ExampleTranslation = "我晚上读书。", Category = "动作" },
            new EnglishWord { Word = "sing", Meaning = "唱", Example = "She likes to sing.", ExampleTranslation = "她喜欢唱歌。", Category = "动作" },

            new EnglishWord { Word = "house", Meaning = "房子", Example = "My house has a garden.", ExampleTranslation = "我的房子有个花园。", Category = "日常" },
            new EnglishWord { Word = "door", Meaning = "门", Example = "Please close the door.", ExampleTranslation = "请关门。", Category = "日常" },
            new EnglishWord { Word = "bed", Meaning = "床", Example = "The cat is on the bed.", ExampleTranslation = "猫在床上。", Category = "日常" },
            new EnglishWord { Word = "book", Meaning = "书", Example = "This book is interesting.", ExampleTranslation = "这本书很有趣。", Category = "日常" },
            new EnglishWord { Word = "sun", Meaning = "太阳", Example = "The sun is bright today.", ExampleTranslation = "今天阳光很灿烂。", Category = "日常" },
            new EnglishWord { Word = "moon", Meaning = "月亮", Example = "The moon is in the sky.", ExampleTranslation = "月亮在天上。", Category = "日常" },
            new EnglishWord { Word = "star", Meaning = "星星", Example = "I can see many stars.", ExampleTranslation = "我能看到很多星星。", Category = "日常" },
            new EnglishWord { Word = "friend", Meaning = "朋友", Example = "You are my best friend.", ExampleTranslation = "你是我最好的朋友。", Category = "日常" },
            new EnglishWord { Word = "happy", Meaning = "开心", Example = "I am happy to see you.", ExampleTranslation = "见到你我很开心。", Category = "日常" },
            new EnglishWord { Word = "love", Meaning = "爱", Example = "I love my pet.", ExampleTranslation = "我爱我的宠物。", Category = "日常" }
        };

        public static int WordCount => Words.Length;

        /// <summary>The four categories, in table order.</summary>
        public static readonly string[] Categories = { "动物", "食物", "动作", "日常" };

        /// <summary>Every word whose category matches, in table order.</summary>
        public static EnglishWord[] WordsInCategory(string category)
        {
            var list = new List<EnglishWord>();
            foreach (var w in Words)
            {
                if (w.Category == category) list.Add(w);
            }
            return list.ToArray();
        }

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

        /// <summary>Wraps an index into the word list (never throws on negatives).</summary>
        public static EnglishWord WordAt(int index)
            => Words[((index % WordCount) + WordCount) % WordCount];

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
