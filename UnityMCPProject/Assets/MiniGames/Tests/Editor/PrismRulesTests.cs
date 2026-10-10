using NUnit.Framework;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// The sequence-memory game's arithmetic: board sizes, deterministic combos, the shape→pitch /
    /// colour→register / fill→timbre mapping, and the difficulty unlock curve.
    /// </summary>
    public class PrismRulesTests
    {
        [Test]
        public void DifficultyComboCountsAre6Then30Then60()
        {
            Assert.AreEqual(6, PrismRules.ComboCountFor(PrismDifficulty.Rainbow));
            Assert.AreEqual(30, PrismRules.ComboCountFor(PrismDifficulty.Spectrum));
            Assert.AreEqual(60, PrismRules.ComboCountFor(PrismDifficulty.Prism));
        }

        [Test]
        public void PrismBoardIsExactly60UniqueCombos()
        {
            var seen = new System.Collections.Generic.HashSet<PrismCombo>();
            for (int i = 0; i < PrismRules.PrismCombos; i++) seen.Add(PrismRules.ComboAt(PrismDifficulty.Prism, i));
            Assert.AreEqual(60, seen.Count, "the full board has 60 distinct shape/colour/fill combos");

            var spectrum = new System.Collections.Generic.HashSet<PrismCombo>();
            for (int i = 0; i < PrismRules.SpectrumCombos; i++) spectrum.Add(PrismRules.ComboAt(PrismDifficulty.Spectrum, i));
            Assert.AreEqual(30, spectrum.Count, "spectrum has 30 distinct shape/colour combos");
        }

        [Test]
        public void ShapeSetsThePitchAndColourShiftsTheRegister()
        {
            // Same shape + colour, different fill: same pitch (fill only changes timbre).
            var a = new PrismCombo(PrismShape.Sphere, PrismColor.Red, PrismFill.Piano);
            var b = new PrismCombo(PrismShape.Sphere, PrismColor.Red, PrismFill.Bell);
            Assert.AreEqual(PrismRules.FrequencyOf(a), PrismRules.FrequencyOf(b));

            // Different shape: different base note.
            var c = new PrismCombo(PrismShape.Cube, PrismColor.Red, PrismFill.Piano);
            Assert.AreNotEqual(PrismRules.FrequencyOf(a), PrismRules.FrequencyOf(c));

            // Different colour: different register (the ratio is constant per colour, not random).
            var d = new PrismCombo(PrismShape.Sphere, PrismColor.Purple, PrismFill.Piano);
            Assert.Greater(PrismRules.FrequencyOf(d), PrismRules.FrequencyOf(a),
                "a higher register sounds higher at the same shape");
        }

        [Test]
        public void FillChangesTimbreNotPitch()
        {
            var piano = new PrismCombo(PrismShape.Sphere, PrismColor.Blue, PrismFill.Piano);
            var bell = new PrismCombo(PrismShape.Sphere, PrismColor.Blue, PrismFill.Bell);

            Assert.AreEqual(PrismRules.FrequencyOf(piano), PrismRules.FrequencyOf(bell), "same note");
            Assert.Greater(PrismRules.HarmonicOf(bell.Fill), PrismRules.HarmonicOf(piano.Fill),
                "the bell has a brighter overtone than the piano");
            Assert.Greater(PrismRules.DecayOf(bell.Fill), PrismRules.DecayOf(piano.Fill),
                "the bell rings longer");
        }

        [Test]
        public void SequenceIsSeededAndStaysOnTheBoard()
        {
            var first = PrismRules.Sequence(1234, 12, PrismDifficulty.Spectrum);
            var again = PrismRules.Sequence(1234, 12, PrismDifficulty.Spectrum);
            Assert.AreEqual(12, first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i], again[i], "the same seed replays the same sequence");
            }

            var board = new System.Collections.Generic.HashSet<PrismCombo>();
            for (int i = 0; i < PrismRules.SpectrumCombos; i++) board.Add(PrismRules.ComboAt(PrismDifficulty.Spectrum, i));
            foreach (var combo in first) Assert.IsTrue(board.Contains(combo), "every note is on the board");
        }

        [Test]
        public void DifficultyUnlocksAtTheDocumentedLengths()
        {
            Assert.AreEqual(PrismDifficulty.Rainbow, PrismRules.UnlockedFor(0));
            Assert.AreEqual(PrismDifficulty.Rainbow, PrismRules.UnlockedFor(7));
            Assert.AreEqual(PrismDifficulty.Spectrum, PrismRules.UnlockedFor(8));
            Assert.AreEqual(PrismDifficulty.Spectrum, PrismRules.UnlockedFor(14));
            Assert.AreEqual(PrismDifficulty.Prism, PrismRules.UnlockedFor(15));
            Assert.AreEqual(PrismDifficulty.Prism, PrismRules.UnlockedFor(60));
        }

        [Test]
        public void ScoreAndCoinsAreMonotonic()
        {
            Assert.AreEqual(0, PrismRules.ScoreFor(-3));
            Assert.AreEqual(8, PrismRules.ScoreFor(8));
            Assert.Greater(PrismRules.CoinsFor(10), PrismRules.CoinsFor(4), "a longer run pays more");
        }
    }
}
