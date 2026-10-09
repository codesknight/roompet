using UnityEngine;

namespace DshMiniGames
{
    /// <summary>
    /// The numbers that make the flappy game playable, and the arithmetic that uses them.
    ///
    /// Pure and separate from the scene for the same reason the pet's needs curve is: "is the gap
    /// big enough to fly through", "does the game get harder without becoming impossible" and
    /// "does a tap do the same thing at 30fps and 120fps" are questions with answers, and the
    /// answers belong in a test rather than in a play session on a phone.
    /// </summary>
    public struct FlyBirdSettings
    {
        /// <summary>Downward acceleration, in world units per second squared.</summary>
        public float Gravity;

        /// <summary>The velocity a tap imposes, in world units per second.</summary>
        public float FlapSpeed;

        /// <summary>How fast the bird falls when it is not flapping.</summary>
        public float MaxFallSpeed;

        /// <summary>Upward limit, so a burst of taps cannot launch it off the screen.</summary>
        public float MaxRiseSpeed;

        /// <summary>How fast the pipes travel towards the bird.</summary>
        public float ScrollSpeed;

        /// <summary>World units between one pipe pair and the next.</summary>
        public float PipeSpacing;

        /// <summary>Height of the hole the bird has to fly through, at the start.</summary>
        public float GapHeight;

        /// <summary>How much the hole shrinks per point, and the floor it shrinks to.</summary>
        public float GapTightenPerPoint;
        public float MinGapHeight;

        /// <summary>Half-height of the play area, used to place the gaps.</summary>
        public float PlayHeight;

        /// <summary>
        /// The shortest the game ever makes the gap between two pipes, in seconds.
        ///
        /// One flap takes <c>2 * FlapSpeed / Gravity</c> seconds to rise and come back, so a rhythm
        /// shorter than that cannot be played deliberately: the player is always catching a bird
        /// that is mid-cycle. 0.85 is comfortably longer than the 0.64 second cycle, and the
        /// difficulty above that comes from the hole shrinking and the hole crossing the bird
        /// faster instead.
        /// </summary>
        public float MinPipeSeconds;

        /// <summary>Height of the ground, so the bird can be shown resting on it.</summary>
        public float GroundHeight;

        /// <summary>
        /// The share of the bird's physical travel that a gap may ever *require*.
        ///
        /// The bird can climb at <see cref="FlapSpeed"/> and fall at <see cref="MaxFallSpeed"/>, so
        /// between two pipes there is a hard limit on how far it can move — and a generator that
        /// ignores that limit produces runs that were lost before the player touched the screen.
        /// 0.7 leaves room for the human part: the time to notice, decide and react.
        /// </summary>
        public float ReachSafety;

        /// <summary>How much of the reachable window is used at the start (1 = all of it).</summary>
        public float StartReachShare;

        public static FlyBirdSettings Default => new FlyBirdSettings
        {
            // Tuned against a phone screen, and then re-tuned against an honest simulation of a
            // player (see Generator_AnAutopilotCanFlyALongRun), which is what found the real
            // relationship between these numbers: one flap lifts the bird
            // FlapSpeed^2 / 2 * Gravity units, so the hole has to be at least that *plus* the bird's
            // own height for a player aiming at the middle to fit. At 7.4 the flap lifted 1.24 and
            // the tightest hole was 2.8 — a player aiming at the middle clipped the ceiling every
            // time the hole was at its smallest.
            Gravity = 22f,
            FlapSpeed = 6.4f,
            MaxFallSpeed = 12f,
            MaxRiseSpeed = 7.0f,
            ScrollSpeed = 4.2f,
            PipeSpacing = 4.6f,
            GapHeight = 3.9f,
            GapTightenPerPoint = 0.04f,
            MinGapHeight = 3.4f,
            PlayHeight = 9f,
            MinPipeSeconds = 0.85f,
            GroundHeight = 0.6f,

            // Half the bird's physical travel. An honest continuous simulation dies around pipe 12
            // at 0.7, because "reachable" measured from a standstill at the very limit of the bird's
            // travel is not the same thing as playable: half leaves room for a tap that was one
            // frame late.
            ReachSafety = 0.5f,
            StartReachShare = 1f
        };
    }

    /// <summary>Everything the flappy game does that does not need a scene.</summary>
    public static class FlyBirdRules
    {
        /// <summary>
        /// One step of the bird's motion. <paramref name="dt"/> is clamped: a frame that took a
        /// second (a backgrounded app, a shader compile) must not teleport the bird through a
        /// pipe, which is the classic way a physics-lite game cheats the player.
        /// </summary>
        public static void Step(ref float height, ref float velocity, float dt, bool flap,
            FlyBirdSettings settings)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);

            if (flap) velocity = settings.FlapSpeed;

            velocity -= settings.Gravity * step;
            velocity = Mathf.Clamp(velocity, -settings.MaxFallSpeed, settings.MaxRiseSpeed);

            height += velocity * step;
        }

        /// <summary>
        /// How fast the pipes travel, and the ceiling that ramp reaches.
        ///
        /// The ceiling was 2.2x, which puts a pipe in front of the bird every 0.5 seconds at high
        /// scores — measured against a phone that is past what a thumb can do, and a difficulty
        /// curve that ends in "nobody gets further" is not a difficulty curve. 1.9x keeps a real
        /// ramp (pipes arrive twice as often as at the start) and stays playable.
        /// </summary>
        public const float MaxSpeedScale = 1.9f;

        /// <summary>How fast the bird's climb rate decays with the score, if it ever needs to.</summary>
        public static float SpeedFor(int score, FlyBirdSettings settings)
        {
            // +6% per point, capped: the game has to stay winnable, and the ceiling is what stops a
            // good player from reaching a speed no thumb can play.
            float scale = Mathf.Min(1f + Mathf.Max(0, score) * 0.06f, MaxSpeedScale);
            return settings.ScrollSpeed * scale;
        }

        /// <summary>Size of the hole after this many points.</summary>
        public static float GapFor(int score, FlyBirdSettings settings)
        {
            float gap = settings.GapHeight - Mathf.Max(0, score) * settings.GapTightenPerPoint;
            return Mathf.Max(settings.MinGapHeight, gap);
        }

        /// <summary>
        /// Where the middle of the next gap sits, given a random roll in 0..1.
        ///
        /// The gap is kept away from both the ground and the ceiling by half a gap plus a little
        /// air, because a hole you cannot physically reach is a lost run rather than a mistake.
        ///
        /// This is the *first* pipe's rule: the band is the whole play area, because the bird starts
        /// in the middle with nothing to compare against. Every later pipe uses
        /// <see cref="NextGapCentre"/>, which also has to be flyable from the previous one.
        /// </summary>
        public static float GapCentre(float roll, float gap, FlyBirdSettings settings, float span)
        {
            float half = gap * 0.5f + 0.35f;
            float low = -settings.PlayHeight + half;
            float high = settings.PlayHeight - half;
            if (high < low) high = low;

            // `span` lets a test (or a second pipe in a batch) drive the spread without a Random.
            return Mathf.Lerp(low, high, Mathf.Clamp01(roll) * Mathf.Clamp01(span <= 0f ? 1f : span));
        }

        // ------------------------------------------------------- the pipe generator

        /// <summary>
        /// How long the bird has between two consecutive pipes, at this score.
        ///
        /// This is the number that decides whether a run is possible at all. It used to be
        /// <c>spacing / speed</c> with a fixed spacing, which halved as the game sped up — down to
        /// 0.5 seconds, while one flap takes 0.64 seconds to rise and fall. A rhythm shorter than
        /// the bird's own cycle is not difficulty, it is a coin flip, so the spacing now grows with
        /// the speed and this floor is what the pipes are laid out to hit.
        /// </summary>
        public static float PipeSeconds(int score, FlyBirdSettings settings)
            => Mathf.Max(settings.MinPipeSeconds, settings.PipeSpacing / Mathf.Max(0.05f, SpeedFor(score, settings)));

        /// <summary>World units between two pipes at this score, so they arrive at a playable rate.</summary>
        public static float SpacingFor(int score, FlyBirdSettings settings)
            => PipeSeconds(score, settings) * SpeedFor(score, settings);

        /// <summary>
        /// How far the bird falls in a given time, starting from rest.
        ///
        /// This is the number the first attempt at the generator got wrong, and it is the difference
        /// between "the pipes are random" and "the pipes are fair": a flap sets the velocity
        /// *instantly*, so the bird can start climbing the moment the player wants it to — but going
        /// down it has to be accelerated by gravity, and 12 units a second of terminal velocity
        /// takes half a second to reach. Between two pipes 0.5 seconds apart, a bird that is hovering
        /// drops about 2.7 units, not the 6 that terminal velocity would suggest.
        /// </summary>
        public static float FallDistance(float seconds, FlyBirdSettings settings)
        {
            float t = Mathf.Max(0f, seconds);
            float gravity = Mathf.Max(0.01f, settings.Gravity);
            float terminalTime = settings.MaxFallSpeed / gravity;

            if (t <= terminalTime) return 0.5f * gravity * t * t;

            float atTerminal = 0.5f * gravity * terminalTime * terminalTime;
            return atTerminal + settings.MaxFallSpeed * (t - terminalTime);
        }

        /// <summary>
        /// How far up and down the bird can travel between two pipes, in world units.
        ///
        /// Upwards: a flap is an instant velocity change, so the climb rate is the flap speed (the
        /// safety share stands in for how fast a thumb can actually tap). Downwards: gravity, from
        /// rest, through <see cref="FallDistance"/>. Scaled by
        /// <see cref="FlyBirdSettings.ReachSafety"/> and by how far into the run we are — early
        /// pipes ask for less travel, later ones use more of what the bird has.
        /// </summary>
        public static void ReachFor(int score, FlyBirdSettings settings, out float up, out float down)
        {
            float seconds = PipeSeconds(score, settings);
            float climb = Mathf.Min(settings.FlapSpeed, settings.MaxRiseSpeed);

            // Difficulty here is *how much of its travel the bird is asked to use*, not how
            // impossible the pipes are: 55% at the start, rising to the safety ceiling.
            float share = Mathf.Clamp(
                Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(Mathf.Max(0, score) / 20f)), 0.1f, 1f);

            float budget = seconds * settings.ReachSafety * share;
            up = climb * budget;
            down = FallDistance(budget, settings);
        }

        /// <summary>
        /// The centre of the next gap, given the previous one, the score and a random roll.
        ///
        /// The whole point of this function is the *window*: the roll is mapped into the band the
        /// bird can actually fly to from where the last gap left it, intersected with the band that
        /// keeps the hole on screen. Randomising over the full height instead — which is what the
        /// first version did — makes runs that need 14 units of travel in the 4 the bird has,
        /// and the player experiences that as "the pipes are too random", which is exactly right.
        ///
        /// <paramref name="previous"/> is the height the bird leaves the last gap at (the run starts
        /// from the middle of the screen).
        /// </summary>
        public static float NextGapCentre(float roll, float previous, float gap, int score,
            FlyBirdSettings settings)
        {
            float half = gap * 0.5f + 0.35f;
            float screenLow = -settings.PlayHeight + half;
            float screenHigh = settings.PlayHeight - half;
            if (screenHigh < screenLow) screenHigh = screenLow;

            float up, down;
            ReachFor(score, settings, out up, out down);

            float low = Mathf.Max(screenLow, previous - down);
            float high = Mathf.Min(screenHigh, previous + up);

            // A window can invert when the gap grew (or the speed rose) faster than the bird can
            // travel: the honest answer then is "as close to where you are as the screen allows",
            // which is the nearest end of the screen band.
            if (high < low) high = low;

            return Mathf.Lerp(low, high, Mathf.Clamp01(roll));
        }

        /// <summary>
        /// Whether the move from one gap to the next is physically flyable.
        ///
        /// Exposed so the generator's guarantee can be asserted from outside — the game does not
        /// need to ask, because it only ever calls <see cref="NextGapCentre"/>.
        /// </summary>
        public static bool IsReachable(float from, float to, float gap, int score,
            FlyBirdSettings settings)
        {
            float up, down;
            ReachFor(score, settings, out up, out down);

            float half = gap * 0.5f + 0.35f;
            float screenLow = -settings.PlayHeight + half;
            float screenHigh = settings.PlayHeight - half;

            return to >= from - down - 0.001f && to <= from + up + 0.001f &&
                   to >= screenLow - 0.001f && to <= screenHigh + 0.001f;
        }

        /// <summary>Whether the bird is inside the gap of a pipe centred at this height.</summary>
        public static bool PassesGap(float birdHeight, float birdRadius, float centre, float gap)
            => birdHeight - birdRadius >= centre - gap * 0.5f &&
               birdHeight + birdRadius <= centre + gap * 0.5f;

        /// <summary>Coins a run is worth. One per pipe, with a small bonus for a long run.</summary>
        public static int CoinsFor(int score)
        {
            int points = Mathf.Max(0, score);
            return points + points / 5;   // every fifth pipe is worth a second coin
        }

        /// <summary>
        /// The run's medal, for the result panel: something to say beyond the number.
        /// </summary>
        public static string RankFor(int score)
        {
            if (score >= 40) return "飞得比房子还高";
            if (score >= 25) return "老练的飞行员";
            if (score >= 15) return "飞得不错";
            if (score >= 8) return "刚学会扇翅膀";
            if (score >= 3) return "起飞了";
            return "再试一次就好";
        }
    }
}
