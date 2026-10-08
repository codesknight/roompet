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

        /// <summary>Long-term bond, 0..1. Only grows, and only from attention.</summary>
        public float Affection = 0.1f;

        // Seconds for each need to fall from 1 to 0 at DecayScale 1.
        public const float HungerPeriod = 300f;
        public const float EnergyPeriod = 420f;
        public const float JoyPeriod = 240f;
        public const float CleanPeriod = 600f;

        public float HungerRate => 1f / HungerPeriod;
        public float EnergyRate => 1f / EnergyPeriod;
        public float JoyRate => 1f / JoyPeriod;
        public float CleanRate => 1f / CleanPeriod;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            float dt = deltaSeconds * Mathf.Max(0.01f, DecayScale);

            Hunger = Mathf.Clamp01(Hunger - HungerRate * dt);
            Cleanliness = Mathf.Clamp01(Cleanliness - CleanRate * dt);

            // Energy drains faster while awake and restless, and recovers while asleep.
            float energyDrain = EnergyRate * dt * (Joy < 0.3f ? 1.35f : 1f);
            Energy = Mathf.Clamp01(Energy - energyDrain);

            // Joy sags faster when the pet is hungry or filthy: neglect compounds.
            float joyDrain = JoyRate * dt * (Hunger < 0.3f ? 1.4f : 1f) * (Cleanliness < 0.3f ? 1.3f : 1f);
            Joy = Mathf.Clamp01(Joy - joyDrain);
        }

        public void Feed(float amount = 0.55f)
        {
            Hunger = Mathf.Clamp01(Hunger + amount);
            Joy = Mathf.Clamp01(Joy + amount * 0.15f);
        }

        public void GiveWater(float amount = 0.3f)
        {
            Hunger = Mathf.Clamp01(Hunger + amount * 0.35f);
            Joy = Mathf.Clamp01(Joy + amount * 0.1f);
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
            Hunger * 0.30f + Energy * 0.25f + Joy * 0.30f + Cleanliness * 0.15f;

        /// <summary>The need most worth acting on, or an empty string when comfortable.</summary>
        public string DominantNeed
        {
            get
            {
                float worst = 0.45f;
                string name = "";
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
            Affection = status.Affection;
        }
    }
}
