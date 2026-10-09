using System.Collections.Generic;
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

        /// <summary>Attempts made for the current press, including the automatic retries.</summary>
        private static int _attemptsThisPress;

        /// <summary>
        /// How many times one press may try.
        ///
        /// Three, because there are three shapes of the question to ask: with the locale, without it,
        /// and bare. Each one is a *different request*, and a recogniser that refuses the first is
        /// saying something about the request rather than about the phone.
        /// </summary>
        public const int AttemptsPerPress = 3;

        /// <summary>The last shape that is only "recognise speech" — everything else stripped out.</summary>
        public const int MinimalVariant = 2;

        /// <summary>Which shape of the intent an attempt index uses. Pure, so the policy is a test.</summary>
        public static int VariantFor(int attemptIndex)
            => Mathf.Clamp(attemptIndex, 0, MinimalVariant);

        /// <summary>What the diagnostics call that shape.</summary>
        public static string VariantName(int variant)
        {
            switch (variant)
            {
                case 0: return "中文 + 部分结果";
                case 1: return "交给系统（无语言）";
                default: return "最简 + 优先离线";
            }
        }

        /// <summary>
        /// Whether this attempt is the last one this press will make.
        ///
        /// The retry only happens when the engine never became ready, so "final" means "we have asked
        /// three different ways and it refused all of them" — which is worth telling the player about.
        /// </summary>
        public static bool IsFinalAttempt(int attemptsMade, bool sawReady)
            => sawReady || attemptsMade >= AttemptsPerPress;

        /// <summary>The shape the current attempt is using.</summary>
        private static int _variant;

        /// <summary>How the recogniser was finally built, for the diagnostics (empty until one works).</summary>
        private static string _createPath = "";

        /// <summary>The last thing that went wrong while building one, kept until one works.</summary>
        private static string _createError = "";

        /// <summary>
        /// The last failure that has not been cleared by a working recogniser.
        ///
        /// Kept apart from <see cref="_error"/>, which every attempt resets: this is the "has this
        /// feature ever worked on this phone" answer, and the settings panel shows it.
        /// </summary>
        private static string _lastFailure = "";

        /// <summary>Which press the last reported error belonged to.</summary>
        private static int _reportedPress = -1;

        /// <summary>Press counter, so one press can be told from the next.</summary>
        private static int _pressId;

        private static float _lastReportAt = -999f;

        /// <summary>
        /// How long between two voice notes in the conversation.
        ///
        /// A phone whose recogniser cannot be built used to write a line into the chat for *every*
        /// error code it produced — five lines for one press (5, 9, 11, 9, 11), each one a different
        /// sentence, so the "only report it once" rule never fired. The player asked for a diagnosis,
        /// not a transcript of the failure.
        /// </summary>
        public const float ReportCooldown = 5f;

        /// <summary>
        /// Whether a failure should be written into the conversation.
        ///
        /// Pure and shared with the UI test: a retry that is still queued must say nothing (its own
        /// outcome will be reported), one press gets at most one note, and two notes never arrive
        /// closer together than <see cref="ReportCooldown"/>.
        /// </summary>
        public static bool ShouldReport(bool retrying, bool alreadyReportedThisPress,
            float secondsSinceLastReport, float cooldown = ReportCooldown)
            => !retrying && !alreadyReportedThisPress && secondsSinceLastReport >= cooldown;

        /// <summary>
        /// Set by a failed attempt; <see cref="Tick"/> runs it on the next frame.
        /// </summary>
        private static bool _retryPending;

        /// <summary>Whether the engine ever reported that it was ready for speech.</summary>
        private static bool _sawReady;

        /// <summary>Whether the last attempt got as far as calling startListening on the engine.</summary>
        private static bool _startCalled;

        /// <summary>Destroy the recogniser before the next attempt (a broken one never recovers).</summary>
        private static bool _needsRebuild;

        /// <summary>Whether the platform reported a recognition service at the last attempt.</summary>
        private static bool _recognitionAvailable;

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
            sb.Append("· 系统识别服务：")
              .Append(_builds == 0 ? "还没问过" : (_recognitionAvailable ? "有" : "没有"))
              .Append('\n');
            sb.Append("· 识别器：").Append(HasRecognizer ? "已创建" : "未创建")
              .Append("（第 ").Append(_builds).Append(" 次尝试）");
            if (!string.IsNullOrEmpty(_createPath)) sb.Append("，方式：").Append(_createPath);
            sb.Append('\n');
            sb.Append("· 上次：startListening ").Append(_startCalled ? "已调用" : "未调用")
              .Append("；引擎就绪 ").Append(_sawReady ? "是" : "否");
            if (_errorCode != int.MinValue) sb.Append("；错误码 ").Append(_errorCode);
            sb.Append('\n');
            sb.Append("· 问法：").Append(VariantName(_variant))
              .Append("（本次按下已试 ").Append(_attemptsThisPress).Append('/').Append(AttemptsPerPress).Append(" 次）");
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
            _pressId++;                 // one press, one conversation note at most
            _variant = 0;
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

            _attemptsThisPress++;
            _variant = VariantFor(_attemptsThisPress - 1);
            _heard = "";
            _error = "";
            _errorCode = int.MinValue;
            _sawReady = false;
            HasResult = false;
            _listening = true;
            _listeningSince = Time.realtimeSinceStartup;
            _startCalled = true;

            // Everything that touches the recogniser happens in ONE block on the UI thread, in
            // order: build it if it does not exist yet, then start it.
            //
            // One block, because the calls are *posted* rather than run inline whenever the caller
            // is off the UI thread (which is what the platform's main-thread rule says happens on
            // this device). Two separate posts would still run in order, but each would have to
            // agree about state it cannot see yet — and a synchronous "did it work?" answer after a
            // post is simply wrong. One block, one owner, and the result lands in _error.
            RunOnUiThread("start", () =>
            {
                try
                {
                    if (!CreateIfNeeded())
                    {
                        _listening = false;
                        _startCalled = false;
                        _needsRebuild = true;
                        return;
                    }

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
                    _error = "开始识别失败：" + e.Message;
                    Debug.LogWarning("[DshMobile] " + _error);
                }
            });

            return true;
        }

        /// <summary>
        /// Builds the recogniser and its intent, on the calling thread (which is the UI thread —
        /// see <see cref="RunOnUiThread"/>). Returns false with <see cref="_error"/> set.
        ///
        /// Every JNI wrapper this needs is created and disposed *here*, inside the posted block.
        /// The first version captured an <c>AndroidJavaClass</c> from a <c>using</c> block in the
        /// caller, and because the block is posted rather than run inline on this device, the class
        /// it captured had already been disposed by the time it ran: the failure surfaced as
        /// "创建识别器失败：Object reference not set to an instance of an object", which says
        /// nothing about the actual mistake. Capturing a disposed handle is the Managed-to-Java
        /// version of using a pointer after free.
        ///
        /// <b>Three ways to get a recogniser, tried in order</b> — added after a phone reported
        /// "系统识别服务：有 / 识别器：未创建（第 51 次尝试）": <c>isRecognitionAvailable</c> answered yes
        /// while <c>createSpeechRecognizer(context)</c> answered null, fifty-one times in a row. The
        /// one-argument call binds whatever service the system *prefers*, and on a Chinese ROM that is
        /// often a disabled one — a Google service on a phone with no Google account, for instance. So:
        /// the default call, then the same call for every RecognitionService the package manager can
        /// actually see (by explicit component name), then the on-device recogniser where the platform
        /// has one. Whichever wins is recorded, because knowing *which* one works on a given phone is
        /// the difference between a fix and a guess.
        /// </summary>
        private static bool CreateIfNeeded()
        {
            if (HasRecognizer) return true;

            try
            {
                using (var activity = CurrentActivity())
                using (var recognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
                {
                    if (activity == null)
                    {
                        Fail("拿不到当前的 Activity，无法创建识别器。");
                        return false;
                    }

                    _recognitionAvailable = recognizerClass.CallStatic<bool>(
                        "isRecognitionAvailable", activity);
                    if (!_recognitionAvailable)
                    {
                        Fail("这台手机没有语音识别服务（系统里可能没装）。");
                        return false;
                    }

                    // 1. The platform's default choice.
                    if (TryCreate(recognizerClass, activity, "默认"))
                    {
                        return true;
                    }

                    // 2. Every recognition service it can actually see, named explicitly.
                    var services = RecognitionServices(activity);
                    for (int i = 0; i < services.Count; i++)
                    {
                        if (TryCreate(recognizerClass, activity, services[i].Package,
                                services[i].Package, services[i].Name))
                        {
                            return true;
                        }
                    }

                    // 3. The on-device (offline) recogniser, API 31 and up.
                    try
                    {
                        if (recognizerClass.CallStatic<bool>("isOnDeviceRecognitionAvailable", activity))
                        {
                            _listener = new Listener();
                            _builds++;
                            _recognizer = recognizerClass.CallStatic<AndroidJavaObject>(
                                "createOnDeviceSpeechRecognizer", activity);
                            if (HasRecognizer && FinishCreating("本机离线识别")) return true;
                            _recognizer = null;
                        }
                    }
                    catch (System.Exception e)
                    {
                        _createError = "离线识别不可用：" + e.Message;
                    }

                    // Nothing worked. The reason is kept rather than cleared, because "which step did
                    // not happen" is the only thing that can be acted on from a phone.
                    string seen = services.Count == 0
                        ? "一个都没找到"
                        : services.Count + " 个（" + services[0].Package + "…）";

                    Fail("系统不给识别器：找到服务 " + seen +
                         (string.IsNullOrEmpty(_createError) ? "" : "；" + _createError) +
                         "。到手机「设置 → 应用管理」里找到系统的语音识别服务（可能叫「语音服务」" +
                         "「Google」「小爱同学」等），确认它没有被停用或卸载，再回来点一次麦克风。");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Fail("创建识别器失败：" + e.Message);
                _recognizer = null;
                return false;
            }
        }

        /// <summary>One recognition service the package manager will admit to.</summary>
        private struct ServiceRef
        {
            public string Package;
            public string Name;
        }

        /// <summary>
        /// Every <c>RecognitionService</c> the package manager can see.
        ///
        /// <c>isRecognitionAvailable</c> answers "is there one at all", which is not the same question
        /// as "is the one it will use usable" — and the second is the one that was biting this device.
        /// </summary>
        private static List<ServiceRef> RecognitionServices(AndroidJavaObject activity)
        {
            var found = new List<ServiceRef>();

            try
            {
                using (var serviceClass = new AndroidJavaClass("android.speech.RecognitionService"))
                using (var intent = new AndroidJavaObject("android.content.Intent",
                           serviceClass.GetStatic<string>("SERVICE_INTERFACE")))
                using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var list = manager.Call<AndroidJavaObject>("queryIntentServices", intent, 0))
                {
                    if (list == null) return found;

                    int size = list.Call<int>("size");
                    for (int i = 0; i < size; i++)
                    {
                        using (var info = list.Call<AndroidJavaObject>("get", i))
                        {
                            if (info == null) continue;

                            using (var serviceInfo = info.Get<AndroidJavaObject>("serviceInfo"))
                            {
                                if (serviceInfo == null) continue;

                                string package = serviceInfo.Get<string>("packageName");
                                string name = serviceInfo.Get<string>("name");
                                if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(name)) continue;

                                found.Add(new ServiceRef { Package = package, Name = name });
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                _createError = "查服务列表失败：" + e.Message;
            }

            return found;
        }

        /// <summary>Tries one way of building a recogniser. Leaves <see cref="_recognizer"/> set on success.</summary>
        private static bool TryCreate(AndroidJavaClass recognizerClass, AndroidJavaObject activity,
            string label, string package = null, string name = null)
        {
            _listener = new Listener();
            _builds++;

            try
            {
                if (string.IsNullOrEmpty(package))
                {
                    _recognizer = recognizerClass.CallStatic<AndroidJavaObject>(
                        "createSpeechRecognizer", activity);
                }
                else
                {
                    using (var component = new AndroidJavaObject("android.content.ComponentName", package, name))
                    {
                        _recognizer = recognizerClass.CallStatic<AndroidJavaObject>(
                            "createSpeechRecognizer", activity, component);
                    }
                }
            }
            catch (System.Exception e)
            {
                _createError = label + "：" + e.Message;
                _recognizer = null;
                return false;
            }

            if (!HasRecognizer)
            {
                // A wrapper around a Java null is not a recogniser, and calling into it is what once
                // produced a bare NullReferenceException instead of a diagnosis.
                _recognizer = null;
                _createError = label + "：系统返回了 null";
                return false;
            }

            return FinishCreating(label);
        }

        /// <summary>Hooks the listener and the intent up to a recogniser that exists.</summary>
        private static bool FinishCreating(string label)
        {
            try
            {
                _recognizer.Call("setRecognitionListener", _listener);
                _intent = BuildIntent(_variant);
                _createPath = label;
                _lastFailure = "";
                return true;
            }
            catch (System.Exception e)
            {
                Fail("识别器创建后设置失败：" + e.Message);
                _recognizer = null;
                return false;
            }
        }

        /// <summary>
        /// Records a failure, and keeps it.
        ///
        /// This is the fix for a diagnostic that lied by omission: <see cref="_error"/> is cleared at
        /// the start of every attempt, so a phone whose recogniser had *never* been created displayed
        /// "语音输入就绪：点麦克风说话" — the panel was reporting the last attempt's silence, not the
        /// feature's state. <see cref="_lastFailure"/> is only cleared by a recogniser that works.
        /// </summary>
        private static void Fail(string message)
        {
            _error = message;
            _lastFailure = message;
            Debug.LogWarning("[DshMobile] " + message);
        }

        /// <summary>The intent the recogniser is started with. Built on the UI thread with the rest.</summary>
        private static AndroidJavaObject BuildIntent(int variant)
        {
            using (var intentClass = new AndroidJavaClass("android.speech.RecognizerIntent"))
            using (var activity = CurrentActivity())
            {
                string action = intentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
                var intent = new AndroidJavaObject("android.content.Intent", action);

                intent.Call<AndroidJavaObject>("putExtra",
                    intentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"),
                    intentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));

                if (variant > MinimalVariant) return intent;   // the last shape is the action and nothing else

                // The locale is a *try*, not a fact: asking for zh-CN explicitly is what makes a Chinese
                // phone recognise Chinese rather than answering in English, and it is also what some ROMs
                // choke on (they answer ERROR_CLIENT and never become ready). So it goes on the first
                // attempt and comes off the next one — one of the shapes is what this device wants, and
                // the diagnostics line says which.
                if (variant == 0)
                {
                    intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_LANGUAGE"), "zh-CN");
                }

                // The third shape asks for the *on-device* engine, which is a genuinely different
                // question from "recognise this online": a phone whose online recognition backend is a
                // service it cannot reach (a Google one on a phone with no Google account, say) can
                // still have a perfectly good offline engine.
                if (variant == MinimalVariant)
                {
                    try
                    {
                        intent.Call<AndroidJavaObject>("putExtra",
                            intentClass.GetStatic<string>("EXTRA_PREFER_OFFLINE"), true);
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("[DshMobile] prefer-offline extra skipped: " + e.Message);
                    }
                }

                // Partial results make the wait feel shorter, and they are all the input the player
                // needs to see before they stop talking.
                intent.Call<AndroidJavaObject>("putExtra",
                    intentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), true);

                // Several recognisers — the Chinese ROMs especially — ignore an intent that does not
                // name the package that is asking, and answer with ERROR_CLIENT instead of listening.
                try
                {
                    string package = activity == null ? null : activity.Call<string>("getPackageName");
                    if (!string.IsNullOrEmpty(package))
                    {
                        intent.Call<AndroidJavaObject>("putExtra",
                            intentClass.GetStatic<string>("EXTRA_CALLING_PACKAGE"), package);
                    }
                }
                catch (System.Exception e)
                {
                    // Not fatal: the older recognisers do not need it at all.
                    Debug.Log("[DshMobile] calling_package extra skipped: " + e.Message);
                }

                return intent;
            }
        }

        /// <summary>
        /// Whether there is a recogniser behind the wrapper.
        ///
        /// A Java object that came back null still produces a managed wrapper, and calling a method
        /// on that wrapper is a NullReferenceException — which is how "creation failed" turned into
        /// a message about an object reference. The raw pointer is the only honest test.
        /// </summary>
        private static bool HasRecognizer =>
            _recognizer != null && _recognizer.GetRawObject() != System.IntPtr.Zero;

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

            // The automatic retry: the attempt failed without the engine ever being ready, so ask a
            // different way. Three shapes, one per attempt — not a loop: a recogniser that refuses all
            // three is a recogniser that does not work on this phone, and hammering it would turn a
            // clear message into a mystery (and, before the report rules above, five chat lines).
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
        /// The most recent failure, once, for the UI to put in the conversation.
        ///
        /// Recognition now runs in a block posted to the UI thread, so a failure can happen *after*
        /// the tap that caused it has returned — there is no return value left to check. Without a
        /// hand-off like this, those failures would live only in <see cref="LastError"/> (a status
        /// line) and in the log, which is precisely the "the microphone does nothing and says
        /// nothing" experience this class has already been rewritten twice to avoid.
        /// </summary>
        public static string TakeErrorReport()
        {
            if (string.IsNullOrEmpty(_error)) return "";

            if (!ShouldReport(_retryPending, _reportedPress == _pressId,
                    Time.realtimeSinceStartup - _lastReportAt))
            {
                return "";
            }

            _reportedPress = _pressId;
            _lastReportAt = Time.realtimeSinceStartup;
            return _error;
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
            if (errorCode != int.MinValue && errorCode > 0)
            {
                string sentence = ErrorText(errorCode);
                return _retryPending ? sentence + "（正在自动重试）" : sentence;
            }
            if (!string.IsNullOrEmpty(error)) return error;

            // A phone whose recogniser has never once been built must not be told "everything is
            // ready": that is what this line said while the diagnostics a few rows below were saying
            // "识别器：未创建（第 51 次尝试）". The panel's own silence is not a status.
            if (!string.IsNullOrEmpty(_lastFailure)) return _lastFailure;

            return "语音输入就绪：点麦克风说话，识别到的文字会填进输入框。";
        }

        /// <summary>
        /// What an Android recognition error code means, in a sentence the player can act on.
        ///
        /// Pure, and — this is the point — <b>shared</b>. The first version had two mappings: a
        /// detailed one for the settings panel and a bare "识别失败（错误码 9）" for the conversation.
        /// That is exactly backwards, because the conversation is where the player is looking: the
        /// report came back twice from the phone, with codes 9 and 11, as a message that explained
        /// nothing and offered nothing to do. A number is a fact about the platform; this is the
        /// fact about the *player's* situation.
        /// </summary>
        public static string ErrorText(int code)
        {
            switch (code)
            {
                case 1: return "网络超时：识别服务没连上，检查一下网络再来一次。";
                case 2: return "网络出错：这台手机的语音识别要联网，检查一下网络。";
                case 3: return "录音出错：麦克风可能被别的应用占用了，关掉它们再试。";
                case 4: return "识别服务出错了（错误码 4），再点一次麦克风通常就好了。";
                case 5: return "识别服务拒绝了这次请求（错误码 5），我换个方式再试一次。";
                case 6: return "没听清（说得太久或太安静），再说一次就行。";
                case 7: return "没听到声音：靠近一点、大声一点再说一次。";
                case 8: return "识别服务正忙（错误码 8），等一下再点麦克风。";
                case 9: return "麦克风权限不够（错误码 9）：到系统设置里允许「录音」，" +
                               "顺便看看系统的语音服务有没有被禁用。";
                case 10: return "请求太频繁了（错误码 10），歇一会儿再说话。";
                case 11: return "系统语音服务断开了（错误码 11），再点一次麦克风重连。";
                case 12: return "这台手机的语音识别不支持中文，先用打字吧。";
                case 13: return "这台手机还没下载中文语音包（错误码 13），到系统设置里装上再试。";
                case 14: return "这台手机的语音识别不可用（错误码 14），先用打字吧。";
                case 15: return "系统正在处理语音包的下载（错误码 15），稍后再试。";
                default: return $"识别失败（错误码 {code}），再点一次麦克风；" +
                                "一直这样的话，把设置里的「语音诊断」发我看看。";
            }
        }

        /// <summary>
        /// Whether an error is the kind that a second attempt can fix on its own.
        ///
        /// These are the ones that are about the *attempt* rather than about the phone: a server that
        /// dropped the connection, a request that timed out, an engine that refused this particular
        /// question. Retrying one of those is invisible and useful; retrying "no speech package
        /// installed" would be a loop that hides the reason, so those are reported instead.
        /// </summary>
        public static bool IsTransient(int code)
        {
            switch (code)
            {
                case 1:   // ERROR_NETWORK_TIMEOUT
                case 2:   // ERROR_NETWORK
                case 4:   // ERROR_SERVER
                case 11:  // ERROR_SERVER_DISCONNECTED
                    return true;
                default:
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

                // The same sentence the settings panel shows, because the conversation is where the
                // player is actually looking. Codes 9 and 11 were reported from the phone as a bare
                // "识别失败（错误码 9）", which tells a player nothing and offers nothing to do.
                _error = ErrorText(error);

                // The engine never even became ready: this is the shape of failure that means "this
                // phone did not like the question", not "the phone did not hear you". Ask it a different
                // way — the next attempt uses the next intent shape — and rebuild the engine first,
                // because a recogniser that has errored can keep failing forever.
                if (!IsFinalAttempt(_attemptsThisPress, _sawReady))
                {
                    _needsRebuild = true;
                    _retryPending = true;
                    return;
                }

                // An engine that *was* working and then lost its connection gets one more attempt with
                // the same question and a fresh object — the shape is not the problem, the drop is.
                // (ERROR_SERVER_DISCONNECTED, 11, arrived from the phone as a bare error code.)
                if (IsTransient(error) && _attemptsThisPress < AttemptsPerPress)
                {
                    _needsRebuild = true;
                    _retryPending = true;
                    return;
                }

                // 5 ERROR_CLIENT and 8 ERROR_RECOGNIZER_BUSY both leave the object in a state where
                // the next press would otherwise be dropped on the floor. 9 INSUFFICIENT_PERMISSIONS
                // does the same on the ROMs that report it for a *stale* recogniser rather than for a
                // missing permission — the permission was checked before starting, so the object is
                // the suspect, and rebuilding it is the only lever this class has.
                if (error == 5 || error == 8 || error == 9) _needsRebuild = true;
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
