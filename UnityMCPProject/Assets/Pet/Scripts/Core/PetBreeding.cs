using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Two pets in the same room having a baby.
    ///
    /// Local and free, like the chatter it grows out of: the whole thing is a coin flip on a
    /// temperament blend, and it must not cost a token. It is also the one place in the game where
    /// a *new pet* appears without the shop — which is the point: the shop sells you an animal, this
    /// gives you one that is made of the two you already have.
    ///
    /// The child is a real blend rather than a copy: species from one parent, and each of the four
    /// temperament numbers averaged between them with a little noise. Two calm cats produce a calm
    /// kitten; a bold fox and a shy rabbit produce something in between, which is exactly the kind
    /// of "wait, what will this be" the feature is for.
    /// </summary>
    public static class PetBreeding
    {
        /// <summary>Minutes between two litters, so a room cannot become a factory.</summary>
        public const int CooldownMinutes = 10;

        /// <summary>Chance of a litter on a good exchange, before the temperament weighting.</summary>
        public const float BaseChance = 0.07f;

        /// <summary>Below this affection, nothing happens — a new pet is a reward, not a leak.</summary>
        public const float MinAffection = 0.25f;

        public const string LastLitterKey = "dshpet.breeding.at";

        /// <summary>How long ago the last litter was, in minutes. Large when there has never been one.</summary>
        public static float MinutesSinceLastLitter()
        {
            string raw = PlayerPrefs.GetString(LastLitterKey, "");
            if (string.IsNullOrEmpty(raw)) return float.MaxValue;

            System.DateTime when;
            if (!System.DateTime.TryParse(raw, out when)) return float.MaxValue;
            return (float)(System.DateTime.UtcNow - when.ToUniversalTime()).TotalMinutes;
        }

        public static void MarkLitter()
        {
            PlayerPrefs.SetString(LastLitterKey, System.DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
        }

        public static void ResetCooldown()
        {
            PlayerPrefs.DeleteKey(LastLitterKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Whether this exchange produces a kitten, and how likely it was.
        ///
        /// Pure: the two records, the mood of the pair, the roll, and the clock are all arguments,
        /// so "a playful friendly pair breeds more often than two strangers" is a test rather than
        /// something you have to watch for an hour.
        /// </summary>
        public static bool TryBreed(PetRecord a, PetRecord b, float affection, float minutesSinceLast,
            float roll, out float chance)
        {
            chance = 0f;

            if (a == null || b == null) return false;
            if (a.Id == b.Id) return false;
            if (minutesSinceLast < CooldownMinutes) return false;
            if (affection < MinAffection) return false;

            // Comfortable, familiar pets are likelier; the pair's own temperaments colour it a
            // little, so a lively household is a livelier household.
            float warmth = Mathf.InverseLerp(MinAffection, 1f, affection);
            float liveliness = (a.Personality.Liveliness + b.Personality.Liveliness) * 0.5f;
            float clinginess = (a.Personality.Clinginess + b.Personality.Clinginess) * 0.5f;

            chance = BaseChance * Mathf.Lerp(0.5f, 2f, warmth) * Mathf.Lerp(0.75f, 1.35f, clinginess)
                     * Mathf.Lerp(0.85f, 1.15f, liveliness);

            return roll < chance;
        }

        /// <summary>
        /// The child. <paramref name="seed"/> drives both the species pick and the noise, so the
        /// same parents and the same seed always produce the same kitten — which is what makes it
        /// testable.
        /// </summary>
        public static PetRecord Child(PetRecord a, PetRecord b, int seed, string name = null)
        {
            if (a == null || b == null) return null;

            var rng = new System.Random(seed);

            // Either parent's species: the child of a fox and a rabbit is one or the other, which
            // keeps the species table meaningful and the art honest.
            string speciesId = rng.Next(2) == 0 ? a.SpeciesId : b.SpeciesId;
            var species = PetSpecies.Get(speciesId);

            var personality = new PetPersonality
            {
                Liveliness = Blend(a.Personality.Liveliness, b.Personality.Liveliness, rng),
                Clinginess = Blend(a.Personality.Clinginess, b.Personality.Clinginess, rng),
                Curiosity = Blend(a.Personality.Curiosity, b.Personality.Curiosity, rng),
                Neatness = Blend(a.Personality.Neatness, b.Personality.Neatness, rng)
            };

            string chosen = string.IsNullOrEmpty(name)
                ? PetCollection.UniqueName(species != null ? species.DisplayName : "宝宝")
                : PetCollection.UniqueName(name);

            var child = PetRecord.Create(speciesId, chosen, personality, seed);
            child.Parents = (a.Name ?? "") + " 和 " + (b.Name ?? "");
            return child;
        }

        /// <summary>Average of the two parents' numbers, plus a little noise, clamped to 0..1.</summary>
        private static float Blend(float first, float second, System.Random rng)
        {
            float middle = (first + second) * 0.5f;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.12f;
            return Mathf.Clamp01(middle + noise);
        }

        /// <summary>What the room says about it, written as a stage direction like the chatter.</summary>
        public static string Announcement(PetRecord first, PetRecord second, PetRecord child)
        {
            if (child == null) return "";
            string a = first != null ? first.Name : "它";
            string b = second != null ? second.Name : "它";
            return $"（{a}和{b}在你脚边挤成一团，然后家里多了一只{child.Name}）";
        }
    }
}
