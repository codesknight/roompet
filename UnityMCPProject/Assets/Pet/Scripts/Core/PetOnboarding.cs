using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The new-player onboarding state: which starter pet was picked, and whether the first
    /// feeding has happened. Plain PlayerPrefs-backed flags plus the helpers the flow needs, so
    /// the whole thing can be unit tested without a scene.
    ///
    /// A fresh save opens on the front door with three pets to pick from; only after choosing
    /// does the room exist. The tutorial then has one goal — the first feed — which unlocks the
    /// food bowl and the water basin.
    /// </summary>
    public static class PetOnboarding
    {
        public const string StarterKey = "dshpet.onboarding.starter";
        public const string FirstFeedKey = "dshpet.onboarding.firstfeed";
        public const string KitKey = "dshpet.onboarding.kit";

        /// <summary>The three starter pets a new player picks from.</summary>
        public static readonly string[] StarterChoices = { "fox", "cat", "rabbit" };

        /// <summary>One bag of kibble (three meals) granted so the first feed is possible at once.</summary>
        public const int StartingFood = 3;

        /// <summary>The chosen starter species id, or "" when nothing has been chosen yet.</summary>
        public static string StarterChoice => PlayerPrefs.GetString(StarterKey, "");

        public static bool HasChosenStarter => !string.IsNullOrEmpty(StarterChoice);

        public static bool FirstFeedDone => PlayerPrefs.GetInt(FirstFeedKey, 0) == 1;

        public static bool IsStarterChoice(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId)) return false;
            for (int i = 0; i < StarterChoices.Length; i++)
            {
                if (StarterChoices[i] == speciesId) return true;
            }
            return false;
        }

        /// <summary>
        /// Picks the starter pet. Writes the species id to both the onboarding flag and the
        /// shared species key, so the start-menu preview and the room agree on the animal.
        /// Returns false for anything that is not a starter choice.
        /// </summary>
        public static bool ChooseStarter(string speciesId)
        {
            if (!IsStarterChoice(speciesId)) return false;

            PlayerPrefs.SetString(StarterKey, speciesId);
            PlayerPrefs.SetString(DshMobile.MiniAnimal.SpeciesKey, speciesId);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Marks the choice made without validating the species, for migrating an existing save
        /// whose primary pet may predate the three starters (a bear, a gacha pet, and so on).
        /// </summary>
        public static void MarkChosen(string speciesId)
        {
            PlayerPrefs.SetString(StarterKey, string.IsNullOrEmpty(speciesId) ? "fox" : speciesId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Marks the first feeding done. The food bowl and the water basin are gated on this
        /// flag, so completing it is also what puts them in the room.
        /// </summary>
        public static string CompleteFirstFeed()
        {
            if (FirstFeedDone) return "首次喂食已经完成了";
            PlayerPrefs.SetInt(FirstFeedKey, 1);
            PlayerPrefs.Save();
            return "首次喂食完成！食物碗和水盆已经摆好了。";
        }

        /// <summary>Whether the one-time starter kit (kibble) has already been granted.</summary>
        public static bool StarterKitGranted => PlayerPrefs.GetInt(KitKey, 0) == 1;

        /// <summary>
        /// Grants the one-time starter kit — a bag of kibble — so the tutorial's first feed is
        /// possible without earning coins first. Idempotent: the flag means a reload (or a test
        /// that simulates a fresh save) never hands out a second bag.
        /// </summary>
        public static void GrantStarterKit()
        {
            if (StarterKitGranted) return;
            PlayerPrefs.SetInt(KitKey, 1);
            PlayerPrefs.Save();
            PetInventory.AddFood(StartingFood);
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(StarterKey);
            PlayerPrefs.DeleteKey(FirstFeedKey);
            PlayerPrefs.DeleteKey(KitKey);
            PlayerPrefs.Save();
        }
    }
}
