using System.Text;
using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Speaking the pet's lines out loud, through the platform's own text-to-speech engine.
    ///
    /// Deliberately the platform engine and not a downloaded voice model. The pet's replies are
    /// the one place in this game where the text is unbounded — it comes from a language model —
    /// so anything that needs a fixed clip per line cannot work, and a neural TTS would mean
    /// shipping hundreds of megabytes to say sentences that are different every time. Android's
    /// TextToSpeech is already installed, already speaks Chinese, and costs nothing.
    ///
    /// Two honest limits, both stated in the settings panel rather than hidden:
    ///  - it exists on Android only. In the editor and on desktop <see cref="Available"/> is
    ///    false and every call here is a no-op, so nothing has to be conditioned on platform;
    ///  - the voice is the phone's, bent per pet by pitch and rate. A bear and a rabbit sound
    ///    like different animals reading the same sentence, which is as far as this can honestly
    ///    go without shipping voices.
    ///
    /// Input is not implemented. Speech recognition needs a microphone permission, a recognizer
    /// intent, and a whole turn-taking UI; a half-built one that pops a system dialog and
    /// sometimes returns nothing is worse than the keyboard that already works.
    /// </summary>
    public static class MobileTts
    {
        public const string EnabledKey = "dshpet.tts";

        private static bool _enabledLoaded;
        private static bool _enabled;

        private static bool _initTried;
        private static bool _ready;
        private static bool _failed;
        private static AndroidJavaObject _engine;
        private static AndroidJavaObject _bundle;
        private static InitListener _listener;

        /// <summary>True when there is a text-to-speech engine to use at all.</summary>
        public static bool Available => Application.platform == RuntimePlatform.Android;

        /// <summary>True once the engine has come up and can be spoken to.</summary>
        public static bool Ready => _ready;

        /// <summary>
        /// Whether the pet reads its lines out loud.
        ///
        /// Defaults to OFF, unlike the pet's own chirps. A phone that suddenly starts talking is
        /// a phone you mute, and the player who wants this will find it in the settings.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (!_enabledLoaded)
                {
                    _enabled = PlayerPrefs.GetInt(EnabledKey, 0) != 0;
                    _enabledLoaded = true;
                }
                return _enabled;
            }
            set
            {
                _enabled = value;
                _enabledLoaded = true;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Forgets the cached setting, so the next read comes from disk.</summary>
        public static void ResetCache() => _enabledLoaded = false;

        /// <summary>
        /// Says one line. Safe to call with anything, on any platform, at any time.
        ///
        /// Stage directions are dropped: the pet's transcript is full of "（开心地晃了晃）", and a
        /// synthetic voice reading parentheses aloud is exactly the thing that makes a talking
        /// pet embarrassing rather than charming. A line that is nothing but a stage direction
        /// produces no speech at all, which is why poking the pet chirps instead of narrating.
        /// </summary>
        public static void Speak(string text, float pitch = 1f, float rate = 1f)
        {
            if (!Available || !Enabled) return;

            string line = Speech(text);
            if (string.IsNullOrEmpty(line)) return;

            if (!EnsureEngine()) return;

            try
            {
                _engine.Call("setPitch", Mathf.Clamp(pitch, 0.5f, 2f));
                _engine.Call("setSpeechRate", Mathf.Clamp(rate, 0.5f, 2f));

                // QUEUE_FLUSH: the newest line wins. A pet that queues four replies and reads them
                // out one after another long after the conversation moved on is worse than one
                // that interrupts itself.
                _engine.Call<int>("speak", line, 1, _bundle, "dshpet");
            }
            catch (System.Exception e)
            {
                _failed = true;
                Debug.LogWarning("[DshMobile] TTS speak failed: " + e.Message);
            }
        }

        /// <summary>Cuts off whatever is being spoken, for when the player leaves the room.</summary>
        public static void Stop()
        {
            if (_engine == null) return;
            try { _engine.Call<int>("stop"); }
            catch { /* the engine is going away anyway */ }
        }

        public static void Shutdown()
        {
            Stop();
            if (_engine != null)
            {
                try { _engine.Call("shutdown"); }
                catch { /* nothing useful to do */ }
                _engine.Dispose();
                _engine = null;
            }

            if (_bundle != null) { _bundle.Dispose(); _bundle = null; }
            _listener = null;
            _initTried = false;
            _ready = false;
            _failed = false;
        }

        /// <summary>
        /// The spoken form of a line: stage directions and emoji removed.
        ///
        /// Pure and public because this is the part that can be wrong in a way nobody notices
        /// until they hear it, and it unit tests without an Android device.
        /// </summary>
        public static string Speech(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var builder = new StringBuilder(text.Length);
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '（' || c == '(' || c == '[' || c == '【') { depth++; continue; }
                if (c == '）' || c == ')' || c == ']' || c == '】')
                {
                    if (depth > 0) depth--;
                    continue;
                }
                if (depth > 0) continue;

                // Emoji and pictographs: the voice would read them as their Unicode name, or
                // skip them with a click. Neither belongs in a spoken sentence.
                if (c >= 0x2190 && c <= 0x2BFF) continue;
                if (c >= 0xD800 && c <= 0xDBFF)      // high surrogate: skip the pair
                {
                    if (i + 1 < text.Length && text[i + 1] >= 0xDC00 && text[i + 1] <= 0xDFFF) i++;
                    continue;
                }
                if (c == 0xFE0F || c == 0x200D) continue;

                builder.Append(c);
            }

            // Collapse the whitespace the removals leave behind, and drop leading punctuation
            // that only made sense in front of the stage direction.
            string result = builder.ToString().Replace("　", " ").Trim();
            while (result.Length > 0 && (result[0] == '，' || result[0] == '。' || result[0] == ',' ||
                                         result[0] == '.' || result[0] == '、' || result[0] == ' '))
            {
                result = result.Substring(1);
            }

            return result.Trim();
        }

        private static bool EnsureEngine()
        {
            if (_ready) return true;
            if (_failed || _initTried) return _ready;

            _initTried = true;

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var china = locale.GetStatic<AndroidJavaObject>("CHINA"))
                {
                    _listener = new InitListener();
                    _engine = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, _listener);
                    _engine.Call<int>("setLanguage", china);
                    _bundle = new AndroidJavaObject("android.os.Bundle");
                }
            }
            catch (System.Exception e)
            {
                _failed = true;
                Debug.LogWarning("[DshMobile] TTS unavailable: " + e.Message);
                return false;
            }

            // Initialisation is asynchronous: onInit arrives a few hundred milliseconds later.
            // The first line or two may therefore be silent, which is why the engine is also
            // woken when the switch is turned on rather than on the first reply.
            return _ready;
        }

        /// <summary>Warms the engine up, so the first reply is not swallowed by initialisation.</summary>
        public static void WarmUp()
        {
            if (!Available || !Enabled) return;
            EnsureEngine();
        }

        /// <summary>The Android OnInitListener, bridged into managed code.</summary>
        private class InitListener : AndroidJavaProxy
        {
            public InitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }

            // ReSharper disable once InconsistentNaming — the JNI name is what matters.
            public void onInit(int status)
            {
                _ready = status == 0;   // 0 == TextToSpeech.SUCCESS
                if (!_ready) _failed = true;
            }
        }
    }
}
