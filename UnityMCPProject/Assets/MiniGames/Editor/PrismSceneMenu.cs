using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshMiniGames.EditorTools
{
    /// <summary>Builds the sequence-memory game's scene from code, like every other mini-game scene.</summary>
    public static class PrismSceneMenu
    {
        public const string ScenePath = "Assets/MiniGames/Scenes/Prism.unity";

        [MenuItem("Tools/DSH Mini/Build Prism Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Prism");
            root.AddComponent<PrismGame>();
            root.AddComponent<PrismHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            FlyBirdSceneMenu.RegisterInBuildSettings();
            Debug.Log($"[DshMini] Prism scene built at {ScenePath} and registered in Build Settings.");
        }
    }
}
