using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// Keeps the clock honest across scene changes.
    ///
    /// The forest runner pauses by setting <c>Time.timeScale</c> to zero, which is the right way to
    /// pause a game and the wrong thing to leave behind: <c>Time.timeScale</c> is global and survives
    /// a scene load, so a player who pauses and then taps "返回宠物小屋" arrives in the pet room with
    /// the whole world stopped. Nothing in the room is broken — the pet's needs freeze, the player
    /// cannot walk, animations stop — and there is no error to read, which is what made it look like
    /// a bug in the room rather than in the game they just left.
    ///
    /// So every scene load restores it. This is deliberately blunt: the pet room and all four mini
    /// games run at normal speed, and a *paused* scene that wants the clock stopped pauses again on
    /// its own first frame. Forgetting to reset it is a silent, unrecoverable state; resetting it too
    /// eagerly costs one frame of a menu that is about to pause anyway.
    /// </summary>
    public static class SceneClock
    {
        /// <summary>The scale everything in this project runs at.</summary>
        public const float NormalTimeScale = 1f;

        /// <summary>
        /// Puts the clock back to normal, unless somebody has deliberately slowed it down outside a
        /// pause (nothing does today, but a slow-motion power-up is exactly the kind of thing that
        /// would, and a frame of it being thrown away is not worth the risk).
        /// </summary>
        public static void Restore(string why = null)
        {
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = NormalTimeScale;
                if (!string.IsNullOrEmpty(why))
                {
                    Debug.Log("[DshMobile] restored Time.timeScale after " + why);
                }
            }

            // Time.fixedDeltaTime is scaled by timeScale, so it is left alone; the audio listener
            // needs no help either. Only the clock itself was ever left in the wrong state.
        }
    }
}
