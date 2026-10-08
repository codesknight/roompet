using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshRunner
{
    /// <summary>One decision point along the track.</summary>
    [Serializable]
    public struct StepPlan
    {
        public float Z;
        public bool HasObstacle;
        public ObstacleKind Kind;

        /// <summary>Lanes carrying an obstacle. A Block row must never fill every lane.</summary>
        public int[] BlockedLanes;

        /// <summary>Lane carrying a coin run, or -1.</summary>
        public int CoinLane;
        public bool CoinArc;

        /// <summary>Lane carrying a power-up, or -1.</summary>
        public int PowerUpLane;
        public PowerUpKind PowerUp;

        public int BlockedCount => BlockedLanes == null ? 0 : BlockedLanes.Length;
    }

    /// <summary>
    /// Turns a (level, segment index) pair into a concrete layout. Deliberately free of
    /// Unity object creation so the fairness rules can be unit tested: the same seed
    /// always yields the same steps, and a Block row always leaves an escape lane.
    /// </summary>
    public static class TrackPlanner
    {
        public const float StartBuffer = 4f;
        public const float EndBuffer = 4f;
        public const float CoinSpacing = 1.7f;

        public static StepPlan[] PlanSegment(LevelDefinition level, int segmentIndex)
        {
            var steps = new List<StepPlan>();
            float startZ = segmentIndex * GameConfig.SegmentLength;
            float endZ = startZ + GameConfig.SegmentLength;
            var levelForRules = level ?? LevelLibrary.Endless;

            var rng = new System.Random(HashSeed(levelForRules.Seed, segmentIndex));
            float cursor = startZ + StartBuffer;

            while (cursor < endZ - EndBuffer)
            {
                float speed = Difficulty.Speed(levelForRules, cursor);
                float gap = Mathf.Clamp(speed * TrackManager.MinReactionTime, TrackManager.MinGap, TrackManager.MaxGap);

                var step = new StepPlan
                {
                    Z = cursor,
                    CoinLane = -1,
                    PowerUpLane = -1,
                    BlockedLanes = Array.Empty<int>()
                };

                float chance = Difficulty.ObstacleChance(levelForRules, cursor);
                if (rng.NextDouble() < chance)
                {
                    PlanObstacle(levelForRules, cursor, rng, out step.Kind, out step.BlockedLanes);
                    step.HasObstacle = true;
                }

                var free = FreeLanes(step.BlockedLanes);
                if (free.Count > 0)
                {
                    if (rng.NextDouble() < GameConfig.CoinsPerSegmentChance * 0.6f)
                    {
                        step.CoinLane = free[rng.Next(free.Count)];
                        step.CoinArc = step.HasObstacle && step.BlockedCount > 0 && rng.NextDouble() < 0.5;
                    }

                    float powerUpChance = levelForRules.PowerUpChance;
                    if (rng.NextDouble() < powerUpChance * 0.5f)
                    {
                        step.PowerUpLane = free[rng.Next(free.Count)];
                        step.PowerUp = RandomPowerUp(rng);
                    }
                }

                steps.Add(step);
                cursor += gap;
            }

            return steps.ToArray();
        }

        private static void PlanObstacle(LevelDefinition level, float z, System.Random rng,
            out ObstacleKind kind, out int[] lanes)
        {
            float progress = Difficulty.Progress(level, z);

            float totalWeight = 0f;
            var kinds = (ObstacleKind[])Enum.GetValues(typeof(ObstacleKind));
            for (int i = 0; i < kinds.Length; i++) totalWeight += Difficulty.KindWeight(kinds[i], progress);

            ObstacleKind chosen = ObstacleKind.Block;
            if (totalWeight > 0f)
            {
                double roll = rng.NextDouble() * totalWeight;
                for (int i = 0; i < kinds.Length; i++)
                {
                    roll -= Difficulty.KindWeight(kinds[i], progress);
                    if (roll <= 0)
                    {
                        chosen = kinds[i];
                        break;
                    }
                }
            }

            int laneCount = GameConfig.LaneCount;
            int count;
            if (chosen == ObstacleKind.Block)
            {
                // A wall must always leave somewhere to run.
                int maxBlocked = Mathf.Min(Difficulty.MaxBlockedLanes(level, z), laneCount - 1);
                count = 1 + rng.Next(Mathf.Max(1, maxBlocked));
            }
            else
            {
                // Barriers may span the whole width: they are answered with an action.
                count = 1 + rng.Next(laneCount);
                if (progress > 0.35f && rng.NextDouble() < 0.45) count = laneCount;
            }

            count = Mathf.Clamp(count, 1, laneCount);

            var order = new List<int>(laneCount);
            for (int i = 0; i < laneCount; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            kind = chosen;
            lanes = new int[count];
            for (int i = 0; i < count; i++) lanes[i] = order[i];
        }

        public static List<int> FreeLanes(int[] blocked)
        {
            var free = new List<int>();
            for (int lane = 0; lane < GameConfig.LaneCount; lane++)
            {
                bool taken = false;
                if (blocked != null)
                {
                    for (int i = 0; i < blocked.Length; i++)
                    {
                        if (blocked[i] == lane) { taken = true; break; }
                    }
                }
                if (!taken) free.Add(lane);
            }
            return free;
        }

        private static PowerUpKind RandomPowerUp(System.Random rng)
        {
            var values = (PowerUpKind[])Enum.GetValues(typeof(PowerUpKind));
            return values[rng.Next(values.Length)];
        }

        /// <summary>Stable per-segment seed so a level replays identically.</summary>
        public static int HashSeed(int seed, int index)
        {
            unchecked
            {
                int h = seed * 73856093 ^ index * 19349663;
                h ^= h >> 13;
                h *= 1274126177;
                return h ^ (h >> 16);
            }
        }
    }
}
