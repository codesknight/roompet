using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The apple tree's own state: how many apples it is bearing.
    ///
    /// The growth arithmetic lives in <see cref="GardenRules"/> so it can be tested; this
    /// component is the clock that runs it. Apples grow back over time and the tree can be
    /// watered (with the bucket) for an instant extra apple — the garden's own closed loop.
    /// </summary>
    public class PetAppleTree : MonoBehaviour
    {
        /// <summary>Apples on the tree right now, 0..<see cref="GardenRules.TreeMaxApples"/>.</summary>
        public int Apples { get; private set; }

        private float _nextAppleAt = -1f;

        private void Awake()
        {
            if (_nextAppleAt < 0f) _nextAppleAt = Time.time + GardenRules.AppleGrowSeconds;
        }

        private void Update()
        {
            if (Apples >= GardenRules.TreeMaxApples) return;
            if (_nextAppleAt < 0f) _nextAppleAt = Time.time + GardenRules.AppleGrowSeconds;
            if (Time.time < _nextAppleAt) return;

            Apples = GardenRules.ClampApples(Apples + 1);
            _nextAppleAt = Time.time + GardenRules.AppleGrowSeconds;
        }

        /// <summary>Takes every apple and returns how many were taken.</summary>
        public int PickAll()
        {
            int taken = Apples;
            Apples = 0;
            return taken;
        }

        /// <summary>The pet eats one apple straight off the branch.</summary>
        public bool EatOne()
        {
            if (Apples <= 0) return false;
            Apples--;
            return true;
        }

        /// <summary>Watering with the bucket grows an extra apple immediately.</summary>
        public void Water() => Apples = GardenRules.AfterWatering(Apples);

        /// <summary>For tests: sets the count directly, bypassing the clock.</summary>
        public void SetApplesForTests(int apples) => Apples = GardenRules.ClampApples(apples);
    }
}
