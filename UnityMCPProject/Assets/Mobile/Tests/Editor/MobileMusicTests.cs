using NUnit.Framework;

namespace DshMobile.Tests
{
    /// <summary>
    /// The decisions behind the background music that do not need a speaker.
    ///
    /// The audible crossfade can only be judged by ear, but everything around it is a rule and a
    /// table: which scene gets which track, which place a pet's room is in, and the on/off and
    /// volume settings. Those are the parts that break silently — a game that plays the garden's
    /// music in the cabin, or a switch that forgets it was turned off — so they are the parts
    /// with tests.
    /// </summary>
    public class MobileMusicTests
    {
        [TearDown]
        public void TearDown() => MobileMusic.ResetForTests();

        [Test]
        public void SceneNamesMapToDistinctTracks()
        {
            Assert.AreEqual(MusicId.Menu, MobileMusic.TrackForScene("StartMenu"));
            Assert.AreEqual(MusicId.PetRoom, MobileMusic.TrackForScene("PetRoom"));
            Assert.AreEqual(MusicId.RunnerForest, MobileMusic.TrackForScene("Main"));
            Assert.AreEqual(MusicId.FlyBird, MobileMusic.TrackForScene("FlyBird"));
            Assert.AreEqual(MusicId.JumpQuest, MobileMusic.TrackForScene("JumpQuest"));
            Assert.AreEqual(MusicId.CatchFruit, MobileMusic.TrackForScene("CatchFruit"));
            Assert.AreEqual(MusicId.SliceFruit, MobileMusic.TrackForScene("SliceFruit"));
            Assert.AreEqual(MusicId.AngryBirds, MobileMusic.TrackForScene("AngryBirds"));
            Assert.AreEqual(MusicId.None, MobileMusic.TrackForScene("SomeOtherScene"));
        }

        [Test]
        public void PlacesMapToTheirOwnTracksAndTheCabinIsTheDefault()
        {
            // The real caller passes RoomTheme (an enum) boxed as object, and TrackForTheme reads
            // its ToString(); plain strings here exercise the same path without the enum type.
            Assert.AreEqual(MusicId.Garden, MobileMusic.TrackForTheme("Garden"));
            Assert.AreEqual(MusicId.Terrace, MobileMusic.TrackForTheme("Terrace"));
            Assert.AreEqual(MusicId.PetRoom, MobileMusic.TrackForTheme("Cabin"), "the cabin keeps the room's music");
            Assert.AreEqual(MusicId.PetRoom, MobileMusic.TrackForTheme("AnythingElse"), "unknown places fall back to the room");
            Assert.AreEqual(MusicId.PetRoom, MobileMusic.TrackForTheme(null), "a null theme must not throw");
        }

        [Test]
        public void EveryTrackHasAResourceName()
        {
            foreach (var track in MobileMusic.Tracks)
            {
                Assert.IsFalse(string.IsNullOrEmpty(track.Resource), track.Id + " has no resource path");
                Assert.IsTrue(track.Resource.StartsWith("Music/"), track.Id + " must live under Resources/Music");
            }
        }

        [Test]
        public void SwitchAndVolumePersistThroughPlayerPrefs()
        {
            MobileMusic.Enabled = false;
            Assert.IsFalse(MobileMusic.Enabled);

            MobileMusic.ResetForTests();
            Assert.IsTrue(MobileMusic.Enabled, "music is on by default");

            MobileMusic.Volume = 0.62f;
            MobileMusic.ResetForTests();
            MobileMusic.Volume = 0.62f;
            Assert.AreEqual(0.62f, MobileMusic.Volume, 0.0001f);
        }

        [Test]
        public void VolumeIsClampedAndTheSwitchIsRemembered()
        {
            MobileMusic.Volume = 5f;
            Assert.AreEqual(1f, MobileMusic.Volume, 0.0001f);
            MobileMusic.Volume = -2f;
            Assert.AreEqual(0f, MobileMusic.Volume, 0.0001f);

            MobileMusic.Enabled = false;
            MobileMusic.ResetForTests();
            Assert.IsTrue(MobileMusic.Enabled, "reset clears the setting, which is what tests need");
        }
    }
}
