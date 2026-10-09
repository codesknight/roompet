using UnityEngine;

namespace DshMiniGames
{
    /// <summary>Everything the hop game's numbers are.</summary>
    public struct HopSettings
    {
        /// <summary>How far the charge grows per second of holding, in world units.</summary>
        public float ChargePerSecond;

        /// <summary>The shortest hop a tap produces.</summary>
        public float MinDistance;

        /// <summary>The longest hop, however long the player holds.</summary>
        public float MaxDistance;

        /// <summary>How close to the middle of a box counts as 完美.</summary>
        public float PerfectRadius;

        /// <summary>Half-width of the boxes, so the landing test knows what "on the box" means.</summary>
        public float BoxHalfSize;

        /// <summary>Shortest and longest gap between two boxes.</summary>
        public float MinGap;
        public float MaxGap;

        /// <summary>How long the hop takes to fly, in seconds.</summary>
        public float HopSeconds;

        /// <summary>Height of the hop's arc.</summary>
        public float HopArc;

        public static HopSettings Default => new HopSettings
        {
            // Tuned so a full charge takes about 1.2s: long enough to be a decision, short enough
            // that a wrong guess costs a second rather than a run.
            ChargePerSecond = 2.6f,
            MinDistance = 0.5f,
            MaxDistance = 3.6f,
            PerfectRadius = 0.17f,
            BoxHalfSize = 0.62f,
            MinGap = 1.5f,
            MaxGap = 3.2f,
            HopSeconds = 0.42f,
            HopArc = 1.5f
        };
    }

    /// <summary>
    /// The hop game (跳一跳), as arithmetic.
    ///
    /// One input, one decision: how long to hold. Everything the player can get wrong — too short,
    /// too long, and the two ways to overshoot — is a comparison between two numbers, so the whole
    /// game fits in a class with no scene in it and gets tested that way.
    ///
    /// The rule that matters most for feel: the boxes are generated so that <b>every gap is
    /// reachable by a full charge</b>. A gap the player physically cannot cross is not a challenge,
    /// it is a lost run — the same principle the platformer's level check was built on.
    /// </summary>
    public static class HopRules
    {
        /// <summary>How far a charge of this many seconds carries the character.</summary>
        public static float HopDistance(float heldSeconds, HopSettings settings)
        {
            float raw = Mathf.Max(0f, heldSeconds) * settings.ChargePerSecond;
            return Mathf.Clamp(settings.MinDistance + raw, settings.MinDistance, settings.MaxDistance);
        }

        /// <summary>How long a full charge takes, for the charge bar's scale.</summary>
        public static float FullChargeSeconds(HopSettings settings)
            => Mathf.Max(0.01f, (settings.MaxDistance - settings.MinDistance) / settings.ChargePerSecond);

        /// <summary>What happened when the character came down.</summary>
        public enum Landing
        {
            Missed,     // nowhere near anything: the run is over
            Landed,     // on the box
            Perfect     // on the box, dead centre
        }

        /// <summary>
        /// Judges a landing. <paramref name="boxCentre"/> is where the next box's middle is; the
        /// character comes down at <paramref name="from"/> + <paramref name="distance"/>.
        /// </summary>
        public static Landing Judge(float from, float distance, float boxCentre,
            HopSettings settings)
        {
            float landing = from + distance;
            float offset = Mathf.Abs(landing - boxCentre);

            if (offset <= settings.PerfectRadius) return Landing.Perfect;
            if (offset <= settings.BoxHalfSize) return Landing.Landed;
            return Landing.Missed;
        }

        /// <summary>
        /// The gap to the next box.
        ///
        /// Capped at the reach of a full charge minus a margin: if the generator could place a box
        /// further than that, the game would hand the player an unwinnable turn — and they would
        /// have no way to know it was not their fault.
        /// </summary>
        public static float NextGap(float roll, int score, HopSettings settings)
        {
            float high = Mathf.Min(settings.MaxGap, settings.MaxDistance - 0.25f);

            // The floor creeps up with the score, so a long run is mostly long hops — and it stays
            // below the ceiling, so a low roll is still a short one.
            float floor = Mathf.Lerp(settings.MinGap, Mathf.Min(high - 0.2f, settings.MinGap + 0.9f),
                Mathf.Clamp01(score / 40f));

            return Mathf.Lerp(floor, high, Mathf.Clamp01(roll));
        }

        /// <summary>
        /// A small sideways drift for each box, so the line of them is not a ruler. Cosmetic: the
        /// landing test is one-dimensional and stays that way.
        /// </summary>
        public static float NextDrift(float roll, HopSettings settings, float maxDrift = 0.9f)
            => Mathf.Lerp(-maxDrift, maxDrift, Mathf.Clamp01(roll)) * (settings.BoxHalfSize * 1.2f);

        /// <summary>Points for a landing: a perfect one is worth two, as in the game this copies.</summary>
        public static int PointsFor(Landing landing)
            => landing == Landing.Perfect ? 2 : landing == Landing.Landed ? 1 : 0;

        /// <summary>Coins a finished run is worth: one per point, plus a small bonus for surviving.</summary>
        public static int CoinsFor(int score)
            => Mathf.Max(0, score) + Mathf.Max(0, score) / 8;

        /// <summary>A word for the result panel.</summary>
        public static string RankFor(int score)
        {
            if (score >= 60) return "你就是跳一跳本人";
            if (score >= 40) return "手很稳";
            if (score >= 25) return "中心大师";
            if (score >= 12) return "跳得不错";
            if (score >= 5) return "有点感觉了";
            return "再来一次吧";
        }

        /// <summary>
        /// The hop's position along its arc, 0..1 through the flight.
        ///
        /// Pure so the animation cannot quietly disagree with the landing the rules already
        /// decided — the character arrives where the judge says they arrive.
        /// </summary>
        public static Vector3 ArcPosition(float from, float to, float t, HopSettings settings)
        {
            float clamped = Mathf.Clamp01(t);
            float x = Mathf.Lerp(from, to, clamped);
            float y = Mathf.Sin(clamped * Mathf.PI) * settings.HopArc;
            return new Vector3(x, y, 0f);
        }
    }
}
