using DshPet;
using NUnit.Framework;

namespace DshPet.Tests
{
    /// <summary>The English corner's vocabulary and quiz builder. The interesting failure modes
    /// are "the answer is not in the options", "distractors duplicate the answer" and "an index
    /// walks off the end of the list" — all arithmetic, so all testable.</summary>
    public class EnglishCornerTests
    {
        [Test]
        public void Words_AreCompleteAndUniqueByMeaning()
        {
            Assert.Greater(EnglishCorner.WordCount, 30, "the vocabulary has to be big enough to quiz");

            var meanings = new System.Collections.Generic.HashSet<string>();
            foreach (var w in EnglishCorner.Words)
            {
                Assert.IsFalse(string.IsNullOrEmpty(w.Word), "every entry needs an English word");
                Assert.IsFalse(string.IsNullOrEmpty(w.Meaning), "every entry needs a Chinese meaning");
                Assert.IsFalse(string.IsNullOrEmpty(w.Example), "every entry needs an example to read out");
                Assert.IsTrue(meanings.Add(w.Meaning), $"duplicate meaning: {w.Meaning}");
            }
        }

        [Test]
        public void WordAt_WrapsAround()
        {
            var first = EnglishCorner.WordAt(0);
            var wrapped = EnglishCorner.WordAt(EnglishCorner.WordCount);
            Assert.AreEqual(first.Word, wrapped.Word, "the index wraps back to the start");

            var last = EnglishCorner.WordAt(-1);
            Assert.AreEqual(EnglishCorner.Words[EnglishCorner.WordCount - 1].Word, last.Word,
                "a negative index wraps to the end");
        }

        [Test]
        public void EnglishTeacherPrompt_IsMountedAsACompleteInstruction()
        {
            Assert.IsFalse(string.IsNullOrEmpty(EnglishCorner.EnglishTeacherPrompt),
                "the teacher prompt must exist");
            Assert.IsTrue(EnglishCorner.EnglishTeacherPrompt.Contains("英语"),
                "the instruction says the pet is an English teacher");
            Assert.IsTrue(EnglishCorner.EnglishTeacherPrompt.Contains("单词"),
                "the pet teaches words");
            Assert.IsTrue(EnglishCorner.EnglishTeacherPrompt.Contains("例句"),
                "the pet teaches example sentences");
            Assert.IsTrue(EnglishCorner.EnglishTeacherPrompt.Contains("对话"),
                "the pet teaches dialogues");
        }

        [Test]
        public void Quiz_ContainsTheAnswerOnceAndDistinctDistractors()
        {
            var rng = new System.Random(1234);
            for (int index = 0; index < EnglishCorner.WordCount; index += 7)
            {
                var answer = EnglishCorner.WordAt(index);
                var options = EnglishCorner.QuizOptions(index, rng, 4);

                Assert.AreEqual(4, options.Length, "a quiz has four options");

                int matches = 0;
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var o in options)
                {
                    if (o.Meaning == answer.Meaning) matches++;
                    Assert.IsTrue(seen.Add(o.Meaning), $"duplicate option meaning: {o.Meaning}");
                }
                Assert.AreEqual(1, matches, "exactly one option is the answer");
            }
        }
    }
}
