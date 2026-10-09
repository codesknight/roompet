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
        public enum Mode { Idle, Wander, Approach, React, Sleep, Fetch, Follow }

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

            TickBladder(dt);

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

                case Mode.Follow:
                    TickFollow(dt);
                    break;

                default:
                    TickIdle(dt);
                    break;
            }
        }

        // -------------------------------------------------------------------- bladder

        /// <summary>
        /// The one need with a deadline.
        ///
        /// The pet heads for the tray on its own (the behaviour table has a row for it), but a
        /// pet that is asleep, mid-fetch or simply too far away does not always make it — and
        /// that failure is the point: it leaves a mess, the mood takes a hit, and tidying up is
        /// something the player (or a sheepish pet) has to do. A game where the tray is
        /// optional decoration would have no reason to exist in the room at all.
        ///
        /// The grace period is what makes this fair rather than instant. Without it the
        /// accident fires on the same frame the need goes critical — the scheduler only scores
        /// the behaviour table every few seconds, so the pet would never once reach the tray,
        /// and a system the player can never avoid is just a punishment on a timer.
        /// </summary>
        private void TickBladder(float dt)
        {
            if (!Needs.BladderCritical)
            {
                _bladderGrace = 0f;
                return;
            }

            // Already on the way to the tray? Give it the benefit of the doubt.
            if (CurrentMode == Mode.Approach && _pending != null && _pending.Kind == InteractableKind.Toilet)
            {
                _bladderGrace = 0f;
                return;
            }

            _bladderGrace += dt;

            // Sleeping pets get a longer grace rather than a hard veto: waking up to a mess
            // every single night would make putting it to bed feel like a trap.
            float allowed = CurrentMode == Mode.Sleep ? SleepGraceSeconds : GraceToReachTheTray();
            if (_bladderGrace < allowed) return;

            // And no pet has two accidents in a row. The point of the system is one memorable
            // moment, not a pet that has to be cleaned up after every couple of minutes.
            if (Time.time - _lastAccidentAt < AccidentCooldown) { _bladderGrace = 0f; return; }
            _lastAccidentAt = Time.time;

            _bladderGrace = 0f;
            Needs.Accident();
            HasAccident = true;
            Avatar.PlayAction(PetAction.Sad, 2.6f);
            PetAudioDirector.Instance?.Play(SfxId.Whine);
            BladderAccident?.Invoke();

            if (Room != null)
            {
                Vector3 where = transform.position + AvatarFacing() * 0.5f;
                Room.SpawnMess(where);
            }
        }

        /// <summary>Raised when the pet has an accident, so the notebook can record it.</summary>
        public event Action BladderAccident;

        /// <summary>
        /// How long the pet gets to reach the tray, from where it currently is.
        ///
        /// The walk is the fair part of the deadline. With a flat grace a pet on the far side
        /// of the room was judged by the same clock as one standing next to the tray, which
        /// turns "did it make it" into a coin flip rather than something the player can see
        /// coming and act on.
        /// </summary>
        private float GraceToReachTheTray()
        {
            var tray = Room != null ? NearestOfKind(InteractableKind.Toilet) : null;
            if (tray == null) return GraceSeconds;

            float distance = Vector3.Distance(transform.position, tray.ApproachPoint);
            float walk = distance / Mathf.Max(0.5f, WalkSpeed);
            return Mathf.Clamp(GraceSeconds + walk, GraceSeconds, MaxGraceSeconds);
        }

        private Interactable NearestOfKind(InteractableKind kind)
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Room.Interactables.Count; i++)
            {
                var candidate = Room.Interactables[i];
                if (candidate == null || candidate.Kind != kind) continue;
                float distance = Vector3.Distance(transform.position, candidate.ApproachPoint);
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }

        /// <summary>Seconds a pet can hold it before it has an accident, before the walk is added.</summary>
        public float GraceSeconds = 6.5f;

        /// <summary>Ceiling on the walk allowance, so a pet in a far corner still has a deadline.</summary>
        public float MaxGraceSeconds = 14f;

        /// <summary>Longer while asleep, so bedtime is not a trap.</summary>
        public float SleepGraceSeconds = 22f;

        /// <summary>
        /// Minimum seconds between two accidents.
        ///
        /// Tuned by playing it: at the earlier numbers a pet left alone for a few minutes
        /// produced a puddle about every other minute, which reads as a bug rather than as a
        /// pet with a problem. One every few minutes is enough for the player to notice the
        /// system exists and to feel that ignoring the tray costs something.
        /// </summary>
        public float AccidentCooldown = 200f;

        private float _lastAccidentAt = -9999f;

        private float _bladderGrace;
        private Vector3 AvatarFacing() => Avatar != null ? Avatar.transform.forward : transform.forward;

        /// <summary>Consumed by the HUD/prompt layer: true for a few seconds after an accident.</summary>
        public bool HasAccident { get; private set; }

        public void ClearAccidentFlag() => HasAccident = false;

        /// <summary>
        /// A short-lived spray of bubbles above the bath: three spheres that rise and fade.
        /// Procedural, like everything else here, and cheap enough to fire on every bath.
        /// </summary>
        private void StartBubbles(Vector3 origin)
        {
            var root = new GameObject("Bubbles");
            root.transform.position = origin + Vector3.up * 0.7f;
            var script = root.AddComponent<PetBubbles>();
            script.Lifetime = 1.6f;
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
            _stayUntil = 0f;
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

        // ------------------------------------------------------------------ orders
        //
        // What "听得懂主人指令" means in code. An order is not a new kind of movement: it is the same
        // walk-to-it-and-use-it the pet already does on its own, aimed by the owner instead of by the
        // need table. That is also why it works at all — the pet's spatial knowledge is the thing
        // being commanded, exactly as the player described it.

        /// <summary>
        /// Walks to a kind of object and uses it. False when the room has none that is usable, which
        /// is the one case the pet has to admit to rather than silently ignore.
        /// </summary>
        public bool OrderTo(InteractableKind kind)
        {
            var item = FindInteractable(kind);
            if (item == null) return false;

            GoTo(item);
            return true;
        }

        /// <summary>Come and stand in front of the owner.</summary>
        public bool ComeToOwner()
        {
            if (Player == null) return false;

            Vector3 spot = Player.position - (Player.position - transform.position).normalized * 1.1f;
            GoToPoint(ClampToRoom(spot), PetBehaviorLibrary.Get("seek_attention"));
            return true;
        }

        /// <summary>
        /// Walk with the owner for a while.
        ///
        /// A mode of its own rather than "go to the player once": the point of 「跟着我」 is that it
        /// stays true while the owner moves, so the target is re-read every frame instead of being
        /// fixed at the moment of the order.
        /// </summary>
        public bool FollowOwner(float seconds = 25f)
        {
            if (Player == null) return false;

            DropCarriedBall();
            _pending = null;
            _freeTarget = null;
            _freeBehavior = null;
            _followUntil = Time.time + seconds;
            CurrentBehavior = PetBehaviorLibrary.Get("seek_attention");
            CurrentMode = Mode.Follow;
            return true;
        }

        private float _followUntil;

        /// <summary>How close the pet tries to stay while following.</summary>
        public float FollowDistance = 1.6f;

        private void TickFollow(float dt)
        {
            if (Player == null || Time.time > _followUntil)
            {
                EnterIdle();
                return;
            }

            Avatar.FaceTowards(Player.position);

            Vector3 flat = Player.position - transform.position;
            flat.y = 0f;

            if (flat.magnitude > FollowDistance)
            {
                Avatar.SetLocomotion(1f);
                transform.position = ClampToRoom(
                    transform.position + flat.normalized * (WalkSpeed * dt));
            }
            else
            {
                // Close enough: walk in place visually stopped, but keep facing the owner.
                Avatar.SetLocomotion(0f);
            }
        }

        /// <summary>
        /// Stay put for a while — 「别动」.
        ///
        /// Implemented as a long scripted reaction rather than a new mode, because "not moving" is
        /// already what the reaction state does, and because the scheduler only runs when the pet is
        /// idle: being in any other state is exactly what stops it wandering off mid-order.
        /// </summary>
        public void Stay(float seconds = 20f)
        {
            ReactTo(PetAction.Sit, seconds);
            _stayUntil = Time.time + seconds;
            CurrentBehavior = PetBehaviorLibrary.Get("sit_stare");
        }

        private float _stayUntil;

        /// <summary>True while the pet is holding position on purpose, for the HUD and the prompt.</summary>
        public bool IsStaying => _stayUntil > Time.time && CurrentMode == Mode.React;

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
                case InteractableKind.Bath:
                    // A bath is a proper wash, not a brush-down, and the pet shakes itself dry.
                    Needs.Clean(1f);
                    Avatar.PlayAction(PetAction.Jump, 2.4f);
                    PetAudioDirector.Instance?.Play(SfxId.Splash);
                    StartBubbles(target.transform.position);
                    break;
                case InteractableKind.Toilet:
                    Needs.Relieve();
                    Avatar.PlayAction(PetAction.Sit, 3f);
                    break;
                case InteractableKind.Mess:
                    // The pet tidying up after itself, or the player cleaning: either way the
                    // puddle is gone. `Needs.Clean` is small — scrubbing is not a bath.
                    Needs.Clean(0.25f);
                    Avatar.PlayAction(PetAction.Sad, 1.8f);
                    Room?.RemoveMess(target);
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
