using System;
using UnityEngine;

namespace DshPet
{
    /// <summary>
    /// Autonomy. The pet decides what to do on its own — wander, rest, go use something the
    /// player clicked — and the chat layer can interrupt with a scripted reaction.
    ///
    /// The controller owns no state that the UI needs to read except <see cref="Mode"/>,
    /// which the HUD shows so the behaviour is inspectable while tuning.
    /// </summary>
    public class PetController : MonoBehaviour
    {
        public enum Mode { Idle, Wander, Approach, React, Sleep, Fetch }

        [Header("Wiring")]
        public PetAvatar Avatar;
        public PetRoom Room;
        public PetNeeds Needs;

        /// <summary>The player's character, when the scene has one. Enables "come to you"
        /// behaviour and eye contact.</summary>
        public Transform Player;

        /// <summary>The throwable ball, for fetching. Wired by the game manager.</summary>
        public PetBall Ball;

        [Header("Movement")]
        public float WalkSpeed = 1.5f;
        public float ArriveDistance = 0.5f;

        /// <summary>Fetching is a run, not a stroll.</summary>
        public float FetchSpeedMultiplier = 1.8f;

        [Header("Behaviour")]
        public float IdleMin = 1.5f;
        public float IdleMax = 5f;
        public float SleepBelowEnergy = 0.16f;
        public float WakeAboveEnergy = 0.62f;

        public Mode CurrentMode { get; private set; } = Mode.Idle;

        public event Action<Interactable> Interacted;

        /// <summary>Raised once the pet has carried the ball back and set it down.</summary>
        public event Action<PetBall> Fetched;

        private Vector3 _wanderTarget;
        private float _idleTimer = 2f;
        private float _reactTimer;
        private Interactable _pending;
        private System.Random _rng = new System.Random();
        private float _sleepTimer;

        private void Update()
        {
            if (Avatar == null || Needs == null) return;

            float dt = Time.deltaTime;

            switch (CurrentMode)
            {
                case Mode.React:
                    _reactTimer -= dt;
                    Avatar.SetLocomotion(0f);
                    if (_reactTimer <= 0f) EnterIdle();
                    break;

                case Mode.Approach:
                    TickApproach(dt);
                    break;

                case Mode.Wander:
                    TickWander(dt);
                    break;

                case Mode.Sleep:
                    TickSleep(dt);
                    break;

                case Mode.Fetch:
                    TickFetch(dt);
                    break;

                default:
                    TickIdle(dt);
                    break;
            }
        }

        // -------------------------------------------------------------------- states

        private void TickIdle(float dt)
        {
            Avatar.SetLocomotion(0f);
            _idleTimer -= dt;

            if (_idleTimer > 0f)
            {
                // Standing still next to the player, the pet keeps an eye on them.
                if (Player != null && Vector3.Distance(transform.position, Player.position) < 2.6f)
                {
                    Avatar.FaceTowards(Player.position, 4f);
                }
                return;
            }

            // Needs are no longer handled here: PetBehaviorScheduler owns every "go do
            // something" decision, so the pet has exactly one brain deciding its actions.
            EnterWander();
        }

        private void TickWander(float dt)
        {
            Vector3 flat = _wanderTarget - transform.position;
            flat.y = 0f;

            if (flat.magnitude <= ArriveDistance)
            {
                EnterIdle();
                return;
            }

            Avatar.SetLocomotion(1f);
            Avatar.FaceTowards(_wanderTarget);
            transform.position += flat.normalized * (WalkSpeed * dt);
        }

        private void TickApproach(float dt)
        {
            // Two flavours of "go somewhere": use an object, or simply stand at a point
            // (which is how the pet comes over to the player).
            if (_pending == null && !_freeTarget.HasValue) { EnterIdle(); return; }

            Vector3 target = _pending != null ? _pending.ApproachPoint : _freeTarget.Value;
            Vector3 flat = target - transform.position;
            flat.y = 0f;

            Avatar.FaceTowards(target);

            if (flat.magnitude > ArriveDistance)
            {
                Avatar.SetLocomotion(1f);
                transform.position += flat.normalized * (WalkSpeed * dt);
                return;
            }

            Avatar.SetLocomotion(0f);

            if (_pending != null) UsePending();
            else
            {
                var behavior = _freeBehavior;
                _freeTarget = null;
                _freeBehavior = null;
                ReactTo(behavior != null ? behavior.Action : PetAction.Sit,
                    behavior != null ? behavior.Duration : 2.5f);
            }
        }

        private void TickSleep(float dt)
        {
            Avatar.SetLocomotion(0f);
            _sleepTimer += dt;

            // Energy recovers in chunks so a nap is visible rather than instantaneous.
            Needs.Sleep(dt * 1.4f);

            if (Needs.Energy >= WakeAboveEnergy)
            {
                Needs.Joy = Mathf.Clamp01(Needs.Joy + 0.08f);
                Avatar.PlayAction(PetAction.Happy, 1.4f);
                EnterIdle();
            }
        }

        // -------------------------------------------------------------------- fetching

        private enum FetchPhase { Chase, Return }

        private FetchPhase _fetchPhase;
        private Transform _mouth;

        /// <summary>Where a carried ball rides. A synthetic bone, so no rig surgery is needed.</summary>
        private Transform Mouth
        {
            get
            {
                if (_mouth == null)
                {
                    var go = new GameObject("Mouth");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 0.52f, 0.44f);
                    _mouth = go.transform;
                }
                return _mouth;
            }
        }

        public bool IsFetching => CurrentMode == Mode.Fetch;

        /// <summary>True when "go get the ball" is a thing that can actually happen now.</summary>
        public bool CanFetch => Ball != null && Ball.IsLoose &&
            CurrentMode != Mode.Sleep && CurrentMode != Mode.React;

        /// <summary>Run after the ball, carry it back to the player, drop it at their feet.</summary>
        public void FetchBall()
        {
            if (Ball == null && Room != null) Ball = Room.Ball;
            if (CanFetch == false) return;

            DropCarriedBall();

            _pending = null;
            _freeTarget = null;
            _freeBehavior = null;
            _fetchPhase = FetchPhase.Chase;
            _selfInitiated = true;
            CurrentBehavior = PetBehaviorLibrary.Get("fetch");
            CurrentMode = Mode.Fetch;
        }

        private void TickFetch(float dt)
        {
            // A carried ball is not "loose" any more — the pet has it — so this cannot be a
            // plain IsLoose test or the retrieve would abort the instant it succeeded.
            if (Ball == null)
            {
                EnterIdle();
                return;
            }

            // The player can snatch it back mid-run.
            if (Ball.State == BallState.Held)
            {
                EnterIdle();
                return;
            }

            if (_fetchPhase == FetchPhase.Chase) TickFetchChase(dt);
            else TickFetchReturn(dt);
        }

        private void TickFetchChase(float dt)
        {
            if (Ball.State == BallState.Carried) { _fetchPhase = FetchPhase.Return; return; }

            // While the ball is still in the air the pet runs at where it will LAND, which
            // is what makes it read as "read the throw" rather than "chase the pixels".
            Vector3 target = Ball.State == BallState.Flying
                ? Ball.PredictLanding()
                : Ball.transform.position;
            target = ClampToRoom(target);

            Vector3 flat = target - transform.position;
            flat.y = 0f;
            Avatar.FaceTowards(target);

            if (flat.magnitude > ArriveDistance + 0.32f)
            {
                Avatar.SetLocomotion(1.4f);
                transform.position = ClampToRoom(
                    transform.position + flat.normalized * (WalkSpeed * FetchSpeedMultiplier * dt));
                return;
            }

            Avatar.SetLocomotion(0f);

            // Stand over the landing spot and wait out the bounces.
            if (Ball.State == BallState.Flying) return;

            Ball.Carry(Mouth);
            Needs.Play(0.18f, true);
            Avatar.PlayAction(PetAction.Jump, 0.7f);
            PetAudioDirector.Instance?.Play(SfxId.PickUp);
            _fetchPhase = FetchPhase.Return;
        }

        private void TickFetchReturn(float dt)
        {
            Vector3 target = ClampToRoom(Player != null ? Player.position : transform.position);
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            Avatar.FaceTowards(flat.sqrMagnitude > 0.0001f ? target : transform.position + transform.forward);

            float stopAt = Player != null ? 1.15f : 0f;
            if (flat.magnitude > stopAt)
            {
                Avatar.SetLocomotion(1.4f);
                transform.position = ClampToRoom(
                    transform.position + flat.normalized * (WalkSpeed * FetchSpeedMultiplier * dt));
                return;
            }

            Avatar.SetLocomotion(0f);

            // Set it down between the two of them, then show off a little.
            Vector3 facing = transform.forward;
            if (flat.sqrMagnitude > 0.0001f) facing = flat.normalized;
            var dropped = Ball;
            dropped.Drop(transform.position + facing * 0.5f);

            Needs.Play(0.22f, true);
            Avatar.PlayAction(PetAction.Happy, 1.8f);
            PetAudioDirector.Instance?.Play(SfxId.Happy);

            CurrentBehavior = PetBehaviorLibrary.Get("fetch");
            CurrentMode = Mode.React;
            _reactTimer = 1.8f;

            // The pet just put the ball at your feet: on a phone this is the one moment worth
            // a real buzz, because you are watching the screen rather than the cursor.
            DshMobile.MobileHaptics.Medium();

            Fetched?.Invoke(dropped);
        }

        /// <summary>Never leave the ball stuck in the pet's mouth when the plan changes.</summary>
        private void DropCarriedBall()
        {
            if (Ball != null && Ball.State == BallState.Carried)
            {
                Ball.Drop(transform.position + transform.forward * 0.5f);
            }
        }

        // ------------------------------------------------------------------ commands

        public void EnterIdle()
        {
            DropCarriedBall();
            CurrentMode = Mode.Idle;
            _idleTimer = Mathf.Lerp(IdleMin, IdleMax, (float)_rng.NextDouble());
            _pending = null;
            CurrentBehavior = null;
        }

        /// <summary>True while the pet is free to start something new.</summary>
        public bool IsIdle => CurrentMode == Mode.Idle;

        public void EnterWander()
        {
            if (Room == null) { EnterIdle(); return; }

            float limit = Room.Size * 0.5f - 1.6f;
            _wanderTarget = new Vector3(
                Mathf.Lerp(-limit, limit, (float)_rng.NextDouble()),
                0f,
                Mathf.Lerp(-limit, limit, (float)_rng.NextDouble()));

            CurrentMode = Mode.Wander;
        }

        public void EnterSleep()
        {
            var bed = FindInteractable(InteractableKind.Bed);
            if (bed != null && Vector3.Distance(transform.position, bed.ApproachPoint) > ArriveDistance)
            {
                GoTo(bed);
                return;
            }

            CurrentMode = Mode.Sleep;
            _sleepTimer = 0f;
            Avatar.PlayAction(PetAction.Sleep, 900f);
        }

        /// <summary>Walk over and use an object. Called when the player clicks it.</summary>
        public void GoTo(Interactable target, bool selfInitiated = false)
        {
            if (target == null) return;
            DropCarriedBall();
            _pending = target;
            _selfInitiated = selfInitiated;
            CurrentMode = Mode.Approach;
        }

        private bool _selfInitiated;

        /// <summary>Scripted reaction, used by the chat layer to act out what the pet said.</summary>
        public void ReactTo(PetAction action, float seconds = 1.8f)
        {
            DropCarriedBall();
            _pending = null;
            CurrentMode = Mode.React;
            _reactTimer = seconds;
            Avatar.SetLocomotion(0f);
            Avatar.PlayAction(action, seconds);

            if (action == PetAction.Sleep) _reactTimer = seconds;
        }

        /// <summary>The behaviour the scheduler picked, for the HUD's "why is it doing that".</summary>
        public PetBehavior CurrentBehavior { get; private set; }

        /// <summary>
        /// Runs one row from the behaviour table. A behaviour that names a target walks
        /// there first; otherwise it just plays its animation in place.
        /// </summary>
        public void RunBehavior(PetBehavior behavior)
        {
            if (behavior == null) return;
            CurrentBehavior = behavior;

            // Fetching is not "walk to the ball and act": it is a whole retrieve loop, so
            // the row hands off to the state machine instead of the generic walker.
            if (behavior.Fetch)
            {
                if (Ball == null && Room != null) Ball = Room.Ball;
                if (Ball != null && Ball.IsLoose) { FetchBall(); return; }
            }

            // "Player" is a virtual target: there is no Interactable to walk to, just a spot
            // beside the character.
            if (string.Equals(behavior.TargetKind, "Player", StringComparison.OrdinalIgnoreCase))
            {
                if (Player != null)
                {
                    Vector3 spot = Player.position - (Player.position - transform.position).normalized * 1.1f;
                    GoToPoint(spot, behavior);
                    return;
                }
                ReactTo(behavior.Action, behavior.Duration);
                return;
            }

            var target = behavior.Target;
            if (target.HasValue)
            {
                var item = FindInteractable(target.Value);
                if (item != null)
                {
                    GoTo(item, true);
                    return;
                }
            }

            ReactTo(behavior.Action, behavior.Duration);
        }

        /// <summary>Walk to a bare world position, then play the behaviour's action.</summary>
        public void GoToPoint(Vector3 point, PetBehavior behavior)
        {
            DropCarriedBall();
            _pending = null;
            _freeTarget = point;
            _freeBehavior = behavior;
            _selfInitiated = true;
            CurrentMode = Mode.Approach;
        }

        private Vector3? _freeTarget;
        private PetBehavior _freeBehavior;

        /// <summary>Turn to look at a world point (the camera, when the player is talking).</summary>
        public void LookAt(Vector3 worldPoint) => Avatar.FaceTowards(worldPoint);

        public void SnapTo(Vector3 position)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            EnterIdle();
        }

        // ------------------------------------------------------------------- helpers

        private void UsePending()
        {
            var target = _pending;
            _pending = null;
            if (target == null) { EnterIdle(); return; }

            switch (target.Kind)
            {
                case InteractableKind.Food:
                    Needs.Feed();
                    Avatar.PlayAction(PetAction.Eat, 2.6f);
                    break;
                case InteractableKind.Water:
                    Needs.GiveWater();
                    Avatar.PlayAction(PetAction.Drink, 2.2f);
                    break;
                case InteractableKind.Ball:
                    Needs.Play(0.35f, !_selfInitiated);
                    Avatar.PlayAction(PetAction.Play, 2.8f);
                    break;
                case InteractableKind.Brush:
                    Needs.Clean();
                    Avatar.PlayAction(PetAction.Wag, 2.6f);
                    break;
                case InteractableKind.Bed:
                    CurrentMode = Mode.Sleep;
                    _sleepTimer = 0f;
                    Avatar.PlayAction(PetAction.Sleep, 900f);
                    target.MarkUsed();
                    Interacted?.Invoke(target);
                    return;
                default:
                    Needs.Pet(0.1f);
                    Avatar.PlayAction(PetAction.Happy, 1.6f);
                    break;
            }

            target.MarkUsed();
            Interacted?.Invoke(target);
            CurrentMode = Mode.React;
            _reactTimer = 2.4f;
        }

        /// <summary>Keeps the pet inside the walls, which matters once it starts running.</summary>
        private Vector3 ClampToRoom(Vector3 position)
        {
            float limit = (Room != null ? Room.Size : 14f) * 0.5f - 0.8f;
            return new Vector3(
                Mathf.Clamp(position.x, -limit, limit),
                0f,
                Mathf.Clamp(position.z, -limit, limit));
        }

        private Interactable FindInteractable(InteractableKind kind)
        {
            if (Room == null) return null;
            Interactable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                var candidate = Room.Interactables[i];
                if (candidate == null || candidate.Kind != kind) continue;
                if (!candidate.IsReady) continue;

                float distance = Vector3.Distance(transform.position, candidate.ApproachPoint);
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }
    }
}
