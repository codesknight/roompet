using System.Collections.Generic;
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

        /// <summary>
        /// How far from the middle of a box the animal can come down and still be on it. Measured in
        /// the plane the animal hops across — the boxes are squares, and the animal aims at them, so
        /// the landing zone is a disc rather than a strip.
        /// </summary>
        public float BoxHalfSize;

        /// <summary>Shortest and longest gap between two boxes, centre to centre.</summary>
        public float MinGap;
        public float MaxGap;

        /// <summary>How long the hop takes to fly, in seconds.</summary>
        public float HopSeconds;

        /// <summary>Height of the hop's arc.</summary>
        public float HopArc;

        /// <summary>
        /// How much of the full charge is held back from the longest gap. This is the difference
        /// between a hard hop and an impossible one, so it is a named number rather than a literal:
        /// a gap that only an exactly-full charge can cross turns every hop into a coin toss on a
        /// phone's frame timing.
        /// </summary>
        public float SafetyMargin;

        /// <summary>How far a box may sit off the line, in world units.</summary>
        public float MaxDrift;

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
            MaxGap = 2.9f,
            HopSeconds = 0.42f,
            HopArc = 0.7f,
            SafetyMargin = 0.18f,
            MaxDrift = 0.30f
        };
    }

    /// <summary>
    /// The hop game (跳一跳), as arithmetic.
    ///
    /// One input, one decision: how long to hold. Everything the player can get wrong — too short,
    /// too long, and the two ways to overshoot — is a comparison between two numbers, so the whole
    /// game fits in a class with no scene in it and gets tested that way.
    ///
    /// Three rules carry the whole design, and each of them came from a bug report:
    ///
    /// <list type="bullet">
    /// <item><b>The animal always heads at the box.</b> The first version moved it a fixed number of
    /// units along +X whatever the boxes did, so a box that sat a little to the side was one the
    /// animal visibly flew past — "动物每次下一步方向必须朝着下一个方块（否则一个方向容易落空）". A hop
    /// is now a length along the line from the animal to the middle of the next box, and the landing
    /// is judged by the distance from that middle.</item>
    /// <item><b>Every gap can be crossed with a margin.</b> The first version capped the gap at
    /// <c>MaxDistance - 0.25</c> measured <i>centre to centre</i> — which ignores where the animal
    /// actually came down. Land on the near edge of a box and the next hop needed 3.82 units from a
    /// charge that reaches 3.6: a lost run the player could not see coming. <see cref="SafeGap"/>
    /// fixes the arithmetic, and <see cref="LandingWindowSeconds"/> is the property that matters —
    /// how much of the hold actually lands, which is the game's real difficulty control.</item>
    /// <item><b>The camera is computed, not eyeballed.</b> <see cref="Frame"/> answers "what view
    /// contains the animal and both boxes" arithmetically, so a portrait phone and a landscape
    /// window both get a correct answer instead of a guess that happens to look fine in the
    /// editor.</item>
    /// </list>
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

        /// <summary>
        /// How long to hold to travel exactly this far. The inverse of <see cref="HopDistance"/>,
        /// clamped to the charge a finger can actually produce: an autopilot uses it, and so does the
        /// test that proves a whole level is playable.
        /// </summary>
        public static float ChargeFor(float distance, HopSettings settings)
        {
            float wanted = (distance - settings.MinDistance) / Mathf.Max(0.01f, settings.ChargePerSecond);
            return Mathf.Clamp(wanted, 0f, FullChargeSeconds(settings));
        }

        /// <summary>What happened when the character came down.</summary>
        public enum Landing
        {
            Missed,     // nowhere near anything: the run is over
            Landed,     // on the box
            Perfect     // on the box, dead centre
        }

        /// <summary>
        /// Judges a landing in the plane the animal hops across. The animal lands at
        /// <paramref name="landing"/>; the box's middle is at <paramref name="boxCentre"/>.
        /// </summary>
        public static Landing JudgeAt(Vector2 landing, Vector2 boxCentre, HopSettings settings)
        {
            float offset = (landing - boxCentre).magnitude;

            if (offset <= settings.PerfectRadius) return Landing.Perfect;
            if (offset <= settings.BoxHalfSize) return Landing.Landed;
            return Landing.Missed;
        }

        /// <summary>
        /// The same judgement on a straight line, which is the case every hop degenerates to when the
        /// boxes are not offset sideways. The character comes down at
        /// <paramref name="from"/> + <paramref name="distance"/>.
        /// </summary>
        public static Landing Judge(float from, float distance, float boxCentre, HopSettings settings)
            => JudgeAt(new Vector2(from + distance, 0f), new Vector2(boxCentre, 0f), settings);

        /// <summary>
        /// Where a hop comes down when the animal aims at the box instead of along a world axis.
        ///
        /// The animal travels <paramref name="distance"/> units *towards* the target, so a full
        /// charge lands past it and a tap lands short of it — both on the same line, which is what
        /// makes the miss readable: the player can see they were long or short, not mysteriously
        /// beside the box.
        /// </summary>
        public static Vector2 AimedLanding(Vector2 from, Vector2 target, float distance)
        {
            var delta = target - from;
            float length = delta.magnitude;
            if (length < 0.0001f) return from + new Vector2(distance, 0f);

            return from + delta * (distance / length);
        }

        /// <summary>
        /// The longest gap the generator may draw, in the plane the animal hops across, so that a
        /// full charge from the <i>worst</i> legal standing point on the current box still reaches
        /// the middle of the next one.
        ///
        /// The worst standing point is the near edge, <see cref="HopSettings.BoxHalfSize"/> behind the
        /// middle, which is why the box's own size is subtracted here rather than ignored.
        /// </summary>
        public static float SafeGap(HopSettings settings)
            => Mathf.Max(settings.MinGap, settings.MaxDistance - settings.BoxHalfSize - settings.SafetyMargin);

        /// <summary>
        /// Whether a full charge from <paramref name="from"/> reaches the box at
        /// <paramref name="centre"/> at all. This is the game's promise: a hop may be hard, but it is
        /// never impossible, and the player is never the reason a box could not be reached.
        /// </summary>
        public static bool CanReach(Vector2 from, Vector2 centre, HopSettings settings)
            => (from - centre).magnitude - settings.BoxHalfSize <= settings.MaxDistance + 0.0001f;

        /// <summary>
        /// How many seconds of holding land on a box <paramref name="distance"/> away.
        ///
        /// This, not the gap, is the game's difficulty: a box takes a range of holds, and the range is
        /// <c>2 * BoxHalfSize / ChargePerSecond</c> seconds wide minus whatever the ends of the charge
        /// clamp away. Aiming for the middle of the range is 完美, and the difference between the two
        /// is what the player is actually learning.
        /// </summary>
        public static float LandingWindowSeconds(float distance, HopSettings settings)
        {
            float width = settings.BoxHalfSize * 2f;
            float low = Mathf.Max(settings.MinDistance, distance - settings.BoxHalfSize);
            float high = Mathf.Min(settings.MaxDistance, distance + settings.BoxHalfSize);
            if (high <= low) return 0f;

            return Mathf.Min(width, high - low) / Mathf.Max(0.01f, settings.ChargePerSecond);
        }

        /// <summary>
        /// The gap to the next box.
        ///
        /// <paramref name="sideways"/> is how far the next box sits off the line of the hop. The hop
        /// is a straight line to the box, so a box that is <c>sideways</c> to the side eats into the
        /// length the animal has to travel: the gap gets straighter to keep the <i>length</i> inside
        /// <see cref="SafeGap"/>. Ignoring this was the second half of "跳不过去" — the reachability
        /// test only ever looked at the X gap while the boxes also drifted sideways.
        /// </summary>
        public static float NextGap(float roll, int score, float sideways, HopSettings settings)
        {
            float budget = SafeGap(settings);
            float straight = Mathf.Sqrt(Mathf.Max(0f, budget * budget - sideways * sideways));

            // Never shorter than the floor, and never longer than the budget allows. The floor creeps
            // up with the score, so a long run is mostly long hops — and it stays below the ceiling,
            // so a low roll is still a short one.
            float high = Mathf.Max(settings.MinGap + 0.1f, straight);
            float floor = Mathf.Lerp(settings.MinGap, Mathf.Min(high - 0.2f, settings.MinGap + 0.9f),
                Mathf.Clamp01(score / 40f));
            floor = Mathf.Min(floor, high - 0.05f);

            return Mathf.Lerp(Mathf.Max(settings.MinGap, floor), high, Mathf.Clamp01(roll));
        }

        /// <inheritdoc cref="NextGap(float, int, float, HopSettings)"/>
        public static float NextGap(float roll, int score, HopSettings settings)
            => NextGap(roll, score, 0f, settings);

        /// <summary>
        /// A small sideways offset for each box, so the line of them is not a ruler. Cosmetic: the
        /// hop aims at wherever the box ended up, so the offset costs reach but never correctness.
        /// </summary>
        public static float NextDrift(float roll, HopSettings settings, float maxDrift = -1f)
        {
            if (maxDrift < 0f) maxDrift = settings.MaxDrift;
            return Mathf.Lerp(-maxDrift, maxDrift, Mathf.Clamp01(roll));
        }

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
        /// The hop's position along its arc, 0..1 through the flight, from one point in the plane to
        /// another. The height is the arc; x and z are the straight line to the box, so the animation
        /// and the landing test cannot quietly disagree.
        ///
        /// Pure so the animation cannot contradict the landing the rules already decided — the
        /// character arrives where the judge says they arrive.
        /// </summary>
        public static Vector3 ArcPosition(Vector2 from, Vector2 to, float t, HopSettings settings)
        {
            float clamped = Mathf.Clamp01(t);
            var ground = Vector2.Lerp(from, to, clamped);
            return new Vector3(ground.x, Mathf.Sin(clamped * Mathf.PI) * settings.HopArc, ground.y);
        }

        /// <summary>The line case of the arc, kept because it is the one the tests read.</summary>
        public static Vector3 ArcPosition(float from, float to, float t, HopSettings settings)
        {
            float clamped = Mathf.Clamp01(t);
            float x = Mathf.Lerp(from, to, clamped);
            float y = Mathf.Sin(clamped * Mathf.PI) * settings.HopArc;
            return new Vector3(x, y, 0f);
        }

        // ------------------------------------------------------------------ camera

        /// <summary>
        /// The smallest orthographic view that contains every point, measured on the camera's own
        /// screen axes.
        ///
        /// A camera is normally framed by eye, which makes it the one thing in a game that is never
        /// checked — and the thing that broke here: the view was placed at a fixed offset that did
        /// not actually put the animal in the middle of the screen, and the zoom was
        /// <c>max(height, width / aspect)</c>, which on a portrait phone zoomed <i>out</i> to the
        /// landscape demand and turned the toy into a strip in the middle. So the frame is arithmetic
        /// now, and the test asserts that every point is on screen for aspects from a tall phone to a
        /// desktop window.
        ///
        /// The focus is moved along <paramref name="right"/> and <paramref name="up"/> only: for an
        /// orthographic camera the depth of the eye is irrelevant, so the only freedom that changes
        /// what is on screen is the projection of the focus.
        /// </summary>
        public static void Frame(IList<Vector3> points, Vector3 right, Vector3 up, float aspect,
            float padding, float minSize, float maxSize, out Vector3 focus, out float size)
        {
            int count = points == null ? 0 : points.Count;
            if (count == 0)
            {
                focus = Vector3.zero;
                size = Mathf.Max(0.1f, minSize);
                return;
            }

            float minU = 0f, maxU = 0f, minV = 0f, maxV = 0f;
            var centroid = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                var point = points[i];
                float u = Vector3.Dot(point, right);
                float v = Vector3.Dot(point, up);

                if (i == 0) { minU = maxU = u; minV = maxV = v; }
                else
                {
                    minU = Mathf.Min(minU, u);
                    maxU = Mathf.Max(maxU, u);
                    minV = Mathf.Min(minV, v);
                    maxV = Mathf.Max(maxV, v);
                }

                centroid += point;
            }

            centroid /= count;

            // The padded span is symmetric, so the middle of the unpadded span is the middle of the
            // frame — which is what makes the padding a margin on every side rather than a shift.
            float uMid = (minU + maxU) * 0.5f;
            float vMid = (minV + maxV) * 0.5f;

            focus = centroid
                + right * (uMid - Vector3.Dot(centroid, right))
                + up * (vMid - Vector3.Dot(centroid, up));

            float halfWidth = (maxU - minU) * 0.5f + Mathf.Max(0f, padding);
            float halfHeight = (maxV - minV) * 0.5f + Mathf.Max(0f, padding);
            float needed = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.05f, aspect));

            size = Mathf.Clamp(needed, Mathf.Max(0.1f, minSize), Mathf.Max(minSize, maxSize));
        }

        /// <summary>
        /// Where a world point sits in the frame, in units of the half-view: (-1,-1) is the bottom
        /// left corner, (1,1) the top right. Used by the game to notice that the animal has drifted
        /// to the edge, and by the test that proves it cannot.
        /// </summary>
        public static Vector2 ViewOffset(Vector3 point, Vector3 focus, Vector3 right, Vector3 up,
            float size, float aspect)
        {
            float u = Vector3.Dot(point - focus, right);
            float v = Vector3.Dot(point - focus, up);
            return new Vector2(u / Mathf.Max(0.0001f, size * aspect), v / Mathf.Max(0.0001f, size));
        }
    }
}
