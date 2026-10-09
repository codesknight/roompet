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

            // …and it has to be more than one flap tall: a single flap lifts the bird
            // FlapSpeed^2/2g units, and a hole that is exactly that gives the player nowhere to aim.
            float flapRise = Settings.FlapSpeed * Settings.FlapSpeed / (2f * Settings.Gravity);
            Assert.Greater(Settings.MinGapHeight, flapRise * 2f - 0.3f,
                $"the tightest hole ({Settings.MinGapHeight}) is barely one flap tall ({flapRise:F2})");
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

        // ------------------------------------------------------- the reachable generator

        /// <summary>
        /// Plays a whole run with a machine that always aims at the next gap.
        ///
        /// This is a faithful copy of what <c>FlyBirdGame</c> does with the same rules: pipes spawn
        /// at a fixed spacing, travel left, and the bird is tested against a pipe **every frame**
        /// while that pipe is at the bird's x. Checking only the moment of passing — which is what
        /// the first version of this helper did — lets the bird clip a pipe on the way in and still
        /// be recorded as a clean run, which is exactly the kind of test that passes while the game
        /// is unfair.
        /// </summary>
        private static int FlyAutopilot(int pipeCount, int seed, float dt = 1f / 60f)
        {
            const float halfWidth = 5.3f;              // a portrait view, as the phone frames it
            float birdX = -halfWidth + 1.1f;

            var rng = new System.Random(seed);
            float height = 0.4f, velocity = 0f, lastCentre = height, nextSpawn = 4.5f;

            var pipes = new System.Collections.Generic.List<TestPipe>();
            int score = 0;
            float elapsed = 0f;

            while (score < pipeCount && elapsed < 900f)
            {
                elapsed += dt;
                float speed = FlyBirdRules.SpeedFor(score, Settings);

                for (int i = 0; i < pipes.Count; i++) pipes[i].X -= speed * dt;

                nextSpawn -= speed * dt;
                if (nextSpawn <= 0f)
                {
                    float gap = FlyBirdRules.GapFor(score, Settings);
                    float centre = FlyBirdRules.NextGapCentre((float)rng.NextDouble(), lastCentre, gap,
                        score, Settings);
                    lastCentre = centre;
                    pipes.Add(new TestPipe { X = halfWidth + 1.6f, Centre = centre, Gap = gap });
                    nextSpawn = FlyBirdRules.SpacingFor(score, Settings);
                }

                // The controller: aim at the hole it is flying through, and keep aiming at it until
                // the hole is clear — switching to the next one while still inside a pipe is how a
                // player clips the pipe they are in. Then: flap while below the middle of the hole,
                // and let gravity do the descending. The clearing margin is just past the collision
                // window (0.75): a run that waits longer than that reaches the next hole late.
                float target = 0f;
                float nearestAhead = float.MaxValue;
                for (int i = 0; i < pipes.Count; i++)
                {
                    if (pipes[i].X < birdX - 0.9f || pipes[i].X >= nearestAhead) continue;
                    nearestAhead = pipes[i].X;
                    target = pipes[i].Centre;
                }

                if (height < target && velocity <= 0.5f) FlyBirdRules.Step(ref height, ref velocity, dt, true, Settings);
                else FlyBirdRules.Step(ref height, ref velocity, dt, false, Settings);

                for (int i = pipes.Count - 1; i >= 0; i--)
                {
                    var pipe = pipes[i];

                    if (!pipe.Scored && pipe.X < birdX)
                    {
                        pipe.Scored = true;
                        score++;
                    }

                    if (Mathf.Abs(pipe.X - birdX) < 0.75f &&
                        !FlyBirdRules.PassesGap(height, 0.30f, pipe.Centre, pipe.Gap))
                    {
                        return score;
                    }

                    if (pipe.X < birdX - 8f) pipes.RemoveAt(i);
                }

                if (height < -Settings.PlayHeight + Settings.GroundHeight) return score;
            }

            return score;
        }

        private class TestPipe
        {
            public float X;
            public float Centre;
            public float Gap;
            public bool Scored;
        }

        [Test]
        public void Generator_NeverAsksForAFlightTheBirdCannotMake()
        {
            // This is the bug the phone reported as "the pipes are too random": a gap drawn from the
            // whole play area can be 14 units from the last one when the bird can travel about 4, so
            // the run was lost before the player touched the screen.
            var rng = new System.Random(20250607);

            for (int pipe = 0; pipe < 4000; pipe++)
            {
                int score = pipe % 60;
                float gap = FlyBirdRules.GapFor(score, Settings);

                float from = FlyBirdRules.GapCentre((float)rng.NextDouble(), gap, Settings, 1f);
                float to = FlyBirdRules.NextGapCentre((float)rng.NextDouble(), from, gap, score, Settings);

                Assert.IsTrue(FlyBirdRules.IsReachable(from, to, gap, score, Settings),
                    $"pipe {pipe} at score {score} asks to move from {from:F2} to {to:F2}, " +
                    "which the bird cannot fly");

                // …and the hole still has to be on screen.
                Assert.GreaterOrEqual(to - gap * 0.5f, -Settings.PlayHeight - 0.001f);
                Assert.LessOrEqual(to + gap * 0.5f, Settings.PlayHeight + 0.001f);
            }
        }

        [Test]
        public void Generator_DifficultyComesFromSpeedAndGapsNotFromImpossibleJumps()
        {
            // What has to be true at once: the game gets harder as the number climbs, and the moves
            // it asks for stay inside an achievable window the whole way up.
            //
            // This test used to assert that the reachable window *shrinks* with the score, which was
            // true when the pipe spacing was a fixed distance: faster pipes meant less time between
            // them. That turned out to be the wrong design — at the cap the pipes arrived every 0.5
            // seconds while one flap cycle takes 0.64 — so the spacing now grows with the speed and
            // the rhythm is floored. Difficulty comes from the hole shrinking, from the hole crossing
            // the bird faster, and from the pipes wandering further between one another.
            float gapStart = FlyBirdRules.GapFor(0, Settings);
            float gapEnd = FlyBirdRules.GapFor(60, Settings);
            Assert.Less(gapEnd, gapStart, "the hole has to get tighter");

            Assert.Greater(FlyBirdRules.SpeedFor(60, Settings), FlyBirdRules.SpeedFor(0, Settings),
                "and the hole has to cross the bird faster");

            float firstUp = 0f, firstDown = 0f, laterUp = 0f, laterDown = 0f;
            FlyBirdRules.ReachFor(0, Settings, out firstUp, out firstDown);
            FlyBirdRules.ReachFor(60, Settings, out laterUp, out laterDown);
            Assert.Greater(laterUp + laterDown, firstUp + firstDown,
                "and the pipes have to wander further between one another, not less");

            for (int score = 0; score <= 80; score++)
            {
                float up, down;
                FlyBirdRules.ReachFor(score, Settings, out up, out down);

                float seconds = FlyBirdRules.PipeSeconds(score, Settings);
                float climb = Mathf.Min(Settings.FlapSpeed, Settings.MaxRiseSpeed);

                // Never more than the bird can physically do, and never a window so small that the
                // pipes become a straight tunnel the player just holds altitude through.
                Assert.LessOrEqual(up, climb * seconds + 0.001f, $"unreachable climb at score {score}");
                Assert.LessOrEqual(down, FlyBirdRules.FallDistance(seconds, Settings) + 0.001f,
                    $"the bird cannot fall that far in time at score {score}");
                Assert.Greater(up + down, 1.2f, $"the pipes barely move at score {score}, which is not a game");

                // And the rhythm has to stay longer than one flap cycle, at every score: a rhythm
                // shorter than the bird's own cycle is a coin flip, not a difficulty curve.
                float cycle = 2f * Settings.FlapSpeed / Settings.Gravity;
                Assert.Greater(seconds, cycle,
                    $"at score {score} the pipes arrive every {seconds:F2}s but one flap takes {cycle:F2}s");
            }
        }

        [Test]
        public void Generator_AnAutopilotCanFlyALongRun()
        {
            // The end-to-end claim: "there is a difficulty curve, and it is always passable". A
            // controller that only knows "the hole is above me, flap" clears 150 pipes in a row on
            // twenty different generated runs, with the bird checked against every pipe on every
            // frame it is passing — which is the strongest statement this project can make about a
            // game without a human thumb.
            //
            // This test failed when it was first written honestly: the generator was asking for
            // moves that were reachable *in theory* (from a standstill, at the very limit of the
            // bird's travel) but not by anything that actually plays the game. The safety share came
            // down from 0.7 of the bird's travel to 0.5 as a result.
            for (int seed = 1; seed <= 20; seed++)
            {
                int cleared = FlyAutopilot(150, seed * 7919);
                Assert.AreEqual(150, cleared,
                    $"seed {seed} produced a run the autopilot could not finish ({cleared}/150 pipes)");
            }
        }

        [Test]
        public void Generator_StillWandersSoTheRunIsNotAStraightLine()
        {
            // The other way to make every run passable is to stop moving the gaps at all. That would
            // pass every test above and be a terrible game, so the spread is asserted too.
            var rng = new System.Random(4242);
            float centre = 0f;
            float lowest = float.MaxValue, highest = float.MinValue;

            for (int pipe = 0; pipe < 300; pipe++)
            {
                centre = FlyBirdRules.NextGapCentre((float)rng.NextDouble(), centre,
                    FlyBirdRules.GapFor(pipe, Settings), pipe, Settings);
                lowest = Mathf.Min(lowest, centre);
                highest = Mathf.Max(highest, centre);
            }

            Assert.Greater(highest - lowest, Settings.PlayHeight * 0.5f,
                "the gaps have to use the height of the screen, or the game plays itself");
        }
    }
}
