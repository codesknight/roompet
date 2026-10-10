using System.Collections.Generic;

namespace DshPet
{
    /// <summary>One vocabulary entry in the English corner.</summary>
    public class EnglishWord
    {
        public string Word;
        public string Meaning;
        public string Example;
        public string Category;
    }

    /// <summary>
    /// The English corner's vocabulary and quiz logic — pure and testable, like every other rule
    /// layer in the project. The word list is data; the quiz builder is arithmetic over that data.
    /// </summary>
    public static class EnglishCorner
    {
        /// <summary>The vocabulary, grouped by category. English word → Chinese meaning → an
        /// example sentence the TTS can read out.</summary>
        public static readonly EnglishWord[] Words =
        {
            new EnglishWord { Word = "cat", Meaning = "猫", Example = "The cat is sleeping on the bed.", Category = "动物" },
            new EnglishWord { Word = "dog", Meaning = "狗", Example = "The dog runs in the park.", Category = "动物" },
            new EnglishWord { Word = "rabbit", Meaning = "兔子", Example = "The rabbit eats a carrot.", Category = "动物" },
            new EnglishWord { Word = "bear", Meaning = "熊", Example = "The bear likes honey.", Category = "动物" },
            new EnglishWord { Word = "fox", Meaning = "狐狸", Example = "The fox has a big tail.", Category = "动物" },
            new EnglishWord { Word = "panda", Meaning = "熊猫", Example = "The panda eats bamboo.", Category = "动物" },
            new EnglishWord { Word = "bird", Meaning = "鸟", Example = "The bird can fly high.", Category = "动物" },
            new EnglishWord { Word = "fish", Meaning = "鱼", Example = "The fish swims in the water.", Category = "动物" },
            new EnglishWord { Word = "pig", Meaning = "猪", Example = "The pig is pink.", Category = "动物" },
            new EnglishWord { Word = "frog", Meaning = "青蛙", Example = "The frog jumps on the leaf.", Category = "动物" },

            new EnglishWord { Word = "apple", Meaning = "苹果", Example = "I eat an apple every day.", Category = "食物" },
            new EnglishWord { Word = "orange", Meaning = "橘子", Example = "The orange is sweet.", Category = "食物" },
            new EnglishWord { Word = "banana", Meaning = "香蕉", Example = "The monkey likes bananas.", Category = "食物" },
            new EnglishWord { Word = "carrot", Meaning = "胡萝卜", Example = "Rabbits love carrots.", Category = "食物" },
            new EnglishWord { Word = "bread", Meaning = "面包", Example = "I have bread for breakfast.", Category = "食物" },
            new EnglishWord { Word = "milk", Meaning = "牛奶", Example = "I drink a glass of milk.", Category = "食物" },
            new EnglishWord { Word = "water", Meaning = "水", Example = "Please drink more water.", Category = "食物" },
            new EnglishWord { Word = "egg", Meaning = "鸡蛋", Example = "I cook an egg for lunch.", Category = "食物" },
            new EnglishWord { Word = "rice", Meaning = "米饭", Example = "We eat rice for dinner.", Category = "食物" },
            new EnglishWord { Word = "cake", Meaning = "蛋糕", Example = "The cake is very sweet.", Category = "食物" },

            new EnglishWord { Word = "run", Meaning = "跑", Example = "I run in the morning.", Category = "动作" },
            new EnglishWord { Word = "jump", Meaning = "跳", Example = "The frog can jump high.", Category = "动作" },
            new EnglishWord { Word = "sleep", Meaning = "睡觉", Example = "I sleep at ten o'clock.", Category = "动作" },
            new EnglishWord { Word = "eat", Meaning = "吃", Example = "We eat dinner together.", Category = "动作" },
            new EnglishWord { Word = "drink", Meaning = "喝", Example = "I drink tea in the afternoon.", Category = "动作" },
            new EnglishWord { Word = "play", Meaning = "玩", Example = "Let's play a game.", Category = "动作" },
            new EnglishWord { Word = "walk", Meaning = "走", Example = "We walk to school.", Category = "动作" },
            new EnglishWord { Word = "talk", Meaning = "说话", Example = "They talk for an hour.", Category = "动作" },
            new EnglishWord { Word = "read", Meaning = "读", Example = "I read a book at night.", Category = "动作" },
            new EnglishWord { Word = "sing", Meaning = "唱", Example = "She likes to sing.", Category = "动作" },

            new EnglishWord { Word = "house", Meaning = "房子", Example = "My house has a garden.", Category = "日常" },
            new EnglishWord { Word = "door", Meaning = "门", Example = "Please close the door.", Category = "日常" },
            new EnglishWord { Word = "bed", Meaning = "床", Example = "The cat is on the bed.", Category = "日常" },
            new EnglishWord { Word = "book", Meaning = "书", Example = "This book is interesting.", Category = "日常" },
            new EnglishWord { Word = "sun", Meaning = "太阳", Example = "The sun is bright today.", Category = "日常" },
            new EnglishWord { Word = "moon", Meaning = "月亮", Example = "The moon is in the sky.", Category = "日常" },
            new EnglishWord { Word = "star", Meaning = "星星", Example = "I can see many stars.", Category = "日常" },
            new EnglishWord { Word = "friend", Meaning = "朋友", Example = "You are my best friend.", Category = "日常" },
            new EnglishWord { Word = "happy", Meaning = "开心", Example = "I am happy to see you.", Category = "日常" },
            new EnglishWord { Word = "love", Meaning = "爱", Example = "I love my pet.", Category = "日常" }
        };

        public static int WordCount => Words.Length;

        /// <summary>Wraps an index into the word list (never throws on negatives).</summary>
        public static EnglishWord WordAt(int index)
            => Words[((index % WordCount) + WordCount) % WordCount];

        /// <summary>
        /// Builds a multiple-choice quiz for the word at <paramref name="answerIndex"/>: the answer
        /// plus <paramref name="count"/>-1 other meanings as distractors, shuffled. Distractors are
        /// distinct from the answer and from each other.
        /// </summary>
        public static EnglishWord[] QuizOptions(int answerIndex, System.Random rng, int count = 4)
        {
            var answer = WordAt(answerIndex);
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
