using System;
using System.Collections.Generic;

namespace DshPet
{
    /// <summary>
    /// The 扭蛋机: spend a fixed amount, win a random gacha-only pet.
    ///
    /// The pool is the species marked <see cref="PetSpecies.GachaOnly"/> — they are not in the
    /// direct shop, so the only way to get them is to pull. The weights make one species common
    /// and one rare, which is the whole point of a gacha over a shop: a pull is a bet, and the
    /// result has to be uneven to be exciting. Pure and seeded, so the draw can be tested.
    /// </summary>
    public static class PetGacha
    {
        /// <summary>Coins per pull.</summary>
        public const int Cost = 500;

        /// <summary>The gacha-only species, in table order.</summary>
        public static List<PetSpecies> Pool()
        {
            var list = new List<PetSpecies>();
            foreach (var species in PetSpecies.All)
            {
                if (species.GachaOnly) list.Add(species);
            }
            return list;
        }

        /// <summary>How likely a species is, on a relative scale (higher = more common).</summary>
        public static int Weight(PetSpecies species)
        {
            if (species == null) return 0;
            switch (species.Id)
            {
                case "hamster": return 50;      // 常见
                case "red_panda": return 35;    // 少见
                case "penguin": return 15;      // 稀有
                default: return 40;
            }
        }

        /// <summary>Draws a species by weight. Null only when the pool is empty.</summary>
        public static PetSpecies Pick(Random rng)
        {
            var pool = Pool();
            if (pool.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < pool.Count; i++) total += Weight(pool[i]);
            if (total <= 0) return pool[0];

            int roll = rng.Next(total);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Weight(pool[i]);
                if (roll < 0) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>Human-readable odds, for the machine's fine print.</summary>
        public static string OddsText()
        {
            var pool = Pool();
            int total = 0;
            for (int i = 0; i < pool.Count; i++) total += Weight(pool[i]);
            if (total <= 0) return "奖池是空的";

            var parts = new List<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                int percent = (int)Math.Round(Weight(pool[i]) * 100.0 / total);
                parts.Add(pool[i].DisplayName + " " + percent + "%");
            }
            return string.Join("　", parts.ToArray());
        }
    }
}
