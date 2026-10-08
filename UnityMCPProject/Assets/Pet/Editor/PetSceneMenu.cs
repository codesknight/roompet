using DshPet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshPetEditor
{
    /// <summary>Authoring entry points for the virtual pet scene.</summary>
    public static class PetSceneMenu
    {
        private const string RootName = "PetRoot";

        [MenuItem("Tools/DSH Pet/Build Pet Scene")]
        public static void BuildScene()
        {
            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);

            var manager = root.GetComponent<PetGameManager>();
            if (manager == null) manager = root.AddComponent<PetGameManager>();
            if (root.GetComponent<PetHud>() == null) root.AddComponent<PetHud>();

            BuildCamera();
            BuildLighting();

            manager.EditorBuild();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[DshPet] Scene built. Press Play, then type to your pet. " +
                      "Tools/DSH Pet/Validate reports the wiring.");
        }

        private static void BuildCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            camera.transform.position = new Vector3(0f, 2.55f, -6.4f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0.85f, 0.9f) - camera.transform.position, Vector3.up);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;

            // An interior reads better against a flat colour than against the skybox.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.11f, 0.15f);
        }

        private static void BuildLighting()
        {
            var light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
            }
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.05f;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.40f, 0.46f);
            RenderSettings.fog = false;
        }

        [MenuItem("Tools/DSH Pet/Validate Wiring")]
        public static void ValidateWiring()
        {
            var manager = Object.FindObjectOfType<PetGameManager>();
            if (manager == null)
            {
                Debug.LogError("[DshPet] No PetGameManager in the open scene. Run Tools/DSH Pet/Build Pet Scene.");
                return;
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine("[DshPet] Wiring report");
            report.AppendLine($"  manager on: {manager.gameObject.name}");
            report.AppendLine($"  room: {(manager.Room != null ? manager.Room.name : "<missing>")}");
            report.AppendLine($"  avatar: {(manager.Avatar != null ? manager.Avatar.name : "<missing>")}");
            report.AppendLine($"  controller: {(manager.Controller != null ? manager.Controller.name : "<missing>")}");
            report.AppendLine($"  player: {(manager.Player != null ? manager.Player.name : "<missing>")}");
            report.AppendLine($"  camera rig: {(manager.CameraRig != null ? manager.CameraRig.name : "<missing>")}");
            report.AppendLine($"  hud: {(manager.GetComponent<PetHud>() != null ? "present" : "<missing>")}");
            report.AppendLine($"  mini games: {MiniGameLibrary.All.Length}");
            foreach (var game in MiniGameLibrary.All)
            {
                bool inBuild = MiniGameLibrary.IsRegistered(game);
                report.AppendLine($"    - {game.Id,-8} {game.DisplayName}  scene={game.SceneName}  registered={inBuild}");
            }
            report.AppendLine($"  PetRoom registered: {MiniGameLibrary.IsRegistered(MiniGameLibrary.RoomScenePath)}");
            report.AppendLine($"  species: {PetSpecies.Count}");
            foreach (var species in PetSpecies.All)
            {
                report.AppendLine($"    - {species.Id,-8} {species.DisplayName}  ears={species.Ears} tail={species.Tail}");
            }

            if (manager.Room != null)
            {
                report.AppendLine($"  interactables: {manager.Room.Interactables.Count}");
                foreach (var item in manager.Room.Interactables)
                {
                    report.AppendLine($"    - {item.Kind,-6} {item.Label} @ {item.ApproachPoint}");
                }

                // The ball is the one interactable with extra parts: a clickable collider and
                // the PetBall state machine that the throw/fetch loop runs on.
                var ball = manager.Room.Ball;
                if (ball == null)
                {
                    report.AppendLine("  ball: <missing>  (run Build Pet Scene — the throw loop needs it)");
                }
                else
                {
                    report.AppendLine($"  ball: {ball.name}  collider={ball.GetComponent<Collider>() != null}  " +
                                      $"state={ball.State}  radius={ball.Radius}");
                }
            }

            var fetch = PetBehaviorLibrary.Get("fetch");
            report.AppendLine($"  fetch behaviour: {(fetch != null && fetch.Fetch ? "present" : "<missing>")}");

            // HUD geometry. Text input and click routing cannot be checked from a screenshot,
            // and this is where the "panel overlaps the button" and "modal swallows the click"
            // class of bug shows up as a number instead.
            var hud = Object.FindObjectOfType<PetHud>();
            if (hud != null)
            {
                report.AppendLine("  hud layout: " + hud.DescribeLayout());
                report.AppendLine($"  hud modal: {PetHud.ModalOpen}");
            }
            else
            {
                report.AppendLine("  hud layout: <no PetHud in the open scene>");
            }

            var config = PetBrainConfig.Load();
            report.AppendLine($"  brain: {config.Describe()}");

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/DSH Pet/Fix Script Encodings")]
        public static void FixScriptEncodings()
        {
            // Unity's compiler reads a .cs file according to its byte-order mark. On a
            // Chinese-locale Windows a file with Chinese literals but NO BOM is decoded as GBK,
            // so every string in it silently becomes mojibake ("全景" showed up as "锟斤拷" in
            // the HUD). Unity writes its own scripts with a BOM; anything edited by an outside
            // tool can lose it, so this repairs the whole project in one click.
            var utf8WithBom = new System.Text.UTF8Encoding(true);
            int fixedCount = 0;
            var report = new System.Text.StringBuilder();
            var corrupted = new System.Text.StringBuilder();
            int corruptedCount = 0;

            string root = Application.dataPath;
            foreach (var path in System.IO.Directory.GetFiles(root, "*.cs",
                         System.IO.SearchOption.AllDirectories))
            {
                byte[] bytes;
                try { bytes = System.IO.File.ReadAllBytes(path); }
                catch { continue; }

                bool hasBom = bytes.Length >= 3 &&
                              bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                string text = System.Text.Encoding.UTF8.GetString(bytes);
                bool hasNonAscii = false;
                for (int i = 0; i < text.Length && !hasNonAscii; i++)
                {
                    if (text[i] > 127) hasNonAscii = true;
                }

                // U+FFFD means a multi-byte character was ALREADY destroyed by something that
                // read the file with the wrong encoding and wrote it back (a shell redirect or
                // a text-mode round trip will do it). A BOM cannot undo that, so it is reported
                // for a human to retype instead of being silently accepted.
                int at = text.IndexOf('\uFFFD');
                if (at >= 0)
                {
                    int line = 1;
                    for (int i = 0; i < at; i++) if (text[i] == '\n') line++;
                    corruptedCount++;
                    corrupted.AppendLine($"  {path.Replace(root, "Assets")}:{line}");
                }

                if (!hasNonAscii || hasBom) continue;

                System.IO.File.WriteAllText(path, text, utf8WithBom);
                fixedCount++;
                report.AppendLine("  " + path.Replace(root, "Assets"));
            }

            if (fixedCount == 0)
            {
                Debug.Log("[DshPet] Script encodings are fine: every file with non-ASCII text already " +
                          "has a UTF-8 BOM.");
            }
            else
            {
                Debug.Log($"[DshPet] Added a UTF-8 BOM to {fixedCount} script(s). Unity will recompile; " +
                          "Chinese text in those files would otherwise render as mojibake.\n" + report);
            }

            if (corruptedCount > 0)
            {
                Debug.LogError($"[DshPet] {corruptedCount} script(s) contain U+FFFD — characters destroyed " +
                               "by a lossy read/write round trip. A BOM does NOT fix this; the affected " +
                               "literals must be retyped.\n" + corrupted);
            }
        }

        [MenuItem("Tools/DSH Pet/Add Scenes To Build Settings")]
        public static void AddScenesToBuildSettings()
        {
            // SceneManager.LoadScene only works for scenes registered here, so the door's
            // trip to the runner and the trip home both depend on this.
            string[] wanted = { "Assets/Pet/Scenes/PetRoom.unity", "Assets/Scenes/Main.unity" };
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var path in wanted)
            {
                if (System.IO.File.Exists(path) &&
                    !scenes.Exists(s => s.path == path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                    Debug.Log("[DshPet] Added to build settings: " + path);
                }
                else if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[DshPet] Scene not found, skipped: " + path);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[DshPet] Build settings now hold {EditorBuildSettings.scenes.Length} scene(s).");
        }

        [MenuItem("Tools/DSH Pet/Clear Pet Save")]
        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey("dshpet.brain.config");
            foreach (var species in PetSpecies.All)
            {
                PlayerPrefs.DeleteKey("dshpet.memory.recent." + species.Id);
                PlayerPrefs.DeleteKey("dshpet.memory.facts." + species.Id);
                PlayerPrefs.DeleteKey("dshpet.needs." + species.Id);
                PlayerPrefs.DeleteKey("dshpet.petname." + species.Id);
            }
            PlayerPrefs.DeleteKey("dshpet.species");
            PlayerPrefs.Save();

            // The notebook lives in its own JSON file per species, not in PlayerPrefs, so
            // clearing the save has to reach it too — otherwise a reset pet still opens a
            // calendar full of the old one's days.
            int removed = 0;
            string folder = Application.persistentDataPath;
            if (System.IO.Directory.Exists(folder))
            {
                foreach (var file in System.IO.Directory.GetFiles(folder, "dshpet-journal-*.json"))
                {
                    System.IO.File.Delete(file);
                    removed++;
                }
            }

            Debug.Log($"[DshPet] Pet save cleared (PlayerPrefs + {removed} notebook file(s)).");
        }
    }
}
