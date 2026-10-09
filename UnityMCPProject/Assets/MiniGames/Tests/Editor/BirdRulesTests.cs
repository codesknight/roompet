using DshMiniGames;
using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// 愤怒的小鸟's arithmetic, and — the point of the whole design — the proof that every generated
    /// level can be cleared.
    ///
    /// The generator builds a random structure and then *plays it* with the same simulation the game
    /// uses, handing over only levels a machine has already beaten. These tests assert that promise
    /// holds for every stage and a spread of seeds, and that the promise is not being kept by
    /// generating trivial levels: the structures have to keep getting harder.
    /// </summary>
    public class BirdRulesTests
    {
        private static readonly BirdSettings Settings = BirdSettings.Default;

        // ------------------------------------------------------------------ physics

        [Test]
        public void Launch_PullBackAndFlyTheOtherWay()
        {
            var sling = new Vector2(0f, 0f);

            var flat = BirdRules.LaunchVelocity(sling, new Vector2(-1f, 0f), Settings);
            Assert.Greater(flat.x, 0f, "pulling left has to fire right");
            Assert.AreEqual(0f, flat.y, 0.001f);

            var up = BirdRules.LaunchVelocity(sling, new Vector2(-1f, -1f), Settings);
            Assert.Greater(up.x, 0f);
            Assert.Greater(up.y, 0f, "pulling down has to fire upward");

            // The pull is clamped, so a wild drag cannot produce a shot the solver never considered.
            var wild = BirdRules.LaunchVelocity(sling, new Vector2(-40f, -40f), Settings);
            Assert.AreEqual(Settings.MaxLaunchSpeed, wild.magnitude, 0.01f);

            var tiny = BirdRules.LaunchVelocity(sling, new Vector2(-0.05f, 0f), Settings);
            Assert.GreaterOrEqual(tiny.magnitude, Settings.MinLaunchSpeed - 0.01f);
            Assert.LessOrEqual(tiny.magnitude, Settings.MaxLaunchSpeed + 0.01f);
        }

        [Test]
        public void Launch_ATinyDragIsNotAShot()
        {
            var sling = new Vector2(0f, 0f);
            Assert.IsFalse(BirdRules.CanLaunch(sling, new Vector2(-0.05f, 0f), Settings),
                "a tap must not fire the bird");
            Assert.IsTrue(BirdRules.CanLaunch(sling, new Vector2(-1.2f, 0f), Settings),
                "a real pull must fire it");
        }

        [Test]
        public void Damage_MaterialsTellThePlayerWhatTheyAreMadeOf()
        {
            // Ice shatters at a tap, stone shrugs off everything but a proper hit: the material has to
            // be readable from one shot, or it is just decoration.
            Assert.Greater(BirdRules.DamageFor(4f, BlockKind.Ice, Settings), 0, "ice breaks easily");
            Assert.AreEqual(0, BirdRules.DamageFor(4f, BlockKind.Stone, Settings), "stone does not");
            Assert.Greater(BirdRules.DamageFor(12f, BlockKind.Stone, Settings), 0, "a real hit does");

            Assert.Greater(BirdRules.DamageFor(9f, BlockKind.Wood, Settings),
                BirdRules.DamageFor(9f, BlockKind.Stone, Settings),
                "the same hit does more to wood than to stone");

            // Damage never goes backwards as the hit gets harder.
            float previous = -1f;
            for (float speed = 0f; speed <= 20f; speed += 0.5f)
            {
                float damage = BirdRules.DamageFor(speed, BlockKind.Wood, Settings);
                Assert.GreaterOrEqual(damage, previous, $"damage fell at {speed:F1}");
                previous = damage;
            }

            // Health and score have to agree with the material: stone is the toughest and worth most.
            Assert.Greater(BirdRules.HealthFor(BlockKind.Stone), BirdRules.HealthFor(BlockKind.Wood));
            Assert.GreaterOrEqual(BirdRules.HealthFor(BlockKind.Wood), BirdRules.HealthFor(BlockKind.Ice));
            Assert.Greater(BirdRules.ScoreFor(BlockKind.Stone), BirdRules.ScoreFor(BlockKind.Wood));
            Assert.Greater(BirdRules.ScoreFor(BlockKind.Wood), BirdRules.ScoreFor(BlockKind.Ice));
        }

        [Test]
        public void Geometry_CircleAgainstBoxIsExact()
        {
            var box = new BirdBlock { X = 0f, Y = 0f, HalfW = 1f, HalfH = 1f, Alive = true };

            Assert.IsTrue(BirdRules.CircleHitsBox(Vector2.zero, 0.2f, box), "a bird inside the box is touching it");
            Assert.IsTrue(BirdRules.CircleHitsBox(new Vector2(1.1f, 0f), 0.2f, box), "just past the edge");
            Assert.IsFalse(BirdRules.CircleHitsBox(new Vector2(1.4f, 0f), 0.2f, box), "clear of the edge");

            // Outside the corner: the distance is to the corner, not to the nearest face. A circle
            // far enough away must miss even though its bounding box overlaps.
            Assert.IsTrue(BirdRules.CircleHitsBox(new Vector2(1.3f, 1.3f), 0.5f, box), "the corner counts");
            Assert.IsFalse(BirdRules.CircleHitsBox(new Vector2(1.3f, 1.3f), 0.2f, box),
                "a circle that only overlaps the corner square must miss");

            Assert.IsTrue(BirdRules.CircleHitsCircle(Vector2.zero, 0.5f, new Vector2(0.9f, 0f), 0.5f));
            Assert.IsFalse(BirdRules.CircleHitsCircle(Vector2.zero, 0.5f, new Vector2(1.2f, 0f), 0.5f));
        }

        [Test]
        public void Simulate_StopsOnTheGroundAndNeverRunsForever()
        {
            var level = BirdLevels.BuildFallback(1, Settings);
            var result = BirdRules.Simulate(level.Clone(), new Vector2(9f, 3f), Settings);

            Assert.IsFalse(result.Path.Count == 0, "the shot has to produce a path to animate");
            Assert.LessOrEqual(result.EndTime, Settings.MaxFlightSeconds + 0.001f,
                "a shot that never ends is a game that never comes back");
            Assert.Greater(result.Path.Count, 10);

            // The path descends at the end: whatever happened, the bird came down.
            var last = result.Path[result.Path.Count - 1];
            var earlier = result.Path[Mathf.Max(0, result.Path.Count - 40)];
            Assert.Less(last.y, earlier.y + 0.01f, "gravity has to win in the end");
        }

        // ------------------------------------------------------------------ the promise

        [Test]
        public void Generator_EveryStageIsWinnableWithItsOwnBirdCount()
        {
            // The requirement, asserted: "每关的场景地图可随机搭建但是必须保证能够通关". The check runs
            // the real simulation over a grid of real shots, so this is a statement about the level the
            // player receives — not about a diagram of it.
            for (int stage = 1; stage <= BirdLevels.StageCount; stage++)
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    var level = BirdLevels.Generate(stage, seed * 7919, Settings);

                    Assert.IsNotNull(level, $"stage {stage} seed {seed} produced no level");
                    Assert.Greater(level.Pigs.Count, 0, $"stage {stage} seed {seed} has no pigs to hit");
                    Assert.Greater(level.Blocks.Count, 0, $"stage {stage} seed {seed} is an empty field");
                    Assert.Greater(level.Birds, 0);

                    var replay = level.Clone();
                    var solution = level.Solution;

                    Assert.IsNotNull(solution, $"stage {stage} seed {seed} came with no solution");
                    Assert.LessOrEqual(solution.Count, level.Birds,
                        "a solution that needs more birds than the level gives is not a solution");

                    // Replay the recorded solution against the *live* rules: if this does not clear the
                    // level, the level was handed over on a bad verdict.
                    for (int i = 0; i < solution.Count && !replay.Cleared; i++)
                    {
                        BirdRules.Simulate(replay, solution[i], Settings);
                    }

                    Assert.IsTrue(replay.Cleared,
                        $"stage {stage} seed {seed}: the recorded solution does not clear the level");
                }
            }
        }

        [Test]
        public void Generator_HandsOverVariedStructures()
        {
            // The other half of "randomly built": if every level came out identical, the promise
            // above would be trivially true. Structures are compared by their shape — the pigs'
            // positions and the block count — which is what the player actually sees change.
            var shapes = new System.Collections.Generic.HashSet<string>();
            for (int stage = 1; stage <= BirdLevels.StageCount; stage += 4)
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    var level = BirdLevels.Generate(stage, seed * 104729, Settings);

                    var signature = new System.Text.StringBuilder();
                    signature.Append(level.Blocks.Count).Append('|');
                    for (int i = 0; i < level.Pigs.Count; i++)
                    {
                        signature.Append(Mathf.RoundToInt(level.Pigs[i].X * 10f)).Append(',');
                        signature.Append(Mathf.RoundToInt(level.Pigs[i].Y * 10f)).Append(';');
                    }

                    for (int i = 0; i < level.Blocks.Count; i++)
                    {
                        signature.Append(Mathf.RoundToInt(level.Blocks[i].X * 10f)).Append(',');
                    }

                    shapes.Add(signature.ToString());
                }
            }

            Assert.GreaterOrEqual(shapes.Count, 6,
                "twelve generated levels came out with fewer than six different layouts, which is not 'randomly built'");
        }

        [Test]
        public void Generator_LaterStagesAreHarderButNotImpossible()
        {
            // Difficulty has to climb and solvability has to hold at the same time — the two halves of
            // the requirement pulling against each other, checked together.
            int firstBlocks = 0, lastBlocks = 0;
            int firstPigs = 0, lastPigs = 0;

            for (int seed = 1; seed <= 6; seed++)
            {
                var early = BirdLevels.Generate(1, seed * 7919, Settings);
                var late = BirdLevels.Generate(BirdLevels.StageCount, seed * 7919, Settings);

                firstBlocks += early.Blocks.Count;
                lastBlocks += late.Blocks.Count;
                firstPigs += early.Pigs.Count;
                lastPigs += late.Pigs.Count;

                Assert.IsTrue(late.Pigs.Count >= 2, "the last stage should not be a single pig");
                Assert.IsTrue(late.Solution != null && late.Solution.Count > 0);
            }

            Assert.Greater(lastBlocks, firstBlocks, "later stages have to build more");
            Assert.Greater(lastPigs, firstPigs, "later stages have to have more pigs");
        }

        [Test]
        public void Generator_KeepsEveryStructureInsideTheSlingshotsReach()
        {
            // A level containing a pig the slingshot cannot reach is not a hard level, it is a broken
            // one — and this is exactly how the first version of this generator failed: towers eleven
            // units away from a slingshot that could throw ten, so every shot fell short and the
            // solver rejected every layout.
            Assert.Greater(BirdRules.MaxRise(Settings), 3f, "a bird has to be able to clear a tower");
            Assert.Greater(BirdRules.MaxRange(Settings, 0f), 8f, "and reach the far side of the field");

            var sling = new Vector2(Settings.SlingX, Settings.GroundY + Settings.SlingHeight);
            Assert.IsTrue(BirdRules.InRange(sling + new Vector2(5f, 0f), Settings));
            Assert.IsFalse(BirdRules.InRange(sling + new Vector2(40f, 0f), Settings), "the far horizon is not a target");
            Assert.IsFalse(BirdRules.InRange(sling + new Vector2(0f, 40f), Settings), "nor is the sky");

            for (int stage = 1; stage <= BirdLevels.StageCount; stage++)
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    var level = BirdLevels.Generate(stage, seed * 7919, Settings);
                    Assert.IsTrue(BirdLevels.AllInRange(level, Settings),
                        $"stage {stage} seed {seed} puts something out of the slingshot's reach");
                }
            }

            Assert.IsTrue(BirdLevels.AllInRange(BirdLevels.BuildFallback(1, Settings), Settings));
        }

        [Test]
        public void Generator_AFallbackExistsAndIsTriviallyWinnable()
        {
            // If every random attempt is rejected there is still a level to play, and it is one that
            // cannot be unfair: one ice tower, one pig, three birds.
            var fallback = BirdLevels.BuildFallback(7, Settings);

            Assert.AreEqual(1, fallback.Pigs.Count);
            Assert.AreEqual(3, fallback.Birds);

            System.Collections.Generic.List<Vector2> solution;
            Assert.IsTrue(BirdLevels.Solve(fallback, Settings, out solution),
                "the last-resort level has to be winnable");
            Assert.Greater(solution.Count, 0);
        }

        [Test]
        public void Generator_SolvingDoesNotDamageTheLevelItWasAsked()
        {
            // The solver works on copies: the level the player is given has to be intact, with every
            // pig alive and every block whole, or the "verified" level is a level that arrives broken.
            var level = BirdLevels.Generate(5, 4242, Settings);
            int pigs = level.PigsAlive;
            int blocks = level.Blocks.Count;

            System.Collections.Generic.List<Vector2> solution;
            BirdLevels.Solve(level, Settings, out solution);

            Assert.AreEqual(pigs, level.PigsAlive, "the solver killed pigs in the level itself");
            Assert.AreEqual(blocks, level.Blocks.Count, "the solver removed blocks from the level itself");

            int alive = 0;
            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (level.Blocks[i].Alive) alive++;
            }
            Assert.AreEqual(blocks, alive, "the solver left the player's level half-wrecked");
        }

        [Test]
        public void Solve_ReportsFailureForALevelThatCannotBeBeaten()
        {
            // The other half of a solvability check: it has to be able to say no. A pig walled in by
            // a full-height stone box cannot be reached by any shot in the candidate grid, so the
            // generator would reject this layout rather than hand it over.
            var level = new BirdLevel
            {
                Stage = 99,
                GroundY = Settings.GroundY,
                SlingX = Settings.SlingX,
                SlingY = Settings.GroundY + Settings.SlingHeight,
                Birds = 2
            };

            for (int row = 0; row < 12; row++)
            {
                level.Blocks.Add(new BirdBlock
                {
                    X = 2.4f,
                    Y = level.GroundY + 0.4f + row * 0.8f,
                    HalfW = 0.9f,
                    HalfH = 0.4f,
                    Kind = BlockKind.Stone,
                    Health = BirdRules.HealthFor(BlockKind.Stone),
                    Alive = true
                });
            }

            level.Pigs.Add(new BirdPig { X = 2.4f, Y = level.GroundY + 9f, Radius = 0.3f, Alive = true });

            System.Collections.Generic.List<Vector2> solution;
            Assert.IsFalse(BirdLevels.Solve(level, Settings, out solution),
                "a walled-in pig has to be reported as unreachable");
            Assert.IsTrue(level.PigsAlive > 0, "and the check must not have killed it as a side effect");
        }

        [Test]
        public void Blocks_FallWhenWhatHeldThemUpIsGone()
        {
            // The rule that makes a shot at the bottom of a tower worth more than the same shot at
            // the middle. Built by hand so the answer is known: two beams stacked on four uprights,
            // broken from underneath, must come down and take the pig with them.
            var level = new BirdLevel
            {
                Stage = 1,
                GroundY = Settings.GroundY,
                SlingX = Settings.SlingX,
                SlingY = Settings.GroundY + Settings.SlingHeight,
                Birds = 3
            };

            const float x = 1.0f;
            float baseY = level.GroundY + 0.34f;

            // Left and right uprights, a beam across them, a pig sitting on the beam.
            level.Blocks.Add(new BirdBlock
            {
                X = x - 0.5f, Y = baseY, HalfW = 0.2f, HalfH = 0.34f,
                Kind = BlockKind.Ice, Health = 1, Alive = true
            });
            level.Blocks.Add(new BirdBlock
            {
                X = x + 0.5f, Y = baseY, HalfW = 0.2f, HalfH = 0.34f,
                Kind = BlockKind.Ice, Health = 1, Alive = true
            });
            level.Blocks.Add(new BirdBlock
            {
                X = x, Y = baseY + 0.34f + 0.1f, HalfW = 0.7f, HalfH = 0.1f,
                Kind = BlockKind.Wood, Health = 2, Alive = true
            });
            level.Pigs.Add(new BirdPig { X = x, Y = baseY + 0.44f + 0.3f, Radius = 0.28f, Alive = true });

            // Record where the beam started, take the left upright away, then let the level settle:
            // the beam has nothing holding its left end, so it has to come down — and the pig riding
            // in the chamber below it goes down with it.
            float beamBefore = level.Blocks[2].Y;

            var left = level.Blocks[0];
            left.Alive = false;
            level.Blocks[0] = left;

            BirdRules.Settle(level, Settings);

            Assert.Less(level.Blocks[2].Y, beamBefore - 0.1f,
                "the beam kept floating after the support under it was removed");
            Assert.AreEqual(0, level.PigsAlive,
                "the block that came down should have taken the pig with it");
        }

        [Test]
        public void Coins_AndRanksSaySomethingAtEveryOutcome()
        {
            var level = BirdLevels.BuildFallback(1, Settings);

            Assert.Greater(BirdRules.CoinsFor(level, 3, 400), BirdRules.CoinsFor(level, 0, 100),
                "a better run pays more");
            Assert.GreaterOrEqual(BirdRules.CoinsFor(level, 0, 0), 0);
            Assert.AreEqual(0, BirdRules.CoinsFor(null, 0, 0));

            for (int birds = 0; birds <= 4; birds++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(BirdRules.RankFor(birds, birds * 100)));
            }

            Assert.AreNotEqual(BirdRules.RankFor(3, 300), BirdRules.RankFor(0, 300));
            Assert.IsFalse(string.IsNullOrEmpty(BirdLevels.Blurb(level)));
            Assert.IsFalse(string.IsNullOrEmpty(level.Describe()));
        }

        [Test]
        public void CandidateShots_AreAllShotsAPlayerCouldActuallyMake()
        {
            // The solver may only use shots the slingshot can produce: a "solution" that needs a
            // launch speed outside the clamp is a solution the player cannot reproduce.
            var shots = BirdLevels.CandidateShots(Settings);
            Assert.Greater(shots.Count, 10, "a solver with three shots is not a solver");

            for (int i = 0; i < shots.Count; i++)
            {
                float speed = shots[i].magnitude;
                Assert.GreaterOrEqual(speed, Settings.MinLaunchSpeed - 0.01f, $"shot {i} is too weak");
                Assert.LessOrEqual(speed, Settings.MaxLaunchSpeed + 0.01f, $"shot {i} is too strong");
                Assert.Greater(shots[i].x, 0f, "every shot has to go towards the tower");
            }
        }
    }
}
