using System.Collections.Generic;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The few surfaces that are too big to be made of boxes: grass, a tiled deck, and the city
    /// beyond the terrace railing.
    ///
    /// Drawn rather than downloaded, like everything else the pet's world is made of, and small
    /// (128 px) on purpose: these are seen from several metres away through a phone screen, and a
    /// big texture here would cost memory to look the same. Each one is generated once per run
    /// and cached, with <see cref="HideFlags.HideAndDontSave"/> so a rebuild of the room does not
    /// leave orphaned textures behind in the editor.
    /// </summary>
    public static class RoomTextures
    {
        private static Texture2D _grass;
        private static Texture2D _deck;
        private static Texture2D _skyline;
        private static Texture2D _pickets;
        private static Texture2D _balusters;

        /// <summary>
        /// A picket fence with the gaps *between* the pickets, as alpha.
        ///
        /// The fence is one textured panel per side rather than forty little boxes: a fence that
        /// reads as a fence needs pickets every 12 cm, and at that density the boxes cost more
        /// draw calls than the whole rest of the room put together. Drawn as cutout art the same
        /// fence is four objects, and the camera still sees the garden through the gaps.
        /// </summary>
        public static Texture2D Pickets(int width = 128, int height = 64)
        {
            if (_pickets != null) return _pickets;

            var texture = New(width, height);
            for (int x = 0; x < width; x++) { for (int y = 0; y < height; y++) texture.SetPixel(x, y, Clear); }

            const int step = 22;     // pitch of one picket
            const int picket = 12;   // how wide it is
            const int point = 12;    // height of the pointed top

            // Centred in its step, so two tiles side by side do not grow a double-wide picket at
            // the seam — a fence that only looks right once is a fence nobody looked at twice.
            int left = (step - picket) / 2;

            for (int x = 0; x < width; x++)
            {
                int into = ((x - left) % step + step) % step;
                if (into >= picket) continue;

                for (int y = 0; y < height; y++)
                {
                    // The top of each picket is a triangle: it narrows to a point over `point` pixels.
                    int fromTop = height - 1 - y;
                    if (fromTop < point)
                    {
                        float half = picket * 0.5f * (fromTop / (float)point);
                        float centre = picket * 0.5f;
                        if (Mathf.Abs(into + 0.5f - centre) > half) continue;
                    }

                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply();
            return _pickets = texture;
        }

        /// <summary>Turned balusters: thin bars with a bead in the middle, as cutout art.</summary>
        public static Texture2D Balusters(int width = 128, int height = 64)
        {
            if (_balusters != null) return _balusters;

            var texture = New(width, height);
            for (int x = 0; x < width; x++) { for (int y = 0; y < height; y++) texture.SetPixel(x, y, Clear); }

            const int step = 20;
            const int bar = 6;

            int left = (step - bar) / 2;

            for (int x = 0; x < width; x++)
            {
                int into = ((x - left) % step + step) % step;
                float centre = bar * 0.5f;
                float distance = Mathf.Abs(into + 0.5f - centre);

                for (int y = 0; y < height; y++)
                {
                    float t = y / (float)(height - 1);
                    // A bead around the middle, widening to 10 px, and narrower at the ends.
                    float swell = 1f + 0.9f * Mathf.Sin(Mathf.PI * t);
                    if (distance > centre * swell) continue;
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply();
            return _balusters = texture;
        }

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
        private static readonly Dictionary<string, Texture2D> _flowers = new Dictionary<string, Texture2D>();

        /// <summary>
        /// One flower, drawn whole: stem, two leaves, and a head of petals in the given colour.
        ///
        /// A texture per colour rather than one white texture tinted eight ways, because the tint
        /// would land on the stem too and turn a red flower's stalk red. Eight 64-pixel textures
        /// cost less than one of the room's walls.
        /// </summary>
        public static Texture2D Flower(Color petals, int petalCount = 6)
        {
            petalCount = Mathf.Clamp(petalCount, 5, 8);
            string key = ColorUtility.ToHtmlStringRGB(petals) + "_" + petalCount;
            Texture2D cached;
            if (_flowers.TryGetValue(key, out cached) && cached != null) return cached;

            const int size = 64;
            var texture = New(size, size);
            for (int x = 0; x < size; x++) { for (int y = 0; y < size; y++) texture.SetPixel(x, y, Clear); }

            var stem = new Color(0.28f, 0.48f, 0.22f);
            var centre = new Color(0.98f, 0.84f, 0.36f);

            float cx = size * 0.5f;
            float cy = size * 0.66f;

            // Stem, then two leaves either side of it.
            for (int y = 2; y < cy; y++)
            {
                for (int x = Mathf.RoundToInt(cx) - 1; x <= Mathf.RoundToInt(cx) + 1; x++) Plot(texture, x, y, stem);
            }
            Leaf(texture, cx - 7f, cy - 14f, -1f, stem);
            Leaf(texture, cx + 7f, cy - 9f, 1f, stem);

            // Petals, then the middle over the top of them.
            for (int i = 0; i < petalCount; i++)
            {
                float angle = i / (float)petalCount * Mathf.PI * 2f;
                float px = cx + Mathf.Cos(angle) * 8.5f;
                float py = cy + Mathf.Sin(angle) * 8.5f;
                Disc(texture, px, py, 6.2f, petals);
            }
            Disc(texture, cx, cy, 4.2f, centre);

            texture.Apply();
            _flowers[key] = texture;
            return texture;
        }

        /// <summary>A tuft of grass blades, for the lawn's edges.</summary>
        public static Texture2D GrassTuft(int seed = 4242)
        {
            string key = "tuft" + seed;
            Texture2D cached;
            if (_flowers.TryGetValue(key, out cached) && cached != null) return cached;

            const int size = 64;
            var texture = New(size, size);
            for (int x = 0; x < size; x++) { for (int y = 0; y < size; y++) texture.SetPixel(x, y, Clear); }

            var rng = new System.Random(seed);
            var dark = new Color(0.22f, 0.44f, 0.18f);
            var light = new Color(0.46f, 0.70f, 0.30f);

            for (int blade = 0; blade < 9; blade++)
            {
                float baseX = 10f + (float)rng.NextDouble() * 44f;
                float height = 22f + (float)rng.NextDouble() * 34f;
                float lean = ((float)rng.NextDouble() * 2f - 1f) * 12f;
                var colour = Color.Lerp(dark, light, (float)rng.NextDouble());

                for (int y = 0; y < height; y++)
                {
                    float t = y / height;
                    int x = Mathf.RoundToInt(baseX + lean * t * t);
                    Plot(texture, x, y + 2, colour);
                    if (y > 6) Plot(texture, x + 1, y + 2, colour);
                }
            }

            texture.Apply();
            _flowers[key] = texture;
            return texture;
        }

        private static void Leaf(Texture2D texture, float x, float y, float direction, Color colour)
        {
            for (int i = 0; i < 8; i++)
            {
                int px = Mathf.RoundToInt(x + direction * i * 0.8f);
                int py = Mathf.RoundToInt(y + i * 0.5f);
                Plot(texture, px, py, colour);
                Plot(texture, px, py + 1, colour);
            }
        }

        private static void Disc(Texture2D texture, float cx, float cy, float radius, Color colour)
        {
            int min = Mathf.FloorToInt(cx - radius), max = Mathf.CeilToInt(cx + radius);
            for (int x = min; x <= max; x++)
            {
                for (int y = min; y <= max; y++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius) Plot(texture, x, y, colour);
                }
            }
        }

        private static void Plot(Texture2D texture, int x, int y, Color colour)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
            texture.SetPixel(x, y, colour);
        }

        /// <summary>Lawn: mottled green with a scatter of blades so it is not flat paint.</summary>
        public static Texture2D Grass(int size = 128)
        {
            if (_grass != null) return _grass;

            var rng = new System.Random(20240);
            var texture = New(size, size);

            var dark = new Color(0.24f, 0.42f, 0.20f);
            var light = new Color(0.44f, 0.64f, 0.32f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = (float)rng.NextDouble();
                    texture.SetPixel(x, y, Color.Lerp(dark, light, noise * 0.55f));
                }
            }

            // Blades: two-pixel strokes, biased upwards so the lawn has a direction.
            int blades = size * 3;
            for (int i = 0; i < blades; i++)
            {
                int x = rng.Next(size);
                int y = rng.Next(size);
                var blade = Color.Lerp(light, Color.white, (float)rng.NextDouble() * 0.35f);
                texture.SetPixel(x, y, blade);
                texture.SetPixel(x, Mathf.Min(size - 1, y + 1), Color.Lerp(light, blade, 0.6f));
            }

            texture.Apply();
            return _grass = texture;
        }

        /// <summary>Stone slabs with grout lines, for the terrace deck.</summary>
        public static Texture2D Deck(int size = 128)
        {
            if (_deck != null) return _deck;

            var rng = new System.Random(90210);
            var texture = New(size, size);

            const int tiles = 4;
            int tile = size / tiles;
            var grout = new Color(0.16f, 0.17f, 0.21f);

            for (int ty = 0; ty < tiles; ty++)
            {
                for (int tx = 0; tx < tiles; tx++)
                {
                    // One tint per slab, so the deck reads as laid stone rather than as linoleum.
                    var tone = Color.Lerp(new Color(0.40f, 0.41f, 0.47f), new Color(0.52f, 0.52f, 0.58f),
                        (float)rng.NextDouble());

                    for (int y = 0; y < tile; y++)
                    {
                        for (int x = 0; x < tile; x++)
                        {
                            bool edge = x == 0 || y == 0;
                            var colour = edge ? grout : tone;
                            if (!edge) colour = Color.Lerp(colour, Color.white, (float)rng.NextDouble() * 0.06f);
                            texture.SetPixel(tx * tile + x, ty * tile + y, colour);
                        }
                    }
                }
            }

            texture.Apply();
            return _deck = texture;
        }

        /// <summary>
        /// The city the terrace looks out on: a night sky, a skyline of towers, and lit windows.
        ///
        /// The point of it is depth. A railing with a dark void behind it reads as the edge of the
        /// world; the same railing with a skyline behind it reads as a rooftop, which is the whole
        /// difference between "the room again, in blue" and a place worth unlocking.
        /// </summary>
        public static Texture2D Skyline(int width = 256, int height = 128)
        {
            if (_skyline != null) return _skyline;

            var rng = new System.Random(77123);
            var texture = New(width, height);

            var top = new Color(0.05f, 0.06f, 0.13f);
            var horizon = new Color(0.22f, 0.20f, 0.34f);

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                var sky = Color.Lerp(top, horizon, Mathf.Pow(1f - t, 1.6f));
                for (int x = 0; x < width; x++)
                {
                    // A few stars in the upper half: cheap, and they sell the night.
                    var colour = sky;
                    if (t > 0.55f && rng.NextDouble() < 0.0016) colour = new Color(0.95f, 0.95f, 0.85f);
                    texture.SetPixel(x, y, colour);
                }
            }

            // Towers along the bottom third, tallest in the middle.
            int cursor = 0;
            while (cursor < width)
            {
                int towerWidth = 10 + rng.Next(16);
                int towerHeight = 22 + rng.Next(46);
                float middle = 1f - Mathf.Abs(cursor + towerWidth * 0.5f - width * 0.5f) / (width * 0.5f);
                towerHeight = Mathf.RoundToInt(towerHeight * Mathf.Lerp(0.6f, 1.25f, middle));

                var body = Color.Lerp(new Color(0.10f, 0.10f, 0.16f), new Color(0.16f, 0.16f, 0.24f),
                    (float)rng.NextDouble());

                for (int x = cursor; x < Mathf.Min(width, cursor + towerWidth); x++)
                {
                    for (int y = 0; y < Mathf.Min(height, towerHeight); y++)
                    {
                        texture.SetPixel(x, y, body);
                    }
                }

                // Lit windows: a sparse grid, warm, and uneven so the tower looks inhabited.
                for (int wy = 3; wy < towerHeight - 2; wy += 4)
                {
                    for (int wx = cursor + 2; wx < Mathf.Min(width, cursor + towerWidth - 1); wx += 3)
                    {
                        if (rng.NextDouble() > 0.42) continue;
                        var lit = Color.Lerp(new Color(1f, 0.86f, 0.55f), new Color(0.75f, 0.85f, 1f),
                            (float)rng.NextDouble());
                        texture.SetPixel(wx, wy, lit);
                    }
                }

                cursor += towerWidth + 1 + rng.Next(4);
            }

            texture.Apply();
            return _skyline = texture;
        }

        /// <summary>Forgets the cached textures. Used between tests; the game never calls it.</summary>
        public static void ClearCache()
        {
            _grass = null;
            _deck = null;
            _skyline = null;
            _pickets = null;
            _balusters = null;
            _flowers.Clear();
        }

        private static Texture2D New(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }
    }
}
