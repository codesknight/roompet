using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshMiniGames.EditorTools
{
    /// <summary>Builds the tunnel runner's scene from code.</summary>
    public static class TunnelSceneMenu
    {
        public const string ScenePath = "Assets/MiniGames/Scenes/Tunnel.unity";

        [MenuItem("Tools/DSH Mini/Build Tunnel Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Tunnel");
            root.AddComponent<TunnelGame>();
            root.AddComponent<TunnelHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            FlyBirdSceneMenu.RegisterInBuildSettings();
            Debug.Log($"[DshMini] Tunnel scene built at {ScenePath} and registered in Build Settings.");
        }
    }
}
