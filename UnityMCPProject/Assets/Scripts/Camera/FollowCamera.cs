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
            SnapToTarget();
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

            transform.position = Target.position + Offset;
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

            Vector3 desired = Target.position + Offset;
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
