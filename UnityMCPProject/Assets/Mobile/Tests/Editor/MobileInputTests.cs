using NUnit.Framework;
using UnityEngine;

namespace DshMobile.Tests
{
    /// <summary>
    /// Covers the touch layer: gesture recognition, the virtual stick, the HUD scale, and the
    /// phone control layout.
    ///
    /// All of it is arithmetic on positions and sizes, which is exactly the kind of thing that
    /// cannot be checked by looking at a phone screenshot — a swipe threshold that is 2% too
    /// large just feels like the game ignoring you.
    /// </summary>
    public class MobileInputTests
    {
        private const float ShortSide = 1080f;   // a typical phone's short side, in pixels

        // ------------------------------------------------------------------- gestures

        private static TouchGesture NewGesture()
        {
            // Pin the reference size so the thresholds mean the same thing here as on a device,
            // instead of following the editor's Game view.
            return new TouchGesture { ReferenceSize = ShortSide };
        }

        /// <summary>Presses, drags by a fraction of the short side, and releases.</summary>
        private static void Drag(TouchGesture gesture, Vector2 from, Vector2 to, float seconds, float dt = 0.02f)
        {
            gesture.Begin(from);
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 1; i <= steps; i++)
            {
                gesture.Move(Vector2.Lerp(from, to, (float)i / steps), dt);
            }
            gesture.End(to, dt);
        }

        [Test]
        public void Gesture_ShortPressIsATap()
        {
            var gesture = NewGesture();
            var point = new Vector2(540f, 540f);

            gesture.Begin(point);
            gesture.Move(point + new Vector2(3f, 2f), 0.05f);
            gesture.End(point + new Vector2(3f, 2f), 0.02f);

            Assert.IsTrue(gesture.Tapped, "a short press that barely moves is a tap");
            Assert.AreEqual(SwipeDirection.None, gesture.Swipe);
        }

        [Test]
        public void Gesture_HorizontalDragIsALaneSwipe()
        {
            var gesture = NewGesture();
            float travel = ShortSide * 0.12f;

            Drag(gesture, new Vector2(400f, 500f), new Vector2(400f + travel, 500f), 0.2f);
            Assert.AreEqual(SwipeDirection.Right, gesture.Swipe, "dragging right changes lane right");
            Assert.IsFalse(gesture.Tapped, "a swipe is not also a tap");

            Drag(gesture, new Vector2(800f, 500f), new Vector2(800f - travel, 500f), 0.2f);
            Assert.AreEqual(SwipeDirection.Left, gesture.Swipe);
        }

        [Test]
        public void Gesture_VerticalDragIsJumpOrSlide()
        {
            var gesture = NewGesture();
            float travel = ShortSide * 0.12f;

            // Screen Y grows downwards, so "up" on screen is a smaller Y.
            Drag(gesture, new Vector2(500f, 700f), new Vector2(500f, 700f - travel), 0.2f);
            Assert.AreEqual(SwipeDirection.Up, gesture.Swipe, "dragging up jumps");

            Drag(gesture, new Vector2(500f, 300f), new Vector2(500f, 300f + travel), 0.2f);
            Assert.AreEqual(SwipeDirection.Down, gesture.Swipe, "dragging down slides");
        }

        [Test]
        public void Gesture_SmallMovementInsideTheThresholdIsNotASwipe()
        {
            var gesture = NewGesture();
            // Just under the threshold, and a bit too slow to be a flick either.
            float travel = ShortSide * gesture.SwipeThreshold * 0.7f;

            Drag(gesture, new Vector2(500f, 500f), new Vector2(500f + travel, 500f), 0.15f);

            Assert.AreEqual(SwipeDirection.None, gesture.Swipe, "a nudge must not change lane");
        }

        [Test]
        public void Gesture_SlowSmallDragIsNotASwipe()
        {
            var gesture = NewGesture();
            // Well under the distance threshold, and taken slowly: a pan, not a flick.
            float travel = ShortSide * gesture.SwipeThreshold * 0.5f;

            Drag(gesture, new Vector2(300f, 500f), new Vector2(300f + travel, 500f), 1.2f, 0.05f);

            Assert.AreEqual(SwipeDirection.None, gesture.Swipe,
                "a short slow drag must not change lane");
        }

        [Test]
        public void Gesture_QuickFlickFiresEvenIfShort()
        {
            var gesture = NewGesture();
            float travel = ShortSide * (gesture.SwipeThreshold + 0.01f);

            // Same order of distance as the previous test, but flicked: this is what a player
            // does when they want to change lane quickly.
            Drag(gesture, new Vector2(300f, 500f), new Vector2(300f + travel, 500f), 0.1f, 0.02f);

            Assert.AreEqual(SwipeDirection.Right, gesture.Swipe);
        }

        [Test]
        public void Gesture_SwipeFiresOncePerPress()
        {
            var gesture = NewGesture();
            float travel = ShortSide * 0.12f;

            gesture.Begin(new Vector2(200f, 500f));
            gesture.Move(new Vector2(200f + travel, 500f), 0.05f);
            Assert.AreEqual(SwipeDirection.Right, gesture.Swipe, "fires as soon as it is far enough");

            // Keep dragging in the same direction: the swipe must not repeat, or one drag
            // would jump two lanes.
            gesture.Move(new Vector2(200f + travel * 2f, 500f), 0.05f);
            Assert.AreEqual(SwipeDirection.Right, gesture.Swipe);
            gesture.ConsumeSwipe();

            gesture.Move(new Vector2(200f + travel * 3f, 500f), 0.05f);
            Assert.AreEqual(SwipeDirection.None, gesture.Swipe,
                "a swipe is consumed once per press, not once per frame");
        }

        [Test]
        public void Gesture_HoldIsReportedOnceAfterTheThreshold()
        {
            var gesture = NewGesture();
            var point = new Vector2(500f, 500f);

            gesture.Begin(point);
            gesture.Move(point, 0.05f);
            Assert.IsFalse(gesture.IsHolding, "not a hold yet");

            gesture.Move(point, gesture.HoldSeconds);
            Assert.IsTrue(gesture.HoldJustStarted, "the transition is reported once");

            gesture.Move(point, 0.02f);
            Assert.IsTrue(gesture.IsHolding);
            Assert.IsFalse(gesture.HoldJustStarted, "and not again every frame");
        }

        [Test]
        public void Gesture_FrameDeltaTracksMovementBetweenFrames()
        {
            var gesture = NewGesture();
            gesture.Begin(new Vector2(100f, 100f));

            gesture.Move(new Vector2(130f, 90f), 0.02f);
            Assert.AreEqual(30f, gesture.FrameDelta.x, 0.01f);
            Assert.AreEqual(-10f, gesture.FrameDelta.y, 0.01f);

            gesture.Move(new Vector2(140f, 90f), 0.02f);
            Assert.AreEqual(10f, gesture.FrameDelta.x, 0.01f,
                "the frame delta is per frame, for continuous camera drags");
        }

        // ---------------------------------------------------------------------- stick

        [Test]
        public void Stick_InsideTheDeadZoneProducesNoMovement()
        {
            var stick = new VirtualJoystick { Radius = 100f, DeadZone = 0.2f };
            stick.Begin(0, Vector2.zero);

            stick.Move(new Vector2(10f, 0f));   // 10% of the radius
            Assert.AreEqual(Vector2.zero, stick.Value, "a resting thumb must not drift the pet");

            stick.Move(new Vector2(19f, 0f));
            Assert.AreEqual(Vector2.zero, stick.Value);
        }

        [Test]
        public void Stick_RampsFromZeroAtTheDeadZoneEdge()
        {
            var stick = new VirtualJoystick { Radius = 100f, DeadZone = 0.2f };
            stick.Begin(0, Vector2.zero);

            // Just past the dead zone: barely moving, not a jump to full speed.
            stick.Move(new Vector2(25f, 0f));
            Assert.Greater(stick.Value.x, 0f);
            Assert.Less(stick.Value.x, 0.2f, "output ramps up from the dead-zone edge");

            stick.Move(new Vector2(100f, 0f));
            Assert.AreEqual(1f, stick.Value.x, 0.01f, "full deflection is full speed");
        }

        [Test]
        public void Stick_ClampsBeyondTheRadius()
        {
            var stick = new VirtualJoystick { Radius = 100f, DeadZone = 0f };
            stick.Begin(0, Vector2.zero);

            stick.Move(new Vector2(500f, 0f));

            Assert.AreEqual(1f, stick.Value.magnitude, 0.01f, "a huge drag is still 1.0");
            Assert.AreEqual(100f, stick.Knob.magnitude, 0.5f, "the knob stays on the rim");
        }

        [Test]
        public void Stick_ScreenUpIsForward()
        {
            var stick = new VirtualJoystick { Radius = 100f, DeadZone = 0f };
            stick.Begin(0, new Vector2(500f, 500f));

            // Dragging towards the top of the screen must be +Y in gameplay space.
            stick.Move(new Vector2(500f, 400f));
            Assert.AreEqual(1f, stick.Value.y, 0.01f, "pushing up walks forward");

            stick.Move(new Vector2(500f, 600f));
            Assert.AreEqual(-1f, stick.Value.y, 0.01f, "pushing down walks back");
        }

        [Test]
        public void Stick_ReleaseStopsMovement()
        {
            var stick = new VirtualJoystick { Radius = 100f, DeadZone = 0f };
            stick.Begin(0, Vector2.zero);
            stick.Move(new Vector2(100f, 0f));
            Assert.AreNotEqual(Vector2.zero, stick.Value);

            stick.End();

            Assert.IsFalse(stick.Active);
            Assert.AreEqual(Vector2.zero, stick.Value, "letting go must stop the character");
        }

        // ----------------------------------------------------------------------- scale

        [Test]
        public void Scale_GrowsWithTheScreenAndIsClamped()
        {
            // A small phone: below the floor the text would be unreadable.
            Assert.AreEqual(0.9f, MobileUi.ScaleFor(400f, 0f), 0.001f,
                "tiny screens are clamped to the minimum scale");

            // 720p is the reference the layout was tuned against.
            Assert.AreEqual(1f, MobileUi.ScaleFor(720f, 0f), 0.001f);

            // A 1080p phone gets bigger controls.
            Assert.AreEqual(1.5f, MobileUi.ScaleFor(1080f, 0f), 0.001f);

            // A 1440p phone is clamped so one panel cannot swallow the screen.
            Assert.AreEqual(1.8f, MobileUi.ScaleFor(1440f, 0f), 0.001f);
            Assert.AreEqual(1.8f, MobileUi.ScaleFor(2400f, 0f), 0.001f);
        }

        [Test]
        public void Scale_IgnoresImplausibleDpi()
        {
            // Screen.dpi is 0 on many devices and in the editor; it must not distort the scale.
            Assert.AreEqual(MobileUi.ScaleFor(1080f, 0f), MobileUi.ScaleFor(1080f, 40f), 0.0001f);

            // A genuinely high-dpi screen gets a small upward correction.
            Assert.Greater(MobileUi.ScaleFor(1080f, 500f), MobileUi.ScaleFor(1080f, 0f));
        }

        [Test]
        public void TouchTarget_NeverGetsSmallerThanAFinger()
        {
            Assert.AreEqual(MobileUi.MinTouchTarget, MobileUi.Touchable(10f));
            Assert.AreEqual(MobileUi.MinTouchTarget, MobileUi.Touchable(MobileUi.MinTouchTarget));
            Assert.AreEqual(120f, MobileUi.Touchable(120f));
        }

        // ------------------------------------------------------------ phone HUD layout

        /// <summary>Design-space viewports, i.e. what a phone's screen divides down to.</summary>
        private static readonly Vector2[] PhoneDesigns =
        {
            new Vector2(1600f, 720f),   // 2400x1080 at scale 1.5
            new Vector2(1422f, 800f),   // 2560x1440 at scale 1.8
            new Vector2(1280f, 640f),   // 1920x960 at 1.5 — a short landscape phone
            new Vector2(1067f, 480f),   // 1600x720 at 1.5
            new Vector2(960f, 540f)     // 1280x720 at scale 1.33
        };

        [Test]
        public void PhoneLayout_ControlsAreTappableAndDoNotOverlap()
        {
            foreach (var design in PhoneDesigns)
            {
                var layout = DshPet.PetHud.ComputeLayout(design.x, design.y, 4);
                var controls = DshPet.PetHud.ComputeMobileControls(layout);
                string where = $"at {design.x}x{design.y}";

                // Tappable.
                foreach (var pair in new[]
                         {
                             new { Name = "action", Rect = controls.Action },
                             new { Name = "throw", Rect = controls.Throw },
                             new { Name = "chat", Rect = controls.Chat }
                         })
                {
                    Assert.GreaterOrEqual(pair.Rect.width, MobileUi.MinTouchTarget,
                        $"{pair.Name} is too narrow to tap {where}");
                    Assert.GreaterOrEqual(pair.Rect.height, MobileUi.MinTouchTarget,
                        $"{pair.Name} is too short to tap {where}");
                }

                // On screen.
                foreach (var rect in new[] { controls.Action, controls.Throw, controls.Chat, controls.StickZone })
                {
                    Assert.GreaterOrEqual(rect.x, 0f, $"control off the left edge {where}");
                    Assert.GreaterOrEqual(rect.y, 0f, $"control off the top edge {where}");
                    Assert.LessOrEqual(rect.xMax, design.x + 0.5f, $"control off the right edge {where}");
                    Assert.LessOrEqual(rect.yMax, design.y + 0.5f, $"control off the bottom edge {where}");
                }

                // The action and throw buttons must not sit on top of each other, or a
                // thumb press would be a coin flip.
                Assert.IsFalse(controls.Action.Overlaps(controls.Throw),
                    $"the action and throw buttons overlap {where}");

                // And they must not sit under the chat bar, which also takes touches.
                var chatBar = new Rect(layout.Chat.x,
                    layout.Chat.yMax - DshPet.PetHud.MobileChatBarHeight,
                    layout.Chat.width, DshPet.PetHud.MobileChatBarHeight);
                Assert.IsFalse(controls.Action.Overlaps(chatBar), $"action button under the chat bar {where}");
                Assert.IsFalse(controls.Throw.Overlaps(chatBar), $"throw button under the chat bar {where}");

                // The stick belongs to the left thumb.
                Assert.Less(controls.StickZone.center.x, design.x * 0.5f,
                    $"the stick zone drifted to the right thumb {where}");
            }
        }

        [Test]
        public void PhoneLayout_StatusAndChatStillDoNotOverlap()
        {
            // The desktop invariant, re-checked at phone design sizes: the chat panel is drawn
            // after the status panel, so any overlap swallows the buttons.
            foreach (var design in PhoneDesigns)
            {
                var layout = DshPet.PetHud.ComputeLayout(design.x, design.y, 4);
                Assert.LessOrEqual(layout.Status.yMax, layout.Chat.y,
                    $"status and chat overlap at {design.x}x{design.y}");
            }
        }

        // ------------------------------------------------- widget coordinate spaces

        /// <summary>
        /// The HUD draws through <c>GUI.matrix</c> in design pixels, while the touch layer
        /// hit-tests real screen pixels. Widgets must therefore convert exactly once. Doing it
        /// twice (handing screen rects to something that draws under the matrix) applies scale²
        /// — a 10% error at the editor's 0.9 that nobody notices, and 3.24x at the 1.8 a phone
        /// actually uses, which puts the buttons off the screen entirely. These tests pin the
        /// transform down.
        /// </summary>
        [Test]
        public void Widgets_ToScreenAppliesTheHudTransformOnce()
        {
            MobileWidgets.BeginFrame(1.8f, new Vector2(12f, 34f));

            var design = new Rect(100f, 200f, 50f, 40f);
            var screen = MobileWidgets.ToScreen(design);

            Assert.AreEqual(12f + 100f * 1.8f, screen.x, 0.001f, "x is offset then scaled");
            Assert.AreEqual(34f + 200f * 1.8f, screen.y, 0.001f, "y is offset then scaled");
            Assert.AreEqual(50f * 1.8f, screen.width, 0.001f, "width is scaled once, not twice");
            Assert.AreEqual(40f * 1.8f, screen.height, 0.001f, "height is scaled once, not twice");
        }

        [Test]
        public void Widgets_ToDesignIsTheInverseOfToScreen()
        {
            foreach (float scale in new[] { 0.9f, 1.33f, 1.8f })
            {
                MobileWidgets.BeginFrame(scale, new Vector2(60f, 0f));

                var screen = new Vector2(640f, 360f);
                var design = MobileWidgets.ToDesign(screen);
                var back = MobileWidgets.ToScreen(new Rect(design.x, design.y, 0f, 0f));

                Assert.AreEqual(screen.x, back.x, 0.001f, $"x round-trips at scale {scale}");
                Assert.AreEqual(screen.y, back.y, 0.001f, $"y round-trips at scale {scale}");
            }
        }

        [Test]
        public void Widgets_NoTransformMeansScreenEqualsDesign()
        {
            // The desktop case, and the guard against a zero scale producing NaN rects.
            MobileWidgets.BeginFrame(0f, Vector2.zero);
            Assert.AreEqual(1f, MobileWidgets.Scale, 0.001f, "a zero scale falls back to 1");

            var rect = new Rect(5f, 6f, 7f, 8f);
            var screen = MobileWidgets.ToScreen(rect);
            Assert.AreEqual(rect.x, screen.x, 0.001f);
            Assert.AreEqual(rect.y, screen.y, 0.001f);
            Assert.AreEqual(rect.width, screen.width, 0.001f);
            Assert.AreEqual(rect.height, screen.height, 0.001f);
        }

        [Test]
        public void Widgets_TheJoystickLandsUnderTheThumbThatHoldsIt()
        {
            // The stick's origin comes back from the touch layer in screen pixels and is drawn
            // in design space, so a round trip must return the finger's own position.
            const float scale = 1.8f;
            var safe = new Vector2(88f, 0f);
            MobileWidgets.BeginFrame(scale, safe);

            var finger = new Vector2(300f, 900f);
            var drawn = MobileWidgets.ToDesign(finger);

            Assert.AreEqual(300f, safe.x + drawn.x * scale, 0.001f, "the stick is drawn where the thumb is");
            Assert.AreEqual(900f, safe.y + drawn.y * scale, 0.001f, "the stick is drawn where the thumb is");
        }
    }
}
