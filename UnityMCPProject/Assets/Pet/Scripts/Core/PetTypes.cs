using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>What the pet is doing. The brain picks one of these per reply, and the
    /// avatar turns it into an animation.</summary>
    public enum PetAction
    {
        Idle,
        Happy,
        Jump,
        Wag,
        Curious,
        Play,
        Eat,
        Drink,
        Sleep,
        Sit,
        Beg,
        Sad
    }

    /// <summary>Coarse mood derived from the needs. Used both for the HUD and as
    /// prompt material for the language model.</summary>
    public enum PetMood
    {
        Sleepy,
        Hungry,
        Dirty,
        Lonely,
        Bored,
        Content,
        Happy,
        Excited,

        /// <summary>Holding it in and looking for the tray — the one mood with a deadline.</summary>
        NeedsToilet
    }

    public enum EarStyle { Pointy, Round, Long, Small }

    public enum TailStyle { Bushy, Curly, Puff, Short }

    public enum InteractableKind
    {
        Food, Water, Ball, Bed, Brush, Toy, Door, Toilet, Bath, Mess,

        /// <summary>
        /// A shrub, a hedge, the back of a tree: somewhere to hide. Not furniture — it exists so
        /// the pet can play hide-and-seek, which is the garden's own game.
        /// </summary>
        HidingSpot,

        /// <summary>Garden furniture: bears apples the pet or the owner can take.</summary>
        AppleTree,

        /// <summary>Garden furniture: the pet drinks from it; the owner fishes in it.</summary>
        Pond,

        /// <summary>Garden furniture: a soft heap the pet sleeps in.</summary>
        GrassHeap,

        /// <summary>Garden furniture: the pet swings on it for joy.</summary>
        Swing,

        /// <summary>Terrace furniture: the pet peers through it at the stars.</summary>
        Telescope,

        /// <summary>Terrace furniture: a chair the pet dozes off in.</summary>
        RockingChair
    }

    /// <summary>One turn of the conversation.</summary>
    [Serializable]
    public struct ChatMessage
    {
        public string Role;   // "user", "pet" or "system"
        public string Text;
        public float At;      // Time.realtimeSinceStartup when it happened

        public ChatMessage(string role, string text, float at)
        {
            Role = role;
            Text = text;
            At = at;
        }

        public bool IsUser => Role == "user";

        /// <summary>
        /// A note from the game rather than from the pet or the player.
        ///
        /// Rendered as a centred grey line, and kept out of the model's prompt: the pet must not
        /// start answering the UI's own advice, and a hint is not part of the relationship.
        /// </summary>
        public bool IsSystem => Role == "system";
    }

    /// <summary>What the brain returns for one user turn.</summary>
    [Serializable]
    public struct PetReply
    {
        public string Speech;
        public PetAction Action;
        public string MoodWord;
        public float AffectionDelta;
        public bool FromNetwork;

        /// <summary>Optional durable note the model asked the pet to remember. Filed into
        /// the journal as a pinned, never-pruned entry.</summary>
        public string MemoryNote;

        /// <summary>Raw model output, kept for debugging in the HUD.</summary>
        public string Raw;

        public bool IsValid => !string.IsNullOrEmpty(Speech);

        public static PetReply Invalid(string raw) => new PetReply
        {
            Speech = null,
            Action = PetAction.Idle,
            MoodWord = "",
            AffectionDelta = 0f,
            FromNetwork = false,
            Raw = raw
        };
    }

    /// <summary>Everything the brain is allowed to know about the pet right now. Keeping
    /// this a plain snapshot is what lets the brain be tested without a scene.</summary>
    [Serializable]
    public struct PetContext
    {
        public string SpeciesId;
        public string SpeciesName;
        public string Personality;
        public string VoiceStyle;
        public string PetName;
        public PetMood Mood;
        public float Hunger;
        public float Energy;
        public float Joy;
        public float Cleanliness;
        public float Bladder;
        public float Affection;
        public string DominantNeed;
        /// <summary>Recent conversation, oldest first. Used both for the prompt text and
        /// as the chat history sent to the API.</summary>
        public ChatMessage[] History;
        public string[] LongTermFacts;
        /// <summary>Compact summary of the last few days from the journal.</summary>
        public string DayDigest;
        public string LastInteraction;

        /// <summary>This pet's temperament, as a paragraph for the prompt.</summary>
        public string Temperament;
        /// <summary>Short archetype name, for the HUD and for the model's self-image.</summary>
        public string Archetype;

        /// <summary>Player-authored text appended to the system prompt (see PetBrainConfig).</summary>
        public string ExtraInstructions;

        /// <summary>
        /// What the pet can see right now: where it is in the room, where its things are, and where the
        /// owner is (see <see cref="PetPerception"/>).
        ///
        /// Part of the context rather than something the prompt builder fetches, for the same reason
        /// everything else is: the prompt stays a pure function of a struct, so what the model is told
        /// can be asserted in a test instead of inspected in a log.
        /// </summary>
        public string Perception;
    }

    /// <summary>Serialisable snapshot of the pet's condition.</summary>
    [Serializable]
    public struct PetStatus
    {
        public PetMood Mood;
        public float Hunger;
        public float Energy;
        public float Joy;
        public float Cleanliness;
        public float Bladder;
        public float Affection;
        public float MoodScore;
        public string DominantNeed;
    }

    /// <summary>Small helpers shared by the rest of the pet code.</summary>
    public static class PetUtil
    {
        /// <summary>
        /// Cleans up a player-typed pet name.
        ///
        /// The name is injected straight into the system prompt, so it is treated as untrusted
        /// input: newlines are collapsed (a name containing a newline could forge a prompt
        /// section), whitespace is trimmed, and the length is capped — a 200-character "name"
        /// would swamp the persona line it sits in. Blank falls back to the species name.
        /// </summary>
        public static string SanitizeName(string raw, string fallback, int maxLength = 12)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            var sb = new System.Text.StringBuilder(raw.Length);
            bool lastWasSpace = false;
            foreach (char c in raw.Trim())
            {
                // Control characters (including newlines and tabs) become single spaces.
                char ch = char.IsControl(c) ? ' ' : c;
                bool space = ch == ' ';
                if (space && lastWasSpace) continue;
                sb.Append(ch);
                lastWasSpace = space;
            }

            string cleaned = sb.ToString().Trim();
            if (cleaned.Length > maxLength) cleaned = cleaned.Substring(0, maxLength).Trim();
            return string.IsNullOrEmpty(cleaned) ? fallback : cleaned;
        }

        public static string MoodLabel(PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Sleepy: return "困了";
                case PetMood.Hungry: return "饿了";
                case PetMood.Dirty: return "脏兮兮";
                case PetMood.NeedsToilet: return "憋不住了";
                case PetMood.Lonely: return "想你了";
                case PetMood.Bored: return "有点无聊";
                case PetMood.Content: return "还行";
                case PetMood.Happy: return "开心";
                case PetMood.Excited: return "超兴奋";
                default: return mood.ToString();
            }
        }

        public static string ActionLabel(PetAction action)
        {
            switch (action)
            {
                case PetAction.Idle: return "发呆";
                case PetAction.Happy: return "开心地晃";
                case PetAction.Jump: return "跳了一下";
                case PetAction.Wag: return "摇尾巴";
                case PetAction.Curious: return "歪头看你";
                case PetAction.Play: return "玩起来";
                case PetAction.Eat: return "吃东西";
                case PetAction.Drink: return "喝水";
                case PetAction.Sleep: return "睡着了";
                case PetAction.Sit: return "坐下";
                case PetAction.Beg: return "讨食";
                case PetAction.Sad: return "委屈";
                default: return action.ToString();
            }
        }

        /// <summary>Case-insensitive action lookup that also tolerates the model writing
        /// Chinese or a synonym.</summary>
        public static bool TryParseAction(string raw, out PetAction action)
        {
            action = PetAction.Idle;
            if (string.IsNullOrEmpty(raw)) return false;

            string value = raw.Trim().ToLowerInvariant();

            foreach (PetAction candidate in Enum.GetValues(typeof(PetAction)))
            {
                if (value == candidate.ToString().ToLowerInvariant())
                {
                    action = candidate;
                    return true;
                }
            }

            // Chinese / loose synonyms the model may produce instead of the enum name.
            if (Contains(value, "跳", "jump", "hop")) { action = PetAction.Jump; return true; }
            if (Contains(value, "摇尾", "wag", "tail")) { action = PetAction.Wag; return true; }
            if (Contains(value, "歪头", "curious", "tilt", "好奇")) { action = PetAction.Curious; return true; }
            if (Contains(value, "玩", "play", "ball")) { action = PetAction.Play; return true; }
            if (Contains(value, "吃", "eat", "food")) { action = PetAction.Eat; return true; }
            if (Contains(value, "喝", "drink", "water")) { action = PetAction.Drink; return true; }
            if (Contains(value, "睡", "sleep", "nap")) { action = PetAction.Sleep; return true; }
            if (Contains(value, "坐", "sit")) { action = PetAction.Sit; return true; }
            if (Contains(value, "讨", "beg")) { action = PetAction.Beg; return true; }
            if (Contains(value, "难过", "委屈", "sad", "cry")) { action = PetAction.Sad; return true; }
            if (Contains(value, "开心", "happy", "joy", "兴奋")) { action = PetAction.Happy; return true; }

            return false;
        }

        private static bool Contains(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (haystack.Contains(needles[i])) return true;
            }
            return false;
        }
    }
}
