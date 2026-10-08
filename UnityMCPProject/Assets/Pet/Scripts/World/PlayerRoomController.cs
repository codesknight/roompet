using DshMobile;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Walks the player's character around the room.
    ///
    /// Movement is camera-relative (W is always "away from the camera"), which is what makes
    /// a third-person room feel right regardless of where the camera has drifted. The pet is
    /// told where the player is so it can come over on its own.
    /// </summary>
    public class PlayerRoomController : MonoBehaviour
    {
        [Header("Wiring")]
        public PlayerAvatar Avatar;
        public PetRoom Room;
        public PetController Pet;

        [Header("Movement")]
        public float WalkSpeed = 3.1f;
        public float Acceleration = 14f;
        public float TurnSmoothing = 11f;

        [Header("Interaction")]
        public float InteractRange = 2.1f;
        public KeyCode InteractKey = KeyCode.E;

        /// <summary>The closest usable object, for the "press E" prompt.</summary>
        public Interactable Nearby { get; private set; }

        /// <summary>True while the player is close enough for the pet to notice.</summary>
        public bool IsNearPet => Pet != null &&
            Vector3.Distance(transform.position, Pet.transform.position) < 3.2f;

        private CharacterController _controller;
        private Vector3 _velocity;

        private void Awake()
        {
            Avatar = Avatar != null ? Avatar : GetComponentInChildren<PlayerAvatar>();
        }

        private void Start()
        {
            if (Avatar == null)
            {
                var go = new GameObject("PlayerVisual");
                go.transform.SetParent(transform, false);
                Avatar = go.AddComponent<PlayerAvatar>();
                Avatar.Build(false);
            }
        }

        public void SnapTo(Vector3 position)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            var gm = PetGameManager.Instance;
            if (gm == null) return;

            // Typing in the chat box must not also walk the character around, and neither
            // should WASD while a panel (notebook, settings, door) is open over the room.
            bool typing = PetHud.IsTextInputFocused || PetHud.ModalOpen;
            Vector3 input = typing ? Vector3.zero : ReadInput() + ReadTouchInput();
            input = Vector3.ClampMagnitude(input, 1f);

            Vector3 wish = ToCameraSpace(input) * WalkSpeed;

            // Ease toward the wish velocity so starts and stops are not instant.
            _velocity = Vector3.Lerp(_velocity, wish, 1f - Mathf.Exp(-Acceleration * Time.deltaTime));

            Vector3 next = transform.position + _velocity * Time.deltaTime;
            next = ClampToRoom(next);
            transform.position = next;

            float speed01 = WalkSpeed > 0.01f ? Mathf.Clamp01(_velocity.magnitude / WalkSpeed) : 0f;
            if (Avatar != null)
            {
                Avatar.SetLocomotion(speed01);
                if (speed01 > 0.05f) Avatar.FaceTowards(transform.position + _velocity, TurnSmoothing);
            }

            TickFootsteps(speed01);
            UpdateNearby();

            // One "use" verb for both platforms: E on the keyboard, the on-screen action
            // button on a phone. Picking the ball up wins over the generic "send the pet
            // over" interaction when the ball is the nearest object.
            bool interact = !typing &&
                            (Input.GetKeyDown(InteractKey) || MobileTouch.Pressed(MobileButtonIds.PetAction));

            bool consumed = TickBall(typing, interact);
            if (!typing && !consumed && interact && Nearby != null)
            {
                Nearby.Interact();
            }
        }

        /// <summary>
        /// Virtual stick input, shaped like WASD so it can go through exactly the same
        /// camera-relative path: x = strafe, z = forward.
        /// </summary>
        private static Vector3 ReadTouchInput()
        {
            if (!MobileTouch.PlayInputEnabled) return Vector3.zero;

            Vector2 stick = MobileTouch.Stick.Value;
            return new Vector3(stick.x, 0f, stick.y);
        }

        // --------------------------------------------------------------------- ball

        /// <summary>The ball in the room, if there is one.</summary>
        public PetBall Ball { get; private set; }

        /// <summary>Where the player is aiming, on the floor plane.</summary>
        public Vector3 AimPoint { get; private set; }

        /// <summary>Returns true when the press was used to pick the ball up.</summary>
        private bool TickBall(bool typing, bool interact)
        {
            if (Ball == null)
            {
                Ball = FindObjectOfType<PetBall>();
                if (Ball == null) return false;
            }

            if (Ball.State == BallState.Held)
            {
                // Aiming and charging differ per platform: the mouse can point anywhere,
                // a thumb cannot. On touch the stick aims (falling back to where the
                // character is facing) and the throw button charges while held.
                Vector3 aim;
                bool charging;
                bool released;

                if (MobileUi.UseTouchControls)
                {
                    Vector2 stick = MobileTouch.Stick.Value;
                    aim = stick.sqrMagnitude > 0.04f
                        ? transform.position + new Vector3(stick.x, 0f, stick.y) * 5f
                        : transform.position + transform.forward * 5f;
                    charging = MobileTouch.Held(MobileButtonIds.PetThrow);
                    released = MobileTouch.Released(MobileButtonIds.PetThrow);
                }
                else
                {
                    aim = MouseGroundPoint();
                    charging = Input.GetMouseButton(0);
                    released = Input.GetMouseButtonUp(0);
                }

                AimPoint = aim;

                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(Flat(aim - transform.position), Vector3.up),
                    1f - Mathf.Exp(-14f * Time.deltaTime));

                if (charging) Ball.BeginCharge(Time.deltaTime);
                else if (released) Ball.Throw(aim - transform.position);
                return false;
            }

            if (!typing && interact && Ball.IsAtRest && IsNear(Ball.transform.position))
            {
                Ball.PickUp(transform);
                return true;
            }

            return false;
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude < 0.0001f ? Vector3.forward : value.normalized;
        }

        public bool IsNear(Vector3 worldPoint, float range = -1f)
            => Vector3.Distance(transform.position, worldPoint) <= (range > 0f ? range : InteractRange);

        /// <summary>Ray from the camera onto the floor plane, so aiming feels like pointing.</summary>
        private static Vector3 MouseGroundPoint()
        {
            var camera = Camera.main;
            if (camera == null) return Vector3.zero;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        /// <summary>Steps keyed to distance travelled, so the cadence matches the walk cycle.</summary>
        private void TickFootsteps(float speed01)
        {
            if (PetAudioDirector.Instance == null) return;

            _stepDistance += _velocity.magnitude * Time.deltaTime;
            if (_stepDistance < StepLength) return;

            _stepDistance = 0f;
            if (speed01 > 0.15f)
            {
                PetAudioDirector.Instance.Play(SfxId.Footstep, speed01 * 0.5f, 0.14f);
            }
        }

        private float _stepDistance;
        public float StepLength = 0.62f;

        private static Vector3 ReadInput()
        {
            float x = 0f, z = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;

            var value = new Vector3(x, 0f, z);
            return value.sqrMagnitude > 1f ? value.normalized : value;
        }

        private static Vector3 ToCameraSpace(Vector3 input)
        {
            var camera = Camera.main;
            if (camera == null) return input;

            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = camera.transform.right;
            right.y = 0f;
            right.Normalize();

            return right * input.x + forward * input.z;
        }

        private Vector3 ClampToRoom(Vector3 position)
        {
            float limit = (Room != null ? Room.Size : 14f) * 0.5f - 0.7f;
            return new Vector3(
                Mathf.Clamp(position.x, -limit, limit),
                0f,
                Mathf.Clamp(position.z, -limit, limit));
        }

        private void UpdateNearby()
        {
            Nearby = null;
            if (Room == null) return;

            float best = InteractRange;
            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                var item = Room.Interactables[i];
                if (item == null) continue;

                float distance = Vector3.Distance(transform.position, item.transform.position);
                if (distance <= best)
                {
                    best = distance;
                    Nearby = item;
                }
            }
        }
    }
}
