using System.Collections.Generic;
using UnityEngine;

namespace DshMiniGames
{
    /// <summary>What a structure is made of.</summary>
    public enum BlockKind { Ice = 0, Wood = 1, Stone = 2 }

    /// <summary>One box in the structure.</summary>
    public struct BirdBlock
    {
        public float X;
        public float Y;          // centre
        public float HalfW;
        public float HalfH;
        public BlockKind Kind;
        public int Health;
        public bool Alive;

        /// <summary>Set while the block is falling, so the view can drop it without re-deriving.</summary>
        public bool Falling;

        /// <summary>How far it has fallen since it lost its support, for the landing damage.</summary>
        public float FallDistance;

        public float Bottom => Y - HalfH;
        public float Top => Y + HalfH;
        public float Left => X - HalfW;
        public float Right => X + HalfW;
    }

    /// <summary>One pig.</summary>
    public struct BirdPig
    {
        public float X;
        public float Y;
        public float Radius;
        public bool Alive;

        public float Bottom => Y - Radius;
        public float Left => X - Radius;
        public float Right => X + Radius;
    }

    /// <summary>
    /// A level: the structure, the pigs, and how many birds the player gets.
    ///
    /// Plain data, deliberately. The same object is built by the generator, judged by the solvability
    /// check, and played by the game — so "the level is winnable" is a statement about the level the
    /// player actually receives, not about a diagram of it.
    /// </summary>
    public sealed class BirdLevel
    {
        public int Stage;
        public int Birds;
        public float GroundY;
        public List<BirdBlock> Blocks = new List<BirdBlock>();
        public List<BirdPig> Pigs = new List<BirdPig>();

        /// <summary>Where the slingshot stands.</summary>
        public float SlingX;
        public float SlingY;

        /// <summary>
        /// The shots the generator proved will clear this level, one per bird.
        ///
        /// Kept because it turns "this level is solvable" from a claim into something the game can
        /// actually use: the HUD offers it as a hint, and a test can replay it against the live
        /// rules. Empty for a hand-made level that was never verified.
        /// </summary>
        public List<Vector2> Solution;

        public bool Cleared
        {
            get
            {
                for (int i = 0; i < Pigs.Count; i++)
                {
                    if (Pigs[i].Alive) return false;
                }
                return Pigs.Count > 0;
            }
        }

        public int PigsAlive
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < Pigs.Count; i++)
                {
                    if (Pigs[i].Alive) alive++;
                }
                return alive;
            }
        }

        /// <summary>A deep copy: the solver must be able to try a shot without wrecking the level.</summary>
        public BirdLevel Clone()
        {
            var copy = new BirdLevel
            {
                Stage = Stage,
                Birds = Birds,
                GroundY = GroundY,
                SlingX = SlingX,
                SlingY = SlingY,
                Blocks = new List<BirdBlock>(Blocks),
                Pigs = new List<BirdPig>(Pigs)
            };
            return copy;
        }

        /// <summary>How many blocks/pigs the level has, for the diagnostics line.</summary>
        public string Describe()
            => "第 " + Stage + " 关：" + Pigs.Count + " 只猪、" + Blocks.Count + " 块、" + Birds + " 只鸟";
    }

    /// <summary>The physics and damage numbers.</summary>
    public struct BirdSettings
    {
        public float Gravity;

        /// <summary>The slingshot's pull range, in world units, and the speeds it maps to.</summary>
        public float MaxPull;
        public float MinLaunchSpeed;
        public float MaxLaunchSpeed;

        /// <summary>Radius of the bird, for the collision tests.</summary>
        public float BirdRadius;

        /// <summary>Where the ground is, where the slingshot stands, and how high its fork is.</summary>
        public float GroundY;
        public float SlingX;
        public float SlingHeight;

        /// <summary>
        /// How fast the bird must be going to do one point of damage, per material.
        ///
        /// Ice shatters at a tap, stone shrugs off everything but a proper hit. This is the number
        /// that makes the material readable: the player learns it in one shot.
        /// </summary>
        public float IceThreshold;
        public float WoodThreshold;
        public float StoneThreshold;

        /// <summary>Fixed simulation step. The rules and the solvability check must agree exactly.</summary>
        public float Step;

        /// <summary>Longest a shot may run, in seconds.</summary>
        public float MaxFlightSeconds;

        /// <summary>How far a falling block has to drop to hurt what it lands on.</summary>
        public float CrushDistance;

        /// <summary>Impact speed below which the bird just bumps and keeps going.</summary>
        public float MinImpactSpeed;

        /// <summary>
        /// How much horizontal speed survives a second of rolling along the ground.
        ///
        /// Applied as a rate rather than a per-step multiplier, because a per-step 0.88 at 120 steps a
        /// second is a brick wall and the bird stopped the instant it landed.
        /// </summary>
        public float GroundFriction;

        public static BirdSettings Default => new BirdSettings
        {
            // Gravity and launch speed set the slingshot's *reach*, and the reach is what decides
            // whether a level is a level or a museum piece: with the first numbers here (18 and 13.5,
            // sling 1.9 above the ground) the farthest a bird could fly was about ten units, and the
            // towers were built eleven away — so every shot fell short, the solver rejected every
            // layout, and the generator quietly handed out its trivial fallback instead. Reach is
            // asserted against the level geometry now (see BirdRules.MaxRange).
            Gravity = 16f,
            MaxPull = 2.6f,
            MinLaunchSpeed = 7f,
            MaxLaunchSpeed = 15f,
            BirdRadius = 0.26f,
            GroundY = -5.2f,
            SlingX = -9.4f,

            // The sling stands well above the structure's base — like the hill it stands on in the
            // game this is modelled on, and for the same mechanical reason: a flat shot has to be able
            // to arrive *low*, at the base of a tower. With the sling a metre above the ground every
            // flat shot hit the floor before reaching the structure, so the only shots that connected
            // were lofted ones that arrived at roof height, and a pig sitting in a ground-floor
            // chamber could not be touched by anything.
            SlingHeight = 2.6f,
            IceThreshold = 3.5f,
            WoodThreshold = 5.5f,
            StoneThreshold = 9.5f,
            Step = 1f / 120f,
            MaxFlightSeconds = 7f,
            CrushDistance = 0.55f,
            MinImpactSpeed = 2.2f,
            GroundFriction = 0.55f
        };
    }

    /// <summary>Something that happened during a shot, for the view to replay.</summary>
    public enum BirdEventKind { BlockBroken, BlockFell, PigKilled, BirdStopped }

    public struct BirdEvent
    {
        public BirdEventKind Kind;
        public int Index;
        public float Time;
        public Vector2 At;
    }

    /// <summary>What a shot did, with the path to animate it.</summary>
    public sealed class BirdShotResult
    {
        public List<Vector2> Path = new List<Vector2>();
        public List<BirdEvent> Events = new List<BirdEvent>();
        public int PigsKilled;
        public int BlocksBroken;
        public float EndTime;
        public bool Cleared;
    }

    /// <summary>
    /// 愤怒的小鸟, as arithmetic.
    ///
    /// There is no physics engine here and that is the point. A level that "must be solvable" needs a
    /// simulation that the generator, the checker and the game all share — one that runs the same way
    /// every time, at a fixed step, with no engine deciding anything. So the whole shot is a pure
    /// function: give it a level and a launch velocity, and it returns the path and everything that
    /// broke, in order. The view replays that list; the generator uses the same function to prove a
    /// level can be cleared before the player ever sees it.
    ///
    /// The model is deliberately simple — one bird, a couple of impacts, blocks that fall when what
    /// held them up is gone, and drops that crush what they land on — because a simpler model is a
    /// model whose verdict the player can trust.
    /// </summary>
    public static class BirdRules
    {
        /// <summary>Damage one impact does to a block of this material.</summary>
        public static int DamageFor(float speed, BlockKind kind, BirdSettings settings)
        {
            float threshold;
            switch (kind)
            {
                case BlockKind.Ice: threshold = settings.IceThreshold; break;
                case BlockKind.Stone: threshold = settings.StoneThreshold; break;
                default: threshold = settings.WoodThreshold; break;
            }

            if (speed < threshold) return 0;
            return Mathf.Max(1, Mathf.FloorToInt(speed / threshold));
        }

        /// <summary>Health a block of this material starts with.</summary>
        public static int HealthFor(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Ice: return 1;
                case BlockKind.Stone: return 4;
                default: return 2;
            }
        }

        /// <summary>Points for breaking a block.</summary>
        public static int ScoreFor(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Ice: return 2;
                case BlockKind.Stone: return 8;
                default: return 4;
            }
        }

        /// <summary>Points for a pig. Extra birds left are worth more, which rewards a good shot.</summary>
        public const int PigScore = 40;

        /// <summary>Coins a cleared level is worth, plus what is left over.</summary>
        public static int CoinsFor(BirdLevel level, int birdsLeft, int score)
        {
            if (level == null) return 0;
            return Mathf.Max(0, score / 10) + Mathf.Max(0, birdsLeft) * 5 + 10;
        }

        /// <summary>What the result panel says.</summary>
        public static string RankFor(int birdsLeft, int score)
        {
            if (birdsLeft >= 3) return "一鸟定乾坤";
            if (birdsLeft == 2) return "省着用";
            if (birdsLeft == 1) return "刚刚好";
            return score >= 300 ? "砸得漂亮" : "过关了";
        }

        // ------------------------------------------------------------------ the slingshot

        /// <summary>
        /// The launch velocity for a drag from the slingshot to <paramref name="dragWorld"/>.
        ///
        /// Pull back and the bird flies the other way — the drag *is* the aim, so the sign is
        /// negated here and nowhere else. The pull is clamped to the sling's reach, which is what
        /// stops a long drag from becoming an unwinnable super-shot.
        /// </summary>
        public static Vector2 LaunchVelocity(Vector2 sling, Vector2 dragWorld, BirdSettings settings)
        {
            Vector2 pull = sling - dragWorld;
            float length = pull.magnitude;
            if (length > settings.MaxPull) pull = pull * (settings.MaxPull / length);

            float t = Mathf.Clamp01(pull.magnitude / Mathf.Max(0.01f, settings.MaxPull));
            float speed = Mathf.Lerp(settings.MinLaunchSpeed, settings.MaxLaunchSpeed, t);
            if (pull.sqrMagnitude <= 0.0001f) return new Vector2(settings.MinLaunchSpeed, 0f);

            return pull.normalized * speed;
        }

        /// <summary>Whether a drag is far enough from the sling to count as a shot.</summary>
        public static bool CanLaunch(Vector2 sling, Vector2 dragWorld, BirdSettings settings)
            => Vector2.Distance(sling, dragWorld) >= settings.MaxPull * 0.18f;

        /// <summary>
        /// How far up the slingshot can throw a bird, in world units above its own height.
        ///
        /// Straight up, minus the bird's own radius: the top of the tallest tower has to be under
        /// this, or the level contains a pig nobody can reach.
        /// </summary>
        public static float MaxRise(BirdSettings settings)
            => settings.MaxLaunchSpeed * settings.MaxLaunchSpeed / (2f * Mathf.Max(0.01f, settings.Gravity));

        /// <summary>
        /// How far to the right a bird can reach a target this far above (or below) the sling.
        ///
        /// The envelope of a projectile at the best angle for the height difference — the honest
        /// version of "can the slingshot reach that?". The generator keeps every block inside it, so
        /// a level cannot contain a pig that is simply out of range.
        /// </summary>
        public static float MaxRange(BirdSettings settings, float heightAboveSling)
        {
            float v = settings.MaxLaunchSpeed;
            float g = Mathf.Max(0.01f, settings.Gravity);

            // Best angle for a target at this height: 45 degrees plus half the elevation angle.
            float ratio = Mathf.Clamp(heightAboveSling / Mathf.Max(0.01f, v * v / g), -0.95f, 0.95f);
            float angle = 0.5f * Mathf.Asin(ratio) + Mathf.PI * 0.25f;

            float vx = v * Mathf.Cos(angle);
            float vy = v * Mathf.Sin(angle);
            float discriminant = vy * vy - 2f * g * heightAboveSling;
            if (discriminant <= 0f) return 0f;

            float time = (vy + Mathf.Sqrt(discriminant)) / g;
            return vx * time;
        }

        /// <summary>Whether a point is inside the slingshot's reach.</summary>
        public static bool InRange(Vector2 point, BirdSettings settings)
        {
            var sling = new Vector2(settings.SlingX, settings.GroundY + settings.SlingHeight);
            float up = point.y - sling.y;

            // Nothing above the sling's own ceiling is reachable, whatever the angle.
            if (up > MaxRise(settings)) return false;

            // Behind or above the sling: the only question is that ceiling.
            if (point.x <= sling.x) return true;

            float range = MaxRange(settings, up);
            return range > 0.01f && point.x - sling.x <= range + 0.01f;
        }

        // ------------------------------------------------------------------ simulation

        private class Sim
        {
            public BirdLevel Level;
            public BirdSettings Settings;
            public Vector2 Position;
            public Vector2 Velocity;
            public int Impacts;
            public float Time;
            public bool Stopped;
            public BirdShotResult Result = new BirdShotResult();
            public List<int> Falling = new List<int>();
        }

        /// <summary>
        /// Runs a shot to completion and returns what it did.
        ///
        /// <paramref name="applyToLevel"/> decides whether the level is left wrecked (the game) or
        /// untouched (the generator trying twenty candidate shots on the same level).
        /// </summary>
        public static BirdShotResult Simulate(BirdLevel level, Vector2 velocity, BirdSettings settings,
            bool applyToLevel = true)
        {
            var target = applyToLevel ? level : level.Clone();
            var sim = new Sim
            {
                Level = target,
                Settings = settings,
                Position = new Vector2(target.SlingX, target.SlingY),
                Velocity = velocity
            };

            sim.Result.Path.Add(sim.Position);

            int maxSteps = Mathf.CeilToInt(settings.MaxFlightSeconds / Mathf.Max(0.001f, settings.Step));
            for (int step = 0; step < maxSteps; step++)
            {
                sim.Time += settings.Step;
                Step(sim);
                sim.Result.Path.Add(sim.Position);

                if (sim.Stopped && sim.Falling.Count == 0) break;
            }

            sim.Result.EndTime = sim.Time;
            sim.Result.Cleared = target.Cleared;

            if (!applyToLevel)
            {
                // The caller only wants the verdict, not the wrecks; the clone goes out of scope.
                return sim.Result;
            }

            return sim.Result;
        }

        /// <summary>
        /// Lets a level settle: anything that has lost its support falls, and lands.
        ///
        /// The simulation calls this by itself whenever a block breaks, but it is exposed because
        /// "what holds what up" is a rule the tests need to be able to ask about directly — and
        /// because a level whose blocks were removed by something other than a shot (a hand-made
        /// layout, a future editor) must not be left with a tower floating in the air.
        /// </summary>
        public static void Settle(BirdLevel level, BirdSettings settings)
        {
            if (level == null) return;

            var sim = new Sim
            {
                Level = level,
                Settings = settings,
                Stopped = true,
                Position = new Vector2(level.SlingX, level.SlingY)
            };

            // Everything that is already gone is a hole in the structure, so every support is
            // recomputed from scratch rather than from the last break.
            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (!level.Blocks[i].Alive) LoseSupport(sim, i);
            }

            int guard = 0;
            while (sim.Falling.Count > 0 && guard++ < 4000)
            {
                sim.Time += settings.Step;
                StepFalling(sim);
            }
        }

        private static void Step(Sim sim)
        {
            var settings = sim.Settings;
            float dt = settings.Step;

            if (!sim.Stopped)
            {
                sim.Velocity.y -= settings.Gravity * dt;
                sim.Position += sim.Velocity * dt;

                // Hitting the ground slows the bird down; it does not end the shot.
                //
                // This is the difference between a slingshot game and a shooting gallery: a bird that
                // dies where it lands can never reach the base of a tower — every low shot falls short
                // and every shot that connects arrives at roof height, so a pig in a ground-floor
                // chamber is simply unhittable. Letting the bird skip and roll along the ground is
                // both what the game this is modelled on does and what makes the low shot a real
                // option. (The first version stopped the bird on contact, and the symptom was that no
                // candidate shot in the whole solver grid could kill anything.)
                float ground = sim.Level.GroundY + settings.BirdRadius;
                if (sim.Position.y <= ground)
                {
                    sim.Position.y = ground;
                    if (sim.Velocity.y < 0f) sim.Velocity.y = -sim.Velocity.y * 0.22f;

                    // Friction is per *second*, not per frame: the first version multiplied by 0.88
                    // every step, which at 120 steps a second is a brick wall — the bird stopped
                    // within a couple of frames of touching the ground, so "roll into the base" was
                    // not actually possible and every low shot still died where it landed.
                    sim.Velocity.x *= Mathf.Pow(settings.GroundFriction, dt);

                    if (Mathf.Abs(sim.Velocity.x) < 0.8f && Mathf.Abs(sim.Velocity.y) < 0.8f) Stop(sim);
                }

                if (!sim.Stopped) HitTest(sim);
            }

            StepFalling(sim);
        }

        private static void Stop(Sim sim)
        {
            if (sim.Stopped) return;
            sim.Stopped = true;
            sim.Result.Events.Add(new BirdEvent
            {
                Kind = BirdEventKind.BirdStopped,
                Time = sim.Time,
                At = sim.Position
            });
        }

        /// <summary>Bird against blocks and pigs, in one pass, taking the nearest hit.</summary>
        private static void HitTest(Sim sim)
        {
            var settings = sim.Settings;
            float speed = sim.Velocity.magnitude;
            if (speed < settings.MinImpactSpeed) return;

            var level = sim.Level;

            for (int i = 0; i < level.Pigs.Count; i++)
            {
                var pig = level.Pigs[i];
                if (!pig.Alive) continue;
                if (!CircleHitsCircle(sim.Position, settings.BirdRadius, new Vector2(pig.X, pig.Y), pig.Radius))
                    continue;

                KillPig(sim, i, new Vector2(pig.X, pig.Y));
                Bounce(sim);
                return;
            }

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                var block = level.Blocks[i];
                if (!block.Alive) continue;
                if (!CircleHitsBox(sim.Position, settings.BirdRadius, block)) continue;

                int damage = DamageFor(speed, block.Kind, settings);
                if (damage <= 0)
                {
                    Bounce(sim);
                    return;
                }

                var updated = level.Blocks[i];
                updated.Health -= damage;
                level.Blocks[i] = updated;

                if (updated.Health <= 0) BreakBlock(sim, i);
                Bounce(sim);
                return;
            }
        }

        /// <summary>
        /// The bird loses most of its speed on impact and keeps going a little.
        ///
        /// Two impacts are allowed before it drops: one-hit-per-bird is easier to reason about but
        /// makes a shot that clips a corner feel wasted, and the second impact is what lets a bird
        /// punch through ice into what is behind it.
        /// </summary>
        private static void Bounce(Sim sim)
        {
            sim.Impacts++;
            sim.Velocity = new Vector2(sim.Velocity.x * -0.22f, Mathf.Abs(sim.Velocity.y) * 0.28f);

            if (sim.Impacts >= 2)
            {
                sim.Velocity = new Vector2(sim.Velocity.x * 0.2f, Mathf.Min(sim.Velocity.y, 0f));
                Stop(sim);
            }
        }

        private static void BreakBlock(Sim sim, int index)
        {
            var level = sim.Level;
            var block = level.Blocks[index];
            block.Alive = false;
            level.Blocks[index] = block;

            sim.Result.BlocksBroken++;
            sim.Result.Events.Add(new BirdEvent
            {
                Kind = BirdEventKind.BlockBroken,
                Index = index,
                Time = sim.Time,
                At = new Vector2(block.X, block.Y)
            });

            LoseSupport(sim, index);
        }

        /// <summary>
        /// Everything that has lost its balance starts to fall.
        ///
        /// Support is not "resting on something" — it is **balance**. A beam lying across two
        /// uprights is held up by both; take one away and the beam's own weight is no longer over
        /// anything, so it tips and comes down. That distinction is the whole game: the first version
        /// asked "is any support left?", so a beam on two legs survived losing one, the pig inside the
        /// chamber could not be reached by any shot (the bird does not fit through the gap), and most
        /// generated towers turned out to be unwinnable — which the level generator then quietly
        /// replaced with a trivial fallback level, killing the variety it was supposed to provide.
        /// </summary>
        private static void LoseSupport(Sim sim, int removed)
        {
            var level = sim.Level;
            var queue = new Queue<int>();
            queue.Enqueue(removed);

            while (queue.Count > 0)
            {
                int gone = queue.Dequeue();

                for (int i = 0; i < level.Blocks.Count; i++)
                {
                    var candidate = level.Blocks[i];
                    if (!candidate.Alive || candidate.Falling) continue;
                    if (!WasRestingOn(level, candidate, gone)) continue;
                    if (IsBalanced(level, candidate, gone)) continue;

                    candidate.Falling = true;
                    level.Blocks[i] = candidate;
                    sim.Falling.Add(i);

                    sim.Result.Events.Add(new BirdEvent
                    {
                        Kind = BirdEventKind.BlockFell,
                        Index = i,
                        Time = sim.Time,
                        At = new Vector2(candidate.X, candidate.Y)
                    });

                    queue.Enqueue(i);
                }
            }
        }

        /// <summary>Whether this block was one of the things holding that one up.</summary>
        private static bool WasRestingOn(BirdLevel level, BirdBlock block, int supportIndex)
        {
            if (supportIndex < 0 || supportIndex >= level.Blocks.Count) return false;

            // Deliberately *not* checking whether the support is still alive: this is asked about the
            // block that just died, and its geometry is exactly what mattered. The first version
            // required `support.Alive` here, so the question "was the beam resting on the upright I
            // just broke?" was always answered "no" — the beam never fell, nothing was ever crushed,
            // and most generated levels turned out to be unwinnable for a reason no screenshot could
            // show.
            var support = level.Blocks[supportIndex];

            const float tolerance = 0.14f;
            if (Mathf.Abs(support.Top - block.Bottom) > tolerance) return false;

            float overlap = Mathf.Min(support.Right, block.Right) - Mathf.Max(support.Left, block.Left);
            return overlap >= Mathf.Min(block.HalfW, 0.5f) * 0.4f;
        }

        /// <summary>
        /// Whether the block's weight is over what is still holding it.
        ///
        /// The rule is a hull rather than a vote: the block's centre must lie within the horizontal
        /// span of *all* the blocks touching its underside. Across two uprights the span covers the
        /// middle and the beam stands; with one upright gone the span is off to one side and it tips.
        /// A block on the ground is always balanced.
        /// </summary>
        private static bool IsBalanced(BirdLevel level, BirdBlock block, int ignoreIndex)
        {
            const float tolerance = 0.14f;

            if (block.Bottom <= level.GroundY + tolerance) return true;

            float left = float.MaxValue, right = float.MinValue;
            bool any = false;

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (i == ignoreIndex) continue;
                var other = level.Blocks[i];
                if (!other.Alive || other.Falling) continue;

                if (Mathf.Abs(other.Top - block.Bottom) > tolerance) continue;

                float overlap = Mathf.Min(other.Right, block.Right) - Mathf.Max(other.Left, block.Left);
                if (overlap < Mathf.Min(block.HalfW, 0.5f) * 0.4f) continue;

                left = Mathf.Min(left, other.Left);
                right = Mathf.Max(right, other.Right);
                any = true;
            }

            if (!any) return false;

            // A little slack, so a block centred a hair outside its supports does not tip for a
            // reason the player cannot see.
            return block.X >= left - 0.06f && block.X <= right + 0.06f;
        }

        /// <summary>Falling blocks drop until they land, crushing and damaging on the way.</summary>
        private static void StepFalling(Sim sim)
        {
            if (sim.Falling.Count == 0) return;

            var level = sim.Level;
            float dt = sim.Settings.Step;

            for (int f = sim.Falling.Count - 1; f >= 0; f--)
            {
                int index = sim.Falling[f];
                var block = level.Blocks[index];
                if (!block.Alive)
                {
                    sim.Falling.RemoveAt(f);
                    continue;
                }

                float target = LandingHeight(sim, index);
                float step = Mathf.Max(1.5f, 6f) * dt;
                float next = block.Y - step;

                if (next <= target)
                {
                    // Landed: the distance it covered is what decides whether it hurt what it hit.
                    float fell = block.FallDistance + (block.Y - target);
                    block.Y = target;
                    block.Falling = false;
                    block.FallDistance = 0f;
                    level.Blocks[index] = block;
                    sim.Falling.RemoveAt(f);

                    Land(sim, index, fell);
                    continue;
                }

                block.Y = next;
                block.FallDistance += step;
                level.Blocks[index] = block;

                // Anything a falling block passes through on the way down is hit.
                Crush(sim, index);
            }
        }

        /// <summary>
        /// How far a falling block can drop: the ground, or the top of something it would be balanced
        /// on.
        ///
        /// The balance rule matters here too, and leaving it out was a real bug: a beam tipping off
        /// the single leg left under it found that same leg as its landing place — it "fell" from one
        /// height to the identical height and never came down, so nothing was ever crushed by a
        /// collapse. A block comes to rest where it would stand, not wherever it happens to touch.
        /// </summary>
        private static float LandingHeight(Sim sim, int index)
        {
            var level = sim.Level;
            var block = level.Blocks[index];
            float rest = level.GroundY + block.HalfH;

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (i == index) continue;
                var other = level.Blocks[i];
                if (!other.Alive) continue;
                if (other.Top > block.Bottom + 0.05f) continue;

                if (block.X < other.Left - 0.06f || block.X > other.Right + 0.06f) continue;

                rest = Mathf.Max(rest, other.Top + block.HalfH);
            }

            return rest;
        }

        /// <summary>Pigs under a falling block are killed; blocks under it are damaged by the drop.</summary>
        private static void Crush(Sim sim, int index)
        {
            var level = sim.Level;
            var block = level.Blocks[index];

            for (int p = 0; p < level.Pigs.Count; p++)
            {
                var pig = level.Pigs[p];
                if (!pig.Alive) continue;

                // Two separation tests: apart on x, or apart on y, means no overlap. The first version
                // of this compared the pig to the block's *own* edges rather than to the opposite
                // ones, so nothing ever overlapped and a block falling straight onto a pig passed
                // through it — which made most levels unwinnable for a reason nothing on screen
                // explained.
                if (pig.X + pig.Radius < block.Left || pig.X - pig.Radius > block.Right) continue;
                if (pig.Y + pig.Radius < block.Bottom || pig.Y - pig.Radius > block.Top) continue;

                KillPig(sim, p, new Vector2(pig.X, pig.Y));
            }
        }

        /// <summary>A block has landed: what is under it takes the hit.</summary>
        private static void Land(Sim sim, int index, float drop)
        {
            if (drop < sim.Settings.CrushDistance) return;

            int damage = Mathf.Max(1, Mathf.FloorToInt(drop / sim.Settings.CrushDistance));
            var level = sim.Level;
            var landed = level.Blocks[index];

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (i == index) continue;
                var other = level.Blocks[i];
                if (!other.Alive || other.Falling) continue;
                if (Mathf.Abs(other.Top - landed.Bottom) > 0.12f) continue;

                float overlap = Mathf.Min(other.Right, landed.Right) - Mathf.Max(other.Left, landed.Left);
                if (overlap < Mathf.Min(other.HalfW, 0.4f) * 0.5f) continue;

                var updated = level.Blocks[i];
                updated.Health -= damage;
                level.Blocks[i] = updated;
                if (updated.Health <= 0) BreakBlock(sim, i);
                return;
            }
        }

        private static void KillPig(Sim sim, int index, Vector2 at)
        {
            var pig = sim.Level.Pigs[index];
            if (!pig.Alive) return;

            pig.Alive = false;
            sim.Level.Pigs[index] = pig;
            sim.Result.PigsKilled++;

            sim.Result.Events.Add(new BirdEvent
            {
                Kind = BirdEventKind.PigKilled,
                Index = index,
                Time = sim.Time,
                At = at
            });
        }

        // ------------------------------------------------------------------ geometry

        public static bool CircleHitsCircle(Vector2 centre, float radius, Vector2 other, float otherRadius)
            => (centre - other).sqrMagnitude <= (radius + otherRadius) * (radius + otherRadius);

        /// <summary>Circle against an axis-aligned box — the only collision shape the game needs.</summary>
        public static bool CircleHitsBox(Vector2 centre, float radius, BirdBlock box)
        {
            float closestX = Mathf.Clamp(centre.x, box.Left, box.Right);
            float closestY = Mathf.Clamp(centre.y, box.Bottom, box.Top);
            float dx = centre.x - closestX;
            float dy = centre.y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>Whether a point is inside a box, for hit-testing a tap.</summary>
        public static bool PointInBox(Vector2 point, BirdBlock box)
            => point.x >= box.Left && point.x <= box.Right && point.y >= box.Bottom && point.y <= box.Top;
    }
}
