using System;
using System.Collections.Generic;
using UnityEngine;

namespace DshRunner
{
    /// <summary>Global tuning values. Everything gameplay-facing lives here so the
    /// feel can be adjusted in one place.</summary>
    public static class GameConfig
    {
        public const int LaneCount = 3;
        public const float LaneWidth = 2.4f;
        public const float SegmentLength = 24f;
        public const int SegmentsAhead = 7;
        public const int SegmentsBehind = 2;

        public const float PlayerHalfHeight = 0.5f;
        public const float PlayerSize = 0.9f;

        public const float JumpHeight = 2.5f;
        public const float JumpDuration = 0.62f;
        public const float LaneChangeTime = 0.11f;
        public const float SlideDuration = 0.6f;
        // The slide is a crouch (squash), not a shrink: the whole character used to scale down to
        // 45%, which read as "变小". The squash is driven by PlayerController._squash instead.
        public const float SlideScaleY = 1f;
        public const float Gravity = 46f;

        public const float CoinsPerSegmentChance = 0.75f;
        public const float PowerUpPerSegmentChance = 0.14f;

        /// <summary>Used when no level is supplied, and as the reference speed for
        /// camera framing and animation rates.</summary>
        public const float BaseSpeedFallback = 12f;

        /// <summary>World-space X of a lane index (0 = left).</summary>
        public static float LaneX(int lane)
        {
            int clamped = Mathf.Clamp(lane, 0, LaneCount - 1);
            return (clamped - (LaneCount - 1) * 0.5f) * LaneWidth;
        }
    }

    public enum GameState
    {
        Boot,
        Menu,
        LevelSelect,
        Playing,
        Paused,
        GameOver,
        LevelComplete
    }

    public enum ObstacleKind
    {
        /// <summary>Full-height block: must be dodged by changing lane.</summary>
        Block,
        /// <summary>Low barrier: must be jumped.</summary>
        JumpBarrier,
        /// <summary>Overhead barrier: must be slid under.</summary>
        SlideBarrier
    }

    public enum PowerUpKind
    {
        Shield,
        Magnet,
        DoubleScore,
        SlowMotion
    }

    /// <summary>One playable level. Plain data so it can be built in code and unit tested.</summary>
    [Serializable]
    public class LevelDefinition
    {
        public int Index;
        public string Name = "Level";
        public string Blurb = "";
        public float TargetDistance = 600f;
        public float StartSpeed = 12f;
        public float SpeedRamp = 0.012f;
        public float MaxSpeed = 26f;
        public float ObstacleDensity = 0.30f;
        public float PowerUpChance = GameConfig.PowerUpPerSegmentChance;
        public int Seed = 12345;
        public bool Endless;

        public bool IsEndless => Endless;

        public override string ToString() => $"{Index}:{Name}";
    }

    /// <summary>The shipped level set. Level 0 is endless free-run.</summary>
    public static class LevelLibrary
    {
        public static readonly LevelDefinition[] Levels =
        {
            new LevelDefinition
            {
                Index = 0, Name = "无尽模式", Blurb = "没有终点，看你能跑多远",
                Endless = true, TargetDistance = 0f,
                StartSpeed = 11f, SpeedRamp = 0.011f, MaxSpeed = 34f,
                ObstacleDensity = 0.28f, PowerUpChance = 0.14f, Seed = 20261008
            },
            new LevelDefinition
            {
                Index = 1, Name = "第 1 关 · 起步", Blurb = "熟悉换道与跳跃",
                TargetDistance = 500f, StartSpeed = 10f, SpeedRamp = 0.008f, MaxSpeed = 16f,
                ObstacleDensity = 0.20f, PowerUpChance = 0.20f, Seed = 1001
            },
            new LevelDefinition
            {
                Index = 2, Name = "第 2 关 · 加速", Blurb = "障碍开始成对出现",
                TargetDistance = 700f, StartSpeed = 12f, SpeedRamp = 0.010f, MaxSpeed = 19f,
                ObstacleDensity = 0.28f, PowerUpChance = 0.18f, Seed = 1002
            },
            new LevelDefinition
            {
                Index = 3, Name = "第 3 关 · 低栏", Blurb = "学会在跑动中滑铲",
                TargetDistance = 850f, StartSpeed = 13f, SpeedRamp = 0.011f, MaxSpeed = 22f,
                ObstacleDensity = 0.34f, PowerUpChance = 0.17f, Seed = 1003
            },
            new LevelDefinition
            {
                Index = 4, Name = "第 4 关 · 窄缝", Blurb = "只留一条活路",
                TargetDistance = 1000f, StartSpeed = 15f, SpeedRamp = 0.012f, MaxSpeed = 25f,
                ObstacleDensity = 0.42f, PowerUpChance = 0.16f, Seed = 1004
            },
            new LevelDefinition
            {
                Index = 5, Name = "第 5 关 · 组合", Blurb = "连跳与变向混合",
                TargetDistance = 1200f, StartSpeed = 16f, SpeedRamp = 0.013f, MaxSpeed = 28f,
                ObstacleDensity = 0.50f, PowerUpChance = 0.15f, Seed = 1005
            },
            new LevelDefinition
            {
                Index = 6, Name = "第 6 关 · 疾走", Blurb = "反应时间越来越短",
                TargetDistance = 1400f, StartSpeed = 18f, SpeedRamp = 0.014f, MaxSpeed = 31f,
                ObstacleDensity = 0.58f, PowerUpChance = 0.14f, Seed = 1006
            },
            new LevelDefinition
            {
                Index = 7, Name = "第 7 关 · 极限", Blurb = "给熟练玩家准备的收尾",
                TargetDistance = 1800f, StartSpeed = 20f, SpeedRamp = 0.015f, MaxSpeed = 36f,
                ObstacleDensity = 0.68f, PowerUpChance = 0.13f, Seed = 1007
            }
        };

        public static int Count => Levels.Length;

        public static LevelDefinition Get(int index)
        {
            if (index < 0) index = 0;
            if (index >= Levels.Length) index = Levels.Length - 1;
            return Levels[index];
        }

        public static LevelDefinition Endless => Levels[0];
    }

    /// <summary>
    /// Distance-driven difficulty. Pure functions so the curve can be unit tested
    /// without booting a scene.
    /// </summary>
    public static class Difficulty
    {
        public static float Speed(LevelDefinition level, float distance)
        {
            if (level == null) return GameConfig.BaseSpeedFallback;
            float raw = level.StartSpeed + Mathf.Max(0f, distance) * level.SpeedRamp;
            return Mathf.Min(level.MaxSpeed, raw);
        }

        /// <summary>0 at the start of a level, 1 at its target distance (or a rolling
        /// 2 km window in endless mode).</summary>
        public static float Progress(LevelDefinition level, float distance)
        {
            if (level == null || level.Endless) return Mathf.Clamp01(distance / 2000f);
            float target = Mathf.Max(1f, level.TargetDistance);
            return Mathf.Clamp01(distance / target);
        }

        /// <summary>Chance that a slot in a segment carries an obstacle, 0..1.</summary>
        public static float ObstacleChance(LevelDefinition level, float distance)
        {
            float baseDensity = level == null ? 0.3f : level.ObstacleDensity;
            float ramp = 0.55f + 0.85f * Progress(level, distance);
            return Mathf.Clamp(baseDensity * ramp, 0f, 0.92f);
        }

        /// <summary>How many lanes may be blocked at once. Never all three.</summary>
        public static int MaxBlockedLanes(LevelDefinition level, float distance)
        {
            float p = Progress(level, distance);
            if (p < 0.25f) return 1;
            if (p < 0.60f) return 2;
            return 2; // three lanes blocked would be unwinnable
        }

        /// <summary>Relative weights for the obstacle mix at a given point.</summary>
        public static float KindWeight(ObstacleKind kind, float progress)
        {
            switch (kind)
            {
                case ObstacleKind.Block:
                    return 1.0f;
                case ObstacleKind.JumpBarrier:
                    return 0.25f + 0.65f * progress;
                case ObstacleKind.SlideBarrier:
                    return progress < 0.2f ? 0f : 0.15f + 0.60f * progress;
                default:
                    return 0f;
            }
        }
    }

    /// <summary>Scoring rules. Kept pure so they can be unit tested.</summary>
    public static class ScoreRules
    {
        /// <summary>Distance that earns one multiplier tier.</summary>
        public const float TierLength = 150f;

        /// <summary>Extra multiplier per tier. Further in == more points per metre.</summary>
        public const float TierStep = 0.3f;

        public const int CoinValue = 10;

        public static int Tier(float distance)
        {
            if (distance < 0f) distance = 0f;
            return Mathf.FloorToInt(distance / TierLength);
        }

        /// <summary>Score per metre at this distance.</summary>
        public static float DistanceMultiplier(float distance) => 1f + Tier(distance) * TierStep;
    }

    /// <summary>
    /// Accumulates score from distance and pickups. Deliberately a plain class with
    /// no Unity dependencies beyond Mathf so the rules stay testable.
    /// </summary>
    public class ScoreKeeper
    {
        public float Distance { get; private set; }
        public int Coins { get; private set; }
        public float RawScore { get; private set; }

        /// <summary>Set by power-ups (2x score, for example).</summary>
        public float ExternalMultiplier = 1f;

        public int Total => Mathf.FloorToInt(RawScore);

        /// <summary>Current points-per-metre, including power-up multipliers.</summary>
        public float CurrentMultiplier => ScoreRules.DistanceMultiplier(Distance) * Mathf.Max(0f, ExternalMultiplier);

        public void Advance(float deltaDistance)
        {
            if (deltaDistance <= 0f) return;
            // Integrate with the multiplier at the midpoint so a tier boundary inside
            // one frame does not lose or double-count points.
            float mid = Distance + deltaDistance * 0.5f;
            Distance += deltaDistance;
            RawScore += deltaDistance * ScoreRules.DistanceMultiplier(mid) * Mathf.Max(0f, ExternalMultiplier);
        }

        public void AddCoin(int count = 1)
        {
            if (count <= 0) return;
            Coins += count;
            RawScore += count * ScoreRules.CoinValue * Mathf.Max(0f, ExternalMultiplier);
        }

        public void Reset()
        {
            Distance = 0f;
            Coins = 0;
            RawScore = 0f;
            ExternalMultiplier = 1f;
        }

        public ScoreSnapshot Snapshot() => new ScoreSnapshot
        {
            Distance = Distance,
            Coins = Coins,
            Score = Total,
            Multiplier = CurrentMultiplier
        };
    }

    [Serializable]
    public struct ScoreSnapshot
    {
        public float Distance;
        public int Coins;
        public int Score;
        public float Multiplier;
    }

    /// <summary>Persisted progress, backed by PlayerPrefs.</summary>
    public static class ProgressStore
    {
        private const string BestScoreKey = "dshrunner.best.score";
        private const string BestDistanceKey = "dshrunner.best.distance";
        private const string CoinsKey = "dshrunner.coins.total";
        private const string UnlockedKey = "dshrunner.levels.unlocked";
        private const string LevelBestPrefix = "dshrunner.level.best.";

        public static int BestScore
        {
            get => PlayerPrefs.GetInt(BestScoreKey, 0);
            set { PlayerPrefs.SetInt(BestScoreKey, Mathf.Max(BestScore, value)); PlayerPrefs.Save(); }
        }

        public static float BestDistance
        {
            get => PlayerPrefs.GetFloat(BestDistanceKey, 0f);
            set { PlayerPrefs.SetFloat(BestDistanceKey, Mathf.Max(BestDistance, value)); PlayerPrefs.Save(); }
        }

        public static int TotalCoins
        {
            get => PlayerPrefs.GetInt(CoinsKey, 0);
            set { PlayerPrefs.SetInt(CoinsKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>Highest level index the player may enter. Level 1 is always open.</summary>
        public static int UnlockedLevels
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, LevelLibrary.Count - 1);
            set { PlayerPrefs.SetInt(UnlockedKey, Mathf.Clamp(value, 1, LevelLibrary.Count - 1)); PlayerPrefs.Save(); }
        }

        public static int LevelBest(int levelIndex) => PlayerPrefs.GetInt(LevelBestPrefix + levelIndex, 0);

        public static void RecordLevel(int levelIndex, int score)
        {
            if (score > LevelBest(levelIndex))
            {
                PlayerPrefs.SetInt(LevelBestPrefix + levelIndex, score);
                PlayerPrefs.Save();
            }
        }

        public static void AddCoins(int coins)
        {
            TotalCoins = TotalCoins + Mathf.Max(0, coins);
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(BestScoreKey);
            PlayerPrefs.DeleteKey(BestDistanceKey);
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(UnlockedKey);
            for (int i = 0; i < LevelLibrary.Count; i++) PlayerPrefs.DeleteKey(LevelBestPrefix + i);
            PlayerPrefs.Save();
        }
    }
}
