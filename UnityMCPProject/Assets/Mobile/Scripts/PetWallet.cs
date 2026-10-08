using System;
using UnityEngine;

namespace DshMobile
{    /// <summary>
    /// The player's money.
    ///
    /// Coins earned in the runner are spendable in the pet room, which is the only reason the
    /// two halves of the game are one game: the runner pays for the shop, and the shop is what
    /// the runner's distance is for. Kept deliberately dead simple — one number, one file.
    /// </summary>
    public static class PetWallet
    {
        public const string Key = "dshpet.coins";

        /// <summary>Raised whenever the balance changes, so open panels can refresh.</summary>
        public static event Action Changed;

        public static int Coins
        {
            get => PlayerPrefs.GetInt(Key, 0);
            private set
            {
                PlayerPrefs.SetInt(Key, Mathf.Max(0, value));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void Add(int amount)
        {
            if (amount == 0) return;
            Coins += amount;
        }

        /// <summary>Spends coins, or returns false and changes nothing.</summary>
        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }

        public static bool CanAfford(int amount) => Coins >= amount;

        public static void Reset() => Coins = 0;

        /// <summary>The "earned in the runner, spent here" bridge, called on every run's end.</summary>
        public static void DepositRunCoins(int runCoins)
        {
            if (runCoins > 0) Add(runCoins);
        }
    }

}