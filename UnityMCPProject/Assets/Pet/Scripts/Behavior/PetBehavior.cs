using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Which half of the interaction model a behaviour belongs to.
    ///
    /// - <see cref="Proactive"/>: driven by state or the environment. The pet decides
    ///   "I am hungry, so I am going to the bowl" on its own.
    /// - <see cref="Passive"/>: ambient quirks fired on a random timer. The pet stretches,
    ///   yawns, looks around — the layer that keeps it from reading as a state machine.
    /// </summary>
    public enum BehaviorDrive { Proactive, Passive }

    /// <summary>
    /// Everything a behaviour is allowed to look at. A plain snapshot, so behaviour
    /// scoring is a pure function and can be unit tested without a scene.
    /// </summary>
    public struct PetBehaviorContext
    {
        public float Hunger;
        public float Energy;
        public float Joy;
        public float Cleanliness;
        public float Bladder;
        public float Affection;
        public PetMood Mood;
        public string DominantNeed;

        /// <summary>0..24 local clock hour, for time-of-day behaviour.</summary>
        public float HourOfDay;

        /// <summary>True when the player's attention is available (in this build, always).</summary>
        public bool PlayerPresent;

        /// <summary>True while the ball is out of its resting spot and worth chasing.</summary>
        public bool BallLoose;

        /// <summary>True while a mess is waiting to be cleaned up.</summary>
        public bool MessPresent;

        /// <summary>Kinds of interactable currently reachable and off cooldown.</summary>
        public InteractableKind[] AvailableTargets;

        /// <summary>
        /// The pet's temperament. Optional: a null personality simply means "no bias", which
        /// is what keeps the behaviour table testable on its own.
        /// </summary>
        public PetPersonality Personality;

        /// <summary>
        /// Where the pet is. Carried as a name rather than as the <see cref="RoomTheme"/> enum so
        /// the behaviour table stays a plain data table that a test can fill in with one word —
        /// and so a new place does not have to touch this file to be allowed to have its own
        /// behaviour.
        /// </summary>
        public string Place;

        public bool InPlace(string name)
            => !string.IsNullOrEmpty(Place) &&
               string.Equals(Place, name, StringComparison.OrdinalIgnoreCase);

        public float Need(string name)
        {
            switch (name)
            {
                case "Hunger": return Hunger;
                case "Energy": return Energy;
                case "Joy": return Joy;
                case "Cleanliness": return Cleanliness;
                case "Bladder": return Bladder;
                case "Affection": return Affection;
                default: return 1f;
            }
        }

        public bool HasTarget(InteractableKind kind)
        {
            if (AvailableTargets == null) return false;
            for (int i = 0; i < AvailableTargets.Length; i++)
            {
                if (AvailableTargets[i] == kind) return true;
            }
            return false;
        }

        public bool IsNight => HourOfDay < 6f || HourOfDay >= 22f;
    }

    /// <summary>
    /// One entry in the pet's behaviour table. Declarative rather than a delegate: every
    /// field is inspectable in the inspector and in tests, and adding a behaviour means
    /// adding a row, not writing a new class.
    /// </summary>
    [Serializable]
    public class PetBehavior
    {
        public string Id = "";
        public string Label = "";
        public BehaviorDrive Drive = BehaviorDrive.Passive;

        /// <summary>Animation the avatar plays.</summary>
        public PetAction Action = PetAction.Idle;

        public float Duration = 2f;

        /// <summary>Seconds before this behaviour may fire again.</summary>
        public float Cooldown = 30f;

        /// <summary>Relative preference when several behaviours are eligible.</summary>
        public float Weight = 1f;

        // ---- proactive conditions (all optional) ----

        /// <summary>"Hunger" | "Energy" | "Joy" | "Cleanliness" | "Affection" | "" .</summary>
        public string RequiresNeed = "";

        /// <summary>Only fires while the named need is below this. -1 disables the test.</summary>
        public float NeedBelow = -1f;

        /// <summary>Only fires while the named need is above this. -1 disables the test.</summary>
        public float NeedAbove = -1f;

        /// <summary>Walk to this kind of object before acting. Null acts in place.</summary>
        public string TargetKind = "";

        /// <summary>Restrict to a time window (0..24). Negative disables.</summary>
        public float MinHour = -1f;
        public float MaxHour = -1f;

        /// <summary>
        /// Restrict to one place: "Garden", "Terrace", "Cabin". Empty means anywhere.
        ///
        /// This is what lets a place have its own behaviour rather than its own palette. Hopping
        /// about the garden and hiding behind a shrub are the two things a pet does outdoors that
        /// it cannot do in a room, and they are gated here rather than by rewriting the scheduler.
        /// </summary>
        public string OnlyInPlace = "";

        /// <summary>Lines the pet may say when this fires. Empty means silent.</summary>
        public string[] Lines = Array.Empty<string>();

        /// <summary>When true the pet also opens a conversation instead of only acting.</summary>
        public bool SpeakUp;

        /// <summary>When true the row runs the ball retrieve loop instead of a plain action.</summary>
        public bool Fetch;

        /// <summary>
        /// When true the pet actually leaves the ground — repeated little jumps, not a jump
        /// animation played in place. The avatar does the bouncing; this is the switch.
        /// </summary>
        public bool Hop;

        /// <summary>When true the row fires only while a mess is on the floor.</summary>
        public bool NeedsMess;

        /// <summary>When true the row cleans up a mess instead of using a target.</summary>
        public bool CleansMess;

        public InteractableKind? Target
        {
            get
            {
                if (string.IsNullOrEmpty(TargetKind)) return null;
                InteractableKind parsed;
                return Enum.TryParse(TargetKind, out parsed) ? parsed : (InteractableKind?)null;
            }
        }

        /// <summary>
        /// Whether every declared condition currently holds. Shared by both drives: the
        /// passive pool uses it to gate quirks ("only grumble when miserable"), the
        /// proactive pool to gate activities.
        /// </summary>
        public bool IsEligible(PetBehaviorContext ctx)
        {
            if (!string.IsNullOrEmpty(OnlyInPlace) && !ctx.InPlace(OnlyInPlace)) return false;

            if (MinHour >= 0f && MaxHour >= 0f)
            {
                bool inside = MinHour <= MaxHour
                    ? ctx.HourOfDay >= MinHour && ctx.HourOfDay < MaxHour
                    : ctx.HourOfDay >= MinHour || ctx.HourOfDay < MaxHour; // wraps midnight
                if (!inside) return false;
            }

            var target = Target;
            if (target.HasValue && !ctx.HasTarget(target.Value)) return false;

            // A ball sitting in its corner is not worth a trip; a ball that has just been
            // thrown across the room is the most interesting thing in the world.
            if (Fetch && !ctx.BallLoose) return false;
            if (NeedsMess && !ctx.MessPresent) return false;

            if (!string.IsNullOrEmpty(RequiresNeed))
            {
                float need = ctx.Need(RequiresNeed);
                if (NeedBelow >= 0f && need >= NeedBelow) return false;
                if (NeedAbove >= 0f && need <= NeedAbove) return false;
            }

            return true;
        }

        /// <summary>
        /// How much the pet wants to do this right now. 0 means "not applicable".
        /// Pure, so the whole table's behaviour is unit testable.
        ///
        /// The personality multiplies the result rather than replacing it, and the multiplier
        /// fades out as the need gets desperate: at half-empty the pet's character decides
        /// between two options, at nearly zero the need does. Without that fade a lively but
        /// scruffy pet would rather play than wash at 3% cleanliness forever — which is
        /// characterful right up to the point where the mud never comes off.
        /// </summary>
        public float Score(PetBehaviorContext ctx)
        {
            if (Drive != BehaviorDrive.Proactive) return 0f;
            if (!IsEligible(ctx)) return 0f;

            float urgency = string.IsNullOrEmpty(RequiresNeed)
                ? 0.5f
                : 1f - Mathf.Clamp01(ctx.Need(RequiresNeed));

            float bias = ctx.Personality != null ? ctx.Personality.BiasFor(Id) : 1f;
            bias = Mathf.Lerp(1f, bias, 1f - urgency);

            return Mathf.Max(0.01f, urgency) * Mathf.Max(0.01f, Weight) * Mathf.Max(0.05f, bias);
        }

        /// <summary>
        /// The same weighting for a passive quirk, where there is no urgency to fade against:
        /// ambient behaviour is exactly where temperament should show through at full strength.
        /// </summary>
        public float AmbientWeight(PetBehaviorContext ctx)
        {
            if (!IsEligible(ctx)) return 0f;
            float bias = ctx.Personality != null ? ctx.Personality.BiasFor(Id) : 1f;
            return Mathf.Max(0.01f, Weight * Mathf.Max(0.05f, bias));
        }

        public bool AllowsSpeech => SpeakUp || (Lines != null && Lines.Length > 0);

        public string PickLine(System.Random rng)
        {
            if (Lines == null || Lines.Length == 0) return null;
            return Lines[rng.Next(Lines.Length)];
        }
    }
}
