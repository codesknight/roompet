using UnityEngine;

namespace DshRunner
{
    /// <summary>Chase camera: trails the runner, looks slightly ahead, and widens the
    /// field of view as speed rises so the sense of speed keeps building.</summary>
    [DefaultExecutionOrder(100)]
    public class FollowCamera : MonoBehaviour
    {
        public Transform Target;

        [Header("Framing")]
        public Vector3 Offset = new Vector3(0f, 3.6f, -6.4f);
        public Vector3 LookAhead = new Vector3(0f, 1.0f, 7f);
        public float FollowSmoothing = 9f;
        public float LookSmoothing = 11f;

        [Header("Speed feel")]
        public float BaseFov = 62f;
        public float MaxFov = 84f;
        public float SpeedForMaxFov = 34f;

        [Header("Lane framing")]
        [Tooltip("Half-width of the drivable track in metres, measured from the centre lane. " +
                 "The camera pulls back until this fits the viewport's WIDTH: a camera is " +
                 "defined by a vertical field of view, so on a narrow (portrait) screen the " +
                 "outer lanes fall off both edges however good the height is. " +
                 "0 means 'ask the level config', which is the normal case.")]
        public float TrackHalfWidth;

        [Tooltip("Fraction of the screen width kept clear on each side of the track.")]
        public float SideMargin = 0.08f;

        private float _distanceScale = 1f;

        [Header("Shake")]
        public float ShakeAmplitude = 0.05f;

        private Camera _camera;
        private Vector3 _lookPoint;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
        }

        private void Start()
        {
            ResolveTarget();
            ResolveTrackWidth();
            SnapToTarget();
        }

        /// <summary>
        /// Takes the track's half-width from the level config unless it was set by hand, so the
        /// framing follows the lanes rather than a number that has to be kept in sync.
        /// </summary>
        private void ResolveTrackWidth()
        {
            if (TrackHalfWidth > 0.01f) return;

            // The outermost lane's centre plus most of a character's width, since it is the
            // character standing in that lane that must not be clipped.
            TrackHalfWidth = Mathf.Abs(GameConfig.LaneX(GameConfig.LaneCount - 1)) + 0.7f;
        }

        /// <summary>
        /// The multiplier that keeps the outer lanes inside the frame.
        ///
        /// Solved against the BASE field of view, not the live one: the speed widening would
        /// otherwise feed back into the framing, and the camera would creep forward as the run
        /// got faster — visible as the track slowly closing in.
        /// </summary>
        private void UpdateDistanceScale()
        {
            if (_camera == null) return;

            ResolveTrackWidth();
            _distanceScale = DshMobile.MobileUi.WidthFitScale(
                -Offset.z, TrackHalfWidth, BaseFov, _camera.aspect, SideMargin);
        }

        private void ResolveTarget()
        {
            if (Target != null) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.Player != null) Target = gm.Player.transform;
            if (Target == null)
            {
                var player = FindObjectOfType<PlayerController>();
                if (player != null) Target = player.transform;
            }
        }

        public void SnapToTarget()
        {
            ResolveTarget();
            if (Target == null) return;

            UpdateDistanceScale();
            transform.position = Target.position + Offset * _distanceScale;
            _lookPoint = Target.position + LookAhead;
            transform.rotation = Quaternion.LookRotation(_lookPoint - transform.position, Vector3.up);
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                ResolveTarget();
                if (Target == null) return;
            }

            float dt = Time.unscaledDeltaTime;
            float follow = 1f - Mathf.Exp(-FollowSmoothing * dt);
            float look = 1f - Mathf.Exp(-LookSmoothing * dt);

            // Re-solved every frame so a phone rotated mid-run re-frames instead of cropping.
            UpdateDistanceScale();

            Vector3 desired = Target.position + Offset * _distanceScale;
            transform.position = Vector3.Lerp(transform.position, desired, follow);

            Vector3 targetLook = Target.position + LookAhead;
            // Look further ahead the faster we go.
            var gm = GameManager.Instance;
            if (gm != null)
            {
                float speed01 = Mathf.InverseLerp(GameConfig.BaseSpeedFallback, SpeedForMaxFov, gm.CurrentSpeed);
                targetLook += Vector3.forward * (speed01 * 6f);
            }

            _lookPoint = Vector3.Lerp(_lookPoint, targetLook, look);
            Vector3 forward = _lookPoint - transform.position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(forward, Vector3.up), look);
            }

            if (_camera != null)
            {
                float speed01 = gm != null
                    ? Mathf.InverseLerp(GameConfig.BaseSpeedFallback, SpeedForMaxFov, gm.CurrentSpeed)
                    : 0f;
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView,
                    Mathf.Lerp(BaseFov, MaxFov, speed01), 1f - Mathf.Exp(-3f * dt));
            }

            if (ShakeAmplitude > 0f && gm != null && gm.State == GameState.Playing)
            {
                float amount = ShakeAmplitude * Mathf.InverseLerp(GameConfig.BaseSpeedFallback, SpeedForMaxFov, gm.CurrentSpeed);
                transform.position += new Vector3(
                    (Mathf.PerlinNoise(Time.time * 17f, 0f) - 0.5f),
                    (Mathf.PerlinNoise(0f, Time.time * 19f) - 0.5f),
                    0f) * amount;
            }
        }
    }
}
