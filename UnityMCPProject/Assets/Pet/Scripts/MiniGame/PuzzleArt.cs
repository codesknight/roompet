using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Draws the picture the puzzle is made of.
    ///
    /// Procedural, like everything else in this project: a puzzle needs exactly one image, and
    /// shipping a JPEG for it would be the only binary art in the repository — and would also
    /// mean the puzzle always shows the same thing. Painting it per run means the picture can be
    /// this pet, in this room, as it is right now, which is a much better reason to solve it.
    ///
    /// The picture is 96x96 and blurry on purpose: a sliding puzzle is solved by noticing which
    /// pieces belong together, and sharp detail makes it a test of eyesight instead.
    /// </summary>
    public static class PuzzleArt
    {
        public const int Size = 96;

        /// <summary>Paints one picture. Deterministic for a given seed.</summary>
        public static Texture2D Paint(PetSpecies species, RoomThemeInfo theme, int seed)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "PetPuzzleArt",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            var rng = new System.Random(seed);

            var info = theme ?? RoomThemeInfo.Get(RoomTheme.Cabin);
            var fur = species != null ? (Color)species.Fur : new Color(0.86f, 0.55f, 0.28f);
            bool night = info.Theme == RoomTheme.Terrace;

            Color skyTop = night ? new Color(0.16f, 0.18f, 0.34f) : Color.Lerp(info.Wall, Color.white, 0.35f);
            Color skyBottom = night ? new Color(0.32f, 0.30f, 0.48f) : Color.Lerp(info.Light, info.Wall, 0.45f);
            Color ground = info.Floor;
            Color roof = info.Rug;
            Color wall = Color.Lerp(info.Wall, Color.white, 0.25f);

            // Sky: a vertical gradient, which is most of what makes a painted picture read as
            // painted rather than as a diagram.
            for (int y = 0; y < Size; y++)
            {
                float t = (float)y / (Size - 1);
                Color row = Color.Lerp(ground, Color.Lerp(skyBottom, skyTop, t), t * 0.85f + 0.15f);
                for (int x = 0; x < Size; x++) pixels[y * Size + x] = row;
            }

            // Stars, or a couple of clouds.
            if (night)
            {
                for (int i = 0; i < 26; i++)
                {
                    int x = rng.Next(Size), y = Size / 2 + rng.Next(Size / 2);
                    byte alpha = (byte)(120 + rng.Next(135));
                    pixels[y * Size + x] = new Color32(255, 246, 216, alpha);
                }

                Circle(pixels, 74, 78, 9, new Color(1f, 0.93f, 0.74f));
                Circle(pixels, 71, 81, 8, new Color(0.16f, 0.18f, 0.34f));
            }
            else
            {
                Circle(pixels, 74, 78, 8, new Color(1f, 0.94f, 0.66f));
                Circle(pixels, 74, 78, 12, new Color(1f, 0.94f, 0.66f, 0.25f));
            }

            // Ground and a horizon band, so the composition has a floor.
            FillRect(pixels, 0, 0, Size, 22, ground);
            FillRect(pixels, 0, 22, Size, 2, Color.Lerp(ground, Color.black, 0.25f));

            // A little house on the left: wall, roof, door, window.
            FillRect(pixels, 10, 22, 26, 24, wall);
            FillRect(pixels, 10, 42, 26, 4, Color.Lerp(wall, Color.black, 0.18f));
            Triangle(pixels, 8, 46, 38, 46, 23, 62, roof);
            FillRect(pixels, 19, 22, 8, 13, Color.Lerp(roof, Color.black, 0.35f));
            FillRect(pixels, 28, 30, 5, 5, night ? new Color(1f, 0.9f, 0.6f) : new Color(0.55f, 0.78f, 0.9f));

            // The pet, sitting in front of the house.
            int bodyX = 54, bodyY = 30;
            Circle(pixels, bodyX, bodyY, 11, fur);
            Circle(pixels, bodyX, bodyY + 13, 8, fur);
            Triangle(pixels, bodyX - 8, bodyY + 17, bodyX - 2, bodyY + 17, bodyX - 6, bodyY + 26,
                Color.Lerp(fur, Color.black, 0.15f));
            Triangle(pixels, bodyX + 2, bodyY + 17, bodyX + 8, bodyY + 17, bodyX + 6, bodyY + 26,
                Color.Lerp(fur, Color.black, 0.15f));
            Circle(pixels, bodyX - 3, bodyY + 15, 1, Color.black);
            Circle(pixels, bodyX + 3, bodyY + 15, 1, Color.black);
            Circle(pixels, bodyX, bodyY + 11, 2, Color.Lerp(fur, Color.white, 0.6f));

            // A mat under the pet, so it is standing on the ground rather than floating.
            FillRect(pixels, bodyX - 13, 20, 26, 3, Color.Lerp(ground, Color.white, 0.35f));

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void FillRect(Color32[] pixels, int x, int y, int w, int h, Color color)
        {
            for (int iy = y; iy < y + h; iy++)
            {
                if (iy < 0 || iy >= Size) continue;
                for (int ix = x; ix < x + w; ix++)
                {
                    if (ix < 0 || ix >= Size) continue;
                    pixels[iy * Size + ix] = Blend(pixels[iy * Size + ix], color);
                }
            }
        }

        private static void Circle(Color32[] pixels, int cx, int cy, int radius, Color color)
        {
            for (int iy = cy - radius; iy <= cy + radius; iy++)
            {
                if (iy < 0 || iy >= Size) continue;
                for (int ix = cx - radius; ix <= cx + radius; ix++)
                {
                    if (ix < 0 || ix >= Size) continue;
                    int dx = ix - cx, dy = iy - cy;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    pixels[iy * Size + ix] = Blend(pixels[iy * Size + ix], color);
                }
            }
        }

        private static void Triangle(Color32[] pixels, int x0, int y0, int x1, int y1, int x2, int y2,
            Color color)
        {
            int minX = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
            int maxX = Mathf.Min(Size - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
            int minY = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
            int maxY = Mathf.Min(Size - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!Inside(x, y, x0, y0, x1, y1, x2, y2)) continue;
                    pixels[y * Size + x] = Blend(pixels[y * Size + x], color);
                }
            }
        }

        /// <summary>Barycentric sign test — the standard "is this pixel in the triangle".</summary>
        private static bool Inside(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            int d1 = (px - x1) * (y0 - y1) - (x0 - x1) * (py - y1);
            int d2 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
            int d3 = (px - x0) * (y2 - y0) - (x2 - x0) * (py - y0);

            bool anyNegative = d1 < 0 || d2 < 0 || d3 < 0;
            bool anyPositive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(anyNegative && anyPositive);
        }

        /// <summary>Alpha-blends one colour over a pixel.</summary>
        private static Color32 Blend(Color32 under, Color over)
        {
            if (over.a >= 1f) return over;

            float a = over.a;
            return new Color32(
                (byte)Mathf.Clamp(over.r * 255f * a + under.r * (1f - a), 0f, 255f),
                (byte)Mathf.Clamp(over.g * 255f * a + under.g * (1f - a), 0f, 255f),
                (byte)Mathf.Clamp(over.b * 255f * a + under.b * (1f - a), 0f, 255f),
                255);
        }

        /// <summary>
        /// The UV rectangle of one tile, for <c>GUI.DrawTextureWithTexCoords</c>.
        ///
        /// The picture is drawn top-down and the board is numbered from the top-left, so the v
        /// coordinate is flipped here rather than at every call site — the classic way to end up
        /// with a puzzle whose picture is upside down, which looks solvable and is not.
        /// </summary>
        public static Rect TileUv(int index)
        {
            int row = index / PetPuzzle.Size;
            int column = index % PetPuzzle.Size;
            float step = 1f / PetPuzzle.Size;

            // A hair of inset avoids sampling the neighbouring tile at the seam.
            const float inset = 0.0015f;
            return new Rect(column * step + inset, 1f - (row + 1) * step + inset,
                step - inset * 2f, step - inset * 2f);
        }
    }
}
