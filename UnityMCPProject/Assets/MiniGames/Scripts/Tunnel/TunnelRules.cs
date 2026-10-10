using UnityEngine;

namespace DshMiniGames
{
    /// <summary>How the tunnel runner is played.</summary>
    public enum TunnelMode { Survival, Mine }

    /// <summary>A colour scheme for the tunnel, picked at random per run.</summary>
    public enum TunnelTheme { Neon, Ice, Lava, Forest }

    /// <summary>The shape of a gate hole. Adds variety to survival mode.</summary>
    public enum GateShape { Circle, Square, Triangle, Semicircle }

    /// <summary>
    /// The tunnel runner's arithmetic: where gates and obstacles go, whether the ship can reach
    /// them, and how a run scores. Pure and testable, like every other mini-game's rules.
    /// </summary>
    public static class TunnelRules
    {
        /// <summary>The ship and the tunnel cross-section live in this radius, in world units.</summary>
        public const float TunnelRadius = 1.9f;

        /// <summary>Ship collision radius.</summary>
        public const float ShipRadius = 0.24f;

        /// <summary>How fast the ship can move in the cross-section, in tunnel units per second.</summary>
        public const float ShipSpeed = 2.6f;

        /// <summary>Radius of a gate hole the ship flies through. Kept as the comfortable default;
        /// live difficulty shrinks it via <see cref="GateHoleRadiusFor"/>.</summary>
        public const float GateHoleRadius = 0.52f;

        /// <summary>Radius of a mine obstacle.</summary>
        public const float MineRadius = 0.34f;

        /// <summary>Distance ahead at which the next obstacle spawns.</summary>
        public const float SpawnAhead = 18f;

        /// <summary>How far two successive obstacles are apart along the tunnel. Wide enough that
        /// an obstacle never lingers between the camera and the ship after it has passed.</summary>
        public const float Spacing = 6.5f;

        /// <summary>How fast the tunnel scrolls past the ship, in tunnel units per second.</summary>
        public const float ForwardSpeed = 7f;

        /// <summary>Seconds between two successive obstacles reaching the ship.</summary>
        public const float TimeBetweenObstacles = Spacing / ForwardSpeed;

        /// <summary>
        /// The gate hole radius at a given score. Starts wide and easy, shrinks as the run goes on,
        /// so the difficulty ramps gradually rather than hitting the player at full strength.
        /// </summary>
        public static float GateHoleRadiusFor(int score)
            => Mathf.Lerp(0.85f, 0.44f, Mathf.Clamp01(score / 30f));

        /// <summary>Mine radius at a given score: slightly smaller early, slightly larger late.</summary>
        public static float MineRadiusFor(int score)
            => Mathf.Lerp(0.40f, 0.30f, Mathf.Clamp01(score / 40f));

        /// <summary>
        /// Clamps a ship position into the tunnel cross-section, which is a circle: a ship at the
        /// corner of a square tunnel would have a diagonal the circle does not allow.
        /// </summary>
        public static Vector2 ClampToTunnel(Vector2 position)
        {
            float limit = TunnelRadius - ShipRadius;
            if (position.sqrMagnitude <= limit * limit) return position;
            return position.normalized * limit;
        }

        /// <summary>
        /// The next gate hole position, reachable from <paramref name="from"/> (the previous hole,
        /// not the ship's current spot). The reachability rule is the fairness promise: two
        /// consecutive holes are never farther apart than the ship can fly in the time between
        /// them, so no two gates can be an impossible pair.
        /// </summary>
        public static Vector2 NextGateHole(Vector2 from, float roll1, float roll2, float holeRadius)
        {
            float maxJump = ShipSpeed * TimeBetweenObstacles;
            float angle = roll1 * Mathf.PI * 2f;
            float radius = Mathf.Lerp(0f, TunnelRadius - holeRadius - 0.15f, Mathf.Sqrt(roll2));
            var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var delta = target - from;
            if (delta.magnitude > maxJump) target = from + delta.normalized * maxJump;
            return ClampToTunnel(target);
        }

        /// <summary>
        /// A mine obstacle position. Most mines aim near the ship — holding still must get you
        /// hit — while the rest scatter for variety. The ship has SpawnAhead / ForwardSpeed
        /// seconds to move, so even a close mine is fair to dodge.
        /// </summary>
        public static Vector2 NextMine(Vector2 from, float roll1, float roll2)
        {
            float angle = roll1 * Mathf.PI * 2f;
            if (roll2 < 0.6f)
            {
                // Aim near the ship: standing still is a hit, moving dodges it.
                float offset = Mathf.Lerp(0.05f, 0.35f, roll2 / 0.6f);
                var aim = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * offset;
                return ClampToTunnel(aim);
            }
            // The rest scatter anywhere in the tunnel for variety.
            float radius = Mathf.Lerp(0.1f, TunnelRadius - MineRadius - 0.1f, (roll2 - 0.6f) / 0.4f);
            var at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            return ClampToTunnel(at);
        }

        /// <summary>Whether the ship overlaps a gate hole (survival) or an obstacle (mine).</summary>
        public static bool Collides(Vector2 ship, Vector2 thing, float thingRadius)
            => Vector2.Distance(ship, thing) < thingRadius;

        // ---------------------------------------------------------------- gate hole shapes

        /// <summary>
        /// Which hole shape to use. Early gates are all circles; as the score rises, squares,
        /// triangles and semicircles join the mix, so the difficulty of *reading* the hole grows too.
        /// </summary>
        public static GateShape GateShapeFor(int score, int index)
        {
            int variety = score < 5 ? 1 : score < 12 ? 2 : score < 22 ? 3 : 4;
            return (GateShape)(((index % variety) + variety) % variety);
        }

        /// <summary>Whether the ship is inside the hole of the given shape (i.e. it passes).</summary>
        public static bool IsInsideHole(Vector2 ship, Vector2 hole, GateShape shape, float radius)
        {
            Vector2 d = ship - hole;
            switch (shape)
            {
                case GateShape.Square:
                    return Mathf.Abs(d.x) < radius && Mathf.Abs(d.y) < radius;
                case GateShape.Triangle:
                    return PointInTriangle(d,
                        new Vector2(0f, radius),
                        new Vector2(-0.866f * radius, -0.5f * radius),
                        new Vector2(0.866f * radius, -0.5f * radius));
                case GateShape.Semicircle:
                    return d.sqrMagnitude < radius * radius && d.y > 0f;
                default:
                    return d.sqrMagnitude < radius * radius;
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        /// <summary>
        /// The boundary points of a hole shape (relative to the hole centre), for drawing the glowing
        /// rim and the spokes that make it read as a wall with a shaped hole cut in it.
        /// </summary>
        public static Vector2[] GateShapePoints(GateShape shape, float radius, int count)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            switch (shape)
            {
                case GateShape.Square:
                    pts.Add(new Vector2(-radius, -radius));
                    pts.Add(new Vector2(radius, -radius));
                    pts.Add(new Vector2(radius, radius));
                    pts.Add(new Vector2(-radius, radius));
                    break;
                case GateShape.Triangle:
                    pts.Add(new Vector2(0f, radius));
                    pts.Add(new Vector2(-0.866f * radius, -0.5f * radius));
                    pts.Add(new Vector2(0.866f * radius, -0.5f * radius));
                    break;
                case GateShape.Semicircle:
                    int half = Mathf.Max(2, count / 2);
                    for (int i = 0; i <= half; i++)
                    {
                        float a = Mathf.PI * i / half;
                        pts.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
                    }
                    pts.Add(new Vector2(-radius, 0f));
                    break;
                default:
                    for (int i = 0; i < count; i++)
                    {
                        float a = Mathf.PI * 2f * i / count;
                        pts.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
                    }
                    break;
            }
            return pts.ToArray();
        }

        // ---------------------------------------------------------------- sensitivity

        /// <summary>Clamps a sensitivity multiplier (how fast the ship answers the stick).</summary>
        public static float ClampSensitivity(float value) => Mathf.Clamp(value, 0.5f, 2f);

        /// <summary>Score for passing one gate / dodging one mine.</summary>
        public static int ScoreForPass(int gatesPassed) => Mathf.Max(0, gatesPassed);

        /// <summary>Coins a run is worth.</summary>
        public static int CoinsFor(int score) => Mathf.Max(0, score) / 3 + Mathf.Max(0, score - 10) / 5;

        /// <summary>The colour palette for a theme.</summary>
        public static Color TunnelColor(TunnelTheme theme, bool accent)
        {
            switch (theme)
            {
                case TunnelTheme.Ice: return accent ? new Color(0.45f, 0.85f, 1f) : new Color(0.16f, 0.30f, 0.46f);
                case TunnelTheme.Lava: return accent ? new Color(1f, 0.42f, 0.18f) : new Color(0.36f, 0.10f, 0.08f);
                case TunnelTheme.Forest: return accent ? new Color(0.5f, 0.95f, 0.5f) : new Color(0.06f, 0.24f, 0.14f);
                default: return accent ? new Color(0.35f, 0.95f, 1f) : new Color(0.10f, 0.08f, 0.22f);
            }
        }

        public static string ThemeName(TunnelTheme theme)
        {
            switch (theme)
            {
                case TunnelTheme.Ice: return "冰窟";
                case TunnelTheme.Lava: return "熔岩";
                case TunnelTheme.Forest: return "绿林";
                default: return "霓虹";
            }
        }
    }
}
