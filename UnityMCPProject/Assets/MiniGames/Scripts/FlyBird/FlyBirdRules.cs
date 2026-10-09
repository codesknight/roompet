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

        /// <summary>Height of the ground, so the bird can be shown resting on it.</summary>
        public float GroundHeight;

        public static FlyBirdSettings Default => new FlyBirdSettings
        {
            // Tuned against a phone screen: the bird crosses a gap in about 0.6 seconds, which is
            // the window a thumb has to react in, and a tap lifts it roughly one gap in height.
            Gravity = 22f,
            FlapSpeed = 7.4f,
            MaxFallSpeed = 12f,
            MaxRiseSpeed = 8f,
            ScrollSpeed = 4.2f,
            PipeSpacing = 4.6f,
            GapHeight = 3.4f,
            GapTightenPerPoint = 0.045f,
            MinGapHeight = 2.3f,
            PlayHeight = 9f,
            GroundHeight = 0.6f
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

        /// <summary>Speed of the pipes after this many points.</summary>
        public static float SpeedFor(int score, FlyBirdSettings settings)
        {
            // +6% per point, capped at 2.2x: the game has to stay winnable, and the ceiling is
            // what stops a good player from reaching a speed no thumb can play.
            float scale = Mathf.Min(1f + Mathf.Max(0, score) * 0.06f, 2.2f);
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
