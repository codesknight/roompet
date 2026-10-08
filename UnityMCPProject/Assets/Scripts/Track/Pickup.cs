using UnityEngine;

namespace DshRunner
{
    /// <summary>A collectable: either a coin or a power-up capsule.</summary>
    public class Pickup : MonoBehaviour
    {
        public bool IsCoin = true;
        public PowerUpKind PowerUp = PowerUpKind.Shield;
        public bool Collected { get; private set; }

        [Header("Motion")]
        public float SpinSpeed = 160f;
        public float BobAmplitude = 0.12f;
        public float BobSpeed = 2.4f;

        private Vector3 _restPosition;
        private float _phase;
        private Transform _cached;

        private void Awake()
        {
            _cached = transform;
            _restPosition = _cached.localPosition;
            _phase = Random.value * 6.283f;
        }

        public void ConfigureCoin()
        {
            IsCoin = true;
            Collected = false;
            name = "Coin";
        }

        public void ConfigurePowerUp(PowerUpKind kind)
        {
            IsCoin = false;
            PowerUp = kind;
            Collected = false;
            name = "PowerUp_" + kind;
        }

        /// <summary>Called by the track when the pickup is taken out of the pool.</summary>
        public void ResetForPool(Vector3 localPosition)
        {
            Collected = false;
            _restPosition = localPosition;
            _cached = transform;
            _cached.localPosition = localPosition;
            _phase = Random.value * 6.283f;
            gameObject.SetActive(true);
        }

        public void Collect(PlayerController player)
        {
            if (Collected) return;
            Collected = true;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (IsCoin)
                {
                    gm.Score.AddCoin(1);
                }
                else if (gm.PowerUps != null)
                {
                    gm.PowerUps.Activate(PowerUp);
                }
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Spin/bob, plus magnet attraction. Attraction is resolved analytically (the
        /// pickup flies to the player and is collected on arrival) so it never depends on
        /// a trigger firing while two colliders move.
        /// </summary>
        public void Tick(float dt, float magnetRadius, Vector3 playerPosition)
        {
            if (Collected) return;

            if (_cached == null) _cached = transform;

            Vector3 world = _cached.position;

            if (magnetRadius > 0f)
            {
                float distance = Vector3.Distance(world, playerPosition);
                if (distance < magnetRadius)
                {
                    float pull = Mathf.Lerp(24f, 6f, distance / magnetRadius);
                    world = Vector3.MoveTowards(world, playerPosition, pull * dt);
                    _cached.position = world;
                    if (Vector3.Distance(world, playerPosition) < 0.9f)
                    {
                        Collect(null);
                        return;
                    }
                    return;
                }
            }

            _phase += dt * BobSpeed;
            Vector3 local = _restPosition;
            local.y += Mathf.Sin(_phase) * BobAmplitude;
            _cached.localPosition = local;
            _cached.Rotate(Vector3.up, SpinSpeed * dt, Space.Self);
        }
    }
}
