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
        public void Words_AreCompleteAndUnique()
        {
            Assert.Greater(EnglishCorner.WordCount, 200, "the vocabulary has to be big enough to actually learn from");

            var words = new System.Collections.Generic.HashSet<string>();
            foreach (var w in EnglishCorner.Words)
            {
                Assert.IsFalse(string.IsNullOrEmpty(w.Word), "every entry needs an English word");
                Assert.IsFalse(string.IsNullOrEmpty(w.Meaning), "every entry needs a Chinese meaning");
                Assert.IsFalse(string.IsNullOrEmpty(w.Example), "every entry needs an example to read out");
                Assert.IsTrue(words.Add(w.Word), $"duplicate word: {w.Word}");
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

        [Test]
        public void Words_EveryExampleHasATranslation()
        {
            foreach (var w in EnglishCorner.Words)
            {
                Assert.IsFalse(string.IsNullOrEmpty(w.ExampleTranslation),
                    $"word {w.Word} needs a Chinese translation of its example");
            }
        }

        [Test]
        public void FindWord_LooksUpBySpelling()
        {
            Assert.AreEqual("cat", EnglishCorner.FindWord("cat").Word);
            Assert.IsNull(EnglishCorner.FindWord("nonsenseword"));
        }

        [Test]
        public void Progress_MarkLearnedPaysOnceAndOnlyOnce()
        {
            int original = DshMobile.PetWallet.Coins;
            try
            {
                EnglishProgress.ResetAll();
                DshMobile.PetWallet.Reset();

                Assert.IsTrue(EnglishProgress.MarkLearned("cat"), "first time learning pays");
                Assert.AreEqual(EnglishProgress.CoinPerWord, DshMobile.PetWallet.Coins);
                Assert.IsFalse(EnglishProgress.MarkLearned("cat"), "already learned pays nothing");
                Assert.AreEqual(EnglishProgress.CoinPerWord, DshMobile.PetWallet.Coins);

                // 不会 then 会 again: it leaves the learned set, so learning it again pays once more.
                EnglishProgress.MarkUnlearned("cat");
                Assert.IsFalse(EnglishProgress.IsLearned("cat"));
                Assert.IsTrue(EnglishProgress.MarkLearned("cat"));
                Assert.AreEqual(EnglishProgress.CoinPerWord * 2, DshMobile.PetWallet.Coins);
            }
            finally
            {
                EnglishProgress.ResetAll();
                DshMobile.PetWallet.Reset();
                DshMobile.PetWallet.Add(original);
            }
        }

        [Test]
        public void Progress_UnlearnedJoinsTheNewWordListAndLearnedLeavesIt()
        {
            try
            {
                EnglishProgress.ResetAll();
                EnglishProgress.MarkUnlearned("cat");
                Assert.IsTrue(EnglishProgress.IsInNewWords("cat"), "不会 lands in the 生词表");

                EnglishProgress.MarkLearned("cat");
                Assert.IsFalse(EnglishProgress.IsInNewWords("cat"), "learned leaves the 生词表");
                Assert.AreEqual(0, EnglishProgress.NewWordCount);
            }
            finally
            {
                EnglishProgress.ResetAll();
            }
        }

        [Test]
        public void Progress_FavoritesToggleAndExplicitAdd()
        {
            try
            {
                EnglishProgress.ResetAll();
                Assert.IsTrue(EnglishProgress.ToggleFavorite("cat"), "first toggle favourites");
                Assert.IsTrue(EnglishProgress.IsFavorite("cat"));
                Assert.IsFalse(EnglishProgress.ToggleFavorite("cat"), "second toggle unfavourites");

                Assert.IsTrue(EnglishProgress.AddToNewWords("dog"), "explicit add to 生词表");
                Assert.IsFalse(EnglishProgress.AddToNewWords("dog"), "adding twice is a no-op");
                Assert.AreEqual(1, EnglishProgress.NewWordCount);
            }
            finally
            {
                EnglishProgress.ResetAll();
            }
        }

        [Test]
        public void EnglishCornerAccess_UnlockSpendsCoinsOnce()
        {
            int original = DshMobile.PetWallet.Coins;
            try
            {
                EnglishCornerAccess.ResetAll();
                DshMobile.PetWallet.Reset();
                DshMobile.PetWallet.Add(EnglishCornerAccess.Cost - 1);
                string msg;
                Assert.IsFalse(EnglishCornerAccess.TryUnlock(out msg), "cannot afford");
                Assert.IsTrue(msg.Contains("还差"), "it says how much is missing");

                DshMobile.PetWallet.Add(1);
                Assert.IsTrue(EnglishCornerAccess.TryUnlock(out msg));
                Assert.IsTrue(EnglishCornerAccess.IsUnlocked);
                Assert.AreEqual(0, DshMobile.PetWallet.Coins, "the unlock spends the cost");

                Assert.IsTrue(EnglishCornerAccess.TryUnlock(out msg), "already unlocked is a success no-op");
                Assert.AreEqual(0, DshMobile.PetWallet.Coins, "no double charge");
            }
            finally
            {
                EnglishCornerAccess.ResetAll();
                DshMobile.PetWallet.Reset();
                DshMobile.PetWallet.Add(original);
            }
        }

        [Test]
        public void Progress_FavoritesCanBeListedAndRemoved()
        {
            try
            {
                EnglishProgress.ResetAll();
                EnglishProgress.ToggleFavorite("cat");
                EnglishProgress.ToggleFavorite("dog");

                var favs = EnglishProgress.Favorites();
                Assert.AreEqual(2, favs.Count);
                Assert.IsTrue(favs.Contains("cat") && favs.Contains("dog"));

                EnglishProgress.RemoveFavorite("cat");
                Assert.IsFalse(EnglishProgress.IsFavorite("cat"));
                Assert.AreEqual(1, EnglishProgress.FavoriteCount);
            }
            finally
            {
                EnglishProgress.ResetAll();
            }
        }

        [Test]
        public void Scenarios_HaveNamesAndPrompts()
        {
            Assert.GreaterOrEqual(EnglishCorner.Scenarios.Length, 4, "a handful of scenarios for practice");
            for (int i = 0; i < EnglishCorner.Scenarios.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(EnglishCorner.Scenarios[i].Name), "a scenario needs a name");
                Assert.IsFalse(string.IsNullOrEmpty(EnglishCorner.Scenarios[i].DefaultPrompt),
                    "a scenario needs a default prompt");
            }
            Assert.IsFalse(string.IsNullOrEmpty(EnglishCorner.ScenarioPrompt(0)));
            Assert.AreEqual("", EnglishCorner.ScenarioPrompt(999), "out of range returns empty");
        }

        [Test]
        public void Scenarios_HaveACustomScenarioAsTheLastOne()
        {
            // 反馈: 新增一个"其他情景"，玩家可自主定义场景对话。
            Assert.GreaterOrEqual(EnglishCorner.Scenarios.Length, 7, "six fixed scenarios plus 自定义");
            Assert.AreEqual(EnglishCorner.Scenarios.Length - 1, EnglishCorner.CustomScenarioIndex);
            Assert.IsFalse(string.IsNullOrEmpty(EnglishCorner.Scenarios[EnglishCorner.CustomScenarioIndex].Name),
                "the custom scenario needs a name");
        }

        [Test]
        public void Dictionaries_HaveJuniorSeniorAndCollegeWords()
        {
            // 反馈: 词典只有一个初中，高中和大学的呢？
            string[] levels = { "初中", "高中", "大学" };
            foreach (string level in levels)
            {
                var words = EnglishCorner.WordsForCategory(level);
                Assert.Greater(words.Length, 200, level + " dictionary has to be a real word list");

                foreach (var w in words)
                {
                    Assert.AreEqual(level, w.Category, "every word in the " + level + " list must be tagged " + level);
                }
            }
        }

        [Test]
        public void Search_FindsByEnglishAndChinese()
        {
            Assert.Greater(EnglishCorner.Search("cat").Length, 0, "an English spelling finds the word");
            Assert.IsTrue(ContainsWord(EnglishCorner.Search("cat"), "cat"));

            // 中文释义也能反查（"猫" matches the meaning of "cat"）。
            Assert.IsTrue(ContainsWord(EnglishCorner.Search("猫"), "cat"), "a Chinese meaning finds the word");

            Assert.AreEqual(0, EnglishCorner.Search("").Length, "an empty query finds nothing");
            Assert.AreEqual(0, EnglishCorner.Search("zzzzzzqq").Length, "gibberish finds nothing");
        }

        [Test]
        public void Search_ReturnsAUniqueListWhenAmbiguous()
        {
            // "ab" is a shared prefix (abandon / ability / able / about …), so it must return several.
            var results = EnglishCorner.Search("ab", 50);
            Assert.GreaterOrEqual(results.Length, 3, "an ambiguous prefix returns multiple cards");

            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var w in results)
            {
                Assert.IsTrue(seen.Add(w.Word), "search results must not duplicate: " + w.Word);
            }
        }

        private static bool ContainsWord(EnglishWord[] words, string word)
        {
            foreach (var w in words)
            {
                if (w.Word == word) return true;
            }
            return false;
        }
    }
}
