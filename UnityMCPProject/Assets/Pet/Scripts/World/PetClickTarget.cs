using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Makes the pet itself a clickable object.
    ///
    /// Unity delivers OnMouseDown to the collider's own GameObject and to nothing else, so the
    /// capsule that <see cref="PetGameManager.EnsurePokeTarget"/> puts on the pet root is what
    /// makes "click the pet" possible at all. The same event arrives from a tap on a phone,
    /// because Unity synthesises mouse messages from the primary touch.
    ///
    /// Respects the modal rule like every other world object: while a panel is open the world
    /// is not the thing being pointed at, and a physics message is not blocked by IMGUI.
    /// </summary>
    public class PetClickTarget : MonoBehaviour
    {
        /// <summary>False for the companions: poking them reacts locally and costs nothing.</summary>
        public bool Primary = true;

        /// <summary>Randomises the reaction of a companion, which has no brain of its own.</summary>
        public PetSpecies Species;
        public PetPersonality Personality;

        /// <summary>
        /// Which collection record this pet is, so clicking it can hand the room over to it.
        /// Empty for the primary pet, which already owns the room.
        /// </summary>
        public string RecordId = "";

        private float _cooldown;

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        }

        private void OnMouseEnter()
        {
            if (PointerOverUi()) return;
            if (PetHud.ModalOpen) return;
            PetHud.SetCursorHint("摸一摸它");
        }

        private void OnMouseExit() => PetHud.SetCursorHint(null);

        private void OnMouseDown()
        {
            if (PointerOverUi()) return;
            if (PetHud.ModalOpen) return;

            // Which pet is being touched is also which pet the status card should be about, and
            // that has to happen before the cooldown guard: selecting is not a reaction, and a
            // tap during a reaction still means "I am looking at this one".
            var manager = PetGameManager.Instance;
            if (manager != null) manager.SelectPetObject(gameObject);

            // A pet that is being tapped repeatedly should not machine-gun reactions.
            if (_cooldown > 0f) return;
            _cooldown = 0.6f;

            if (!Primary)
            {
                // A companion: the same reaction table, no journal and no manager state.
                var reaction = PetInteraction.Choose(new PetBehaviorContext
                {
                    Hunger = 0.7f, Energy = 0.7f, Joy = 0.6f, Cleanliness = 0.8f, Bladder = 0.8f,
                    Affection = 0.4f, Personality = Personality
                }, Random.value);

                var avatar = GetComponent<PetAvatar>();
                if (avatar != null) avatar.PlayAction(PetInteraction.Action(reaction), 1.6f);

                PetAudioDirector.Instance?.Speak(Species, Personality,
                    PetInteraction.MoodFor(reaction, PetMood.Content));
                DshMobile.MobileHaptics.Light();
                return;
            }

            if (manager != null) manager.PokePet();
        }

        private static bool PointerOverUi()
            => UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
