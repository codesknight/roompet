using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Free placement: dragging furniture around the room, and — since it must be rotatable —
    /// turning it.
    ///
    /// Two gestures, both single-touch:
    ///  - <b>drag</b> (press, move, release) puts the item somewhere else;
    ///  - <b>tap</b> (press and release without moving) rotates it 90° in place.
    ///
    /// A separate component rather than more code in the HUD because it is the only part of the
    /// HUD that talks to the physics world every frame. While it is enabled the HUD marks the
    /// room "modal", which suppresses the normal click-to-walk and poke reactions — a tap in
    /// placement mode means "rotate this", not "go use the bed".
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

        /// <summary>Below this much movement a release counts as a "tap" (rotate), not a drop.</summary>
        private const float TapMoveThreshold = 0.30f;

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
        private Vector3 _pickupFloor;
        private float _dragYaw;
        private float _dragY;
        private bool _moved;

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
                // Taps on the placement bar (旋转 / 完成) belong to the buttons, not to whatever
                // furniture sits behind them in the world.
                if (PointerOverUi()) return;

                // A press in placement mode picks furniture up; it never walks the pet.
                Dragging = RaycastFurniture(ray);
                if (Dragging != null)
                {
                    _dragOffset = onFloor ? Dragging.transform.position - floorPoint : Vector3.zero;
                    _pickupFloor = floorPoint;
                    _dragY = Dragging.transform.position.y;
                    _dragYaw = PetInventory.RotationOf(Dragging.ItemId, PetWorldMap.Current);
                    _moved = false;
                    Dragging.transform.localRotation = Quaternion.Euler(0f, _dragYaw, 0f);
                }
            }
            else if (Dragging != null && Input.GetMouseButton(0))
            {
                if (onFloor)
                {
                    Vector3 target = PetInventory.ClampToRoom(
                        new Vector2(floorPoint.x + _dragOffset.x, floorPoint.z + _dragOffset.z));

                    Vector3 next = new Vector3(target.x, _dragY, target.y);
                    Vector3 delta = next - Dragging.transform.position;

                    Dragging.transform.position = next;
                    Dragging.ApproachPoint += delta;

                    if (!_moved && Vector3.Distance(floorPoint, _pickupFloor) > TapMoveThreshold) _moved = true;

                    // The ball's resting spot is where it lands after a fetch; carry it too.
                    var ball = Dragging.GetComponent<PetBall>();
                    if (ball != null) ball.SnapToRest(next);
                }
            }
            else if (Input.GetMouseButtonUp(0) && Dragging != null)
            {
                if (_moved) SaveAndDrop();
                else RotateInPlace();
            }
        }

        /// <summary>Persists the carried furniture and lets go. Called when placement is switched off.</summary>
        public void Drop()
        {
            if (Dragging == null) return;
            if (_moved) SaveAndDrop();
            else RotateInPlace();
        }

        /// <summary>
        /// Rotates the item currently being carried, for the placement bar's rotate button.
        /// Marks the gesture as a deliberate action so the release saves instead of rotating again.
        /// </summary>
        public void RotateDragged(float deltaDegrees)
        {
            if (Dragging == null) return;
            _moved = true;
            ApplyRotation(deltaDegrees);
        }

        private void ApplyRotation(float deltaDegrees)
        {
            _dragYaw = (_dragYaw + deltaDegrees) % 360f;
            if (_dragYaw < 0f) _dragYaw += 360f;
            Dragging.transform.localRotation = Quaternion.Euler(0f, _dragYaw, 0f);
            RotateApproachPoint(deltaDegrees);
        }

        private void SaveAndDrop()
        {
            var at = Dragging.transform.position;
            Vector2 resolved = PetInventory.MoveItem(Dragging.ItemId, new Vector2(at.x, at.z), PetWorldMap.Current);
            PetInventory.SetRotation(Dragging.ItemId, PetWorldMap.Current, _dragYaw);

            // Snap the visual to exactly what was saved: the room must match the save the next
            // time the player opens the game, or furniture looks "rearranged" after a restart.
            Vector3 snapped = new Vector3(resolved.x, _dragY, resolved.y);
            Vector3 delta = snapped - at;
            Dragging.transform.position = snapped;
            Dragging.ApproachPoint += delta;
            Dragging.transform.localRotation = Quaternion.Euler(0f, _dragYaw, 0f);
            Dragging = null;
        }

        private void RotateInPlace()
        {
            _dragYaw = (_dragYaw + 90f) % 360f;
            PetInventory.SetRotation(Dragging.ItemId, PetWorldMap.Current, _dragYaw);
            Dragging.transform.localRotation = Quaternion.Euler(0f, _dragYaw, 0f);
            RotateApproachPoint(90f);
            Dragging = null;
        }

        /// <summary>Swing the approach point around the item's own centre, so the pet still walks
        /// to the item's (new) front after it is turned.</summary>
        private void RotateApproachPoint(float deltaDegrees)
        {
            var center = new Vector3(Dragging.transform.position.x, 0f, Dragging.transform.position.z);
            var offset = Dragging.ApproachPoint - center;
            float rad = deltaDegrees * Mathf.Deg2Rad;
            Dragging.ApproachPoint = center + new Vector3(
                offset.x * Mathf.Cos(rad) - offset.z * Mathf.Sin(rad),
                offset.y,
                offset.x * Mathf.Sin(rad) + offset.z * Mathf.Cos(rad));
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

        private static bool PointerOverUi()
            => UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
