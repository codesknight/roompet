using System.Collections.Generic;
using UnityEngine;

namespace DshMiniGames
{
    /// <summary>What a rectangle in the level is.</summary>
    public enum BlockKind
    {
        Ground,     // solid: stand on it, bump into its sides
        Platform,   // solid, but thinner and floating
        Coin,       // pickup, not solid
        Enemy,      // patrols its platform, hurts on contact, dies underfoot
        Goal        // the flag at the end
    }

    /// <summary>One rectangle of level, in world units.</summary>
    public struct Block
    {
        public BlockKind Kind;
        public float X;
        public float Y;
        public float Width;
        public float Height;

        /// <summary>How far an enemy patrols either side of where it was placed.</summary>
        public float Patrol;

        public Rect Rect => new Rect(X - Width * 0.5f, Y - Height * 0.5f, Width, Height);

        public static Block Make(BlockKind kind, float x, float y, float width, float height,
            float patrol = 0f)
            => new Block { Kind = kind, X = x, Y = y, Width = width, Height = height, Patrol = patrol };
    }

    /// <summary>Everything the platformer's movement needs to know.</summary>
    public struct JumpSettings
    {
        public float Gravity;
        public float JumpSpeed;
        public float RunSpeed;
        public float MaxFallSpeed;

        /// <summary>How long a jump can be held, in seconds.</summary>
        public float HoldSeconds;

        /// <summary>Extra upward acceleration while the button is held.</summary>
        public float HoldBoost;

        /// <summary>Half-width and half-height of the player's box.</summary>
        public float BodyHalfWidth;
        public float BodyHalfHeight;

        /// <summary>How far below the feet the ground check looks.</summary>
        public float GroundProbe;

        public static JumpSettings Default => new JumpSettings
        {
            Gravity = 34f,
            JumpSpeed = 11.4f,
            RunSpeed = 6.4f,
            MaxFallSpeed = 20f,
            HoldSeconds = 0.26f,
            HoldBoost = 26f,
            BodyHalfWidth = 0.34f,
            BodyHalfHeight = 0.46f,
            GroundProbe = 0.09f
        };
    }

    /// <summary>
    /// The platformer's arithmetic, with no scene in sight.
    ///
    /// A side-scroller is the one genre where "the numbers" *are* the game: how high a jump goes,
    /// how far it carries you, and whether a level generator can produce a gap that cannot be
    /// crossed. All of that is testable, and none of it needs a camera — so the tests here check
    /// jump arcs, landings, stomps and reachability, and the scene only draws rectangles.
    /// </summary>
    public static class JumpQuestRules
    {
        /// <summary>
        /// One step of the player's motion. <paramref name="dt"/> is clamped for the same reason
        /// the flappy game clamps it: one long frame must not teleport the player into a wall.
        /// </summary>
        public static void Step(ref Vector2 position, ref Vector2 velocity, float dt,
            float move, bool jumpHeld, bool jumpPressed, float heldFor, JumpSettings settings)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);

            position.x += move * settings.RunSpeed * step;

            if (jumpPressed) velocity.y = settings.JumpSpeed;
            if (jumpHeld && heldFor < settings.HoldSeconds && velocity.y > 0f)
            {
                velocity.y += settings.HoldBoost * step;
            }

            velocity.y -= settings.Gravity * step;
            velocity.y = Mathf.Max(velocity.y, -settings.MaxFallSpeed);
            position.y += velocity.y * step;
        }

        /// <summary>
        /// How high a jump goes from a standing start, with the button held for the full window.
        /// Level generation uses this to guarantee every gap is crossable.
        /// </summary>
        public static float JumpHeight(JumpSettings settings)
        {
            var position = Vector2.zero;
            var velocity = Vector2.zero;
            float peak = 0f;

            // Simulate it rather than solving it, because the hold boost makes the closed form
            // depend on whether the window ends before the apex.
            for (float t = 0f; t < 2f; t += 1f / 120f)
            {
                Step(ref position, ref velocity, 1f / 120f, 0f, true, t == 0f, t, settings);
                peak = Mathf.Max(peak, position.y);
            }
            return peak;
        }

        /// <summary>
        /// How far a running jump carries, in world units: the width of the widest gap the level
        /// generator is allowed to leave.
        /// </summary>
        public static float JumpDistance(JumpSettings settings)
        {
            var position = Vector2.zero;
            var velocity = Vector2.zero;

            float apex = JumpHeight(settings);
            float airtime = 0f;
            for (float t = 0f; t < 4f; t += 1f / 120f)
            {
                Step(ref position, ref velocity, 1f / 120f, 1f, true, t == 0f, t, settings);
                airtime = t;
                if (position.y < 0f && t > 0.1f) break;
            }

            _ = apex;
            return settings.RunSpeed * airtime;
        }

        /// <summary>True when the player's box overlaps a solid block.</summary>
        public static bool Overlaps(Vector2 centre, JumpSettings settings, Rect block)
        {
            var box = new Rect(centre.x - settings.BodyHalfWidth, centre.y - settings.BodyHalfHeight,
                settings.BodyHalfWidth * 2f, settings.BodyHalfHeight * 2f);
            return box.Overlaps(block);
        }

        /// <summary>
        /// Pushes the player out of anything solid they are overlapping.
        ///
        /// The push is along the axis of *least* penetration, which is the whole trick: resolving
        /// vertically first looked simpler and was wrong — a player who ran into a wall was
        /// standing "inside" it, so the vertical pass helpfully put them on top of it and they
        /// walked over every wall in the level. Two passes, because a corner can need the first
        /// push to expose the second.
        /// </summary>
        public static void Resolve(ref Vector2 position, ref Vector2 velocity, IList<Block> blocks,
            JumpSettings settings)
        {
            if (blocks == null) return;

            for (int pass = 0; pass < 2; pass++)
            {
                bool touched = false;

                for (int i = 0; i < blocks.Count; i++)
                {
                    if (!Solid(blocks[i])) continue;
                    var block = blocks[i].Rect;
                    if (!Overlaps(position, settings, block)) continue;

                    float boxLeft = position.x - settings.BodyHalfWidth;
                    float boxRight = position.x + settings.BodyHalfWidth;
                    float boxBottom = position.y - settings.BodyHalfHeight;
                    float boxTop = position.y + settings.BodyHalfHeight;

                    float pushLeft = boxRight - block.xMin;
                    float pushRight = block.xMax - boxLeft;
                    float pushDown = boxTop - block.yMin;
                    float pushUp = block.yMax - boxBottom;

                    float horizontal = Mathf.Min(pushLeft, pushRight);
                    float vertical = Mathf.Min(pushDown, pushUp);

                    if (vertical <= horizontal)
                    {
                        if (pushUp < pushDown)
                        {
                            position.y += pushUp;
                            if (velocity.y < 0f) velocity.y = 0f;
                        }
                        else
                        {
                            position.y -= pushDown;
                            if (velocity.y > 0f) velocity.y = 0f;
                        }
                    }
                    else if (pushLeft < pushRight)
                    {
                        position.x -= pushLeft;
                    }
                    else
                    {
                        position.x += pushRight;
                    }

                    touched = true;
                }

                if (!touched) break;
            }
        }

        /// <summary>Whether anything solid is within the ground probe's reach.</summary>
        public static bool OnGround(Vector2 position, IList<Block> blocks, JumpSettings settings)
        {
            if (blocks == null) return false;

            var feet = new Rect(
                position.x - settings.BodyHalfWidth + 0.02f,
                position.y - settings.BodyHalfHeight - settings.GroundProbe,
                settings.BodyHalfWidth * 2f - 0.04f,
                settings.GroundProbe);

            for (int i = 0; i < blocks.Count; i++)
            {
                if (!Solid(blocks[i])) continue;
                if (feet.Overlaps(blocks[i].Rect)) return true;
            }
            return false;
        }

        public static bool Solid(Block block)
            => block.Kind == BlockKind.Ground || block.Kind == BlockKind.Platform;

        /// <summary>
        /// What happens when the player's box touches a block, if anything.
        ///
        /// The stomp rule is the whole reason a platformer with enemies is fun rather than
        /// stressful: coming down on one kills it, walking into one is a mistake, and the
        /// difference is whether the player's feet were above the enemy's middle last frame.
        /// </summary>
        public static bool IsStomp(Vector2 position, Vector2 velocity, Rect enemy,
            JumpSettings settings)
        {
            if (velocity.y > 0.5f) return false;                 // rising through it is a hit
            float feet = position.y - settings.BodyHalfHeight;
            return feet >= enemy.center.y - 0.06f;
        }

        /// <summary>
        /// Builds a level: a run of ground, gaps, floating platforms, coins and a flag.
        ///
        /// Generated rather than hand-placed, because a hand-built level in a scene file is the
        /// one thing this project keeps avoiding — and because a generated one can be *checked*:
        /// every gap is shorter than a jump, every step is lower than a jump, and the goal is
        /// always reachable.
        /// </summary>
        public static List<Block> BuildLevel(int seed, JumpSettings settings, int sections = 14)
        {
            var rng = new System.Random(seed);
            var blocks = new List<Block>();

            const float groundTop = 0f;
            float maxGap = JumpDistance(settings) * 0.62f;
            float maxStep = JumpHeight(settings) * 0.62f;

            float x = -6f;
            blocks.Add(Block.Make(BlockKind.Ground, x + 6f, groundTop - 1f, 12f, 2f));
            x += 12f;

            for (int i = 0; i < sections; i++)
            {
                float roll = (float)rng.NextDouble();

                if (roll < 0.35f)
                {
                    // A gap with a platform in the middle of it, so the jump is optional.
                    float gap = Mathf.Lerp(1.6f, maxGap, (float)rng.NextDouble());
                    x += gap;
                    blocks.Add(Block.Make(BlockKind.Ground, x + 3f, groundTop - 1f, 6f, 2f));

                    if (rng.NextDouble() < 0.5f)
                    {
                        blocks.Add(Block.Make(BlockKind.Platform, x - gap * 0.5f,
                            groundTop + maxStep * 0.8f, 1.6f, 0.35f));
                    }
                    x += 6f;
                }
                else if (roll < 0.65f)
                {
                    // Stairs: two or three platforms, each within a jump of the last.
                    int steps = 2 + rng.Next(2);
                    float stepX = x;
                    float stepY = groundTop + 0.9f;
                    for (int s = 0; s < steps; s++)
                    {
                        stepX += 2.4f;
                        stepY = Mathf.Min(stepY + maxStep * 0.6f, groundTop + maxStep * 1.6f);
                        blocks.Add(Block.Make(BlockKind.Platform, stepX, stepY, 2f, 0.35f));
                        if (rng.NextDouble() < 0.5f)
                        {
                            blocks.Add(Block.Make(BlockKind.Coin, stepX, stepY + 0.7f, 0.5f, 0.5f));
                        }
                    }
                    x = stepX + 1.4f;
                    blocks.Add(Block.Make(BlockKind.Ground, x + 3f, groundTop - 1f, 6f, 2f));
                    x += 6f;
                }
                else
                {
                    // Flat run with an enemy on it.
                    float width = Mathf.Lerp(6f, 10f, (float)rng.NextDouble());
                    blocks.Add(Block.Make(BlockKind.Ground, x + width * 0.5f, groundTop - 1f,
                        width, 2f));

                    if (rng.NextDouble() < 0.75f)
                    {
                        blocks.Add(Block.Make(BlockKind.Enemy, x + width * 0.5f, groundTop + 0.45f,
                            0.7f, 0.7f, patrol: Mathf.Min(2.2f, width * 0.3f)));
                    }

                    for (int c = 0; c < 3; c++)
                    {
                        // At chest height, not head height: a coin the walking player's box only
                        // just touches is a coin that reads as "I ran through it and got nothing".
                        blocks.Add(Block.Make(BlockKind.Coin, x + 1.2f + c * 0.9f,
                            groundTop + 0.7f, 0.5f, 0.5f));
                    }
                    x += width;
                }
            }

            blocks.Add(Block.Make(BlockKind.Ground, x + 4f, groundTop - 1f, 8f, 2f));
            blocks.Add(Block.Make(BlockKind.Goal, x + 4f, groundTop + 1.4f, 0.5f, 2.8f));

            return blocks;
        }

        /// <summary>
        /// Checks a generated level can actually be finished: every gap is jumpable and every
        /// step is climbable. This is the test that makes a generated level trustworthy —
        /// "unwinnable level" is not a difficulty setting, it is a bug.
        /// </summary>
        public static bool IsPassable(IList<Block> blocks, JumpSettings settings,
            out string complaint)
        {
            complaint = "";
            if (blocks == null || blocks.Count == 0)
            {
                complaint = "level is empty";
                return false;
            }

            float maxGap = JumpDistance(settings);
            float maxStep = JumpHeight(settings);

            float previousTop = float.NaN;
            float previousRight = float.NaN;
            bool seenGoal = false;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (!Solid(block)) continue;

                var rect = block.Rect;

                if (!float.IsNaN(previousRight))
                {
                    float gap = rect.xMin - previousRight;
                    if (gap > maxGap)
                    {
                        complaint = $"gap of {gap:F2} at x={rect.xMin:F2} is wider than the " +
                                    $"{maxGap:F2} a jump carries";
                        return false;
                    }
                }

                if (!float.IsNaN(previousTop))
                {
                    float rise = rect.yMax - previousTop;
                    if (rise > maxStep)
                    {
                        complaint = $"a step of {rise:F2} at x={rect.xMin:F2} is higher than the " +
                                    $"{maxStep:F2} a jump reaches";
                        return false;
                    }
                }

                previousTop = rect.yMax;
                previousRight = Mathf.Max(previousRight, rect.xMax);
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Kind == BlockKind.Goal) seenGoal = true;
            }

            if (!seenGoal)
            {
                complaint = "level has no goal";
                return false;
            }

            return true;
        }

        /// <summary>Coins a finished level is worth, including the completion bonus.</summary>
        public static int RewardFor(int coins, bool finished)
            => Mathf.Max(0, coins) + (finished ? 15 : 0);

        /// <summary>A word for the result panel.</summary>
        public static string RankFor(bool finished, int coins)
        {
            if (!finished) return coins >= 8 ? "差一点就到了" : "下次小心点";
            if (coins >= 30) return "一路捡到手软";
            if (coins >= 18) return "走得漂亮";
            return "到达终点";
        }
    }
}
