using UnityEngine;

namespace DshMiniGames
{
    /// <summary>How the tunnel runner is played.</summary>
    public enum TunnelMode { Survival, Mine }

    /// <summary>A colour scheme for the tunnel, picked at random per run.</summary>
    public enum TunnelTheme { Neon, Ice, Lava, Forest }

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

        /// <summary>Radius of a gate hole the ship flies through.</summary>
        public const float GateHoleRadius = 0.52f;

        /// <summary>Radius of a mine obstacle.</summary>
        public const float MineRadius = 0.34f;

        /// <summary>Distance ahead at which the next obstacle spawns.</summary>
        public const float SpawnAhead = 14f;

        /// <summary>How far two successive obstacles are apart along the tunnel.</summary>
        public const float Spacing = 4.6f;

        /// <summary>How fast the tunnel scrolls past the ship, in tunnel units per second.</summary>
        public const float ForwardSpeed = 7f;

        /// <summary>Seconds between two successive obstacles reaching the ship.</summary>
        public const float TimeBetweenObstacles = Spacing / ForwardSpeed;

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
        public static Vector2 NextGateHole(Vector2 from, float roll1, float roll2)
        {
            float maxJump = ShipSpeed * TimeBetweenObstacles;
            float angle = roll1 * Mathf.PI * 2f;
            float radius = Mathf.Lerp(0f, TunnelRadius - GateHoleRadius - 0.15f, Mathf.Sqrt(roll2));
            var target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var delta = target - from;
            if (delta.magnitude > maxJump) target = from + delta.normalized * maxJump;
            return ClampToTunnel(target);
        }

        /// <summary>A mine obstacle position, also reachable (dodgeable) from the ship's spot.</summary>
        public static Vector2 NextMine(Vector2 from, float roll1, float roll2)
        {
            float angle = roll1 * Mathf.PI * 2f;
            float radius = Mathf.Lerp(0.1f, TunnelRadius - MineRadius - 0.1f, roll2);
            var at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            // A mine too close to the ship would be undodgeable; push it to the far half.
            if (Vector2.Distance(at, from) < TunnelRadius * 0.8f)
            {
                at = -from.normalized * radius;
            }
            return ClampToTunnel(at);
        }

        /// <summary>Whether the ship overlaps a gate hole (survival) or an obstacle (mine).</summary>
        public static bool Collides(Vector2 ship, Vector2 thing, float thingRadius)
            => Vector2.Distance(ship, thing) < thingRadius;

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
