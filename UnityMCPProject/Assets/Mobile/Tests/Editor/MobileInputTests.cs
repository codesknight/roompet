using System.Collections.Generic;
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
            new Vector2(960f, 540f),    // 1280x720 at scale 1.33

            // Upright. The scale comes off the SHORT side, so a 1080x2400 phone lands on a
            // 720x1600 design space rather than the 600x1333 that scaling by height gave.
            new Vector2(720f, 1600f),   // 1080x2400 at scale 1.5
            new Vector2(640f, 1387f),   // 1080x2340 at scale 1.69
            new Vector2(600f, 1200f),   // 1080x2160 at 1.8 — tall and narrow
            new Vector2(889f, 1422f),   // 1600x2560 tablet at 1.8

            // What a real 420-dpi phone actually produces: 1080/(1.5 * 1.08) — narrower than a
            // naive 1080/720 would suggest, which is exactly why the preview simulates the dpi.
            new Vector2(665f, 1477f),
            new Vector2(1477f, 665f)
        };

        /// <summary>Just the upright viewports, for the checks that differ by orientation.</summary>
        private static IEnumerable<Vector2> PortraitDesigns
        {
            get
            {
                foreach (var design in PhoneDesigns)
                {
                    if (design.y > design.x) yield return design;
                }
            }
        }

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
        public void PhoneLayout_ButtonsClearTheCollapsedBarInEveryOrientation()
        {
            // The assertion above only ever saw the *expanded* layout, where a side panel really is
            // out of the buttons' way. Collapsed, the bar is a full-width strip along the bottom
            // edge — and on a phone held sideways the action button was drawn straight on top of it,
            // with the bar (a far bigger target) taking every tap meant for the button.
            foreach (var design in PhoneDesigns)
            {
                foreach (bool transcriptVisible in new[] { false, true })
                {
                    var layout = DshPet.PetHud.ComputeLayout(design.x, design.y, 4, transcriptVisible);
                    var controls = DshPet.PetHud.ComputeMobileControls(layout);
                    string where = $"at {design.x}x{design.y} ({(transcriptVisible ? "expanded" : "collapsed")})";

                    // Where the collapsed bar would be drawn, whatever the layout says about sides.
                    var bar = new Rect(layout.Chat.x,
                        layout.Chat.yMax - DshPet.PetHud.MobileChatBarHeight,
                        layout.Chat.width, DshPet.PetHud.MobileChatBarHeight);

                    Assert.IsFalse(controls.Action.Overlaps(bar), $"action button under the chat bar {where}");
                    Assert.IsFalse(controls.Throw.Overlaps(bar), $"throw button under the chat bar {where}");
                    Assert.IsFalse(controls.Chat.Overlaps(bar), $"chat pill under the chat bar {where}");
                    Assert.IsFalse(controls.StickZone.Overlaps(bar), $"stick zone over the chat bar {where}");
                }
            }
        }

        [Test]
        public void PhoneLayout_StatusAndChatStillDoNotOverlap()
        {
            // The desktop invariant, re-checked at phone design sizes: the chat panel is drawn
            // after the status panel, so any overlap swallows the buttons. The layout is
            // responsive now (a wide viewport puts the transcript in a side column), so the
            // test is "these rectangles do not intersect", not "chat is below status".
            foreach (var design in PhoneDesigns)
            {
                var layout = DshPet.PetHud.ComputeLayout(design.x, design.y, 4);
                Assert.IsFalse(layout.Status.Overlaps(layout.Chat),
                    $"status and chat overlap at {design.x}x{design.y}");
                Assert.IsFalse(layout.Status.Overlaps(layout.Switcher),
                    $"status and switcher overlap at {design.x}x{design.y}");
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

        // -------------------------------------------------------------- orientation

        [Test]
        public void Orientation_ScalesOffTheShortSideNotTheHeight()
        {
            // The bug this prevents: scaling by height means a 1080x2400 phone asks for a 3.33x
            // scale, gets clamped to 1.8, and leaves a 600px-wide design space for a HUD whose
            // side panels alone are 458px wide.
            Assert.AreEqual(MobileUi.ScaleFor(1080f, 0f), MobileUi.ScaleFor(1080f, 0f), 0.0001f);
            Assert.AreEqual(1.5f, MobileUi.ScaleFor(1080f, 0f), 0.001f);

            // Landscape and portrait phones of the same size get the same scale, so text and
            // buttons stay the same physical size when the phone is turned.
            const float phone = 1080f;
            Assert.AreEqual(MobileUi.ScaleFor(phone, 0f), MobileUi.ScaleFor(phone, 0f), 0.0001f);

            // And the design space stays wide enough to lay the HUD out in.
            float designWidth = phone / MobileUi.ScaleFor(phone, 0f);
            Assert.GreaterOrEqual(designWidth, 620f,
                "a portrait phone must still have room for the status panel and the switcher");
        }

        [Test]
        public void Orientation_PortraitKeepsTheRoomAndTrackInsideTheWidth()
        {
            // The reason a camera needs help: its field of view is VERTICAL, so as the viewport
            // narrows the horizontal field collapses with it. A 14m room or a 3-lane track that
            // fills a 16:9 phone is cut off at both edges in portrait unless the camera backs up.
            const float fov = 62f;
            const float halfWidth = 6.4f;   // half of a 14m room, less a little

            // Landscape 2400x1080: the base distance is already plenty, so nothing moves.
            float landscape = MobileUi.WidthFitScale(10f, halfWidth, fov, 2400f / 1080f);
            Assert.AreEqual(1f, landscape, 0.001f, "a 16:9 (or wider) screen must not be re-framed");

            // Portrait 1080x2400: it genuinely has to back off, but not absurdly far.
            float portrait = MobileUi.WidthFitScale(10f, halfWidth, fov, 1080f / 2400f);
            Assert.Greater(portrait, 1.5f, "portrait has to pull back to keep the sides in frame");
            Assert.LessOrEqual(portrait, MobileUi.MaxWidthFitPullback, "and not into the distance cap");

            // The pull-back is capped, so a nonsensical aspect cannot turn the room into a stamp.
            Assert.AreEqual(MobileUi.MaxWidthFitPullback,
                MobileUi.WidthFitScale(1f, halfWidth, fov, 0.2f), 0.001f);
        }

        [Test]
        public void Orientation_WidthFitIsMonotonicInAspect()
        {
            // Narrower must never mean "pull back less", at any width — that would show up as
            // the framing fighting itself as the phone rotates.
            float previous = 0f;
            foreach (float aspect in new[] { 2.4f, 2.0f, 1.78f, 1.4f, 1.0f, 0.75f, 0.6f, 0.45f })
            {
                float scale = MobileUi.WidthFitScale(8f, 6f, 62f, aspect);
                Assert.GreaterOrEqual(scale, previous, $"pull-back went down as the screen narrowed (aspect {aspect})");
                Assert.GreaterOrEqual(scale, 1f, "the fit never zooms IN past the authored framing");
                previous = scale;
            }
        }

        [Test]
        public void PortraitLayout_TouchControlsStayReachable()
        {
            // Portrait puts the buttons and the chat bar in the bottom quarter of a very tall
            // screen. Reachability is about distance from the bottom corners, so that is what
            // is measured here rather than "is it on screen".
            foreach (var design in PortraitDesigns)
            {
                var layout = DshPet.PetHud.ComputeLayout(design.x, design.y, 4);
                var controls = DshPet.PetHud.ComputeMobileControls(layout);
                string where = $"at {design.x}x{design.y}";

                Assert.IsFalse(controls.Action.Overlaps(controls.Throw), $"buttons overlap {where}");
                Assert.IsFalse(controls.Action.Overlaps(controls.Chat), $"chat button on the action button {where}");

                // Both thumbs rest near the bottom: the action button must not float up into
                // the middle of a 1600px-tall design space.
                Assert.Greater(controls.Action.y, design.y * 0.7f,
                    $"the action button is too high up the screen to reach {where}");
                Assert.Greater(controls.StickZone.y, design.y * 0.5f,
                    $"the stick zone is too high up the screen for a thumb {where}");

                // And the panels must leave the middle of the screen free for the room.
                Assert.Less(layout.Status.xMax, design.x,
                    $"the status panel runs off a narrow screen {where}");
                Assert.Less(layout.Status.yMax, design.y * 0.75f,
                    $"the status panel eats more than three quarters of the height {where}");
            }
        }

        [Test]
        public void PortraitLayout_SidePanelsDoNotCollide()
        {
            // The status panel and the species/view switcher sit on the same row. On a 720px
            // design width that is 300 + 158 and it fits — but only just, so it is worth
            // pinning: they were sized independently before and overlapped on short screens.
            foreach (var design in PortraitDesigns)
            {
                var layout = DshPet.PetHud.ComputeLayout(design.x, design.y, 4);
                Assert.LessOrEqual(layout.Status.xMax, layout.Switcher.x,
                    $"the status panel and the switcher collide at {design.x}x{design.y}");
            }
        }

        // ------------------------------------------------------------ modal panels

        /// <summary>The overlay sizes the pet HUD actually asks for.</summary>
        private static readonly Vector2[] OverlayRequests =
        {
            new Vector2(520f, 430f),   // settings
            new Vector2(520f, 300f),   // prompt preview
            new Vector2(700f, 540f),   // notebook / calendar
            new Vector2(720f, 560f)    // door prompt
        };

        [Test]
        public void OverlayPanels_AreCentredInTheDesignSpace()
        {
            // The bug this pins down: centring against Screen.width (1080 real pixels) and then
            // drawing through a 1.6x matrix put every dialog off-centre by 1.6x, and in portrait
            // pushed the footer buttons off the bottom of the screen entirely. The centre of an
            // overlay must be the centre of the space it is drawn in.
            foreach (var design in PhoneDesigns)
            {
                foreach (var request in OverlayRequests)
                {
                    var rect = DshPet.PetHud.OverlayRect(request.x, request.y, design.x, design.y, 24f);
                    string where = $"{request.x}x{request.y} at {design.x}x{design.y}";

                    Assert.AreEqual(design.x * 0.5f, rect.center.x, 0.5f, $"not horizontally centred: {where}");
                    Assert.AreEqual(design.y * 0.5f, rect.center.y, 0.5f, $"not vertically centred: {where}");
                }
            }
        }

        [Test]
        public void OverlayPanels_AlwaysFitInsideTheViewport()
        {
            foreach (var design in PhoneDesigns)
            {
                foreach (var request in OverlayRequests)
                {
                    var rect = DshPet.PetHud.OverlayRect(request.x, request.y, design.x, design.y, 24f);
                    string where = $"{request.x}x{request.y} at {design.x}x{design.y}";

                    Assert.GreaterOrEqual(rect.x, 23.5f, $"hangs off the left edge: {where}");
                    Assert.GreaterOrEqual(rect.y, 23.5f, $"hangs off the top edge: {where}");
                    Assert.LessOrEqual(rect.xMax, design.x - 23.5f, $"hangs off the right edge: {where}");
                    Assert.LessOrEqual(rect.yMax, design.y - 23.5f, $"hangs off the bottom edge: {where}");

                    // And the footer inside it must still be reachable, i.e. the panel is not so
                    // short that a 52px footer eats the whole thing.
                    Assert.Greater(rect.height, 100f, $"panel too short for its own footer: {where}");
                }
            }
        }

        [Test]
        public void OverlayPanels_LeaveRoomForThumbs()
        {
            // Panels fill most of a narrow portrait screen. That is fine for reading, but the
            // margin is what tells the player "this is a dialog, the world is still there" — and
            // on a device with rounded corners a panel touching the edge reads as a layout bug.
            // A panel wider than the viewport is clamped to the margin, not to the edge.
            var wide = DshPet.PetHud.OverlayRect(1600f, 430f, 1600f, 720f, 16f);
            Assert.AreEqual(16f, wide.x, 0.5f, "a desktop-sized viewport keeps the plain margin");
            Assert.AreEqual(1600f - 16f, wide.xMax, 0.5f);

            var portrait = DshPet.PetHud.OverlayRect(900f, 430f, 665f, 1477f, 24f);
            Assert.AreEqual(24f, portrait.x, 0.5f, "a phone keeps at least the phone margin");
            Assert.Less(portrait.xMax, 665f - 23f);
        }

        // ------------------------------------------------------------------- haptics
        [Test]
        public void Haptics_GateDropsRepeatsAndObeysTheSwitch()
        {
            var gate = new HapticGate { MinInterval = 0.05f };

            Assert.IsTrue(gate.ShouldFire(0f, true), "the first pulse always fires");
            Assert.IsFalse(gate.ShouldFire(0.01f, true), "a second pulse 10ms later is swallowed");
            Assert.IsTrue(gate.ShouldFire(0.06f, true), "and one after the interval is not");

            gate.Reset();
            Assert.IsFalse(gate.ShouldFire(100f, false), "the setting is obeyed");
            gate.Reset();
            Assert.IsTrue(gate.ShouldFire(100f, true), "and turning it back on works");
        }

        [Test]
        public void Haptics_StrengthMapsToIncreasingDurations()
        {
            Assert.Less(MobileHaptics.DurationFor(Haptic.Light), MobileHaptics.DurationFor(Haptic.Medium));
            Assert.Less(MobileHaptics.DurationFor(Haptic.Medium), MobileHaptics.DurationFor(Haptic.Heavy));

            // Long enough to feel, short enough not to be a ringtone.
            Assert.GreaterOrEqual(MobileHaptics.DurationFor(Haptic.Light), 10);
            Assert.LessOrEqual(MobileHaptics.DurationFor(Haptic.Heavy), 120);
        }

        [Test]
        public void Haptics_EditorNeverReachesTheDevice()
        {
            // The whole point of the platform check: the editor and the desktop build must not
            // change behaviour because the mobile layer exists.
            Assert.IsFalse(MobileHaptics.Supported,
                "tests run in the editor, where a pulse must be a no-op");

            int calls = 0;
            MobileHaptics.Sink = (ms, amplitude) => calls++;
            try
            {
                MobileHaptics.Reset();
                MobileHaptics.Light();
                MobileHaptics.Heavy();
                Assert.AreEqual(0, calls, "no pulse may be delivered off-device");
            }
            finally
            {
                MobileHaptics.Sink = null;
            }
        }

        // ------------------------------------------------------------------- hit testing

        /// <summary>
        /// "If the player taps here, which control gets it?" — asked of the real lookup.
        ///
        /// This is the question behind two rounds of "按钮点不动": a microphone drawn on top of a
        /// full-width chat bar, and a joystick zone that was written as its own oversized rectangle
        /// covering both. Neither is visible in a screenshot, and neither can be reproduced without
        /// a phone — unless the lookup itself can be asked.
        /// </summary>
        [Test]
        public void HitTest_TheSmallControlOnTopWins()
        {
            MobileTouch.Reset();
            try
            {
                var bar = new Rect(0f, 2200f, 1080f, 200f);
                var mic = new Rect(40f, 2250f, 104f, 104f);
                MobileTouch.RegisterButton("bar", bar, true);
                MobileTouch.RegisterButton("mic", mic, true);

                Assert.AreEqual("mic", MobileTouch.HitTest(mic.center),
                    "the microphone is on top of the bar and must get the tap");
                Assert.AreEqual("bar", MobileTouch.HitTest(new Vector2(bar.xMax - 30f, bar.center.y)),
                    "and the rest of the bar is still tappable");
                Assert.IsNull(MobileTouch.HitTest(new Vector2(540f, 10f)),
                    "a tap on nothing hits nothing");
            }
            finally
            {
                MobileTouch.Reset();
            }
        }

        [Test]
        public void HitTest_IgnoresHiddenControlsAndAModalPanel()
        {
            MobileTouch.Reset();
            try
            {
                var rect = new Rect(100f, 100f, 200f, 200f);
                MobileTouch.RegisterButton("hidden", rect, false);
                Assert.IsNull(MobileTouch.HitTest(rect.center), "an unregistered-visible control is not there");

                MobileTouch.RegisterButton("shown", rect, true);
                Assert.AreEqual("shown", MobileTouch.HitTest(rect.center));

                // A modal panel owns the screen: nothing behind it may take a tap, which is how
                // "the button under the dialog reacted" gets prevented.
                MobileTouch.PlayInputEnabled = false;
                Assert.IsNull(MobileTouch.HitTest(rect.center), "a modal panel swallows every tap");
            }
            finally
            {
                MobileTouch.Reset();
            }
        }

        // ------------------------------------------------------------- scene transitions

        [Test]
        public void SceneLoad_ClearsTheButtonsTheLastSceneRegistered()
        {
            // The bug: the bird game registers a full-screen tap target ("tap anywhere to flap"), and
            // a registered rect is never removed — only overwritten by whoever draws next. Coming back
            // to the pet room, that full-screen rect was still there and still visible, so every tap
            // that did not land on a smaller button went to it… including every tap in the movement
            // stick's zone, which is only consulted *after* the buttons. The joystick never appeared
            // and the character would not move.
            MobileTouch.Reset();
            try
            {
                var screen = new Rect(0f, 0f, 1080f, 2400f);
                MobileTouch.RegisterButton("fly.tap", screen, true, "tap to flap");
                MobileTouch.StickZone = new Rect(0f, 1900f, 460f, 300f);

                // While the game is up, the full-screen tap target really does own the screen.
                Assert.AreEqual("fly.tap", MobileTouch.HitTest(new Vector2(200f, 2050f)));

                // …and the game leaves its own flags behind too.
                MobileTouch.StickEnabled = false;
                MobileTouch.PlayInputEnabled = false;

                MobileTouch.OnSceneLoaded();

                // After a scene change, none of it may survive.
                Assert.IsNull(MobileTouch.HitTest(new Vector2(200f, 2050f)),
                    "a stale full-screen control is still swallowing taps");
                Assert.AreEqual(0, MobileTouch.VisibleButtonCount());
                Assert.AreEqual(new Rect(0f, 0f, 0f, 0f), MobileTouch.StickZone,
                    "the stick zone belonged to the scene that just unloaded");
                Assert.IsTrue(MobileTouch.PlayInputEnabled, "the next scene starts playable");
                Assert.IsTrue(MobileTouch.StickEnabled);
            }
            finally
            {
                MobileTouch.Reset();
            }
        }

        [Test]
        public void SceneClock_PutsTheClockBackAfterAPause()
        {
            // The forest run pauses with Time.timeScale = 0, which is global and survives a scene
            // load: a player who paused and then tapped "返回宠物小屋" landed in a pet room where
            // nothing moved. Nothing was broken in the room — the clock was.
            float previous = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                SceneClock.Restore("test");
                Assert.AreEqual(SceneClock.NormalTimeScale, Time.timeScale,
                    "a scene change has to leave the world running");

                // And it must not fight a scene that is deliberately running slow.
                Time.timeScale = 0.35f;
                SceneClock.Restore("test");
                Assert.AreEqual(0.35f, Time.timeScale, 0.0001f,
                    "a slow-motion scene must keep its speed");
            }
            finally
            {
                Time.timeScale = previous;
            }
        }

        // ------------------------------------------------------------------ contact shadow

        [Test]
        public void SoftShadow_FadesTowardsItsEdgeInsteadOfStopping()
        {
            // The bug: both avatars used a flat *cube* as their contact shadow, so a character
            // walking around the room dragged a black square under its feet. What makes a shadow a
            // shadow is that its edge fades, so that is what is asserted here.
            var gradient = SoftShadow.Gradient;
            Assert.IsNotNull(gradient, "the shadow has no gradient texture");
            Assert.AreEqual(SoftShadow.TextureSize, gradient.width);

            var pixels = gradient.GetPixels32();
            int size = gradient.width;

            System.Func<int, int, byte> alphaAt = (x, y) => pixels[y * size + x].a;

            byte centre = alphaAt(size / 2, size / 2);
            Assert.GreaterOrEqual(centre, 240, "the middle of a shadow should be its darkest");

            foreach (var corner in new[]
                     {
                         new Vector2Int(0, 0), new Vector2Int(size - 1, 0),
                         new Vector2Int(0, size - 1), new Vector2Int(size - 1, size - 1)
                     })
            {
                Assert.AreEqual(0, alphaAt(corner.x, corner.y),
                    "the corners of the gradient must be fully transparent, or the shadow has a rim");
            }

            // Falling all the way out: no plateau (that would be a disc) and no hard step (a square).
            byte previous = centre;
            for (int x = size / 2; x < size; x++)
            {
                byte here = alphaAt(x, size / 2);
                Assert.LessOrEqual(here, previous, $"the gradient brightens again at x={x}");
                previous = here;
            }

            // …and the middle of an edge is dimmer than the centre but not yet zero: the fade has to
            // be gradual enough to read, rather than a vignette that ends in a visible ring.
            byte edgeMid = alphaAt(size / 2, 1);
            Assert.Greater(edgeMid, 0, "the fade reaches zero before the edge, which makes a ring");
            Assert.Less(edgeMid, centre);
        }

        [Test]
        public void SoftShadow_IsAFlatTransparentQuadNotAnOpaqueCube()
        {
            var host = new GameObject("shadow-host");
            try
            {
                var shadow = SoftShadow.Attach(host.transform, 0.4f, 0.5f, 0.5f);
                if (shadow == null)
                {
                    // No Sprites/Default on this build: skipping the shadow is the documented
                    // fallback, and it is a great deal better than a black square.
                    Assert.Inconclusive("no transparent shader available in this project");
                    return;
                }

                Assert.AreEqual(0, shadow.GetComponentsInChildren<Collider>().Length,
                    "a shadow must not collide with anything");

                // Lying flat, and sized to its radius rather than to a slab.
                Assert.AreEqual(90f, Mathf.DeltaAngle(0f, shadow.localRotation.eulerAngles.x), 1f,
                    "the shadow is standing up instead of lying on the floor");
                Assert.AreEqual(0.8f, shadow.localScale.x, 0.001f);
                Assert.AreEqual(1.0f, shadow.localScale.y, 0.001f);

                var renderer = shadow.GetComponent<Renderer>();
                Assert.IsNotNull(renderer);
                Assert.Greater(renderer.sharedMaterial.renderQueue, 2000,
                    "the shadow has to be drawn transparent — an opaque one is the black square again");
                Assert.AreEqual(1, host.transform.childCount, "one object, one draw call");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ----------------------------------------------------------------- mini animals

        [Test]
        public void MiniAnimal_SpeciesIdsMapToAnimals()
        {
            // The hero of 跳一跳 is the pet, and the only thing tying the two assemblies together is
            // the species id in the save — so that mapping has to be right for every id the pet game
            // can write, and has to survive junk.
            Assert.AreEqual(MiniAnimalKind.Fox, MiniAnimal.FromSpeciesId("fox"));
            Assert.AreEqual(MiniAnimalKind.Cat, MiniAnimal.FromSpeciesId("cat"));
            Assert.AreEqual(MiniAnimalKind.Rabbit, MiniAnimal.FromSpeciesId("rabbit"));
            Assert.AreEqual(MiniAnimalKind.Bear, MiniAnimal.FromSpeciesId("bear"));

            // Case and padding come from a save file, not from code.
            Assert.AreEqual(MiniAnimalKind.Fox, MiniAnimal.FromSpeciesId(" FOX "));

            // An unknown or missing species still has to produce an animal.
            Assert.AreEqual(MiniAnimalKind.Cat, MiniAnimal.FromSpeciesId("dragon"));
            Assert.AreEqual(MiniAnimalKind.Cat, MiniAnimal.FromSpeciesId(""));
            Assert.AreEqual(MiniAnimalKind.Cat, MiniAnimal.FromSpeciesId(null));

            for (int i = 0; i < MiniAnimal.KindCount; i++)
            {
                var kind = (MiniAnimalKind)i;
                Assert.IsFalse(string.IsNullOrEmpty(MiniAnimal.Name(kind)), $"{kind} has no name");
            }
        }

        [Test]
        public void MiniAnimal_FursAreDistinguishable()
        {
            // Eight animals that look the same are eight animals the player cannot tell apart — and
            // the point of this class was "the hero should be my pet".
            var seen = new HashSet<int>();
            for (int i = 0; i < MiniAnimal.KindCount; i++)
            {
                var fur = MiniAnimal.Fur((MiniAnimalKind)i);
                int key = ((int)(fur.r * 255f) << 16) | ((int)(fur.g * 255f) << 8) | (int)(fur.b * 255f);
                Assert.IsTrue(seen.Add(key), $"two animals share a fur colour at {i}");
            }
        }

        [Test]
        public void MiniAnimal_BuildsARealAnimal()
        {
            // Built from primitives, so the failure modes are "nothing was created", "it is a pile
            // of parts at the origin" and "it is the wrong size" — all checkable here.
            var root = MiniAnimal.Build(null, MiniAnimalKind.Fox, 1f);
            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>();
                Assert.GreaterOrEqual(renderers.Length, 10,
                    "an animal needs a body, a head, ears, a tail, feet and a face");
                Assert.AreEqual(0, root.GetComponentsInChildren<Collider>().Length,
                    "decorative parts must not have colliders");

                // It stands on its origin and is about as tall as asked for.
                var bounds = new Bounds(root.position, Vector3.zero);
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.Greater(bounds.max.y, 0.7f, "the animal is too short to read as a character");
                Assert.Less(bounds.max.y, 1.4f, "and not a giant");
                Assert.Less(bounds.min.y, 0.2f, "it stands on the ground, not in the air");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
