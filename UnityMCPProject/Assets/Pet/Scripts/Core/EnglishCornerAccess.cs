using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The English corner is its own scene, unlocked with pet coins — not a free in-room mini-game.
    /// A PlayerPrefs flag plus a one-time coin spend; pure enough to test.
    /// </summary>
    public static class EnglishCornerAccess
    {
        public const string UnlockedKey = "dshpet.english.unlocked";
        public const int Cost = 300;

        public static bool IsUnlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;

        /// <summary>Spends the coins once and unlocks the English corner. Returns false and leaves
        /// everything alone when it cannot.</summary>
        public static bool TryUnlock(out string message)
        {
            if (IsUnlocked)
            {
                message = "英语角已经解锁了";
                return true;
            }

            if (!DshMobile.PetWallet.TrySpend(Cost))
            {
                message = $"还差 {Cost - DshMobile.PetWallet.Coins} 个宠物币";
                return false;
            }

            PlayerPrefs.SetInt(UnlockedKey, 1);
            PlayerPrefs.Save();
            message = "解锁了英语角";
            return true;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.Save();
        }
    }
}
