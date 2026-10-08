using UnityEngine;

namespace DshMobileEditor
{
    /// <summary>
    /// A tiny software rasteriser, just enough to draw a flat app icon in code.
    ///
    /// The icon follows the same rule as the rest of this project: it is generated rather than
    /// shipped. No binary art in the repository, no external drawing tool in the loop, and the
    /// palette can be kept in step with the game's own colours by editing a few constants.
    ///
    /// Everything is drawn at 4x and box-filtered down, which is all the anti-aliasing a flat
    /// icon needs, and every coordinate is normalised (0..1, y downwards) so one drawing routine
    /// produces the 432px adaptive layers and the small density fallbacks without any scaling
    /// arithmetic at the call site.
    /// </summary>
    public class IconPainter
    {
        private readonly int _size;
        private readonly int _supersample;
        private readonly int _n;

        // Straight (non-premultiplied) colour, alpha 0..1.
        private readonly float[] _r;
        private readonly float[] _g;
        private readonly float[] _b;
        private readonly float[] _a;

        public IconPainter(int size, int supersample = 4)
        {
            _size = Mathf.Max(1, size);
            _supersample = Mathf.Clamp(supersample, 1, 8);
            _n = _size * _supersample;

            int count = _n * _n;
            _r = new float[count];
            _g = new float[count];
            _b = new float[count];
            _a = new float[count];
        }

        /// <summary>Hex colour, e.g. <c>0xE4735C</c>. Alpha is a separate argument.</summary>
        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
        }

        // ------------------------------------------------------------------ painting

        /// <summary>Three-stop vertical wash: sky above, warmth below.</summary>
        public void VerticalGradient(Color top, Color middle, float middleAt, Color bottom)
        {
            for (int y = 0; y < _n; y++)
            {
                float t = (y + 0.5f) / _n;
                Color c = t <= middleAt
                    ? Color.Lerp(top, middle, middleAt <= 0f ? 0f : t / middleAt)
                    : Color.Lerp(middle, bottom, (t - middleAt) / Mathf.Max(0.0001f, 1f - middleAt));

                for (int x = 0; x < _n; x++)
                {
                    int i = y * _n + x;
                    _r[i] = c.r;
                    _g[i] = c.g;
                    _b[i] = c.b;
                    _a[i] = 1f;
                }
            }
        }

        public void Rect(float x0, float y0, float x1, float y1, Color color)
        {
            Fill((x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1, color, x0, y0, x1, y1);
        }

        public void Circle(float cx, float cy, float radius, Color color)
        {
            float r2 = radius * radius;
            Fill((x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                return dx * dx + dy * dy <= r2;
            }, color, cx - radius, cy - radius, cx + radius, cy + radius);
        }

        /// <summary>An ellipse; the cat's body is one of these.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color color)
        {
            Fill((x, y) =>
            {
                float dx = (x - cx) / Mathf.Max(0.0001f, rx);
                float dy = (y - cy) / Mathf.Max(0.0001f, ry);
                return dx * dx + dy * dy <= 1f;
            }, color, cx - rx, cy - ry, cx + rx, cy + ry);
        }

        public void RoundedRect(float x0, float y0, float x1, float y1, float radius, Color color)
        {
            float r = Mathf.Max(0f, radius);
            Fill((x, y) =>
            {
                if (x < x0 || x > x1 || y < y0 || y > y1) return false;
                float cx = Mathf.Clamp(x, x0 + r, x1 - r);
                float cy = Mathf.Clamp(y, y0 + r, y1 - r);
                float dx = x - cx, dy = y - cy;
                return dx * dx + dy * dy <= r * r;
            }, color, x0, y0, x1, y1);
        }

        /// <summary>A doorway: straight sides with a semicircular top.</summary>
        public void Arch(float cx, float halfWidth, float top, float bottom, Color color)
        {
            float radius = halfWidth;
            float shoulder = top + radius;    // where the semicircle meets the sides
            float r2 = radius * radius;

            Fill((x, y) =>
            {
                if (y < top || y > bottom) return false;

                float dx = x - cx;
                if (y >= shoulder) return Mathf.Abs(dx) <= halfWidth;

                float dy = y - shoulder;
                return dx * dx + dy * dy <= r2;
            }, color, cx - halfWidth, top, cx + halfWidth, bottom);
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            float x0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            float x1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float y0 = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float y1 = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

            Fill((x, y) => InTriangle(x, y, a, b, c), color, x0, y0, x1, y1);
        }

        /// <summary>
        /// A radial glow: full strength at the centre fading to nothing at the edge. Used for
        /// the lamp light around the doorway and the halo behind the moon.
        /// </summary>
        public void SoftCircle(float cx, float cy, float radius, Color color, float falloff = 1.4f)
        {
            Fill((x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > radius) return false;

                float t = 1f - distance / Mathf.Max(0.0001f, radius);
                CurrentCoverage = Mathf.Pow(t, falloff);
                return true;
            }, color, cx - radius, cy - radius, cx + radius, cy + radius);
        }

        /// <summary>Keeps only what is inside a circle, for the round launcher icon.</summary>
        public void ClipCircle(float cx, float cy, float radius)
        {
            float r2 = radius * radius;
            for (int y = 0; y < _n; y++)
            for (int x = 0; x < _n; x++)
            {
                float px = (x + 0.5f) / _n, py = (y + 0.5f) / _n;
                float dx = px - cx, dy = py - cy;
                if (dx * dx + dy * dy > r2) _a[y * _n + x] = 0f;
            }
        }

        /// <summary>Keeps only what is inside a rounded square, for the legacy icon.</summary>
        public void ClipRoundedRect(float x0, float y0, float x1, float y1, float radius)
        {
            float r = Mathf.Max(0f, radius);
            for (int y = 0; y < _n; y++)
            for (int x = 0; x < _n; x++)
            {
                float px = (x + 0.5f) / _n, py = (y + 0.5f) / _n;
                bool inside = px >= x0 && px <= x1 && py >= y0 && py <= y1;
                if (inside && r > 0f)
                {
                    float cx = Mathf.Clamp(px, x0 + r, x1 - r);
                    float cy = Mathf.Clamp(py, y0 + r, y1 - r);
                    float dx = px - cx, dy = py - cy;
                    inside = dx * dx + dy * dy <= r * r;
                }
                if (!inside) _a[y * _n + x] = 0f;
            }
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>Coverage written by a fill test that wants a soft edge (see SoftCircle).</summary>
        private float CurrentCoverage = 1f;

        private void Fill(System.Func<float, float, bool> inside, Color color,
            float bx0, float by0, float bx1, float by1)
        {
            if (color.a <= 0f) return;

            // Clamped to the shape's own bounds: without this every shape would test all 3M
            // supersampled pixels, which turns a two-second render into a twenty-second one.
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(bx0, bx1) * _n) - 1);
            int x1 = Mathf.Min(_n - 1, Mathf.CeilToInt(Mathf.Max(bx0, bx1) * _n) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(by0, by1) * _n) - 1);
            int y1 = Mathf.Min(_n - 1, Mathf.CeilToInt(Mathf.Max(by0, by1) * _n) + 1);

            for (int y = y0; y <= y1; y++)
            {
                float py = (y + 0.5f) / _n;
                int row = y * _n;

                for (int x = x0; x <= x1; x++)
                {
                    float px = (x + 0.5f) / _n;

                    CurrentCoverage = 1f;
                    if (!inside(px, py)) continue;

                    Blend(row + x, color, CurrentCoverage);
                }
            }
        }

        /// <summary>Source-over, straight alpha.</summary>
        private void Blend(int index, Color color, float coverage)
        {
            float sa = Mathf.Clamp01(color.a * coverage);
            if (sa <= 0f) return;

            float da = _a[index];
            float outA = sa + da * (1f - sa);
            if (outA <= 0.0001f)
            {
                _a[index] = 0f;
                return;
            }

            float keep = da * (1f - sa);
            _r[index] = (_r[index] * keep + color.r * sa) / outA;
            _g[index] = (_g[index] * keep + color.g * sa) / outA;
            _b[index] = (_b[index] * keep + color.b * sa) / outA;
            _a[index] = outA;
        }

        private static bool InTriangle(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(px, py, a, b);
            float d2 = Sign(px, py, b, c);
            float d3 = Sign(px, py, c, a);

            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(float px, float py, Vector2 a, Vector2 b)
            => (px - b.x) * (a.y - b.y) - (a.x - b.x) * (py - b.y);

        /// <summary>
        /// Downsamples to the output texture.
        ///
        /// The box filter averages PREMULTIPLIED values and divides the alpha out again: averaging
        /// straight colour at an edge drags the transparent side's black into the result and every
        /// shape gets a dark fringe.
        /// </summary>
        public Texture2D ToTexture()
        {
            var texture = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[_size * _size];
            int block = _supersample * _supersample;

            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int sy = 0; sy < _supersample; sy++)
                    {
                        int row = (y * _supersample + sy) * _n + x * _supersample;
                        for (int sx = 0; sx < _supersample; sx++)
                        {
                            int i = row + sx;
                            float pa = _a[i];
                            r += _r[i] * pa;
                            g += _g[i] * pa;
                            b += _b[i] * pa;
                            a += pa;
                        }
                    }

                    a /= block;
                    if (a > 0.0001f)
                    {
                        r /= block * a;
                        g /= block * a;
                        b /= block * a;
                    }
                    else
                    {
                        r = g = b = 0f;
                    }

                    // Textures count y upwards; the drawing code counts it downwards.
                    pixels[(_size - 1 - y) * _size + x] = new Color(r, g, b, a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
