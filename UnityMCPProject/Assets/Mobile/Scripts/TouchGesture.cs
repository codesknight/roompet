using UnityEngine;

namespace DshMobile
{
    /// <summary>What a completed gesture turned out to be.</summary>
    public enum SwipeDirection { None, Left, Right, Up, Down }

    /// <summary>
    /// One-finger gestures: tap, swipe, and hold.
    ///
    /// Written as a pure state machine over (position, phase, dt) instead of reading
    /// <c>Input</c> directly, for two reasons: touch behaviour is otherwise only testable
    /// by holding a phone, and the runner's lane changes are exactly the kind of thing that
    /// silently stops working when the threshold is wrong.
    ///
    /// Thresholds are expressed as fractions of the screen's short side, so the same
    /// numbers feel identical on a small phone and a large tablet.
    /// </summary>
    public class TouchGesture
    {
        /// <summary>Movement (fraction of the short side) that turns a drag into a swipe.</summary>
        public float SwipeThreshold = 0.045f;

        /// <summary>Seconds after touch-down that a gesture still counts as a swipe.</summary>
        public float SwipeMaxSeconds = 0.6f;

        /// <summary>Movement below this (and a short press) is a tap.</summary>
        public float TapMaxMovement = 0.03f;

        public float TapMaxSeconds = 0.35f;

        public float HoldSeconds = 0.22f;

        /// <summary>Swipes are only reported once per touch-down.</summary>
        private bool _swipeFired;

        /// <summary>
        /// Screen size the thresholds are relative to.
        ///
        /// Injected rather than read from <c>Screen</c> inside the math, for two reasons: the
        /// thresholds then mean the same thing on every device ("5% of the short side"), and
        /// the behaviour is testable without resizing the editor's Game view.
        /// </summary>
        public float ReferenceSize = 1080f;

        private Vector2 _start;
        private Vector2 _current;
        private float _elapsed;
        private bool _active;
        private bool _heldReported;

        public bool IsActive => _active;
        public float Elapsed => _elapsed;
        public Vector2 StartPosition => _start;
        public Vector2 Position => _current;

        /// <summary>Net movement since touch-down, in fractions of <see cref="ReferenceSize"/>.</summary>
        public Vector2 Delta => new Vector2(
            (_current.x - _start.x) / Mathf.Max(1f, ReferenceSize),
            (_current.y - _start.y) / Mathf.Max(1f, ReferenceSize));

        /// <summary>
        /// Movement since the previous frame, in raw pixels. Used for continuous drags —
        /// the camera orbit needs the frame delta, not the total travel.
        /// </summary>
        public Vector2 FrameDelta { get; private set; }

        private Vector2 _lastFramePosition;

        /// <summary>True after the press has lasted long enough to count as a hold.</summary>
        public bool IsHolding => _active && _elapsed >= HoldSeconds;

        /// <summary>Raised once when the press becomes a hold (used for "hold to jump higher").</summary>
        public bool HoldJustStarted { get; private set; }

        /// <summary>Frame result: the direction a swipe resolved to, if any.</summary>
        public SwipeDirection Swipe { get; private set; }

        /// <summary>Raised on release for a short press that did not move.</summary>
        public bool Tapped { get; private set; }

        public void Begin(Vector2 position)
        {
            _active = true;
            _start = position;
            _current = position;
            _lastFramePosition = position;
            _elapsed = 0f;
            _swipeFired = false;
            _heldReported = false;
            Swipe = SwipeDirection.None;
            Tapped = false;
            HoldJustStarted = false;
            FrameDelta = Vector2.zero;
        }

        /// <summary>Advances a live press. Call once per frame while the finger is down.</summary>
        public void Move(Vector2 position, float dt)
        {
            if (!_active) return;

            FrameDelta = position - _lastFramePosition;
            _lastFramePosition = position;

            _current = position;
            _elapsed += dt;
            Tapped = false;

            bool wasHolding = _heldReported;
            HoldJustStarted = !wasHolding && _elapsed >= HoldSeconds;
            if (HoldJustStarted) _heldReported = true;

            // A swipe resolves as soon as the finger has travelled far enough, which makes
            // the lane change feel immediate instead of waiting for the finger to lift.
            if (!_swipeFired && _elapsed <= SwipeMaxSeconds)
            {
                var delta = Delta;
                float ax = Mathf.Abs(delta.x), ay = Mathf.Abs(delta.y);
                if (Mathf.Max(ax, ay) >= SwipeThreshold)
                {
                    _swipeFired = true;
                    // Screen Y grows DOWNWARDS, so a smaller Y is "up". Getting this backwards
                    // makes an upward swipe slide instead of jump, which is exactly the kind of
                    // bug a phone makes hard to notice and a test makes obvious.
                    Swipe = ax >= ay
                        ? (delta.x > 0f ? SwipeDirection.Right : SwipeDirection.Left)
                        : (delta.y < 0f ? SwipeDirection.Up : SwipeDirection.Down);
                }
            }
        }

        /// <summary>Ends the press. Reports a tap when the finger barely moved.</summary>
        public void End(Vector2 position, float dt)
        {
            if (!_active) return;

            Move(position, dt);
            _active = false;
            HoldJustStarted = false;

            var delta = Delta;
            bool smallMovement = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < TapMaxMovement;
            Tapped = !_swipeFired && smallMovement && _elapsed <= TapMaxSeconds;

            // The swipe flag survives the release so the consumer still sees it this frame.
        }

        /// <summary>Clears one-frame flags. Call at the end of the frame that consumed them.</summary>
        public void ClearFrameFlags()
        {
            if (!_active)
            {
                Swipe = SwipeDirection.None;
                Tapped = false;
            }
            HoldJustStarted = false;
        }

        /// <summary>Test/manual helper: consume the swipe so it is not seen twice.</summary>
        public void ConsumeSwipe() => Swipe = SwipeDirection.None;

        /// <summary>
        /// Clears the per-frame movement, after a consumer has applied it.
        ///
        /// A continuous drag is the one gesture whose result is *not* a flag, and that makes it the
        /// one that can be applied twice: the delta stays in this object until the next
        /// <see cref="Move"/>, which is normally the next frame but is not guaranteed to be — a
        /// finger whose events stop arriving leaves the last delta sitting here, and a consumer that
        /// reads it every frame would slide the basket across the room on its own. The consumer
        /// takes it, and clears it.
        /// </summary>
        public void ConsumeFrameDelta() => FrameDelta = Vector2.zero;

        public void ConsumeTap() => Tapped = false;
    }
}
