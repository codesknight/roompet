using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshPet
{
    /// <summary>
    /// One activity the player can leave the room for. Adding a game means adding a row
    /// here plus a scene in Build Settings — no new code, which is the point: this is the
    /// hook the "virtual town" direction needs.
    /// </summary>
    [System.Serializable]
    public class MiniGameDefinition
    {
        public string Id = "";
        public string DisplayName = "";
        public string Blurb = "";

        /// <summary>Scene name as registered in Build Settings (used to load).</summary>
        public string SceneName = "";

        /// <summary>Asset path of the same scene. Validation uses this, because
        /// <c>Application.CanStreamedLevelBeLoaded</c> answers false in the Editor even for
        /// scenes that are correctly registered.</summary>
        public string ScenePath = "";

        /// <summary>Only offered for this species id. Empty means any pet.</summary>
        public string RequiresSpecies = "";

        /// <summary>Shown as a hint on the door panel.</summary>
        public string Icon = "▸";

        public bool AvailableFor(string speciesId)
            => string.IsNullOrEmpty(RequiresSpecies) ||
               string.Equals(RequiresSpecies, speciesId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The activity catalogue and the scene hand-off.
    ///
    /// The pet's state is flushed before leaving, because the room scene is unloaded: needs
    /// go to PlayerPrefs and the journal to its file, so the pet is exactly as you left it
    /// when you come back. Returning is driven by a PlayerPrefs flag that the other scene
    /// reads, which keeps the two gameplay assemblies from having to reference each other.
    /// </summary>
    public static class MiniGameLibrary
    {
        /// <summary>Set while the player is away in an activity. The other scene shows a
        /// "back to the room" affordance when this is 1.</summary>
        public const string AwayFlagKey = "dshpet.away";

        /// <summary>Scene that the door belongs to, used as the return destination.</summary>
        public const string RoomScene = "PetRoom";
        public const string RoomScenePath = "Assets/Pet/Scenes/PetRoom.unity";

        public static readonly MiniGameDefinition[] All =
        {
            new MiniGameDefinition
            {
                Id = "runner",
                DisplayName = "森林奔跑",
                Blurb = "在森林小径上跑一段，跳木头、躲石头、收集水果。",
                SceneName = "Main",
                ScenePath = "Assets/Scenes/Main.unity",
                Icon = "🏃"
            }
            // 新增玩法就在这里加一行：填一个已在 Build Settings 里的场景即可。
        };

        /// <summary>True when the scene is registered in Build Settings.</summary>
        public static bool IsRegistered(string scenePath)
            => !string.IsNullOrEmpty(scenePath) &&
               SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0;

        public static bool IsRegistered(MiniGameDefinition game)
            => game != null && IsRegistered(game.ScenePath);

        public static List<MiniGameDefinition> AvailableFor(string speciesId)
        {
            var list = new List<MiniGameDefinition>();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].AvailableFor(speciesId)) list.Add(All[i]);
            }
            return list;
        }

        public static MiniGameDefinition Get(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        public static bool IsAway => PlayerPrefs.GetInt(AwayFlagKey, 0) == 1;

        /// <summary>Flush the pet, then load the activity scene.</summary>
        public static bool Launch(MiniGameDefinition game, PetGameManager manager)
        {
            if (game == null || string.IsNullOrEmpty(game.SceneName)) return false;
            if (!IsRegistered(game))
            {
                Debug.LogWarning($"[DshPet] Scene '{game.ScenePath}' is not in Build Settings; " +
                                 "run Tools/DSH Pet/Add Scenes To Build Settings.");
                return false;
            }

            manager?.FlushToDisk();
            PlayerPrefs.SetInt(AwayFlagKey, 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene(game.SceneName);
            return true;
        }

        /// <summary>Leave the activity and come home.</summary>
        public static void ReturnToRoom()
        {
            PlayerPrefs.SetInt(AwayFlagKey, 0);
            PlayerPrefs.Save();

            if (IsRegistered(RoomScenePath))
            {
                SceneManager.LoadScene(RoomScene);
            }
            else
            {
                Debug.LogWarning($"[DshPet] Scene '{RoomScenePath}' is not in Build Settings.");
            }
        }
    }
}
