using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DshMobileEditor
{
    /// <summary>
    /// Fixes up the Gradle project Unity generates for Android, for the two things this game
    /// needs and Player Settings cannot express.
    ///
    /// <b>1. The launcher label.</b> Android takes the app name from
    /// <c>PlayerSettings.productName</c>, but on desktop that same field is part of
    /// <c>Application.persistentDataPath</c> and of the PlayerPrefs registry path. Renaming the
    /// product to get a nicer icon label would therefore orphan every existing save — the pet
    /// would forget its name, its journal and its brain config. So the product name stays as it
    /// is and the label is written into the generated <c>strings.xml</c> instead.
    ///
    /// <b>2. Cleartext http.</b> Two separate things have to allow a plain-http server on the
    /// local network: Unity's <c>InsecureHttpOption</c>, which governs UnityWebRequest (the pet's
    /// brain client goes through that), and Android itself. An app targeting API 28+ gets
    /// <c>usesCleartextTraffic=false</c> by default, and Unity 2022.3 does not write that
    /// attribute for <c>DevelopmentOnly</c> — verified against the generated project, whose
    /// manifest had no cleartext entry at all. Anything reaching the network through Java-level
    /// APIs would then fail on device with no visible error. The attribute is added explicitly,
    /// and only where the setting says it belongs: <c>AlwaysAllowed</c> for every build,
    /// <c>DevelopmentOnly</c> for development builds, and nothing at all for <c>NotAllowed</c> —
    /// a release build keeps Android's secure default.
    /// </summary>
    public class MobileAndroidPackaging : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;

        /// <summary>
        /// Whether the build in progress is a development build. Recorded in the preprocess
        /// step, which is the only place the build options are visible — the post-generate hook
        /// gets a project path and nothing else.
        /// </summary>
        private static bool _developmentBuild;

        public void OnPreprocessBuild(BuildReport report)
        {
            _developmentBuild = (report.summary.options & BuildOptions.Development) != 0
                                || EditorUserBuildSettings.development;
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            ApplyAppLabel(path);
            ApplyManifestExtensions(path);
        }

        // ------------------------------------------------------------------- label

        private static void ApplyAppLabel(string path)
        {
            string stringsPath = Path.Combine(path, "..", "launcher", "src", "main", "res", "values", "strings.xml");
            stringsPath = Path.GetFullPath(stringsPath);

            if (!File.Exists(stringsPath))
            {
                Debug.LogWarning($"[DshMobile] No launcher strings.xml at {stringsPath}; " +
                                 "the app will use the product name as its label.");
                return;
            }

            string xml = File.ReadAllText(stringsPath);
            if (xml.Contains($">{MobileBuildMenu.AppLabel}<"))
            {
                return;
            }

            string patched = System.Text.RegularExpressions.Regex.Replace(
                xml,
                "(<string\\s+name=\"app_name\"\\s*>)[^<]*(</string>)",
                "$1" + MobileBuildMenu.AppLabel + "$2");

            if (patched == xml)
            {
                Debug.LogWarning("[DshMobile] Launcher strings.xml has no app_name entry to patch.");
                return;
            }

            File.WriteAllText(stringsPath, patched, new UTF8Encoding(false));
            Debug.Log($"[DshMobile] Launcher label set to \"{MobileBuildMenu.AppLabel}\".");
        }

        // --------------------------------------------------------------- manifest

        /// <summary>
        /// Adds what the game needs from the manifest and Unity cannot express: the VIBRATE
        /// permission for haptics, the Android 11 package-visibility declaration the speech
        /// engine needs to be visible at all, and — where the insecure-http policy allows it —
        /// cleartext for a local gateway.
        /// </summary>
        private void ApplyManifestExtensions(string path)
        {
            string manifestPath = FindManifest(path);
            if (manifestPath == null)
            {
                Debug.LogWarning($"[DshMobile] No module manifest found to patch under {path}; " +
                                 "cleartext http will follow Android's secure default.");
                return;
            }

            string manifest = File.ReadAllText(manifestPath);
            string patched = manifest;

            // Vibration needs a normal permission, and Unity has no player setting for it
            // (only INTERNET has one). Without it every pulse is silently dropped.
            bool vibrate = !patched.Contains("android.permission.VIBRATE");
            if (vibrate)
            {
                patched = patched.Insert(ApplicationAnchor(patched),
                    "<uses-permission android:name=\"android.permission.VIBRATE\" />\n  ");
            }

            // Text-to-speech, and this is the one that made "the pet never talks on my phone"
            // happen: from Android 11 (API 30) an app can only see other packages it declares
            // an intent for. Without this <queries> block the speech engine is invisible, the
            // TextToSpeech constructor never calls back, and every utterance is dropped — on the
            // device only, with nothing in the log and nothing in the editor to reproduce it.
            bool queries = !patched.Contains("android.intent.action.TTS_SERVICE");
            if (queries)
            {
                patched = patched.Insert(ApplicationAnchor(patched), TtsQueries);
            }

            bool cleartext = AllowsCleartext() && !patched.Contains("usesCleartextTraffic");
            if (cleartext)
            {
                patched = patched.Insert(ApplicationAnchor(patched) + "<application ".Length,
                    "android:usesCleartextTraffic=\"true\" ");
            }

            if (patched == manifest)
            {
                Debug.Log("[DshMobile] Manifest already declares the permissions this build needs.");
                return;
            }

            // Written without a BOM: this file is consumed by Gradle, and a byte-order mark
            // ahead of the XML declaration trips some parsers.
            File.WriteAllText(manifestPath, patched, new UTF8Encoding(false));

            var added = new System.Collections.Generic.List<string>();
            if (vibrate) added.Add("VIBRATE");
            if (queries) added.Add("TTS queries (Android 11+ package visibility)");
            added.Add(cleartext
                ? $"cleartext http ({(IsDevelopmentBuild() ? "development" : "release")} build, " +
                  $"InsecureHttpOption.{PlayerSettings.insecureHttpOption})"
                : "no cleartext (secure default)");
            Debug.Log("[DshMobile] Manifest patched: " + string.Join(", ", added.ToArray()) + ".");
        }

        /// <summary>
        /// The package-visibility declaration for text-to-speech.
        ///
        /// A direct child of &lt;manifest&gt;, which is why it is inserted at the &lt;application&gt;
        /// anchor rather than inside it.
        /// </summary>
        private const string TtsQueries =
            "<!-- Android 11+ package visibility: the speech engine is invisible without this. -->\n" +
            "  <queries>\n" +
            "    <intent>\n" +
            "      <action android:name=\"android.intent.action.TTS_SERVICE\" />\n" +
            "    </intent>\n" +
            "  </queries>\n  ";

        /// <summary>Offset of the <c>&lt;application&gt;</c> element, or -1.</summary>
        private static int ApplicationAnchor(string manifest)
            => manifest.IndexOf("<application ", System.StringComparison.Ordinal);

        private static bool IsDevelopmentBuild() => _developmentBuild;

        /// <summary>True when the player settings and the build kind together allow plain http.</summary>
        private static bool AllowsCleartext()
        {
            switch (PlayerSettings.insecureHttpOption)
            {
                case InsecureHttpOption.AlwaysAllowed:
                    return true;
                case InsecureHttpOption.DevelopmentOnly:
                    return _developmentBuild;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Locates the module manifest. Unity's documentation says this hook receives the root
        /// of the Gradle project but in practice hands over the unityLibrary module, and the two
        /// differ between versions — so both are tried rather than assumed.
        /// </summary>
        private static string FindManifest(string path)
        {
            string[] candidates =
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "launcher", "src", "main", "AndroidManifest.xml")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }
}
