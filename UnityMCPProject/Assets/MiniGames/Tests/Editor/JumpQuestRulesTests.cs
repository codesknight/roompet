using DshMiniGames;
using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// The hop game's arithmetic.
    ///
    /// One input and one decision means almost nothing can go wrong except the numbers, which is
    /// exactly why they are worth pinning down: how far a charge carries, what counts as landing on
    /// the box, and whether the generator can ever hand the player a hop that is physically
    /// impossible.
    /// </summary>
    public class HopRulesTests
    {
        private static readonly HopSettings Settings = HopSettings.Default;

        [Test]
        public void HopDistance_GrowsWithTheChargeAndThenStops()
        {
            Assert.AreEqual(Settings.MinDistance, HopRules.HopDistance(0f, Settings), 0.001f,
                "a tap still has to go somewhere");
            Assert.AreEqual(Settings.MinDistance, HopRules.HopDistance(-5f, Settings), 0.001f,
                "a negative hold is not a backwards jump");

            float previous = 0f;
            for (float held = 0f; held <= 3f; held += 0.1f)
            {
                float distance = HopRules.HopDistance(held, Settings);
                Assert.GreaterOrEqual(distance, previous - 0.0001f, "more charge cannot mean less");
                previous = distance;
            }

            Assert.AreEqual(Settings.MaxDistance, HopRules.HopDistance(10f, Settings), 0.001f,
                "holding forever must not fly off the map");
        }

        [Test]
        public void FullChargeSeconds_MatchesTheDistanceItBuys()
        {
            // The charge bar is scaled by this, so a wrong answer means the bar fills at a different
            // rate than the distance grows — the one thing the player reads.
            float full = HopRules.FullChargeSeconds(Settings);
            Assert.AreEqual(Settings.MaxDistance, HopRules.HopDistance(full, Settings), 0.02f);
            Assert.Less(HopRules.HopDistance(full * 0.5f, Settings), Settings.MaxDistance);
            Assert.Greater(full, 0.6f, "a charge that fills in half a second is a coin toss");
            Assert.Less(full, 2.5f, "and one that takes three seconds is a chore");
        }

        [Test]
        public void Judge_TellsAMissFromALandingFromAPerfectOne()
        {
            const float centre = 5f;

            Assert.AreEqual(HopRules.Landing.Perfect,
                HopRules.Judge(0f, centre, centre, Settings));
            Assert.AreEqual(HopRules.Landing.Perfect,
                HopRules.Judge(0f, centre + Settings.PerfectRadius * 0.9f, centre, Settings),
                "just inside the middle is still perfect");

            Assert.AreEqual(HopRules.Landing.Landed,
                HopRules.Judge(0f, centre + Settings.BoxHalfSize * 0.9f, centre, Settings),
                "on the box, off centre, is a landing");
            Assert.AreEqual(HopRules.Landing.Landed,
                HopRules.Judge(0f, centre - Settings.BoxHalfSize * 0.9f, centre, Settings));

            Assert.AreEqual(HopRules.Landing.Missed,
                HopRules.Judge(0f, centre + Settings.BoxHalfSize + 0.01f, centre, Settings),
                "past the edge is a miss");
            Assert.AreEqual(HopRules.Landing.Missed,
                HopRules.Judge(0f, centre - Settings.BoxHalfSize - 0.01f, centre, Settings),
                "short of the edge is a miss too");
        }

        [Test]
        public void Judge_KeepsThePerfectWindowInsideTheBox()
        {
            // A "perfect" zone wider than the box would be a contradiction, and a perfect radius of
            // zero would make the bonus unreachable.
            Assert.Less(Settings.PerfectRadius, Settings.BoxHalfSize * 0.5f);
            Assert.Greater(Settings.PerfectRadius, 0.05f);
        }

        [Test]
        public void NextGap_IsAlwaysReachableAndNeverTrivial()
        {
            // The whole reason the generator takes the settings: a box further away than a full
            // charge reaches is not a challenge, it is a lost run the player cannot see coming.
            float reach = Settings.MaxDistance;

            for (int score = 0; score <= 80; score += 4)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float gap = HopRules.NextGap(i / 20f, score, Settings);
                    Assert.GreaterOrEqual(gap, Settings.MinGap - 0.001f,
                        $"a gap of {gap:F2} at score {score} is shorter than the minimum");
                    Assert.Less(gap, reach, $"a gap of {gap:F2} at score {score} cannot be jumped");
                }
            }
        }

        [Test]
        public void NextGap_LeavesRoomForAMistakeAtEveryScore()
        {
            // The margin matters as much as the reach: a box that only a *perfectly* full charge can
            // reach turns every hop into a coin toss on a phone's frame timing.
            for (int score = 0; score <= 80; score += 8)
            {
                float longest = HopRules.NextGap(1f, score, Settings);
                Assert.LessOrEqual(longest, Settings.MaxDistance - 0.2f,
                    $"at score {score} the longest hop needs an exact full charge");
            }
        }

        [Test]
        public void NextGap_GetsHarderWithTheScoreAndThenStops()
        {
            float early = HopRules.NextGap(0f, 0, Settings);
            float late = HopRules.NextGap(0f, 60, Settings);
            Assert.Greater(late, early, "the shortest hop should get longer as the run goes on");

            float mid = HopRules.NextGap(0f, 20, Settings);
            Assert.GreaterOrEqual(late, mid - 0.0001f, "and it must never go backwards");
        }

        [Test]
        public void Points_PayDoubleForTheMiddle()
        {
            Assert.AreEqual(1, HopRules.PointsFor(HopRules.Landing.Landed));
            Assert.AreEqual(2, HopRules.PointsFor(HopRules.Landing.Perfect),
                "the middle is what the game is about");
            Assert.AreEqual(0, HopRules.PointsFor(HopRules.Landing.Missed));
        }

        [Test]
        public void Coins_RewardTheRunAndNeverGoNegative()
        {
            Assert.AreEqual(0, HopRules.CoinsFor(0));
            Assert.AreEqual(1, HopRules.CoinsFor(1));
            Assert.AreEqual(9, HopRules.CoinsFor(8), "every eighth point is worth a second coin");
            Assert.AreEqual(0, HopRules.CoinsFor(-10));

            int previous = -1;
            for (int score = 0; score < 60; score++)
            {
                int coins = HopRules.CoinsFor(score);
                Assert.GreaterOrEqual(coins, previous, "a better run cannot pay less");
                previous = coins;
            }
        }

        [Test]
        public void Rank_SaysSomethingAtEveryScore()
        {
            for (int score = 0; score < 90; score++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(HopRules.RankFor(score)), $"no rank at {score}");
            }
            Assert.AreNotEqual(HopRules.RankFor(0), HopRules.RankFor(60),
                "a rank that never changes is not a rank");
        }

        [Test]
        public void Arc_StartsAndEndsAtTheTwoPointsAndGoesUpInBetween()
        {
            // The animation must agree with the landing the rules already judged: the character
            // arrives exactly where Judge was told they would.
            var start = HopRules.ArcPosition(2f, 5f, 0f, Settings);
            var middle = HopRules.ArcPosition(2f, 5f, 0.5f, Settings);
            var end = HopRules.ArcPosition(2f, 5f, 1f, Settings);

            Assert.AreEqual(2f, start.x, 0.001f);
            Assert.AreEqual(0f, start.y, 0.001f);
            Assert.AreEqual(5f, end.x, 0.001f);
            Assert.AreEqual(0f, end.y, 0.001f);
            Assert.Greater(middle.y, 0f, "a hop that does not leave the ground is a slide");
            Assert.AreEqual(3.5f, middle.x, 0.001f);

            Assert.AreEqual(2f, HopRules.ArcPosition(2f, 5f, -1f, Settings).x, 0.001f,
                "a negative t clamps rather than flying backwards");
            Assert.AreEqual(5f, HopRules.ArcPosition(2f, 5f, 9f, Settings).x, 0.001f);
        }

        [Test]
        public void Drift_KeepsTheBoxesNearTheLine()
        {
            // The landing test is one-dimensional: if the boxes drifted far sideways, the game would
            // be lying about where the player can land.
            for (int i = 0; i <= 10; i++)
            {
                float drift = HopRules.NextDrift(i / 10f, Settings);
                Assert.LessOrEqual(Mathf.Abs(drift), Settings.BoxHalfSize * 1.2f + 0.001f);
            }
            Assert.Less(HopRules.NextDrift(0f, Settings), HopRules.NextDrift(1f, Settings),
                "the roll has to actually move it");
        }
    }
}
