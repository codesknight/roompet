using UnityEngine;

namespace DshRunner
{
    /// <summary>
    /// The runner. Auto-runs forward, switches lanes, jumps and slides.
    ///
    /// Hit resolution is logic-based rather than collider-shape-based: every obstacle is
    /// a generous trigger volume, and <see cref="ResolveObstacle"/> decides whether the
    /// player actually cleared it. That keeps jump/slide tuning in one place and avoids
    /// depending on collider resizing.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Feel")]
        public float LaneChangeSmoothing = 1f;

        [Tooltip("Forward tumble per metre travelled, in degrees. 0 keeps the cube " +
                 "upright (default) — a continuous tumble is disorienting at speed. " +
                 "Raise it (10-25) for a rolling-cube look, or ~90 for a physical " +
                 "face-over-face tumble.")]
        public float RollDegreesPerMetre = 0f;

        [Tooltip("Forward lean, in degrees, at the level's top speed. Reads as " +
                 "'leaning into the run' without spinning.")]
        public float SpeedLeanDegrees = 9f;

        [Tooltip("Bank angle applied while changing lanes.")]
        public float TiltDegrees = 26f;

        /// <summary>Y of the cube's centre while standing on the track. The floor's top
        /// face is at y = 0, so this is half the cube's height.</summary>
        public float GroundY = 0.5f;

        [Header("Runtime")]
        public int Lane = 1;
        public bool IsGrounded = true;
        public bool IsSliding { get; private set; }

        public float TraveledDistance { get; private set; }

        private Rigidbody _body;
        private float _laneX;
        private float _laneFromX;
        private float _laneT;
        private float _verticalVelocity;
        private float _slideTimer;
        private float _baseY;
        private float _visualRoll;
        private float _speedLean;
        private float _tilt;
        private float _squash = 1f;
        private Vector3 _baseScale = Vector3.one;
        private Transform _visual;
        private float _jumpGravity;
        private float _jumpImpulse;
        private float _pendingTravel;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            // Interpolation must stay off: on a kinematic body it makes Unity drive the
            // transform from the physics state, which silently eats part of the
            // transform-based movement below and desyncs the distance counter.
            _body.interpolation = RigidbodyInterpolation.None;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            _baseScale = transform.localScale;
            _baseY = GroundY;
            _laneX = transform.position.x;
            _laneFromX = _laneX;
            transform.position = new Vector3(_laneX, _baseY, transform.position.z);

            _jumpGravity = 8f * GameConfig.JumpHeight / (GameConfig.JumpDuration * GameConfig.JumpDuration);
            _jumpImpulse = 4f * GameConfig.JumpHeight / GameConfig.JumpDuration;

            // A child used for roll/tilt/squash so the root transform stays axis-aligned
            // for collision bookkeeping.
            if (transform.childCount > 0 && transform.GetChild(0).name == "Visual")
            {
                _visual = transform.GetChild(0);
            }
            else
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(transform, false);
                _visual = visual.transform;
            }
        }

        public void ResetForRun(float x, float z)
        {
            Lane = GameConfig.LaneCount / 2;
            _laneX = x;
            _laneFromX = x;
            _laneT = 1f;
            _verticalVelocity = 0f;
            _slideTimer = 0f;
            IsSliding = false;
            IsGrounded = true;
            TraveledDistance = 0f;
            _pendingTravel = 0f;
            _visualRoll = 0f;
            _speedLean = 0f;
            _tilt = 0f;
            _squash = 1f;

            transform.position = new Vector3(x, _baseY, z);
            transform.rotation = Quaternion.identity;
            ApplyVisual();
        }

        // ------------------------------------------------------------------- input

        private void Update()
        {
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.State == GameState.Playing;
            if (!playing) return;

            ReadInput();
            float speed = Mathf.Max(0f, gm.CurrentSpeed);

            UpdateLane(Time.deltaTime);
            UpdateVertical(Time.deltaTime);
            UpdateSlide(Time.deltaTime);

            float forward = speed * Time.deltaTime;
            TraveledDistance += forward;
            _pendingTravel += forward;

            Vector3 pos = transform.position;
            pos.x = _laneX;
            pos.y = _baseY + _jumpOffset;
            pos.z += forward;
            transform.position = pos;

            _visualRoll = (_visualRoll + forward * RollDegreesPerMetre) % 360f;

            // Lean into the run instead of tumbling: a small forward pitch that grows
            // with speed, which reads as motion without making the cube hard to track.
            float topSpeed = gm.Level != null ? Mathf.Max(1f, gm.Level.MaxSpeed) : GameConfig.BaseSpeedFallback;
            float leanTarget = SpeedLeanDegrees * Mathf.Clamp01(speed / topSpeed);
            _speedLean = Mathf.Lerp(_speedLean, leanTarget, 1f - Mathf.Exp(-3f * Time.deltaTime));

            ApplyVisual();
        }

        private float _jumpOffset;

        /// <summary>
        /// Keyboard and touch, side by side.
        ///
        /// Touch uses swipes as the primary gesture because that is what players expect from
        /// a mobile runner — swipe sideways to change lane, up to jump, down to slide, tap to
        /// jump. The on-screen buttons are an alternative for people who prefer holding a
        /// control, and both work at once (the button layer is multi-touch; IMGUI alone is
        /// not).
        /// </summary>
        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MoveLane(-1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MoveLane(1);
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space)) Jump();
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Slide();

            if (!DshMobile.MobileUi.UseTouchControls) return;

            var gesture = DshMobile.MobileTouch.Gesture;

            switch (gesture.Swipe)
            {
                case DshMobile.SwipeDirection.Left: MoveLane(-1); break;
                case DshMobile.SwipeDirection.Right: MoveLane(1); break;
                case DshMobile.SwipeDirection.Up: Jump(); break;
                case DshMobile.SwipeDirection.Down: Slide(); break;
            }
            if (gesture.Swipe != DshMobile.SwipeDirection.None) gesture.ConsumeSwipe();

            if (gesture.Tapped)
            {
                Jump();
                gesture.ConsumeTap();
            }

            if (DshMobile.MobileTouch.Pressed(DshMobile.MobileButtonIds.RunnerLeft)) MoveLane(-1);
            if (DshMobile.MobileTouch.Pressed(DshMobile.MobileButtonIds.RunnerRight)) MoveLane(1);
            if (DshMobile.MobileTouch.Pressed(DshMobile.MobileButtonIds.RunnerJump)) Jump();
            if (DshMobile.MobileTouch.Pressed(DshMobile.MobileButtonIds.RunnerSlide)) Slide();
        }

        /// <summary>True while the player is asking for a jump — keyboard or on-screen button.</summary>
        private static bool JumpHeld()
        {
            bool keyboard = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space);
            if (keyboard) return true;

            return DshMobile.MobileUi.UseTouchControls &&
                   DshMobile.MobileTouch.Held(DshMobile.MobileButtonIds.RunnerJump);
        }

        public void MoveLane(int direction)
        {
            int next = Mathf.Clamp(Lane + direction, 0, GameConfig.LaneCount - 1);
            if (next == Lane) return;
            Lane = next;
            _laneFromX = _laneX;
            _laneT = 0f;
            _tilt = -direction * TiltDegrees;
            DshMobile.MobileHaptics.Light();
        }

        public void Jump()
        {
            if (!IsGrounded) return;
            IsGrounded = false;
            CancelSlide();
            _verticalVelocity = _jumpImpulse;
            _squash = 0.82f;
            DshMobile.MobileHaptics.Light();
        }

        public void Slide()
        {
            if (IsSliding) return;
            IsSliding = true;
            _slideTimer = GameConfig.SlideDuration;
            // Lie flat ("趴下"), not just crouch: a slide is a belly-dive under the bar. The old
            // 1.25 + SlideScaleY shrink made the whole character look like it was shrinking.
            _squash = 0.35f;
            DshMobile.MobileHaptics.Light();
        }

        private void CancelSlide()
        {
            if (!IsSliding) return;
            IsSliding = false;
            _slideTimer = 0f;
        }

        // ------------------------------------------------------------------ motion

        private void UpdateLane(float dt)
        {
            float target = GameConfig.LaneX(Lane);
            float duration = Mathf.Max(0.01f, GameConfig.LaneChangeTime / Mathf.Max(0.2f, LaneChangeSmoothing));
            _laneT = Mathf.Min(1f, _laneT + dt / duration);
            float eased = 1f - Mathf.Pow(1f - _laneT, 3f); // ease-out cubic
            _laneX = Mathf.Lerp(_laneFromX, target, eased);

            _tilt = Mathf.Lerp(_tilt, 0f, 1f - Mathf.Exp(-8f * dt));
        }

        private void UpdateVertical(float dt)
        {
            if (IsGrounded && _verticalVelocity <= 0f)
            {
                _jumpOffset = 0f;
                return;
            }

            bool holding = JumpHeld();
            float gravity = _jumpGravity * (holding && _verticalVelocity > 0f ? 1f : 1.7f);

            _verticalVelocity -= gravity * dt;
            _jumpOffset += _verticalVelocity * dt;

            if (_jumpOffset <= 0f)
            {
                _jumpOffset = 0f;
                _verticalVelocity = 0f;
                if (!IsGrounded) _squash = 0.78f;
                IsGrounded = true;
            }
        }

        private void UpdateSlide(float dt)
        {
            if (!IsSliding) return;
            _slideTimer -= dt;
            if (_slideTimer <= 0f) CancelSlide();
        }

        private void ApplyVisual()
        {
            if (_visual == null) return;

            _squash = Mathf.Lerp(_squash, 1f, 1f - Mathf.Exp(-14f * Time.deltaTime));

            float slideScale = IsSliding ? GameConfig.SlideScaleY : 1f;
            float sx = _baseScale.x * (2f - _squash) * slideScale;
            float sy = _baseScale.y * _squash * slideScale;
            float sz = _baseScale.z * (2f - _squash) * slideScale;

            _visual.localScale = new Vector3(sx, sy, sz);

            // Single-axis pitch only. The old version fed the same accumulator into both
            // X and Y, which produced a fast yaw spin on top of the tumble.
            _visual.localRotation = Quaternion.Euler(_visualRoll + _speedLean, 0f, _tilt);
        }

        /// <summary>Player's feet in world space; used for "did I clear the barrier".</summary>
        public float FeetY => transform.position.y - GameConfig.PlayerHalfHeight * transform.localScale.y;

        public float HeadY => transform.position.y + GameConfig.PlayerHalfHeight * transform.localScale.y;

        /// <summary>Distance covered since the last call, used by the score keeper.</summary>
        public float ConsumeTraveledDistance()
        {
            float value = _pendingTravel;
            _pendingTravel = 0f;
            return value;
        }

        // -------------------------------------------------------------- collisions

        private void OnTriggerEnter(Collider other)
        {
            var pickup = other.GetComponent<Pickup>();
            if (pickup != null)
            {
                pickup.Collect(this);
                return;
            }

            var obstacle = other.GetComponent<ObstacleMarker>();
            if (obstacle != null)
            {
                ResolveObstacle(obstacle);
            }
        }

        private void ResolveObstacle(ObstacleMarker obstacle)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            bool cleared;
            switch (obstacle.Kind)
            {
                case ObstacleKind.JumpBarrier:
                    // Cleared when the feet are above the barrier's top face.
                    cleared = FeetY >= obstacle.TopY - 0.12f;
                    break;
                case ObstacleKind.SlideBarrier:
                    // Cleared by sliding under it.
                    cleared = IsSliding;
                    break;
                default:
                    cleared = false;
                    break;
            }

            if (cleared)
            {
                obstacle.NotifyCleared();
                return;
            }

            obstacle.NotifyHit();
            gm.Fail(obstacle.FailMessage);
        }
    }
}
