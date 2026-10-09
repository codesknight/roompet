using DshMiniGames;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DshMiniGames.Tests
{
    /// <summary>
    /// The hop game's arithmetic.
    ///
    /// One input and one decision means almost nothing can go wrong except the numbers, which is
    /// exactly why they are worth pinning down: how far a charge carries, what counts as landing on
    /// the box, and whether the generator can ever hand the player a hop that is physically
    /// impossible.
    ///
    /// The two tests that matter most here are the ones that play the game rather than poke at it:
    /// <see cref="EveryHopCanBeLandedFromTheWorstPlaceTheAnimalCanStand"/> runs the generator for
    /// hundreds of hops and checks the promise at every one, and
    /// <see cref="Frame_KeepsTheAnimalAndBothBoxesOnScreen"/> does the same for the camera, because
    /// both of them were reported broken from the phone and neither is visible in a unit test that
    /// only checks one function at a time.
    /// </summary>
    public class HopRulesTests
    {
        private static readonly HopSettings Settings = HopSettings.Default;

        [Test]
        public void HopDistance_GrowsWithTheChargeAndThenStops()
        {
            Assert.AreEqual(Settings.MinDistance, HopRules.HopDistance(0f, Settings), 0.001f,
                "a tap still has to go somewhere");
            Assert.AreEqual(Settings.MinDistance, HopRules.HopDistance(-5f, Settings), 0.001f,
                "a negative hold is not a backwards jump");

            float previous = 0f;
            for (float held = 0f; held <= 3f; held += 0.1f)
            {
                float distance = HopRules.HopDistance(held, Settings);
                Assert.GreaterOrEqual(distance, previous - 0.0001f, "more charge cannot mean less");
                previous = distance;
            }

            Assert.AreEqual(Settings.MaxDistance, HopRules.HopDistance(10f, Settings), 0.001f,
                "holding forever must not fly off the map");
        }

        [Test]
        public void FullChargeSeconds_MatchesTheDistanceItBuys()
        {
            // The charge bar is scaled by this, so a wrong answer means the bar fills at a different
            // rate than the distance grows — the one thing the player reads.
            float full = HopRules.FullChargeSeconds(Settings);
            Assert.AreEqual(Settings.MaxDistance, HopRules.HopDistance(full, Settings), 0.02f);
            Assert.Less(HopRules.HopDistance(full * 0.5f, Settings), Settings.MaxDistance);
            Assert.Greater(full, 0.6f, "a charge that fills in half a second is a coin toss");
            Assert.Less(full, 2.5f, "and one that takes three seconds is a chore");
        }

        [Test]
        public void Judge_TellsAMissFromALandingFromAPerfectOne()
        {
            const float centre = 5f;

            Assert.AreEqual(HopRules.Landing.Perfect,
                HopRules.Judge(0f, centre, centre, Settings));
            Assert.AreEqual(HopRules.Landing.Perfect,
                HopRules.Judge(0f, centre + Settings.PerfectRadius * 0.9f, centre, Settings),
                "just inside the middle is still perfect");

            Assert.AreEqual(HopRules.Landing.Landed,
                HopRules.Judge(0f, centre + Settings.BoxHalfSize * 0.9f, centre, Settings),
                "on the box, off centre, is a landing");
            Assert.AreEqual(HopRules.Landing.Landed,
                HopRules.Judge(0f, centre - Settings.BoxHalfSize * 0.9f, centre, Settings));

            Assert.AreEqual(HopRules.Landing.Missed,
                HopRules.Judge(0f, centre + Settings.BoxHalfSize + 0.01f, centre, Settings),
                "past the edge is a miss");
            Assert.AreEqual(HopRules.Landing.Missed,
                HopRules.Judge(0f, centre - Settings.BoxHalfSize - 0.01f, centre, Settings),
                "short of the edge is a miss too");
        }

        [Test]
        public void Judge_KeepsThePerfectWindowInsideTheBox()
        {
            // A "perfect" zone wider than the box would be a contradiction, and a perfect radius of
            // zero would make the bonus unreachable.
            Assert.Less(Settings.PerfectRadius, Settings.BoxHalfSize * 0.5f);
            Assert.Greater(Settings.PerfectRadius, 0.05f);
        }

        [Test]
        public void NextGap_IsAlwaysReachableAndNeverTrivial()
        {
            // The whole reason the generator takes the settings: a box further away than a full
            // charge reaches is not a challenge, it is a lost run the player cannot see coming.
            float reach = Settings.MaxDistance;

            for (int score = 0; score <= 80; score += 4)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float gap = HopRules.NextGap(i / 20f, score, Settings);
                    Assert.GreaterOrEqual(gap, Settings.MinGap - 0.001f,
                        $"a gap of {gap:F2} at score {score} is shorter than the minimum");
                    Assert.Less(gap, reach, $"a gap of {gap:F2} at score {score} cannot be jumped");
                }
            }
        }

        [Test]
        public void NextGap_LeavesRoomForAMistakeAtEveryScore()
        {
            // The margin matters as much as the reach: a box that only a *perfectly* full charge can
            // reach turns every hop into a coin toss on a phone's frame timing.
            for (int score = 0; score <= 80; score += 8)
            {
                float longest = HopRules.NextGap(1f, score, Settings);
                Assert.LessOrEqual(longest, Settings.MaxDistance - 0.2f,
                    $"at score {score} the longest hop needs an exact full charge");
            }
        }

        [Test]
        public void NextGap_GetsHarderWithTheScoreAndThenStops()
        {
            float early = HopRules.NextGap(0f, 0, Settings);
            float late = HopRules.NextGap(0f, 60, Settings);
            Assert.Greater(late, early, "the shortest hop should get longer as the run goes on");

            float mid = HopRules.NextGap(0f, 20, Settings);
            Assert.GreaterOrEqual(late, mid - 0.0001f, "and it must never go backwards");
        }

        [Test]
        public void Points_PayDoubleForTheMiddle()
        {
            Assert.AreEqual(1, HopRules.PointsFor(HopRules.Landing.Landed));
            Assert.AreEqual(2, HopRules.PointsFor(HopRules.Landing.Perfect),
                "the middle is what the game is about");
            Assert.AreEqual(0, HopRules.PointsFor(HopRules.Landing.Missed));
        }

        [Test]
        public void Coins_RewardTheRunAndNeverGoNegative()
        {
            Assert.AreEqual(0, HopRules.CoinsFor(0));
            Assert.AreEqual(1, HopRules.CoinsFor(1));
            Assert.AreEqual(9, HopRules.CoinsFor(8), "every eighth point is worth a second coin");
            Assert.AreEqual(0, HopRules.CoinsFor(-10));

            int previous = -1;
            for (int score = 0; score < 60; score++)
            {
                int coins = HopRules.CoinsFor(score);
                Assert.GreaterOrEqual(coins, previous, "a better run cannot pay less");
                previous = coins;
            }
        }

        [Test]
        public void Rank_SaysSomethingAtEveryScore()
        {
            for (int score = 0; score < 90; score++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(HopRules.RankFor(score)), $"no rank at {score}");
            }
            Assert.AreNotEqual(HopRules.RankFor(0), HopRules.RankFor(60),
                "a rank that never changes is not a rank");
        }

        [Test]
        public void Arc_StartsAndEndsAtTheTwoPointsAndGoesUpInBetween()
        {
            // The animation must agree with the landing the rules already judged: the character
            // arrives exactly where Judge was told they would.
            var start = HopRules.ArcPosition(2f, 5f, 0f, Settings);
            var middle = HopRules.ArcPosition(2f, 5f, 0.5f, Settings);
            var end = HopRules.ArcPosition(2f, 5f, 1f, Settings);

            Assert.AreEqual(2f, start.x, 0.001f);
            Assert.AreEqual(0f, start.y, 0.001f);
            Assert.AreEqual(5f, end.x, 0.001f);
            Assert.AreEqual(0f, end.y, 0.001f);
            Assert.Greater(middle.y, 0f, "a hop that does not leave the ground is a slide");
            Assert.AreEqual(3.5f, middle.x, 0.001f);

            Assert.AreEqual(2f, HopRules.ArcPosition(2f, 5f, -1f, Settings).x, 0.001f,
                "a negative t clamps rather than flying backwards");
            Assert.AreEqual(5f, HopRules.ArcPosition(2f, 5f, 9f, Settings).x, 0.001f);
        }

        [Test]
        public void Drift_KeepsTheBoxesNearTheLine()
        {
            // The landing test is measured in the plane the animal hops across: if the boxes drifted
            // far sideways, the game would be lying about where the player can land.
            for (int i = 0; i <= 10; i++)
            {
                float drift = HopRules.NextDrift(i / 10f, Settings);
                Assert.LessOrEqual(Mathf.Abs(drift), Settings.BoxHalfSize * 1.2f + 0.001f);
            }
            Assert.Less(HopRules.NextDrift(0f, Settings), HopRules.NextDrift(1f, Settings),
                "the roll has to actually move it");
        }

        // ------------------------------------------------------------------ the fairness promise
        //
        // Reported from the phone: "确保每次跳跃距离一定在最大跳跃范围内、不会出现跳不过去". The old
        // generator capped the gap centre-to-centre and ignored where the animal had actually come
        // down, so landing on the near edge of a box could make the next hop unreachable. These four
        // tests are the promise, stated four ways.

        [Test]
        public void Settings_LeaveRoomForTheWorstLandingOnEveryHop()
        {
            // The arithmetic the generator depends on, as assertions rather than as a comment.
            float safe = HopRules.SafeGap(Settings);

            Assert.Greater(safe, Settings.MinGap, "a gap range with no range in it is not a game");
            Assert.Less(safe + Settings.BoxHalfSize, Settings.MaxDistance,
                "the longest hop from the worst landing still has to leave the safety margin");

            // Two boxes drifting opposite ways is the most sideways a hop can be asked to travel.
            float sideways = Settings.MaxDrift * 2f;
            float straight = Mathf.Sqrt(Mathf.Max(0f, safe * safe - sideways * sideways));
            Assert.GreaterOrEqual(straight, Settings.MinGap,
                "the drift alone must not eat the whole hop budget");

            // The shortest hop there is, measured from the worst landing: the charge that lands it
            // has to be a range a thumb can hit, not a single frame.
            Assert.GreaterOrEqual(
                HopRules.LandingWindowSeconds(Settings.MinGap - Settings.BoxHalfSize, Settings), 0.3f,
                "even the shortest hop has to be more than a knife edge");
        }

        [Test]
        public void EveryHopCanBeLandedFromTheWorstPlaceTheAnimalCanStand()
        {
            var rng = new System.Random(20240607);
            var centre = Vector2.zero;
            float drift = 0f;
            int score = 0;

            for (int hop = 0; hop < 250; hop++)
            {
                // The next box, laid out exactly the way the game lays it out.
                float nextDrift = HopRules.NextDrift((float)rng.NextDouble(), Settings);
                float sideways = Mathf.Abs(nextDrift - drift);
                float gap = HopRules.NextGap((float)rng.NextDouble(), score, sideways, Settings);
                var next = new Vector2(centre.x + gap, nextDrift);

                // The worst place the animal can be standing: the near edge of the box it is on,
                // as far from the next box as the box allows. Anywhere else is closer, so this is
                // the hop the generator has to keep possible.
                var standing = centre - (next - centre).normalized * Settings.BoxHalfSize;
                float distance = (next - standing).magnitude;

                Assert.IsTrue(HopRules.CanReach(standing, next, Settings),
                    $"hop {hop}: the box at {next} cannot be reached from {standing}");
                Assert.LessOrEqual(distance, Settings.MaxDistance - Settings.SafetyMargin + 0.0001f,
                    $"hop {hop}: the hop needs {distance:F2} of a charge that reaches {Settings.MaxDistance}");

                float window = HopRules.LandingWindowSeconds(distance, Settings);
                Assert.GreaterOrEqual(window, 0.3f,
                    $"hop {hop}: only {window:F2}s of holding lands on the box");

                // And a player holding for exactly the right time lands on it, dead centre — which
                // is the whole loop: the guess is the game, the reach is arithmetic.
                float held = HopRules.ChargeFor(distance, Settings);
                var landing = HopRules.AimedLanding(standing, next,
                    HopRules.HopDistance(held, Settings));

                Assert.AreEqual(HopRules.Landing.Perfect, HopRules.JudgeAt(landing, next, Settings),
                    $"hop {hop}: aiming at the box did not land on it");

                centre = next;
                drift = nextDrift;
                score += 2;
            }
        }

        [Test]
        public void AimedLanding_TravelsStraightAtTheBox()
        {
            var from = new Vector2(0f, 0f);
            var target = new Vector2(3f, 1f);        // a box that is off to the side
            float needed = (target - from).magnitude;

            // Short, exactly right, and long: all three are on the line to the box, so a miss is
            // visibly "short" or "long" rather than mysteriously beside the box.
            var shortHop = HopRules.AimedLanding(from, target, needed - 1f);
            var exact = HopRules.AimedLanding(from, target, needed);
            var longHop = HopRules.AimedLanding(from, target, needed + 1f);

            Assert.AreEqual(0f, Cross(target - from, shortHop - from), 0.0001f,
                "a hop that is short has to be short *along the line*, not off it");
            Assert.AreEqual(0f, Cross(target - from, longHop - from), 0.0001f);
            Assert.AreEqual(needed - 1f, (shortHop - from).magnitude, 0.0001f);
            Assert.AreEqual(target.x, exact.x, 0.0001f);
            Assert.AreEqual(target.y, exact.y, 0.0001f);
            Assert.Less((shortHop - target).magnitude, (longHop - target).magnitude,
                "one of them has to be short and the other long");

            // The bug this replaced: a fixed direction along +X, which flew past any box that sat
            // to the side. The animal must not still be doing that.
            Assert.Greater(Mathf.Abs(exact.y), Settings.BoxHalfSize * 0.5f,
                "the test target is too close to the line to prove anything");
            Assert.AreNotEqual(0f, shortHop.y, "the hop went along +X instead of at the box");
        }

        [Test]
        public void NextGap_SpendsTheSidewaysOffsetOutOfTheSameBudget()
        {
            // The hop is a line to the box, so a box 0.6 to the side costs 0.6 of length. The gap has
            // to give that back, or the reachability proof is only true for boxes on the line.
            float safe = HopRules.SafeGap(Settings);

            for (float sideways = 0f; sideways <= Settings.MaxDrift * 2f; sideways += 0.15f)
            {
                for (int i = 0; i <= 10; i++)
                {
                    float gap = HopRules.NextGap(i / 10f, 40, sideways, Settings);
                    float length = new Vector2(gap, sideways).magnitude;

                    Assert.LessOrEqual(length, safe + 0.001f,
                        $"a gap of {gap:F2} with {sideways:F2} of drift needs {length:F2}");
                    Assert.GreaterOrEqual(gap, Settings.MinGap - 0.001f);
                }
            }
        }

        // ------------------------------------------------------------------ the camera

        [Test]
        public void Frame_KeepsTheAnimalAndBothBoxesOnScreen()
        {
            // "相机跟随动物视角": the camera is computed, not eyeballed, so this is a property that
            // can be asserted for every screen shape rather than checked by looking at one.
            var rotation = Quaternion.Euler(27f, 56f, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;

            float[] aspects = { 0.42f, 0.5f, 0.5625f, 0.75f, 1f, 1.6f, 1.7778f, 2.4f };
            float[] gaps = { Settings.MinGap, 2f, HopRules.SafeGap(Settings) };

            foreach (float aspect in aspects)
            {
                foreach (float gap in gaps)
                {
                    foreach (float sideways in new[] { -Settings.MaxDrift, 0f, Settings.MaxDrift })
                    {
                        var points = BuildView(gap, sideways, Settings);
                        Vector3 focus;
                        float size;
                        HopRules.Frame(points, right, up, aspect, 0.7f, 3.5f, 40f,
                            out focus, out size);

                        for (int i = 0; i < points.Count; i++)
                        {
                            var offset = HopRules.ViewOffset(points[i], focus, right, up, size, aspect);
                            Assert.LessOrEqual(Mathf.Abs(offset.x), 1.001f,
                                $"aspect {aspect:F2}, gap {gap:F2}: a point is off the side");
                            Assert.LessOrEqual(Mathf.Abs(offset.y), 1.001f,
                                $"aspect {aspect:F2}, gap {gap:F2}: a point is off the top or bottom");
                        }
                    }
                }
            }
        }

        [Test]
        public void Frame_StaysInsideItsZoomLimitForRealGeometry()
        {
            // The frame has a ceiling so a runaway hop cannot zoom the toy into a dot. A ceiling is
            // also a way to break the promise above, so this asserts it never actually binds at the
            // widest real geometry — the animal, the box it is on, and the furthest box there is.
            var rotation = Quaternion.Euler(27f, 56f, 0f);
            var points = BuildView(HopRules.SafeGap(Settings), Settings.MaxDrift, Settings);

            Vector3 focus;
            float size;
            HopRules.Frame(points, rotation * Vector3.right, rotation * Vector3.up, 0.42f, 0.7f,
                3.5f, 11f, out focus, out size);

            Assert.Less(size, 11f, "the zoom ceiling is clipping the view at real geometry");
        }

        /// <summary>
        /// The points the game hands the camera: the animal (in the air, at the top of its arc), the
        /// middle of the box it is standing on, the eight corners of the box it is going to, and the
        /// look-ahead point a third of the way between them.
        /// </summary>
        private static List<Vector3> BuildView(float gap, float sideways, HopSettings settings)
        {
            var points = new List<Vector3>();
            var nextBase = new Vector3(gap, 0f, sideways);
            var animal = new Vector3(gap * 0.3f, settings.HopArc, sideways * 0.5f);

            points.Add(animal);
            points.Add(animal + Vector3.up * 1.05f);

            points.Add(Vector3.zero);
            points.Add(new Vector3(0f, 1.1f, 0f));

            Corners(points, nextBase, settings.BoxHalfSize);

            points.Add(Vector3.Lerp(animal, nextBase + Vector3.up * 1.1f, 0.35f));
            return points;
        }

        private static void Corners(List<Vector3> points, Vector3 centre, float half)
        {
            for (int i = 0; i < 4; i++)
            {
                float dx = (i == 0 || i == 3) ? -half : half;
                float dz = i < 2 ? -half : half;

                points.Add(new Vector3(centre.x + dx, 0f, centre.z + dz));
                points.Add(new Vector3(centre.x + dx, 1.1f, centre.z + dz));
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
