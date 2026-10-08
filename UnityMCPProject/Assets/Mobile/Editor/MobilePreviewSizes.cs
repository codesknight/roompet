using System;
using System.Reflection;
using DshMobile;
using UnityEditor;
using UnityEngine;

namespace DshMobileEditor
{
    /// <summary>
    /// Puts the Game view into a phone-shaped viewport.
    ///
    /// A hand-written IMGUI layout can only be judged at the size it will really run at, and the
    /// editor's Game view defaults to whatever the window happens to be. There is no public API
    /// for this — <c>Screen.SetResolution</c> is a no-op in the editor — so the Game view's own
    /// size list is driven through reflection instead. Everything is looked up by name at call
    /// time and every step is guarded, because this is the kind of code that breaks silently
    /// when a Unity version moves a field.
    ///
    /// Used to produce the phone screenshots in <c>docs/evidence</c>; also the quickest way to
    /// check a layout change before installing an APK.
    /// </summary>
    public static class MobilePreviewSizes
    {
        /// <summary>
        /// Where the simulated density is remembered.
        ///
        /// It has to be persisted rather than just set: every play-mode entry reloads the script
        /// domain, which resets <see cref="MobileUi.ReferenceDpi"/> to 0 — so a preview that
        /// looked right in edit mode would silently fall back to the editor's 96 dpi the moment
        /// you pressed Play, and the layout you checked would not be the one you were looking at.
        /// </summary>
        private const string PreviewDpiKey = "DshMobile.PreviewDpi";

        /// <summary>Re-applies the remembered density after every domain reload.</summary>
        [InitializeOnLoadMethod]
        private static void RestorePreviewDensity()
        {
            float dpi = EditorPrefs.GetFloat(PreviewDpiKey, 0f);
            if (dpi > 60f) MobileUi.ReferenceDpi = dpi;
        }

        /// <summary>A size the game is expected to look right at.</summary>
        private struct Preset
        {
            public string Label;
            public int Width;
            public int Height;

            /// <summary>Density to assume, so the preview lays out the same design space as the device.</summary>
            public float Dpi;
        }

        private static readonly Preset[] Presets =
        {
            new Preset { Label = "DSH Phone Portrait 1080x2400", Width = 1080, Height = 2400, Dpi = 420f },
            new Preset { Label = "DSH Phone Landscape 2400x1080", Width = 2400, Height = 1080, Dpi = 420f },
            new Preset { Label = "DSH Tall Portrait 1080x2340", Width = 1080, Height = 2340, Dpi = 400f },
            new Preset { Label = "DSH Tablet Portrait 1600x2560", Width = 1600, Height = 2560, Dpi = 320f }
        };

        [MenuItem("Tools/DSH Mobile/Preview/Phone Portrait 1080x2400 %#1")]
        public static void PreviewPortraitPhone() => Apply(Presets[0]);

        [MenuItem("Tools/DSH Mobile/Preview/Phone Landscape 2400x1080 %#2")]
        public static void PreviewLandscapePhone() => Apply(Presets[1]);

        [MenuItem("Tools/DSH Mobile/Preview/Tall Portrait 1080x2340 %#3")]
        public static void PreviewTallPortrait() => Apply(Presets[2]);

        [MenuItem("Tools/DSH Mobile/Preview/Tablet Portrait 1600x2560 %#4")]
        public static void PreviewTabletPortrait() => Apply(Presets[3]);

        /// <summary>
        /// Drops the simulated density, so the scale goes back to whatever the platform reports
        /// (96 in the editor). Only useful to see how a layout behaves on a low-density screen.
        /// </summary>
        [MenuItem("Tools/DSH Mobile/Preview/Use Platform DPI")]
        public static void UsePlatformDpi()
        {
            MobileUi.ReferenceDpi = 0f;
            EditorPrefs.SetFloat(PreviewDpiKey, 0f);
            Debug.Log($"[DshMobile] Back to the platform's {MobileUi.EffectiveDpi:F0} dpi " +
                      $"(scale {MobileUi.UiScale:F2}, design " +
                      $"{Screen.width / MobileUi.UiScale:F0}x{Screen.height / MobileUi.UiScale:F0}).");
        }

        /// <summary>Reports what the Game view is currently set to, and the layout it implies.</summary>
        [MenuItem("Tools/DSH Mobile/Preview/Report Current Viewport")]
        public static void ReportCurrentViewport()
        {
            var view = GameViewWindow();
            int index = view != null ? SelectedSizeIndex(view) : -1;
            string label = index >= 0 ? DisplayText(index) : "(unknown)";

            Debug.Log($"[DshMobile] Game view: {Screen.width}x{Screen.height} " +
                      $"[{label}] index={index} | " +
                      $"orientation={(MobileUi.IsPortrait ? "portrait" : "landscape")} " +
                      $"aspect={MobileUi.Aspect:F2} dpi={MobileUi.EffectiveDpi:F0}" +
                      $"{(MobileUi.ReferenceDpi > 60f ? " (simulated)" : " (platform)")} " +
                      $"scale={MobileUi.UiScale:F2} " +
                      $"design={Screen.width / MobileUi.UiScale:F0}x{Screen.height / MobileUi.UiScale:F0}");
        }

        // ------------------------------------------------------------------ plumbing

        private static void Apply(Preset preset)
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            var sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
            if (gameViewType == null || sizesType == null || sizeType == null)
            {
                Debug.LogWarning("[DshMobile] Cannot reach the Game view size list on this Unity version.");
                return;
            }

            var group = CurrentGroup(sizesType);
            if (group == null)
            {
                Debug.LogWarning("[DshMobile] Could not read the Game view size group.");
                return;
            }

            int index = IndexOfOrAdd(group, sizeType, preset);
            if (index < 0)
            {
                Debug.LogWarning($"[DshMobile] Could not register the size {preset.Width}x{preset.Height}.");
                return;
            }

            var view = GameViewWindow();
            var property = gameViewType.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (view == null || property == null)
            {
                Debug.LogWarning("[DshMobile] Could not select the Game view size.");
                return;
            }

            property.SetValue(view, index);
            if (view is EditorWindow window) window.Repaint();

            // Simulate the density too. Without this the preview is laid out for a 96-dpi
            // screen and the phone for a 420-dpi one, so the two design spaces differ by ~20%
            // and anything verified here can still be wrong there.
            MobileUi.ReferenceDpi = preset.Dpi;
            EditorPrefs.SetFloat(PreviewDpiKey, preset.Dpi);

            Debug.Log($"[DshMobile] Game view set to {preset.Label} " +
                      $"({preset.Width}x{preset.Height}, index {index}), dpi simulated at {preset.Dpi:F0} " +
                      $"→ scale {MobileUi.UiScale:F2}, design " +
                      $"{Screen.width / MobileUi.UiScale:F0}x{Screen.height / MobileUi.UiScale:F0}. " +
                      "Turn on Tools/DSH Mobile/Toggle Touch Preview to see the phone HUD.");
        }

        /// <summary>
        /// Finds the size in the Game view's list, adding it if this is the first time. Adding is
        /// sticky — Unity remembers custom sizes in the editor's preferences — so the list grows
        /// by at most four entries, ever.
        /// </summary>
        private static int IndexOfOrAdd(object group, Type sizeType, Preset preset)
        {
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize");

            for (int i = 0; i < total; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                if (size == null) continue;

                int width = (int)sizeType.GetProperty("width").GetValue(size);
                int height = (int)sizeType.GetProperty("height").GetValue(size);
                if (width == preset.Width && height == preset.Height) return i;
            }

            var sizeTypeEnum = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
            if (sizeTypeEnum == null) return -1;

            object fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");
            object created = Activator.CreateInstance(sizeType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { fixedResolution, (object)preset.Width, preset.Height, preset.Label }, null);

            var addCustom = groupType.GetMethod("AddCustomSize");
            if (addCustom == null || created == null) return -1;

            addCustom.Invoke(group, new[] { created });

            // Re-scan rather than trusting IndexOf: it does not compare the way this needs
            // (it answered 0 — "Free Aspect" — for a size that had just been appended at the
            // end of the list), and selecting the wrong index silently gives the wrong viewport.
            total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < total; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                if (size == null) continue;

                int width = (int)sizeType.GetProperty("width").GetValue(size);
                int height = (int)sizeType.GetProperty("height").GetValue(size);
                if (width == preset.Width && height == preset.Height) return i;
            }

            return -1;
        }

        private static string DisplayText(int index)
        {
            var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            var group = sizesType != null ? CurrentGroup(sizesType) : null;
            if (group == null) return "(unknown)";

            var getSize = group.GetType().GetMethod("GetGameViewSize");
            object size = getSize.Invoke(group, new object[] { index });
            if (size == null) return "(unknown)";

            var baseText = size.GetType().GetProperty("baseText");
            return baseText != null ? Convert.ToString(baseText.GetValue(size)) : "(unknown)";
        }

        /// <summary>
        /// The size group for the active build target. <c>GameViewSizes</c> derives from
        /// <c>ScriptableSingleton&lt;T&gt;</c>, and its <c>instance</c> property lives on that
        /// base — a plain <c>GetProperty</c> on the derived type returns null and the whole
        /// thing quietly does nothing.
        /// </summary>
        private static object CurrentGroup(Type sizesType)
        {
            var instance = sizesType.GetProperty("instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (instance == null) return null;

            object sizes = instance.GetValue(null);
            if (sizes == null) return null;

            var groupProperty = sizes.GetType().GetProperty("currentGroup");
            return groupProperty != null ? groupProperty.GetValue(sizes) : null;
        }

        private static EditorWindow GameViewWindow()
        {
            var type = Type.GetType("UnityEditor.GameView,UnityEditor");
            return type != null ? EditorWindow.GetWindow(type) : null;
        }

        private static int SelectedSizeIndex(EditorWindow view)
        {
            var property = view.GetType().GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object value = property != null ? property.GetValue(view) : null;
            return value is int index ? index : -1;
        }
    }
}
