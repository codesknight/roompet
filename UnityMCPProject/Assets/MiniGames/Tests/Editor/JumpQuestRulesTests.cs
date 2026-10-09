using System.Collections.Generic;
using DshMiniGames;
using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// The platformer's arithmetic.
    ///
    /// A side-scroller is the genre where the numbers *are* the game, and the two that matter
    /// most are also the two that are impossible to eyeball: how high a jump goes, and whether the
    /// level generator can produce a gap that cannot be crossed.
    /// </summary>
    public class JumpQuestRulesTests
    {
        private static readonly JumpSettings Settings = JumpSettings.Default;

        [Test]
        public void Step_JumpGoesUpAndComesBackDown()
        {
            var position = Vector2.zero;
            var velocity = Vector2.zero;

            JumpQuestRules.Step(ref position, ref velocity, 1f / 60f, 0f, false, true, 0f, Settings);
            Assert.Greater(velocity.y, 0f);
            Assert.Greater(position.y, 0f);

            float peak = position.y;
            for (int i = 0; i < 120; i++)
            {
                JumpQuestRules.Step(ref position, ref velocity, 1f / 60f, 0f, false, false, 1f, Settings);
                peak = Mathf.Max(peak, position.y);
            }

            Assert.Less(position.y, peak, "gravity has to bring the player back down");
        }

        [Test]
        public void Step_HoldingTheButtonJumpsHigher()
        {
            float tapped = SimulateJump(holdSeconds: 0f);
            float held = SimulateJump(holdSeconds: 0.4f);

            Assert.Greater(held, tapped + 0.3f, "holding the jump has to be worth doing");
            Assert.Less(held, tapped * 3f, "but not so much that tapping is pointless");
        }

        private static float SimulateJump(float holdSeconds)
        {
            var position = Vector2.zero;
            var velocity = Vector2.zero;
            float peak = 0f;

            for (float t = 0f; t < 3f; t += 1f / 120f)
            {
                bool held = t <= holdSeconds;
                JumpQuestRules.Step(ref position, ref velocity, 1f / 120f, 0f, held, t == 0f, t, Settings);
                peak = Mathf.Max(peak, position.y);
            }
            return peak;
        }

        [Test]
        public void Step_IsFrameRateIndependentAndClampsLongFrames()
        {
            float slow = Fall(30), fast = Fall(120);
            Assert.AreEqual(slow, fast, 0.25f, "30fps and 120fps have to fall the same distance");

            var position = Vector2.zero;
            var velocity = Vector2.zero;
            JumpQuestRules.Step(ref position, ref velocity, 1f, 0f, false, false, 0f, Settings);
            Assert.LessOrEqual(Mathf.Abs(position.y), Settings.MaxFallSpeed * 0.05f + 0.01f,
                "a one-second frame must be treated as a 50ms one");
        }

        private static float Fall(float fps)
        {
            var position = Vector2.zero;
            var velocity = Vector2.zero;
            for (int i = 0; i < (int)fps; i++)
            {
                JumpQuestRules.Step(ref position, ref velocity, 1f / fps, 0f, false, false, 0f, Settings);
            }
            return position.y;
        }

        [Test]
        public void Resolve_LandsOnAPlatformInsteadOfFallingThroughIt()
        {
            var blocks = new List<Block> { Block.Make(BlockKind.Platform, 0f, 0f, 6f, 0.4f) };
            var position = new Vector2(0f, 2f);
            var velocity = new Vector2(0f, -8f);

            for (int i = 0; i < 60; i++)
            {
                JumpQuestRules.Step(ref position, ref velocity, 1f / 60f, 0f, false, false, 0f, Settings);
                JumpQuestRules.Resolve(ref position, ref velocity, blocks, Settings);
            }

            Assert.AreEqual(0.2f + Settings.BodyHalfHeight, position.y, 0.02f,
                "the player should be standing on top of the platform");
            Assert.AreEqual(0f, velocity.y, 0.001f);
            Assert.IsTrue(JumpQuestRules.OnGround(position, blocks, Settings));
        }

        [Test]
        public void Resolve_StopsAtAWallWithoutLosingHeight()
        {
            var blocks = new List<Block>
            {
                Block.Make(BlockKind.Ground, 0f, -1f, 40f, 2f),
                Block.Make(BlockKind.Ground, 3f, 1f, 1f, 4f)
            };

            var position = new Vector2(1f, 1f);
            var velocity = Vector2.zero;

            for (int i = 0; i < 120; i++)
            {
                JumpQuestRules.Step(ref position, ref velocity, 1f / 60f, 1f, false, false, 0f, Settings);
                JumpQuestRules.Resolve(ref position, ref velocity, blocks, Settings);
            }

            Assert.Less(position.x, 3f, "the wall has to stop the player");
            Assert.AreEqual(2.5f - Settings.BodyHalfWidth, position.x, 0.06f,
                "and it has to stop them against the wall's face, not before it");
            Assert.Greater(position.y, 0.3f, "running into a wall is not climbing it");
        }

        [Test]
        public void OnGround_IsFalseInMidAir()
        {
            var blocks = new List<Block> { Block.Make(BlockKind.Ground, 0f, -1f, 20f, 2f) };

            Assert.IsTrue(JumpQuestRules.OnGround(new Vector2(0f, Settings.BodyHalfHeight),
                blocks, Settings));
            Assert.IsFalse(JumpQuestRules.OnGround(new Vector2(0f, 4f), blocks, Settings));
        }

        [Test]
        public void Stomp_OnlyCountsWhenComingDownOnTopOfIt()
        {
            var enemy = new Rect(-0.35f, -0.35f, 0.7f, 0.7f);

            // Falling onto its head, feet above the middle: a stomp.
            var above = new Vector2(0f, 0.46f + 0.35f + 0.05f);
            Assert.IsTrue(JumpQuestRules.IsStomp(above, new Vector2(0f, -4f), enemy, Settings));

            // Walking into its side at the same height: a hit, not a stomp.
            Assert.IsFalse(JumpQuestRules.IsStomp(new Vector2(0f, 0f), new Vector2(3f, -1f),
                enemy, Settings));

            // Rising through it: also a hit.
            Assert.IsFalse(JumpQuestRules.IsStomp(above, new Vector2(0f, 6f), enemy, Settings));
        }

        [Test]
        public void Level_EveryGapIsJumpableAndTheGoalExists()
        {
            // The important one. A generated level that cannot be finished is not a difficulty
            // setting, it is a bug — so it is checked rather than hoped for.
            float jumpHeight = JumpQuestRules.JumpHeight(Settings);
            float jumpDistance = JumpQuestRules.JumpDistance(Settings);

            Assert.Greater(jumpHeight, 1.5f, "a jump has to clear a reasonable platform");
            Assert.Greater(jumpDistance, 4f, "and carry the player across a gap");
            Assert.Less(jumpDistance, 14f, "but not across the whole screen");

            for (int seed = 1; seed <= 60; seed++)
            {
                var level = JumpQuestRules.BuildLevel(seed * 6151, Settings);
                Assert.IsNotEmpty(level, $"seed {seed} produced an empty level");

                string complaint;
                Assert.IsTrue(JumpQuestRules.IsPassable(level, Settings, out complaint),
                    $"seed {seed}: {complaint}");

                bool hasGoal = false, hasEnemy = false, hasCoin = false;
                foreach (var block in level)
                {
                    if (block.Kind == BlockKind.Goal) hasGoal = true;
                    if (block.Kind == BlockKind.Enemy) hasEnemy = true;
                    if (block.Kind == BlockKind.Coin) hasCoin = true;
                }

                Assert.IsTrue(hasGoal, $"seed {seed} has no goal");
                Assert.IsTrue(hasEnemy || hasCoin, $"seed {seed} has nothing to do");
            }
        }

        [Test]
        public void IsPassable_RejectsTheTwoWaysALevelCanBeImpossible()
        {
            float jumpDistance = JumpQuestRules.JumpDistance(Settings);
            float jumpHeight = JumpQuestRules.JumpHeight(Settings);

            var tooWide = new List<Block>
            {
                Block.Make(BlockKind.Ground, 0f, -1f, 4f, 2f),
                Block.Make(BlockKind.Ground, 4f + jumpDistance * 2f, -1f, 4f, 2f),
                Block.Make(BlockKind.Goal, 4f + jumpDistance * 2f, 1f, 0.5f, 2.8f)
            };

            string complaint;
            Assert.IsFalse(JumpQuestRules.IsPassable(tooWide, Settings, out complaint),
                "an unjumpable gap must be rejected");
            Assert.IsTrue(complaint.Contains("gap"), complaint);

            var tooTall = new List<Block>
            {
                Block.Make(BlockKind.Ground, 0f, -1f, 4f, 2f),
                Block.Make(BlockKind.Platform, 6f, jumpHeight * 3f, 4f, 0.4f),
                Block.Make(BlockKind.Goal, 6f, jumpHeight * 3f + 1f, 0.5f, 2.8f)
            };

            Assert.IsFalse(JumpQuestRules.IsPassable(tooTall, Settings, out complaint),
                "a step higher than a jump must be rejected");
            Assert.IsTrue(complaint.Contains("step"), complaint);

            var noGoal = new List<Block> { Block.Make(BlockKind.Ground, 0f, -1f, 4f, 2f) };
            Assert.IsFalse(JumpQuestRules.IsPassable(noGoal, Settings, out complaint));
            Assert.IsTrue(complaint.Contains("goal"), complaint);
        }

        [Test]
        public void Reward_PaysForCoinsAndMoreForFinishing()
        {
            Assert.AreEqual(0, JumpQuestRules.RewardFor(0, false));
            Assert.AreEqual(10, JumpQuestRules.RewardFor(10, false));
            Assert.Greater(JumpQuestRules.RewardFor(10, true), JumpQuestRules.RewardFor(10, false),
                "finishing has to be worth more than not finishing");
            Assert.AreEqual(0, JumpQuestRules.RewardFor(-5, false), "a negative run cannot pay out");

            Assert.AreNotEqual(JumpQuestRules.RankFor(true, 30), JumpQuestRules.RankFor(false, 0));
            Assert.IsFalse(string.IsNullOrEmpty(JumpQuestRules.RankFor(true, 0)));
        }

        [Test]
        public void Block_RectIsCentredOnItsPosition()
        {
            var block = Block.Make(BlockKind.Platform, 5f, 2f, 4f, 1f);
            var rect = block.Rect;

            Assert.AreEqual(3f, rect.xMin, 0.001f);
            Assert.AreEqual(7f, rect.xMax, 0.001f);
            Assert.AreEqual(1.5f, rect.yMin, 0.001f);
            Assert.AreEqual(2.5f, rect.yMax, 0.001f);
        }
    }
}
