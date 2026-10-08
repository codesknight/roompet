using System.Collections.Generic;
using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Rounded panels and soft shadows, drawn from textures generated at runtime.
    ///
    /// Lives in the shared mobile layer rather than inside either game: both HUDs are
    /// hand-written IMGUI, both wanted the same rounded chrome, and the two gameplay assemblies
    /// deliberately do not reference each other.
    ///
    /// Stock IMGUI boxes are flat rectangles with a 1px border — which is what made a working
    /// interface still look like a programmer's debug overlay. Every "proper game UI" look comes
    /// from things IMGUI does not have: rounded corners, a border that follows them, and a
    /// shadow that lifts a panel off the scene.
    ///
    /// Both are generated here rather than imported, in keeping with the rest of the project,
    /// and drawn with a nine-slice so the corner radius stays a radius instead of stretching
    /// into an ellipse on a wide panel.
    /// </summary>
    public static class UiSkin
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// A rounded box: fill, optional border, optional shadow underneath.
        ///
        /// <paramref name="rect"/> is in the current GUI space, so callers inside the HUD's
        /// scaled block pass design pixels and get a correctly scaled panel for free.
        /// </summary>
        public static void Panel(Rect rect, float radius, Color fill, Color border, float borderWidth = 2f,
            bool shadow = false)
        {
            // A panel narrower than its own corners would render as mush; shrinking the radius
            // is what keeps small buttons looking deliberate.
            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (radius < 1f)
            {
                Fill(rect, fill);
                if (borderWidth > 0f && border.a > 0f) Outline(rect, border, borderWidth);
                return;
            }

            if (shadow) Shadow(rect, radius);

            int r = Mathf.RoundToInt(radius);
            var texture = BoxTexture(r, fill, border, Mathf.RoundToInt(Mathf.Max(0f, borderWidth)));
            NineSlice(rect, texture, r, r + 2);
        }

        /// <summary>A soft drop shadow, drawn as a few offset translucent rounded boxes.</summary>
        public static void Shadow(Rect rect, float radius, float spread = 3f, float alpha = 0.35f)
        {
            int r = Mathf.RoundToInt(Mathf.Max(2f, radius));
            for (int i = Mathf.RoundToInt(spread); i >= 1; i--)
            {
                var layer = new Rect(rect.x - i * 0.5f, rect.y + i * 0.8f,
                    rect.width + i, rect.height + i);
                var texture = BoxTexture(r, new Color(0f, 0f, 0f, alpha / (i + 1f)),
                    new Color(0f, 0f, 0f, 0f), 0);
                NineSlice(layer, texture, r, r + 2);
            }
        }

        /// <summary>A flat filled rect, used where corners would only add noise.</summary>
        public static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>A one-pixel-per-side outline, for the cheap cases (bars, dividers).</summary>
        public static void Outline(Rect rect, Color color, float width = 1f)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        // ------------------------------------------------------------------ textures

        private static Texture2D BoxTexture(int radius, Color fill, Color border, int borderWidth)
        {
            string key = $"{radius}|{ColorKey(fill)}|{ColorKey(border)}|{borderWidth}";
            Texture2D cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            int size = radius * 2 + 4;              // the 4px centre is the stretchable slice
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            // Supersampled so the corners are smooth: IMGUI has no anti-aliasing of its own.
            const int ss = 3;
            int n = size * ss;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int outer = 0;
                    int inner = 0;

                    for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px = (x * ss + sx + 0.5f) / n * size;
                        float py = (y * ss + sy + 0.5f) / n * size;

                        if (InsideRounded(px, py, size, radius)) outer++;
                        if (borderWidth > 0 && InsideRounded(px, py, size, radius - borderWidth)) inner++;
                    }

                    float alpha = (float)outer / (ss * ss);
                    if (alpha <= 0f)
                    {
                        pixels[(size - 1 - y) * size + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    // Between the two shapes is the border; inside the inner one is the fill.
                    Color color = fill;
                    if (borderWidth > 0 && border.a > 0f)
                    {
                        float fillShare = outer > 0 ? (float)inner / outer : 0f;
                        color = Color.Lerp(border, fill, fillShare);
                    }

                    color.a *= alpha;
                    // Textures count y upwards.
                    pixels[(size - 1 - y) * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Cache[key] = texture;
            return texture;
        }

        /// <summary>
        /// Draws a texture as a nine-slice: corners at their natural size, edges stretched
        /// along one axis, centre stretched both ways.
        /// </summary>
        private static void NineSlice(Rect rect, Texture2D texture, int slice, int centre)
        {
            float size = texture.width;
            float s = slice;
            float c = centre;

            float[] xs = { 0f, s, size - s };
            float[] ys = { 0f, s, size - s };
            float[] ws = { s, size - 2f * s, s };
            float[] hs = { s, size - 2f * s, s };

            // Destination slices: corners keep their size, middles take what is left.
            float dl = Mathf.Min(s, rect.width * 0.5f);
            float dr = Mathf.Min(s, rect.width * 0.5f);
            float dt = Mathf.Min(s, rect.height * 0.5f);
            float db = Mathf.Min(s, rect.height * 0.5f);

            float[] dx = { rect.x, rect.x + dl, rect.xMax - dr };
            float[] dy = { rect.y, rect.y + dt, rect.yMax - db };
            float[] dw = { dl, Mathf.Max(0f, rect.width - dl - dr), dr };
            float[] dh = { dt, Mathf.Max(0f, rect.height - dt - db), db };

            var previous = GUI.color;
            GUI.color = Color.white;

            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                if (dw[col] <= 0f || dh[row] <= 0f) continue;

                var dest = new Rect(dx[col], dy[row], dw[col], dh[row]);
                var source = new Rect(xs[col] / size, 1f - (ys[row] + hs[row]) / size,
                    ws[col] / size, hs[row] / size);

                GUI.DrawTextureWithTexCoords(dest, texture, source);
            }

            GUI.color = previous;
        }

        private static string ColorKey(Color color)
            => $"{(int)(color.r * 255)},{(int)(color.g * 255)},{(int)(color.b * 255)},{(int)(color.a * 255)}";

        /// <summary>Whether a point is inside a rounded box spanning 0..size with radius r.</summary>
        private static bool InsideRounded(float px, float py, float size, float radius)
        {
            if (radius <= 0f) return px >= 0f && px <= size && py >= 0f && py <= size;

            float cx = Mathf.Clamp(px, radius, size - radius);
            float cy = Mathf.Clamp(py, radius, size - radius);
            float dx = px - cx, dy = py - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>Drops the generated textures, e.g. when the HUD is torn down.</summary>
        public static void ClearCache()
        {
            foreach (var pair in Cache)
            {
                if (pair.Value != null) Object.Destroy(pair.Value);
            }
            Cache.Clear();
        }
    }
}
