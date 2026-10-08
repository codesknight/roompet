using System.IO;
using DshMobile;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DshMobileEditor
{
    /// <summary>
    /// The Android side of the project: player settings and a one-click APK build.
    ///
    /// Kept as editor menu items rather than a script the build machine runs by hand, so the
    /// settings that make the game work on a phone (landscape, ARM64, internet permission,
    /// touch-friendly orientation) are written down somewhere reviewable — and so a rebuild is
    /// one click after a checkout.
    /// </summary>
    public static class MobileBuildMenu
    {
        private const string PackageId = "com.codesknight.roompet";
        private const string OutputDir = "Builds/Android";
        private const string ApkName = "RoomPet.apk";

        /// <summary>Name shown under the icon on the phone.</summary>
        public const string AppLabel = "RoomPet";

        [MenuItem("Tools/DSH Mobile/Configure Android Player Settings")]
        public static void ConfigurePlayerSettings()
        {
            // Company and product name are deliberately NOT set here. On desktop they are part
            // of Application.persistentDataPath and of the PlayerPrefs registry path, so
            // "renaming the game" silently orphans every existing save — the pet would forget
            // its name, its journal and the brain config. The label on the phone is patched
            // into the generated Gradle resources instead (see MobileAndroidPackaging).

            // ------------------------------------------------------------ orientation
            // The room is wider than it is tall and the HUD is laid out sideways, so the game
            // is landscape only — portrait would letterbox everything into a strip.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Hide the status bar and keep the navigation bar from resizing the view: both
            // would otherwise show up as a black band and confuse the safe-area padding.
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.Android.renderOutsideSafeArea = false;

            // --------------------------------------------------------------- packaging
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageId);
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            // ARM64 only: every Android device that can run this is 64-bit, and shipping both
            // architectures doubles the build for no benefit.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // The pet talks to an LLM over the network; without INTERNET the brain silently
            // falls back to the offline one, which looks like a bug rather than a setting.
            PlayerSettings.Android.forceInternetPermission = true;

            // The internal gateway is plain http. Development-only keeps cleartext working in a
            // test build without weakening a release build.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;

            // The default Android splash is a Unity logo on white; a flat dark background
            // matches the game and costs nothing.
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.10f, 0.09f, 0.13f);

            AssetDatabase.SaveAssets();
            Debug.Log($"[DshMobile] Android settings applied: {PackageId}, landscape, ARM64/IL2CPP, " +
                      $"min SDK {PlayerSettings.Android.minSdkVersion}, INTERNET permission on.");
        }

        [MenuItem("Tools/DSH Mobile/Build APK")]
        public static void BuildApk()
        {
            ConfigurePlayerSettings();

            Directory.CreateDirectory(OutputDir);
            string output = Path.Combine(OutputDir, ApkName);

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[DshMobile] Build {summary.result}: {summary.totalErrors} error(s).");
                return;
            }

            double megabytes = summary.totalSize / (1024.0 * 1024.0);
            Debug.Log($"[DshMobile] APK built: {Path.GetFullPath(output)} " +
                      $"({megabytes:F1} MB, {summary.totalTime.TotalSeconds:F0}s)");
        }

        /// <summary>Scenes currently enabled in Build Settings, in order.</summary>
        private static string[] EnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) list.Add(scene.path);
            }

            if (list.Count == 0)
            {
                Debug.LogWarning("[DshMobile] No enabled scenes; running Tools/DSH Pet/Add Scenes To Build Settings.");
                list.Add("Assets/Pet/Scenes/PetRoom.unity");
            }

            return list.ToArray();
        }

        /// <summary>
        /// Shows the phone layout in the editor: touch controls, phone scaling and the
        /// collapsed chat, driven with the mouse. Without this the mobile UI can only be
        /// checked by installing an APK.
        /// </summary>
        [MenuItem("Tools/DSH Mobile/Toggle Touch Preview %#t")]
        public static void ToggleTouchPreview()
        {
            MobileUi.ForceTouchControls = !MobileUi.ForceTouchControls;
            Debug.Log(MobileUi.ForceTouchControls
                ? "[DshMobile] Touch preview ON: the HUD now draws the phone layout and the mouse acts as a finger."
                : "[DshMobile] Touch preview OFF: back to the desktop HUD.");
        }

        [MenuItem("Tools/DSH Mobile/Toggle Touch Preview %#t", true)]
        private static bool ToggleTouchPreviewValidate() => true;

        [MenuItem("Tools/DSH Mobile/Report Mobile Status")]
        public static void ReportStatus()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[DshMobile] status");
            sb.AppendLine($"  platform            : {Application.platform}");
            sb.AppendLine($"  isMobilePlatform    : {MobileUi.IsMobile}");
            sb.AppendLine($"  touch controls      : {MobileUi.UseTouchControls} (forced={MobileUi.ForceTouchControls})");
            sb.AppendLine($"  screen              : {Screen.width}x{Screen.height} @ {Screen.dpi:F0} dpi");
            sb.AppendLine($"  safe area           : {MobileUi.SafeArea}");
            sb.AppendLine($"  ui scale            : {MobileUi.UiScale:F2} (design {Screen.width / MobileUi.UiScale:F0}x{Screen.height / MobileUi.UiScale:F0})");
            sb.AppendLine($"  android target ok   : {BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)}");
            sb.AppendLine($"  package id          : {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");
            sb.AppendLine($"  architectures       : {PlayerSettings.Android.targetArchitectures}");
            sb.AppendLine($"  scripting backend   : {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}");
            sb.AppendLine($"  orientation         : {PlayerSettings.defaultInterfaceOrientation}");
            Debug.Log(sb.ToString());
        }
    }
}
