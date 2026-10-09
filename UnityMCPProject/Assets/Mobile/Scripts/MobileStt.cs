using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Talking to the pet instead of typing.
    ///
    /// Android's own <c>SpeechRecognizer</c>, for the same reason the pet's replies use the
    /// platform's text-to-speech: it is already installed, it already knows Chinese, and it costs
    /// nothing. No model is shipped, no key is needed, and nothing leaves the device that the
    /// player did not just say out loud.
    ///
    /// Three platform traps, all of them the same shape as the text-to-speech ones:
    ///  - <b>Android 11 package visibility:</b> without a <c>&lt;queries&gt;</c> entry for
    ///    <c>android.speech.RecognitionService</c> the recogniser is invisible and
    ///    <c>createSpeechRecognizer</c> returns something that never calls back;
    ///  - <b>the microphone is a runtime permission</b>, not a manifest one: it has to be asked
    ///    for while the game is running, and the answer can be "no";
    ///  - <b>every method on the listener is required.</b> The Java interface has ten callbacks and
    ///    a proxy that implements only the interesting ones throws on the first event it misses.
    ///
    /// So this class is deliberately chatty: it reports whether the permission was granted,
    /// whether a recogniser exists, and what the engine said when it failed. The settings panel
    /// shows that, because "I pressed the microphone and nothing happened" is not an answer.
    /// </summary>
    public static class MobileStt
    {
        public const string EnabledKey = "dshpet.stt";
        public const string Permission = "android.permission.RECORD_AUDIO";

        private static bool _enabledLoaded;
        private static bool _enabled;

        private static AndroidJavaObject _recognizer;
        private static AndroidJavaObject _intent;
        private static Listener _listener;

        private static bool _listening;
        private static string _heard = "";
        private static string _error = "";
        private static int _errorCode = int.MinValue;
        private static float _listeningSince;

        /// <summary>Give up on a listen that never produced a result.</summary>
        public const float TimeoutSeconds = 12f;

        public static bool Available => Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// Editor switch: pretend a recogniser exists, so the microphone button can be laid out and
        /// looked at without a phone.
        ///
        /// The same trick as <see cref="MobileUi.ForceTouchControls"/>, and for the same reason: a
        /// control that only exists on a device is a control nobody checks until a player reports
        /// that it is missing — which is what happened to this one, drawn outside the panel and
        /// never on screen at all.
        /// </summary>
        public static bool ForceAvailable;

        /// <summary>True when the microphone button should be offered.</summary>
        public static bool Offered => (Available || ForceAvailable) && Enabled;

        /// <summary>True when there is a recogniser behind the button right now.</summary>
        public static bool AvailableNow => Available || ForceAvailable;

        public static bool Listening => _listening;

        /// <summary>The last thing the recogniser understood. Empty until something is heard.</summary>
        public static string Heard => _heard;

        public static string LastError => _error;
        public static int LastErrorCode => _errorCode;

        /// <summary>True once a result has arrived that the UI has not consumed yet.</summary>
        public static bool HasResult { get; private set; }

        /// <summary>
        /// Whether the microphone button is offered at all.
        ///
        /// Defaults to ON where a recogniser exists: unlike the pet talking (which is a surprise
        /// the player should opt into), a microphone button does nothing until it is pressed.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (!_enabledLoaded)
                {
                    _enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;
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

        public static void ResetCache() => _enabledLoaded = false;

        /// <summary>Whether the app has the microphone permission right now.</summary>
        public static bool HasPermission
        {
            get
            {
                if (!Available) return false;
                try { return UnityEngine.Android.Permission.HasUserAuthorizedPermission(Permission); }
                catch { return false; }
            }
        }

        /// <summary>
        /// Asks for the microphone. The answer arrives asynchronously — the platform shows its own
        /// dialog — so the caller has to be able to carry on without it.
        /// </summary>
        public static void RequestPermission()
        {
            if (!Available) return;
            try
            {
                UnityEngine.Android.Permission.RequestUserPermission(Permission);
            }
            catch (System.Exception e)
            {
                _error = e.Message;
            }
        }

        /// <summary>
        /// Starts listening. Returns false — with a reason in <see cref="LastError"/> — when there
        /// is no recogniser or no permission, rather than pretending to listen.
        /// </summary>
        public static bool StartListening()
        {
            if (!Available)
            {
                _error = "语音输入只在安卓上可用";
                return false;
            }

            if (!HasPermission)
            {
                RequestPermission();
                _error = "需要麦克风权限：同意之后再点一次麦克风";
                return false;
            }

            if (!EnsureRecognizer())
            {
                return false;
            }

            try
            {
                _heard = "";
                _error = "";
                _errorCode = int.MinValue;
                HasResult = false;
                _listening = true;
                _listeningSince = Time.realtimeSinceStartup;
                _recognizer.Call("startListening", _intent);
                return true;
            }
            catch (System.Exception e)
            {
                _listening = false;
                _error = e.Message;
                Debug.LogWarning("[DshMobile] Speech recogniser failed to start: " + e.Message);
                return false;
            }
        }

        public static void StopListening()
        {
            if (_recognizer == null) return;
            try { _recognizer.Call("stopListening"); }
            catch { /* it is going away anyway */ }
            _listening = false;
        }

        public static void Cancel()
        {
            if (_recognizer == null) return;
            try { _recognizer.Call("cancel"); }
            catch { /* nothing useful to do */ }
            _listening = false;
        }

        public static void Shutdown()
        {
            Cancel();
            if (_recognizer != null)
            {
                try { _recognizer.Call("destroy"); }
                catch { /* nothing useful to do */ }
                _recognizer.Dispose();
                _recognizer = null;
            }

            if (_intent != null)
            {
                _intent.Dispose();
                _intent = null;
            }

            _listener = null;
            _listening = false;
        }

        /// <summary>Call once a frame: gives up on a listen that produced nothing.</summary>
        public static void Tick()
        {
            if (!_listening) return;
            if (Time.realtimeSinceStartup - _listeningSince < TimeoutSeconds) return;

            Cancel();
            _error = "没听到声音（超时）";
        }

        /// <summary>Consumes the result, so the UI can fill the input box exactly once.</summary>
        public static string TakeResult()
        {
            if (!HasResult) return "";
            HasResult = false;
            return _heard;
        }

        /// <summary>
        /// A sentence for the settings panel: what the recogniser is doing, in words. Pure, so the
        /// mapping from Android's error codes to "what do I tell the player" is a test.
        /// </summary>
        public static string StatusText(bool enabled, bool available, bool permission,
            bool listening, int errorCode, string error)
        {
            if (!available) return "语音输入只在安卓上可用（需要手机的语音识别）。";
            if (!enabled) return "语音输入已关闭。打开后聊天框旁边会出现麦克风按钮。";
            if (!permission) return "还没有麦克风权限：点麦克风按钮，同意系统弹窗即可。";
            if (listening) return "正在听……说完会自动停下。";
            if (errorCode == 6) return "没听清（说了太久或太安静），再说一次就行。";
            if (errorCode == 7) return "没听到声音，再试一次。";
            if (errorCode == 9) return "这台手机没有麦克风权限或没有识别服务，到系统设置里检查一下。";
            if (errorCode > 0) return $"识别失败（错误码 {errorCode}）。";
            if (!string.IsNullOrEmpty(error)) return error;
            return "语音输入就绪：点麦克风说话，识别到的文字会填进输入框。";
        }

        private static bool EnsureRecognizer()
        {
            if (_recognizer != null) return true;

            try
            {
                using (var recognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
                {
                    if (!recognizerClass.CallStatic<bool>("isRecognitionAvailable",
                            CurrentActivity()))
                    {
                        _error = "这台手机没有语音识别服务（系统里可能没装）";
                        return false;
                    }

                    _listener = new Listener();
                    _recognizer = recognizerClass.CallStatic<AndroidJavaObject>(
                        "createSpeechRecognizer", CurrentActivity());
                    _recognizer.Call("setRecognitionListener", _listener);
                }

                using (var intentClass = new AndroidJavaClass("android.speech.RecognizerIntent"))
                {
                    string action = intentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
                    _intent = new AndroidJavaObject("android.content.Intent", action);

                    _intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"),
                        intentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));
                    _intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_LANGUAGE"), "zh-CN");

                    // Partial results make the wait feel shorter, and they are all the input the
                    // player needs to see before they stop talking.
                    _intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), true);
                }

                return true;
            }
            catch (System.Exception e)
            {
                _error = e.Message;
                Debug.LogWarning("[DshMobile] No speech recogniser: " + e.Message);
                return false;
            }
        }

        private static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        /// <summary>
        /// The Android RecognitionListener, bridged into managed code.
        ///
        /// Every one of the ten callbacks has to exist, even the ones that do nothing: the proxy
        /// looks the method up by name when Java calls it, and a missing one throws inside the
        /// platform's own thread — where the only symptom is that results never arrive.
        /// </summary>
        private class Listener : AndroidJavaProxy
        {
            public Listener() : base("android.speech.RecognitionListener") { }

            public void onReadyForSpeech(AndroidJavaObject parameters)
            {
                _listening = true;
            }

            public void onBeginningOfSpeech() { }

            public void onRmsChanged(float rms) { }

            public void onBufferReceived(byte[] buffer) { }

            public void onEndOfSpeech() { }

            public void onError(int error)
            {
                _listening = false;
                _errorCode = error;
                _error = "识别失败（错误码 " + error + "）";
            }

            public void onResults(AndroidJavaObject results)
            {
                _listening = false;
                string text = FirstResult(results);
                if (string.IsNullOrEmpty(text))
                {
                    _error = "没听清";
                    return;
                }

                _heard = text;
                HasResult = true;
            }

            public void onPartialResults(AndroidJavaObject partialResults)
            {
                // Shown live while the player is still talking, so the wait has feedback.
                string text = FirstResult(partialResults);
                if (!string.IsNullOrEmpty(text)) _heard = text;
            }

            public void onEvent(int eventType, AndroidJavaObject parameters) { }
        }

        /// <summary>Reads the first string out of a recognition Bundle.</summary>
        private static string FirstResult(AndroidJavaObject bundle)
        {
            if (bundle == null) return "";

            try
            {
                using (var list = bundle.Call<AndroidJavaObject>("getStringArrayList",
                           "results_recognition"))
                {
                    if (list == null) return "";
                    int size = list.Call<int>("size");
                    if (size <= 0) return "";
                    return list.Call<string>("get", 0) ?? "";
                }
            }
            catch (System.Exception e)
            {
                _error = e.Message;
                return "";
            }
        }
    }
}
