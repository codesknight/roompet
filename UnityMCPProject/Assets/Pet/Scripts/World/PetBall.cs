using System;
using UnityEngine;

namespace DshPet
{
    public enum BallState { Resting, Held, Flying, Carried }

    /// <summary>
    /// The ball, with just enough physics to throw and fetch it.
    ///
    /// Motion is integrated by hand rather than by a Rigidbody: the pet's carry logic needs
    /// to place the ball exactly, and a hand-rolled arc keeps the whole thing deterministic
    /// and testable. Bounces are a velocity flip plus friction, which is plenty for a toy.
    ///
    /// The transform pivot sits on the FLOOR under the ball, not at its centre: the visual
    /// sphere and the click collider are children/offsets riding <see cref="Radius"/> above
    /// it. That keeps "is it on the ground" a simple y == 0 test.
    /// </summary>
    public class PetBall : MonoBehaviour
    {
        [Header("Flight")]
        public float Radius = 0.26f;
        public float Gravity = 16f;
        public float BounceRestitution = 0.46f;
        public float GroundFriction = 0.72f;
        public float AirDrag = 0.06f;
        public float SleepSpeed = 0.55f;

        [Header("Throw")]
        public float MinThrowSpeed = 5.5f;
        public float MaxThrowSpeed = 13f;
        public float ThrowElevationDegrees = 34f;
        public float ChargeSeconds = 1.1f;

        [Header("Carry")]
        public Vector3 HoldOffset = new Vector3(0f, 0.75f, 0.55f);

        public BallState State { get; private set; } = BallState.Resting;

        /// <summary>0..1 while the player holds the throw button.</summary>
        public float Charge { get; private set; }

        /// <summary>Where the ball will come to rest, for the "pick it up" prompt.</summary>
        public bool IsAtRest => State == BallState.Resting;

        /// <summary>
        /// True when the ball is out of anyone's hands and worth chasing: it is either in
        /// the air, or it has been thrown and nobody has picked it up yet.
        ///
        /// This is deliberately NOT "resting anywhere". A ball parked in its corner is a toy
        /// to go play with; only a thrown ball is a fetch, and this flag is what keeps the
        /// pet from looping "carry it back, fetch it again" forever.
        /// </summary>
        public bool IsLoose => State == BallState.Flying || (State == BallState.Resting && WasThrown);

        /// <summary>Set by a throw, cleared the moment somebody picks the ball up.</summary>
        public bool WasThrown { get; private set; }

        public event Action<PetBall> Landed;
        public event Action<PetBall> Thrown;

        private Vector3 _velocity;
        private Transform _anchor;      // hand (player) or mouth (pet)
        private float _restTimer;
        private Vector3 _spin;

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (State)
            {
                case BallState.Flying:
                    TickFlight(dt);
                    break;

                case BallState.Held:
                case BallState.Carried:
                    TickCarried(dt);
                    break;
            }

            // A little roll animation so a resting ball still looks alive.
            if (State == BallState.Resting && _spin.sqrMagnitude > 0.001f)
            {
                transform.Rotate(_spin * dt, Space.World);
                _spin = Vector3.Lerp(_spin, Vector3.zero, 1f - Mathf.Exp(-2f * dt));
            }
        }

        // --------------------------------------------------------------------- states

        public void PickUp(Transform anchor)
        {
            if (State == BallState.Held) return;

            _anchor = anchor;
            _velocity = Vector3.zero;
            Charge = 0f;
            WasThrown = false;
            State = BallState.Held;

            PetAudioDirector.Instance?.Play(SfxId.PickUp);
        }

        /// <summary>Held by the pet's mouth on the way back.</summary>
        public void Carry(Transform anchor)
        {
            _anchor = anchor;
            _velocity = Vector3.zero;
            Charge = 0f;
            // Picked up by the pet: the fetch is satisfied, so the ball stops being "loose"
            // and the pet will not immediately re-fetch what it is already holding.
            WasThrown = false;
            State = BallState.Carried;
        }

        public void BeginCharge(float dt)
        {
            if (State != BallState.Held) return;
            Charge = Mathf.Clamp01(Charge + dt / Mathf.Max(0.05f, ChargeSeconds));
        }

        /// <summary>Launch toward <paramref name="direction"/> (flattened) with the current charge.</summary>
        public void Throw(Vector3 direction)
        {
            if (State != BallState.Held) return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();

            float speed = Mathf.Lerp(MinThrowSpeed, MaxThrowSpeed, Charge);
            float elevation = ThrowElevationDegrees * Mathf.Deg2Rad;

            _velocity = direction * (speed * Mathf.Cos(elevation)) + Vector3.up * (speed * Mathf.Sin(elevation));
            _spin = new Vector3(_velocity.z, 0f, -_velocity.x) * 1.6f;

            _anchor = null;
            Charge = 0f;
            WasThrown = true;
            State = BallState.Flying;

            PetAudioDirector.Instance?.Play(SfxId.Throw);
            Thrown?.Invoke(this);
        }

        /// <summary>Put it down in front of the player (the pet's "here you go").</summary>
        public void Drop(Vector3 position)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            _anchor = null;
            _velocity = Vector3.zero;
            Charge = 0f;
            State = BallState.Resting;
            Landed?.Invoke(this);
        }

        public void SnapToRest(Vector3 position)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            _anchor = null;
            _velocity = Vector3.zero;
            Charge = 0f;
            State = BallState.Resting;
        }

        // -------------------------------------------------------------------- physics

        private void TickCarried(float dt)
        {
            if (_anchor == null) { State = BallState.Resting; return; }

            Vector3 target = _anchor.position + _anchor.rotation * HoldOffset;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-24f * dt));
            transform.Rotate(Vector3.up, 40f * dt, Space.World);
        }

        private void TickFlight(float dt)
        {
            _velocity += Vector3.down * Gravity * dt;
            _velocity -= _velocity * (AirDrag * dt);

            transform.position += _velocity * dt;
            transform.Rotate(_spin * dt, Space.World);

            float floor = 0f;
            if (transform.position.y <= floor)
            {
                var pos = transform.position;
                pos.y = floor;
                transform.position = pos;

                if (_velocity.y < -0.6f)
                {
                    _velocity.y = -_velocity.y * BounceRestitution;
                    _velocity.x *= GroundFriction;
                    _velocity.z *= GroundFriction;
                    _spin *= 0.7f;
                    PetAudioDirector.Instance?.Play(SfxId.BallBounce, 0.6f, 0.1f);
                }
                else
                {
                    // Rolling out: bleed the horizontal speed and settle.
                    _velocity.y = 0f;
                    _velocity.x *= Mathf.Pow(0.06f, dt);
                    _velocity.z *= Mathf.Pow(0.06f, dt);

                    if (_velocity.magnitude < SleepSpeed)
                    {
                        _restTimer += dt;
                        if (_restTimer > 0.25f)
                        {
                            _velocity = Vector3.zero;
                            State = BallState.Resting;
                            Landed?.Invoke(this);
                        }
                    }
                    else
                    {
                        _restTimer = 0f;
                    }
                }
            }
            else
            {
                _restTimer = 0f;
            }

            // Keep it inside the room.
            var room = PetGameManager.Instance != null ? PetGameManager.Instance.Room : null;
            float limit = (room != null ? room.Size : 14f) * 0.5f - 0.6f;
            var clamped = transform.position;
            if (Mathf.Abs(clamped.x) > limit)
            {
                clamped.x = Mathf.Sign(clamped.x) * limit;
                _velocity.x = -_velocity.x * BounceRestitution;
            }
            if (Mathf.Abs(clamped.z) > limit)
            {
                clamped.z = Mathf.Sign(clamped.z) * limit;
                _velocity.z = -_velocity.z * BounceRestitution;
            }
            transform.position = clamped;
        }

        /// <summary>Predicted landing spot, used by the pet to run to the right place.</summary>
        public Vector3 PredictLanding(float maxSeconds = 3f)
        {
            if (State != BallState.Flying) return transform.position;

            Vector3 p = transform.position;
            Vector3 v = _velocity;
            float dt = 0.05f;

            for (float t = 0f; t < maxSeconds; t += dt)
            {
                v += Vector3.down * Gravity * dt;
                p += v * dt;
                if (p.y <= 0f) break;
            }

            p.y = 0f;

            // The flight is clamped to the room, so the prediction must be too — otherwise
            // the pet sprints at a spot against the wall that the ball never reaches.
            var room = PetGameManager.Instance != null ? PetGameManager.Instance.Room : null;
            float limit = (room != null ? room.Size : 14f) * 0.5f - 0.6f;
            p.x = Mathf.Clamp(p.x, -limit, limit);
            p.z = Mathf.Clamp(p.z, -limit, limit);
            return p;
        }
    }
}
