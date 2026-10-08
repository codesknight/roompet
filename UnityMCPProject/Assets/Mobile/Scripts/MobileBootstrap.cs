using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Applies the phone-friendly runtime settings at startup.
    ///
    /// Installed automatically, so no scene has to be edited: the two scenes are shared
    /// between the desktop and mobile builds and should stay that way.
    /// </summary>
    public static class MobileBootstrap
    {
        /// <summary>Frame rate cap on mobile. 60 on a phone costs battery; 30 feels sluggish.</summary>
        public const int MobileFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (!MobileUi.IsMobile)
            {
                // In the editor and on desktop, leave the project's own settings alone.
                return;
            }

            // Vertical sync would override targetFrameRate, so it goes off first.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = MobileFrameRate;

            // A pet that sleeps while you are reading its diary is annoying.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Shadows are the first thing to cost frames on a phone, and the room is lit by a
            // single directional light with a small footprint: keep the shadows, pull the
            // distance in, and drop multi-sample AA to 2x.
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 26f);
            QualitySettings.antiAliasing = 2;
            QualitySettings.softParticles = false;

            Debug.Log($"[DshMobile] Mobile runtime: {Application.targetFrameRate} fps target, " +
                      $"{Screen.width}x{Screen.height} @ {Screen.dpi:F0} dpi, ui scale {MobileUi.UiScale:F2}");
        }
    }
}
