using DshMiniGames;
using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// The flappy game's arithmetic.
    ///
    /// A one-button game lives or dies on its numbers, and these are the ones that decide whether
    /// it is playable: how fast a tap lifts the bird, how much room the gap leaves, and whether
    /// the difficulty curve ever reaches a point where no thumb can play it.
    /// </summary>
    public class FlyBirdRulesTests
    {
        private static readonly FlyBirdSettings Settings = FlyBirdSettings.Default;

        [Test]
        public void Step_ATapLiftsTheBirdAndGravityBringsItBack()
        {
            float height = 0f, velocity = 0f;
            FlyBirdRules.Step(ref height, ref velocity, 1f / 60f, true, Settings);

            Assert.Greater(velocity, 0f, "a flap has to push the bird upwards");
            Assert.Greater(height, 0f, "and move it up in the same frame");

            // A tap is a hop, not a hover: it rises for about a third of a second and then falls.
            float peak = height;
            for (int i = 0; i < 60; i++)
            {
                FlyBirdRules.Step(ref height, ref velocity, 1f / 60f, false, Settings);
                peak = Mathf.Max(peak, height);
            }

            Assert.Less(height, peak, "gravity has to bring it back down");
            Assert.Less(peak, Settings.GapHeight,
                "one tap must not carry the bird through a whole gap on its own");
        }

        [Test]
        public void Step_IsFrameRateIndependent()
        {
            // The bug this guards: a game whose feel depends on the frame rate feels different on
            // every phone. One second of falling has to be one second of falling.
            float slowHeight = 0f, slowVelocity = 0f;
            for (int i = 0; i < 30; i++) FlyBirdRules.Step(ref slowHeight, ref slowVelocity, 1f / 30f, false, Settings);

            float fastHeight = 0f, fastVelocity = 0f;
            for (int i = 0; i < 120; i++) FlyBirdRules.Step(ref fastHeight, ref fastVelocity, 1f / 120f, false, Settings);

            Assert.AreEqual(slowHeight, fastHeight, 0.25f,
                "30fps and 120fps have to fall about the same distance in a second");
        }

        [Test]
        public void Step_ClampsALongFrameSoTheBirdCannotTeleportThroughAPipe()
        {
            // A backgrounded app, a shader compile, a garbage collection: any of them can hand a
            // frame that is ten times too long, and an unclamped step would move the bird further
            // than a pipe is wide — through it, without touching it.
            float height = 0f, velocity = 0f;
            FlyBirdRules.Step(ref height, ref velocity, 1f, false, Settings);

            float travelled = Mathf.Abs(height);
            Assert.LessOrEqual(travelled, Settings.MaxFallSpeed * 0.05f + 0.001f,
                "a one-second frame has to be treated as a 50ms one");
        }

        [Test]
        public void Step_RespectsItsSpeedLimits()
        {
            float height = 0f, velocity = 0f;
            for (int i = 0; i < 200; i++) FlyBirdRules.Step(ref height, ref velocity, 1f / 60f, true, Settings);
            Assert.LessOrEqual(velocity, Settings.MaxRiseSpeed + 0.001f, "spam-tapping must not launch it");

            for (int i = 0; i < 400; i++) FlyBirdRules.Step(ref height, ref velocity, 1f / 60f, false, Settings);
            Assert.GreaterOrEqual(velocity, -Settings.MaxFallSpeed - 0.001f, "and it must not fall forever");
        }

        [Test]
        public void Gap_ShrinksWithTheScoreButNeverCloses()
        {
            Assert.AreEqual(Settings.GapHeight, FlyBirdRules.GapFor(0, Settings), 0.001f);

            float previous = Settings.GapHeight + 1f;
            for (int score = 0; score < 200; score++)
            {
                float gap = FlyBirdRules.GapFor(score, Settings);
                Assert.LessOrEqual(gap, previous, "the gap must never grow back");
                Assert.GreaterOrEqual(gap, Settings.MinGapHeight - 0.001f,
                    $"the gap closed completely at score {score}");
                previous = gap;
            }

            Assert.Greater(Settings.MinGapHeight, 1f, "a hole under one bird wide is not a hole");
        }

        [Test]
        public void Speed_RisesWithTheScoreAndThenStops()
        {
            Assert.AreEqual(Settings.ScrollSpeed, FlyBirdRules.SpeedFor(0, Settings), 0.001f);

            float previous = 0f;
            for (int score = 0; score < 60; score++)
            {
                float speed = FlyBirdRules.SpeedFor(score, Settings);
                Assert.GreaterOrEqual(speed, previous, "the game must never slow down mid-run");
                previous = speed;
            }

            Assert.LessOrEqual(FlyBirdRules.SpeedFor(500, Settings), Settings.ScrollSpeed * 2.2f + 0.001f,
                "there has to be a ceiling a thumb can still play");
        }

        [Test]
        public void GapCentre_StaysReachableWhateverTheRoll()
        {
            // A gap whose hole is off the top or bottom of the screen is not a mistake the player
            // made, it is a run that was lost for them.
            for (int score = 0; score <= 40; score += 4)
            {
                float gap = FlyBirdRules.GapFor(score, Settings);
                for (int i = 0; i <= 20; i++)
                {
                    float roll = i / 20f;
                    float centre = FlyBirdRules.GapCentre(roll, gap, Settings, 1f);
                    float low = centre - gap * 0.5f;
                    float high = centre + gap * 0.5f;

                    Assert.GreaterOrEqual(low, -Settings.PlayHeight - 0.001f,
                        $"the gap hangs below the play area at score {score}, roll {roll}");
                    Assert.LessOrEqual(high, Settings.PlayHeight + 0.001f,
                        $"the gap runs off the top at score {score}, roll {roll}");
                }
            }
        }

        [Test]
        public void PassesGap_CountsTheBirdWidth()
        {
            // The bird is not a point: a collision test that ignores its size lets it clip a pipe
            // corner and survive, which reads as the game cheating.
            const float gap = 3f;
            const float radius = 0.3f;

            Assert.IsTrue(FlyBirdRules.PassesGap(0f, radius, 0f, gap));
            Assert.IsTrue(FlyBirdRules.PassesGap(gap * 0.5f - radius - 0.01f, radius, 0f, gap),
                "a bird that just fits should fit");
            Assert.IsFalse(FlyBirdRules.PassesGap(gap * 0.5f - radius + 0.05f, radius, 0f, gap),
                "a bird whose body crosses the edge has hit it");
            Assert.IsFalse(FlyBirdRules.PassesGap(-gap * 0.5f - 0.4f, radius, 0f, gap));
        }

        [Test]
        public void Coins_RewardTheRunAndNeverGoNegative()
        {
            Assert.AreEqual(0, FlyBirdRules.CoinsFor(0));
            Assert.AreEqual(1, FlyBirdRules.CoinsFor(1));
            Assert.AreEqual(6, FlyBirdRules.CoinsFor(5), "every fifth pipe is worth a second coin");
            Assert.AreEqual(0, FlyBirdRules.CoinsFor(-3), "a negative score cannot pay out");

            int previous = -1;
            for (int score = 0; score < 50; score++)
            {
                int coins = FlyBirdRules.CoinsFor(score);
                Assert.GreaterOrEqual(coins, previous, "more pipes can never mean fewer coins");
                previous = coins;
            }
        }

        [Test]
        public void Rank_SaysSomethingAtEveryScore()
        {
            for (int score = 0; score < 80; score++)
            {
                string rank = FlyBirdRules.RankFor(score);
                Assert.IsFalse(string.IsNullOrEmpty(rank), $"no rank at score {score}");
            }

            Assert.AreNotEqual(FlyBirdRules.RankFor(0), FlyBirdRules.RankFor(50),
                "a medal that never changes is not a medal");
        }
    }
}
