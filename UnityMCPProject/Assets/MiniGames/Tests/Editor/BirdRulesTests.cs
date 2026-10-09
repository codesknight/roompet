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

        /// <summary>
        /// The same rules, with a shorter shot and fewer generation attempts.
        ///
        /// Round 21 made a shot cost real simulation time — a rigid body has to be integrated, contacted
        /// and settled — and a test that proves the promise over dozens of levels at the shipped settings
        /// takes minutes. The physics is the same code with a smaller time budget, and the *shipped*
        /// settings still get their own test below, so the sweep stays honest and the suite stays usable.
        /// </summary>
        private static BirdSettings Fast()
        {
            var fast = BirdSettings.Default;
            fast.MaxFlightSeconds = 2.0f;
            return fast;
        }

        private static readonly BirdSettings Quick = Fast();

        // ------------------------------------------------------------------ physics

        [Test]
        public void Physics_ABlockInMidAirFallsAllTheWayDown()
        {
            // The most basic thing a physics step has to do, and the one the first version of the resting
            // rule broke: a block with nothing under it fell three centimetres and went to sleep hanging
            // in the air, because the damping that keeps a tower quiet had slowed its fall below the
            // sleep threshold. Gravity is not a resting contact.
            var level = new BirdLevel
            {
                Stage = 1,
                GroundY = Settings.GroundY,
                SlingX = Settings.SlingX,
                SlingY = Settings.GroundY + Settings.SlingHeight,
                Birds = 3
            };

            level.Blocks.Add(new BirdBlock
            {
                X = 0f,
                Y = Settings.GroundY + 2f,
                HalfW = 0.2f,
                HalfH = 0.2f,
                Kind = BlockKind.Wood,
                Health = 2,
                Alive = true
            });

            BirdRules.Settle(level, Settings);

            Assert.AreEqual(Settings.GroundY + 0.2f, level.Blocks[0].Y, 0.05f,
                "the block stopped falling before it reached the ground");
        }

        [Test]
        public void Physics_ATowerLeftAloneStandsStill()
        {
            // The other half of the same problem. A stack of boxes in a hand-written solver creeps:
            // residual velocities a few centimetres a second walk the whole structure sideways, and the
            // symptom is a tower that quietly falls over while the player is aiming at it.
            var level = ThreeStoreyTower();
            var start = new Vector2[level.Blocks.Count];
            for (int i = 0; i < level.Blocks.Count; i++) start[i] = level.Blocks[i].Centre;

            BirdRules.Settle(level, Settings);

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                float drift = (level.Blocks[i].Centre - start[i]).magnitude;
                Assert.Less(drift, 0.10f, $"block {i} drifted {drift:F3} with nobody touching it");
            }

            Assert.AreEqual(1, level.PigsAlive, "the pig died before anyone shot at it");
        }

        /// <summary>Two uprights, a beam across them, a second storey and a pig in the bottom chamber.</summary>
        private static BirdLevel ThreeStoreyTower()
        {
            var level = new BirdLevel
            {
                Stage = 1,
                GroundY = Settings.GroundY,
                SlingX = Settings.SlingX,
                SlingY = Settings.GroundY + Settings.SlingHeight,
                Birds = 3
            };

            for (int row = 0; row < 3; row++)
            {
                float y = Settings.GroundY + 0.344f + row * 0.88f;
                Upright(level, -0.6f, y);
                Upright(level, 0.6f, y);
                if (row < 2) Beam(level, 0f, y + 0.44f, 0.78f);
            }

            level.Pigs.Add(new BirdPig
            {
                X = 0f,
                Y = Settings.GroundY + 0.264f,
                Radius = 0.26f,
                Alive = true
            });

            return level;
        }

        private static void Upright(BirdLevel level, float x, float y)
        {
            level.Blocks.Add(new BirdBlock
            {
                X = x, Y = y, HalfW = 0.24f, HalfH = 0.34f,
                Kind = BlockKind.Wood, Health = 2, Alive = true
            });
        }

        private static void Beam(BirdLevel level, float x, float y, float halfWidth)
        {
            level.Blocks.Add(new BirdBlock
            {
                X = x, Y = y, HalfW = halfWidth, HalfH = 0.1f,
                Kind = BlockKind.Wood, Health = 2, Alive = true
            });
        }

        [Test]
        public void Physics_KnockingOutALegBringsTheStructureDown()
        {
            // "倒塌也能压死猪", as a rule: take the support out from under a beam and the beam has to
            // come down. The previous version of this game moved blocks along a script when their
            // support disappeared; this one has real bodies, and what is asserted here is that a body
            // whose weight is over nothing actually starts falling.
            var level = ThreeStoreyTower();
            BirdRules.Settle(level, Settings);

            float beamStart = level.Blocks[2].Y;

            var leg = level.Blocks[0];
            leg.Alive = false;
            level.Blocks[0] = leg;

            BirdRules.Settle(level, Settings);

            bool cameDown = level.Blocks[2].Y < beamStart - 0.2f
                            || Mathf.Abs(level.Blocks[2].Angle) > 0.4f;
            Assert.IsTrue(cameDown,
                $"the beam kept floating after its support was removed (y {beamStart:F2} → {level.Blocks[2].Y:F2}, " +
                $"angle {level.Blocks[2].Angle:F2})");

            // And the storey above it went with it, which is what a collapse *is*.
            bool upperMoved = Mathf.Abs(level.Blocks[3].Angle) > 0.2f
                              || level.Blocks[3].Y < Settings.GroundY + 0.344f + 0.88f - 0.15f;
            Assert.IsTrue(upperMoved, "the storey above the beam stayed hanging in the air");
        }

        [Test]
        public void Physics_AShotTopplesTheStructureInsteadOfFilingItAway()
        {
            // What the player asked for, stated as a test: wood, stone and ice are objects now, so a
            // bird that goes through a tower leaves blocks rotated, displaced and broken — not a tidy
            // row of intact boxes with one missing.
            var level = ThreeStoreyTower();
            BirdRules.Settle(level, Settings);

            var start = new Vector2[level.Blocks.Count];
            for (int i = 0; i < level.Blocks.Count; i++) start[i] = level.Blocks[i].Centre;

            // Fired at the base of the near leg, like a player would.
            var shot = new Vector2(Settings.MaxLaunchSpeed * 0.98f, -1.4f);
            var result = BirdRules.Simulate(level, shot, Settings, true, false);

            int disturbed = 0;
            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (!level.Blocks[i].Alive) { disturbed++; continue; }
                if ((level.Blocks[i].Centre - start[i]).magnitude > 0.2f) disturbed++;
                else if (Mathf.Abs(level.Blocks[i].Angle) > 0.2f) disturbed++;
            }

            Assert.Greater(result.BlocksBroken + disturbed, 1,
                "the shot left the structure standing as if nothing had happened");
            Assert.LessOrEqual(result.EndTime, Settings.MaxFlightSeconds + 0.001f,
                "a shot that never ends is a game that never comes back");
        }

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
        public void Preview_ShowsTheArcTheBirdWillActuallyFly()
        {
            // The dotted line is computed by the same integrator the simulation uses, so it cannot
            // promise an arc the bird will not take — and it has to stop at the ground rather than
            // running on past it.
            var sling = new Vector2(Settings.SlingX, Settings.GroundY + Settings.SlingHeight);
            var points = new System.Collections.Generic.List<Vector2>();
            BirdRules.PreviewArc(sling, new Vector2(12f, 5f), Settings, points);

            Assert.Greater(points.Count, 5, "an aim aid with four dots in it is not an arc");
            Assert.AreEqual(sling.x, points[0].x, 0.001f, "the arc has to start at the sling");

            float highest = float.MinValue;
            for (int i = 0; i < points.Count; i++) highest = Mathf.Max(highest, points[i].y);
            Assert.Greater(highest, sling.y, "a shot fired upwards has to go up");

            var last = points[points.Count - 1];
            Assert.GreaterOrEqual(last.y, Settings.GroundY, "the preview ran on through the floor");
            Assert.Greater(last.x, points[0].x, "the arc has to go somewhere");
        }

        [Test]
        public void Preview_DotsSitOnTheRealParabola()
        {
            // 「抛物线完全错位」 was the report, and this is the part of it that a test can hold: the dots are
            // not a lookalike curve, they are *the* shot. The flight integrates semi-implicitly
            // (v -= g·dt, then p += v·dt), so after K steps the position is exactly
            // x = x₀ + K·dt·vx and y = y₀ + K·dt·vy − g·dt²·K(K+1)/2 — the dots must match that, one per
            // step, or what the player aims at is not where the bird goes.
            var sling = new Vector2(Settings.SlingX, Settings.GroundY + Settings.SlingHeight);
            var velocity = new Vector2(-13.5f, 9.2f);
            var points = new System.Collections.Generic.List<Vector2>();

            // Four seconds of flight, sampled once per step: long enough for this shot to reach the
            // ground, which is where the preview has to stop.
            BirdRules.PreviewArc(sling, velocity, Settings, points, 4f, Settings.Step);

            float dt = Settings.Step;
            Assert.Greater(points.Count, 10, "one dot per step was asked for");

            for (int i = 0; i < points.Count; i++)
            {
                float expectedX = sling.x + velocity.x * dt * i;
                float expectedY = sling.y + velocity.y * dt * i
                                  - Settings.Gravity * dt * dt * (i * (i + 1)) * 0.5f;

                Assert.AreEqual(expectedX, points[i].x, 0.01f, $"dot {i} is off the line of the shot");
                Assert.AreEqual(expectedY, points[i].y, 0.01f, $"dot {i} is off the arc of the shot");
            }

            float floor = Settings.GroundY + Settings.BirdRadius;
            var last = points[points.Count - 1];
            Assert.GreaterOrEqual(last.y, floor, "the preview sampled a dot below the ground");
            Assert.Less(last.y, floor + 0.3f,
                "the preview should stop when it reaches the ground, not carry on past it");
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

        [Test]
        public void Replay_CarriesEveryBlockAndPigEveryFrame()
        {
            // The view has nothing but the recording: if a frame is missing a body, or the recording
            // stops early, the picture on screen and the outcome that was verified come apart.
            var level = BirdLevels.BuildFallback(1, Settings);
            var result = BirdRules.Simulate(level, new Vector2(13f, 1f), Settings, true, true);

            Assert.Greater(result.Frames.Count, 10, "a shot worth watching needs more than ten frames");
            Assert.LessOrEqual(result.Frames[result.Frames.Count - 1].Time, result.EndTime + 0.001f);

            for (int i = 0; i < result.Frames.Count; i++)
            {
                var frame = result.Frames[i];
                Assert.AreEqual(level.Blocks.Count, frame.Blocks.Length, "a frame is missing blocks");
                Assert.AreEqual(level.Pigs.Count, frame.Pigs.Length, "a frame is missing pigs");
                Assert.AreEqual(frame.Blocks.Length, frame.Angles.Length);
                Assert.AreEqual(frame.Blocks.Length, frame.Alive.Length);
            }

            // Time only moves forwards, or the view would jump about.
            for (int i = 1; i < result.Frames.Count; i++)
            {
                Assert.Greater(result.Frames[i].Time, result.Frames[i - 1].Time);
            }
        }

        // ------------------------------------------------------------------ the promise

        [Test]
        public void Generator_EveryStageIsWinnableWithItsOwnBirdCount()
        {
            // The requirement, asserted: "每关的场景地图可随机搭建但是必须保证能够通关". The check runs
            // the real simulation over a grid of real shots, so this is a statement about the level the
            // player receives — not about a diagram of it.
            //
            // Three stages at the *shipped* settings, rather than all twelve at four seeds each: a
            // rigid-body shot costs a couple of hundred milliseconds and the full matrix took minutes.
            // The sweep below covers every stage on the same code with a shorter shot.
            int[] stages = { 1, 6, 12 };

            for (int i = 0; i < stages.Length; i++)
            {
                var level = BirdLevels.Generate(stages[i], 20250607 + stages[i] * 977, Settings);
                AssertLevelClears(level, Settings, "stage " + stages[i]);
            }
        }

        [Test]
        public void Generator_HoldsForEveryStageOnTheQuickProfile()
        {
            // All twelve stages, every stage's own shipped seed, on the short-shot profile: the promise
            // is about *every* stage, and this is what says so without needing a minute of simulation.
            for (int stage = 1; stage <= BirdLevels.StageCount; stage++)
            {
                var level = BirdLevels.Generate(stage, 20250607 + stage * 977, Quick);
                AssertLevelClears(level, Quick, "stage " + stage);

                Assert.IsTrue(BirdLevels.IsStable(level, Quick),
                    $"stage {stage} was handed over in a pose that falls apart on its own");
            }
        }

        /// <summary>Replays the recorded solution and insists that it clears the level.</summary>
        private static void AssertLevelClears(BirdLevel level, BirdSettings settings, string who)
        {
            Assert.IsNotNull(level, who + " produced no level");
            Assert.Greater(level.Pigs.Count, 0, who + " has no pigs to hit");
            Assert.Greater(level.Blocks.Count, 0, who + " is an empty field");
            Assert.Greater(level.Birds, 0);

            var solution = level.Solution;
            Assert.IsNotNull(solution, who + " came with no solution");
            Assert.LessOrEqual(solution.Count, level.Birds,
                who + ": a solution that needs more birds than the level gives is not a solution");

            var replay = level.Clone();
            for (int i = 0; i < solution.Count && !replay.Cleared; i++)
            {
                BirdRules.Simulate(replay, solution[i], settings);
            }

            Assert.IsTrue(replay.Cleared, who + ": the recorded solution does not clear the level");
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
                for (int seed = 1; seed <= 2; seed++)
                {
                    var level = BirdLevels.Generate(stage, seed * 104729, Quick);

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

            for (int seed = 1; seed <= 3; seed++)
            {
                var early = BirdLevels.Generate(1, seed * 7919, Quick);
                var late = BirdLevels.Generate(BirdLevels.StageCount, seed * 7919, Quick);

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

            for (int stage = 1; stage <= BirdLevels.StageCount; stage += 4)
            {
                for (int seed = 1; seed <= 2; seed++)
                {
                    var level = BirdLevels.Generate(stage, seed * 7919, Quick);
                    Assert.IsTrue(BirdLevels.AllInRange(level, Quick),
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
            var level = BirdLevels.Generate(5, 4242, Quick);
            int pigs = level.PigsAlive;
            int blocks = level.Blocks.Count;

            System.Collections.Generic.List<Vector2> solution;
            BirdLevels.Solve(level, Quick, out solution);

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
            // The other half of a solvability check: it has to be able to say no. The pig here stands
            // *behind* the slingshot, and every candidate shot fires downrange, so no shot in the grid
            // can reach it and the generator would reject the layout rather than hand it over.
            //
            // The old fixture walled the pig inside twelve rows of stone. That was unreachable under the
            // old rules — where blocks only ever fell straight down — and is not any more: with real
            // bodies the whole wall can be toppled onto it. A fixture has to be impossible for a reason
            // the *current* physics cannot route around, which is what "behind the sling" is.
            var level = new BirdLevel
            {
                Stage = 99,
                GroundY = Settings.GroundY,
                SlingX = Settings.SlingX,
                SlingY = Settings.GroundY + Settings.SlingHeight,
                Birds = 2
            };

            level.Pigs.Add(new BirdPig
            {
                X = Settings.SlingX - 6f,
                Y = Settings.GroundY + 0.3f,
                Radius = 0.3f,
                Alive = true
            });

            System.Collections.Generic.List<Vector2> solution;
            Assert.IsFalse(BirdLevels.Solve(level, Settings, out solution),
                "a pig behind the slingshot has to be reported as unreachable");
            Assert.IsTrue(level.PigsAlive > 0, "and the check must not have killed it as a side effect");
        }

        [Test]
        public void LevelCache_SurvivesARoundTripAndStillClears()
        {
            // A verified level is remembered so the generator only has to prove it once, which is only
            // safe if what comes back is the same level — same pose, same pigs, same solution.
            var level = BirdLevels.Generate(2, 20250607 + 2 * 977, Quick);
            string text = BirdLevels.Encode(level);
            var back = BirdLevels.Decode(text);

            Assert.IsNotNull(back, "the encoded level could not be read back");
            Assert.AreEqual(level.Blocks.Count, back.Blocks.Count);
            Assert.AreEqual(level.Pigs.Count, back.Pigs.Count);
            Assert.AreEqual(level.Solution.Count, back.Solution.Count);
            Assert.AreEqual(level.Birds, back.Birds);
            Assert.AreEqual(level.Stage, back.Stage);

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                Assert.AreEqual(level.Blocks[i].X, back.Blocks[i].X, 0.01f);
                Assert.AreEqual(level.Blocks[i].Y, back.Blocks[i].Y, 0.01f);
                Assert.AreEqual(level.Blocks[i].Kind, back.Blocks[i].Kind);
            }

            AssertLevelClears(back, Quick, "the cached level");

            // And rubbish in the cache is not a level, rather than a level with no blocks in it.
            Assert.IsNull(BirdLevels.Decode("this is not a level"));
            Assert.IsNull(BirdLevels.Decode(""));
            Assert.IsNull(BirdLevels.Decode(null));
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
            // launch speed outside the clamp is a solution the player cannot reproduce — and so is one
            // *below* the speed a real drag produces, which is the subtler half of the same rule and
            // the one that was wrong until the grid learned about the launch threshold.
            var shots = BirdLevels.CandidateShots(Settings);
            Assert.Greater(shots.Count, 10, "a solver with three shots is not a solver");

            var fine = BirdLevels.FineShots(Settings);
            var coarse = BirdLevels.CoarseShots(Settings);

            for (int i = 0; i < shots.Count; i++)
            {
                Assert.IsTrue(BirdRules.IsLaunchReachable(shots[i], Settings),
                    $"candidate {i} ({shots[i].magnitude:F2}) is not a shot a player can make");
                Assert.Greater(shots[i].x, 0f, "every shot has to go towards the tower");
            }

            for (int i = 0; i < fine.Count; i++) Assert.IsTrue(BirdRules.IsLaunchReachable(fine[i], Settings));
            for (int i = 0; i < coarse.Count; i++) Assert.IsTrue(BirdRules.IsLaunchReachable(coarse[i], Settings));

            // A drag just past the threshold fires, and one just short of it does not.
            var sling = new Vector2(0f, 0f);
            float threshold = Settings.MaxPull * BirdRules.MinPullFraction;
            Assert.IsTrue(BirdRules.CanLaunch(sling, new Vector2(-threshold * 1.02f, 0f), Settings));
            Assert.IsFalse(BirdRules.CanLaunch(sling, new Vector2(-threshold * 0.9f, 0f), Settings));

            var weakest = BirdRules.LaunchVelocity(sling, new Vector2(-threshold * 1.02f, 0f), Settings);
            Assert.AreEqual(BirdRules.SlowestLaunchSpeed(Settings), weakest.magnitude, 0.05f,
                "the weakest shot a player can fire is not the one the solver assumes");
        }
    }
}
