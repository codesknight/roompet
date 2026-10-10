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

        /// <summary>Rotation in radians. Zero until something hits it.</summary>
        public float Angle;

        /// <summary>Linear velocity, written by the simulation so the view can read the state back.</summary>
        public float VX;
        public float VY;

        /// <summary>Angular velocity, radians per second.</summary>
        public float Spin;

        /// <summary>
        /// False while the block is asleep.
        ///
        /// This is the whole reason a tower can stand up in a hand-written physics solver: a stack of
        /// boxes in contact is never *exactly* balanced, and a solver that integrates it from the first
        /// frame will have it creep and jitter apart before the player has even pulled the sling. A
        /// block that has come to rest and been undisturbed stops being simulated entirely, and starts
        /// again the moment anything touches it (or the structure under it is broken).
        /// </summary>
        public bool Awake;

        /// <summary>How long it has been still, for the sleep rule.</summary>
        public float RestTimer;

        public Vector2 Centre => new Vector2(X, Y);

        /// <summary>Half-extent of the box's world-space bounding box, which is what rotation changes.</summary>
        public float ExtentX => Mathf.Abs(HalfW * Mathf.Cos(Angle)) + Mathf.Abs(HalfH * Mathf.Sin(Angle));
        public float ExtentY => Mathf.Abs(HalfW * Mathf.Sin(Angle)) + Mathf.Abs(HalfH * Mathf.Cos(Angle));

        public float Bottom => Y - ExtentY;
        public float Top => Y + ExtentY;
        public float Left => X - ExtentX;
        public float Right => X + ExtentX;
    }

    /// <summary>One pig: a circle with its own little bit of physics.</summary>
    public struct BirdPig
    {
        public float X;
        public float Y;
        public float Radius;
        public bool Alive;

        public float VX;
        public float VY;
        public bool Awake;
        public float RestTimer;

        /// <summary>How long something heavy has been sitting on it, for the squash rule.</summary>
        public float SquashTimer;

        public Vector2 Centre => new Vector2(X, Y);
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
            return new BirdLevel
            {
                Stage = Stage,
                Birds = Birds,
                GroundY = GroundY,
                SlingX = SlingX,
                SlingY = SlingY,
                Blocks = new List<BirdBlock>(Blocks),
                Pigs = new List<BirdPig>(Pigs)
            };
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

        /// <summary>Impact speed below which the bird just bumps and keeps going.</summary>
        public float MinImpactSpeed;

        /// <summary>
        /// How much horizontal speed survives a second of rolling along the ground.
        ///
        /// Applied as a rate rather than a per-step multiplier, because a per-step 0.88 at 120 steps a
        /// second is a brick wall and the bird stopped the instant it landed.
        /// </summary>
        public float GroundFriction;

        // ---- the rigid-body solver ----

        /// <summary>Velocity kept per second (0.2 = lose 20% a second) and the same for spin.</summary>
        public float LinearDamping;
        public float AngularDamping;

        /// <summary>How bouncy a block-block contact is. Wood on wood barely bounces.</summary>
        public float Restitution;

        /// <summary>Coulomb friction for block contacts.</summary>
        public float Friction;

        /// <summary>
        /// Extra friction for a contact that is barely sliding — static friction, in effect.
        ///
        /// This is what actually holds a tower still. The residual velocities a hand-written solver
        /// leaves behind are *tangential* (bodies creeping sideways a few centimetres a second), and the
        /// fix for that is friction rather than damping: damping the normal direction as well is how the
        /// first attempt froze a beam that had lost its support, because a topple is a *normal* motion
        /// the damping was quietly cancelling every step.
        /// </summary>
        public float StaticFriction;

        /// <summary>Sliding speed below which the static friction applies.</summary>
        public float StaticSpeed;

        /// <summary>Velocity-solver iterations per step.</summary>
        public int SolverIterations;

        /// <summary>
        /// Speed below which a body is treated as *resting* and damped hard towards zero.
        ///
        /// This is the difference between a tower and a pile. A hand-written impulse solver never quite
        /// cancels gravity on a stack of boxes: a few centimetres per second survive the solve every
        /// step, and over the couple of thousand steps of a level's life that residue walks the whole
        /// structure sideways — measured at 0.4 to 0.7 units before this existed, which is a tower
        /// that quietly collapses on its own with the player watching. A real engine gets this from
        /// warm starting plus sleeping; this gets it from saying "if you are barely moving, you are
        /// not moving". Nothing that is actually falling is affected: a toppling block moves at metres
        /// per second, not centimetres.
        /// </summary>
        public float RestSpeed;

        /// <summary>How much of the velocity survives one step while resting.</summary>
        public float RestDamping;

        /// <summary>
        /// Spin below which a resting body's rotation is damped too.
        ///
        /// Deliberately much smaller than <see cref="SleepSpin"/>. A beam sitting on two uprights picks
        /// up a few hundredths of a radian per second of numerical spin, and over the couple of thousand
        /// steps of a level's life that is enough to walk it off its supports — so it has to be damped.
        /// But a beam that has *lost* a support starts rotating from zero and is past this threshold
        /// within a couple of steps, so the same damping cannot freeze the collapse. The gap between the
        /// two numbers is what makes both of those true at once.
        /// </summary>
        public float RestSpin;

        /// <summary>Speed at which one body wakes another that was asleep. Below it, contact is resting.</summary>
        public float WakeSpeed;

        /// <summary>
        /// How hard an unsupported block is nudged downwards to start it falling, and how much tipping
        /// spin it is given when its weight is hanging over the edge of what holds it up.
        ///
        /// Both are above the resting damping's speed threshold, which is the entire point: they are
        /// what the damping cannot swallow.
        /// </summary>
        public float FallNudge;
        public float TipNudge;

        /// <summary>Speed and spin below which a body counts as still, and how long that has to hold.</summary>
        public float SleepSpeed;
        public float SleepSpin;
        public float SleepSeconds;

        /// <summary>Hard clamp on how fast a block may move, so one bad contact cannot fling the level.</summary>
        public float MaxBlockSpeed;

        /// <summary>
        /// Impact speed at which a pig dies.
        ///
        /// This is what makes a collapse worth aiming at: knock the floor out from under a pig and the
        /// fall finishes it, exactly as in the game this is modelled on.
        /// </summary>
        public float PigCrushSpeed;

        /// <summary>How heavy a block has to be to squash a pig it comes to rest on.</summary>
        public float PigSquashMass;

        /// <summary>How long a heavy block has to sit on a pig before the pig gives up.</summary>
        public float PigSquashSeconds;

        /// <summary>Speed at which a block hitting the ground or another block takes damage.</summary>
        public float GroundBreakSpeed;

        /// <summary>How much of the bird's momentum goes into the block it hits.</summary>
        public float BirdPush;

        /// <summary>How often the recorded replay is sampled, in frames per second.</summary>
        public float ReplayRate;

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
            // to arrive *low*, at the base of a tower.
            SlingHeight = 2.6f,
            IceThreshold = 3.5f,
            WoodThreshold = 5.5f,
            StoneThreshold = 9.5f,
            Step = 1f / 120f,

            // Shorter than the old 7 seconds, and not for taste: with real bodies the structure has
            // fallen and settled within a couple of seconds of the impact, and every extra second is
            // 120 steps the level generator has to pay for on every one of its candidate shots.
            MaxFlightSeconds = 3.4f,
            MinImpactSpeed = 2.2f,
            GroundFriction = 0.55f,

            LinearDamping = 0.22f,
            AngularDamping = 0.7f,
            Restitution = 0f,
            Friction = 0.58f,
            StaticFriction = 1.15f,
            StaticSpeed = 0.30f,
            SolverIterations = 12,
            RestSpeed = 0.45f,
            RestSpin = 0.12f,
            RestDamping = 0.45f,
            WakeSpeed = 0.9f,
            FallNudge = 1.1f,
            TipNudge = 1.2f,
            SleepSpeed = 0.14f,
            SleepSpin = 0.30f,
            SleepSeconds = 0.45f,
            MaxBlockSpeed = 16f,
            PigCrushSpeed = 4.6f,
            PigSquashMass = 1.1f,
            PigSquashSeconds = 0.35f,
            GroundBreakSpeed = 4.2f,
            BirdPush = 0.85f,
            ReplayRate = 60f
        };
    }

    /// <summary>Something that happened during a shot, for the view to react to.</summary>
    public enum BirdEventKind { BlockBroken, PigKilled, BirdStopped }

    public struct BirdEvent
    {
        public BirdEventKind Kind;
        public int Index;
        public float Time;
        public Vector2 At;
    }

    /// <summary>
    /// One recorded instant of a shot: where the bird was, and where every block and pig was.
    ///
    /// The view replays these instead of re-deriving anything, which is what keeps "the animation" and
    /// "the outcome the generator verified" the same thing. Recording is off for the solver (it only
    /// wants the verdict) and on for the game.
    /// </summary>
    public struct BirdFrame
    {
        public float Time;
        public Vector2 Bird;

        /// <summary>Centre of every block, index-aligned with <see cref="BirdLevel.Blocks"/>.</summary>
        public Vector2[] Blocks;
        public float[] Angles;
        public bool[] Alive;

        /// <summary>Centre of every pig, index-aligned with <see cref="BirdLevel.Pigs"/>.</summary>
        public Vector2[] Pigs;
        public bool[] PigAlive;
    }

    /// <summary>What a shot did, with the path to animate it.</summary>
    public sealed class BirdShotResult
    {
        public List<Vector2> Path = new List<Vector2>();
        public List<BirdEvent> Events = new List<BirdEvent>();

        /// <summary>Sampled replay, empty unless the caller asked for it.</summary>
        public List<BirdFrame> Frames = new List<BirdFrame>();

        public int PigsKilled;
        public int BlocksBroken;
        public float EndTime;
        public bool Cleared;
    }

    /// <summary>
    /// 愤怒的小鸟, as arithmetic — now with blocks that actually fall over.
    ///
    /// The rules are still the only place a shot is decided: give the simulation a level and a launch
    /// velocity and it returns the replay and everything that broke, in order. The generator proves a
    /// level clearable with that same function before the player ever sees it, so what is on screen is
    /// what was verified. What changed in round 21 is *what the simulation is*: it used to be a
    /// hand-rolled approximation in which a block either stood still or dropped straight down, and the
    /// player asked, reasonably, for wood, stone and ice that behave like objects.
    ///
    /// So it is a small rigid-body solver now: boxes with mass and rotation, impulses with friction at
    /// the contact points, pigs that get knocked off their perch and die from the fall, and a sleep
    /// rule so that an untouched tower stands perfectly still. It is still deterministic, still a pure
    /// function of (level, velocity), and still the thing the generator uses to prove the level is
    /// winnable — which is the property worth protecting. A physics engine with a *nondeterministic*
    /// verdict would have made "this level can be cleared" unprovable.
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

        /// <summary>Mass per unit area. Ice is light, stone is heavy — which is the whole difference.</summary>
        public static float DensityFor(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Ice: return 0.30f;
                case BlockKind.Stone: return 1.05f;
                default: return 0.48f;
            }
        }

        /// <summary>The mass of a block: what decides whether it crushes a pig and how it topples.</summary>
        public static float MassFor(BirdBlock block)
            => Mathf.Max(0.02f, 4f * block.HalfW * block.HalfH * DensityFor(block.Kind));

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
            => Vector2.Distance(sling, dragWorld) >= settings.MaxPull * MinPullFraction;

        /// <summary>
        /// How far the drag has to travel before the sling will fire at all, as a fraction of its reach.
        ///
        /// Exposed because it decides the *slowest shot a player can make*: the launch speed is
        /// interpolated from the pull length, so a drag shorter than this never fires and the weakest
        /// shot in the game is not <see cref="BirdSettings.MinLaunchSpeed"/> but the speed this fraction
        /// produces. The generator's candidate grid used to start at the former, which meant a level
        /// could be "proved" with a shot no player could reproduce.
        /// </summary>
        public const float MinPullFraction = 0.18f;

        /// <summary>The slowest shot a drag can actually produce.</summary>
        public static float SlowestLaunchSpeed(BirdSettings settings)
            => Mathf.Lerp(settings.MinLaunchSpeed, settings.MaxLaunchSpeed, MinPullFraction);

        /// <summary>Whether a launch velocity is one a player can actually produce with a drag.</summary>
        public static bool IsLaunchReachable(Vector2 velocity, BirdSettings settings)
        {
            float speed = velocity.magnitude;
            return speed >= SlowestLaunchSpeed(settings) - 0.01f
                   && speed <= settings.MaxLaunchSpeed + 0.01f;
        }

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

        /// <summary>
        /// Where a bird would fly with nothing in the way: the dotted line the HUD draws while aiming.
        ///
        /// Pure, and the *same integrator* the simulation uses, so the preview cannot promise a path
        /// the bird will not take — as far as gravity is concerned. It stops at the ground and does not
        /// know about blocks, which is honest: the preview is for judging the arc, not for solving the
        /// level for the player.
        /// </summary>
        public static void PreviewArc(Vector2 sling, Vector2 velocity, BirdSettings settings,
            List<Vector2> into, float seconds = 2.4f, float spacing = 0.05f)
        {
            if (into == null) return;
            into.Clear();

            float dt = Mathf.Max(0.005f, settings.Step);
            int maxSteps = Mathf.CeilToInt(Mathf.Max(0.1f, seconds) / dt);
            int every = Mathf.Max(1, Mathf.RoundToInt(spacing / dt));

            var position = sling;
            var speed = velocity;
            float floor = settings.GroundY + settings.BirdRadius;

            for (int step = 0; step <= maxSteps; step++)
            {
                if (step % every == 0) into.Add(position);

                speed.y -= settings.Gravity * dt;
                position += speed * dt;
                if (position.y <= floor) break;
            }
        }

        // ------------------------------------------------------------------ simulation

        private const int KindStatic = 0;
        private const int KindBlock = 1;
        private const int KindPig = 2;

        private struct BodyRef
        {
            public int Kind;
            public int Index;

            public static BodyRef Ground => new BodyRef { Kind = KindStatic, Index = -1 };
        }

        private struct Contact
        {
            public BodyRef A;
            public BodyRef B;
            public Vector2 Point;
            public Vector2 Normal;      // from A to B
            public float Depth;
            public float NormalImpulse;
            public float TangentImpulse;
        }

        private class Sim
        {
            public BirdSettings S;
            public BirdLevel Level;
            public SimBlock[] Blocks;
            public SimPig[] Pigs;
            public Vector2 BirdPosition;
            public Vector2 BirdVelocity;
            public int Impacts;
            public bool Stopped;
            public float Time;
            public BirdShotResult Result = new BirdShotResult();
            public readonly List<Contact> Contacts = new List<Contact>(64);
            public float NextFrame;
            public bool Record;
            public float Floor;
        }

        private class SimBlock
        {
            public int Index;
            public Vector2 P;
            public float A;
            public Vector2 V;
            public float W;
            public float HalfW;
            public float HalfH;
            public float InvMass;
            public float InvInertia;
            public bool Awake;
            public float Rest;
            public bool Alive;

            /// <summary>Fastest it has moved since the last contact resolution, for the break rule.</summary>
            public float ImpactSpeed;

            /// <summary>Whether anything was touching it at the end of the last step.</summary>
            public bool Supported;
        }

        private class SimPig
        {
            public int Index;
            public Vector2 P;
            public Vector2 V;
            public float R;
            public float InvMass;
            public bool Awake;
            public float Rest;
            public float Squash;
            public bool Alive;

            /// <summary>Fastest it has moved since the last contact resolution, for the crush rule.</summary>
            public float ImpactSpeed;

            /// <summary>Whether anything was touching it at the end of the last step.</summary>
            public bool Supported;
        }

        /// <summary>
        /// Runs a shot to completion and returns what it did.
        ///
        /// <paramref name="applyToLevel"/> decides whether the level is left wrecked (the game) or
        /// untouched (the generator trying sixty-five candidate shots on the same level), and
        /// <paramref name="record"/> decides whether the full replay is sampled (the game) or only the
        /// verdict is kept (the generator, where sixty-five recordings would be pure garbage).
        /// </summary>
        public static BirdShotResult Simulate(BirdLevel level, Vector2 velocity, BirdSettings settings,
            bool applyToLevel = true, bool record = false)
        {
            if (level == null) return new BirdShotResult();

            var target = applyToLevel ? level : level.Clone();
            var sim = Build(target, settings, record);

            sim.BirdPosition = new Vector2(target.SlingX, target.SlingY);
            sim.BirdVelocity = velocity;
            sim.Result.Path.Add(sim.BirdPosition);
            RecordFrame(sim);

            int maxSteps = Mathf.CeilToInt(settings.MaxFlightSeconds / Mathf.Max(0.001f, settings.Step));
            for (int step = 0; step < maxSteps; step++)
            {
                Step(sim);
                sim.Result.Path.Add(sim.BirdPosition);

                if (sim.Stopped && AllAsleep(sim)) break;
            }

            Sync(sim);

            sim.Result.EndTime = sim.Time;
            sim.Result.Cleared = target.Cleared;
            return sim.Result;
        }

        /// <summary>
        /// Lets a level settle: everything is woken and simulated until it comes to rest.
        ///
        /// This is what the tests use to ask "if the support under this is gone, what happens?", and
        /// what any code that removes a block outside a shot (a hand-made layout, a future editor)
        /// should call so the level is not left with a tower hanging in the air.
        /// </summary>
        public static void Settle(BirdLevel level, BirdSettings settings)
        {
            if (level == null) return;

            var sim = Build(level, settings, false);
            sim.Stopped = true;

            // Everything wakes, because the question this answers is "given the level as it now is,
            // where does it end up?" — and a block that is asleep cannot notice that the thing under
            // it has been removed.
            WakeAll(sim);

            int guard = 0;
            int max = Mathf.CeilToInt(settings.MaxFlightSeconds / Mathf.Max(0.001f, settings.Step));
            while (!AllAsleep(sim) && guard++ < max * 4)
            {
                Step(sim);
            }

            Sync(sim);
        }

        private static Sim Build(BirdLevel level, BirdSettings settings, bool record)
        {
            var sim = new Sim
            {
                S = settings,
                Level = level,
                Floor = level.GroundY,
                Record = record,
                Blocks = new SimBlock[level.Blocks.Count],
                Pigs = new SimPig[level.Pigs.Count]
            };

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                var block = level.Blocks[i];
                float mass = MassFor(block);
                float inertia = mass * (block.HalfW * block.HalfW + block.HalfH * block.HalfH) / 3f;

                sim.Blocks[i] = new SimBlock
                {
                    Index = i,
                    P = new Vector2(block.X, block.Y),
                    A = block.Angle,
                    V = new Vector2(block.VX, block.VY),
                    W = block.Spin,
                    HalfW = block.HalfW,
                    HalfH = block.HalfH,
                    InvMass = mass > 0.0001f ? 1f / mass : 0f,
                    InvInertia = inertia > 0.0001f ? 1f / inertia : 0f,
                    Awake = block.Awake,
                    Rest = block.RestTimer,
                    Alive = block.Alive
                };
            }

            for (int i = 0; i < level.Pigs.Count; i++)
            {
                var pig = level.Pigs[i];
                float mass = Mathf.Max(0.05f, pig.Radius * pig.Radius * 6f);

                sim.Pigs[i] = new SimPig
                {
                    Index = i,
                    P = new Vector2(pig.X, pig.Y),
                    V = new Vector2(pig.VX, pig.VY),
                    R = pig.Radius,
                    InvMass = 1f / mass,
                    Awake = pig.Awake,
                    Rest = pig.RestTimer,
                    Squash = pig.SquashTimer,
                    Alive = pig.Alive
                };
            }

            return sim;
        }

        /// <summary>Writes the simulated bodies back into the level, which is the caller's view of it.</summary>
        private static void Sync(Sim sim)
        {
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var body = sim.Blocks[i];
                var block = sim.Level.Blocks[i];

                block.X = body.P.x;
                block.Y = body.P.y;
                block.Angle = body.A;
                block.VX = body.V.x;
                block.VY = body.V.y;
                block.Spin = body.W;
                block.Awake = body.Awake;
                block.RestTimer = body.Rest;
                block.Alive = body.Alive;
                sim.Level.Blocks[i] = block;
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                var body = sim.Pigs[i];
                var pig = sim.Level.Pigs[i];

                pig.X = body.P.x;
                pig.Y = body.P.y;
                pig.VX = body.V.x;
                pig.VY = body.V.y;
                pig.Awake = body.Awake;
                pig.RestTimer = body.Rest;
                pig.SquashTimer = body.Squash;
                pig.Alive = body.Alive;
                sim.Level.Pigs[i] = pig;
            }
        }

        private static bool AllAsleep(Sim sim)
        {
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                if (sim.Blocks[i].Alive && sim.Blocks[i].Awake) return false;
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                if (sim.Pigs[i].Alive && sim.Pigs[i].Awake) return false;
            }

            return true;
        }

        private static void WakeAll(Sim sim)
        {
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                if (!sim.Blocks[i].Alive) continue;
                sim.Blocks[i].Awake = true;
                sim.Blocks[i].Rest = 0f;
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                if (!sim.Pigs[i].Alive) continue;
                sim.Pigs[i].Awake = true;
                sim.Pigs[i].Rest = 0f;
            }
        }

        private static void Step(Sim sim)
        {
            float dt = sim.S.Step;
            sim.Time += dt;

            BirdStep(sim, dt);
            Integrate(sim, dt);
            BuildContacts(sim);
            NudgeUnsupported(sim);

            for (int iteration = 0; iteration < sim.S.SolverIterations; iteration++)
            {
                ResolveContacts(sim, dt);
            }

            CorrectPositions(sim);
            PigDamage(sim, dt);
            SleepBodies(sim, dt);

            RecordFrame(sim);
        }

        // ------------------------------------------------------------------ the bird

        private static void BirdStep(Sim sim, float dt)
        {
            if (sim.Stopped) return;

            var settings = sim.S;
            Vector2 prev = sim.BirdPosition;
            sim.BirdVelocity.y -= settings.Gravity * dt;
            sim.BirdPosition += sim.BirdVelocity * dt;

            float ground = sim.Floor + settings.BirdRadius;
            if (sim.BirdPosition.y <= ground)
            {
                sim.BirdPosition.y = ground;
                if (sim.BirdVelocity.y < 0f) sim.BirdVelocity.y = -sim.BirdVelocity.y * 0.22f;

                // Friction is per *second*, not per frame: a per-step multiplier at 120 steps a second
                // is a brick wall, and "roll into the base" then is not actually possible.
                sim.BirdVelocity.x *= Mathf.Pow(settings.GroundFriction, dt);

                if (Mathf.Abs(sim.BirdVelocity.x) < 0.8f && Mathf.Abs(sim.BirdVelocity.y) < 0.8f) Stop(sim);
            }

            if (!sim.Stopped) BirdHit(sim, prev);
        }

        private static void Stop(Sim sim)
        {
            if (sim.Stopped) return;
            sim.Stopped = true;
            sim.Result.Events.Add(new BirdEvent
            {
                Kind = BirdEventKind.BirdStopped,
                Time = sim.Time,
                At = sim.BirdPosition
            });
        }

        /// <summary>
        /// The bird against pigs and blocks, taking the nearest hit.
        ///
        /// The bird is not a rigid body: it is a projectile with two impacts in it, and the blocks are
        /// what move. That asymmetry is deliberate — a bird that tumbled down the tower would make the
        /// shot hard to read, and the game is about where the blocks end up.
        /// </summary>
        private static void BirdHit(Sim sim, Vector2 prev)
        {
            var settings = sim.S;
            float speed = sim.BirdVelocity.magnitude;
            bool hard = speed >= settings.MinImpactSpeed;

            // Pigs: swept against the bird's path this step. A slow bird still bounces off a pig
            // (it never tunnels through), but only a fast one kills it.
            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                var pig = sim.Pigs[i];
                if (!pig.Alive) continue;
                if (!SegmentHitsPoint(prev, sim.BirdPosition, settings.BirdRadius + pig.R, pig.P)) continue;

                if (hard) KillPig(sim, i, pig.P);
                Bounce(sim);
                return;
            }

            // Blocks: swept against the bird's path this step, for the same no-tunnel guarantee.
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive) continue;
                if (!SegmentHitsBox(prev, sim.BirdPosition, settings.BirdRadius, block)) continue;

                // The block gets pushed where the bird hit it, so a hit near a corner spins the block
                // and a hit in the middle shoves it — which is what makes one tower fall differently
                // from the next.
                var normal = (sim.BirdPosition - block.P);
                if (normal.sqrMagnitude < 0.0001f) normal = new Vector2(1f, 0f);
                normal.Normalize();

                var point = ClosestPointOnBox(sim.BirdPosition, block);

                if (hard)
                {
                    float birdMass = 0.6f;
                    var impulse = normal * (speed * birdMass * settings.BirdPush);
                    Wake(sim, block);
                    ApplyImpulse(sim, new BodyRef { Kind = KindBlock, Index = i }, impulse, point);

                    int damage = DamageFor(speed, KindOf(sim.Level.Blocks[i]), settings);
                    if (damage > 0)
                    {
                        var updated = sim.Level.Blocks[i];
                        updated.Health -= damage;
                        sim.Level.Blocks[i] = updated;
                        if (updated.Health <= 0) BreakBlock(sim, i, point);
                    }
                }

                // The bird bounces back off the normal, keeping some of its tangent speed. A soft hit
                // still bounces — the bird never passes through an obstacle.
                var tangential = sim.BirdVelocity - normal * Vector2.Dot(sim.BirdVelocity, normal);
                sim.BirdVelocity = -normal * (speed * 0.22f) + tangential * 0.35f;

                sim.Impacts++;
                if (sim.Impacts >= 2)
                {
                    sim.BirdVelocity *= 0.2f;
                    sim.BirdVelocity.y = Mathf.Min(sim.BirdVelocity.y, 0f);
                    Stop(sim);
                }

                return;
            }
        }

        private static BlockKind KindOf(BirdBlock block) => block.Kind;

        private static void Bounce(Sim sim)
        {
            sim.Impacts++;
            sim.BirdVelocity = new Vector2(sim.BirdVelocity.x * -0.22f, Mathf.Abs(sim.BirdVelocity.y) * 0.28f);

            if (sim.Impacts >= 2)
            {
                sim.BirdVelocity = new Vector2(sim.BirdVelocity.x * 0.2f, Mathf.Min(sim.BirdVelocity.y, 0f));
                Stop(sim);
            }
        }

        // ------------------------------------------------------------------ integration

        private static void Integrate(Sim sim, float dt)
        {
            // Damping is a *rate per second*, not a per-step multiplier: at 120 steps a second the two
            // are two orders of magnitude apart, and the per-step version turns every block into a
            // body moving through treacle.
            float linear = Mathf.Pow(Mathf.Clamp01(1f - sim.S.LinearDamping), dt);
            float angular = Mathf.Pow(Mathf.Clamp01(1f - sim.S.AngularDamping), dt);

            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive || !block.Awake) continue;

                block.V.y -= sim.S.Gravity * dt;
                block.V *= linear;
                block.W *= angular;

                // The resting rule, before anything else can move it: a body creeping at centimetres
                // per second is a body at rest that the solver has not finished with.
                //
                // Only if something is *touching* it, which is the distinction the first version of this
                // missed: damping a body in mid-air slows its fall to six centimetres a second, which is
                // under the sleep threshold, so a block dropped from two metres fell three centimetres
                // and went to sleep hanging in the air. Gravity is not a resting contact.
                if (block.Supported)
                {
                    if (block.V.sqrMagnitude < sim.S.RestSpeed * sim.S.RestSpeed)
                    {
                        block.V *= sim.S.RestDamping;
                    }

                    if (Mathf.Abs(block.W) < sim.S.RestSpin) block.W *= sim.S.RestDamping;
                }

                // A hard speed clamp: one bad contact must not be able to fling a block across the map,
                // which is the failure mode that turns a physics demo into a bug report.
                float limit = sim.S.MaxBlockSpeed;
                if (block.V.sqrMagnitude > limit * limit) block.V = block.V.normalized * limit;
                block.W = Mathf.Clamp(block.W, -18f, 18f);

                block.P += block.V * dt;
                block.A += block.W * dt;
                block.ImpactSpeed = block.V.magnitude;

                if (block.P.x < sim.Level.SlingX - 26f || block.P.x > sim.Level.SlingX + 40f)
                {
                    // Way off the field: stop pretending it is part of the level.
                    block.Awake = false;
                    block.V = Vector2.zero;
                    block.W = 0f;
                }
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                var pig = sim.Pigs[i];
                if (!pig.Alive || !pig.Awake) continue;

                pig.V.y -= sim.S.Gravity * dt;
                pig.V *= linear;
                if (pig.Supported && pig.V.sqrMagnitude < sim.S.RestSpeed * sim.S.RestSpeed)
                {
                    pig.V *= sim.S.RestDamping;
                }
                pig.P += pig.V * dt;
                pig.ImpactSpeed = pig.V.magnitude;

                float limit = sim.S.MaxBlockSpeed;
                if (pig.V.sqrMagnitude > limit * limit) pig.V = pig.V.normalized * limit;
            }
        }

        // ------------------------------------------------------------------ contacts

        private static void BuildContacts(Sim sim)
        {
            sim.Contacts.Clear();
            float floor = sim.Floor;

            for (int i = 0; i < sim.Blocks.Length; i++) sim.Blocks[i].Supported = false;
            for (int i = 0; i < sim.Pigs.Length; i++) sim.Pigs[i].Supported = false;

            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive) continue;

                // Ground: every corner below the floor is a contact, which is what lets a box tip over
                // the edge of a step instead of sliding along it.
                if (block.Awake)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        var corner = Corner(block, c);
                        if (corner.y >= floor) continue;

                        sim.Contacts.Add(new Contact
                        {
                            A = BodyRef.Ground,
                            B = new BodyRef { Kind = KindBlock, Index = i },
                            Point = new Vector2(corner.x, floor),
                            Normal = new Vector2(0f, 1f),
                            Depth = floor - corner.y
                        });
                    }
                }

                // Pairs are generated if *either* body is awake, which is the whole point of a body
                // being asleep: it is a static anchor, not an invisible one.
                //
                // The first version tested `Awake` on the outer body and skipped the rest of the
                // iteration, so a beam resting on two legs that had already gone to sleep had no
                // contacts at all with them and simply fell between them to the floor. Every "the tower
                // collapses by itself" symptom in this round traced back to that one line.
                for (int j = i + 1; j < sim.Blocks.Length; j++)
                {
                    var other = sim.Blocks[j];
                    if (!other.Alive) continue;
                    if (!block.Awake && !other.Awake) continue;

                    BoxBox(sim, i, j);
                }
            }

            for (int p = 0; p < sim.Pigs.Length; p++)
            {
                var pig = sim.Pigs[p];
                if (!pig.Alive) continue;

                if (pig.Awake && pig.P.y - pig.R < floor)
                {
                    sim.Contacts.Add(new Contact
                    {
                        A = BodyRef.Ground,
                        B = new BodyRef { Kind = KindPig, Index = p },
                        Point = new Vector2(pig.P.x, floor),
                        Normal = new Vector2(0f, 1f),
                        Depth = floor - (pig.P.y - pig.R)
                    });
                }

                for (int i = 0; i < sim.Blocks.Length; i++)
                {
                    var block = sim.Blocks[i];
                    if (!block.Alive) continue;
                    if (!pig.Awake && !block.Awake) continue;

                    CircleBox(sim, p, i);
                }

                for (int q = p + 1; q < sim.Pigs.Length; q++)
                {
                    var other = sim.Pigs[q];
                    if (!other.Alive) continue;
                    if (!pig.Awake && !other.Awake) continue;

                    var delta = other.P - pig.P;
                    float distance = delta.magnitude;
                    float overlap = pig.R + other.R - distance;
                    if (overlap <= 0f) continue;

                    var normal = distance > 0.0001f ? delta / distance : new Vector2(1f, 0f);
                    sim.Contacts.Add(new Contact
                    {
                        A = new BodyRef { Kind = KindPig, Index = p },
                        B = new BodyRef { Kind = KindPig, Index = q },
                        Point = pig.P + normal * pig.R,
                        Normal = normal,
                        Depth = overlap
                    });
                }
            }
            for (int i = 0; i < sim.Contacts.Count; i++)
            {
                var contact = sim.Contacts[i];
                if (contact.A.Kind == KindBlock) sim.Blocks[contact.A.Index].Supported = true;
                else if (contact.A.Kind == KindPig) sim.Pigs[contact.A.Index].Supported = true;

                if (contact.B.Kind == KindBlock) sim.Blocks[contact.B.Index].Supported = true;
                else if (contact.B.Kind == KindPig) sim.Pigs[contact.B.Index].Supported = true;
            }
        }

        /// <summary>
        /// Gives a shove to any block whose weight is no longer over anything.
        ///
        /// This is the one place this round puts a *rule* on top of the solver, and it is here because
        /// of what a solver cannot do on its own: a stack of boxes in a hand-written impulse solver
        /// creeps, and the cure for the creep is a resting damping that quietly cancels the first
        /// fraction of a second of any slow motion — which is exactly the fraction of a second in which
        /// a beam that has lost a leg decides whether to fall. Left to itself the beam just sits there,
        /// hovering on nothing, which is the one thing a player would never forgive.
        ///
        /// So the balance question is asked directly, once per step, and answered by pushing: if the
        /// block's centre of mass is outside the span of the things holding it up, it gets a downward
        /// nudge bigger than the damping threshold, and from there gravity and the contacts do the rest.
        /// It is the same rule the previous version used to <i>move</i> blocks outright; it only seeds
        /// the motion now, and the motion is real.
        /// </summary>
        private static void NudgeUnsupported(Sim sim)
        {
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive || !block.Awake) continue;

                float left = float.MaxValue;
                float right = float.MinValue;
                bool supported = false;

                for (int c = 0; c < sim.Contacts.Count; c++)
                {
                    var contact = sim.Contacts[c];

                    bool blockIsB = contact.B.Kind == KindBlock && contact.B.Index == i;
                    bool blockIsA = contact.A.Kind == KindBlock && contact.A.Index == i;
                    if (!blockIsA && !blockIsB) continue;

                    // A support is a contact from *below*: the push on the block points up.
                    float lift = blockIsB ? contact.Normal.y : -contact.Normal.y;
                    if (lift < 0.5f) continue;
                    if (contact.Point.y > block.P.y + 0.02f) continue;

                    left = Mathf.Min(left, contact.Point.x);
                    right = Mathf.Max(right, contact.Point.x);
                    supported = true;
                }

                bool balanced = supported && block.P.x >= left - 0.06f && block.P.x <= right + 0.06f;
                if (balanced) continue;

                // Nothing under it, or its weight is past the edge of what is: start it moving, so the
                // resting damping has nothing to hold on to.
                block.V.y -= sim.S.FallNudge;
                if (supported)
                {
                    // Tipping towards the side the weight is hanging over.
                    block.W += (block.P.x > (left + right) * 0.5f ? 1f : -1f) * sim.S.TipNudge;
                }
            }
        }
        private static void BoxBox(Sim sim, int indexA, int indexB)
        {
            var a = sim.Blocks[indexA];
            var b = sim.Blocks[indexB];

            Vector2 ax = AxisX(a.A), ay = AxisY(a.A);
            Vector2 bx = AxisX(b.A), by = AxisY(b.A);
            var delta = b.P - a.P;

            var axes = new[] { ax, ay, bx, by };
            float bestOverlap = float.MaxValue;
            Vector2 bestNormal = ax;

            for (int i = 0; i < 4; i++)
            {
                var axis = axes[i];
                float radiusA = a.HalfW * Mathf.Abs(Vector2.Dot(ax, axis)) + a.HalfH * Mathf.Abs(Vector2.Dot(ay, axis));
                float radiusB = b.HalfW * Mathf.Abs(Vector2.Dot(bx, axis)) + b.HalfH * Mathf.Abs(Vector2.Dot(by, axis));
                float distance = Vector2.Dot(delta, axis);
                float overlap = radiusA + radiusB - Mathf.Abs(distance);

                if (overlap <= 0f) return;      // a separating axis: no contact at all
                if (overlap >= bestOverlap) continue;

                bestOverlap = overlap;
                bestNormal = distance < 0f ? -axis : axis;
            }

            var aRef = new BodyRef { Kind = KindBlock, Index = indexA };
            var bRef = new BodyRef { Kind = KindBlock, Index = indexB };

            // The contact *surface*, not the corners that happen to be inside each other.
            //
            // This is the fix for the single most misleading bug of the round: with corner-only
            // contacts a beam lying across two uprights touched each of them at exactly one point —
            // the beam's own corner, which is at the outer edge of the upright. A beam balanced on two
            // tips is not a beam resting on two legs, and the whole tower crept and toppled over a few
            // thousand quiet steps. Projecting both boxes onto the tangent and taking the *overlap*
            // interval gives the two contacts a face contact actually has.
            var tangent = new Vector2(-bestNormal.y, bestNormal.x);

            float centreA = Vector2.Dot(a.P, tangent);
            float radiusAt = a.HalfW * Mathf.Abs(Vector2.Dot(ax, tangent))
                             + a.HalfH * Mathf.Abs(Vector2.Dot(ay, tangent));
            float centreB = Vector2.Dot(b.P, tangent);
            float radiusBt = b.HalfW * Mathf.Abs(Vector2.Dot(bx, tangent))
                             + b.HalfH * Mathf.Abs(Vector2.Dot(by, tangent));

            float low = Mathf.Max(centreA - radiusAt, centreB - radiusBt);
            float high = Mathf.Min(centreA + radiusAt, centreB + radiusBt);

            if (high > low)
            {
                // Halfway inside the overlap along the normal, which is where a contact point belongs:
                // on neither body's surface but between them.
                float normalA = Vector2.Dot(a.P, bestNormal);
                float radiusAn = a.HalfW * Mathf.Abs(Vector2.Dot(ax, bestNormal))
                                 + a.HalfH * Mathf.Abs(Vector2.Dot(ay, bestNormal));
                float normalPosition = normalA + radiusAn - bestOverlap * 0.5f;

                sim.Contacts.Add(ContactAt(aRef, bRef,
                    bestNormal * normalPosition + tangent * low, bestNormal, bestOverlap));
                sim.Contacts.Add(ContactAt(aRef, bRef,
                    bestNormal * normalPosition + tangent * high, bestNormal, bestOverlap));
                return;
            }

            // No shared span (a corner against a face): the corner inside the other box is the contact.
            int added = 0;
            for (int c = 0; c < 4; c++)
            {
                var corner = Corner(b, c);
                if (!InsideBox(corner, a)) continue;
                sim.Contacts.Add(ContactAt(aRef, bRef, corner, bestNormal, bestOverlap));
                added++;
            }

            for (int c = 0; c < 4; c++)
            {
                var corner = Corner(a, c);
                if (!InsideBox(corner, b)) continue;
                sim.Contacts.Add(ContactAt(aRef, bRef, corner, bestNormal, bestOverlap));
                added++;
            }

            if (added > 0) return;

            // Nothing inside anything: the deepest pair of corners is the honest single contact.
            Vector2 pointA = Corner(a, 0), pointB = Corner(b, 0);
            float best = float.MaxValue;
            for (int c = 0; c < 4; c++)
            {
                var corner = Corner(b, c);
                float depth = Vector2.Dot(corner - a.P, bestNormal);
                if (depth < best) { best = depth; pointB = corner; }
            }

            best = float.MaxValue;
            for (int c = 0; c < 4; c++)
            {
                var corner = Corner(a, c);
                float depth = -Vector2.Dot(corner - b.P, bestNormal);
                if (depth < best) { best = depth; pointA = corner; }
            }

            var mid = (pointA + pointB) * 0.5f;
            sim.Contacts.Add(ContactAt(aRef, bRef, mid, bestNormal, bestOverlap));
        }

        private static Contact ContactAt(BodyRef a, BodyRef b, Vector2 point, Vector2 normal, float depth)
            => new Contact { A = a, B = b, Point = point, Normal = normal, Depth = depth };

        /// <summary>
        /// A pig against a block: circle against oriented box.
        ///
        /// The bodies are ordered block-then-pig so that the normal the geometry returns — which points
        /// away from the box towards the circle — is also the normal the solver wants, from A to B. The
        /// first version had it the other way round, and a normal pointing the wrong way relative to its
        /// own bodies pushes the pig *into* the beam it is standing on (and the beam away underneath it):
        /// the pigs sank, the beams slid, and the towers walked themselves apart over a few seconds.
        /// </summary>
        private static void CircleBox(Sim sim, int pigIndex, int blockIndex)
        {
            var pig = sim.Pigs[pigIndex];
            var block = sim.Blocks[blockIndex];

            Vector2 normal;
            float depth;
            if (!CircleBoxOverlap(pig.P, pig.R, block, out normal, out depth)) return;

            sim.Contacts.Add(new Contact
            {
                A = new BodyRef { Kind = KindBlock, Index = blockIndex },
                B = new BodyRef { Kind = KindPig, Index = pigIndex },
                Point = pig.P - normal * (pig.R - depth * 0.5f),
                Normal = normal,
                Depth = depth
            });
        }

        // ------------------------------------------------------------------ the solver

        private static void ResolveContacts(Sim sim, float dt)
        {
            for (int i = 0; i < sim.Contacts.Count; i++)
            {
                var contact = sim.Contacts[i];

                // A sleeping body is a static body until something hits it hard enough to matter.
                // Waking on *every* contact impulse is how the first version of this ended up with a
                // tower that never slept, never stopped being integrated, and crept half a metre across
                // the floor while the player watched it.
                WakeIfHit(sim, contact.A, contact.B, contact.Point);
                WakeIfHit(sim, contact.B, contact.A, contact.Point);

                var velocityA = VelocityAt(sim, contact.A, contact.Point);
                var velocityB = VelocityAt(sim, contact.B, contact.Point);
                var relative = velocityB - velocityA;

                float normalSpeed = Vector2.Dot(relative, contact.Normal);

                float invMassSum = InvMass(sim, contact.A) + InvMass(sim, contact.B);
                invMassSum += RotationalTerm(sim, contact.A, contact.Point, contact.Normal);
                invMassSum += RotationalTerm(sim, contact.B, contact.Point, contact.Normal);
                if (invMassSum <= 0.00001f) continue;

                // Restitution only for a real impact: applying it to resting contacts is how a
                // hand-written solver gets a tower that buzzes.
                float restitution = normalSpeed < -1.2f ? sim.S.Restitution : 0f;
                float target = -normalSpeed - restitution * normalSpeed;
                if (target < 0f) target = 0f;

                float delta = target / invMassSum;
                float newImpulse = Mathf.Max(0f, contact.NormalImpulse + delta) - contact.NormalImpulse;
                contact.NormalImpulse += newImpulse;

                var impulse = contact.Normal * newImpulse;
                ApplyImpulse(sim, contact.A, -impulse, contact.Point);
                ApplyImpulse(sim, contact.B, impulse, contact.Point);

                // Friction along the tangent, clamped by Coulomb — with a higher coefficient while the
                // contact is barely sliding, which is what stops a settled tower from creeping.
                var tangent = new Vector2(-contact.Normal.y, contact.Normal.x);
                float tangentSpeed = Vector2.Dot(relative, tangent);
                float tangentMass = InvMass(sim, contact.A) + InvMass(sim, contact.B);
                tangentMass += RotationalTerm(sim, contact.A, contact.Point, tangent);
                tangentMass += RotationalTerm(sim, contact.B, contact.Point, tangent);
                if (tangentMass <= 0.00001f) continue;

                float tangentDelta = -tangentSpeed / tangentMass;
                float coefficient = Mathf.Abs(tangentSpeed) < sim.S.StaticSpeed
                    ? Mathf.Max(sim.S.Friction, sim.S.StaticFriction)
                    : sim.S.Friction;
                float maxFriction = coefficient * contact.NormalImpulse;
                float newTangent = Mathf.Clamp(contact.TangentImpulse + tangentDelta,
                    -maxFriction, maxFriction) - contact.TangentImpulse;
                contact.TangentImpulse += newTangent;

                var frictionImpulse = tangent * newTangent;
                ApplyImpulse(sim, contact.A, -frictionImpulse, contact.Point);
                ApplyImpulse(sim, contact.B, frictionImpulse, contact.Point);

                sim.Contacts[i] = contact;
            }
        }

        /// <summary>
        /// Pushes overlapping bodies apart, without touching velocity.
        ///
        /// Positional correction rather than a velocity bias: a bias adds energy, and the thing that
        /// makes a stack of boxes believable is that a resting stack has *no* energy in it.
        ///
        /// One correction per *body*, not per contact, and that distinction is the difference between a
        /// tower and a puddle. A box standing on the floor has four corner contacts, all with the same
        /// depth; correcting each of them separately lifts the box by four times the overlap it has, so
        /// it hops, falls back, and the stack buzzes itself apart — measured at metres of drift before
        /// this existed. The largest single correction is the honest one: it is what it takes to get
        /// that body out of what it is deepest into.
        /// </summary>
        private static void CorrectPositions(Sim sim)
        {
            const float slop = 0.004f;
            const float share = 0.55f;

            if (_corrections == null || _corrections.Length < sim.Blocks.Length + sim.Pigs.Length)
            {
                _corrections = new Vector2[sim.Blocks.Length + sim.Pigs.Length + 8];
            }

            int count = sim.Blocks.Length + sim.Pigs.Length;
            for (int i = 0; i < count; i++) _corrections[i] = Vector2.zero;

            for (int i = 0; i < sim.Contacts.Count; i++)
            {
                var contact = sim.Contacts[i];
                float depth = contact.Depth - slop;
                if (depth <= 0f) continue;

                float invMassA = InvMass(sim, contact.A);
                float invMassB = InvMass(sim, contact.B);
                float sum = invMassA + invMassB;
                if (sum <= 0.00001f) continue;

                var direction = contact.Normal * (depth * share / sum);

                Offer(sim, contact.A, -direction * invMassA);
                Offer(sim, contact.B, direction * invMassB);
            }

            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive || !block.Awake) continue;
                block.P += _corrections[i];
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                var pig = sim.Pigs[i];
                if (!pig.Alive || !pig.Awake) continue;
                pig.P += _corrections[sim.Blocks.Length + i];
            }
        }

        private static Vector2[] _corrections;

        private static void Offer(Sim sim, BodyRef body, Vector2 correction)
        {
            int slot = -1;
            if (body.Kind == KindBlock) slot = body.Index;
            else if (body.Kind == KindPig) slot = sim.Blocks.Length + body.Index;
            if (slot < 0 || slot >= _corrections.Length) return;

            if (correction.sqrMagnitude > _corrections[slot].sqrMagnitude) _corrections[slot] = correction;
        }

        /// <summary>
        /// Wakes <paramref name="target"/> when <paramref name="other"/> is arriving at it fast.
        ///
        /// The threshold is what keeps a resting stack asleep: gravity pushes every block into the one
        /// below it every step, and if that counted as "being hit" nothing would ever settle.
        /// </summary>
        private static void WakeIfHit(Sim sim, BodyRef target, BodyRef other, Vector2 point)
        {
            if (target.Kind != KindBlock) return;

            var block = sim.Blocks[target.Index];
            if (block.Awake || !block.Alive) return;

            if (other.Kind == KindStatic) return;      // the ground does not hit anything

            float speed = VelocityAt(sim, other, point).magnitude;
            if (speed < sim.S.WakeSpeed) return;

            block.Awake = true;
            block.Rest = 0f;
        }

        private static float InvMass(Sim sim, BodyRef body)
        {
            switch (body.Kind)
            {
                case KindBlock:
                    var block = sim.Blocks[body.Index];
                    return block.Alive && block.Awake ? block.InvMass : 0f;

                case KindPig:
                    var pig = sim.Pigs[body.Index];
                    return pig.Alive && pig.Awake ? pig.InvMass : 0f;

                default:
                    return 0f;
            }
        }

        private static float InvInertia(Sim sim, BodyRef body)
            => body.Kind == KindBlock && sim.Blocks[body.Index].Awake ? sim.Blocks[body.Index].InvInertia : 0f;

        /// <summary>The n·(r×n)² term that makes a contact off-centre rotate the body it hits.</summary>
        private static float RotationalTerm(Sim sim, BodyRef body, Vector2 point, Vector2 direction)
        {
            if (body.Kind != KindBlock) return 0f;

            var block = sim.Blocks[body.Index];
            var r = point - block.P;
            float cross = r.x * direction.y - r.y * direction.x;
            return cross * cross * block.InvInertia;
        }

        private static Vector2 VelocityAt(Sim sim, BodyRef body, Vector2 point)
        {
            switch (body.Kind)
            {
                case KindBlock:
                    var block = sim.Blocks[body.Index];
                    if (!block.Awake) return Vector2.zero;
                    var r = point - block.P;
                    return block.V + new Vector2(-block.W * r.y, block.W * r.x);

                case KindPig:
                    var pig = sim.Pigs[body.Index];
                    return pig.Awake ? pig.V : Vector2.zero;

                default:
                    return Vector2.zero;
            }
        }

        private static void ApplyImpulse(Sim sim, BodyRef body, Vector2 impulse, Vector2 point)
        {
            if (body.Kind == KindBlock)
            {
                var block = sim.Blocks[body.Index];
                if (!block.Alive || !block.Awake) return;

                block.V += impulse * block.InvMass;

                var r = point - block.P;
                block.W += (r.x * impulse.y - r.y * impulse.x) * block.InvInertia;
                return;
            }

            if (body.Kind == KindPig)
            {
                var pig = sim.Pigs[body.Index];
                if (!pig.Alive || !pig.Awake) return;

                pig.V += impulse * pig.InvMass;
            }
        }

        // ------------------------------------------------------------------ damage

        /// <summary>
        /// Pigs die from being hit hard, from falling, and from being buried.
        ///
        /// All three matter and none of them is decoration: with real bodies a collapse *is* the
        /// answer to a pig, and a pig that could only ever die to a direct hit would make the towers
        /// pointless. ("倒塌也能压死猪" — the point of the whole physics upgrade.)
        /// </summary>
        private static void PigDamage(Sim sim, float dt)
        {
            for (int p = 0; p < sim.Pigs.Length; p++)
            {
                var pig = sim.Pigs[p];
                if (!pig.Alive) continue;

                // A fall: it arrived at the floor or on top of something at speed. The speed is the one
                // measured *before* the contacts were resolved, because after them the pig is stopped
                // and every landing would look like a gentle one.
                if (pig.ImpactSpeed > sim.S.PigCrushSpeed)
                {
                    KillPig(sim, p, pig.P);
                    continue;
                }

                // Being buried alive under something heavy.
                bool buried = false;
                for (int i = 0; i < sim.Blocks.Length; i++)
                {
                    var block = sim.Blocks[i];
                    if (!block.Alive) continue;

                    // Only things *above* the pig squash it: a pig standing on a beam is not being
                    // crushed by it, and treating the two the same would kill every pig in the game
                    // on the first frame.
                    if (block.P.y < pig.P.y + pig.R * 0.25f) continue;
                    if (!BlockOverlapsPig(block, pig)) continue;
                    if (MassFor(sim.Level.Blocks[i]) < sim.S.PigSquashMass) continue;

                    buried = true;
                    break;
                }

                pig.Squash = buried ? pig.Squash + dt : 0f;
                if (pig.Squash >= sim.S.PigSquashSeconds) KillPig(sim, p, pig.P);
            }

            // Blocks that land hard break, which is what makes a collapse look like a collapse: the
            // ice shatters on the ground, the wood splits, the stone survives.
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive || !block.Awake) continue;
                if (block.ImpactSpeed < sim.S.GroundBreakSpeed) continue;

                bool touchingSomething = false;
                for (int c = 0; c < sim.Contacts.Count; c++)
                {
                    var contact = sim.Contacts[c];
                    bool involvesA = contact.A.Kind == KindBlock && contact.A.Index == i;
                    bool involvesB = contact.B.Kind == KindBlock && contact.B.Index == i;
                    if (!involvesA && !involvesB) continue;
                    touchingSomething = true;
                    break;
                }

                if (!touchingSomething) continue;

                int damage = Mathf.Max(1, Mathf.FloorToInt(block.ImpactSpeed / sim.S.GroundBreakSpeed));
                var updated = sim.Level.Blocks[i];
                updated.Health -= damage;
                sim.Level.Blocks[i] = updated;

                if (updated.Health <= 0) BreakBlock(sim, i, block.P);
            }
        }

        private static void BreakBlock(Sim sim, int index, Vector2 at)
        {
            var block = sim.Blocks[index];
            if (!block.Alive) return;

            block.Alive = false;
            block.Awake = false;
            block.V = Vector2.zero;
            block.W = 0f;

            var data = sim.Level.Blocks[index];
            data.Alive = false;
            data.Health = 0;
            sim.Level.Blocks[index] = data;

            sim.Result.BlocksBroken++;
            sim.Result.Events.Add(new BirdEvent
            {
                Kind = BirdEventKind.BlockBroken,
                Index = index,
                Time = sim.Time,
                At = at
            });

            // Everything wakes up: the tower above a broken leg has to be allowed to notice. Waking
            // only the neighbours would leave a beam held up by a block that is no longer there,
            // because whoever was asleep has no way to notice that gravity now applies to them.
            WakeAll(sim);
        }

        private static void KillPig(Sim sim, int index, Vector2 at)
        {
            var pig = sim.Pigs[index];
            if (!pig.Alive) return;

            pig.Alive = false;
            pig.Awake = false;
            pig.V = Vector2.zero;

            var data = sim.Level.Pigs[index];
            data.Alive = false;
            data.VX = 0f;
            data.VY = 0f;
            sim.Level.Pigs[index] = data;

            sim.Result.PigsKilled++;
            sim.Result.Events.Add(new BirdEvent
            {
                Kind = BirdEventKind.PigKilled,
                Index = index,
                Time = sim.Time,
                At = at
            });
        }

        private static void SleepBodies(Sim sim, float dt)
        {
            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                var block = sim.Blocks[i];
                if (!block.Alive || !block.Awake) continue;

                bool still = block.Supported
                             && block.V.sqrMagnitude <= sim.S.SleepSpeed * sim.S.SleepSpeed
                             && Mathf.Abs(block.W) <= sim.S.SleepSpin;

                block.Rest = still ? block.Rest + dt : 0f;
                if (block.Rest < sim.S.SleepSeconds) continue;

                block.Awake = false;
                block.V = Vector2.zero;
                block.W = 0f;
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                var pig = sim.Pigs[i];
                if (!pig.Alive || !pig.Awake) continue;

                pig.Rest = pig.Supported && pig.V.sqrMagnitude <= sim.S.SleepSpeed * sim.S.SleepSpeed
                    ? pig.Rest + dt : 0f;
                if (pig.Rest < sim.S.SleepSeconds) continue;

                pig.Awake = false;
                pig.V = Vector2.zero;
            }
        }

        private static void Wake(Sim sim, SimBlock block)
        {
            block.Awake = true;
            block.Rest = 0f;
        }

        // ------------------------------------------------------------------ replay

        private static void RecordFrame(Sim sim)
        {
            if (!sim.Record) return;
            if (sim.Time < sim.NextFrame && sim.Result.Frames.Count > 0) return;

            sim.NextFrame = sim.Time + 1f / Mathf.Max(5f, sim.S.ReplayRate);

            var frame = new BirdFrame
            {
                Time = sim.Time,
                Bird = sim.BirdPosition,
                Blocks = new Vector2[sim.Blocks.Length],
                Angles = new float[sim.Blocks.Length],
                Alive = new bool[sim.Blocks.Length],
                Pigs = new Vector2[sim.Pigs.Length],
                PigAlive = new bool[sim.Pigs.Length]
            };

            for (int i = 0; i < sim.Blocks.Length; i++)
            {
                frame.Blocks[i] = sim.Blocks[i].P;
                frame.Angles[i] = sim.Blocks[i].A;
                frame.Alive[i] = sim.Blocks[i].Alive;
            }

            for (int i = 0; i < sim.Pigs.Length; i++)
            {
                frame.Pigs[i] = sim.Pigs[i].P;
                frame.PigAlive[i] = sim.Pigs[i].Alive;
            }

            sim.Result.Frames.Add(frame);
        }

        // ------------------------------------------------------------------ geometry

        public static Vector2 AxisX(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        public static Vector2 AxisY(float angle) => new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

        public static bool CircleHitsCircle(Vector2 centre, float radius, Vector2 other, float otherRadius)
            => (centre - other).sqrMagnitude <= (radius + otherRadius) * (radius + otherRadius);

        /// <summary>
        /// Swept circle-vs-point test: whether the segment a→b comes within <paramref name="radius"/>
        /// of <paramref name="point"/>. This is the fix for the bird tunneling through a pig — a single
        /// per-step point check can skip a body entirely when the bird moves fast.
        /// </summary>
        public static bool SegmentHitsPoint(Vector2 a, Vector2 b, float radius, Vector2 point)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq) : 0f;
            Vector2 closest = a + ab * t;
            return (closest - point).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Swept circle-vs-box test: samples the segment a→b at intervals no larger than the bird
        /// radius, so the swept capsule is fully covered and a fast bird cannot skip a thin beam.
        /// </summary>
        private static bool SegmentHitsBox(Vector2 a, Vector2 b, float radius, SimBlock box)
        {
            float len = (b - a).magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.04f, radius * 0.6f)));
            for (int i = 0; i <= steps; i++)
            {
                if (CircleHitsBox(Vector2.Lerp(a, b, (float)i / steps), radius, box)) return true;
            }
            return false;
        }

        /// <summary>Circle against a box, rotation included.</summary>
        public static bool CircleHitsBox(Vector2 centre, float radius, BirdBlock box)
        {
            var axisX = AxisX(box.Angle);
            var axisY = AxisY(box.Angle);
            var delta = centre - new Vector2(box.X, box.Y);

            float localX = Mathf.Clamp(Vector2.Dot(delta, axisX), -box.HalfW, box.HalfW);
            float localY = Mathf.Clamp(Vector2.Dot(delta, axisY), -box.HalfH, box.HalfH);
            var closest = new Vector2(box.X, box.Y) + axisX * localX + axisY * localY;

            return (centre - closest).sqrMagnitude <= radius * radius;
        }

        private static bool CircleHitsBox(Vector2 centre, float radius, SimBlock box)
        {
            var axisX = AxisX(box.A);
            var axisY = AxisY(box.A);
            var delta = centre - box.P;

            float localX = Mathf.Clamp(Vector2.Dot(delta, axisX), -box.HalfW, box.HalfW);
            float localY = Mathf.Clamp(Vector2.Dot(delta, axisY), -box.HalfH, box.HalfH);
            var closest = box.P + axisX * localX + axisY * localY;

            return (centre - closest).sqrMagnitude <= radius * radius;
        }

        /// <summary>Circle against box, returning the push-out direction and depth.</summary>
        private static bool CircleBoxOverlap(Vector2 centre, float radius, SimBlock box,
            out Vector2 normal, out float depth)
        {
            normal = new Vector2(0f, 1f);
            depth = 0f;

            var axisX = AxisX(box.A);
            var axisY = AxisY(box.A);
            var delta = centre - box.P;

            float dx = Vector2.Dot(delta, axisX);
            float dy = Vector2.Dot(delta, axisY);

            float clampedX = Mathf.Clamp(dx, -box.HalfW, box.HalfW);
            float clampedY = Mathf.Clamp(dy, -box.HalfH, box.HalfH);

            var closest = box.P + axisX * clampedX + axisY * clampedY;
            var away = centre - closest;
            float distance = away.magnitude;

            if (distance > radius) return false;

            if (distance > 0.0001f)
            {
                normal = away / distance;
                depth = radius - distance;
                return true;
            }

            // The centre is inside the box: push out through the nearest face.
            float toLeft = dx + box.HalfW;
            float toRight = box.HalfW - dx;
            float toBottom = dy + box.HalfH;
            float toTop = box.HalfH - dy;

            float min = toLeft;
            normal = -axisX;
            if (toRight < min) { min = toRight; normal = axisX; }
            if (toBottom < min) { min = toBottom; normal = -axisY; }
            if (toTop < min) { min = toTop; normal = axisY; }

            depth = radius + min;
            return true;
        }

        private static bool BlockOverlapsPig(SimBlock block, SimPig pig)
        {
            Vector2 normal;
            float depth;
            return CircleBoxOverlap(pig.P, pig.R, block, out normal, out depth);
        }

        private static Vector2 ClosestPointOnBox(Vector2 point, SimBlock box)
        {
            var axisX = AxisX(box.A);
            var axisY = AxisY(box.A);
            var delta = point - box.P;

            float localX = Mathf.Clamp(Vector2.Dot(delta, axisX), -box.HalfW, box.HalfW);
            float localY = Mathf.Clamp(Vector2.Dot(delta, axisY), -box.HalfH, box.HalfH);
            return box.P + axisX * localX + axisY * localY;
        }

        private static Vector2 Corner(SimBlock box, int index)
        {
            float signX = (index & 1) == 0 ? -1f : 1f;
            float signY = (index & 2) == 0 ? -1f : 1f;
            return box.P + AxisX(box.A) * (signX * box.HalfW) + AxisY(box.A) * (signY * box.HalfH);
        }

        private static bool InsideBox(Vector2 point, SimBlock box)
        {
            var delta = point - box.P;
            float dx = Vector2.Dot(delta, AxisX(box.A));
            float dy = Vector2.Dot(delta, AxisY(box.A));
            return Mathf.Abs(dx) <= box.HalfW && Mathf.Abs(dy) <= box.HalfH;
        }

        /// <summary>Whether a point is inside a box, for hit-testing a tap.</summary>
        public static bool PointInBox(Vector2 point, BirdBlock box)
        {
            var delta = point - new Vector2(box.X, box.Y);
            float dx = Vector2.Dot(delta, AxisX(box.Angle));
            float dy = Vector2.Dot(delta, AxisY(box.Angle));
            return Mathf.Abs(dx) <= box.HalfW && Mathf.Abs(dy) <= box.HalfH;
        }
    }
}
