using DshRunner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DshRunnerEditor
{
    /// <summary>Editor entry points for the runner demo.</summary>
    public static class RunnerSceneMenu
    {
        [MenuItem("Tools/DSH Runner/Build Scene")]
        public static void BuildScene()
        {
            var manager = RunnerSceneBuilder.Build(true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[DshRunner] Scene wired. GameManager on '{manager.gameObject.name}'. " +
                      "Press Play, then Enter to start the endless run or L for the level list.");
        }

        [MenuItem("Tools/DSH Runner/Validate Wiring")]
        public static void ValidateWiring()
        {
            var manager = Object.FindObjectOfType<GameManager>();
            if (manager == null)
            {
                Debug.LogError("[DshRunner] No GameManager in the open scene. Run Tools/DSH Runner/Build Scene.");
                return;
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine("[DshRunner] Wiring report");
            report.AppendLine($"  GameManager: {manager.gameObject.name}");
            report.AppendLine($"  Player: {(manager.Player != null ? manager.Player.name : "<missing>")}");
            report.AppendLine($"  Track: {(manager.Track != null ? manager.Track.name : "<missing>")}");
            report.AppendLine($"  PowerUps: {(manager.PowerUps != null ? manager.PowerUps.name : "<missing>")}");
            report.AppendLine($"  Camera rig: {(manager.CameraRig != null ? manager.CameraRig.name : "<missing>")}");
            report.AppendLine($"  Levels: {LevelLibrary.Count}");

            for (int i = 0; i < LevelLibrary.Count; i++)
            {
                var level = LevelLibrary.Get(i);
                report.AppendLine($"    [{i}] {level.Name} target={level.TargetDistance:F0} " +
                                  $"speed={level.StartSpeed:F0}->{level.MaxSpeed:F0} density={level.ObstacleDensity:F2}");
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/DSH Runner/Reset Saved Progress")]
        public static void ResetProgress()
        {
            ProgressStore.ResetAll();
            Debug.Log("[DshRunner] Saved progress cleared.");
        }
    }
}
