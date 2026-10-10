using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>The tunnel runner's arithmetic: reachable gates, dodgeable mines, clamping, scoring.</summary>
    public class TunnelRulesTests
    {
        [Test]
        public void ShipStaysInsideTheTunnel()
        {
            var inside = TunnelRules.ClampToTunnel(new Vector2(1f, 1f));
            Assert.LessOrEqual(inside.magnitude, TunnelRules.TunnelRadius - TunnelRules.ShipRadius + 0.001f,
                "the ship never leaves the cross-section circle");

            var far = TunnelRules.ClampToTunnel(new Vector2(100f, -100f));
            Assert.LessOrEqual(far.magnitude, TunnelRules.TunnelRadius - TunnelRules.ShipRadius + 0.001f);
        }

        [Test]
        public void EveryGateHoleIsReachableFromThePreviousOne()
        {
            var from = Vector2.zero;
            for (int i = 0; i < 200; i++)
            {
                var hole = TunnelRules.NextGateHole(from, Random.value, Random.value, TunnelRules.GateHoleRadiusFor(0));
                float maxJump = TunnelRules.ShipSpeed * TunnelRules.TimeBetweenObstacles;
                Assert.LessOrEqual(Vector2.Distance(from, hole), maxJump + 0.001f,
                    "the next hole is always within the ship's reach");
                from = hole;
            }
        }

        [Test]
        public void AGeneratedMineIsNotOnTopOfTheShip()
        {
            var ship = new Vector2(0.5f, 0.2f);
            for (int i = 0; i < 100; i++)
            {
                var mine = TunnelRules.NextMine(ship, Random.value, Random.value);
                Assert.Greater(Vector2.Distance(ship, mine), TunnelRules.MineRadius + TunnelRules.ShipRadius,
                    "a mine never spawns already touching the ship");
            }
        }

        [Test]
        public void CollisionRespectsTheRadius()
        {
            var ship = Vector2.zero;
            Assert.IsTrue(TunnelRules.Collides(ship, new Vector2(0.1f, 0f), 0.4f));
            Assert.IsFalse(TunnelRules.Collides(ship, new Vector2(0.9f, 0f), 0.4f));
        }

        [Test]
        public void ScoringIsMonotonic()
        {
            Assert.AreEqual(0, TunnelRules.ScoreForPass(0));
            Assert.AreEqual(5, TunnelRules.ScoreForPass(5));
            Assert.Greater(TunnelRules.CoinsFor(20), TunnelRules.CoinsFor(5));
        }

        [Test]
        public void ThemesHaveNamesAndColours()
        {
            foreach (TunnelTheme theme in System.Enum.GetValues(typeof(TunnelTheme)))
            {
                Assert.IsFalse(string.IsNullOrEmpty(TunnelRules.ThemeName(theme)));
                Assert.AreNotEqual(TunnelRules.TunnelColor(theme, true), TunnelRules.TunnelColor(theme, false),
                    "accent and base colours differ within a theme");
            }
        }

        [Test]
        public void HoleShapesJudgeInsideCorrectly()
        {
            var hole = Vector2.zero;
            const float r = 0.5f;

            // Circle: inside within radius, outside beyond.
            Assert.IsTrue(TunnelRules.IsInsideHole(new Vector2(0.2f, 0f), hole, GateShape.Circle, r));
            Assert.IsFalse(TunnelRules.IsInsideHole(new Vector2(0.9f, 0f), hole, GateShape.Circle, r));

            // Square: the corner of a square at distance 0.5 is outside.
            Assert.IsTrue(TunnelRules.IsInsideHole(new Vector2(0.4f, 0.4f), hole, GateShape.Square, r));
            Assert.IsFalse(TunnelRules.IsInsideHole(new Vector2(0.4f, 0.4f), hole, GateShape.Circle, r),
                "the square's corner is outside the circle, proving the shapes differ");

            // Triangle points up: its top vertex is above centre.
            Assert.IsTrue(TunnelRules.IsInsideHole(new Vector2(0f, 0.2f), hole, GateShape.Triangle, r));
            Assert.IsFalse(TunnelRules.IsInsideHole(new Vector2(0f, -0.4f), hole, GateShape.Triangle, r));

            // Semicircle: upper half only.
            Assert.IsTrue(TunnelRules.IsInsideHole(new Vector2(0f, 0.3f), hole, GateShape.Semicircle, r));
            Assert.IsFalse(TunnelRules.IsInsideHole(new Vector2(0f, -0.3f), hole, GateShape.Semicircle, r));
        }

        [Test]
        public void GateShapeVarietyGrowsWithScore()
        {
            Assert.AreEqual(GateShape.Circle, TunnelRules.GateShapeFor(0, 0), "early gates are all circles");
            // At high score every shape is reachable.
            var seen = new System.Collections.Generic.HashSet<GateShape>();
            for (int i = 0; i < 20; i++) seen.Add(TunnelRules.GateShapeFor(30, i));
            Assert.AreEqual(4, seen.Count, "late gates cycle through all four shapes");
        }

        [Test]
        public void SensitivityIsClamped()
        {
            Assert.AreEqual(0.5f, TunnelRules.ClampSensitivity(0.1f), 0.001f);
            Assert.AreEqual(2f, TunnelRules.ClampSensitivity(5f), 0.001f);
            Assert.AreEqual(1.2f, TunnelRules.ClampSensitivity(1.2f), 0.001f);
        }
    }
}
