using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>One flower to plant: where it goes, what colour it is, how tall, how many petals.</summary>
    public struct FlowerPlan
    {
        public Vector2 At;
        public Color Colour;
        public float Height;
        public int Petals;

        /// <summary>Facing, so a bed of flowers is not a row of identical stamps.</summary>
        public float Turn;
    }

    /// <summary>A patch of soil with flowers in it.</summary>
    public struct BedPlan
    {
        public Vector2 Centre;
        public float Radius;
    }

    /// <summary>
    /// The arithmetic of a garden and a terrace, worked out before anything is built.
    ///
    /// This lives apart from <see cref="PetRoom"/> for the usual reason in this project: the
    /// interesting decisions here are all *placement* decisions — how many fence posts a 14 m
    /// side takes, whether a flower lands inside the bathtub, whether the sag of a string of
    /// lights is symmetric — and those are exactly the things that go wrong silently and are
    /// checked by eye too late. As data, they can be tested without building a scene.
    ///
    /// Everything is deterministic from a seed: the garden looks hand-planted and different every
    /// time the player moves in, but the same seed gives the same garden, which is what makes the
    /// tests above possible at all.
    /// </summary>
    public static class RoomDecor
    {
        /// <summary>
        /// The garden's palette. Eight colours rather than the three downloaded flower props
        /// offer, because 「种满五颜六色的花」 is a request about colour range, and three tints of
        /// the same two shapes is a decoration, not a flower bed.
        /// </summary>
        public static readonly Color[] FlowerColours =
        {
            new Color(0.92f, 0.26f, 0.32f),   // 红
            new Color(0.98f, 0.62f, 0.20f),   // 橙
            new Color(0.97f, 0.86f, 0.28f),   // 黄
            new Color(0.56f, 0.78f, 0.30f),   // 绿（花心里的绿色小花）
            new Color(0.42f, 0.68f, 0.92f),   // 蓝
            new Color(0.66f, 0.46f, 0.88f),   // 紫
            new Color(0.96f, 0.56f, 0.78f),   // 粉
            new Color(0.96f, 0.95f, 0.90f)    // 白
        };

        /// <summary>Soil for a bed, so the flowers read as planted rather than dropped.</summary>
        public static readonly Color Soil = new Color(0.32f, 0.24f, 0.18f);
        public static readonly Color Grass = new Color(0.36f, 0.58f, 0.30f);
        public static readonly Color Path = new Color(0.72f, 0.68f, 0.58f);

        /// <summary>
        /// Evenly spaced posts around a square, corners included once.
        ///
        /// The spacing is *divided out* rather than stepped: a fence that ends with a
        /// three-quarter bay leaves a gap at the corner big enough to walk through, and that is
        /// the kind of thing nobody notices until the pet is standing outside its own garden.
        /// </summary>
        public static Vector2[] Perimeter(float size, float wantedSpacing)
        {
            float half = Mathf.Max(1f, size) * 0.5f;
            int bays = Mathf.Max(2, Mathf.RoundToInt(size / Mathf.Max(0.5f, wantedSpacing)));
            float step = size / bays;

            // Each side has `bays` segments, so the four sides together are 4×bays posts with
            // every corner counted exactly once. North carries both its corners; the following
            // sides start one step in and finish on their own far corner, and the last post plus
            // the first close the loop.
            var points = new List<Vector2>(bays * 4);

            for (int i = 0; i <= bays; i++) points.Add(new Vector2(-half + i * step, half));        // north
            for (int i = 1; i <= bays; i++) points.Add(new Vector2(half, half - i * step));          // east
            for (int i = 1; i <= bays; i++) points.Add(new Vector2(half - i * step, -half));         // south
            for (int i = 1; i < bays; i++) points.Add(new Vector2(-half, -half + i * step));         // west

            return points.ToArray();
        }

        /// <summary>Distance between two neighbours in a <see cref="Perimeter"/> run.</summary>
        public static float PerimeterSpacing(float size, float wantedSpacing)
            => size / Mathf.Max(2, Mathf.RoundToInt(size / Mathf.Max(0.5f, wantedSpacing)));

        /// <summary>
        /// Where the flower beds go.
        ///
        /// Against the fence and around the edges, deliberately: the middle of the room is where
        /// the bowls, the bed and the pet are, and a garden whose flowers are in the way of
        /// everything reads as clutter rather than as a garden.
        /// </summary>
        public static BedPlan[] Beds(float size)
        {
            float half = Mathf.Max(1f, size) * 0.5f;

            return new[]
            {
                new BedPlan { Centre = new Vector2(-half + 1.6f, half - 1.7f), Radius = 1.55f },  // 西北
                new BedPlan { Centre = new Vector2(half - 1.6f, half - 1.7f), Radius = 1.55f },   // 东北
                new BedPlan { Centre = new Vector2(-half + 1.6f, 0.2f), Radius = 1.45f },         // 西
                new BedPlan { Centre = new Vector2(half - 1.6f, 0.2f), Radius = 1.45f },          // 东
                new BedPlan { Centre = new Vector2(-half + 1.7f, -half + 1.6f), Radius = 1.35f }, // 西南
                new BedPlan { Centre = new Vector2(half - 1.7f, -half + 1.6f), Radius = 1.35f },  // 东南
                new BedPlan { Centre = new Vector2(0f, -half + 1.6f), Radius = 1.4f }             // 南（背对镜头）
            };
        }

        /// <summary>
        /// Flowers for one bed, packed by rejection sampling.
        ///
        /// Three rejections, all of them learned from what the first garden looked like:
        ///  - no flower inside a keep-out circle, which is how a rose bush ends up growing out of
        ///    the bathtub;
        ///  - no flower closer than <paramref name="minSpacing"/> to another, because evenly
        ///    random points clump and a clump of eight flowers looks like one flower;
        ///  - nothing outside the soil, so the bed's edge stays an edge.
        /// </summary>
        public static FlowerPlan[] Flowers(BedPlan bed, int seed, Vector2[] keepOut, float keepOutRadius,
            float minSpacing = 0.42f, int wanted = 9, int attemptsPerFlower = 40)
        {
            var rng = new System.Random(seed);
            var placed = new List<FlowerPlan>(Mathf.Max(1, wanted));

            for (int i = 0; i < wanted; i++)
            {
                bool ok = false;
                for (int attempt = 0; attempt < attemptsPerFlower && !ok; attempt++)
                {
                    // Square root keeps the samples even across the disc instead of crowding
                    // the middle, which is what a plain radius*random gives you.
                    double angle = rng.NextDouble() * System.Math.PI * 2.0;
                    double radius = System.Math.Sqrt(rng.NextDouble()) * bed.Radius * 0.92f;
                    var at = bed.Centre + new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * (float)radius;

                    if (Blocked(at, keepOut, keepOutRadius)) continue;

                    bool tooClose = false;
                    for (int j = 0; j < placed.Count && !tooClose; j++)
                    {
                        if (Vector2.Distance(placed[j].At, at) < minSpacing) tooClose = true;
                    }
                    if (tooClose) continue;

                    ok = true;
                    placed.Add(new FlowerPlan
                    {
                        At = at,
                        // Cycled with an offset per bed rather than drawn at random: a random
                        // colour from eight gives whole beds of one colour often enough to
                        // notice, and the request was 五颜六色.
                        Colour = FlowerColours[(placed.Count + seed) % FlowerColours.Length],
                        Height = Mathf.Lerp(0.30f, 0.62f, (float)rng.NextDouble()),
                        Petals = 5 + rng.Next(3),
                        Turn = (float)rng.NextDouble() * 360f
                    });
                }
            }

            return placed.ToArray();
        }

        /// <summary>True when a point is inside one of the keep-out circles.</summary>
        public static bool Blocked(Vector2 at, Vector2[] keepOut, float radius)
        {
            if (keepOut == null) return false;
            for (int i = 0; i < keepOut.Length; i++)
            {
                if (Vector2.Distance(at, keepOut[i]) < radius) return true;
            }
            return false;
        }

        /// <summary>
        /// The bulbs of a string of lights, sagging between two posts.
        ///
        /// A parabola rather than a catenary: at a two-metre span and a hand's width of sag the
        /// two are indistinguishable on a phone screen, and the parabola is one line that a test
        /// can check exactly.
        /// </summary>
        public static Vector3[] StringLights(Vector3 from, Vector3 to, int bulbs, float sag)
        {
            bulbs = Mathf.Max(2, bulbs);
            var points = new Vector3[bulbs];
            for (int i = 0; i < bulbs; i++)
            {
                float t = i / (float)(bulbs - 1);
                var point = Vector3.Lerp(from, to, t);
                point.y -= sag * 4f * t * (1f - t);
                points[i] = point;
            }
            return points;
        }

        /// <summary>Stepping stones between two points, wobbled so the path is not a ruler line.</summary>
        public static Vector2[] SteppingStones(Vector2 from, Vector2 to, int count, int seed, float wobble = 0.22f)
        {
            count = Mathf.Max(2, count);
            var rng = new System.Random(seed);
            var direction = (to - from).normalized;
            var side = new Vector2(-direction.y, direction.x);

            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float offset = (float)(rng.NextDouble() * 2.0 - 1.0) * wobble;
                points[i] = Vector2.Lerp(from, to, t) + side * offset;
            }
            return points;
        }

        /// <summary>
        /// The keep-out circles for a place: the furniture the room always has.
        ///
        /// Passed to <see cref="Flowers"/> rather than baked into it so the room can hand over
        /// its own list — and so a test can hand over a known one and check the rule directly.
        /// </summary>
        public static Vector2[] FurnitureKeepOut()
        {
            return new[]
            {
                new Vector2(-4.4f, -4.4f),   // 小床
                new Vector2(-5.2f, 4.6f),    // 澡盆
                new Vector2(-1.6f, 4.6f),    // 梳子
                new Vector2(1.8f, 4.2f),     // 球
                new Vector2(5.1f, -5.0f),    // 猫砂盆
                new Vector2(2.6f, 3.4f),     // 饭碗
                new Vector2(3.9f, 3.4f),     // 水碗
                new Vector2(0f, 6.2f)        // 门
            };
        }
    }
}
