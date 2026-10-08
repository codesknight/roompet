using System.IO;
using UnityEditor;
using UnityEngine;

namespace DshMobileEditor
{
    /// <summary>
    /// Draws the app icon and hands it to the Android player settings.
    ///
    /// The picture: a small house at dusk with its door open and a cat sitting in the light,
    /// a moon behind the roof. Three flat shapes plus two glows — chosen because a launcher
    /// icon is usually first seen at 48px, where a cat silhouette in a lit doorway still reads
    /// and a detailed illustration would not.
    ///
    /// Colours come from the game itself: the night sky is the same indigo-to-plum wash as the
    /// dreamy skybox shader, the door light is the room's lamp colour, and the roof is the same
    /// coral the runner uses.
    ///
    /// Android wants three separate icon kinds, and they are drawn differently on purpose:
    ///   - Adaptive (API 26+): two layers, and the launcher may crop the outer third and mask
    ///     the result to a circle, so all the art sits inside the middle 66%;
    ///   - Round (API 25): one pre-composed layer, round-masked here so the corners are empty;
    ///   - Legacy: one pre-composed layer, rounded square for launchers that do no masking.
    /// </summary>
    public static class MobileIconBuilder
    {
        /// <summary>Largest size Android asks for; Unity downsamples it for the other densities.</summary>
        private const int IconSize = 432;

        private const string ArtFolder = "Assets/Mobile/Art";

        private const string BackgroundPath = ArtFolder + "/icon_background.png";
        private const string ForegroundPath = ArtFolder + "/icon_foreground.png";
        private const string RoundPath = ArtFolder + "/icon_round.png";
        private const string LegacyPath = ArtFolder + "/icon_legacy.png";

        // ------------------------------------------------------------------- palette
        private static readonly Color NightTop = IconPainter.Hex(0x1B1836);
        private static readonly Color NightMiddle = IconPainter.Hex(0x3A2A5A);
        private static readonly Color Dusk = IconPainter.Hex(0x7C4463);
        private static readonly Color WarmHorizon = IconPainter.Hex(0xD98A5A);

        private static readonly Color Moon = IconPainter.Hex(0xFFE9B8);
        private static readonly Color LampLight = IconPainter.Hex(0xFFC97F);
        private static readonly Color HouseWall = IconPainter.Hex(0xFFF4E2);
        private static readonly Color Roof = IconPainter.Hex(0xE4735C);
        private static readonly Color RoofEdge = IconPainter.Hex(0xF79C82);
        private static readonly Color Silhouette = IconPainter.Hex(0x2E2144);
        private static readonly Color Star = IconPainter.Hex(0xFFF6D8);
        private static readonly Color GroundNear = IconPainter.Hex(0x7A4A5E);
        private static readonly Color Hill = IconPainter.Hex(0xA8605C);

        [MenuItem("Tools/DSH Mobile/Icons/Rebuild App Icon")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(ArtFolder);

            Write(BackgroundPath, DrawBackground(IconSize));
            Write(ForegroundPath, DrawForeground(IconSize));

            // The pre-composed versions are the two layers flattened together.
            var round = new IconPainter(IconSize);
            DrawHouse(round, withSky: true);
            round.ClipCircle(0.5f, 0.5f, 0.5f);
            Write(RoundPath, round.ToTexture());

            var legacy = new IconPainter(IconSize);
            DrawHouse(legacy, withSky: true);
            legacy.ClipRoundedRect(0f, 0f, 1f, 1f, 0.2f);
            Write(LegacyPath, legacy.ToTexture());

            AssetDatabase.Refresh();
            ImportAll();
            Apply();

            Debug.Log($"[DshMobile] App icon rebuilt into {ArtFolder} and applied to the Android player settings.");
        }

        /// <summary>
        /// Renders the icon at the sizes a launcher actually shows it at, side by side, so the
        /// small ones can be judged before an APK is built. Legibility at 48px is the whole
        /// reason this exists.
        /// </summary>
        [MenuItem("Tools/DSH Mobile/Icons/Render Size Preview Sheet")]
        public static void RenderPreviewSheet()
        {
            int[] sizes = { 192, 96, 72, 48, 36 };
            const int gap = 12;

            int width = gap;
            foreach (int size in sizes) width += size + gap;
            int height = 192 + gap * 2;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.12f, 0.12f, 0.14f, 1f);

            int x = gap;
            foreach (int size in sizes)
            {
                var painter = new IconPainter(size);
                DrawHouse(painter, withSky: true);
                var tile = painter.ToTexture();
                var tilePixels = tile.GetPixels();

                int yOffset = (height - size) / 2;
                for (int ty = 0; ty < size; ty++)
                for (int tx = 0; tx < size; tx++)
                {
                    int destination = (yOffset + ty) * width + x + tx;
                    var source = tilePixels[ty * size + tx];
                    pixels[destination] = Color.Lerp(pixels[destination], source, source.a);
                }

                Object.DestroyImmediate(tile);
                x += size + gap;
            }

            sheet.SetPixels(pixels);
            sheet.Apply();

            string path = ArtFolder + "/icon_size_preview.png";
            Directory.CreateDirectory(ArtFolder);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            AssetDatabase.Refresh();
            Debug.Log($"[DshMobile] Size preview written to {path} ({string.Join(", ", sizes)} px).");
        }

        /// <summary>Pushes the drawn icon into the Android icon slots.</summary>
        [MenuItem("Tools/DSH Mobile/Icons/Apply App Icon To Player Settings")]
        public static void Apply()
        {
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            var round = AssetDatabase.LoadAssetAtPath<Texture2D>(RoundPath);
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyPath);

            if (background == null || foreground == null || round == null || legacy == null)
            {
                Debug.LogWarning("[DshMobile] Icon textures missing; rebuild the icon first.");
                return;
            }

            var group = BuildTargetGroup.Android;
            var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(group);

            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(group, kind);
                string name = kind.ToString();

                // "Adaptive (API 26)" is the only two-layer kind; the others are a single
                // flattened image. The names carry the API level, hence the prefix test.
                bool adaptive = name.StartsWith("Adaptive");
                bool isRound = name.StartsWith("Round");

                foreach (var icon in icons)
                {
                    if (adaptive)
                    {
                        icon.layerCount = 2;
                        icon.SetTexture(background, 0);
                        icon.SetTexture(foreground, 1);
                    }
                    else
                    {
                        icon.layerCount = 1;
                        icon.SetTexture(isRound ? round : legacy, 0);
                    }
                }

                PlayerSettings.SetPlatformIcons(group, kind, icons);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[DshMobile] Android app icon applied (adaptive + round + legacy).");
        }

        // -------------------------------------------------------------------- drawing

        /// <summary>The background layer: dusk sky, lamp glow on the ground, a few stars.</summary>
        private static Texture2D DrawBackground(int size)
        {
            var painter = new IconPainter(size);
            DrawSky(painter);
            return painter.ToTexture();
        }

        /// <summary>The foreground layer: moon, house, cat. Nothing outside the safe zone.</summary>
        private static Texture2D DrawForeground(int size)
        {
            var painter = new IconPainter(size);
            DrawHouse(painter, withSky: false);
            return painter.ToTexture();
        }

        private static void DrawSky(IconPainter painter)
        {
            painter.VerticalGradient(NightTop, NightMiddle, 0.42f, Dusk);

            // A broad, gentle hill rather than a flat band: it gives the house something to
            // stand on and curves the horizon, which reads better than a ruled line at any size.
            painter.Rect(0f, 0.66f, 1f, 1f, GroundNear);
            painter.Ellipse(0.5f, 1.06f, 1.05f, 0.36f, Hill);

            // Lamp light pooling in front of the door.
            painter.SoftCircle(0.52f, 0.80f, 0.44f, IconPainter.Hex(0xFFC98A, 0.22f), 2.0f);

            painter.Circle(0.185f, 0.150f, 0.013f, IconPainter.Hex(0xFFF6D8, 0.85f));
            painter.Circle(0.795f, 0.205f, 0.010f, IconPainter.Hex(0xFFF6D8, 0.70f));
            painter.Circle(0.330f, 0.082f, 0.009f, IconPainter.Hex(0xFFF6D8, 0.60f));
        }

        /// <summary>
        /// The house itself, shared by every icon kind so the three variants cannot drift apart.
        ///
        /// Everything is kept inside x 0.20..0.80, y 0.20..0.80 — the adaptive icon's safe zone.
        /// The launcher is allowed to crop and mask the outer third, and a roof sliced off by a
        /// round mask looks like a bug rather than a design.
        /// </summary>
        private static void DrawHouse(IconPainter painter, bool withSky)
        {
            if (withSky) DrawSky(painter);

            // Moon, tucked towards the upper right and clear of the mask and the roof line.
            painter.SoftCircle(0.645f, 0.310f, 0.160f, IconPainter.Hex(0xFFE9B8, 0.20f), 1.6f);
            painter.Circle(0.645f, 0.310f, 0.080f, Moon);

            // Roof: a plain triangle with a lighter lip under it, which is what keeps the roof
            // and the wall from merging into one cream shape at 48px.
            painter.Triangle(new Vector2(0.500f, 0.295f),
                             new Vector2(0.215f, 0.545f),
                             new Vector2(0.785f, 0.545f), Roof);
            painter.Rect(0.215f, 0.537f, 0.785f, 0.567f, RoofEdge);

            // Wall.
            painter.RoundedRect(0.305f, 0.560f, 0.695f, 0.792f, 0.022f, HouseWall);

            // A window on the wall, so the house reads as a house and not as a box.
            painter.RoundedRect(0.345f, 0.606f, 0.412f, 0.672f, 0.010f, LampLight);

            // A sitting cat is a cone, not a snowman: a triangle from the shoulders down to a
            // rounded base reads as "cat" at 432px, where an ellipse body just reads as a ball.
            const float catX = 0.532f;
            painter.Arch(catX, 0.084f, 0.588f, 0.792f, LampLight);

            painter.Triangle(new Vector2(catX, 0.690f),
                             new Vector2(catX - 0.060f, 0.788f),
                             new Vector2(catX + 0.060f, 0.788f), Silhouette);     // body
            painter.Ellipse(catX, 0.788f, 0.060f, 0.022f, Silhouette);            // haunches
            painter.Circle(catX, 0.6640f, 0.0340f, Silhouette);                   // head
            painter.Triangle(new Vector2(catX - 0.035f, 0.6640f),
                             new Vector2(catX - 0.026f, 0.6250f),
                             new Vector2(catX - 0.006f, 0.6520f), Silhouette);    // left ear
            painter.Triangle(new Vector2(catX + 0.035f, 0.6640f),
                             new Vector2(catX + 0.026f, 0.6250f),
                             new Vector2(catX + 0.006f, 0.6520f), Silhouette);    // right ear

            // Tail curling past the haunches: one more dark shape outside the doorway is what
            // makes the silhouette read as an animal rather than a smudge.
            painter.Ellipse(catX + 0.056f, 0.7780f, 0.035f, 0.014f, Silhouette);
        }

        // --------------------------------------------------------------------- assets

        private static void Write(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void ImportAll()
        {
            foreach (string path in new[] { BackgroundPath, ForegroundPath, RoundPath, LegacyPath })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
