using UnityEngine;

namespace DshMiniGames
{
    /// <summary>Everything the catch game's numbers are.</summary>
    public struct CatchSettings
    {
        /// <summary>Half-width of the play area, in world units.</summary>
        public float HalfWidth;

        /// <summary>Where the fruit starts falling from.</summary>
        public float SpawnHeight;

        /// <summary>How fast the basket moves, in world units per second.</summary>
        public float BasketSpeed;

        /// <summary>Half-width of the basket, which is what "caught it" is measured against.</summary>
        public float BasketHalfWidth;

        /// <summary>Half-width of a fruit.</summary>
        public float FruitHalfWidth;

        /// <summary>Fall speed at the start, and how much it grows per point.</summary>
        public float FallSpeed;
        public float FallSpeedPerPoint;

        /// <summary>The ceiling on the fall speed, so the game stays playable.</summary>
        public float MaxFallSpeed;

        /// <summary>Seconds between fruit at the start, and how much that shortens per point.</summary>
        public float SpawnSeconds;
        public float SpawnSecondsPerPoint;
        public float MinSpawnSeconds;

        public static CatchSettings Default => new CatchSettings
        {
            HalfWidth = 5.2f,
            SpawnHeight = 6.4f,
            BasketSpeed = 7.6f,
            BasketHalfWidth = 0.85f,
            FruitHalfWidth = 0.28f,
            FallSpeed = 3.1f,
            FallSpeedPerPoint = 0.075f,
            MaxFallSpeed = 8.2f,
            SpawnSeconds = 1.25f,
            SpawnSecondsPerPoint = 0.022f,
            MinSpawnSeconds = 0.55f
        };
    }

    /// <summary>
    /// 接果子, as arithmetic.
    ///
    /// The game is "move left and right, catch what falls": what can go wrong is entirely in the
    /// numbers — fruit that falls faster than the basket can cross the screen, a spawn gap so short
    /// that two fruit arrive at once, a difficulty ramp with no ceiling. All of that is a
    /// comparison, so all of it is tested here rather than felt out on a phone.
    /// </summary>
    public static class CatchRules
    {
        /// <summary>Where the basket ends up after a frame of holding a direction.</summary>
        public static float StepBasket(float x, float direction, float dt, CatchSettings settings)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);
            float limit = settings.HalfWidth - settings.BasketHalfWidth;
            return Mathf.Clamp(x + Mathf.Clamp(direction, -1f, 1f) * settings.BasketSpeed * step,
                -limit, limit);
        }

        /// <summary>How fast fruit falls after this many points.</summary>
        public static float FallSpeedFor(int score, CatchSettings settings)
        {
            float speed = settings.FallSpeed + Mathf.Max(0, score) * settings.FallSpeedPerPoint;
            return Mathf.Min(speed, settings.MaxFallSpeed);
        }

        /// <summary>How long between two fruit after this many points.</summary>
        public static float SpawnSecondsFor(int score, CatchSettings settings)
        {
            float seconds = settings.SpawnSeconds - Mathf.Max(0, score) * settings.SpawnSecondsPerPoint;
            return Mathf.Max(settings.MinSpawnSeconds, seconds);
        }

        /// <summary>Whether a falling fruit at this height is caught by a basket at this position.</summary>
        public static bool Catches(float basketX, float fruitX, CatchSettings settings)
        {
            float reach = settings.BasketHalfWidth + settings.FruitHalfWidth * 0.5f;
            return Mathf.Abs(basketX - fruitX) <= reach;
        }

        /// <summary>
        /// Where the next fruit appears, given a roll and the basket's position.
        ///
        /// Two deliberately different behaviours: a share of the fruit lands <b>near</b> the basket
        /// (a nudge, a reward for watching the middle) and the rest lands <b>far</b> from it, at a
        /// distance the player has to actually travel for. The first version jittered everything
        /// around a single formula, which produced fruit that was sometimes close and sometimes far
        /// from the same roll — a game you cannot read.
        /// </summary>
        public static float SpawnX(float roll, float basketX, CatchSettings settings,
            float biasTowardsBasket = 0.35f)
        {
            float limit = settings.HalfWidth - settings.FruitHalfWidth;
            float r = Mathf.Clamp01(roll);
            float near = Mathf.Clamp(biasTowardsBasket, 0.05f, 0.9f);

            if (r < near)
            {
                float t = r / near;
                return Mathf.Clamp(basketX + Mathf.Lerp(-2.2f, 2.2f, t), -limit, limit);
            }

            float far = (r - near) / (1f - near);
            float distance = Mathf.Lerp(2.8f, settings.HalfWidth, far);
            float side = Mathf.Repeat(far * 8f, 2f) < 1f ? -1f : 1f;

            return Mathf.Clamp(basketX + side * distance, -limit, limit);
        }

        /// <summary>Lives the run starts with.</summary>
        public const int StartLives = 3;

        /// <summary>Coins a run is worth.</summary>
        public static int CoinsFor(int caught) => Mathf.Max(0, caught) + Mathf.Max(0, caught) / 10;

        /// <summary>A word for the result panel.</summary>
        public static string RankFor(int caught)
        {
            if (caught >= 60) return "果园都被你接空了";
            if (caught >= 40) return "手很快";
            if (caught >= 25) return "接得稳";
            if (caught >= 12) return "还不错";
            if (caught >= 5) return "有点意思了";
            return "再来一次吧";
        }
    }
}
