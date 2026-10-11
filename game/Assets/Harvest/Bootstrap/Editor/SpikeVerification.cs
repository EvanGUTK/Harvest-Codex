using System;
using System.IO;
using Harvest.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Harvest.Bootstrap.Editor
{
    public static class SpikeVerification
    {
        public const string ScenePath = "Assets/Harvest/Presentation/Workspace.unity";
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            HarvestWorkspace workspace = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                workspace = root.GetComponentInChildren<HarvestWorkspace>(true);
                if (workspace != null) break;
            }
            Require(workspace != null, "Entry scene lacks workspace");
            string storageRoot = Path.Combine(Path.GetTempPath(), "harvest-unity-check-" + Guid.NewGuid().ToString("N"));
            try
            {
                var view = FarmBootstrap.Bind(workspace, storageRoot);
                Require(view.Read().IsBound && view.Read().Tractor != null, "Actual session binding required");
                view.Run();
                view.Pump(.1);
                view.Pause();
                long paused = view.Read().Tick;
                view.Pump(10);
                Require(view.Read().Tick == paused, "Pause must freeze Unity adapter");
                view.Step();
                Require(view.Read().Tick == paused + 1, "Step must advance one tick");
                view.Run();
                for (int i = 0; i < 20; i++) view.Pump(.1);
                view.Stop();
                var delivered = view.Read();
                Require(delivered.Diagnostics.Length == 0 && delivered.Tractor.X == 0 && delivered.Tractor.Y == 0,
                    "Actual interpreter must drive tractor back to depot without fault");
                Require(delivered.Objective.Contains("Counted loops"), "Real wheat delivery must progress campaign");
                view.SelectMode(FarmMode.Sandbox);
                Require(view.Read().Tick == 0 && view.Read().Mode == FarmMode.Sandbox, "Sandbox starts independently");
                view.SelectMode(FarmMode.Campaign);
                Require(view.Read().Tick == delivered.Tick && view.Read().Objective == delivered.Objective,
                    "Campaign world/progress survives mode switch");
                string savedDraft = view.Draft;
                view.Save();
                Require(view.ReadStorage().Code == "none", "Real stopped session must save to disk");
                view.Draft = "turn_right()";
                view.RequestLoad();
                Require(view.ReadStorage().CanConfirm && view.Draft == "turn_right()",
                    "Load confirmation must not replace source before confirmation");
                view.CancelReplacement();
                Require(view.Draft == "turn_right()", "Cancel must retain draft");
                view.RequestLoad();
                view.ConfirmReplacement();
                Require(view.ReadStorage().Code == "none" && view.Draft == savedDraft,
                    "Confirmed real disk load must restore source");
                Require(view.ReadReference().Count > 0, "Current policy must expose programming reference");
                Debug.Log("HARVEST_UNITY_INTEGRATION_OK: real tractor, controls, mode isolation, storage confirmation and reference");
            }
            finally { if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true); }
        }

        public static void BuildMac()
        {
            Verify();
            string destination = Environment.GetEnvironmentVariable("HARVEST_BUILD_PATH") ?? "/tmp/Harvest-Codex.app";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = destination,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
            });
            Require(report.summary.result == BuildResult.Succeeded, "macOS player build failed");
            Debug.Log("HARVEST_MAC_BUILD_OK:" + destination);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
