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
    ///
    /// The first version of this shipped and said nothing on a real phone. Two causes, both
    /// invisible in the editor: from Android 11 the speech engine is not even visible to the app
    /// without a &lt;queries&gt; declaration in the manifest (see <c>MobileAndroidPackaging</c>),
    /// and a device whose engine has no Chinese voice data accepts the utterance and then plays
    /// silence. So the engine now reports what it actually did, the settings panel shows that
    /// report, and a button speaks a fixed line on the spot instead of waiting for the pet to
    /// say something interesting.
    /// </summary>
    public static class MobileTts
    {
        public const string EnabledKey = "dshpet.tts";
        public const string HintedKey = "dshpet.tts.hinted";

        /// <summary>The line the test button speaks: fixed, so it needs no reply to exist.</summary>
        public const string TestLine = "你好，我是你的宠物，我现在会说话了。";

        private static bool _enabledLoaded;
        private static bool _enabled;

        private static bool _initTried;
        private static bool _ready;
        private static AndroidJavaObject _engine;
        private static AndroidJavaObject _bundle;
        private static InitListener _listener;

        private static float _lastAttemptAt = -999f;
        private static string _lastError = "";
        private static string _engineName = "";
        private static int _initStatus = int.MinValue;
        private static int _languageStatus = int.MinValue;
        private static int _pitchResult = int.MinValue;
        private static int _rateResult = int.MinValue;

        /// <summary>Seconds between init attempts, so a device without an engine is not hammered.</summary>
        public const float InitRetrySeconds = 6f;

        /// <summary>True when there is a text-to-speech engine to use at all.</summary>
        public static bool Available => Application.platform == RuntimePlatform.Android;

        /// <summary>True once the engine has come up and can be spoken to.</summary>
        public static bool Ready => _ready;

        /// <summary>The last thing that went wrong, for the settings panel.</summary>
        public static string LastError => _lastError;

        /// <summary>The engine's initialisation status, as reported by Android.</summary>
        public static int InitStatus => _initStatus;

        /// <summary>What <c>setLanguage(Locale.CHINA)</c> answered; negative means "no Chinese voice".</summary>
        public static int LanguageStatus => _languageStatus;

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

        /// <summary>Whether the player has already been pointed at the switch.</summary>
        public static bool HintShown
        {
            get => PlayerPrefs.GetInt(HintedKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(HintedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Forgets cached state, so the next read comes from disk and the engine retries.</summary>
        public static void ResetCache()
        {
            _enabledLoaded = false;
            _initTried = false;
            _ready = false;
            _lastAttemptAt = -999f;
            _lastError = "";
            _initStatus = int.MinValue;
            _languageStatus = int.MinValue;
        }

        /// <summary>
        /// Says one line, if the player has switched speech on. Safe to call from anywhere.
        ///
        /// Stage directions are dropped: the pet's transcript is full of "（开心地晃了晃）", and a
        /// synthetic voice reading parentheses aloud is exactly the thing that makes a talking
        /// pet embarrassing rather than charming. A line that is nothing but a stage direction
        /// produces no speech at all, which is why poking the pet chirps instead of narrating.
        /// </summary>
        public static bool Speak(string text, float pitch = 1f, float rate = 1f)
            => Enabled && Say(text, pitch, rate);

        /// <summary>
        /// Speaks regardless of the switch, for the settings panel's test button.
        ///
        /// The switch is a preference; the test is a diagnostic. A player who hears nothing needs
        /// one button that answers "is it my phone or is it my settings".
        /// </summary>
        public static bool Test(float pitch = 1f, float rate = 1f) => Say(TestLine, pitch, rate);

        private static bool Say(string text, float pitch, float rate)
        {
            if (!Available) return false;

            string line = Speech(text);
            if (string.IsNullOrEmpty(line)) return false;

            if (!EnsureEngine()) return false;

            try
            {
                // Both of these RETURN AN INT (SUCCESS/ERROR), they are not void — and Unity's
                // AndroidJavaObject.Call resolves an overload by the JNI signature it derives
                // from the arguments, so Call("setPitch", 1f) looks for "(F)V" and throws
                // NoSuchMethodError on a device while working nowhere at all. The report from the
                // phone was exactly that: "no non-static method with name='setPitch'
                // signature='(F)V'". Everything on this object is interrogated with Call<int>.
                _pitchResult = _engine.Call<int>("setPitch", Mathf.Clamp(pitch, 0.5f, 2f));
                _rateResult = _engine.Call<int>("setSpeechRate", Mathf.Clamp(rate, 0.5f, 2f));

                // QUEUE_FLUSH: the newest line wins. A pet that queues four replies and reads them
                // out one after another long after the conversation moved on is worse than one
                // that interrupts itself.
                int result = _engine.Call<int>("speak", line, 1, _bundle, "dshpet");

                // speak() answers SUCCESS(0) or ERROR(-1). A zero means the engine accepted the
                // text; it is still possible to hear nothing, and that is what the status line in
                // the settings panel is for.
                _lastError = result == 0 ? "" : "speak() 返回 " + result;
                return result == 0;
            }
            catch (System.Exception e)
            {
                _lastError = e.Message;
                Debug.LogWarning("[DshMobile] TTS speak failed: " + e.Message);
                return false;
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
        }

        /// <summary>
        /// A sentence for the settings panel: what the engine is doing, in words.
        ///
        /// Pure, so the mapping from Android's status codes to "what do I tell the player" is a
        /// test rather than something only a phone can check.
        /// </summary>
        public static string StatusText(bool enabled, bool ready, bool available,
            int initStatus, int languageStatus, string error)
        {
            if (!available) return "这台设备没有系统语音（语音输出只在安卓上可用）。";

            if (!ready)
            {
                if (initStatus == -1) return "语音引擎初始化失败：这台手机可能没有装语音服务。";
                if (initStatus != int.MinValue) return $"语音引擎初始化失败（状态 {initStatus}）。";
                if (!string.IsNullOrEmpty(error)) return "语音引擎起不来：" + error;
                return "语音引擎准备中……";
            }

            if (languageStatus < 0)
            {
                // Still worth trying: the engine will read Chinese with whatever voice it has, and
                // refusing to speak at all would be a worse answer than "it sounds wrong".
                return "引擎能起来，但这台手机没有中文语音包（语言状态 " + languageStatus +
                       "）：点下面的「打开系统语音设置」装一个中文语音，或者先凑合听。";
            }

            if (!enabled) return "语音引擎已就绪：打开开关后宠物就会朗读它说的话。";
            return "语音引擎已就绪，会用这只宠物的音色朗读。";
        }

        /// <summary>Short status text for the HUD, without the lecture.</summary>
        public static string ShortStatus()
        {
            if (!Available) return "不可用";
            if (!_ready) return "未就绪";
            return _languageStatus < 0 ? "缺中文语音包" : "就绪";
        }

        /// <summary>Which engine Android picked, for the settings panel.</summary>
        public static string EngineName => _engineName;

        /// <summary>What setPitch/setSpeechRate answered, for the settings panel.</summary>
        public static string TuningStatus
        {
            get
            {
                if (_pitchResult == int.MinValue && _rateResult == int.MinValue) return "";
                return $"音调 {_pitchResult} / 语速 {_rateResult}";
            }
        }

        /// <summary>
        /// Opens the system's text-to-speech settings, so "install a Chinese voice" is one tap
        /// rather than a scavenger hunt through the settings app.
        ///
        /// Returns false when there is nothing to open — the caller says so rather than leaving
        /// the player staring at an unchanged screen.
        /// </summary>
        public static bool OpenSystemSettings()
        {
            if (!Available) return false;

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent",
                           intentClass.GetStatic<string>("ACTION_TTS_SETTINGS")))
                {
                    activity.Call("startActivity", intent);
                }
                return true;
            }
            catch (System.Exception e)
            {
                _lastError = e.Message;
                Debug.LogWarning("[DshMobile] Could not open the TTS settings: " + e.Message);
                return false;
            }
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

        /// <summary>
        /// Starts the engine if it is not up yet.
        ///
        /// Retried rather than latched: the first version marked a failed attempt as permanent,
        /// so a device that was merely slow — or that had no engine for one moment during startup
        /// — stayed silent for the rest of the session. A failure now costs one attempt every
        /// <see cref="InitRetrySeconds"/> seconds.
        /// </summary>
        private static bool EnsureEngine()
        {
            if (_ready) return true;
            if (_initTried && Time.realtimeSinceStartup - _lastAttemptAt < InitRetrySeconds) return false;

            _initTried = true;
            _lastAttemptAt = Time.realtimeSinceStartup;

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var china = locale.GetStatic<AndroidJavaObject>("CHINA"))
                {
                    if (_engine == null)
                    {
                        _listener = new InitListener();
                        _engine = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, _listener);
                        _bundle = new AndroidJavaObject("android.os.Bundle");

                        // Which engine the phone actually chose, so a player looking at a silent
                        // pet can tell "no voice installed" from "the wrong engine is default".
                        try { _engineName = _engine.Call<string>("getDefaultEngine") ?? ""; }
                        catch { _engineName = ""; }
                    }

                    // A phone with an English-only engine answers LANG_MISSING_DATA(-1) or
                    // LANG_NOT_SUPPORTED(-2) here and then plays silence for Chinese text, so the
                    // answer is checked and reported rather than assumed.
                    _languageStatus = _engine.Call<int>("setLanguage", china);
                    if (_languageStatus < 0)
                    {
                        // Fall back to whatever the engine does have: it will pronounce Chinese
                        // badly, which is still better than silence, and the settings line tells
                        // the player what to install to fix it properly. Note the fallback is not
                        // recorded as the status — the player needs to know their Chinese voice is
                        // missing, not that the engine accepted American English.
                        using (var fallback = locale.CallStatic<AndroidJavaObject>("getDefault"))
                        using (var us = locale.GetStatic<AndroidJavaObject>("US"))
                        {
                            if (_engine.Call<int>("isLanguageAvailable", fallback) >= 0)
                            {
                                _engine.Call<int>("setLanguage", fallback);
                            }
                            else
                            {
                                _engine.Call<int>("setLanguage", us);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                _lastError = e.Message;
                Debug.LogWarning("[DshMobile] TTS unavailable: " + e.Message);
                return false;
            }

            // Initialisation is asynchronous: onInit arrives a few hundred milliseconds later.
            // The first line or two may therefore be silent, which is why the engine is also
            // warmed up when the switch is turned on rather than on the first reply.
            return _ready;
        }

        /// <summary>Warms the engine up, so the first reply is not swallowed by initialisation.</summary>
        public static void WarmUp()
        {
            if (!Available) return;
            EnsureEngine();
        }

        /// <summary>The Android OnInitListener, bridged into managed code.</summary>
        private class InitListener : AndroidJavaProxy
        {
            public InitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }

            // ReSharper disable once InconsistentNaming — the JNI name is what matters.
            public void onInit(int status)
            {
                _initStatus = status;
                _ready = status == 0;   // 0 == TextToSpeech.SUCCESS
                if (!_ready) _lastError = "引擎初始化状态 " + status;
            }
        }
    }
}
