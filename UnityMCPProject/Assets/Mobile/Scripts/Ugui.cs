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
        /// Changes the shared canvas's reference resolution. The PetHud lays itself out in a
        /// dynamic "design pixel" space (screen size / <see cref="DshMobile.MobileUi.UiScale"/>),
        /// so it must set the canvas to that space or every design-pixel coordinate is drawn in
        /// the wrong units. Other HUDs (mini games, the start menu, the runner) are authored
        /// against 1280x720 and rely on the default; they reset it back on entry.
        /// </summary>
        public static void SetReferenceResolution(float width, float height)
        {
            if (width < 1f || height < 1f) return;
            var scaler = EnsureCanvas().GetComponent<CanvasScaler>();
            if (scaler == null) return;
            var target = new Vector2(width, height);
            if (scaler.referenceResolution == target) return;
            scaler.referenceResolution = target;
        }
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
        /// Creates a RawImage, for runtime <see cref="Texture2D"/> that has no Sprite (e.g. the
        /// pet's generated avatar art). RawImage renders the texture directly.
        /// </summary>
        public static RawImage RawImage(string name, Transform parent, Texture2D texture, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = texture;
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

        /// <summary>
        /// Creates a horizontal Slider (a filled track + a round handle), wired through the shared
        /// EventSystem. Used for the tunnel's sensitivity control and anything else that needs a
        /// continuous 0..1-style value rather than discrete buttons.
        /// </summary>
        public static Slider Slider(string name, Transform parent, float min, float max, float value,
            Color fillColor, Color handleColor)
        {
            EnsureEventSystem();

            var rt = Rect(name, parent);
            var slider = rt.gameObject.AddComponent<Slider>();

            var bg = Image("Background", rt, new Color(0f, 0f, 0f, 0.4f));
            Stretch(bg.rectTransform);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(rt, false);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = Image("Fill", fillArea.transform, fillColor);
            Stretch(fill.rectTransform);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(rt, false);
            Stretch(handleArea.GetComponent<RectTransform>());
            var handle = Image("Handle", handleArea.transform, handleColor);
            handle.rectTransform.sizeDelta = new Vector2(26f, 26f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return slider;
        }

        /// <summary>
        /// Creates a text InputField (a track, a text component and a placeholder), wired through
        /// the shared EventSystem. Used by the pet-room chat box.
        /// </summary>
        public static InputField InputField(string name, Transform parent)
        {
            EnsureEventSystem();

            var rt = Rect(name, parent);
            var bg = Image(name + "Bg", rt, new Color(1f, 1f, 1f, 0.10f));
            Stretch(bg.rectTransform);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            var text = textGo.AddComponent<Text>();
            text.font = UguiFont.Resolve(17);
            text.fontSize = 17;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10f, 0f);
            text.rectTransform.offsetMax = new Vector2(-10f, 0f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(rt, false);
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = UguiFont.Resolve(17);
            placeholder.fontSize = 17;
            placeholder.color = new Color(0.6f, 0.6f, 0.65f, 0.6f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "说点什么…";
            Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(10f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-10f, 0f);

            var input = rt.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 400;
            return input;
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

        /// <summary>
        /// The general layout form: explicit anchor point, pivot, anchored position and size.
        /// <see cref="SetRect"/>, <see cref="Center"/> and <see cref="Stretch"/> are all
        /// specialisations of this.
        /// </summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }

        /// <summary>Centres a fixed-size rect on the screen centre.</summary>
        public static void Center(RectTransform rt, float w, float h)
        {
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(w, h));
        }

        /// <summary>
        /// Right-aligned, top-origin layout: the element's RIGHT edge sits <paramref name="rightInset"/>
        /// pixels from the parent's right edge, and its top edge <paramref name="y"/> from the top.
        /// Mirrors IMGUI's <c>rect.xMax - (N + w)</c> convention so right-aligned elements migrate 1:1.
        /// </summary>
        public static void SetRectRight(RectTransform rt, float rightInset, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-rightInset, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// Left-aligned, bottom-origin layout: the element's LEFT edge sits <paramref name="x"/> from
        /// the parent's left edge and its BOTTOM edge <paramref name="fromBottom"/> from the parent's
        /// bottom edge.
        /// </summary>
        public static void SetRectBottomLeft(RectTransform rt, float x, float fromBottom, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, fromBottom);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// Right-aligned, bottom-origin layout: RIGHT edge <paramref name="rightInset"/> from the
        /// parent's right edge, BOTTOM edge <paramref name="fromBottom"/> from the parent's bottom edge.
        /// </summary>
        public static void SetRectBottomRight(RectTransform rt, float rightInset, float fromBottom, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-rightInset, fromBottom);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// Creates a full-screen container under the shared canvas to hold one HUD's elements.
        /// The canvas survives scene loads, so a HUD must Destroy this container in its own
        /// OnDestroy — otherwise a mini game's UI leaks into the room it returns to.
        /// </summary>
        public static RectTransform Root(string name)
        {
            RectTransform rt = Rect(name, EnsureCanvas().transform);
            Stretch(rt);
            return rt;
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

        /// <summary>
        /// A filled circle sprite (for joystick bases and knobs), supersampled so the rim is
        /// smooth. Not nine-sliced — a circle is drawn at its own size.
        /// </summary>
        public static Sprite Circle(float radius, Color color)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(radius));
            string key = "circle|" + r + "|" + ColorKey(color);
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            int size = r * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            const int ss = 4;
            var pixels = new Color[size * size];
            float centre = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float dx = x + (sx + 0.5f) / ss - centre;
                    float dy = y + (sy + 0.5f) / ss - centre;
                    if (dx * dx + dy * dy <= r * r) inside++;
                }
                float alpha = inside / (float)(ss * ss);
                var c = color;
                c.a *= alpha;
                pixels[y * size + x] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
            sprite.name = "UguiCircle_" + r;
            Cache[key] = sprite;
            return sprite;
        }

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
