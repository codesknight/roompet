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

        /// <summary>
        /// Set when the player asked to speak but the permission dialog was still in the way.
        ///
        /// The first version told them to "press the microphone again after agreeing", which is a
        /// sentence nobody reads and, on a phone, a button they then press twice with nothing
        /// happening the first time. The platform's dialog is asynchronous, so the honest thing is
        /// to remember the request and start listening the moment the permission lands.
        /// </summary>
        private static bool _pendingStart;
        private static float _pendingSince;

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

        /// <summary>True when a listen is queued behind the permission dialog.</summary>
        public static bool PendingPermission => _pendingStart;

        // ------------------------------------------------------------------ diagnostics
        //
        // "I pressed the microphone and nothing happened" is not a bug report, it is the absence of
        // one — and on a device there is no console to read. So every step of the attempt is
        // recorded as it happens, and the settings panel prints it. The values are deliberately
        // plain facts (did the engine ever say it was ready?) rather than a guess at the cause.

        /// <summary>Attempts made for the current press, including the automatic retry.</summary>
        private static int _attemptsThisPress;

        /// <summary>
        /// Whether the current attempt carries an explicit <c>EXTRA_LANGUAGE</c>.
        ///
        /// Some Chinese ROMs accept a recogniser intent with a locale and then answer with
        /// ERROR_CLIENT without ever becoming ready; the same ROM works when the engine is left to
        /// pick its own language. Rather than guess which kind of phone this is, the first failure
        /// to reach "ready" flips this and tries the other shape once.
        /// </summary>
        private static bool _useLanguageExtra = true;

        /// <summary>Set by a failed attempt; <see cref="Tick"/> runs it on the next frame.</summary>
        private static bool _retryPending;

        /// <summary>Whether the engine ever reported that it was ready for speech.</summary>
        private static bool _sawReady;

        /// <summary>Whether the last attempt got as far as calling startListening on the engine.</summary>
        private static bool _startCalled;

        /// <summary>Destroy the recogniser before the next attempt (a broken one never recovers).</summary>
        private static bool _needsRebuild;

        /// <summary>How many times the recogniser object has been built, for the diagnostics line.</summary>
        private static int _builds;

        /// <summary>Whether the engine said it was ready for speech on the last attempt.</summary>
        public static bool SawReady => _sawReady;

        /// <summary>Whether the last attempt reached the engine at all.</summary>
        public static bool StartCalled => _startCalled;

        /// <summary>Whether an automatic retry is queued.</summary>
        public static bool RetryQueued => _retryPending;

        /// <summary>
        /// The facts of the last attempt, one per line, for the settings panel.
        ///
        /// Kept as plain text rather than a status word because the useful question is not "does it
        /// work" but "which step did not happen" — and a player reading four lines off a phone
        /// screen can answer that where a log cannot.
        /// </summary>
        public static string Diagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("· 平台：").Append(Available ? "安卓" : "非安卓（编辑器/桌面，识别不可用）").Append('\n');
            sb.Append("· 麦克风权限：").Append(HasPermission ? "已授权" : "未授权").Append('\n');
            sb.Append("· 识别器：").Append(_recognizer == null ? "未创建" : "已创建").Append("（第 ")
              .Append(_builds).Append(" 次）").Append('\n');
            sb.Append("· 上次：startListening ").Append(_startCalled ? "已调用" : "未调用")
              .Append("；引擎就绪 ").Append(_sawReady ? "是" : "否");
            if (_errorCode != int.MinValue) sb.Append("；错误码 ").Append(_errorCode);
            sb.Append('\n');
            sb.Append("· 语言参数：").Append(_useLanguageExtra ? "zh-CN" : "交给系统");
            if (_retryPending) sb.Append("（正在自动重试）");
            sb.Append('\n');
            sb.Append("· 调用线程：").Append(OnMainThread ? "主线程" : "非主线程")
              .Append("；帮它排到主线程的次数 ").Append(MarshalledCalls).Append('\n');
            sb.Append("· 上次结果：").Append(string.IsNullOrEmpty(_heard) ? "（无）" : _heard);
            return sb.ToString();
        }

        /// <summary>Throws away the recogniser so the next attempt builds a fresh one.</summary>
        public static void ResetRecognizer()
        {
            DestroyRecognizer();
            _needsRebuild = false;
            _retryPending = false;
            _attemptsThisPress = 0;
            _sawReady = false;
            _startCalled = false;
            _error = "";
            _errorCode = int.MinValue;
        }

        /// <summary>
        /// Starts listening. Returns false — with a reason in <see cref="LastError"/> — when there
        /// is no recogniser or no permission, rather than pretending to listen.
        ///
        /// The no-permission case is not a dead end: the request is remembered and
        /// <see cref="Tick"/> starts listening as soon as the platform reports the permission, so
        /// the player's one press is honoured instead of being thrown away.
        /// </summary>
        public static bool StartListening()
        {
            _attemptsThisPress = 0;
            _retryPending = false;
            return StartAttempt();
        }

        /// <summary>One attempt. Separate from <see cref="StartListening"/> so a retry does not reset the count.</summary>
        private static bool StartAttempt()
        {
            if (!Available)
            {
                _error = "语音输入只在安卓上可用";
                return false;
            }

            if (!HasPermission)
            {
                RequestPermission();
                _pendingStart = true;
                _pendingSince = Time.realtimeSinceStartup;
                _error = "正在申请麦克风权限：同意之后会自动开始听。";
                return false;
            }

            _pendingStart = false;

            if (_needsRebuild)
            {
                // A recogniser that has already failed can be a recogniser that fails forever: the
                // platform object caches its broken state, and every later startListening is
                // silently dropped. Throwing it away is the whole fix for "it worked the first time
                // and never again".
                DestroyRecognizer();
                _needsRebuild = false;
            }

            if (!EnsureRecognizer())
            {
                _needsRebuild = true;
                return false;
            }

            _attemptsThisPress++;
            _heard = "";
            _error = "";
            _errorCode = int.MinValue;
            _sawReady = false;
            HasResult = false;
            _listening = true;
            _listeningSince = Time.realtimeSinceStartup;
            _startCalled = true;

            // The call into the platform goes through the main thread — see RunOnUiThread. The
            // result of a *posted* call cannot be read here, so the managed errors it produces are
            // recorded into _error by the body itself.
            RunOnUiThread("startListening", () =>
            {
                try
                {
                    // cancel() before start(): after an error some engines are still tearing the last
                    // session down, and a start that arrives mid-teardown is ignored.
                    try { _recognizer.Call("cancel"); } catch { /* nothing to cancel */ }
                    _recognizer.Call("startListening", _intent);
                }
                catch (System.Exception e)
                {
                    _listening = false;
                    _startCalled = false;
                    _needsRebuild = true;
                    _error = "startListening 失败：" + e.Message;
                    Debug.LogWarning("[DshMobile] speech start failed: " + e.Message);
                }
            });

            return true;
        }

        private static void DestroyRecognizer()
        {
            var recognizer = _recognizer;
            var intent = _intent;
            _recognizer = null;
            _intent = null;
            _listener = null;
            _listening = false;

            if (recognizer == null && intent == null) return;

            RunOnUiThread("destroy", () =>
            {
                try
                {
                    if (recognizer != null)
                    {
                        recognizer.Call("destroy");
                        recognizer.Dispose();
                    }

                    if (intent != null) intent.Dispose();
                }
                catch (System.Exception e)
                {
                    Debug.Log("[DshMobile] recogniser teardown skipped: " + e.Message);
                }
            });
        }

        public static void StopListening()
        {
            if (_recognizer == null) return;
            _listening = false;

            var recognizer = _recognizer;
            RunOnUiThread("stopListening", () =>
            {
                try { recognizer.Call("stopListening"); }
                catch { /* it is going away anyway */ }
            });
        }

        public static void Cancel()
        {
            if (_recognizer == null) return;
            _listening = false;

            var recognizer = _recognizer;
            RunOnUiThread("cancel", () =>
            {
                try { recognizer.Call("cancel"); }
                catch { /* nothing useful to do */ }
            });
        }

        public static void Shutdown()
        {
            Cancel();
            DestroyRecognizer();
            _pendingStart = false;
            _retryPending = false;
        }

        /// <summary>Call once a frame: starts the queued listen, and gives up on a silent one.</summary>
        public static void Tick()
        {
            if (_pendingStart)
            {
                if (HasPermission)
                {
                    // The dialog has been answered with a yes. Start now, which is what the player
                    // asked for one press ago.
                    _pendingStart = false;
                    StartAttempt();
                }
                else if (Time.realtimeSinceStartup - _pendingSince > PendingPermissionSeconds)
                {
                    // Refused (or dismissed and never granted): stop pretending something is coming.
                    _pendingStart = false;
                    _error = "没有麦克风权限：到系统设置里允许「录音」，再点麦克风。";
                    return;
                }
            }

            // The automatic retry: the first attempt failed before the engine was ever ready, so
            // try the other intent shape once. One retry, not a loop — a recogniser that fails
            // twice is a recogniser that does not work on this phone, and hammering it would turn a
            // clear message into a mystery.
            if (_retryPending && !_listening && !_pendingStart)
            {
                _retryPending = false;
                StartAttempt();
            }

            if (!_listening) return;
            if (Time.realtimeSinceStartup - _listeningSince < TimeoutSeconds) return;

            Cancel();
            _error = "没听到声音（超时）";
        }

        /// <summary>How long a queued listen waits for the permission dialog before giving up.</summary>
        public const float PendingPermissionSeconds = 25f;

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
            if (permission && _pendingStart) return "正在申请麦克风权限：同意之后会自动开始听。";
            if (!permission) return "还没有麦克风权限：点麦克风按钮，同意系统弹窗即可。";
            if (listening) return "正在听……说完会自动停下。";
            if (errorCode == 5) return "识别服务拒绝了这次请求（错误码 5）。已经在自动换个方式重试。" +
                                        "一直这样的话，把下面这段「语音诊断」发我看看。";
            if (errorCode == 6) return "没听清（说了太久或太安静），再说一次就行。";
            if (errorCode == 7) return "没听到声音，再试一次。";
            if (errorCode == 8) return "识别服务正忙（错误码 8），等一下再点一次麦克风。";
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

                    // Creation and listener registration are the two calls the platform is strictest
                    // about: a recogniser built off the main thread binds its internal Handler to the
                    // wrong Looper and then throws on *every* later call. Marshalled like the rest.
                    var created = false;
                    RunOnUiThread("createSpeechRecognizer", () =>
                    {
                        try
                        {
                            _listener = new Listener();
                            _recognizer = recognizerClass.CallStatic<AndroidJavaObject>(
                                "createSpeechRecognizer", CurrentActivity());
                            _recognizer.Call("setRecognitionListener", _listener);
                            created = _recognizer != null;
                        }
                        catch (System.Exception e)
                        {
                            _error = "创建识别器失败：" + e.Message;
                            Debug.LogWarning("[DshMobile] " + _error);
                        }
                    });

                    if (!created) return false;
                }

                using (var intentClass = new AndroidJavaClass("android.speech.RecognizerIntent"))
                {
                    string action = intentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
                    _intent = new AndroidJavaObject("android.content.Intent", action);

                    _intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"),
                        intentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));

                    // The locale is a *try*, not a fact: asking for zh-CN explicitly is what makes a
                    // Chinese phone recognise Chinese rather than answering in English, and it is
                    // also what some ROMs choke on (they answer ERROR_CLIENT and never become
                    // ready). So it goes on the first attempt and comes off the retry — one of the
                    // two shapes is what this device wants, and the diagnostics line says which.
                    if (_useLanguageExtra)
                    {
                        _intent.Call<AndroidJavaObject>("putExtra",
                            intentClass.GetStatic<string>("EXTRA_LANGUAGE"), "zh-CN");
                    }

                    // Partial results make the wait feel shorter, and they are all the input the
                    // player needs to see before they stop talking.
                    _intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), true);

                    // Several recognisers — the Chinese ROMs especially — ignore an intent that does
                    // not name the package that is asking, and answer with ERROR_CLIENT instead of
                    // listening. It costs one extra and turns "the engine is broken" into "the
                    // engine was asked properly", which is the whole difference on a Xiaomi.
                    try
                    {
                        using (var activity = CurrentActivity())
                        {
                            string package = activity.Call<string>("getPackageName");
                            if (!string.IsNullOrEmpty(package))
                            {
                                _intent.Call<AndroidJavaObject>("putExtra",
                                    intentClass.GetStatic<string>("EXTRA_CALLING_PACKAGE"), package);
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        // Not fatal: the older recognisers do not need it at all.
                        Debug.Log("[DshMobile] calling_package extra skipped: " + e.Message);
                    }
                }

                _builds++;
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

        // ------------------------------------------------------- the main-thread promise

        /// <summary>
        /// Whether the current call is on the thread Android considers the main one.
        ///
        /// On Android, Unity's main thread <i>is</i> the UI thread and its managed thread id is 1. A
        /// call from anywhere else may still work until it reaches a platform class that checks — and
        /// <c>SpeechRecognizer</c> checks on every entry point, throwing
        /// <c>RuntimeException: Speech Recognizer should be used only from the application's main
        /// thread</c>. The player experiences that as "the microphone does nothing", so nothing here
        /// is left to chance: every call into the recogniser is marshalled explicitly.
        /// </summary>
        public static bool OnMainThread => System.Threading.Thread.CurrentThread.ManagedThreadId == 1;

        /// <summary>How many calls had to be marshalled onto the UI thread, for the diagnostics.</summary>
        public static int MarshalledCalls { get; private set; }

        /// <summary>
        /// Runs <paramref name="action"/> on the Android UI thread.
        ///
        /// If we are already on it — the normal case, since OnGUI and Update both are — Android's
        /// <c>runOnUiThread</c> runs the action immediately, so this costs one JNI call and changes
        /// nothing else. If we are not, the action is *posted* rather than thrown, which is the whole
        /// point: the platform requires the main thread, it does not require synchrony.
        ///
        /// The action must handle its own exceptions: one thrown inside a posted Runnable surfaces in
        /// Java, where the managed <c>try</c> around the caller cannot see it.
        /// </summary>
        private static void RunOnUiThread(string what, System.Action action)
        {
            if (action == null) return;

            try
            {
                using (var activity = CurrentActivity())
                {
                    if (activity == null)
                    {
                        action();
                        return;
                    }

                    if (!OnMainThread) MarshalledCalls++;
                    var runnable = new AndroidJavaRunnable(action);
                    activity.Call("runOnUiThread", runnable);
                }
            }
            catch (System.Exception e)
            {
                _error = "把语音请求排到主线程时失败：" + e.Message + "（" + what + "）";
                Debug.LogWarning("[DshMobile] " + _error);
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
                _sawReady = true;
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

                // The engine never even became ready: this is the shape of failure that means "this
                // phone did not like the question", not "the phone did not hear you". Try the other
                // question once — with the locale extra taken off, or put back on — and rebuild the
                // engine first, because a recogniser that has errored can keep failing forever.
                if (!_sawReady && _attemptsThisPress < 2)
                {
                    _useLanguageExtra = !_useLanguageExtra;
                    _needsRebuild = true;
                    _retryPending = true;
                    return;
                }

                // 5 ERROR_CLIENT and 8 ERROR_RECOGNIZER_BUSY both leave the object in a state where
                // the next press would otherwise be dropped on the floor.
                if (error == 5 || error == 8) _needsRebuild = true;
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
