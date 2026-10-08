using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DshRunner.Tests
{
    /// <summary>
    /// Covers the parts of the game that are pure logic: scoring, the difficulty curve,
    /// the level table and the track generator's fairness rules.
    /// </summary>
    public class RunnerLogicTests
    {
        // ------------------------------------------------------------------ scoring

        [Test]
        public void DistanceMultiplier_GrowsWithDistance()
        {
            Assert.AreEqual(1f, ScoreRules.DistanceMultiplier(0f), 1e-4f);
            Assert.AreEqual(1f, ScoreRules.DistanceMultiplier(149f), 1e-4f);
            Assert.AreEqual(1.3f, ScoreRules.DistanceMultiplier(150f), 1e-4f);
            Assert.AreEqual(1.6f, ScoreRules.DistanceMultiplier(300f), 1e-4f);

            for (float d = 0f; d < 2000f; d += 25f)
            {
                Assert.LessOrEqual(ScoreRules.DistanceMultiplier(d),
                    ScoreRules.DistanceMultiplier(d + 25f) + 1e-4f,
                    "multiplier must never decrease as distance grows");
            }
        }

        [Test]
        public void ScoreKeeper_FurtherDistanceEarnsMorePerMetre()
        {
            var near = new ScoreKeeper();
            near.Advance(100f);
            float nearRate = near.RawScore / 100f;

            var far = new ScoreKeeper();
            // Walk out to 2000 m in steps, then measure a fresh segment's rate.
            for (int i = 0; i < 100; i++) far.Advance(20f);
            float before = far.RawScore;
            far.Advance(100f);
            float farRate = (far.RawScore - before) / 100f;

            Assert.Greater(farRate, nearRate * 3f,
                "2000 m in, each metre should be worth far more than at the start");
        }

        [Test]
        public void ScoreKeeper_CoinsAndMultiplierApply()
        {
            var score = new ScoreKeeper();
            score.AddCoin(3);
            Assert.AreEqual(3, score.Coins);
            Assert.AreEqual(3 * ScoreRules.CoinValue, score.Total);

            score.ExternalMultiplier = 2f;
            score.AddCoin(1);
            Assert.AreEqual(3 * ScoreRules.CoinValue + 2 * ScoreRules.CoinValue, score.Total);
        }

        [Test]
        public void ScoreKeeper_ResetClearsEverything()
        {
            var score = new ScoreKeeper();
            score.Advance(500f);
            score.AddCoin(9);
            score.ExternalMultiplier = 2f;
            score.Reset();

            Assert.AreEqual(0f, score.Distance, 1e-4f);
            Assert.AreEqual(0, score.Coins);
            Assert.AreEqual(0, score.Total);
            Assert.AreEqual(1f, score.ExternalMultiplier, 1e-4f);
        }

        // --------------------------------------------------------------- difficulty

        [Test]
        public void Difficulty_SpeedRampsAndClamps()
        {
            var level = LevelLibrary.Get(3);
            float atStart = Difficulty.Speed(level, 0f);
            float mid = Difficulty.Speed(level, level.TargetDistance * 0.5f);
            float atEnd = Difficulty.Speed(level, level.TargetDistance * 5f);

            Assert.AreEqual(level.StartSpeed, atStart, 1e-3f);
            Assert.Greater(mid, atStart);
            Assert.LessOrEqual(atEnd, level.MaxSpeed + 1e-3f);
            Assert.AreEqual(level.MaxSpeed, atEnd, 1e-3f);
        }

        [Test]
        public void Difficulty_ObstacleChanceRisesAndStaysWinnable()
        {
            var level = LevelLibrary.Get(4);
            float start = Difficulty.ObstacleChance(level, 0f);
            float end = Difficulty.ObstacleChance(level, level.TargetDistance);

            Assert.Greater(end, start);

            for (float d = 0f; d < 3000f; d += 50f)
            {
                float chance = Difficulty.ObstacleChance(level, d);
                Assert.Less(chance, 1f, "density must leave empty track at every distance");
                Assert.GreaterOrEqual(chance, 0f);
            }
        }

        [Test]
        public void Difficulty_NeverBlocksEveryLaneWithWalls()
        {
            foreach (var level in LevelLibrary.Levels)
            {
                for (float d = 0f; d < 4000f; d += 37f)
                {
                    int maxBlocked = Difficulty.MaxBlockedLanes(level, d);
                    Assert.Less(maxBlocked, GameConfig.LaneCount,
                        $"{level.Name} at {d} m would wall off every lane");
                    Assert.GreaterOrEqual(maxBlocked, 1);
                }
            }
        }

        [Test]
        public void Difficulty_ProgressIsNormalised()
        {
            var level = LevelLibrary.Get(2);
            Assert.AreEqual(0f, Difficulty.Progress(level, 0f), 1e-4f);
            Assert.AreEqual(0.5f, Difficulty.Progress(level, level.TargetDistance * 0.5f), 1e-4f);
            Assert.AreEqual(1f, Difficulty.Progress(level, level.TargetDistance * 2f), 1e-4f);
        }

        // -------------------------------------------------------------- level table

        [Test]
        public void LevelLibrary_IsOrderedAndConsistent()
        {
            Assert.GreaterOrEqual(LevelLibrary.Count, 5, "the level mode needs a real progression");
            Assert.IsTrue(LevelLibrary.Endless.Endless);
            Assert.AreEqual(0, LevelLibrary.Endless.Index);

            for (int i = 1; i < LevelLibrary.Count; i++)
            {
                var level = LevelLibrary.Get(i);
                Assert.AreEqual(i, level.Index);
                Assert.IsFalse(level.Endless);
                Assert.Greater(level.TargetDistance, 0f);
                Assert.Greater(level.MaxSpeed, level.StartSpeed);
                Assert.Greater(level.ObstacleDensity, 0f);
                Assert.Less(level.ObstacleDensity, 1f);

                var previous = LevelLibrary.Get(i - 1);
                if (i > 1)
                {
                    Assert.GreaterOrEqual(level.TargetDistance, previous.TargetDistance,
                        "later levels should not be shorter");
                    Assert.GreaterOrEqual(level.MaxSpeed, previous.MaxSpeed,
                        "later levels should not be slower");
                    Assert.GreaterOrEqual(level.ObstacleDensity, previous.ObstacleDensity,
                        "later levels should not be easier");
                }
            }
        }

        // ----------------------------------------------------------- track planning

        [Test]
        public void Planner_IsDeterministic()
        {
            var level = LevelLibrary.Get(3);
            for (int index = 0; index < 12; index++)
            {
                var a = TrackPlanner.PlanSegment(level, index);
                var b = TrackPlanner.PlanSegment(level, index);
                Assert.AreEqual(a.Length, b.Length, "same seed must plan the same number of steps");

                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].Z, b[i].Z, 1e-4f);
                    Assert.AreEqual(a[i].HasObstacle, b[i].HasObstacle);
                    Assert.AreEqual(a[i].Kind, b[i].Kind);
                    Assert.AreEqual(a[i].CoinLane, b[i].CoinLane);
                    Assert.AreEqual(a[i].PowerUpLane, b[i].PowerUpLane);
                    Assert.AreEqual(a[i].BlockedCount, b[i].BlockedCount);
                }
            }
        }

        [Test]
        public void Planner_WallsAlwaysLeaveAnEscapeLane()
        {
            foreach (var level in LevelLibrary.Levels)
            {
                for (int index = 0; index < 120; index++)
                {
                    foreach (var step in TrackPlanner.PlanSegment(level, index))
                    {
                        if (!step.HasObstacle) continue;
                        if (step.Kind != ObstacleKind.Block) continue;

                        Assert.Less(step.BlockedCount, GameConfig.LaneCount,
                            $"{level.Name} segment {index}: block row at z={step.Z} walls off every lane");

                        Assert.IsTrue(AreDistinct(step.BlockedLanes),
                            $"{level.Name} segment {index}: a lane was blocked twice");
                        foreach (int lane in step.BlockedLanes)
                        {
                            Assert.That(lane, Is.InRange(0, GameConfig.LaneCount - 1));
                        }
                    }
                }
            }
        }

        [Test]
        public void Planner_LeavesEnoughReactionTime()
        {
            var level = LevelLibrary.Get(6);
            for (int index = 0; index < 80; index++)
            {
                var steps = TrackPlanner.PlanSegment(level, index);
                for (int i = 1; i < steps.Length; i++)
                {
                    float gap = steps[i].Z - steps[i - 1].Z;
                    Assert.GreaterOrEqual(gap, TrackManager.MinGap - 1e-3f,
                        "rows must never bunch up closer than the minimum gap");

                    float speed = Difficulty.Speed(level, steps[i - 1].Z);
                    float seconds = gap / Mathf.Max(1f, speed);
                    Assert.GreaterOrEqual(seconds, TrackManager.MinReactionTime - 1e-3f,
                        $"{level.Name} segment {index}: only {seconds:F2}s between rows at {speed:F1} m/s");
                }
            }
        }

        [Test]
        public void Planner_ProducesBothObstaclesAndPickups()
        {
            var level = LevelLibrary.Get(2);
            int obstacles = 0, coins = 0, powerUps = 0;

            for (int index = 1; index < 200; index++)
            {
                foreach (var step in TrackPlanner.PlanSegment(level, index))
                {
                    if (step.HasObstacle) obstacles++;
                    if (step.CoinLane >= 0) coins++;
                    if (step.PowerUpLane >= 0) powerUps++;
                }
            }

            Assert.Greater(obstacles, 20, "the track should not be empty");
            Assert.Greater(coins, 20, "coins should actually spawn");
            Assert.Greater(powerUps, 0, "power-ups should actually spawn");
        }

        [Test]
        public void Planner_KeepsPickupsOutOfBlockedLanes()
        {
            foreach (var level in LevelLibrary.Levels)
            {
                for (int index = 1; index < 40; index++)
                {
                    foreach (var step in TrackPlanner.PlanSegment(level, index))
                    {
                        if (step.CoinLane >= 0)
                        {
                            Assert.IsFalse(Contains(step.BlockedLanes, step.CoinLane),
                                "a coin run must not sit inside an obstacle");
                        }
                        if (step.PowerUpLane >= 0)
                        {
                            Assert.IsFalse(Contains(step.BlockedLanes, step.PowerUpLane),
                                "a power-up must not sit inside an obstacle");
                        }
                    }
                }
            }
        }

        [Test]
        public void Planner_SegmentsDifferFromEachOther()
        {
            var level = LevelLibrary.Get(1);
            var signatures = new HashSet<string>();
            for (int index = 0; index < 40; index++)
            {
                var steps = TrackPlanner.PlanSegment(level, index);
                var signature = new System.Text.StringBuilder();
                foreach (var step in steps)
                {
                    signature.Append(step.HasObstacle ? (int)step.Kind + 1 : 0);
                    signature.Append(':');
                    if (step.BlockedLanes != null)
                    {
                        foreach (int lane in step.BlockedLanes) signature.Append(lane);
                    }
                    signature.Append('|');
                }
                signatures.Add(signature.ToString());
            }

            Assert.Greater(signatures.Count, 12,
                "layouts should vary between segments, not repeat one pattern");
        }

        private static bool AreDistinct(int[] lanes)
        {
            if (lanes == null) return true;
            for (int i = 0; i < lanes.Length; i++)
            {
                for (int j = i + 1; j < lanes.Length; j++)
                {
                    if (lanes[i] == lanes[j]) return false;
                }
            }
            return true;
        }

        private static bool Contains(int[] lanes, int lane)
        {
            if (lanes == null) return false;
            foreach (int value in lanes)
            {
                if (value == lane) return true;
            }
            return false;
        }
    }
}
