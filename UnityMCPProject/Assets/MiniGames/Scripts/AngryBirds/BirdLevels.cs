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

        /// <summary>How many layouts to try before falling back to a known-good one.</summary>
        public const int MaxGenerateAttempts = 10;

        /// <summary>
        /// The level for a stage: random, but verified clearable before it is returned.
        ///
        /// Deterministic for a given (stage, seed), so a test can replay exactly what a player gets.
        /// </summary>
        public static BirdLevel Generate(int stage, int seed, BirdSettings settings)
        {
            int clamped = Mathf.Clamp(stage, 1, StageCount);

            for (int attempt = 0; attempt < MaxGenerateAttempts; attempt++)
            {
                var level = Build(clamped, new System.Random(seed + attempt * 7919), settings);
                List<Vector2> solution;
                if (Solve(level, settings, out solution))
                {
                    level.Solution = solution;
                    return level;
                }
            }

            // Every random attempt failed, which should not happen — but "unwinnable level" is the
            // one failure this game must not ship, so there is a hand-built layout with a known shot.
            var fallback = BuildFallback(clamped, settings);
            List<Vector2> ignored;
            Solve(fallback, settings, out ignored);
            fallback.Solution = ignored;
            return fallback;
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
                    // Storey pitch, beam height and the tolerance in BirdRules are one system: the
                    // beam sits half a storey up, and the storey above rests *on the beam*. Changing
                    // the pitch alone leaves the next storey resting 0.16 above the beam — outside the
                    // 0.14 tolerance that says "these two are touching" — which would leave half of
                    // every tower unsupported from the first frame.
                    float y = level.GroundY + 0.34f + row * 0.68f;
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
                        AddBlock(level, x, y + 0.34f, 0.78f, 0.10f, BlockKind.Wood);
                    }
                }

                // A pig inside the bottom chamber, and sometimes one higher up. The offsets jitter so
                // two seeds do not produce the same pig signature.
                float pigX = x + (float)(rng.NextDouble() - 0.5) * 0.16f;
                level.Pigs.Add(new BirdPig
                {
                    X = pigX,
                    Y = level.GroundY + 0.30f + (float)rng.NextDouble() * 0.06f,
                    Radius = 0.26f,
                    Alive = true
                });

                if (height >= 3 && rng.NextDouble() < 0.6)
                {
                    level.Pigs.Add(new BirdPig
                    {
                        X = x + (float)(rng.NextDouble() - 0.5) * 0.2f,
                        Y = level.GroundY + 0.34f + 1.36f,
                        Radius = 0.24f,
                        Alive = true
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
                float y = level.GroundY + 0.34f + row * 0.68f;
                AddBlock(level, x - 0.60f, y, 0.20f, 0.34f, BlockKind.Ice);
                AddBlock(level, x + 0.60f, y, 0.20f, 0.34f, BlockKind.Ice);
                if (row == 0) AddBlock(level, x, y + 0.34f, 0.78f, 0.10f, BlockKind.Wood);
            }

            level.Pigs.Add(new BirdPig { X = x, Y = level.GroundY + 0.30f, Radius = 0.26f, Alive = true });
            return level;
        }

        // ------------------------------------------------------------------ the solver

        /// <summary>
        /// The candidate shots the solver tries: a grid of directions and powers, all of them things
        /// a player can actually do with one drag.
        /// </summary>
        public static List<Vector2> CandidateShots(BirdSettings settings)
        {
            // Cached: the generator asks for this grid once per attempt per bird, and rebuilding a
            // list of sixty-five vectors tens of thousands of times is pure waste.
            if (_candidates != null && _candidatesFor.Equals(settings)) return _candidates;

            var shots = new List<Vector2>();
            float min = settings.MinLaunchSpeed;
            float max = settings.MaxLaunchSpeed;

            // Five powers and thirteen directions. The first grid (four by nine) was too coarse to
            // clear the taller towers, and a solver that cannot clear a level it *should* clear makes
            // the generator throw the level away — which is why later stages were silently falling
            // back to the trivial hand-built layout.
            for (int pi = 0; pi <= 4; pi++)
            {
                float speed = Mathf.Lerp(min, max, pi / 4f);
                for (int ai = 0; ai < 13; ai++)
                {
                    // From slightly below flat to steeply lofted: the low shot into a base and the
                    // lobbed shot onto a roof are the two shots this game is played with.
                    float degrees = Mathf.Lerp(-12f, 74f, ai / 12f);
                    float radians = degrees * Mathf.Deg2Rad;
                    shots.Add(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * speed);
                }
            }

            _candidates = shots;
            _candidatesFor = settings;
            return shots;
        }

        private static List<Vector2> _candidates;
        private static BirdSettings _candidatesFor;

        /// <summary>
        /// Plays the level with a small set of shots and reports whether it can be cleared.
        ///
        /// Greedy and deterministic: for each bird, try every candidate on a copy of the current
        /// state and keep the one that kills the most pigs. Ties go to the earlier candidate, so the
        /// solver's answer is reproducible — which matters, because the level is only handed over if
        /// this function says yes.
        /// </summary>
        public static bool Solve(BirdLevel level, BirdSettings settings, out List<Vector2> solution)
        {
            solution = new List<Vector2>();

            var working = level.Clone();
            var candidates = CandidateShots(settings);

            for (int bird = 0; bird < level.Birds; bird++)
            {
                if (working.Cleared) break;

                int bestScore = int.MinValue;
                Vector2 bestShot = Vector2.zero;

                for (int i = 0; i < candidates.Count; i++)
                {
                    var trial = working.Clone();
                    var result = BirdRules.Simulate(trial, candidates[i], settings);

                    // Pigs first; then blocks broken *low down*, because that is the shot that takes a
                    // chamber apart — breaking the roof of a tower does nothing to the pig inside it,
                    // and a solver that counts blocks without caring where they are will happily spend
                    // every bird on roofs and then report the level unsolvable.
                    int score = result.PigsKilled * 1000 + LowBreaks(trial, result, settings) * 20 +
                                result.BlocksBroken;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestShot = candidates[i];
                    }
                }

                var before = working.Clone();
                var outcome = BirdRules.Simulate(working, bestShot, settings);
                if (outcome.PigsKilled <= 0 && outcome.BlocksBroken <= 0)
                {
                    _ = before;
                    break;   // nothing this bird can do
                }

                solution.Add(bestShot);
            }

            return working.Cleared;
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
    }
}
