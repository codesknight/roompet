using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The pet's temperament: four traits that bias what it wants to do, how quickly it wears
    /// out, and how it talks about itself.
    ///
    /// Why it exists: without it every pet of a species behaved identically and read as one
    /// character with four skins. Behaviour rows already score themselves from state; a
    /// personality is the missing multiplier that makes one fox a restless explorer and the
    /// next a sleepy lap cat — same room, same needs, different pet.
    ///
    /// Kept as plain data with pure methods so the archetype naming, the bias table and the
    /// serialisation round trip can all be unit tested without a scene. Each species gets its
    /// own personality, seeded from the species id, so switching animals is switching
    /// characters rather than re-rolling the pet you already know.
    /// </summary>
    [Serializable]
    public class PetPersonality
    {
        /// <summary>How much it wants to move and play rather than doze.</summary>
        public float Liveliness = 0.5f;

        /// <summary>How much it seeks the player out when nothing else is wrong.</summary>
        public float Clinginess = 0.5f;

        /// <summary>How much it pokes at the world instead of sitting still.</summary>
        public float Curiosity = 0.5f;

        /// <summary>How much it minds being dirty — the tidy ones groom unprompted.</summary>
        public float Neatness = 0.5f;

        /// <summary>Trait names for the HUD and the prompt, in trait order.</summary>
        public static readonly string[] TraitLabels = { "活力", "黏人", "好奇", "讲究" };

        public float[] Traits => new[] { Liveliness, Clinginess, Curiosity, Neatness };

        /// <summary>Index of the strongest trait, and how strong it is.</summary>
        public int DominantTrait
        {
            get
            {
                var traits = Traits;
                int best = 0;
                for (int i = 1; i < traits.Length; i++)
                {
                    if (traits[i] > traits[best]) best = i;
                }
                return best;
            }
        }

        /// <summary>
        /// A name for the whole temperament, taken from the strongest trait. Every random roll
        /// gets exactly one trait pushed well above the others (see <see cref="Create"/>), so
        /// this always names something real rather than a coin flip between equals.
        /// </summary>
        public string Archetype
        {
            get
            {
                switch (DominantTrait)
                {
                    case 0: return "上蹿下跳的小家伙";
                    case 1: return "甩不掉的跟屁虫";
                    case 2: return "什么都想闻闻的探险家";
                    default: return "一根毛都不许乱的小讲究";
                }
            }
        }

        /// <summary>One-line trait readout for the HUD.</summary>
        public string Summary
        {
            get
            {
                var traits = Traits;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < traits.Length; i++)
                {
                    if (i > 0) sb.Append("　");
                    sb.Append(TraitLabels[i]).Append(' ').Append(Mathf.RoundToInt(traits[i] * 100));
                }
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Restless pets burn energy faster and get tired sooner.</summary>
        public float EnergyDrainScale => Mathf.Lerp(0.85f, 1.30f, Liveliness);

        /// <summary>Tidy pets stay clean longer; scruffy ones need the bath sooner.</summary>
        public float CleanDrainScale => Mathf.Lerp(1.25f, 0.72f, Neatness);

        /// <summary>
        /// How fast joy drains. A clingy pet minds being ignored more, but is also easier to
        /// cheer up — both halves matter or "clingy" would just be a punishment.
        /// </summary>
        public float JoyDrainScale => Mathf.Lerp(1.15f, 0.85f, Clinginess);

        public float PlayJoyScale => Mathf.Lerp(0.9f, 1.2f, Liveliness);

        /// <summary>
        /// Multiplier on a behaviour's score, keyed by behaviour id.
        ///
        /// A table rather than per-row data on purpose: the bias is a property of the pet, not
        /// of the activity, and keeping it in one readable switch means the whole personality's
        /// effect on the behaviour table can be read (and tested) in one place.
        /// </summary>
        public float BiasFor(string behaviorId)
        {
            switch (behaviorId)
            {
                case "play":
                case "fetch":
                case "hop":
                    return 0.55f + Liveliness * 0.95f;

                case "seek_attention":
                case "sit_stare":
                case "speak_up":
                    return 0.55f + Clinginess * 0.95f;

                case "groom":
                case "bathe":
                    return 0.50f + Neatness * 1.05f;

                case "look_around":
                case "stretch":
                    return 0.65f + Curiosity * 0.75f;

                case "bask":
                case "sleep":
                case "nap_night":
                    return 1.45f - Liveliness * 0.95f;

                default:
                    return 1f;
            }
        }

        // ------------------------------------------------------------------- prompt

        /// <summary>
        /// The personality paragraph handed to the model, on top of the species persona.
        ///
        /// Written as prose rather than numbers: a model told "Liveliness 82" writes like a
        /// spec sheet, while "闲不住，一天到晚在屋里巡逻" writes like a pet.
        /// </summary>
        public string PromptLine()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("你的脾气是「").Append(Archetype).Append("」。");
            sb.Append(Liveliness > 0.62f ? "你闲不住，一天到晚在屋里巡逻；"
                : Liveliness < 0.38f ? "你偏爱趴着，能不动就不动；"
                : "你动一动也歇一歇；");
            sb.Append(Clinginess > 0.62f ? "你特别黏主人，主人一出现你就想凑过去；"
                : Clinginess < 0.38f ? "你有点独立，喜欢自己待着，但心里是有主人的；"
                : "你喜欢陪着主人，但也不用一直挨着；");
            sb.Append(Curiosity > 0.62f ? "你什么都要闻一闻、看一看；"
                : Curiosity < 0.38f ? "你对新东西兴趣不大，熟悉的角落最舒服；"
                : "遇到新鲜东西你会凑过去看看；");
            sb.Append(Neatness > 0.62f ? "你见不得自己脏，毛乱了就要舔干净；"
                : Neatness < 0.38f ? "你不太在乎脏，玩起来滚一身灰也无所谓；"
                : "你会打理自己，但玩疯了也不介意脏一下。");
            return sb.ToString();
        }

        // ------------------------------------------------------------------- storage

        private static string PrefKey(string speciesId) => "dshpet.personality." + speciesId;

        public string Serialize() =>
            $"{Liveliness:F3}|{Clinginess:F3}|{Curiosity:F3}|{Neatness:F3}";

        /// <summary>
        /// Parses a stored personality, falling back per-trait rather than wholesale: a
        /// half-corrupt string should cost one trait, not the whole character.
        /// </summary>
        public static PetPersonality Parse(string raw, PetPersonality fallback)
        {
            var result = fallback != null ? fallback.Clone() : new PetPersonality();
            if (string.IsNullOrEmpty(raw)) return result;

            string[] parts = raw.Split('|');
            if (parts.Length < 4) return result;

            float[] values = { result.Liveliness, result.Clinginess, result.Curiosity, result.Neatness };
            for (int i = 0; i < 4; i++)
            {
                float parsed;
                if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out parsed))
                {
                    values[i] = Mathf.Clamp01(parsed);
                }
            }

            result.Liveliness = values[0];
            result.Clinginess = values[1];
            result.Curiosity = values[2];
            result.Neatness = values[3];
            return result;
        }

        public void Save(string speciesId)
        {
            PlayerPrefs.SetString(PrefKey(speciesId), Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Loads this species' personality, rolling and storing one on first sight.</summary>
        public static PetPersonality Load(string speciesId)
        {
            string key = PrefKey(speciesId);
            if (!PlayerPrefs.HasKey(key))
            {
                var created = Create(StableSeed(speciesId));
                created.Save(speciesId);
                return created;
            }

            return Parse(PlayerPrefs.GetString(key, ""), Create(StableSeed(speciesId)));
        }

        public PetPersonality Clone() => new PetPersonality
        {
            Liveliness = Liveliness,
            Clinginess = Clinginess,
            Curiosity = Curiosity,
            Neatness = Neatness
        };

        public void CopyFrom(PetPersonality other)
        {
            if (other == null) return;
            Liveliness = other.Liveliness;
            Clinginess = other.Clinginess;
            Curiosity = other.Curiosity;
            Neatness = other.Neatness;
        }

        // ------------------------------------------------------------------- rolling

        /// <summary>
        /// A personality for a species.
        ///
        /// One trait is deliberately pushed high and one low: four independent 0..1 rolls
        /// average out to the same bland middle every time, and "bland middle" is the exact
        /// problem this class exists to solve. The species id seeds it so the same animal
        /// always comes back the same character.
        /// </summary>
        public static PetPersonality Create(int seed)
        {
            var rng = new System.Random(seed);

            var traits = new float[4];
            for (int i = 0; i < 4; i++) traits[i] = 0.30f + (float)rng.NextDouble() * 0.40f;

            int dominant = rng.Next(4);
            int recessive = (dominant + 1 + rng.Next(3)) % 4;   // any other trait

            traits[dominant] = 0.72f + (float)rng.NextDouble() * 0.24f;
            traits[recessive] = 0.10f + (float)rng.NextDouble() * 0.24f;

            return new PetPersonality
            {
                Liveliness = traits[0],
                Clinginess = traits[1],
                Curiosity = traits[2],
                Neatness = traits[3]
            };
        }

        /// <summary>A repeatable seed per species, so "the fox" is always the same fox.</summary>
        public static int StableSeed(string speciesId)
        {
            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(speciesId))
                {
                    foreach (char c in speciesId) hash = hash * 31 + c;
                }
                return hash & 0x7FFFFFFF;
            }
        }
    }
}
