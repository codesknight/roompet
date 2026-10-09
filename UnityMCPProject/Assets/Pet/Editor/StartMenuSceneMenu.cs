using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshPet.EditorTools
{
    /// <summary>
    /// Builds the front door scene from code, like every other scene here — a `.unity` file checked into
    /// git is a snapshot of a script, and a snapshot that nobody can regenerate is a snapshot that
    /// quietly rots (DEVLOG 坑 52).
    ///
    /// It is also placed **first** in the build settings: the whole point of a start screen is that the
    /// app opens on it, and the order of that list is the only thing that decides what "open" means.
    /// </summary>
    public static class StartMenuSceneMenu
    {
        public const string ScenePath = "Assets/Pet/Scenes/StartMenu.unity";

        [MenuItem("Tools/DSH Pet/Build Start Menu Scene")]
        public static void BuildStartMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("StartMenu");
            root.AddComponent<StartMenu>();
            root.AddComponent<StartMenuHud>();
            root.AddComponent<StartRoom>();

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.10f);
            cam.fieldOfView = 46f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            camera.AddComponent<AudioListener>();

            var light = new GameObject("Key Light");
            var key = light.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.94f, 0.84f);
            key.intensity = 0.75f;
            key.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            light.transform.position = new Vector3(0f, 4f, -3f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.29f, 0.34f);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            PutScenesInOrder();

            Debug.Log($"[DshPet] Start menu scene built at {ScenePath}, and put first in the build list.");
        }

        [MenuItem("Tools/DSH Pet/Put Start Menu First")]
        public static void PutScenesInOrder()
        {
            var wanted = new List<string>
            {
                ScenePath,
                "Assets/Pet/Scenes/PetRoom.unity",
                "Assets/Scenes/Main.unity",
                "Assets/MiniGames/Scenes/FlyBird.unity",
                "Assets/MiniGames/Scenes/JumpQuest.unity",
                "Assets/MiniGames/Scenes/CatchFruit.unity",
                "Assets/MiniGames/Scenes/SliceFruit.unity",
                "Assets/MiniGames/Scenes/AngryBirds.unity"
            };

            var scenes = new List<EditorBuildSettingsScene>();
            var seen = new HashSet<string>();

            for (int i = 0; i < wanted.Count; i++)
            {
                if (!System.IO.File.Exists(wanted[i])) continue;
                if (!seen.Add(wanted[i])) continue;
                scenes.Add(new EditorBuildSettingsScene(wanted[i], true));
            }

            // Anything else that was already enabled stays, at the end: this method's job is the order,
            // not the membership.
            var existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (!existing[i].enabled) continue;
                if (!seen.Add(existing[i].path)) continue;
                scenes.Add(existing[i]);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
