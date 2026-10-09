using NUnit.Framework;
using UnityEngine;

namespace DshPet.Tests
{
    /// <summary>
    /// The placement and texture arithmetic behind the garden and the terrace.
    ///
    /// These are the decisions that go wrong silently: a fence that ends in a half-bay gap, a
    /// flower growing out of the bathtub, a string of lights that sags to nowhere. As data they
    /// can be checked without building a scene, which is the whole point of
    /// <see cref="RoomDecor"/> and <see cref="RoomTextures"/> being separate from PetRoom.
    /// </summary>
    public class RoomDecorTests
    {
        [Test]
        public void Perimeter_EvenlySpacesPostsAroundASquare()
        {
            var points = RoomDecor.Perimeter(14f, 1.6f);
            float spacing = RoomDecor.PerimeterSpacing(14f, 1.6f);
            int bays = Mathf.RoundToInt(14f / 1.6f);

            Assert.AreEqual(bays * 4, points.Length, "four sides of bays posts, every corner once");
            Assert.Less(spacing, 1.6f + 0.01f, "spacing must be divided out, not oversized");

            foreach (var point in points)
            {
                bool onPerimeter =
                    Mathf.Approximately(Mathf.Abs(point.x), 7f) ||
                    Mathf.Approximately(Mathf.Abs(point.y), 7f);
                Assert.IsTrue(onPerimeter, "post drifted off the fence line: " + point);
            }

            // The loop closes: the last post and the first are one bay apart, on the same edge.
            Assert.Greater(Vector2.Distance(points[points.Length - 1], points[0]), 0.001f);
            Assert.Less(Vector2.Distance(points[points.Length - 1], points[0]), spacing * 1.01f);
        }

        [Test]
        public void Flowers_AreDeterministicInsideTheBedAndOutOfTheFurniture()
        {
            var bed = new BedPlan { Centre = new Vector2(0f, 0f), Radius = 2f };
            var keepOut = new[] { new Vector2(1f, 0f) };

            var a = RoomDecor.Flowers(bed, 1234, keepOut, 0.9f, 0.4f, 9);
            var b = RoomDecor.Flowers(bed, 1234, keepOut, 0.9f, 0.4f, 9);

            Assert.AreEqual(a.Length, b.Length, "same seed must give the same bed");
            Assert.Greater(a.Length, 4, "a bed with three flowers is not a flower bed");

            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].At, b[i].At, "placement is not deterministic");
                Assert.LessOrEqual(Vector2.Distance(a[i].At, bed.Centre), bed.Radius, "flower left its soil");
                Assert.IsFalse(RoomDecor.Blocked(a[i].At, keepOut, 0.9f), "flower inside a keep-out circle");
            }

            for (int i = 0; i < a.Length; i++)
            {
                for (int j = i + 1; j < a.Length; j++)
                {
                    Assert.Greater(Vector2.Distance(a[i].At, a[j].At), 0.4f * 0.9f,
                        "two flowers in one hole look like one flower");
                }
            }
        }

        [Test]
        public void Flowers_AcrossTheGardenAreColourful()
        {
            // 「五颜六色」: the whole garden, not one bed, must read as many colours. Eight beds
            // each cycle the palette from a different start, so no two beds match.
            var keepOut = RoomDecor.FurnitureKeepOut();
            var beds = RoomDecor.Beds(14f);
            var seen = new System.Collections.Generic.HashSet<Color>();

            for (int i = 0; i < beds.Length; i++)
            {
                var plans = RoomDecor.Flowers(beds[i], 9173 + i * 613, keepOut, 1.15f, 0.4f, 9);
                for (int f = 0; f < plans.Length; f++) seen.Add(plans[f].Colour);
            }

            Assert.GreaterOrEqual(seen.Count, 4, "a garden with fewer than four colours is not 五颜六色");
        }

        [Test]
        public void Beds_StayInsideTheGarden()
        {
            var beds = RoomDecor.Beds(14f);
            Assert.AreEqual(7, beds.Length);
            foreach (var bed in beds)
            {
                Assert.LessOrEqual(Mathf.Abs(bed.Centre.x) + bed.Radius, 7f + 0.01f, "bed hangs off the lawn");
                Assert.LessOrEqual(Mathf.Abs(bed.Centre.y) + bed.Radius, 7f + 0.01f, "bed hangs off the lawn");
            }
        }

        [Test]
        public void StringLights_SagSymmetricallyAndLowestInTheMiddle()
        {
            var points = RoomDecor.StringLights(new Vector3(-3f, 2f, 0f), new Vector3(3f, 2f, 0f), 13, 0.5f);

            Assert.AreEqual(13, points.Length);
            Assert.AreEqual(-3f, points[0].x, 0.0001f);
            Assert.AreEqual(3f, points[points.Length - 1].x, 0.0001f);

            float lowest = float.MaxValue;
            int lowestIndex = -1;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].y < lowest) { lowest = points[i].y; lowestIndex = i; }

                // Symmetry: bulb i and its mirror sit at the same height and opposite x.
                int mirror = points.Length - 1 - i;
                Assert.AreEqual(points[i].y, points[mirror].y, 0.0001f, "lights must sag the same both ways");
            }

            Assert.AreEqual(points.Length / 2, lowestIndex, "the middle of a string of lights sags lowest");
            Assert.Less(lowest, 2f, "the wire has to actually sag");
        }

        [Test]
        public void Themes_HaveDistinctShells()
        {
            var cabin = RoomThemeInfo.Get(RoomTheme.Cabin);
            var garden = RoomThemeInfo.Get(RoomTheme.Garden);
            var terrace = RoomThemeInfo.Get(RoomTheme.Terrace);

            Assert.AreEqual(RoomShell.Cabin, cabin.Shell);
            Assert.AreEqual(RoomShell.Fenced, garden.Shell, "a garden is enclosed by a fence, not walls");
            Assert.AreEqual(RoomShell.Railed, terrace.Shell, "a terrace is railed at the edge, not walled");
            Assert.IsTrue(garden.Playful, "the garden is where the pet plays");
            Assert.IsFalse(cabin.Playful, "the cabin is not the garden");
        }

        [Test]
        public void Textures_AreSizedAndActuallyPatterned()
        {
            var grass = RoomTextures.Grass();
            var deck = RoomTextures.Deck();
            var sky = RoomTextures.Skyline();
            var pickets = RoomTextures.Pickets();
            var flower = RoomTextures.Flower(RoomDecor.FlowerColours[0], 6);

            Assert.AreEqual(128, grass.width);
            Assert.AreEqual(128, deck.width);
            Assert.AreEqual(256, sky.width);
            Assert.AreEqual(128, pickets.width);
            Assert.AreEqual(64, flower.width);

            // The skyline: sky is dark, the city glow near the horizon is lighter.
            var top = sky.GetPixel(sky.width / 2, sky.height - 2);
            var bottom = sky.GetPixel(sky.width / 2, 1);
            Assert.Less(top.grayscale, bottom.grayscale, "the night sky must get lighter toward the city");

            // The picket fence: gaps are transparent, pickets are not. (The pickets are centred
            // in their pitch, so x=1 is a gap and x=11 the middle of a picket.)
            Assert.AreEqual(0f, pickets.GetPixel(1, 10).a, 0.001f, "gap between pickets must be transparent");
            Assert.AreEqual(1f, pickets.GetPixel(11, 10).a, 0.001f, "a picket must be solid");

            // A flower is mostly transparent but has a solid head somewhere.
            bool anySolid = false;
            for (int y = 20; y < 50 && !anySolid; y++)
            {
                for (int x = 0; x < flower.width && !anySolid; x++)
                {
                    if (flower.GetPixel(x, y).a > 0.5f) anySolid = true;
                }
            }
            Assert.IsTrue(anySolid, "a flower with no petals is a stem");

            RoomTextures.ClearCache();
        }
    }
}
