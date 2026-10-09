using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// The pond's own state: is a line out, and has a fish bitten.
    ///
    /// Fishing is a small timed interaction rather than a separate scene: cast, wait out the
    /// bite, pull up a fish. The pet, meanwhile, just drinks from the same pond.
    /// </summary>
    public class PetPond : MonoBehaviour
    {
        /// <summary>Seconds since a cast, -1 when no line is in the water.</summary>
        private float _biteAt = -1f;

        /// <summary>True while a line is out.</summary>
        public bool IsFishing => _biteAt > 0f;

        /// <summary>True when the fish has taken the bait and can be reeled in.</summary>
        public bool FishReady => IsFishing && Time.time >= _biteAt;

        /// <summary>Throws the line in. A line that is already out is left alone.</summary>
        public void Cast()
        {
            if (!IsFishing) _biteAt = Time.time + GardenRules.FishBiteSeconds;
        }

        /// <summary>Reels in a fish if one has bitten. Returns false when nothing is biting.</summary>
        public bool TryCatch()
        {
            if (!FishReady) return false;
            _biteAt = -1f;
            return true;
        }

        /// <summary>Pulls the line out empty, e.g. when the player walks away.</summary>
        public void Cancel() => _biteAt = -1f;

        /// <summary>For tests: make the next cast bite immediately.</summary>
        public void ForceBiteForTests() => _biteAt = Time.time;
    }
}
