using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The English corner's per-word progress — the "百词斩" half. Three saved sets plus one rule:
    ///
    ///  - <b>会</b>：a word you know. Marking a word 会 for the first time pays one pet coin.
    ///  - <b>不会</b>：a word you are still learning — it lands in the 生词表 automatically.
    ///  - <b>收藏</b>：a star you can put on any word, independent of whether you know it.
    ///
    /// Everything here is a pure PlayerPrefs-backed set, so the whole reward loop is unit-testable
    /// without the UI.
    /// </summary>
    public static class EnglishProgress
    {
        public const string LearnedKey = "dshpet.english.learned";
        public const string NewWordsKey = "dshpet.english.newwords";
        public const string FavoritesKey = "dshpet.english.favorites";

        /// <summary>Pet coins earned for learning one new word.</summary>
        public const int CoinPerWord = 1;

        private static HashSet<string> Loaded(string key)
        {
            var set = new HashSet<string>();
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw)) return set;
            foreach (string part in raw.Split(','))
            {
                if (!string.IsNullOrEmpty(part)) set.Add(part);
            }
            return set;
        }

        private static void Save(string key, HashSet<string> set)
        {
            var parts = new List<string>(set);
            parts.Sort();
            PlayerPrefs.SetString(key, string.Join(",", parts.ToArray()));
            PlayerPrefs.Save();
        }

        public static bool IsLearned(string word) => Loaded(LearnedKey).Contains(word);
        public static bool IsFavorite(string word) => Loaded(FavoritesKey).Contains(word);
        public static bool IsInNewWords(string word) => Loaded(NewWordsKey).Contains(word);

        public static int LearnedCount => Loaded(LearnedKey).Count;
        public static int NewWordCount => Loaded(NewWordsKey).Count;
        public static int FavoriteCount => Loaded(FavoritesKey).Count;

        /// <summary>
        /// Marks a word as known and pays the reward the first time. Returns true only when the word
        /// actually transitioned, so the UI can show "+1 宠物币". Knowing a word takes it out of the
        /// 生词表 (it is no longer "new").
        /// </summary>
        public static bool MarkLearned(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            var learned = Loaded(LearnedKey);
            if (learned.Contains(word)) return false;

            learned.Add(word);
            Save(LearnedKey, learned);

            // The 生词表 keeps "still learning" words, so a learned word leaves it.
            var newWords = Loaded(NewWordsKey);
            if (newWords.Remove(word)) Save(NewWordsKey, newWords);

            DshMobile.PetWallet.Add(CoinPerWord);
            return true;
        }

        /// <summary>Marks a word as not-yet-known. It joins the 生词表 so it can be reviewed.</summary>
        public static void MarkUnlearned(string word)
        {
            if (string.IsNullOrEmpty(word)) return;
            var learned = Loaded(LearnedKey);
            if (learned.Remove(word)) Save(LearnedKey, learned);

            var newWords = Loaded(NewWordsKey);
            if (!newWords.Contains(word))
            {
                newWords.Add(word);
                Save(NewWordsKey, newWords);
            }
        }

        /// <summary>Adds a word to the 生词表 explicitly (favourite-style review list).</summary>
        public static bool AddToNewWords(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            var newWords = Loaded(NewWordsKey);
            if (newWords.Contains(word)) return false;
            newWords.Add(word);
            Save(NewWordsKey, newWords);
            return true;
        }

        public static void RemoveFromNewWords(string word)
        {
            if (string.IsNullOrEmpty(word)) return;
            var newWords = Loaded(NewWordsKey);
            if (newWords.Remove(word)) Save(NewWordsKey, newWords);
        }

        /// <summary>Toggles a favourite. Returns true when the word became a favourite.</summary>
        public static bool ToggleFavorite(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            var favorites = Loaded(FavoritesKey);
            bool added = !favorites.Contains(word);
            if (added) favorites.Add(word); else favorites.Remove(word);
            Save(FavoritesKey, favorites);
            return added;
        }

        /// <summary>The 生词表 words, sorted for a stable review order.</summary>
        public static List<string> NewWordList()
        {
            var list = new List<string>(Loaded(NewWordsKey));
            list.Sort();
            return list;
        }

        /// <summary>The favourited words, sorted for a stable review order.</summary>
        public static List<string> Favorites()
        {
            var list = new List<string>(Loaded(FavoritesKey));
            list.Sort();
            return list;
        }

        /// <summary>Removes a word from the favourites (for the 收藏 word-list management).</summary>
        public static void RemoveFavorite(string word)
        {
            if (string.IsNullOrEmpty(word)) return;
            var favorites = Loaded(FavoritesKey);
            if (favorites.Remove(word)) Save(FavoritesKey, favorites);
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(LearnedKey);
            PlayerPrefs.DeleteKey(NewWordsKey);
            PlayerPrefs.DeleteKey(FavoritesKey);
            PlayerPrefs.Save();
        }
    }
}
