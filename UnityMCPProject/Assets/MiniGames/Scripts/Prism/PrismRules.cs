using UnityEngine;

namespace DshMiniGames
{
    /// <summary>The five pad shapes. Each maps to one note of a pentatonic scale (C D E G A).</summary>
    public enum PrismShape { Sphere, Cube, Cone, Cylinder, Capsule }

    /// <summary>The six pad colours. Each maps to a register (an octave-ish pitch shift).</summary>
    public enum PrismColor { Red, Orange, Yellow, Green, Blue, Purple }

    /// <summary>The fill decides timbre: piano (soft) or bell (bright, ringing).</summary>
    public enum PrismFill { Piano, Bell }

    /// <summary>Classic has no clock; Rush adds a countdown on every reproduction.</summary>
    public enum PrismMode { Classic, Rush }

    /// <summary>How many distinct pad combos are on the board.</summary>
    public enum PrismDifficulty { Rainbow, Spectrum, Prism }

    /// <summary>One pad: a shape, a colour and a fill. Its sound is fully determined by these three.</summary>
    public struct PrismCombo
    {
        public PrismShape Shape;
        public PrismColor Color;
        public PrismFill Fill;

        public PrismCombo(PrismShape shape, PrismColor color, PrismFill fill)
        {
            Shape = shape;
            Color = color;
            Fill = fill;
        }

        public static bool operator ==(PrismCombo a, PrismCombo b)
            => a.Shape == b.Shape && a.Color == b.Color && a.Fill == b.Fill;

        public static bool operator !=(PrismCombo a, PrismCombo b) => !(a == b);

        public override bool Equals(object o) => o is PrismCombo && this == (PrismCombo)o;
        public override int GetHashCode() => ((int)Shape * 100) + ((int)Color * 10) + (int)Fill;
        public override string ToString() => Shape + "/" + Color + "/" + Fill;
    }

    /// <summary>
    /// The arithmetic of the sequence-memory game: how many combos a difficulty shows, how a
    /// sequence is generated, and how a combo turns into a sound. All pure, all testable.
    /// </summary>
    public static class PrismRules
    {
        /// <summary>A round starts with three notes.</summary>
        public const int StartLength = 3;

        public const int ShapeCount = 5;
        public const int ColorCount = 6;
        public const int FillCount = 2;

        public const int RainbowCombos = 6;
        public const int SpectrumCombos = 30;
        public const int PrismCombos = 60;

        /// <summary>Best length needed to unlock the next difficulty tier.</summary>
        public const int SpectrumUnlockLength = 8;
        public const int PrismUnlockLength = 15;

        /// <summary>Pentatonic C4 D4 E4 G4 A4, one per shape.</summary>
        private static readonly float[] Pitches = { 261.63f, 293.66f, 329.63f, 392f, 440f };

        /// <summary>Six registers, one per colour: how far above or below the base pitch a pad sounds.</summary>
        private static readonly float[] Registers = { 0.5f, 0.63f, 0.79f, 1f, 1.26f, 1.59f };

        /// <summary>How long a played note rings.</summary>
        public const float NoteSeconds = 0.42f;

        public static int ComboCountFor(PrismDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PrismDifficulty.Rainbow: return RainbowCombos;
                case PrismDifficulty.Spectrum: return SpectrumCombos;
                default: return PrismCombos;
            }
        }

        /// <summary>
        /// The combo at a board index, deterministic per difficulty. Rainbow is a 3-shape × 2-colour
        /// starter; Spectrum is every shape-colour pair (one fill); Prism is the full 5 × 6 × 2.
        /// </summary>
        public static PrismCombo ComboAt(PrismDifficulty difficulty, int index)
        {
            int n = ComboCountFor(difficulty);
            index = ((index % n) + n) % n;

            switch (difficulty)
            {
                case PrismDifficulty.Rainbow:
                    return new PrismCombo((PrismShape)(index % 3), (PrismColor)(index / 3), PrismFill.Piano);
                case PrismDifficulty.Spectrum:
                    return new PrismCombo((PrismShape)(index % 5), (PrismColor)((index / 5) % 6), PrismFill.Piano);
                default:
                    return new PrismCombo((PrismShape)(index % 5), (PrismColor)((index / 5) % 6),
                        (PrismFill)((index / 30) % 2));
            }
        }

        /// <summary>A random sequence drawn from the difficulty's board, reproducible by seed.</summary>
        public static PrismCombo[] Sequence(int seed, int length, PrismDifficulty difficulty)
        {
            int n = ComboCountFor(difficulty);
            var rng = new System.Random(seed);
            var sequence = new PrismCombo[Mathf.Max(0, length)];
            for (int i = 0; i < sequence.Length; i++) sequence[i] = ComboAt(difficulty, rng.Next(n));
            return sequence;
        }

        /// <summary>The pitch of a pad: shape sets the note, colour shifts the register.</summary>
        public static float FrequencyOf(PrismCombo combo)
            => Pitches[(int)combo.Shape] * Registers[(int)combo.Color];

        /// <summary>Fill sets the timbre: piano is a soft sine, bell is a bright ringing overtone.</summary>
        public static float HarmonicOf(PrismFill fill) => fill == PrismFill.Piano ? 0.12f : 1.35f;

        /// <summary>Bell rings longer than a piano note.</summary>
        public static float DecayOf(PrismFill fill) => fill == PrismFill.Piano ? 0.10f : 0.50f;

        /// <summary>Which difficulty a player's best length has unlocked.</summary>
        public static PrismDifficulty UnlockedFor(int bestLength)
        {
            if (bestLength >= PrismUnlockLength) return PrismDifficulty.Prism;
            if (bestLength >= SpectrumUnlockLength) return PrismDifficulty.Spectrum;
            return PrismDifficulty.Rainbow;
        }

        /// <summary>The score is the sequence length the player persisted to.</summary>
        public static int ScoreFor(int completedLength) => Mathf.Max(0, completedLength);

        /// <summary>Coins a run pays: half the score, with a bonus for a long run.</summary>
        public static int CoinsFor(int score)
            => Mathf.Max(0, score) / 2 + Mathf.Max(0, score - 6) / 4;
    }
}
