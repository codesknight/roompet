using UnityEngine;

namespace DshPet
{
    /// <summary>What the front door is doing.</summary>
    public enum StartPhase
    {
        Menu,       // the four buttons are up
        Settings,   // the switches are up
        HowTo,      // the rules are up
        Quit,       // "are you sure you want to leave?"
        Opening,    // the door is swinging open
        Leaving     // fading out, about to load the room
    }

    /// <summary>
    /// The opening sequence, as arithmetic.
    ///
    /// The point of separating this from the scene is the same one every other game here is built on:
    /// an animation whose timing lives inside a MonoBehaviour can only be checked by watching it. As a
    /// pure function of one number it can be asserted — the door reaches open and stays there, the glow
    /// never overshoots, the fade is black at the start and white at the end, and the room is never
    /// loaded before the picture has finished changing.
    /// </summary>
    public static class StartSequence
    {
        /// <summary>How long the door takes to swing open.</summary>
        public const float DoorSeconds = 1.45f;

        /// <summary>How long the white fade takes, after the door is open.</summary>
        public const float FadeSeconds = 0.55f;

        /// <summary>How far each panel swings, in degrees. Past 90 so it reads as "come in".</summary>
        public const float DoorAngle = 104f;

        /// <summary>The whole sequence.</summary>
        public const float TotalSeconds = DoorSeconds + FadeSeconds;

        /// <summary>Eased 0..1 over the swing.</summary>
        public static float DoorProgress(float t)
            => Smooth(Mathf.Clamp01(t / DoorSeconds));

        /// <summary>How far a panel has swung, in degrees, at this point in the sequence.</summary>
        public static float PanelAngle(float seconds)
            => DoorAngle * DoorProgress(seconds);

        /// <summary>How bright the light through the doorway is.</summary>
        public static float DoorGlow(float seconds)
        {
            float t = DoorProgress(seconds);

            // Squared rather than linear: a door that lights the room *before* it opens looks like a
            // light being switched on, not like a door being opened.
            return t * t;
        }

        /// <summary>How far the camera has drifted towards the doorway, 0..1.</summary>
        public static float CameraPush(float seconds)
        {
            float t = Mathf.Clamp01(seconds / TotalSeconds);
            return Smooth(t);
        }

        /// <summary>The white veil over the picture, 0..1.</summary>
        public static float FadeAlpha(float seconds)
        {
            if (seconds <= DoorSeconds) return 0f;
            return Smooth(Mathf.Clamp01((seconds - DoorSeconds) / FadeSeconds));
        }

        /// <summary>
        /// Whether the room may be loaded yet.
        ///
        /// Deliberately before the fade is fully white: loading a scene takes a moment of its own, and
        /// the fade should still be finishing when the room appears rather than having long since
        /// finished, which reads as a hang.
        /// </summary>
        public static bool ReadyToEnter(float seconds) => seconds >= DoorSeconds + FadeSeconds * 0.7f;

        /// <summary>Where the animal at the doorway has got to, as a fraction of its walk-through.</summary>
        public static float PetWalk(float seconds) => Smooth(Mathf.Clamp01(seconds / TotalSeconds));

        /// <summary>Smoothstep: fast in the middle, gentle at both ends, and exactly 0 and 1 at them.</summary>
        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
