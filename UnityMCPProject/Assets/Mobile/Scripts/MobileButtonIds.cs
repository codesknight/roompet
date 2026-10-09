namespace DshMobile
{
    /// <summary>
    /// The ids that tie on-screen buttons to the code that reads them.
    ///
    /// The HUD registers a rect under an id while drawing; the gameplay scripts ask
    /// <see cref="MobileTouch.Pressed"/> for the same id. Keeping the strings in one place
    /// is what stops a renamed button from silently doing nothing.
    /// </summary>
    public static class MobileButtonIds
    {
        // ------------------------------------------------------------- pet room
        /// <summary>Context action: pick the ball up, use the object you are next to, open the door.</summary>
        public const string PetAction = "pet.action";

        /// <summary>Hold to charge a throw, release to throw. Only shown while holding the ball.</summary>
        public const string PetThrow = "pet.throw";

        /// <summary>Opens/closes the chat panel (and the on-screen keyboard).</summary>
        public const string PetChat = "pet.chat";

        /// <summary>Sends whatever is typed in the chat box.</summary>
        public const string PetSend = "pet.send";

        /// <summary>Collapses/expands the pet status panel.</summary>
        public const string PetStatus = "pet.status";

        /// <summary>The microphone: press to speak instead of typing.</summary>
        public const string PetMic = "pet.mic";

        // ---------------------------------------------------------------- runner
        /// <summary>Left/right lane change, for players who prefer buttons to swipes.</summary>
        public const string RunnerLeft = "runner.left";
        public const string RunnerRight = "runner.right";

        /// <summary>Jump (hold for a higher jump, matching the keyboard behaviour).</summary>
        public const string RunnerJump = "runner.jump";

        /// <summary>Slide under a barrier.</summary>
        public const string RunnerSlide = "runner.slide";

        /// <summary>Pause/resume.</summary>
        public const string RunnerPause = "runner.pause";
    }
}
