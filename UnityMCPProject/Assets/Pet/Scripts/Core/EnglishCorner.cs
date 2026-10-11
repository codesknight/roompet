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
