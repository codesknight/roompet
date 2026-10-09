using UnityEngine;

namespace DshPet
{
    /// <summary>The animals a memory-match card can show.</summary>
    public enum PetFaceKind { Cat = 0, Dog = 1, Rabbit = 2, Bear = 3, Fox = 4, Panda = 5, Pig = 6, Frog = 7 }

    /// <summary>
    /// The little animal faces on the memory-match cards, drawn in code.
    ///
    /// Why not art: this project has no artist and no licence for one, and a card game that needs
    /// eight icons shipped as PNGs is a card game that stops working the moment someone changes a
    /// resolution. Why not the emoji the first version used: Unity's built-in IMGUI font has no
    /// emoji, so every card came out blank — the same lesson the pet chips, the round buttons and
    /// the lives counter each taught separately.
    ///
    /// So the faces are rasterised here, at 96x96, into real <see cref="Texture2D"/>s: a head
    /// ellipse, two ears that differ per animal, eyes with a glint, a muzzle and a nose. Read as a
    /// whole the result is a cartoon animal; read pixel by pixel it is a handful of ellipses and
    /// triangles, which is exactly why it can be tested without looking at it.
    ///
    /// The textures are cached and created on demand — eight of them, once, at 96px — because a
    /// phone that rasterises the same face on every repaint would drop frames in a card game, which
    /// is the one place where it can least afford to.
    /// </summary>
    public static class PetAvatarArt
    {
        /// <summary>Edge length of every face texture, in pixels.</summary>
        public const int Size = 96;

        private static readonly Texture2D[] Cache = new Texture2D[8];

        /// <summary>How many different animals there are.</summary>
        public static int KindCount => Cache.Length;

        /// <summary>The animal's name, for messages and for anyone who cannot see the card.</summary>
        public static string Name(PetFaceKind kind)
        {
            switch (kind)
            {
                case PetFaceKind.Cat: return "猫";
                case PetFaceKind.Dog: return "狗";
                case PetFaceKind.Rabbit: return "兔";
                case PetFaceKind.Bear: return "熊";
                case PetFaceKind.Fox: return "狐狸";
                case PetFaceKind.Panda: return "熊猫";
                case PetFaceKind.Pig: return "猪";
                default: return "青蛙";
            }
        }

        /// <summary>A deck index (0..7) as an animal, clamped so a bad index cannot throw.</summary>
        public static PetFaceKind KindAt(int face)
            => (PetFaceKind)Mathf.Clamp(face, 0, KindCount - 1);

        /// <summary>The texture for a deck index. Built once, then reused forever.</summary>
        public static Texture2D TextureFor(int face)
        {
            int index = Mathf.Clamp(face, 0, KindCount - 1);
            if (Cache[index] != null) return Cache[index];

            var canvas = new Canvas(Size);
            Paint(canvas, (PetFaceKind)index);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "PetFace_" + Name((PetFaceKind)index),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(canvas.Pixels);
            texture.Apply(false, false);

            Cache[index] = texture;
            return texture;
        }

        /// <summary>Forgets the cache, so a test can rebuild from scratch.</summary>
        public static void ClearCache()
        {
            for (int i = 0; i < Cache.Length; i++)
            {
                if (Cache[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(Cache[i]);
                else Object.DestroyImmediate(Cache[i]);
                Cache[i] = null;
            }
        }

        // ------------------------------------------------------------------ palettes

        private static readonly Color32 Dark = new Color32(48, 38, 46, 255);
        private static readonly Color32 White = new Color32(250, 250, 252, 255);
        private static readonly Color32 Pink = new Color32(238, 160, 172, 255);
        private static readonly Color32 Cheek = new Color32(246, 168, 168, 190);

        private static Color32 Fur(PetFaceKind kind)
        {
            switch (kind)
            {
                case PetFaceKind.Cat: return new Color32(244, 186, 108, 255);
                case PetFaceKind.Dog: return new Color32(206, 158, 108, 255);
                case PetFaceKind.Rabbit: return new Color32(246, 242, 240, 255);
                case PetFaceKind.Bear: return new Color32(158, 112, 76, 255);
                case PetFaceKind.Fox: return new Color32(238, 142, 72, 255);
                case PetFaceKind.Panda: return new Color32(246, 246, 248, 255);
                case PetFaceKind.Pig: return new Color32(246, 178, 194, 255);
                default: return new Color32(140, 208, 116, 255);
            }
        }

        private static Color32 Darken(Color32 c, float amount)
            => new Color32(
                (byte)(c.r * (1f - amount)),
                (byte)(c.g * (1f - amount)),
                (byte)(c.b * (1f - amount)),
                c.a);

        // ------------------------------------------------------------------ painting

        /// <summary>
        /// One animal. Order matters and is the same for all of them: ears behind the head, then the
        /// head, then whatever is printed on the face (patches, muzzle, eyes, nose).
        /// </summary>
        private static void Paint(Canvas c, PetFaceKind kind)
        {
            Color32 fur = Fur(kind);
            Color32 furDark = Darken(fur, 0.22f);

            float cx = Size * 0.5f, cy = Size * 0.58f;
            float rx = Size * 0.34f, ry = Size * 0.32f;

            Ears(c, kind, fur, furDark, cx, cy, rx, ry);

            // Head: a darker pass a little larger first, which reads as an outline against a busy
            // card back without any per-pixel edge work.
            c.Ellipse(cx, cy, rx + 1.5f, ry + 1.5f, furDark);
            c.Ellipse(cx, cy, rx, ry, fur);

            Face(c, kind, fur, furDark, cx, cy, rx, ry);
        }

        private static void Ears(Canvas c, PetFaceKind kind, Color32 fur, Color32 furDark,
            float cx, float cy, float rx, float ry)
        {
            switch (kind)
            {
                case PetFaceKind.Cat:
                    c.Triangle(cx - rx * 0.92f, cy - ry * 0.55f, cx - rx * 0.30f, cy - ry * 2.05f,
                        cx - rx * 0.02f, cy - ry * 0.72f, fur);
                    c.Triangle(cx + rx * 0.92f, cy - ry * 0.55f, cx + rx * 0.30f, cy - ry * 2.05f,
                        cx + rx * 0.02f, cy - ry * 0.72f, fur);
                    break;

                case PetFaceKind.Dog:
                    // Droopy ears hanging beside the head.
                    c.Ellipse(cx - rx * 1.02f, cy + ry * 0.16f, rx * 0.34f, ry * 0.70f, furDark);
                    c.Ellipse(cx + rx * 1.02f, cy + ry * 0.16f, rx * 0.34f, ry * 0.70f, furDark);
                    break;

                case PetFaceKind.Rabbit:
                    c.Ellipse(cx - rx * 0.42f, cy - ry * 1.35f, rx * 0.24f, ry * 0.92f, fur);
                    c.Ellipse(cx + rx * 0.42f, cy - ry * 1.35f, rx * 0.24f, ry * 0.92f, fur);
                    c.Ellipse(cx - rx * 0.42f, cy - ry * 1.32f, rx * 0.12f, ry * 0.66f, Pink);
                    c.Ellipse(cx + rx * 0.42f, cy - ry * 1.32f, rx * 0.12f, ry * 0.66f, Pink);
                    break;

                case PetFaceKind.Bear:
                    c.Ellipse(cx - rx * 0.78f, cy - ry * 0.80f, rx * 0.34f, ry * 0.34f, furDark);
                    c.Ellipse(cx + rx * 0.78f, cy - ry * 0.80f, rx * 0.34f, ry * 0.34f, furDark);
                    c.Ellipse(cx - rx * 0.78f, cy - ry * 0.80f, rx * 0.18f, ry * 0.18f, fur);
                    c.Ellipse(cx + rx * 0.78f, cy - ry * 0.80f, rx * 0.18f, ry * 0.18f, fur);
                    break;

                case PetFaceKind.Fox:
                    c.Triangle(cx - rx * 1.05f, cy - ry * 0.30f, cx - rx * 0.42f, cy - ry * 1.95f,
                        cx - rx * 0.02f, cy - ry * 0.55f, furDark);
                    c.Triangle(cx + rx * 1.05f, cy - ry * 0.30f, cx + rx * 0.42f, cy - ry * 1.95f,
                        cx + rx * 0.02f, cy - ry * 0.55f, furDark);
                    c.Triangle(cx - rx * 0.86f, cy - ry * 0.62f, cx - rx * 0.50f, cy - ry * 1.55f,
                        cx - rx * 0.26f, cy - ry * 0.72f, White);
                    c.Triangle(cx + rx * 0.86f, cy - ry * 0.62f, cx + rx * 0.50f, cy - ry * 1.55f,
                        cx + rx * 0.26f, cy - ry * 0.72f, White);
                    break;

                case PetFaceKind.Panda:
                    c.Ellipse(cx - rx * 0.76f, cy - ry * 0.82f, rx * 0.33f, ry * 0.33f, Dark);
                    c.Ellipse(cx + rx * 0.76f, cy - ry * 0.82f, rx * 0.33f, ry * 0.33f, Dark);
                    break;

                case PetFaceKind.Pig:
                    c.Triangle(cx - rx * 0.78f, cy - ry * 0.55f, cx - rx * 0.52f, cy - ry * 1.55f,
                        cx - rx * 0.12f, cy - ry * 0.76f, furDark);
                    c.Triangle(cx + rx * 0.78f, cy - ry * 0.55f, cx + rx * 0.52f, cy - ry * 1.55f,
                        cx + rx * 0.12f, cy - ry * 0.76f, furDark);
                    break;

                default: // Frog: the eyes are on top of the head, which is the whole silhouette.
                    c.Ellipse(cx - rx * 0.66f, cy - ry * 1.05f, rx * 0.34f, ry * 0.34f, fur);
                    c.Ellipse(cx + rx * 0.66f, cy - ry * 1.05f, rx * 0.34f, ry * 0.34f, fur);
                    c.Ellipse(cx - rx * 0.66f, cy - ry * 1.05f, rx * 0.17f, ry * 0.17f, Dark);
                    c.Ellipse(cx + rx * 0.66f, cy - ry * 1.05f, rx * 0.17f, ry * 0.17f, Dark);
                    break;
            }
        }

        private static void Face(Canvas c, PetFaceKind kind, Color32 fur, Color32 furDark,
            float cx, float cy, float rx, float ry)
        {
            float eyeY = cy - ry * 0.10f;
            float eyeX = rx * 0.40f;
            float eyeR = rx * 0.15f;

            if (kind == PetFaceKind.Panda)
            {
                // The patches have to go in before the eyes, or the eyes vanish under them.
                c.Ellipse(cx - eyeX, eyeY, rx * 0.28f, ry * 0.30f, Dark);
                c.Ellipse(cx + eyeX, eyeY, rx * 0.28f, ry * 0.30f, Dark);
            }

            if (kind == PetFaceKind.Frog)
            {
                // A frog's mouth is the widest line on its face; no muzzle, no nose.
                c.Rect(cx - rx * 0.52f, cy + ry * 0.34f, cx + rx * 0.52f, cy + ry * 0.44f, Dark);
                c.Rect(cx - rx * 0.34f, cy + ry * 0.50f, cx - rx * 0.20f, cy + ry * 0.58f, Dark);
                c.Rect(cx + rx * 0.20f, cy + ry * 0.50f, cx + rx * 0.34f, cy + ry * 0.58f, Dark);
                return;
            }

            // Muzzle: a lighter oval the nose sits on. Pig gets a snout instead, with nostrils.
            if (kind == PetFaceKind.Pig)
            {
                c.Ellipse(cx, cy + ry * 0.40f, rx * 0.42f, ry * 0.30f, Darken(fur, 0.10f));
                c.Ellipse(cx - rx * 0.16f, cy + ry * 0.40f, rx * 0.07f, ry * 0.11f, Dark);
                c.Ellipse(cx + rx * 0.16f, cy + ry * 0.40f, rx * 0.07f, ry * 0.11f, Dark);
            }
            else
            {
                c.Ellipse(cx, cy + ry * 0.40f, rx * 0.40f, ry * 0.28f,
                    kind == PetFaceKind.Rabbit ? new Color32(240, 232, 230, 255) : White);
                c.Triangle(cx - rx * 0.13f, cy + ry * 0.30f, cx + rx * 0.13f, cy + ry * 0.30f,
                    cx, cy + ry * 0.48f, Dark);

                // A two-stroke mouth under the nose.
                c.Rect(cx - rx * 0.02f, cy + ry * 0.48f, cx + rx * 0.02f, cy + ry * 0.64f, Dark);
                c.Rect(cx - rx * 0.20f, cy + ry * 0.62f, cx + rx * 0.02f, cy + ry * 0.66f, Dark);
                c.Rect(cx + rx * 0.02f, cy + ry * 0.62f, cx + rx * 0.20f, cy + ry * 0.66f, Dark);
            }

            // Eyes over everything, each with a highlight, because a face without one reads as dead.
            c.Ellipse(cx - eyeX, eyeY, eyeR, eyeR * 1.15f, Dark);
            c.Ellipse(cx + eyeX, eyeY, eyeR, eyeR * 1.15f, Dark);
            c.Ellipse(cx - eyeX + eyeR * 0.35f, eyeY - eyeR * 0.40f, eyeR * 0.34f, eyeR * 0.34f, White);
            c.Ellipse(cx + eyeX + eyeR * 0.35f, eyeY - eyeR * 0.40f, eyeR * 0.34f, eyeR * 0.34f, White);

            if (kind == PetFaceKind.Bear || kind == PetFaceKind.Dog || kind == PetFaceKind.Pig)
            {
                // Cheek blush, the cheapest way to make a brown blob look friendly.
                c.Ellipse(cx - rx * 0.66f, cy + ry * 0.20f, rx * 0.16f, ry * 0.10f, Cheek);
                c.Ellipse(cx + rx * 0.66f, cy + ry * 0.20f, rx * 0.16f, ry * 0.10f, Cheek);
            }

            if (kind == PetFaceKind.Cat || kind == PetFaceKind.Fox)
            {
                // Whiskers: three thin strokes a side.
                for (int i = 0; i < 3; i++)
                {
                    float y = cy + ry * (0.16f + i * 0.13f);
                    c.Rect(cx - rx * 0.98f, y, cx - rx * 0.46f, y + 1.6f, furDark);
                    c.Rect(cx + rx * 0.46f, y, cx + rx * 0.98f, y + 1.6f, furDark);
                }
            }
        }

        // ------------------------------------------------------------------ raster

        /// <summary>
        /// A tiny software rasteriser: ellipses, triangles and rects into a pixel buffer.
        ///
        /// Coordinates are given with <b>y downwards</b> (the way a face is described), and flipped
        /// on the way into the buffer, because Unity's texture rows start at the bottom. Every shape
        /// is sampled 2x2 per pixel, which is what stops a 96px cartoon from looking like a
        /// staircase once a card scales it.
        /// </summary>
        private class Canvas
        {
            public readonly int Size;
            public readonly Color32[] Pixels;

            public Canvas(int size)
            {
                Size = size;
                Pixels = new Color32[size * size];
            }

            private void Plot(int x, int y, Color32 color, float coverage)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size) return;
                if (coverage <= 0f) return;

                int index = (Size - 1 - y) * Size + x;
                Color32 under = Pixels[index];

                // Source-over with the shape's own alpha folded into the coverage.
                float a = Mathf.Clamp01(coverage * (color.a / 255f));
                if (a >= 0.999f)
                {
                    Pixels[index] = new Color32(color.r, color.g, color.b, 255);
                    return;
                }

                if (a <= 0.001f) return;

                float underA = under.a / 255f;
                float outA = a + underA * (1f - a);
                if (outA <= 0.001f)
                {
                    Pixels[index] = new Color32(0, 0, 0, 0);
                    return;
                }

                float r = (color.r * a + under.r * underA * (1f - a)) / outA;
                float g = (color.g * a + under.g * underA * (1f - a)) / outA;
                float b = (color.b * a + under.b * underA * (1f - a)) / outA;
                Pixels[index] = new Color32((byte)r, (byte)g, (byte)b, (byte)(outA * 255f));
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color32 color)
            {
                if (rx <= 0f || ry <= 0f) return;

                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx) - 1);
                int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + rx) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry) - 1);
                int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + ry) + 1);

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int hits = 0;
                        for (int sy = 0; sy < 2; sy++)
                        {
                            for (int sx = 0; sx < 2; sx++)
                            {
                                float px = x + 0.25f + sx * 0.5f;
                                float py = y + 0.25f + sy * 0.5f;
                                float dx = (px - cx) / rx;
                                float dy = (py - cy) / ry;
                                if (dx * dx + dy * dy <= 1f) hits++;
                            }
                        }

                        if (hits > 0) Plot(x, y, color, hits * 0.25f);
                    }
                }
            }

            public void Triangle(float ax, float ay, float bx, float by, float cx, float cy,
                Color32 color)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx))) - 1);
                int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy))) - 1);
                int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy))) + 1);

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int hits = 0;
                        for (int sy = 0; sy < 2; sy++)
                        {
                            for (int sx = 0; sx < 2; sx++)
                            {
                                float px = x + 0.25f + sx * 0.5f;
                                float py = y + 0.25f + sy * 0.5f;
                                if (Inside(ax, ay, bx, by, cx, cy, px, py)) hits++;
                            }
                        }

                        if (hits > 0) Plot(x, y, color, hits * 0.25f);
                    }
                }
            }

            private static bool Inside(float ax, float ay, float bx, float by, float cx, float cy,
                float px, float py)
            {
                float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
                float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
                float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);

                bool anyNegative = d1 < 0f || d2 < 0f || d3 < 0f;
                bool anyPositive = d1 > 0f || d2 > 0f || d3 > 0f;
                return !(anyNegative && anyPositive);
            }

            public void Rect(float x0, float y0, float x1, float y1, Color32 color)
            {
                int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1)));
                int right = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1)));
                int top = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1)));
                int bottom = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1)));

                for (int y = top; y <= bottom; y++)
                {
                    for (int x = left; x <= right; x++) Plot(x, y, color, 1f);
                }
            }
        }
    }
}
