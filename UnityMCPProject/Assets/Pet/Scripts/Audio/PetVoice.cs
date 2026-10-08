using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// How one animal sounds.
    ///
    /// Pure numbers, no audio: the profile is what makes a fox and a bear recognisable with the
    /// sound off, and it is deliberately separate from the synthesis so that "the bear sounds
    /// different from the rabbit" is a testable claim rather than something you have to listen
    /// for. <see cref="PetAudioDirector"/> turns a profile into a waveform.
    /// </summary>
    public struct PetVoiceProfile
    {
        /// <summary>Pitch of the first syllable, in Hz. Big animals are low.</summary>
        public float BaseHz;

        /// <summary>How far the pitch slides across a call. A rising call sounds like a question.</summary>
        public float SlideHz;

        /// <summary>Wobble rate in Hz — a purr or a tremble.</summary>
        public float VibratoHz;

        /// <summary>How much of the wobble is applied, 0..1.</summary>
        public float VibratoDepth;

        /// <summary>Length of one syllable, in seconds.</summary>
        public float SyllableSeconds;

        /// <summary>How many syllables in a call. Cats say one; foxes chatter.</summary>
        public int Syllables;

        /// <summary>Second-harmonic amount, 0..1. Low = hollow, high = bright.</summary>
        public float Harmonic;

        /// <summary>Overall loudness, 0..1.</summary>
        public float Gain;

        public override string ToString()
            => $"{BaseHz:F0}Hz slide {SlideHz:+0;-0;0} vib {VibratoHz:F0}@{VibratoDepth:F2} " +
               $"{Syllables}x{SyllableSeconds:F2}s h{Harmonic:F2}";
    }

    /// <summary>
    /// The pet's voice: a species body plus a personality on top.
    ///
    /// Two layers, because both matter and they are not the same thing. The species sets the
    /// register — a bear is low and slow, a rabbit is high and quick — and the personality
    /// bends it: a lively pet talks faster and higher, a clingy one adds a rising, questioning
    /// tail, a fastidious one is clipped and precise. So two cats can sound like two cats.
    ///
    /// All arithmetic, so it unit tests. Nothing here plays anything.
    /// </summary>
    public static class PetVoice
    {
        public const string EnabledKey = "dshpet.voice";

        private static bool _enabledLoaded;
        private static bool _enabled = true;

        /// <summary>
        /// Whether the pet makes any sound of its own at all.
        ///
        /// One switch for the whole voice layer, including the wordless "talking" chirp. The
        /// player who wants a silent pet gets a silent pet — a half-applied setting (music off,
        /// chirps still on) is worse than either extreme.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (!_enabledLoaded)
                {
                    _enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;
                    _enabledLoaded = true;
                }
                return _enabled;
            }
            set
            {
                _enabled = value;
                _enabledLoaded = true;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Forgets the cached setting, so the next read comes from disk.</summary>
        public static void ResetCache() => _enabledLoaded = false;

        /// <summary>The species' register, before the personality gets a say.</summary>
        public static PetVoiceProfile ForSpecies(PetSpecies species)
        {
            var profile = new PetVoiceProfile
            {
                BaseHz = 560f, SlideHz = 90f, VibratoHz = 22f, VibratoDepth = 0.10f,
                SyllableSeconds = 0.13f, Syllables = 2, Harmonic = 0.35f, Gain = 0.5f
            };

            if (species == null) return profile;

            switch (species.Id)
            {
                case "cat":
                    // A single, low, slightly bored call — the opposite of chatty.
                    profile.BaseHz = 430f; profile.SlideHz = -40f; profile.Syllables = 1;
                    profile.SyllableSeconds = 0.30f; profile.VibratoHz = 26f;
                    profile.VibratoDepth = 0.16f; profile.Harmonic = 0.25f; profile.Gain = 0.45f;
                    break;

                case "rabbit":
                    // High, quick, a bit trembly.
                    profile.BaseHz = 780f; profile.SlideHz = 60f; profile.Syllables = 2;
                    profile.SyllableSeconds = 0.08f; profile.VibratoHz = 30f;
                    profile.VibratoDepth = 0.06f; profile.Harmonic = 0.45f; profile.Gain = 0.38f;
                    break;

                case "bear":
                    // Low and unhurried.
                    profile.BaseHz = 210f; profile.SlideHz = -55f; profile.Syllables = 1;
                    profile.SyllableSeconds = 0.42f; profile.VibratoHz = 12f;
                    profile.VibratoDepth = 0.18f; profile.Harmonic = 0.2f; profile.Gain = 0.62f;
                    break;

                default: // fox
                    profile.BaseHz = 620f; profile.SlideHz = 180f; profile.Syllables = 3;
                    profile.SyllableSeconds = 0.09f; profile.VibratoHz = 18f;
                    profile.VibratoDepth = 0.08f; profile.Harmonic = 0.4f; profile.Gain = 0.48f;
                    break;
            }

            return profile;
        }

        /// <summary>
        /// The full voice: species register bent by the pet's temperament.
        ///
        /// The bends are small on purpose. A personality that doubled the pitch would erase the
        /// species, and then a lively bear and a lazy fox would be confusable — the animal has
        /// to survive the individual.
        /// </summary>
        public static PetVoiceProfile For(PetSpecies species, PetPersonality personality,
            PetMood mood = PetMood.Content)
        {
            var profile = ForSpecies(species);
            if (personality == null) return profile;

            // Lively: faster and brighter. Lazy: slower and heavier.
            float energy = Mathf.Lerp(0.75f, 1.25f, personality.Liveliness);
            profile.BaseHz *= Mathf.Lerp(0.93f, 1.10f, personality.Liveliness);
            profile.SyllableSeconds *= Mathf.Lerp(1.25f, 0.80f, personality.Liveliness);
            profile.Harmonic = Mathf.Clamp01(profile.Harmonic * energy);
            profile.VibratoDepth *= Mathf.Lerp(0.7f, 1.3f, personality.Liveliness);

            // Clingy: a rising tail, like a question. Independent: it trails off downward.
            profile.SlideHz += Mathf.Lerp(-45f, 70f, personality.Clinginess);

            // Curious: more syllables — they chatter. Also subtly higher.
            profile.Syllables = Mathf.Clamp(
                profile.Syllables + Mathf.RoundToInt(Mathf.Lerp(-0.6f, 1.4f, personality.Curiosity)),
                1, 5);

            // Fastidious: clipped and even, less wobble.
            profile.VibratoDepth *= Mathf.Lerp(1.2f, 0.7f, personality.Neatness);

            ApplyMood(ref profile, mood);
            return profile;
        }

        /// <summary>Mood shifts the same voice: a sleepy pet is slower and lower.</summary>
        private static void ApplyMood(ref PetVoiceProfile profile, PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Sleepy:
                    profile.BaseHz *= 0.85f;
                    profile.SyllableSeconds *= 1.5f;
                    profile.SlideHz -= 40f;
                    profile.Gain *= 0.7f;
                    break;
                case PetMood.Excited:
                    profile.BaseHz *= 1.12f;
                    profile.SyllableSeconds *= 0.8f;
                    profile.Syllables = Mathf.Min(5, profile.Syllables + 1);
                    profile.Gain *= 1.15f;
                    break;
                case PetMood.Hungry:
                case PetMood.NeedsToilet:
                    profile.SlideHz += 55f;      // insistent
                    profile.Syllables = Mathf.Min(5, profile.Syllables + 1);
                    break;
                case PetMood.Lonely:
                case PetMood.Bored:
                    profile.BaseHz *= 0.92f;
                    profile.SlideHz -= 50f;      // drooping
                    break;
                case PetMood.Dirty:
                    profile.VibratoDepth *= 1.25f;   // grubby and complaining
                    break;
            }

            profile.Gain = Mathf.Clamp(profile.Gain, 0.15f, 0.9f);
        }

        /// <summary>A short label for the HUD, so the voice setting is not a mystery switch.</summary>
        public static string Describe(PetSpecies species, PetPersonality personality)
        {
            var profile = For(species, personality);
            string register = profile.BaseHz < 320f ? "低沉" : profile.BaseHz < 520f ? "中音" : "清亮";
            string pace = profile.SyllableSeconds > 0.25f ? "慢" : profile.SyllableSeconds > 0.12f ? "适中" : "快";
            return $"{register}·{pace}·{profile.Syllables}音节";
        }
    }
}
