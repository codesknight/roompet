using DshMiniGames;
using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// 切水果's arithmetic.
    ///
    /// The two things that decide whether a slicing game is playable are both numbers: how long a
    /// fruit is in the air (can the player reach it?) and whether a level's target is reachable from
    /// the fruit that level actually throws. Both are asserted here rather than discovered on a phone.
    /// </summary>
    public class SliceRulesTests
    {
        private static readonly SliceSettings Settings = SliceSettings.Default;

        [Test]
        public void Launch_EveryFruitIsInTheAirLongEnoughToCut()
        {
            // The fairness rule. A fruit whose whole flight is a third of a second is a miss the
            // player could not have avoided, and no amount of skill makes it fair.
            Assert.Greater(SliceRules.MinLaunchSpeed(Settings), 0f);

            for (int i = 0; i <= 40; i++)
            {
                float roll = i / 40f;
                Vector2 position, velocity;
                SliceRules.Launch(roll, (roll * 1.7f) % 1f, Settings, out position, out velocity);

                float air = SliceRules.AirSeconds(velocity.y, Settings);
                Assert.GreaterOrEqual(air, Settings.MinAirSeconds - 0.001f,
                    $"roll {roll:F2} throws a fruit that is only in the air for {air:F2}s");
            }
        }

        [Test]
        public void Launch_EveryFruitStaysInsideThePlayArea()
        {
            // Both ends of the flight are inside the width, so nothing in between can leave it, and
            // the peak is under the ceiling — a fruit above the top of the screen is unsliceable, not
            // difficult.
            for (int i = 0; i <= 40; i++)
            {
                float roll = i / 40f;
                Vector2 position, velocity;
                SliceRules.Launch(roll, (roll * 2.3f) % 1f, Settings, out position, out velocity);

                float air = SliceRules.AirSeconds(velocity.y, Settings);
                float peak = SliceRules.PositionAt(position, velocity, air * 0.5f, Settings).y;

                Assert.LessOrEqual(peak, Settings.TopY + 0.001f, $"roll {roll:F2} peaks above the top");
                Assert.Greater(peak, Settings.BottomY + 1f, $"roll {roll:F2} barely leaves the floor");

                for (int step = 0; step <= 20; step++)
                {
                    var at = SliceRules.PositionAt(position, velocity, air * step / 20f, Settings);
                    Assert.LessOrEqual(Mathf.Abs(at.x), Settings.HalfWidth + 0.001f,
                        $"roll {roll:F2} spends part of its flight off the sides");
                }
            }
        }

        [Test]
        public void Slicing_IsASegmentNotAPoint()
        {
            // The bug this prevents: the blade moves several centimetres between frames, and a hit
            // test that only looks at the current position lets fruit slip through — which the player
            // reports as "my swipe went straight through it".
            var fruit = new Vector2(0f, 0f);

            Assert.IsTrue(SliceRules.Slices(new Vector2(-3f, 0f), new Vector2(3f, 0f), fruit,
                0.4f, Settings), "a swipe straight through the middle has to cut it");

            // The graze band is the fruit's radius plus the blade's own width: 0.4 + 0.30 here.
            Assert.IsTrue(SliceRules.Slices(new Vector2(-3f, 0.6f), new Vector2(3f, 0.6f), fruit,
                0.4f, Settings), "a graze inside the blade's reach counts");
            Assert.IsFalse(SliceRules.Slices(new Vector2(-3f, 0.85f), new Vector2(3f, 0.85f), fruit,
                0.4f, Settings), "just outside it does not");

            Assert.IsFalse(SliceRules.Slices(new Vector2(-3f, 3f), new Vector2(3f, 3f), fruit,
                0.4f, Settings), "a swipe well clear of it must not cut it");

            // A swipe that ends exactly on the fruit cuts it, and so does one that starts there.
            Assert.IsTrue(SliceRules.Slices(new Vector2(-2f, 0f), fruit, fruit, 0.4f, Settings));
            Assert.IsTrue(SliceRules.Slices(fruit, new Vector2(2f, 0f), fruit, 0.4f, Settings));

            // A zero-length swipe (a stationary finger) cuts only what it is actually touching.
            Assert.IsTrue(SliceRules.Slices(fruit, fruit, fruit, 0.4f, Settings));
            Assert.IsFalse(SliceRules.Slices(new Vector2(2f, 2f), new Vector2(2f, 2f), fruit, 0.4f, Settings));
        }

        [Test]
        public void Distance_MatchesTheGeometryItClaims()
        {
            // The perpendicular distance to a segment, checked against the cases a person can do in
            // their head — because everything else in this file is built on it.
            Assert.AreEqual(0f, SliceRules.DistanceToSegment(Vector2.zero,
                new Vector2(-1f, 0f), new Vector2(1f, 0f)), 0.0001f);

            Assert.AreEqual(1f, SliceRules.DistanceToSegment(new Vector2(0f, 1f),
                new Vector2(-1f, 0f), new Vector2(1f, 0f)), 0.0001f);

            // Beyond the end: the distance is to the endpoint, not to the infinite line.
            Assert.AreEqual(2f, SliceRules.DistanceToSegment(new Vector2(3f, 0f),
                new Vector2(-1f, 0f), new Vector2(1f, 0f)), 0.0001f);
        }

        [Test]
        public void Combos_PayMoreThanOneAtATime()
        {
            Assert.AreEqual(0, SliceRules.ComboBonus(0));
            Assert.AreEqual(0, SliceRules.ComboBonus(1), "one fruit is not a combo");

            // Strictly increasing: a three-fruit swipe has to be worth more than two separate cuts,
            // or the whole mechanic is decoration.
            int previous = 0;
            for (int n = 2; n <= 8; n++)
            {
                int bonus = SliceRules.ComboBonus(n);
                Assert.Greater(bonus, previous, $"{n} in one swipe must beat {n - 1}");
                Assert.Greater(bonus, SliceRules.ScoreFor(FruitKind.Apple) * (n - 1),
                    $"{n} in one swipe must beat slicing them one by one");
                previous = bonus;
            }
        }

        [Test]
        public void Endless_KeepsRampingWithoutBecomingUnslicable()
        {
            float previousSeconds = float.MaxValue;
            int previousSize = 0;

            for (int score = 0; score <= 200; score += 5)
            {
                float seconds = SliceRules.WaveSecondsFor(score, Settings);
                int size = SliceRules.WaveSizeFor(score, Settings);
                float bombs = SliceRules.BombChanceFor(score, Settings);

                Assert.GreaterOrEqual(seconds, 0.6f, $"a wave every {seconds:F2}s at {score} is not playable");
                Assert.LessOrEqual(seconds, previousSeconds + 0.0001f, "waves cannot slow down");
                Assert.LessOrEqual(size, Settings.MaxSimultaneous,
                    "never more fruit than one swipe can be expected to clear");
                Assert.GreaterOrEqual(size, previousSize, "waves cannot shrink");
                Assert.LessOrEqual(bombs, 0.3f, $"a third of the screen as bombs at {score} is a lottery");

                previousSeconds = seconds;
                previousSize = size;
            }

            Assert.AreEqual(1, SliceRules.WaveSizeFor(0, Settings), "the first wave is one fruit");
            Assert.AreEqual(0f, SliceRules.BombChanceFor(0, Settings), 0.0001f,
                "the first fruit a player ever sees must not be a bomb");
        }

        [Test]
        public void Levels_EveryLevelCanBePassed()
        {
            // The generator's promise for 闯关模式: the fruit a level throws is worth clearly more than
            // its target, so there is a mistake's worth of room. A level that fails this is not hard,
            // it is broken.
            for (int level = 1; level <= SliceRules.LevelCount; level++)
            {
                var plan = SliceRules.LevelPlan(level, Settings);

                Assert.Greater(plan.Waves, 0, $"level {level} throws no waves");
                Assert.Greater(plan.FruitsPerWave, 0);
                Assert.LessOrEqual(plan.FruitsPerWave, Settings.MaxSimultaneous);
                Assert.Greater(plan.TargetScore, 0, $"level {level} asks for nothing");
                Assert.Greater(plan.MaxMisses, 0, $"level {level} fails on the first mistake");
                Assert.IsFalse(string.IsNullOrEmpty(plan.Name));

                Assert.IsTrue(SliceRules.IsBeatable(plan),
                    $"level {level} offers {SliceRules.MaxScore(plan)} points for a target of {plan.TargetScore}");
            }
        }

        [Test]
        public void Levels_GetHarderInTheWaysThatMatter()
        {
            var first = SliceRules.LevelPlan(1, Settings);
            var last = SliceRules.LevelPlan(SliceRules.LevelCount, Settings);

            Assert.Greater(last.TargetScore, first.TargetScore, "the target has to climb");
            Assert.GreaterOrEqual(last.Waves, first.Waves);
            Assert.GreaterOrEqual(last.FruitsPerWave, first.FruitsPerWave);

            // …and the first levels must be bomb-free: a mode that fails you for touching a bomb has
            // to teach the rule before it punishes it.
            for (int level = 1; level <= 3; level++)
            {
                Assert.AreEqual(0f, SliceRules.LevelPlan(level, Settings).BombChance, 0.0001f,
                    $"level {level} has bombs before the rule is taught");
            }

            Assert.Greater(SliceRules.LevelPlan(SliceRules.LevelCount, Settings).BombChance, 0f,
                "the last levels should have some bombs");
        }

        [Test]
        public void Levels_AWaveIsNeverFasterThanASwipe()
        {
            // The fairness rule for a wave: no fruit crosses the screen faster than a swipe can
            // follow. A fruit that goes edge to edge inside its own flight is one the player has to
            // predict rather than see, and in a mode with a score target that is a level that cannot
            // be passed reliably.
            for (int level = 1; level <= SliceRules.LevelCount; level++)
            {
                var plan = SliceRules.LevelPlan(level, Settings);

                for (int seed = 1; seed <= 12; seed++)
                {
                    var rng = new System.Random(seed * 7919 + level);
                    var fruits = new Vector2[plan.FruitsPerWave];
                    var velocities = new Vector2[plan.FruitsPerWave];

                    for (int i = 0; i < fruits.Length; i++)
                    {
                        Vector2 position, velocity;
                        SliceRules.Launch((float)rng.NextDouble(), (float)rng.NextDouble(), Settings,
                            out position, out velocity);
                        fruits[i] = position;
                        velocities[i] = velocity;

                        // Each fruit on its own also has to be reachable.
                        Assert.GreaterOrEqual(SliceRules.AirSeconds(velocity.y, Settings),
                            Settings.MinAirSeconds - 0.001f);
                    }

                    Assert.IsTrue(SliceRules.WaveIsTrackable(fruits, velocities, Settings),
                        $"level {level}, seed {seed}: a fruit crosses the screen too fast to swipe");
                }
            }
        }

        [Test]
        public void Levels_APerfectPlayerClearsEveryLevel()
        {
            // The end-to-end version of "every level can be passed": replay the level's own schedule
            // with a machine that slices everything it can reach, and require both the target and a
            // clean sheet. The cadence comes from the rules, so this is the level the player gets —
            // not a re-invention of it.
            for (int level = 1; level <= SliceRules.LevelCount; level++)
            {
                var plan = SliceRules.LevelPlan(level, Settings);
                var rng = new System.Random(level * 7919 + 13);

                var live = new System.Collections.Generic.List<SliceProbe>();
                float dt = 1f / 60f;
                float nextWave = 0.9f;
                int score = 0, misses = 0, wave = 0;

                // The player's own clock: a swipe takes about a tenth of a second to arrive.
                float swipeCooldown = 0f;

                // Combos are counted the way the game counts them — a chain inside 0.45s pays the
                // running bonus — because "can this level be passed" has to be answered with the
                // scoring the player actually gets, not a simplified one.
                int combo = 0;
                float comboUntil = 0f;

                for (float t = 0f; t < 200f; t += dt)
                {
                    swipeCooldown = Mathf.Max(0f, swipeCooldown - dt);
                    nextWave -= dt;

                    if (nextWave <= 0f && wave < plan.Waves)
                    {
                        for (int i = 0; i < plan.FruitsPerWave; i++)
                        {
                            Vector2 position, velocity;
                            SliceRules.Launch((float)rng.NextDouble(), (float)rng.NextDouble(), Settings,
                                out position, out velocity);

                            // Bombs are in the way from level 4 on, and the perfect player simply
                            // ignores them — they are the reason the target cannot be 100%.
                            bool bomb = rng.NextDouble() < plan.BombChance;
                            live.Add(new SliceProbe { Position = position, Velocity = velocity, Bomb = bomb });
                        }

                        wave++;
                        nextWave = SliceRules.LevelWaveSeconds(wave);
                    }

                    if (combo > 0 && t > comboUntil) combo = 0;

                    for (int i = live.Count - 1; i >= 0; i--)
                    {
                        var piece = live[i];
                        piece.Velocity.y -= Settings.Gravity * dt;
                        piece.Position += piece.Velocity * dt;

                        if (!piece.Bomb && swipeCooldown <= 0f && piece.Position.y > Settings.BottomY + 1f &&
                            piece.Position.y < Settings.TopY)
                        {
                            // A swipe that reaches it: the blade is a line, so an approach that comes
                            // within its reach is a cut.
                            bool reached = SliceRules.Slices(
                                piece.Position + new Vector2(-1f, 0f),
                                piece.Position + new Vector2(1f, 0f),
                                piece.Position, Settings.FruitRadius, Settings);
                            if (reached)
                            {
                                combo++;
                                comboUntil = t + 0.45f;
                                score += SliceRules.ScoreFor(piece.Kind);
                                if (combo >= 2) score += SliceRules.ComboBonus(combo);
                                swipeCooldown = 0.1f;
                                live.RemoveAt(i);
                                continue;
                            }
                        }

                        if (piece.Position.y < Settings.BottomY - 1.5f)
                        {
                            if (!piece.Bomb) misses++;
                            live.RemoveAt(i);
                        }
                    }
                }

                Assert.AreEqual(0, misses, $"level {level}: a perfect player dropped {misses} fruit");
                Assert.GreaterOrEqual(score, plan.TargetScore,
                    $"level {level}: a perfect player scored {score} against a target of {plan.TargetScore}");
            }
        }

        private class SliceProbe
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public FruitKind Kind;
            public bool Bomb;
        }

        [Test]
        public void Coins_RewardTheRunAndSaySomethingAboutIt()
        {
            Assert.AreEqual(0, SliceRules.CoinsFor(0));
            Assert.Greater(SliceRules.CoinsFor(40), SliceRules.CoinsFor(20));
            Assert.AreEqual(0, SliceRules.CoinsFor(-10));

            int previous = -1;
            for (int score = 0; score < 150; score++)
            {
                int coins = SliceRules.CoinsFor(score);
                Assert.GreaterOrEqual(coins, previous, "a better run cannot pay less");
                previous = coins;
            }

            for (int score = 0; score < 200; score += 7)
            {
                Assert.IsFalse(string.IsNullOrEmpty(SliceRules.RankFor(score)), $"no rank at {score}");
            }

            Assert.AreNotEqual(SliceRules.RankFor(0), SliceRules.RankFor(120));
            Assert.Greater(SliceRules.StartLives, 1, "one mistake ending a run is not a game");
        }

        [Test]
        public void Fruit_EveryKindIsBuildableAndNamed()
        {
            // The shapes are shared with 接果子 now, so this checks the shared table rather than a
            // private copy: a kind without a name or a colour is a blank object on screen.
            for (int i = 0; i < CatchRules.FruitKindCount; i++)
            {
                var kind = CatchRules.FruitFor(i);
                Assert.IsFalse(string.IsNullOrEmpty(CatchRules.FruitName(kind)), $"{kind} has no name");
                Assert.AreNotEqual(default(Color), FruitArt.Skin(kind), $"{kind} has no colour");
                Assert.Greater(SliceRules.ScoreFor(kind), 0);
            }

            // The art itself, built for real: the failure modes are "nothing was created" and
            // "it was created at the wrong size", and both are invisible in a screenshot of a menu.
            var host = new GameObject("fruit-under-test");
            try
            {
                FruitArt.Build(host.transform, FruitKind.Apple, 0.4f);
                Assert.GreaterOrEqual(host.transform.childCount, 2,
                    "an apple is a body, a stem and a leaf at least");
                Assert.AreEqual(0, host.GetComponentsInChildren<Collider>().Length,
                    "a fruit must not collide with anything: the games do their own arithmetic");

                FruitArt.BuildBomb(host.transform, 0.4f);
                Assert.Greater(host.transform.childCount, 3, "the bomb is its own shape");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
