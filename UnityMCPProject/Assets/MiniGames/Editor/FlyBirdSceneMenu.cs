using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshMiniGames.EditorTools
{
    /// <summary>
    /// Builds the flappy game's scene from code.
    ///
    /// The same choice as the other two scenes in this project: the scene file holds two objects
    /// — a game object with the scripts and a camera — and everything the player sees is made at
    /// runtime. That keeps the scene diffable and stops it from drifting away from the scripts,
    /// which is what a hand-built hierarchy of pipes would do.
    /// </summary>
    public static class FlyBirdSceneMenu
    {
        public const string ScenePath = "Assets/MiniGames/Scenes/FlyBird.unity";

        [MenuItem("Tools/DSH Mini/Build FlyBird Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("FlyBird");
            root.AddComponent<FlyBirdGame>();
            root.AddComponent<FlyBirdHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
            Debug.Log($"[DshMini] FlyBird scene built at {ScenePath} and registered in Build Settings.");
        }

        [MenuItem("Tools/DSH Mini/Add Mini Game Scenes To Build Settings")]
        public static void RegisterInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            if (!HasScene(scenes, ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            // The room and the runner are registered by their own menus; keeping the pet room here
            // as well means a fresh clone can be made playable by running the menus in any order.
            AddIfPresent(scenes, "Assets/Pet/Scenes/PetRoom.unity");
            AddIfPresent(scenes, "Assets/Scenes/Main.unity");

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[DshMini] Build settings now hold {EditorBuildSettings.scenes.Length} scene(s).");
        }

        private static bool HasScene(System.Collections.Generic.List<EditorBuildSettingsScene> scenes,
            string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path) return true;
            }
            return false;
        }

        private static void AddIfPresent(System.Collections.Generic.List<EditorBuildSettingsScene> scenes,
            string path)
        {
            if (System.IO.File.Exists(path) && !HasScene(scenes, path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        [MenuItem("Tools/DSH Mini/Report Mini Games")]
        public static void Report()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("[DshMini] Mini game report");

            string[] scenes = { ScenePath, "Assets/Pet/Scenes/PetRoom.unity", "Assets/Scenes/Main.unity" };
            foreach (string path in scenes)
            {
                bool registered = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(path) >= 0;
                report.AppendLine($"  {path}  exists={System.IO.File.Exists(path)}  registered={registered}");
            }

            Debug.Log(report.ToString());
        }
    }
}
