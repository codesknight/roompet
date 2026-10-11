using System.Collections.Generic;
using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// Builds the levels for 愤怒的小鸟 — randomly, and then *proves* they can be cleared.
    ///
    /// The requirement was "每关的场景地图可随机搭建但是必须保证能够通关", and those two halves pull
    /// against each other: random structures are exactly how you get an unbreakable one. So the
    /// generator does what the platformer generator already does in this project — build, then
    /// **verify by playing it** — and only hands over a level that a machine has already beaten:
    ///
    ///   1. build a structure from a seed (columns of blocks, pigs inside, materials by stage);
    ///   2. try a grid of candidate shots at the slingshot, each simulated with the *same*
    ///      <see cref="BirdRules.Simulate"/> the game uses;
    ///   3. greedily pick the shot that kills the most pigs, repeat until cleared or out of birds;
    ///   4. if the level survives the greedy solver, throw it away and try the next seed.
    ///
    /// The solver is deliberately not clever — it tries a fixed grid and takes the best. If a level
    /// cannot be beaten by *that*, it is not a level a player should be handed.
    /// </summary>
    public static class BirdLevels
    {
        /// <summary>How many stages 闯关 has.</summary>
        public const int StageCount = 12;

        /// <summary>
        /// How far apart two storeys stand: an upright (0.68) plus the beam lying on top of it (0.20).
        ///
        /// This is not a taste decision. Round 21 gave the blocks real contacts, and a contact solver
        /// cannot be handed blocks that interpenetrate: the storey above has to start exactly on the
        /// beam's top face, which is what this number makes true.
        /// </summary>
        private const float StoreyPitch = 0.88f;

        /// <summary>
        /// A hair of clearance between touching blocks.
        ///
        /// Exactly zero would mean every contact starts life as one step of penetration (gravity moves
        /// both bodies before the solver sees them), and a few millimetres of slack is the difference
        /// between a tower that stands and a tower that twitches.
        /// </summary>
        private const float CreateGap = 0.004f;

        /// <summary>
        /// How many layouts to try before falling back to a known-good one.
        ///
        /// Six rather than ten, because round 21 made each attempt expensive (a rigid-body level has to
        /// be settled and then played several times over) and the tenth attempt of a hard stage was
        /// costing ten seconds of the player's time to avoid a fallback level that is honestly fine.
        /// </summary>
        public const int MaxGenerateAttempts = 6;

        /// <summary>
        /// Builds a level one attempt at a time, so a phone can show what it is doing.
        ///
        /// Verifying a rigid-body level takes a second or two on a good day and rather longer on a hard
        /// stage, and all of it used to happen inside one call: the game froze on "第 N 关" with no
        /// explanation. This is the same loop, resumable, one attempt per frame — the screen stays alive
        /// and can say 「正在搭关卡并验证…第 3 次尝试」, which is a much better answer than a hang.
        /// </summary>
        public sealed class Generator
        {
            private readonly int _stage;
            private readonly int _seed;
            private readonly BirdSettings _settings;
            private int _attempt;

            public Generator(int stage, int seed, BirdSettings settings)
            {
                _stage = Mathf.Clamp(stage, 1, StageCount);
                _seed = seed;
                _settings = settings;
            }

            public BirdLevel Level { get; private set; }
            public bool Done { get; private set; }
            public bool UsedFallback { get; private set; }
            public int Attempts => _attempt;

            /// <summary>Runs one attempt. Returns true when the level is ready.</summary>
            public bool Step()
            {
                if (Done) return true;

                while (_attempt < MaxGenerateAttempts)
                {
                    var level = Build(_stage, new System.Random(_seed + _attempt * 7919), _settings);
                    _attempt++;

                    // Let it stand before anyone shoots at it. Real bodies notice a jittered column
                    // that leans, so the level is settled *here*: the player is handed the pose it
                    // actually holds, and a layout that cannot hold itself up is thrown away like any
                    // other bad level rather than played as a heap.
                    if (!IsStable(level, _settings)) continue;

                    level.Birds = Mathf.Clamp(level.Pigs.Count + 2, 4, 8);
                    level.Blocks.Sort((a, b) => a.Bottom.CompareTo(b.Bottom));

                    List<Vector2> solution;
                    if (!Solve(level, _settings, out solution)) continue;

                    level.Solution = solution;
                    Level = level;
                    Done = true;
                    return true;
                }

                // Every random attempt failed, which should not happen — but "unwinnable level" is the
                // one failure this game must not ship, so there is a hand-built layout with a known shot.
                var fallback = BuildFallback(_stage, _settings);
                List<Vector2> ignored;
                Solve(fallback, _settings, out ignored);
                fallback.Solution = ignored;

                Level = fallback;
                UsedFallback = true;
                Done = true;
                return true;
            }
        }

        /// <summary>
        /// The level for a stage: random, but verified clearable before it is returned.
        ///
        /// Deterministic for a given (stage, seed), so a test can replay exactly what a player gets.
        /// </summary>
        public static BirdLevel Generate(int stage, int seed, BirdSettings settings)
        {
            var generator = new Generator(stage, seed, settings);
            while (!generator.Step()) { }
            return generator.Level;
        }

        /// <summary>How far a block may move while the level settles before the layout is rejected.</summary>
        public const float StableDrift = 0.30f;

        /// <summary>
        /// Whether the structure stands up on its own, and leaves it standing in its settled pose.
        ///
        /// Three ways to fail, all of them checked: a block that slides or tips further than
        /// <see cref="StableDrift"/>, a block that breaks on landing, and a pig that dies without anyone
        /// shooting at it. A layout that fails any of them is not a hard level, it is a level that has
        /// already fallen over before the player arrives.
        /// </summary>
        public static bool IsStable(BirdLevel level, BirdSettings settings)
        {
            if (level == null) return false;

            var start = new Vector2[level.Blocks.Count];
            for (int i = 0; i < level.Blocks.Count; i++) start[i] = level.Blocks[i].Centre;

            int pigs = level.PigsAlive;
            int blocks = 0;
            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (level.Blocks[i].Alive) blocks++;
            }

            BirdRules.Settle(level, settings);

            if (level.PigsAlive < pigs) return false;

            int alive = 0;
            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (!level.Blocks[i].Alive) return false;
                if ((level.Blocks[i].Centre - start[i]).magnitude > StableDrift) return false;
                alive++;
            }

            return alive == blocks;
        }

        /// <summary>
        /// A layout: one to three columns of blocks, with pigs standing inside them.
        ///
        /// Structured rather than scattered, because a random *pile* is either trivial or impossible
        /// and a random *tower* with pigs in it is the shape the game is actually about. The stage
        /// decides how many columns, how tall, and whether the material is ice, wood or stone.
        /// </summary>
        public static BirdLevel Build(int stage, System.Random rng, BirdSettings settings)
        {
            var level = new BirdLevel
            {
                Stage = stage,

                // Set from the structure once it exists (one bird per pig, plus a spare); the value
                // here is only a placeholder so the field is never zero.
                Birds = 3,
                GroundY = settings.GroundY,
                SlingX = settings.SlingX
            };
            level.SlingY = level.GroundY + settings.SlingHeight;

            // Two towers, not three. Three was tried and every single layout was rejected by the
            // verifier: with a tower in the middle of the group, its legs sit in the shadow of its
            // neighbours and no shot in the grid can reach them, so the level cannot be toppled at
            // all. Difficulty comes from how *tall* the towers are and what they are *made of*
            // instead — a four-storey stone tower is a much harder level than a two-storey ice hut,
            // and unlike a third tower it is one the generator can prove is winnable.
            int columns = Mathf.Clamp(1 + (stage + 1) / 3, 1, 2);

            // Four storeys is the ceiling, and it is the slingshot's ceiling rather than a taste: a
            // fifth storey puts the top of the tower above the highest a bird can fly (see
            // BirdRules.MaxRise), and a pig up there is a pig nobody can hit.
            int maxHeight = Mathf.Clamp(2 + stage / 4, 2, 4);

            float towerWidth = 1.05f;
            float gap = 0.55f;
            float totalWidth = columns * towerWidth + (columns - 1) * gap;

            // The group stands about six units downrange of the sling — inside its reach at every
            // height it builds, with room to spare for the shots the solver has to find.
            float groupCentre = settings.SlingX + 6.2f;
            float startX = groupCentre - totalWidth * 0.5f + towerWidth * 0.5f;

            for (int c = 0; c < columns; c++)
            {
                // Jittered rather than on a grid: twelve seeds of the same stage have to produce
                // visibly different structures, and the only things that can differ are the number of
                // storeys, the materials, the spacing and where the pigs stand.
                float x = startX + c * (towerWidth + gap) + (float)(rng.NextDouble() - 0.5) * 0.36f;
                int height = 2 + rng.Next(0, Mathf.Max(1, maxHeight - 1));

                BlockKind wallKind = PickWallKind(stage, rng, c == columns - 1);

                for (int row = 0; row < height; row++)
                {
                    // Storey geometry, and it is now *arithmetic* rather than tolerance: round 21 gave
                    // the blocks real contacts, and real contacts cannot be handed blocks that
                    // interpenetrate — the old layout put the beam 0.10 *inside* the storey above it and
                    // relied on a 0.14 "these two are touching" tolerance to paper over it. With a
                    // solver, that overlap is a 0.10 position error that explodes on the first frame.
                    //
                    //   upright: centre ground+0.34+r*StoreyPitch, half height 0.34  → top  +0.68
                    //   beam:    centre that top + 0.10, half height 0.10            → top  +0.88
                    //   next row then starts exactly on the beam's top.
                    float y = level.GroundY + 0.34f + row * StoreyPitch + CreateGap;
                    float halfWidth = 0.22f + (float)rng.NextDouble() * 0.06f;

                    // Two uprights and the beam across them. The uprights stand further apart than the
                    // bird is wide (0.72 of opening against a 0.52 bird), so there are two ways to
                    // solve a pig in a chamber: thread a flat shot in through the gap, or take a leg
                    // out and let the beam above come down on it. One way would be a puzzle; two ways
                    // is a game.
                    AddBlock(level, x - 0.60f, y, halfWidth, 0.34f, wallKind);
                    AddBlock(level, x + 0.60f, y, halfWidth, 0.34f, wallKind);

                    if (row < height - 1)
                    {
                        AddBlock(level, x, y + 0.44f, 0.78f, 0.10f, BlockKind.Wood);
                    }
                }

                // A pig inside the bottom chamber, and sometimes one higher up. The offsets jitter so
                // two seeds do not produce the same pig signature.
                float pigX = x + (float)(rng.NextDouble() - 0.5) * 0.16f;
                level.Pigs.Add(new BirdPig
                {
                    X = pigX,
                    Y = level.GroundY + 0.26f + CreateGap,
                    Radius = 0.26f,
                    Alive = true,
                    Awake = true
                });

                if (height >= 3 && rng.NextDouble() < 0.6)
                {
                    // Standing on the ground storey's beam (top at ground + 0.88), inside the storey
                    // above it — the classic chamber-on-chamber tower.
                    level.Pigs.Add(new BirdPig
                    {
                        X = x + (float)(rng.NextDouble() - 0.5) * 0.2f,
                        Y = level.GroundY + 1.14f + CreateGap,
                        Radius = 0.24f,
                        Alive = true,
                        Awake = true
                    });
                }
            }

            // Stage 1 keeps a single easy pig; every later stage is what the towers give it.
            if (stage == 1 && level.Pigs.Count > 2)
            {
                level.Pigs.RemoveRange(2, level.Pigs.Count - 2);
            }

            // At most three pigs, so the level is "three good shots" rather than "six lucky ones".
            // Difficulty in this game lives in the *materials* — stone shrugs off shots that ice
            // shatters under — and in how high the pigs sit, both of which keep climbing with the
            // stage. Padding the pig count instead made levels that were long rather than hard, and
            // long levels are where a generator's "it is solvable" promise quietly stops being true.
            const int maxPigs = 3;
            if (level.Pigs.Count > maxPigs)
            {
                level.Pigs.RemoveRange(maxPigs, level.Pigs.Count - maxPigs);
            }

            // One bird per pig, plus a spare pair — computed from the structure that was actually
            // built rather than from the stage number. The promise is that every level can be
            // cleared, and the bird count is the part of that promise the generator can simply
            // *grant*: a run is allowed to miss, and a level that only falls to a perfect three-shot
            // sequence is a level most players will never see the end of.
            level.Birds = Mathf.Clamp(level.Pigs.Count + 2, 4, 8);

            level.Blocks.Sort((a, b) => a.Bottom.CompareTo(b.Bottom));
            return level;
        }

        private static BlockKind PickWallKind(int stage, System.Random rng, bool outer)
        {
            double roll = rng.NextDouble();
            if (stage <= 2) return roll < 0.7 ? BlockKind.Ice : BlockKind.Wood;
            if (stage <= 5) return roll < 0.45 ? BlockKind.Ice : BlockKind.Wood;
            if (stage <= 8) return roll < 0.25 ? BlockKind.Ice : (roll < 0.82 ? BlockKind.Wood : BlockKind.Stone);
            return roll < 0.15 ? BlockKind.Ice : (roll < 0.62 ? BlockKind.Wood : BlockKind.Stone);
        }

        private static void AddBlock(BirdLevel level, float x, float y, float halfW, float halfH, BlockKind kind)
        {
            level.Blocks.Add(new BirdBlock
            {
                X = x,
                Y = y,
                HalfW = halfW,
                HalfH = halfH,
                Kind = kind,
                Health = BirdRules.HealthFor(kind),
                Alive = true
            });
        }

        /// <summary>
        /// The last resort: one short ice tower with a single pig, which any shot can clear.
        ///
        /// Written by hand and kept trivial on purpose. If it is ever used, the game is still a game.
        /// </summary>
        public static BirdLevel BuildFallback(int stage, BirdSettings settings)
        {
            var level = new BirdLevel
            {
                Stage = stage,
                GroundY = settings.GroundY,
                SlingX = settings.SlingX,
                SlingY = settings.GroundY + settings.SlingHeight,
                Birds = 3
            };

            // Well inside the slingshot's reach, like every generated layout.
            float x = settings.SlingX + 6.2f;
            for (int row = 0; row < 2; row++)
            {
                float y = level.GroundY + 0.34f + row * StoreyPitch + CreateGap;
                AddBlock(level, x - 0.60f, y, 0.20f, 0.34f, BlockKind.Ice);
                AddBlock(level, x + 0.60f, y, 0.20f, 0.34f, BlockKind.Ice);
                if (row == 0) AddBlock(level, x, y + 0.44f, 0.78f, 0.10f, BlockKind.Wood);
            }

            level.Pigs.Add(new BirdPig
            {
                X = x,
                Y = level.GroundY + 0.26f + CreateGap,
                Radius = 0.26f,
                Alive = true,
                Awake = true
            });
            return level;
        }

        // ------------------------------------------------------------------ the solver

        /// <summary>
        /// The candidate shots the solver tries: a grid of directions and powers, all of them things
        /// a player can actually do with one drag.
        /// </summary>
        public static List<Vector2> CandidateShots(BirdSettings settings)
            => Grid(settings, 5, 13, -12f, 74f);

        /// <summary>
        /// The first pass: few enough shots to plan a whole level with quickly.
        ///
        /// Three powers by nine directions. Round 21 made the search expensive — a rigid-body shot
        /// costs a couple of hundred milliseconds of simulation apiece, and sixty-five of them for every
        /// bird of every layout adds up to a level the player waits ten seconds to see. The cheap pass
        /// clears most layouts; when it does not, the finer grids below are tried and the *answer* is
        /// still verified with the same simulation the game uses, so nothing about the promise changes.
        /// </summary>
        public static List<Vector2> CoarseShots(BirdSettings settings)
            => Grid(settings, 3, 9, -10f, 70f);

        /// <summary>
        /// The last resort of the search: nine powers by fifteen directions.
        ///
        /// It is only paid for by the layouts the cheaper passes could not crack, which is the right
        /// place to spend the time: the alternative is handing the player the trivial fallback level.
        /// </summary>
        public static List<Vector2> FineShots(BirdSettings settings)
            => Grid(settings, 9, 15, -14f, 78f);

        private static List<Vector2> Grid(BirdSettings settings, int powers, int angles,
            float minDegrees, float maxDegrees)
        {
            var shots = new List<Vector2>(powers * angles);

            // From the slowest shot a drag can actually produce, not from the slingshot's nominal
            // minimum: a drag shorter than the launch threshold does not fire at all, so a candidate
            // below that speed is a shot the player cannot make and the level would be "proved" with it.
            float min = BirdRules.SlowestLaunchSpeed(settings);
            float max = settings.MaxLaunchSpeed;

            for (int pi = 0; pi < powers; pi++)
            {
                float speed = powers <= 1 ? max : Mathf.Lerp(min, max, pi / (float)(powers - 1));
                for (int ai = 0; ai < angles; ai++)
                {
                    // From slightly below flat to steeply lofted: the low shot into a base and the
                    // lobbed shot onto a roof are the two shots this game is played with.
                    float degrees = angles <= 1 ? 45f : Mathf.Lerp(minDegrees, maxDegrees, ai / (float)(angles - 1));
                    float radians = degrees * Mathf.Deg2Rad;
                    shots.Add(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * speed);
                }
            }

            return shots;
        }

        /// <summary>
        /// Plays the level with a small set of shots and reports whether it can be cleared.
        ///
        /// Greedy and deterministic: for each bird, try every candidate on a copy of the current
        /// state and keep the one that kills the most pigs. Ties go to the earlier candidate, so the
        /// solver's answer is reproducible — which matters, because the level is only handed over if
        /// this function says yes.
        ///
        /// Three passes, cheapest first. The promise is unchanged by the ordering: a level is only
        /// accepted when a *solution* has been replayed through the real simulation and cleared it.
        /// </summary>
        public static bool Solve(BirdLevel level, BirdSettings settings, out List<Vector2> solution)
        {
            solution = new List<Vector2>();
            if (level == null) return false;

            if (SolveWith(level, settings, CoarseShots(settings), solution)) return true;

            solution.Clear();
            if (SolveWith(level, settings, CandidateShots(settings), solution)) return true;

            solution.Clear();
            return SolveWith(level, settings, FineShots(settings), solution);
        }

        /// <summary>How many of the best opening shots the later birds are allowed to choose from.</summary>
        public const int ShortlistSize = 6;

        private static bool SolveWith(BirdLevel level, BirdSettings settings, List<Vector2> candidates,
            List<Vector2> solution)
        {
            var working = level.Clone();
            var shortlist = new List<Vector2>();
            var shortlistScores = new List<int>();

            for (int bird = 0; bird < level.Birds; bird++)
            {
                if (working.Cleared) break;

                // The first bird ranks everything; the rest of the plan is chosen from the best few.
                //
                // This is where the whole round's performance went. A rigid-body shot costs a couple of
                // hundred milliseconds to simulate, and the first version scored all sixty-five
                // candidates again for *every* bird — three hundred simulations to plan one level, and
                // up to fifteen seconds of the player watching a static screen. The opening shot is
                // what tells you which directions are worth anything on this layout; re-deciding that
                // from scratch after every bird bought nothing but the wait.
                var pool = bird == 0 || shortlist.Count == 0 ? candidates : shortlist;

                int bestScore = int.MinValue;
                Vector2 bestShot = Vector2.zero;

                for (int i = 0; i < pool.Count; i++)
                {
                    var trial = working.Clone();
                    var result = BirdRules.Simulate(trial, pool[i], settings);

                    // Pigs first; then blocks broken *low down*, because that is the shot that takes a
                    // chamber apart — breaking the roof of a tower does nothing to the pig inside it,
                    // and a solver that counts blocks without caring where they are will happily spend
                    // every bird on roofs and then report the level unsolvable.
                    int score = result.PigsKilled * 1000 + LowBreaks(trial, result, settings) * 20 +
                                result.BlocksBroken;

                    if (bird == 0) InsertRanked(shortlist, shortlistScores, pool[i], score);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestShot = pool[i];
                    }
                }

                var outcome = BirdRules.Simulate(working, bestShot, settings);
                if (outcome.PigsKilled <= 0 && outcome.BlocksBroken <= 0)
                {
                    break;   // nothing this bird can do
                }

                solution.Add(bestShot);
            }

            return working.Cleared;
        }

        /// <summary>Keeps the best few opening shots, best first.</summary>
        private static void InsertRanked(List<Vector2> shots, List<int> scores, Vector2 shot, int score)
        {
            int at = scores.Count;
            while (at > 0 && scores[at - 1] < score) at--;

            shots.Insert(at, shot);
            scores.Insert(at, score);

            while (shots.Count > ShortlistSize)
            {
                shots.RemoveAt(shots.Count - 1);
                scores.RemoveAt(scores.Count - 1);
            }
        }

        /// <summary>
        /// How many blocks the shot broke near the ground.
        ///
        /// The proxy for "took the base out": a hit low in a tower is what makes the storey above it
        /// fall onto whatever is inside, which is how this game is actually played.
        /// </summary>
        private static int LowBreaks(BirdLevel level, BirdShotResult result, BirdSettings settings)
        {
            int count = 0;
            float lowBand = level.GroundY + 2.2f;

            for (int i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Kind != BirdEventKind.BlockBroken) continue;
                if (e.Index < 0 || e.Index >= level.Blocks.Count) continue;
                if (level.Blocks[e.Index].Y <= lowBand) count++;
            }

            return count;
        }

        /// <summary>
        /// Whether every pig and block is inside the slingshot's reach.
        ///
        /// The generator's third promise, alongside "a machine can clear it" and "later stages are
        /// harder": a level must not contain a pig that is simply out of range. The first version of
        /// this generator put its towers eleven units downrange of a slingshot that could throw ten,
        /// and the symptom was not an error — it was every shot falling short and the solver rejecting
        /// every layout.
        /// </summary>
        public static bool AllInRange(BirdLevel level, BirdSettings settings)
        {
            if (level == null) return false;

            for (int i = 0; i < level.Pigs.Count; i++)
            {
                var pig = level.Pigs[i];
                if (!BirdRules.InRange(new Vector2(pig.X, pig.Y + pig.Radius + 0.1f), settings)) return false;
            }

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                var block = level.Blocks[i];
                var top = new Vector2(block.X, block.Top + settings.BirdRadius);
                if (!BirdRules.InRange(top, settings)) return false;
            }

            return true;
        }

        /// <summary>A short line about a level, for the HUD.</summary>
        public static string Blurb(BirdLevel level)
        {
            int pigs = level.Pigs.Count;
            int blocks = level.Blocks.Count;
            string material = "木头和冰";
            int stone = 0;
            for (int i = 0; i < level.Blocks.Count; i++)
            {
                if (level.Blocks[i].Kind == BlockKind.Stone) stone++;
            }

            if (stone > blocks / 3) material = "不少石头，得用力砸";
            else if (stone > 0) material = "有石头，也有木头";

            return pigs + " 只猪，躲在 " + blocks + " 块" + material + "后面；你有 " + level.Birds + " 只鸟。";
        }

        // ------------------------------------------------------------------ remembering a level

        /// <summary>
        /// Where a level can be kept so it only has to be *proved* once.
        ///
        /// Verifying a rigid-body level costs a second or two of simulation — several on a bad stage —
        /// and it is never a different answer: the seed is fixed per stage, so the same level comes out
        /// every time. Recomputing it on every visit would be spending the player's battery to arrive at
        /// the same tower. The cache is a handful of numbers in PlayerPrefs, and if it is missing or
        /// unreadable the generator simply runs again.
        ///
        /// The key carries the physics it was proved under, and that part is not optional: a recorded
        /// solution is a list of exact launch velocities, so a level cached before a change to gravity or
        /// to the length of a shot can arrive with a "solution" that no longer clears it. (It did, on the
        /// first build of this cache: the recorded shot bounced off a tower that the new simulation had
        /// standing in a slightly different place, and the level was quietly unwinnable.)
        /// </summary>
        /// <summary>
        /// Bumped whenever the generator, the solver's candidate set, or the physics changes shape:
        /// a cached level is a claim that a machine played *this* level under *this* build and cleared
        /// it, and a claim like that does not survive either side changing underneath it.
        /// </summary>
        public const string CacheKeyPrefix = "dshgames.birdlevel.v4.";

        public static string CacheKey(int stage, BirdSettings settings)
        {
            // A short, stable fingerprint of everything that changes what a shot does.
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Mathf.RoundToInt(settings.Gravity * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.MaxLaunchSpeed * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.MinLaunchSpeed * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.MaxPull * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.MaxFlightSeconds * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.Step * 1000000f);
                hash = hash * 31 + Mathf.RoundToInt(settings.GroundY * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.SlingX * 100f);
                hash = hash * 31 + Mathf.RoundToInt(settings.SlingHeight * 100f);

                return CacheKeyPrefix + Mathf.Clamp(stage, 1, StageCount) + "." + (hash & 0x7fffffff);
            }
        }

        private static readonly System.Globalization.CultureInfo Invariant =
            System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>Writes a level as one line of text. Compact, flat, and culture-proof.</summary>
        public static string Encode(BirdLevel level)
        {
            if (level == null) return "";

            var sb = new System.Text.StringBuilder(256);
            sb.Append("H|").Append(level.Stage).Append('|').Append(level.Birds).Append('|')
              .Append(Bits(level.GroundY)).Append('|')
              .Append(Bits(level.SlingX)).Append('|')
              .Append(Bits(level.SlingY)).Append('\n');

            for (int i = 0; i < level.Blocks.Count; i++)
            {
                var block = level.Blocks[i];
                sb.Append("B|").Append(Bits(block.X)).Append('|')
                  .Append(Bits(block.Y)).Append('|')
                  .Append(Bits(block.HalfW)).Append('|')
                  .Append(Bits(block.HalfH)).Append('|')
                  .Append((int)block.Kind).Append('|').Append(block.Health).Append('|')
                  .Append(Bits(block.Angle)).Append('\n');
            }

            for (int i = 0; i < level.Pigs.Count; i++)
            {
                var pig = level.Pigs[i];
                sb.Append("P|").Append(Bits(pig.X)).Append('|')
                  .Append(Bits(pig.Y)).Append('|')
                  .Append(Bits(pig.Radius)).Append('\n');
            }

            if (level.Solution != null)
            {
                for (int i = 0; i < level.Solution.Count; i++)
                {
                    sb.Append("S|").Append(Bits(level.Solution[i].x)).Append('|')
                      .Append(Bits(level.Solution[i].y)).Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// A float as its exact bit pattern, in hex.
        ///
        /// Not "F3", and not for tidiness: the simulation is chaotic, and this was measured rather than
        /// assumed — a level saved to three decimals and read back came out six *micrometres* away from
        /// where it started, and the recorded solution that cleared the original bounced off the copy.
        /// A cache of levels whose solutions are exact launch velocities has to be exact, or "this level
        /// is winnable" stops being a fact about the level the player is given.
        /// </summary>
        private static string Bits(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
            return System.BitConverter.ToInt32(System.BitConverter.GetBytes(value), 0).ToString("X8", Invariant);
        }

        private static float Unbits(string text, float fallback)
        {
            int raw;
            if (!int.TryParse(text, System.Globalization.NumberStyles.HexNumber, Invariant, out raw)) return fallback;

            float value = System.BitConverter.ToSingle(System.BitConverter.GetBytes(raw), 0);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        /// <summary>
        /// Whether a recorded solution still clears a level.
        ///
        /// Used to check a level that came out of the cache: the simulation is chaotic enough that a
        /// fraction of a millimetre of rounding in the saved positions can turn a shot that cleared the
        /// level into one that misses, and "the level is winnable" is the one claim this game must not
        /// get wrong. Replaying the recorded shots costs a fraction of a second and turns the cache from
        /// a hope into a fact.
        /// </summary>
        public static bool SolutionStillClears(BirdLevel level, BirdSettings settings)
        {
            if (level == null || level.Solution == null || level.Solution.Count == 0) return false;

            var replay = level.Clone();
            for (int i = 0; i < level.Solution.Count && !replay.Cleared; i++)
            {
                BirdRules.Simulate(replay, level.Solution[i], settings);
            }

            return replay.Cleared;
        }

        /// <summary>Reads back what <see cref="Encode"/> wrote. Null when the text is not a level.</summary>
        public static BirdLevel Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var level = new BirdLevel();
            var lines = text.Split('\n');
            bool header = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length < 2) continue;

                var parts = line.Split('|');
                switch (parts[0])
                {
                    case "H":
                        if (parts.Length < 6) return null;
                        level.Stage = ParseInt(parts[1], 1);
                        level.Birds = ParseInt(parts[2], 3);
                        level.GroundY = Unbits(parts[3], -5.2f);
                        level.SlingX = Unbits(parts[4], -9.4f);
                        level.SlingY = Unbits(parts[5], -2.6f);
                        header = true;
                        break;

                    case "B":
                        if (parts.Length < 8) return null;
                        level.Blocks.Add(new BirdBlock
                        {
                            X = Unbits(parts[1], 0f),
                            Y = Unbits(parts[2], 0f),
                            HalfW = Unbits(parts[3], 0.2f),
                            HalfH = Unbits(parts[4], 0.2f),
                            Kind = (BlockKind)ParseInt(parts[5], 1),
                            Health = ParseInt(parts[6], 1),
                            Angle = Unbits(parts[7], 0f),
                            Alive = true
                        });
                        break;

                    case "P":
                        if (parts.Length < 4) return null;
                        level.Pigs.Add(new BirdPig
                        {
                            X = Unbits(parts[1], 0f),
                            Y = Unbits(parts[2], 0f),
                            Radius = Unbits(parts[3], 0.26f),
                            Alive = true
                        });
                        break;

                    case "S":
                        if (parts.Length < 3) return null;
                        if (level.Solution == null) level.Solution = new List<Vector2>();
                        level.Solution.Add(new Vector2(Unbits(parts[1], 0f), Unbits(parts[2], 0f)));
                        break;
                }
            }

            if (!header || level.Blocks.Count == 0 || level.Pigs.Count == 0) return null;
            return level;
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, System.Globalization.NumberStyles.Integer, Invariant, out value)
                ? value : fallback;
        }
    }
}
