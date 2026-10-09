using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The garden's own rules, pure and therefore testable.
    ///
    /// The apple tree and the pond are furniture, but the numbers behind them are an economy:
    /// how many apples a tree can hold, what watering does, how long a fish takes to bite. Those
    /// are the parts that must not be guessed, so they live here rather than in a MonoBehaviour.
    /// </summary>
    public static class GardenRules
    {
        /// <summary>Most apples a tree can hold at once.</summary>
        public const int TreeMaxApples = 3;

        /// <summary>How long a tree takes to grow one more apple, in seconds.</summary>
        public const float AppleGrowSeconds = 90f;

        /// <summary>Watering the tree adds this many apples immediately (up to the cap).</summary>
        public const int WaterBoostApples = 1;

        /// <summary>Clamps an apple count to what a tree can hold.</summary>
        public static int ClampApples(int apples) => Mathf.Clamp(apples, 0, TreeMaxApples);

        /// <summary>The apple count after a watering.</summary>
        public static int AfterWatering(int apples) => ClampApples(apples + WaterBoostApples);

        /// <summary>Coins the pet's own water source is worth when it actually drinks.</summary>
        public const int DrinkCoinReward = 2;

        /// <summary>How long a fish takes to bite, in seconds.</summary>
        public const float FishBiteSeconds = 8f;

        /// <summary>Coins earned for selling a fish (matches the shop's fish price).</summary>
        public const int FishSell = 12;

        /// <summary>Coins earned for selling an apple.</summary>
        public const int AppleSell = 8;
    }
}
