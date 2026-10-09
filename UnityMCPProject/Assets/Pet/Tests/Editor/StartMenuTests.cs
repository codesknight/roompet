using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DshPet.Tests
{
    /// <summary>
    /// The front door: the opening sequence's arithmetic, and the one piece of wiring that would fail
    /// silently — the scene name it loads.
    ///
    /// A start screen is mostly a picture, and a picture is normally checked by looking at it. The parts
    /// that can be *wrong* rather than merely ugly are the timing (a fade that finishes before the door
    /// is open, a door that never quite opens) and the scene it hands over to. Both are here.
    /// </summary>
    public class StartMenuTests
    {
        [Test]
        public void Door_OpensAllTheWayAndThenStaysOpen()
        {
            Assert.AreEqual(0f, StartSequence.PanelAngle(0f), 0.01f, "a closed door starts closed");
            Assert.AreEqual(StartSequence.DoorAngle, StartSequence.PanelAngle(StartSequence.DoorSeconds), 0.05f,
                "the door has to be fully open when the swing is over");
            Assert.AreEqual(StartSequence.DoorAngle, StartSequence.PanelAngle(99f), 0.05f,
                "and it must not swing back");

            float previous = -1f;
            for (float t = 0f; t <= StartSequence.DoorSeconds; t += 0.05f)
            {
                float angle = StartSequence.PanelAngle(t);
                Assert.GreaterOrEqual(angle, previous - 0.001f, "the door only opens");
                Assert.LessOrEqual(angle, StartSequence.DoorAngle + 0.001f, "and never overshoots");
                previous = angle;
            }

            Assert.Greater(StartSequence.DoorAngle, 90f, "a door that opens exactly 90° reads as a wall");
        }

        [Test]
        public void Light_ComesThroughTheDoorwayOnlyOnceItIsOpen()
        {
            // The light is what sells the door: it has to arrive with the gap, not before it.
            Assert.AreEqual(0f, StartSequence.DoorGlow(0f), 0.001f);
            Assert.AreEqual(1f, StartSequence.DoorGlow(StartSequence.DoorSeconds), 0.001f);

            for (float t = 0f; t <= StartSequence.DoorSeconds; t += 0.05f)
            {
                float glow = StartSequence.DoorGlow(t);
                float open = StartSequence.DoorProgress(t);

                Assert.GreaterOrEqual(glow, 0f);
                Assert.LessOrEqual(glow, 1f);
                Assert.LessOrEqual(glow, open + 0.001f,
                    "the doorway must not be brighter than the gap that is letting the light through");
            }
        }

        [Test]
        public void Fade_IsClearWhileTheDoorOpensAndWhiteBeforeTheRoomLoads()
        {
            Assert.AreEqual(0f, StartSequence.FadeAlpha(0f), 0.001f);
            Assert.AreEqual(0f, StartSequence.FadeAlpha(StartSequence.DoorSeconds), 0.001f,
                "the picture must not be fading while the door is still swinging");
            Assert.AreEqual(1f, StartSequence.FadeAlpha(StartSequence.TotalSeconds), 0.001f);
            Assert.AreEqual(1f, StartSequence.FadeAlpha(99f), 0.001f);

            for (float t = StartSequence.DoorSeconds; t <= StartSequence.TotalSeconds; t += 0.02f)
            {
                float alpha = StartSequence.FadeAlpha(t);
                Assert.GreaterOrEqual(alpha, 0f);
                Assert.LessOrEqual(alpha, 1f);
            }
        }

        [Test]
        public void RoomIsNotLoadedBeforeTheDoorIsOpen()
        {
            // Loading early is the failure that looks like a bug: the room appears before the animation
            // has finished, so the animation was pointless.
            Assert.IsFalse(StartSequence.ReadyToEnter(0f));
            Assert.IsFalse(StartSequence.ReadyToEnter(StartSequence.DoorSeconds * 0.9f),
                "the room cannot be entered before the door is open");
            Assert.IsTrue(StartSequence.ReadyToEnter(StartSequence.TotalSeconds),
                "and it has to be entered at the end");

            float firstReady = -1f;
            for (float t = 0f; t <= StartSequence.TotalSeconds + 0.5f; t += 0.01f)
            {
                if (!StartSequence.ReadyToEnter(t)) continue;
                firstReady = t;
                break;
            }

            Assert.Greater(firstReady, StartSequence.DoorSeconds,
                "the hand-over must come after the door has finished opening");
            Assert.Less(firstReady, StartSequence.TotalSeconds,
                "and slightly before the fade is over, so the fade is still finishing as the room appears");
        }

        [Test]
        public void TheSequenceIsShortEnoughToWatchAndLongEnoughToRead()
        {
            // "点进去要等半天" is a bug report about a start screen the same way "闪一下就没了" is.
            Assert.Greater(StartSequence.TotalSeconds, 1.2f, "a cut is not an opening");
            Assert.Less(StartSequence.TotalSeconds, 4f, "nobody waits four seconds to get into a game");
        }

        [Test]
        public void TheFrontDoorOpensOntoASceneThatExists()
        {
            // The one wire that would fail with no error at all: a renamed scene means LoadScene throws
            // at runtime, on the very first tap, on a phone, with no console to read.
            var scenes = EditorBuildSettings.scenes;
            bool found = false;
            int roomIndex = -1;
            int menuIndex = -1;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;
                if (scenes[i].path.EndsWith(StartMenu.RoomScene + ".unity") && roomIndex < 0) roomIndex = i;
                if (scenes[i].path.EndsWith("/StartMenu.unity") && menuIndex < 0) menuIndex = i;
                if (roomIndex >= 0) found = true;
            }

            Assert.IsTrue(found, "the front door points at 「" + StartMenu.RoomScene + "」, which is not in the build list");
            Assert.Greater(roomIndex, menuIndex,
                "the room should come after the front door in the build list");
            Assert.AreEqual(menuIndex + 1, roomIndex,
                "and right after it: the second scene is the one the door opens onto");
        }

        [Test]
        public void TheStartScreenIsTheFirstSceneInTheBuild()
        {
            // This is what "the app opens on the front door" means: nothing else decides it.
            var scenes = EditorBuildSettings.scenes;
            Assert.Greater(scenes.Length, 0, "the build has no scenes in it");
            Assert.IsTrue(scenes[0].enabled, "the first scene must be enabled");
            Assert.IsTrue(scenes[0].path.EndsWith("/StartMenu.unity"),
                "the first scene is " + scenes[0].path + ", not the start menu");
        }

        [Test]
        public void TheOpeningIsVisibleOnAPhoneShapedScreen()
        {
            // The camera's two positions are the whole animation's picture, and they are solved from the
            // screen's shape: on a portrait phone the doorway is *wider* than the view, and the first
            // version's fixed four metres filled the screen with joinery and no room for anything else.
            float[] aspects = { 0.45f, 0.5625f, 1f, 1.7778f, 2.4f };

            for (int i = 0; i < aspects.Length; i++)
            {
                Vector3 from, to, look;
                StartRoom.FrameDoorway(aspects[i], 60f, out from, out to, out look);

                float half = 60f * 0.5f * Mathf.Deg2Rad;
                float visibleWidth = 2f * Vector3.Distance(from, look) * Mathf.Tan(half) * aspects[i];
                float visibleHeight = 2f * Vector3.Distance(from, look) * Mathf.Tan(half);

                Assert.GreaterOrEqual(visibleWidth, 3.2f,
                    $"at aspect {aspects[i]:F2} the doorway would not fit across the screen");
                Assert.GreaterOrEqual(visibleHeight, 2.6f,
                    $"at aspect {aspects[i]:F2} the doorway would not fit top to bottom");

                Assert.Greater((to - from).magnitude, 1.2f,
                    "the camera barely moves, so the 'walk in' will not read");
                Assert.Greater(to.z, from.z, "the dolly must go towards the wall");
                Assert.Less(to.z, 0.1f, "and stop short of walking through the doorway");
                Assert.Greater(from.z, -12f, "and not start in the next county");
            }

            // A narrower screen needs a longer lens position: that is the whole point of solving it.
            Vector3 wideFrom, wideTo, wideLook;
            Vector3 tallFrom, tallTo, tallLook;
            StartRoom.FrameDoorway(2f, 60f, out wideFrom, out wideTo, out wideLook);
            StartRoom.FrameDoorway(0.45f, 60f, out tallFrom, out tallTo, out tallLook);

            Assert.Greater(tallFrom.z * -1f, wideFrom.z * -1f,
                "a portrait screen has to stand further back than a landscape one");
        }

        [Test]
        public void SettingsSwitch_TappingTurnsItOffAndBackOn()
        {
            // The reported bug: music, once switched off, could not be switched back on. The pill
            // returned the *tap* (true on the click frame) as the new state, so the caller read
            // "tapped" instead of "the value after the tap" — and a switch whose new state is
            // always "tapped" can only ever land on off. The decision is now a pure function.
            Assert.IsFalse(StartMenuHud.Toggled(true, true),
                "tapping an ON switch must turn it off");
            Assert.IsTrue(StartMenuHud.Toggled(false, true),
                "tapping an OFF switch must turn it on — this is what the report said never happened");
            Assert.IsTrue(StartMenuHud.Toggled(true, false),
                "no tap means the switch stays exactly as it was (on)");
            Assert.IsFalse(StartMenuHud.Toggled(false, false),
                "no tap means the switch stays exactly as it was (off)");
        }
    }
}
