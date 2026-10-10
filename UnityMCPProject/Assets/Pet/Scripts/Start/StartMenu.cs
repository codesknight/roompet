using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshPet
{
    /// <summary>
    /// The front door: 进入房间 / 玩法介绍 / 设置 / 离开房间, and the walk-in that follows.
    ///
    /// This is the scene the build starts on, which is a change worth naming: until now the app opened
    /// straight into the pet's room, and "打开游戏" meant "你已经站在房间里了". A front door is not only
    /// nicer, it is the only place a player can see what the game *is* — the room is a place you are
    /// already in, and there is nowhere in it that is about the whole thing.
    ///
    /// The controller owns the phase and the clock; the drawing is in <see cref="StartMenuHud"/> and the
    /// arithmetic of the opening is in <see cref="StartSequence"/>. When the sequence says the picture has
    /// finished changing, this loads the room.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        /// <summary>Where 进入房间 goes. The room's scene, by name.</summary>
        public const string RoomScene = "PetRoom";

        public StartPhase Phase { get; private set; } = StartPhase.Menu;

        /// <summary>Seconds since the door started opening.</summary>
        public float OpeningSeconds { get; private set; }

        /// <summary>The pet's species name, for the welcome line.</summary>
        public string PetName => DshMobile.MiniAnimal.Name(DshMobile.MiniAnimal.Current);

        /// <summary>The version, for the corner of the screen.</summary>
        public string Version => Application.version;

        /// <summary>Coins the player has, so the front door admits the game has a wallet.</summary>
        public int Coins => DshMobile.PetWallet.Coins;

        private StartRoom _room;
        private bool _loading;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            var room = FindObjectOfType<StartRoom>();
            _room = room != null ? room : new GameObject("StartRoom").AddComponent<StartRoom>();

            // The menu is the one scene with no gameplay in it, so it can afford to hold the screen on.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Update()
        {
            if (Phase == StartPhase.Opening) OpeningSeconds += Time.deltaTime;

            // Held every frame in every phase, not only while the door swings: this is also what re-solves
            // the shot when the screen's shape changes, and a phone rotated on the menu would otherwise
            // keep the framing it was given at Awake — for an aspect the editor guessed.
            bool moving = Phase == StartPhase.Opening || Phase == StartPhase.Leaving;
            _room.ApplyOpening(moving ? OpeningSeconds : 0f);

            if (!moving) return;

            if (Phase == StartPhase.Opening && StartSequence.FadeAlpha(OpeningSeconds) >= 0.999f)
            {
                Phase = StartPhase.Leaving;
            }

            if (!_loading && StartSequence.ReadyToEnter(OpeningSeconds))
            {
                _loading = true;
                EnterRoom();
            }
        }

        // ------------------------------------------------------------------ the four doors

        /// <summary>进入房间: open the door and walk in.</summary>
        public void Begin()
        {
            if (Phase != StartPhase.Menu && Phase != StartPhase.Settings && Phase != StartPhase.HowTo) return;

            Phase = StartPhase.Opening;
            OpeningSeconds = 0f;
            DshMobile.MobileHaptics.Medium();
        }

        public void OpenSettings()
        {
            if (Phase != StartPhase.Menu) return;
            Phase = StartPhase.Settings;
            DshMobile.MobileHaptics.Light();
        }

        public void OpenHowTo()
        {
            if (Phase != StartPhase.Menu) return;
            Phase = StartPhase.HowTo;
            DshMobile.MobileHaptics.Light();
        }

        /// <summary>离开房间 asks first: quitting by accident is not recoverable.</summary>
        public void AskToQuit()
        {
            if (Phase != StartPhase.Menu) return;
            Phase = StartPhase.Quit;
            DshMobile.MobileHaptics.Light();
        }

        public void Cancel()
        {
            if (Phase == StartPhase.Opening || Phase == StartPhase.Leaving) return;
            Phase = StartPhase.Menu;
            DshMobile.MobileHaptics.Light();
        }

        public void Quit()
        {
            DshMobile.MobileHaptics.Heavy();
            DshMobile.MobileHaptics.Cancel();

            // On a phone this closes the app; in the editor it does nothing, so the button also says so.
            Application.Quit();
        }

        private void EnterRoom()
        {
            DshMobile.SceneClock.Restore("entering the room from the front door");
            PlayerPrefs.SetInt("dshpet.away", 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene(RoomScene);
        }
    }
}
