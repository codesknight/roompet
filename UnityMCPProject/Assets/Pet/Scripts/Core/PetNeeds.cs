using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The pet's drives. Pure data plus arithmetic, so the decay curve and the mood
    /// mapping can be unit tested without a scene.
    ///
    /// All four needs run 0..1 where 1 is "completely satisfied".
    /// </summary>
    [Serializable]
    public class PetNeeds
    {
        /// <summary>Multiplier on every decay rate: raise it to watch the demo change
        /// quickly, lower it for a slow-burn companion.</summary>
        public float DecayScale = 3f;

        public float Hunger = 0.85f;
        public float Energy = 0.85f;
        public float Joy = 0.7f;
        public float Cleanliness = 0.9f;

        /// <summary>
        /// How empty the pet is, 1 being comfortable. Fills up from eating and drinking, and
        /// unlike the other needs it cannot simply be ignored: past zero the pet has an
        /// accident in the room, which leaves a mess the player has to clean up.
        /// </summary>
        public float Bladder = 0.9f;

        /// <summary>Long-term bond, 0..1. Only grows, and only from attention.</summary>
        public float Affection = 0.1f;

        // Seconds for each need to fall from 1 to 0 at DecayScale 1.
        public const float HungerPeriod = 300f;
        public const float EnergyPeriod = 420f;
        public const float JoyPeriod = 240f;
        public const float CleanPeriod = 600f;

        /// <summary>Bladder is not time-based; this is only the trickle of a slow metabolism.</summary>
        public const float BladderPeriod = 900f;

        public float HungerRate => 1f / HungerPeriod;
        public float EnergyRate => 1f / EnergyPeriod;
        public float JoyRate => 1f / JoyPeriod;
        public float CleanRate => 1f / CleanPeriod;
        public float BladderRate => 1f / BladderPeriod;

        /// <summary>Set by the owner each frame: the pet's temperament scales the decay.</summary>
        public PetPersonality Personality;

        /// <summary>
        /// Place modifiers, pushed by the owner each frame.
        ///
        /// A garden makes a pet happier and dirtier, a night terrace tires it out faster — this
        /// is what "a different place puts the pet in a different state" means in numbers.
        /// </summary>
        public float JoyDrainScale = 1f;
        public float EnergyDrainScale = 1f;
        public float CleanDrainScale = 1f;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            float dt = deltaSeconds * Mathf.Max(0.01f, DecayScale);

            Hunger = Mathf.Clamp01(Hunger - HungerRate * dt);

            float cleanRate = CleanRate * CleanDrainScale * (Personality != null ? Personality.CleanDrainScale : 1f);
            Cleanliness = Mathf.Clamp01(Cleanliness - cleanRate * dt);

            Bladder = Mathf.Clamp01(Bladder - BladderRate * dt);

            // Energy drains faster while awake and restless, and recovers while asleep.
            float energyScale = EnergyDrainScale * (Personality != null ? Personality.EnergyDrainScale : 1f);
            float energyDrain = EnergyRate * dt * energyScale * (Joy < 0.3f ? 1.35f : 1f);
            Energy = Mathf.Clamp01(Energy - energyDrain);

            // Joy sags faster when the pet is hungry or filthy: neglect compounds.
            float joyScale = JoyDrainScale * (Personality != null ? Personality.JoyDrainScale : 1f);
            float joyDrain = JoyRate * dt * joyScale * (Hunger < 0.3f ? 1.4f : 1f) * (Cleanliness < 0.3f ? 1.3f : 1f);
            Joy = Mathf.Clamp01(Joy - joyDrain);
        }

        public void Feed(float amount = 0.55f)
        {
            Hunger = Mathf.Clamp01(Hunger + amount);
            Joy = Mathf.Clamp01(Joy + amount * 0.15f);

            // Eating and drinking are what fill the bladder; a well-fed pet needs the tray
            // sooner, which is the whole point of the loop.
            Bladder = Mathf.Clamp01(Bladder - amount * 0.35f);
        }

        public void GiveWater(float amount = 0.3f)
        {
            Hunger = Mathf.Clamp01(Hunger + amount * 0.35f);
            Joy = Mathf.Clamp01(Joy + amount * 0.1f);
            Bladder = Mathf.Clamp01(Bladder - amount * 0.45f);
        }

        /// <summary>Relief at the litter tray. Also a small comfort — a pet that has been
        /// holding it is visibly happier afterwards.</summary>
        public void Relieve()
        {
            Bladder = 1f;
            Joy = Mathf.Clamp01(Joy + 0.12f);
        }

        /// <summary>
        /// The pet could not hold it. Costs cleanliness and joy, and — unlike the other needs —
        /// leaves something in the room for the player to deal with.
        /// </summary>
        public void Accident()
        {
            Bladder = 1f;
            Cleanliness = Mathf.Clamp01(Cleanliness - 0.35f);
            Joy = Mathf.Clamp01(Joy - 0.20f);
        }

        public void Sleep(float seconds)
        {
            Energy = Mathf.Clamp01(Energy + seconds * 0.25f);
        }

        /// <summary>
        /// Playing cheers the pet up. Only play the player asked for deepens the bond —
        /// a pet that bonds with itself would drift to maximum affection unattended.
        /// </summary>
        public void Play(float amount = 0.35f, bool fromPlayer = true)
        {
            if (Personality != null) amount *= Personality.PlayJoyScale;
            Joy = Mathf.Clamp01(Joy + amount);
            Energy = Mathf.Clamp01(Energy - amount * 0.35f);
            Hunger = Mathf.Clamp01(Hunger - amount * 0.15f);
            if (fromPlayer) AddAffection(amount * 0.5f);
        }

        public void Clean(float amount = 0.8f)
        {
            Cleanliness = Mathf.Clamp01(Cleanliness + amount);
            Joy = Mathf.Clamp01(Joy + amount * 0.1f);
            AddAffection(0.05f);
        }

        public void Pet(float amount = 0.2f)
        {
            Joy = Mathf.Clamp01(Joy + amount);
            AddAffection(amount * 0.6f);
        }

        public void AddAffection(float amount)
        {
            Affection = Mathf.Clamp01(Affection + Mathf.Max(0f, amount));
        }

        /// <summary>Weighted 0..1 wellbeing. Affection nudges it because a bonded pet is
        /// happier at the same need levels.</summary>
        public float MoodScore =>
            Hunger * 0.28f + Energy * 0.22f + Joy * 0.27f + Cleanliness * 0.13f + Bladder * 0.10f;

        /// <summary>True when the pet is about to have an accident if it cannot reach the tray.</summary>
        public bool BladderCritical => Bladder < 0.18f;

        /// <summary>The need most worth acting on, or an empty string when comfortable.</summary>
        public string DominantNeed
        {
            get
            {
                float worst = 0.45f;
                string name = "";
                if (Bladder < 0.35f) { worst = Bladder; name = "想上厕所"; }
                if (Hunger < worst) { worst = Hunger; name = "饿了"; }
                if (Energy < worst) { worst = Energy; name = "困了"; }
                if (Joy < worst) { worst = Joy; name = "想玩"; }
                if (Cleanliness < worst) { worst = Cleanliness; name = "想洗澡"; }
                return name;
            }
        }

        public PetMood Mood
        {
            get
            {
                if (Bladder < 0.14f) return PetMood.NeedsToilet;
                if (Energy < 0.20f) return PetMood.Sleepy;
                if (Hunger < 0.20f) return PetMood.Hungry;
                if (Cleanliness < 0.25f) return PetMood.Dirty;
                if (Joy < 0.22f) return Affection < 0.35f ? PetMood.Lonely : PetMood.Bored;
                if (Joy > 0.80f && Affection > 0.45f) return PetMood.Excited;
                if (MoodScore > 0.62f) return PetMood.Happy;
                return PetMood.Content;
            }
        }

        public PetStatus Snapshot() => new PetStatus
        {
            Mood = Mood,
            Hunger = Hunger,
            Energy = Energy,
            Joy = Joy,
            Cleanliness = Cleanliness,
            Bladder = Bladder,
            Affection = Affection,
            MoodScore = MoodScore,
            DominantNeed = DominantNeed
        };

        public void CopyFrom(PetStatus status)
        {
            Hunger = status.Hunger;
            Energy = status.Energy;
            Joy = status.Joy;
            Cleanliness = status.Cleanliness;
            Bladder = status.Bladder;
            Affection = status.Affection;
        }
    }
}
