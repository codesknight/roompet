using DshMiniGames;
using NUnit.Framework;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// 接果子's arithmetic.
    ///
    /// The game is continuous input, so the interesting failures are all about reachability: a
    /// basket that cannot cross the screen in time, fruit spawned so fast that two arrive at once,
    /// a difficulty ramp that leaves the player no chance. Every one of those is a comparison.
    /// </summary>
    public class CatchRulesTests
    {
        private static readonly CatchSettings Settings = CatchSettings.Default;

        [Test]
        public void Basket_MovesAndStopsAtTheEdges()
        {
            float x = 0f;
            x = CatchRules.StepBasket(x, 1f, 1f / 60f, Settings);
            Assert.Greater(x, 0f, "holding right has to move right");

            for (int i = 0; i < 600; i++) x = CatchRules.StepBasket(x, 1f, 1f / 60f, Settings);
            Assert.LessOrEqual(x, Settings.HalfWidth - Settings.BasketHalfWidth + 0.001f,
                "the basket must not leave the play area");
            Assert.Greater(x, Settings.HalfWidth - Settings.BasketHalfWidth - 0.01f,
                "and it should reach the edge, not stop short of it");

            for (int i = 0; i < 600; i++) x = CatchRules.StepBasket(x, -1f, 1f / 60f, Settings);
            Assert.GreaterOrEqual(x, -Settings.HalfWidth + Settings.BasketHalfWidth - 0.001f);
        }

        [Test]
        public void Basket_IsFrameRateIndependentAndClampsLongFrames()
        {
            float slow = 0f, fast = 0f;
            for (int i = 0; i < 30; i++) slow = CatchRules.StepBasket(slow, 1f, 1f / 30f, Settings);
            for (int i = 0; i < 120; i++) fast = CatchRules.StepBasket(fast, 1f, 1f / 120f, Settings);
            Assert.AreEqual(slow, fast, 0.15f, "30fps and 120fps have to move the same distance");

            float one = CatchRules.StepBasket(0f, 1f, 1f, Settings);
            Assert.LessOrEqual(one, Settings.BasketSpeed * 0.05f + 0.001f,
                "a one-second frame must be treated as a 50ms one");
        }

        [Test]
        public void Difficulty_RampsUpAndThenStops()
        {
            Assert.AreEqual(Settings.FallSpeed, CatchRules.FallSpeedFor(0, Settings), 0.001f);
            Assert.AreEqual(Settings.SpawnSeconds, CatchRules.SpawnSecondsFor(0, Settings), 0.001f);

            float previousSpeed = 0f, previousGap = float.MaxValue;
            for (int score = 0; score < 120; score++)
            {
                float speed = CatchRules.FallSpeedFor(score, Settings);
                float gap = CatchRules.SpawnSecondsFor(score, Settings);

                Assert.GreaterOrEqual(speed, previousSpeed, "fruit cannot slow down as you score");
                Assert.LessOrEqual(gap, previousGap + 0.0001f, "spacing cannot get more generous");
                previousSpeed = speed;
                previousGap = gap;
            }

            Assert.AreEqual(Settings.MaxFallSpeed, CatchRules.FallSpeedFor(500, Settings), 0.001f,
                "the fall speed has to reach a ceiling a thumb can still play");
            Assert.AreEqual(Settings.MinSpawnSeconds, CatchRules.SpawnSecondsFor(500, Settings), 0.001f);
        }

        [Test]
        public void Difficulty_TheBasketCanAlwaysCrossInTime()
        {
            // The one thing that must never happen: fruit that lands faster than the basket can
            // travel to it. Crossing the whole play area has to stay possible at every score.
            float crossing = (Settings.HalfWidth - Settings.BasketHalfWidth) * 2f / Settings.BasketSpeed;

            for (int score = 0; score < 120; score += 6)
            {
                float fallTime = Settings.SpawnHeight * 2f / CatchRules.FallSpeedFor(score, Settings);
                Assert.Greater(fallTime, crossing * 0.75f,
                    $"at score {score} the fruit falls in {fallTime:F2}s and the basket needs " +
                    $"{crossing:F2}s to cross the screen");
            }
        }

        [Test]
        public void Catch_CountsTheBasketAndTheFruit()
        {
            Assert.IsTrue(CatchRules.Catches(0f, 0f, Settings), "dead centre is a catch");
            Assert.IsTrue(CatchRules.Catches(0f, Settings.BasketHalfWidth, Settings),
                "the rim still catches");
            Assert.IsFalse(CatchRules.Catches(0f, Settings.BasketHalfWidth + 1f, Settings),
                "a fruit a whole unit away is not caught");
            Assert.IsFalse(CatchRules.Catches(-3f, 3f, Settings));
        }

        [Test]
        public void Spawn_StaysInsideThePlayArea()
        {
            for (int i = 0; i <= 40; i++)
            {
                float roll = i / 40f;
                float x = CatchRules.SpawnX(roll, 0f, Settings);
                Assert.GreaterOrEqual(x, -Settings.HalfWidth + Settings.FruitHalfWidth - 0.001f);
                Assert.LessOrEqual(x, Settings.HalfWidth - Settings.FruitHalfWidth + 0.001f);
            }
        }

        [Test]
        public void Spawn_IsReadableANudgeOrARealMove()
        {
            // A basket in the middle of the screen, so the clamp does not confuse the question.
            const float basket = 0f;

            for (int i = 0; i < 20; i++)
            {
                float near = CatchRules.SpawnX(i / 100f, basket, Settings);      // rolls under 0.35
                Assert.LessOrEqual(Mathf.Abs(near - basket), 2.3f,
                    "a near roll should be a nudge, not a trek");
            }

            for (int i = 40; i <= 100; i += 5)
            {
                float far = CatchRules.SpawnX(i / 100f, basket, Settings);
                Assert.GreaterOrEqual(Mathf.Abs(far - basket), 2.7f,
                    $"roll {i / 100f:F2} should need the player to actually move");
            }

            // Both sides get used, and the spread covers the whole width at the far end.
            bool leftSeen = false, rightSeen = false;
            float widest = 0f;
            for (int i = 40; i <= 100; i++)
            {
                float x = CatchRules.SpawnX(i / 100f, basket, Settings);
                if (x < -0.1f) leftSeen = true;
                if (x > 0.1f) rightSeen = true;
                widest = Mathf.Max(widest, Mathf.Abs(x));
            }

            Assert.IsTrue(leftSeen && rightSeen, "fruit has to fall on both sides");
            Assert.Greater(widest, Settings.HalfWidth * 0.7f, "and sometimes at the far edge");
        }

        [Test]
        public void Coins_RewardTheRunAndNeverGoNegative()
        {
            Assert.AreEqual(0, CatchRules.CoinsFor(0));
            Assert.AreEqual(1, CatchRules.CoinsFor(1));
            Assert.AreEqual(11, CatchRules.CoinsFor(10), "every tenth fruit is worth a second coin");
            Assert.AreEqual(0, CatchRules.CoinsFor(-4));

            int previous = -1;
            for (int caught = 0; caught < 60; caught++)
            {
                int coins = CatchRules.CoinsFor(caught);
                Assert.GreaterOrEqual(coins, previous, "a better run cannot pay less");
                previous = coins;
            }

            Assert.Greater(CatchRules.StartLives, 1, "one miss ending the run is not a game");
        }

        [Test]
        public void Rank_SaysSomethingAtEveryScore()
        {
            for (int caught = 0; caught < 90; caught++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(CatchRules.RankFor(caught)), $"no rank at {caught}");
            }
            Assert.AreNotEqual(CatchRules.RankFor(0), CatchRules.RankFor(60));
        }

        [Test]
        public void Drag_MovesTheBasketExactlyAsFarAsTheFinger()
        {
            // The control is a drag now, and its whole promise is that it does not overshoot: the
            // basket covers the distance the thumb covered, no more. This is the property that made
            // it worth replacing the two hold-to-move pads with.
            float x = CatchRules.DragTo(0f, 1.25f, Settings);
            Assert.AreEqual(1.25f, x, 0.0001f);

            x = CatchRules.DragTo(x, -0.5f, Settings);
            Assert.AreEqual(0.75f, x, 0.0001f);

            // A drag is not a speed: the same delta moves the same distance however long it took.
            float small = CatchRules.DragTo(0f, 0.2f, Settings);
            for (int i = 0; i < 5; i++) small = CatchRules.DragTo(small, 0.2f, Settings);
            Assert.AreEqual(1.2f, small, 0.0001f, "five small drags add up to one big one");
        }

        [Test]
        public void Drag_StopsAtTheSameWallsAsTheButtons()
        {
            // Two ways to steer must not disagree about where the edge is: a drag that could push
            // the basket past the limit the held-direction path enforces would put the basket off
            // the play area, and the fruit it should catch would land beside it.
            float limit = CatchRules.LimitFor(Settings);

            float right = CatchRules.DragTo(0f, 999f, Settings);
            Assert.AreEqual(limit, right, 0.0001f);

            float left = CatchRules.DragTo(0f, -999f, Settings);
            Assert.AreEqual(-limit, left, 0.0001f);

            float held = 0f;
            for (int i = 0; i < 600; i++) held = CatchRules.StepBasket(held, 1f, 1f / 60f, Settings);
            Assert.AreEqual(held, right, 0.001f, "the drag and the held direction share a wall");
        }

        [Test]
        public void Drag_PixelsBecomeWorldUnitsMonotonically()
        {
            // A drag has to move the basket *towards* the finger on every screen: a portrait phone
            // and a wide tablet cannot both be right unless the mapping scales with the width.
            float phone = CatchRules.PixelsToWorld(200f, 1080f, Settings);
            float tablet = CatchRules.PixelsToWorld(200f, 2160f, Settings);

            Assert.Greater(phone, 0f, "dragging right has to move right");
            Assert.Greater(phone, tablet, "the same pixels are worth more world on a narrow screen");

            // The whole screen is worth the whole play area, which is what makes the basket track
            // the thumb instead of lagging behind it.
            Assert.AreEqual(Settings.HalfWidth * 2f,
                CatchRules.PixelsToWorld(1080f, 1080f, Settings), 0.0001f);

            // A degenerate screen size is a divide-by-zero waiting to happen.
            Assert.IsFalse(float.IsNaN(CatchRules.PixelsToWorld(10f, 0f, Settings)));
            Assert.IsFalse(float.IsInfinity(CatchRules.PixelsToWorld(10f, 0f, Settings)));
        }
    }
}
