using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// A thing in the room the player can click. The pet walks to
    /// <see cref="ApproachPoint"/> and then performs the action, which is what makes the
    /// environment feel interactive rather than decorative.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public InteractableKind Kind = InteractableKind.Food;
        public string Label = "";

        /// <summary>
        /// The shop id this item corresponds to ("" for the door and messes). Free placement
        /// moves the object and writes the new position back under this id.
        /// </summary>
        public string ItemId = "";

        /// <summary>Where the pet stands to use this. Defaults to a spot in front of it.</summary>
        public Vector3 ApproachPoint;

        public float CooldownSeconds = 2.5f;

        [NonSerialized] public float LastUsedAt = -999f;
        [NonSerialized] public bool Hovered;

        public bool IsReady => Time.time - LastUsedAt >= CooldownSeconds;

        public event Action<Interactable> Clicked;

        private Renderer[] _renderers;
        private Vector3 _baseScale = Vector3.one;
        private float _pulse;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private bool _colorCaptured;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _baseScale = transform.localScale;
            if (ApproachPoint == Vector3.zero)
            {
                ApproachPoint = transform.position + new Vector3(0f, 0f, -1.1f);
            }
        }

        private void OnMouseEnter()
        {
            if (PointerOverUi()) return;
            // A panel is open over the room: the world is not the thing being pointed at.
            if (PetHud.ModalOpen) return;

            Hovered = true;
            PetHud.SetCursorHint(Label + "　（点击让宠物过来）");
        }

        private void OnMouseExit()
        {
            Hovered = false;
            PetHud.SetCursorHint(null);
        }

        private void OnMouseDown()
        {
            // A tap over a uGUI panel/button belongs to the UI, not the object behind it. The
            // old ModalOpen check misses the always-on pet card and the mobile touch buttons, so
            // tapping a panel button also walked the pet to whatever interactable sat behind it.
            if (PointerOverUi()) return;
            if (PetHud.ModalOpen) return;
            Interact();
        }

        private static bool PointerOverUi()
            => UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        /// <summary>Raise the click. Public so the player character's "press E" path can use
        /// exactly the same route as a mouse click.</summary>
        public void Interact() => Clicked?.Invoke(this);

        /// <summary>Called by the pet once it has arrived and used the object.</summary>
        public void MarkUsed()
        {
            LastUsedAt = Time.time;
        }

        private void Update()
        {
            // Gentle hover feedback: grow a touch and pulse brighter.
            float target = Hovered ? 1.12f : 1f;
            _pulse = Mathf.Lerp(_pulse, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            transform.localScale = _baseScale * _pulse;

            if (_renderers == null || _renderers.Length == 0) return;

            if (!_colorCaptured)
            {
                _colorCaptured = true;
                var shared = _renderers[0].sharedMaterial;
                if (shared != null && shared.HasProperty(ColorId)) _baseColor = shared.GetColor(ColorId);
            }

            if (_block == null) _block = new MaterialPropertyBlock();
            Color tint = Hovered ? Color.Lerp(_baseColor, Color.white, 0.55f) : _baseColor;
            if (!IsReady) tint *= 0.55f;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(ColorId, tint);
                _renderers[i].SetPropertyBlock(_block);
            }
        }
    }
}
