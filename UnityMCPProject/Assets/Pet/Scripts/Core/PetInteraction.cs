using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>What the pet does when it is poked.</summary>
    public enum PokeReaction
    {
        Nuzzle,     // rubs against your hand
        Lean,       // stands still and enjoys it
        RollOver,   // flops over for a belly rub
        Hop,        // bounces away and comes back
        Squirm,     // wriggles, half-heartedly
        Dodge,      // steps out of reach
        Ignore,     // pretends not to notice
        Purr,       // low, contented rumble
        Yelp,       // startled
        Sniff,      // investigates the hand
        Beg,        // assumes food is involved
        Sleepy      // too tired to care
    }

    /// <summary>
    /// What happens when the player touches the pet.
    ///
    /// The reaction is chosen from the temperament first and the mood second, which is the
    /// order that makes the pet feel like an individual rather than a state machine: a shy cat
    /// and a bold cat do different things when poked even though they are equally happy. The
    /// mood then overrides in the two cases where it obviously should — too tired, or too
    /// hungry to be interested in affection.
    ///
    /// Pure functions, so "a clingy pet nuzzles and an independent one dodges" is a test.
    /// </summary>
    public static class PetInteraction
    {
        /// <summary>Line the pet says. Local, so petting never costs a token.</summary>
        public static string Line(PokeReaction reaction, string petName, string speciesId)
        {
            switch (reaction)
            {
                case PokeReaction.Nuzzle: return "（把脑袋往你手心里顶）";
                case PokeReaction.Lean: return "（靠在你手上不动了）";
                case PokeReaction.RollOver: return "（翻了个身，露出肚子）";
                case PokeReaction.Hop: return "（蹦开半步，又凑回来）";
                case PokeReaction.Squirm: return "（扭了一下，没真的躲）";
                case PokeReaction.Dodge: return "（往旁边挪了半步）";
                case PokeReaction.Ignore: return "（假装没发现，耳朵动了一下）";
                case PokeReaction.Purr: return "（发出很轻的呼噜声）";
                case PokeReaction.Yelp: return "（被吓了一跳，缩成一团）";
                case PokeReaction.Sniff: return "（凑过来闻了闻你的手）";
                case PokeReaction.Beg: return "（盯着你的手，好像在等吃的）";
                case PokeReaction.Sleepy: return "（半睁了一下眼，又闭上了）";
                default: return "（看了你一眼）";
            }
        }

        /// <summary>Animation to play for a reaction.</summary>
        public static PetAction Action(PokeReaction reaction)
        {
            switch (reaction)
            {
                case PokeReaction.Nuzzle: return PetAction.Happy;
                case PokeReaction.Lean: return PetAction.Wag;
                case PokeReaction.RollOver: return PetAction.Play;
                case PokeReaction.Hop: return PetAction.Jump;
                case PokeReaction.Squirm: return PetAction.Curious;
                case PokeReaction.Dodge: return PetAction.Sad;
                case PokeReaction.Ignore: return PetAction.Idle;
                case PokeReaction.Purr: return PetAction.Sit;
                case PokeReaction.Yelp: return PetAction.Sad;
                case PokeReaction.Sniff: return PetAction.Curious;
                case PokeReaction.Beg: return PetAction.Beg;
                case PokeReaction.Sleepy: return PetAction.Sleep;
                default: return PetAction.Idle;
            }
        }

        /// <summary>How much the pet enjoys being poked, -1 (minds it) .. 1 (loves it).</summary>
        public static float AffectionDelta(PokeReaction reaction)
        {
            switch (reaction)
            {
                case PokeReaction.Nuzzle: return 0.030f;
                case PokeReaction.Lean: return 0.022f;
                case PokeReaction.RollOver: return 0.028f;
                case PokeReaction.Hop: return 0.014f;
                case PokeReaction.Squirm: return 0.010f;
                case PokeReaction.Purr: return 0.026f;
                case PokeReaction.Sniff: return 0.012f;
                case PokeReaction.Beg: return 0.006f;
                case PokeReaction.Ignore: return 0f;
                case PokeReaction.Sleepy: return 0.004f;
                case PokeReaction.Dodge: return -0.004f;
                case PokeReaction.Yelp: return -0.008f;
                default: return 0.008f;
            }
        }

        /// <summary>Joy the pet gets from being poked. Negative for the ones it dislikes.</summary>
        public static float JoyDelta(PokeReaction reaction)
        {
            switch (reaction)
            {
                case PokeReaction.Nuzzle: return 0.035f;
                case PokeReaction.RollOver: return 0.040f;
                case PokeReaction.Lean: return 0.025f;
                case PokeReaction.Purr: return 0.030f;
                case PokeReaction.Hop: return 0.020f;
                case PokeReaction.Sniff: return 0.012f;
                case PokeReaction.Squirm: return 0.008f;
                case PokeReaction.Ignore: return -0.004f;
                case PokeReaction.Dodge: return -0.012f;
                case PokeReaction.Yelp: return -0.030f;
                default: return 0.006f;
            }
        }

        /// <summary>
        /// Picks the reaction. <paramref name="roll"/> is 0..1 from the caller's RNG; passing it
        /// in is what keeps this testable.
        /// </summary>
        public static PokeReaction Choose(PetBehaviorContext ctx, float roll)
        {
            // Too tired, or too desperate, and the pet has other priorities.
            if (ctx.Energy < 0.18f) return PokeReaction.Sleepy;
            if (ctx.Bladder < 0.15f) return PokeReaction.Dodge;
            if (ctx.Hunger < 0.20f && roll < 0.6f) return PokeReaction.Beg;

            var personality = ctx.Personality ?? new PetPersonality();

            // A short, readable table: each entry is a reaction with the temperament that wants
            // it. Weights come from the traits, so the same pet is consistent from poke to poke.
            float clingy = personality.Clinginess;
            float lively = personality.Liveliness;
            float curious = personality.Curiosity;
            float neat = personality.Neatness;

            float wNuzzle = 0.35f + clingy * 1.6f;
            float wLean = 0.30f + clingy * 1.1f + neat * 0.4f;
            float wRoll = 0.20f + clingy * 0.8f + lively * 0.5f;
            float wHop = 0.20f + lively * 1.3f;
            float wSquirm = 0.35f + neat * 0.9f;
            float wDodge = 0.55f - clingy * 0.45f + (1f - lively) * 0.3f;
            float wIgnore = 0.45f - clingy * 0.40f;
            float wPurr = 0.15f + clingy * 0.7f + (1f - lively) * 0.5f;
            float wSniff = 0.20f + curious * 1.5f;
            float wYelp = 0.05f + (1f - lively) * 0.25f + (1f - clingy) * 0.2f;

            // Mood colours the mix: a bored pet is more likely to engage, a grumpy one less.
            if (ctx.Joy < 0.25f) { wSquirm *= 1.4f; wIgnore *= 1.5f; wRoll *= 0.6f; }
            if (ctx.Joy > 0.8f) { wRoll *= 1.5f; wHop *= 1.3f; }
            if (ctx.Cleanliness < 0.3f) { wSquirm *= 0.6f; wDodge *= 0.7f; }   // too grubby to care

            float total = wNuzzle + wLean + wRoll + wHop + wSquirm + wDodge + wIgnore + wPurr + wSniff + wYelp;
            if (total <= 0f) return PokeReaction.Lean;

            float target = Mathf.Clamp01(roll) * total;
            target -= wNuzzle; if (target <= 0f) return PokeReaction.Nuzzle;
            target -= wLean; if (target <= 0f) return PokeReaction.Lean;
            target -= wRoll; if (target <= 0f) return PokeReaction.RollOver;
            target -= wPurr; if (target <= 0f) return PokeReaction.Purr;
            target -= wSniff; if (target <= 0f) return PokeReaction.Sniff;
            target -= wHop; if (target <= 0f) return PokeReaction.Hop;
            target -= wSquirm; if (target <= 0f) return PokeReaction.Squirm;
            target -= wYelp; if (target <= 0f) return PokeReaction.Yelp;
            target -= wIgnore; if (target <= 0f) return PokeReaction.Ignore;
            return PokeReaction.Dodge;
        }

        /// <summary>The mood to use when voicing a reaction — mostly its own emotion.</summary>
        public static PetMood MoodFor(PokeReaction reaction, PetMood current)
        {
            switch (reaction)
            {
                case PokeReaction.Nuzzle:
                case PokeReaction.Lean:
                case PokeReaction.RollOver:
                case PokeReaction.Purr:
                    return PetMood.Happy;
                case PokeReaction.Hop:
                    return PetMood.Excited;
                case PokeReaction.Dodge:
                case PokeReaction.Yelp:
                    return PetMood.Lonely;
                case PokeReaction.Beg:
                    return PetMood.Hungry;
                case PokeReaction.Sleepy:
                    return PetMood.Sleepy;
                default:
                    return current;
            }
        }
    }
}
