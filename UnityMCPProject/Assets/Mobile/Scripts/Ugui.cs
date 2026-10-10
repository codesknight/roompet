using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DshMobile
{
    /// <summary>
    /// Runtime uGUI bootstrap and widget factory, the uGUI counterpart of the IMGUI
    /// <see cref="MobileWidgets"/> + <see cref="UiSkin"/> pair.
    ///
    /// Everything is created from code, so no scene wiring and no prefabs are required — the
    /// same "zero asset" rule the rest of the project follows. A screen-space canvas is
    /// created once and survives scene loads, an EventSystem is only spun up when the first
    /// interactive control needs one, and the rounded panels are rasterised into nine-slice
    /// sprites so the corner radius stays a radius instead of stretching.
    ///
    /// Coordinate note: <see cref="SetRect"/> lays a RectTransform out in a top-left origin,
    /// exactly like IMGUI's design pixels, so a HUD migrating from IMGUI can keep its layout
    /// numbers. The CanvasScaler maps those design pixels onto the real screen.
    /// </summary>
    public static class Ugui
    {
        private static Canvas _canvas;
        private static EventSystem _eventSystem;

        // --------------------------------------------------------------- bootstrap

        /// <summary>
        /// Returns the shared screen-space canvas, creating (and keeping alive) it if needed.
        /// </summary>
        public static Canvas EnsureCanvas(string name = "~UguiCanvas")
        {
            if (_canvas != null) return _canvas;

            Canvas existing = Object.FindObjectOfType<Canvas>();
            if (existing != null)
            {
                _canvas = existing;
                return _canvas;
            }

            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();
            return _canvas;
        }

        /// <summary>
        /// Returns the shared EventSystem, creating it (with a StandaloneInputModule) when the
        /// first uGUI Button needs input. Text and images do not call this, so a static HUD
        /// adds no input machinery.
        /// </summary>
        public static EventSystem EnsureEventSystem()
        {
            if (_eventSystem != null) return _eventSystem;

            EventSystem existing = Object.FindObjectOfType<EventSystem>();
            if (existing != null)
            {
                _eventSystem = existing;
                return _eventSystem;
            }

            var go = new GameObject("~EventSystem");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _eventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return _eventSystem;
        }

        // ---------------------------------------------------------------- children

        /// <summary>Creates a bare RectTransform child, for callers that want full control.</summary>
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Creates a Text child with the shared CJK font, non-interactive by default.</summary>
        public static Text Text(string name, Transform parent, string content, int fontSize,
            Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false)
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = UguiFont.Resolve(fontSize);
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.text = content;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Creates a solid-colour Image child, non-interactive by default.</summary>
        public static Image Image(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Creates a rounded panel Image, rasterised into a nine-slice sprite so it scales
        /// cleanly. Returns the Image so callers can tint or replace it.
        /// </summary>
        public static Image Panel(string name, Transform parent, float radius, Color fill, Color border,
            float borderWidth = 2f)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = UguiRounded.Sprite(Mathf.RoundToInt(radius), fill, border, Mathf.RoundToInt(borderWidth));
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Creates a tappable button: a rounded, tinted background with a centred label. The
        /// returned Button fires through the shared EventSystem. Highlight-on-press comes from
        /// the Button's ColorTint, so no custom input layer is involved.
        /// </summary>
        public static Button Button(string name, Transform parent, string label, int fontSize, Color tint)
        {
            EnsureEventSystem();

            var rt = Rect(name, parent);
            var bg = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = UguiRounded.Sprite(14, new Color(tint.r, tint.g, tint.b, 0.55f),
                new Color(1f, 1f, 1f, 0.55f), 2);
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = Color.white;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var labelText = Text("Label", rt, label, fontSize, Color.white);
            labelText.raycastTarget = false;
            Stretch(labelText.rectTransform);
            return btn;
        }

        // --------------------------------------------------------------- layout

        /// <summary>Stretches a RectTransform to fill its parent.</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Lays a RectTransform out in design pixels with a top-left origin, matching IMGUI.
        /// (x, y) is the top-left corner; width/height are the size.
        /// </summary>
        public static void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // -------------------------------------------------------------- teardown

        /// <summary>Destroys the shared canvas and event system, e.g. when tests want a clean slate.</summary>
        public static void Reset()
        {
            if (_eventSystem != null) { Object.Destroy(_eventSystem.gameObject); _eventSystem = null; }
            if (_canvas != null) { Object.Destroy(_canvas.gameObject); _canvas = null; }
            UguiRounded.ClearCache();
            UguiFont.ClearCache();
        }
    }

    /// <summary>
    /// Rounded-panel sprites, rasterised at runtime into nine-slice textures so the corner
    /// radius stays a radius instead of stretching into an ellipse on a wide panel. This is
    /// the uGUI version of <see cref="UiSkin"/>.
    /// </summary>
    public static class UguiRounded
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Gets (or generates and caches) a nine-slice rounded-box sprite.</summary>
        public static Sprite Sprite(int radius, Color fill, Color border, int borderWidth = 2)
        {
            if (radius < 1) radius = 1;
            if (borderWidth < 0) borderWidth = 0;

            string key = radius + "|" + ColorKey(fill) + "|" + ColorKey(border) + "|" + borderWidth;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            // A 2px centre keeps the stretchable slice legal even for small radii.
            int size = radius * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            const int ss = 4; // supersampling, because uGUI has no anti-aliasing of its own
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    int inner = 0;

                    for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px = x + (sx + 0.5f) / ss;
                        float py = y + (sy + 0.5f) / ss;

                        if (InsideRounded(px, py, size, radius)) inside++;
                        if (borderWidth > 0 && border.a > 0f && InsideRounded(px, py, size, radius - borderWidth)) inner++;
                    }

                    float alpha = inside / (float)(ss * ss);
                    if (alpha <= 0f)
                    {
                        pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }

                    Color color = fill;
                    if (borderWidth > 0 && border.a > 0f)
                    {
                        float share = inside > 0 ? inner / (float)inside : 0f;
                        color = Color.Lerp(border, fill, share);
                    }
                    color.a *= alpha;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "UguiRounded_" + radius;
            Cache[key] = sprite;
            return sprite;
        }

        private static string ColorKey(Color c)
            => ((int)(c.r * 255)) + "," + ((int)(c.g * 255)) + "," + ((int)(c.b * 255)) + "," + ((int)(c.a * 255));

        private static bool InsideRounded(float px, float py, float size, float radius)
        {
            if (radius <= 0f) return px >= 0f && px <= size && py >= 0f && py <= size;
            float cx = Mathf.Clamp(px, radius, size - radius);
            float cy = Mathf.Clamp(py, radius, size - radius);
            float dx = px - cx, dy = py - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>Drops the generated sprites.</summary>
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
