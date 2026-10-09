using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Free placement: dragging furniture around the room.
    ///
    /// A separate component rather than more code in the HUD because it is the only part of the
    /// HUD that talks to the physics world every frame. While it is enabled the HUD marks the
    /// room "modal", which suppresses the normal click-to-walk and poke reactions — a tap in
    /// placement mode means "pick this up", not "go use the bed".
    ///
    /// It reads the primary touch through <see cref="Input.mousePosition"/>, exactly like every
    /// other click in this project, so a phone and a desktop share the one path.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class PlacementDragger : MonoBehaviour
    {
        public static PlacementDragger Instance { get; private set; }

        /// <summary>The furniture being carried, or null when nothing is held.</summary>
        public Interactable Dragging { get; private set; }

        public static bool Active => Instance != null && Instance.enabled;

        /// <summary>Turns placement on or off. The object is created on first use.</summary>
        public static void SetActive(bool on)
        {
            if (on)
            {
                var dragger = Instance;
                if (dragger == null)
                {
                    var go = new GameObject("PlacementDragger");
                    dragger = go.AddComponent<PlacementDragger>();
                }
                dragger.enabled = true;
            }
            else if (Instance != null)
            {
                Instance.Drop();
                Instance.enabled = false;
            }
        }

        private Camera _camera;
        private Vector3 _dragOffset;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _camera = Camera.main;
            if (_camera == null) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            var floor = new Plane(Vector3.up, Vector3.zero);
            float enter;
            bool onFloor = floor.Raycast(ray, out enter);
            Vector3 floorPoint = onFloor ? ray.GetPoint(enter) : Vector3.zero;

            if (Input.GetMouseButtonDown(0))
            {
                // A press in placement mode picks furniture up; it never walks the pet.
                Dragging = RaycastFurniture(ray);
                if (Dragging != null)
                {
                    _dragOffset = onFloor ? Dragging.transform.position - floorPoint : Vector3.zero;
                }
            }
            else if (Dragging != null && Input.GetMouseButton(0))
            {
                if (onFloor)
                {
                    Vector3 target = PetInventory.ClampToRoom(
                        new Vector2(floorPoint.x + _dragOffset.x, floorPoint.z + _dragOffset.z));

                    Vector3 next = new Vector3(target.x, 0f, target.y);
                    Vector3 delta = next - Dragging.transform.position;

                    Dragging.transform.position = next;
                    Dragging.ApproachPoint += delta;

                    // The ball's resting spot is where it lands after a fetch; carry it too.
                    var ball = Dragging.GetComponent<PetBall>();
                    if (ball != null) ball.SnapToRest(next);
                }
            }
            else if (Input.GetMouseButtonUp(0) && Dragging != null)
            {
                SaveAndDrop();
            }
        }

        /// <summary>Persists the carried furniture's position and lets go.</summary>
        public void Drop()
        {
            if (Dragging != null) SaveAndDrop();
        }

        private void SaveAndDrop()
        {
            var at = Dragging.transform.position;
            PetInventory.MoveItem(Dragging.ItemId, new Vector2(at.x, at.z));
            Dragging = null;
        }

        private static Interactable RaycastFurniture(Ray ray)
        {
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 250f)) return null;

            var interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable == null) return null;

            // Only furniture the shop/inventory knows about can be moved: the door, the messes
            // and the pets stay where they are.
            if (string.IsNullOrEmpty(interactable.ItemId)) return null;

            // A bowl the pet is currently at, or the bed it is asleep in, is still movable —
            // the player is rearranging, and the pet will adapt when it next walks.
            return interactable;
        }
    }
}
