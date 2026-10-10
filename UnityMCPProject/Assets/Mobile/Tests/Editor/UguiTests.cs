using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DshMobile.Tests
{
    /// <summary>
    /// Covers the uGUI foundation that replaces IMGUI: the CJK font resolver, the rounded
    /// nine-slice sprite generator, and the widget factories. These are the pieces every
    /// migrating HUD will build on, so they are checked here rather than re-verified per HUD.
    ///
    /// Scene objects are created under a throwaway canvas and destroyed immediately after, so
    /// these tests leave no trace in the open scene.
    /// </summary>
    public class UguiTests
    {
        [Test]
        public void Font_ResolveReturnsACjkCapableFont()
        {
            Font font = UguiFont.Resolve(32);
            Assert.IsNotNull(font, "a dynamic font must resolve");
            Assert.IsTrue(UguiFont.CoversCjk(font), "the resolved font must cover the UI's Chinese");
            UguiFont.ClearCache();
        }

        [Test]
        public void Font_CacheReturnsTheSameInstance()
        {
            Font a = UguiFont.Resolve(32);
            Font b = UguiFont.Resolve(32);
            Assert.AreSame(a, b, "same size reuses the cached font");
            UguiFont.ClearCache();
        }

        [Test]
        public void Rounded_SpriteHasCorrectBorderAndSize()
        {
            const int radius = 16;
            Sprite sprite = UguiRounded.Sprite(radius, new Color(0f, 0f, 0f, 0.9f),
                new Color(1f, 1f, 1f, 0.3f), 2);

            Assert.IsNotNull(sprite, "a rounded sprite is produced");
            Assert.AreEqual(radius * 2 + 2, (int)sprite.texture.width, "texture is radius*2+2");
            Assert.AreEqual((int)sprite.texture.width, (int)sprite.texture.height, "texture is square");

            var expected = new Vector4(radius, radius, radius, radius);
            Assert.AreEqual(expected.x, sprite.border.x, 0.001f, "left nine-slice border is the radius");
            Assert.AreEqual(expected.y, sprite.border.y, 0.001f, "bottom nine-slice border is the radius");
            Assert.AreEqual(expected.z, sprite.border.z, 0.001f, "right nine-slice border is the radius");
            Assert.AreEqual(expected.w, sprite.border.w, 0.001f, "top nine-slice border is the radius");
        }

        [Test]
        public void Rounded_SpriteIsCachedPerColour()
        {
            Sprite a = UguiRounded.Sprite(12, Color.black, Color.white, 2);
            Sprite b = UguiRounded.Sprite(12, Color.black, Color.white, 2);
            Assert.AreSame(a, b, "same key reuses the generated sprite");
        }

        [Test]
        public void Text_FactoryAssignsTheCjkFont()
        {
            var canvas = NewCanvas();
            try
            {
                Text text = Ugui.Text("Title", canvas.transform, "宠物房间", 28, Color.white);
                Assert.IsNotNull(text.font, "a Text gets the shared font");
                Assert.IsTrue(UguiFont.CoversCjk(text.font), "the Text font covers Chinese");
                Assert.AreEqual("宠物房间", text.text);
                Assert.IsFalse(text.raycastTarget, "static text does not eat touches");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void Panel_FactoryProducesASlicedImage()
        {
            var canvas = NewCanvas();
            try
            {
                Image panel = Ugui.Panel("Panel", canvas.transform, 16f,
                    new Color(0.08f, 0.09f, 0.13f, 0.94f), new Color(1f, 1f, 1f, 0.16f), 1.5f);

                Assert.IsNotNull(panel.sprite, "a panel carries a rounded sprite");
                Assert.AreEqual(Image.Type.Sliced, panel.type, "a panel is nine-sliced");
                Assert.IsFalse(panel.raycastTarget, "a static panel does not eat touches");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void SetRect_LaysOutWithTopLeftOrigin()
        {
            var canvas = NewCanvas();
            try
            {
                RectTransform rt = Ugui.Rect("Box", canvas.transform);
                Ugui.SetRect(rt, 10f, 20f, 300f, 40f);

                Assert.AreEqual(new Vector2(0f, 1f), rt.anchorMin, "anchored to the top-left");
                Assert.AreEqual(new Vector2(10f, -20f), rt.anchoredPosition, "x right, y down");
                Assert.AreEqual(new Vector2(300f, 40f), rt.sizeDelta, "size is set directly");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void SetRectRight_LaysOutRightAlignedTopOrigin()
        {
            var canvas = NewCanvas();
            try
            {
                RectTransform rt = Ugui.Rect("Box", canvas.transform);
                Ugui.SetRectRight(rt, 18f, 14f, 76f, 30f);

                Assert.AreEqual(new Vector2(1f, 1f), rt.anchorMin, "anchored to the top-right");
                Assert.AreEqual(new Vector2(1f, 1f), rt.pivot, "pivot at the top-right corner");
                Assert.AreEqual(new Vector2(-18f, -14f), rt.anchoredPosition, "right edge 18 in, top 14 down");
                Assert.AreEqual(new Vector2(76f, 30f), rt.sizeDelta, "size is set directly");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void SetRectBottomRight_LaysOutBottomRight()
        {
            var canvas = NewCanvas();
            try
            {
                RectTransform rt = Ugui.Rect("Box", canvas.transform);
                Ugui.SetRectBottomRight(rt, 18f, 18f, 200f, 22f);

                Assert.AreEqual(new Vector2(1f, 0f), rt.anchorMin, "anchored to the bottom-right");
                Assert.AreEqual(new Vector2(-18f, 18f), rt.anchoredPosition, "right 18 in, bottom 18 up");
                Assert.AreEqual(new Vector2(200f, 22f), rt.sizeDelta, "size is set directly");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void SetRectBottomLeft_LaysOutBottomLeft()
        {
            var canvas = NewCanvas();
            try
            {
                RectTransform rt = Ugui.Rect("Box", canvas.transform);
                Ugui.SetRectBottomLeft(rt, 16f, 8f, 150f, 36f);

                Assert.AreEqual(new Vector2(0f, 0f), rt.anchorMin, "anchored to the bottom-left");
                Assert.AreEqual(new Vector2(16f, 8f), rt.anchoredPosition, "left 16 in, bottom 8 up");
                Assert.AreEqual(new Vector2(150f, 36f), rt.sizeDelta, "size is set directly");
            }
            finally
            {
                Object.DestroyImmediate(canvas.gameObject);
            }
        }

        private static Canvas NewCanvas()
        {
            var go = new GameObject("UguiTestCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
