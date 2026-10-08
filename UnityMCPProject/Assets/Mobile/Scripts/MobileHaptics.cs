using UnityEngine;

namespace DshMobile
{
    /// <summary>How hard a haptic nudge is, in words rather than milliseconds.</summary>
    public enum Haptic
    {
        /// <summary>Tiny tick: a lane change, a button going down.</summary>
        Light = 0,

        /// <summary>A whole action landed: jump off, ball thrown, pet arrived.</summary>
        Medium = 1,

        /// <summary>Something went wrong or something big happened: crash, level finished.</summary>
        Heavy = 2
    }

    /// <summary>
    /// When a haptic pulse is allowed to fire, on its own so it can be tested.
    ///
    /// Two rules, both learned from how phones feel in the hand:
    ///  - a pulse is dropped if one fired a moment ago, because a phone that buzzes once per
    ///    frame while a button is held reads as a fault rather than as feedback;
    ///  - a pulse is dropped while haptics are switched off, which is a setting players look
    ///    for and then expect to be obeyed *everywhere* — including the runner.
    /// </summary>
    public class HapticGate
    {
        /// <summary>Seconds that must pass between two pulses.</summary>
        public float MinInterval = 0.045f;

        private float _lastFired = float.NegativeInfinity;

        public bool ShouldFire(float now, bool enabled)
        {
            if (!enabled) return false;
            if (now - _lastFired < MinInterval) return false;
            _lastFired = now;
            return true;
        }

        public void Reset() => _lastFired = float.NegativeInfinity;
    }

    /// <summary>
    /// Vibration feedback.
    ///
    /// Android-only and deliberately dependency-free: the platform call is made through
    /// <c>AndroidJavaObject</c> so there is no plugin to import, and every other platform is a
    /// silent no-op — which is what keeps the editor and the desktop build identical to before.
    ///
    /// It goes through <see cref="HapticGate"/> for the rate limit and the on/off setting, and
    /// the actual pulse is behind <see cref="Sink"/> so tests can watch what would have been
    /// sent without a phone attached.
    /// </summary>
    public static class MobileHaptics
    {
        /// <summary>PlayerPrefs key for the on/off setting.</summary>
        public const string EnabledKey = "dshmobile.haptics";

        /// <summary>Milliseconds per strength, matched to how Android renders them.</summary>
        public const int LightMs = 18;
        public const int MediumMs = 35;
        public const int HeavyMs = 70;

        private static readonly HapticGate Gate = new HapticGate();

        /// <summary>Where a pulse goes. Replaced in tests; the default talks to the device.</summary>
        public static System.Action<int, int> Sink;

        private static bool _enabledLoaded;
        private static bool _enabled = true;

        /// <summary>The player's setting, remembered across runs. On by default.</summary>
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
                if (!value) Cancel();
            }
        }

        /// <summary>
        /// True when this device can actually vibrate. False in the editor and on desktop, so
        /// asking is always safe.
        /// </summary>
        public static bool Supported => Application.isMobilePlatform;

        /// <summary>Duration this build would use for a strength, in milliseconds.</summary>
        public static int DurationFor(Haptic strength)
        {
            switch (strength)
            {
                case Haptic.Heavy: return HeavyMs;
                case Haptic.Medium: return MediumMs;
                default: return LightMs;
            }
        }

        /// <summary>Fires a pulse unless it is switched off, unsupported or too soon.</summary>
        public static void Pulse(Haptic strength)
        {
            if (!Supported) return;
            if (!Gate.ShouldFire(Time.unscaledTime, Enabled)) return;

            int milliseconds = DurationFor(strength);
            int amplitude = AmplitudeFor(strength);
            var sink = Sink;
            if (sink != null) sink(milliseconds, amplitude);
            else VibratePlatform(milliseconds, amplitude);
        }

        public static void Light() => Pulse(Haptic.Light);
        public static void Medium() => Pulse(Haptic.Medium);
        public static void Heavy() => Pulse(Haptic.Heavy);

        /// <summary>Stops a pulse already in progress, e.g. when the player turns it off.</summary>
        public static void Cancel()
        {
            if (!Supported) return;
            try
            {
                using (var vibrator = Vibrator())
                {
                    if (vibrator != null) vibrator.Call("cancel");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DshMobile] Haptic cancel failed: {e.Message}");
            }
        }

        /// <summary>Clears the rate limiter. Used between tests and after a scene hand-off.</summary>
        public static void Reset()
        {
            Gate.Reset();
            _enabledLoaded = false;
        }

        private static int AmplitudeFor(Haptic strength)
        {
            switch (strength)
            {
                // 0 means "let the system decide" on the older API; anything above ~120 is
                // indistinguishable in the hand but drains more.
                case Haptic.Heavy: return 190;
                case Haptic.Medium: return 120;
                default: return 60;
            }
        }

        // ------------------------------------------------------------------ platform

        private static AndroidJavaObject Vibrator()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null) return null;
                return activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
        }

        private static void VibratePlatform(int milliseconds, int amplitude)
        {
            try
            {
                using (var vibrator = Vibrator())
                {
                    if (vibrator == null) return;

                    int sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                    if (sdk >= 26)
                    {
                        // API 26 replaced the bare duration call with an effect object that
                        // carries the amplitude; the old call is deprecated but still works.
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                                   "createOneShot", (long)milliseconds, amplitude))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", (long)milliseconds);
                    }
                }
            }
            catch (System.Exception e)
            {
                // A device without a vibrator, or a manifest without the permission, must not
                // take the game down over a UI nicety.
                Debug.LogWarning($"[DshMobile] Haptic pulse failed: {e.Message}");
            }
        }
    }
}
