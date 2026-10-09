using UnityEngine;

namespace DshMiniGames
{
    /// <summary>How 切水果 is being played.</summary>
    public enum SliceMode { Endless = 0, Levels = 1 }

    /// <summary>The numbers the slicing game is made of.</summary>
    public struct SliceSettings
    {
        /// <summary>Downward acceleration, in world units per second squared.</summary>
        public float Gravity;

        /// <summary>Half-width of the play area, in world units.</summary>
        public float HalfWidth;

        /// <summary>Where fruit is launched from, and the highest it may reach.</summary>
        public float BottomY;
        public float TopY;

        /// <summary>Radius the slicing hit test uses, in world units.</summary>
        public float FruitRadius;

        /// <summary>
        /// How long every fruit is in the air, at minimum.
        ///
        /// This is the game's fairness rule, and the reason launch speeds are computed rather than
        /// picked: a fruit that is only on screen for half a second is a miss the player cannot
        /// avoid, and a mode with levels in it cannot be "completable" if its own schedule hands out
        /// impossible fruit.
        /// </summary>
        public float MinAirSeconds;

        /// <summary>How far from the swipe line a fruit still counts as sliced.</summary>
        public float BladeHalfWidth;

        /// <summary>Most fruit allowed in the air at once, so one swipe can always clear them.</summary>
        public int MaxSimultaneous;

        public static SliceSettings Default => new SliceSettings
        {
            Gravity = 14f,
            HalfWidth = 5.4f,
            BottomY = -8.6f,
            TopY = 8.2f,
            FruitRadius = 0.46f,
            MinAirSeconds = 1.55f,
            BladeHalfWidth = 0.30f,
            MaxSimultaneous = 5
        };
    }

    /// <summary>One level of 闯关模式.</summary>
    public struct SliceLevelPlan
    {
        public int Level;

        /// <summary>How many waves of fruit the level throws.</summary>
        public int Waves;

        /// <summary>Fruit per wave.</summary>
        public int FruitsPerWave;

        /// <summary>Score needed to pass.</summary>
        public int TargetScore;

        /// <summary>How many fruit may be dropped before the level fails.</summary>
        public int MaxMisses;

        /// <summary>Chance that a slot in a wave is a bomb instead of fruit.</summary>
        public float BombChance;

        /// <summary>What the level is called.</summary>
        public string Name => "第 " + Level + " 关";
    }

    /// <summary>
    /// 切水果, as arithmetic.
    ///
    /// The interesting parts of a slicing game are all numbers: how long a fruit is in the air (can
    /// the player possibly cut it?), how far apart fruit may be (can one swipe reach them?), how
    /// many may overlap (is the wave clearable at all?), and whether a level's target is reachable
    /// from the fruit it actually throws. All of that is a comparison, so all of it is tested here
    /// instead of being felt out on a phone.
    /// </summary>
    public static class SliceRules
    {
        /// <summary>How many levels 闯关模式 has.</summary>
        public const int LevelCount = 8;

        // ------------------------------------------------------------------ launch

        /// <summary>Slowest launch that still keeps a fruit in the air long enough to be sliced.</summary>
        public static float MinLaunchSpeed(SliceSettings settings)
            => settings.MinAirSeconds * settings.Gravity * 0.5f;

        /// <summary>Fastest launch that still peaks inside the play area.</summary>
        public static float MaxLaunchSpeed(SliceSettings settings)
        {
            float climb = Mathf.Max(0.5f, settings.TopY - settings.BottomY);
            return Mathf.Sqrt(2f * settings.Gravity * climb);
        }

        /// <summary>
        /// The launch speed for a roll in 0..1, always inside the sliceable window.
        ///
        /// The window exists because both ends are wrong: too slow and the fruit is gone before a
        /// thumb can reach it, too fast and it leaves the top of the screen — which is not "hard",
        /// it is unsliceable.
        /// </summary>
        public static float LaunchSpeedFor(float roll, SliceSettings settings)
            => Mathf.Lerp(MinLaunchSpeed(settings), MaxLaunchSpeed(settings), Mathf.Clamp01(roll));

        /// <summary>How long a fruit launched at this speed stays in the air.</summary>
        public static float AirSeconds(float launchSpeed, SliceSettings settings)
            => 2f * Mathf.Max(0f, launchSpeed) / Mathf.Max(0.01f, settings.Gravity);

        /// <summary>
        /// Where a fruit is launched from and how fast, given two rolls.
        ///
        /// The horizontal velocity is chosen so the fruit *lands* somewhere on screen, which is what
        /// keeps the whole arc inside the play area: with both ends of the flight inside the width,
        /// nothing in between can leave it.
        /// </summary>
        public static void Launch(float speedRoll, float acrossRoll, SliceSettings settings,
            out Vector2 position, out Vector2 velocity)
        {
            float speed = LaunchSpeedFor(speedRoll, settings);
            float air = AirSeconds(speed, settings);

            float limit = settings.HalfWidth * 0.86f;
            float fromX = Mathf.Lerp(-limit, limit, Mathf.Clamp01(acrossRoll));
            float toX = Mathf.Lerp(-limit, limit, Mathf.Clamp01(Mathf.Repeat(acrossRoll * 1.618f + 0.37f, 1f)));

            position = new Vector2(fromX, settings.BottomY);
            velocity = new Vector2((toX - fromX) / Mathf.Max(0.05f, air), speed);
        }

        /// <summary>Where a fruit is after a given time in the air.</summary>
        public static Vector2 PositionAt(Vector2 position, Vector2 velocity, float seconds,
            SliceSettings settings)
            => new Vector2(position.x + velocity.x * seconds,
                position.y + velocity.y * seconds - 0.5f * settings.Gravity * seconds * seconds);

        // ------------------------------------------------------------------ slicing

        /// <summary>Distance from a point to a line segment. The heart of "did the blade touch it".</summary>
        public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 0.000001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>
        /// Whether a swipe from <paramref name="from"/> to <paramref name="to"/> cuts a fruit.
        ///
        /// A segment, not a point: the blade moves several centimetres between two frames, and a
        /// hit test that only looks at the current position lets fruit slip between frames — which
        /// reads as "my swipe went straight through it".
        /// </summary>
        public static bool Slices(Vector2 from, Vector2 to, Vector2 fruit, float fruitRadius,
            SliceSettings settings)
            => DistanceToSegment(fruit, from, to) <= fruitRadius + Mathf.Max(0f, settings.BladeHalfWidth);

        /// <summary>
        /// Whether a wave is trackable rather than a blur.
        ///
        /// The fairness rule for a *wave* is not "can one swipe reach them all" — the blade sweeps
        /// wherever the finger goes, and a player is allowed two swipes. It is that no fruit crosses
        /// the screen faster than a swipe can follow: a fruit whose horizontal speed takes it from one
        /// edge to the other inside its own flight is one the player has to predict rather than see.
        /// </summary>
        public static bool WaveIsTrackable(Vector2[] fruits, Vector2[] velocities, SliceSettings settings)
        {
            if (fruits == null || velocities == null) return true;
            if (fruits.Length != velocities.Length) return false;

            float fastest = settings.HalfWidth * 2f / Mathf.Max(0.4f, settings.MinAirSeconds);

            for (int i = 0; i < velocities.Length; i++)
            {
                if (Mathf.Abs(velocities[i].x) > fastest + 0.001f) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>Points for slicing one fruit.</summary>
        public static int ScoreFor(FruitKind kind) => 1;

        /// <summary>Bonus for taking several fruit in one swipe: 2 fruit = 2, 3 = 4, and so on.</summary>
        public static int ComboBonus(int slicedInOneSwipe)
        {
            int n = Mathf.Max(0, slicedInOneSwipe);
            if (n < 2) return 0;
            return n * (n - 1);      // 2 -> 2, 3 -> 6, 4 -> 12, 5 -> 20
        }

        /// <summary>Coins a run is worth. One per two points, with a small bonus for a long run.</summary>
        public static int CoinsFor(int score)
        {
            int points = Mathf.Max(0, score);
            return points / 2 + points / 20;
        }

        /// <summary>What the result panel says.</summary>
        public static string RankFor(int score)
        {
            if (score >= 120) return "刀光如雨";
            if (score >= 80) return "水果忍者";
            if (score >= 50) return "手起刀落";
            if (score >= 25) return "切得挺顺";
            if (score >= 10) return "找到感觉了";
            return "再来一次吧";
        }

        // ------------------------------------------------------------------ endless mode

        /// <summary>Seconds between waves after this many points. Rises in difficulty, never to zero.</summary>
        public static float WaveSecondsFor(int score, SliceSettings settings)
        {
            float seconds = 1.55f - Mathf.Max(0, score) * 0.012f;
            return Mathf.Max(0.62f, seconds);
        }

        /// <summary>How many fruit a wave throws after this many points.</summary>
        public static int WaveSizeFor(int score, SliceSettings settings)
        {
            int size = 1 + Mathf.Max(0, score) / 12;
            return Mathf.Clamp(size, 1, settings.MaxSimultaneous);
        }

        /// <summary>Chance that a slot in a wave is a bomb, after this many points.</summary>
        public static float BombChanceFor(int score, SliceSettings settings)
        {
            float chance = Mathf.Clamp01((Mathf.Max(0, score) - 6f) * 0.010f);
            return Mathf.Min(chance, 0.22f);
        }

        /// <summary>Lives a run starts with, in both modes.</summary>
        public const int StartLives = 3;

        // ------------------------------------------------------------------ levels

        /// <summary>
        /// The plan for one level of 闯关模式.
        ///
        /// The target is a *share* of the points the level can produce — 62% at first, 82% by the
        /// last level — rather than a number picked by feel. That is what makes "every level can be
        /// passed" a property of the design instead of a hope: the fruit the level throws is always
        /// worth noticeably more than it asks for, and combos put the last levels comfortably inside
        /// reach for a player who chains two or three at a time.
        ///
        /// Levels 1-3 have no bombs at all: a mode with a score target and a fail-on-bomb rule has
        /// to teach the rule before it punishes it.
        /// </summary>
        public static SliceLevelPlan LevelPlan(int level, SliceSettings settings)
        {
            int index = Mathf.Clamp(level, 1, LevelCount);
            int waves = 3 + index / 2;                      // 3,4,4,5,5,6,6,7
            int perWave = Mathf.Clamp(1 + (index - 1) / 2, 1, settings.MaxSimultaneous);
            int perFruit = ScoreFor(FruitKind.Apple);

            int fruitCount = waves * perWave;
            float share = 0.62f + index * 0.025f;           // 0.65 … 0.82

            return new SliceLevelPlan
            {
                Level = index,
                Waves = waves,
                FruitsPerWave = perWave,
                TargetScore = Mathf.Max(1, Mathf.CeilToInt(fruitCount * perFruit * share)),
                MaxMisses = index <= 2 ? 4 : 3,
                BombChance = index <= 3 ? 0f : Mathf.Min(0.10f + (index - 3) * 0.03f, 0.22f)
            };
        }

        /// <summary>
        /// Seconds between waves inside a level.
        ///
        /// Out here and not in the game, because the level's "can this be passed" check has to be able
        /// to replay the level's own schedule — a test that invents a different cadence is testing a
        /// different level.
        /// </summary>
        public static float LevelWaveSeconds(int wave)
            => Mathf.Max(1.1f, 1.7f - Mathf.Max(0, wave) * 0.06f);


        /// <summary>Most points a level's own fruit can produce, ignoring combos.</summary>
        public static int MaxScore(SliceLevelPlan plan)
            => Mathf.Max(0, plan.Waves) * Mathf.Max(0, plan.FruitsPerWave) * ScoreFor(FruitKind.Apple);

        /// <summary>
        /// Whether a level can be passed by playing it well.
        ///
        /// The generator's promise, asserted rather than assumed: the fruit a level throws must be
        /// worth clearly more than its target, so the player has a mistake's worth of room. Levels
        /// that fail this are not hard, they are broken.
        /// </summary>
        public static bool IsBeatable(SliceLevelPlan plan)
            => MaxScore(plan) >= Mathf.CeilToInt(plan.TargetScore * 1.15f);

        /// <summary>A word about the level's difficulty, for the panel.</summary>
        public static string DifficultyFor(SliceLevelPlan plan)
        {
            if (plan.BombChance <= 0f) return "还没有炸弹，先把准头练好";
            if (plan.BombChance < 0.14f) return "开始有炸弹了，别切到";
            if (plan.BombChance < 0.2f) return "炸弹不少，看清楚再下刀";
            return "满屏都是炸弹，手要稳";
        }
    }
}
