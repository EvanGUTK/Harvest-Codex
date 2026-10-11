using System.IO;
using Harvest.Application;
using Harvest.Composition;
using Harvest.Domain;
using Harvest.Persistence;
using Harvest.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Harvest.Bootstrap
{
    public static class FarmBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartScene()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var workspace = root.GetComponentInChildren<HarvestWorkspace>(true);
                if (workspace != null) { Bind(workspace); return; }
            }
        }

        public static ApplicationSessionView Bind(HarvestWorkspace workspace, string storageRoot = null)
        {
            var initial = FirstFarm.Create(SessionMode.Campaign);
            var controller = new SessionController(new Simulation(initial.World), new LanguageProgramFactory(),
                FirstFarm.SimulationId, FirstFarm.ContentId, FirstFarm.TractorId);
            controller.RestoreStopped(initial);
            var codec = new StoppedSessionCodec(SimulationRules.Default, FirstFarm.SimulationId, FirstFarm.ContentId);
            string root = storageRoot ?? Path.Combine(UnityEngine.Application.persistentDataPath, "saves");
            var storage = new SessionStorage(controller, root, codec);
            var view = new ApplicationSessionView(controller, FirstFarm.Create, storage);
            workspace.Bind(view);
            // Workspace is the sole rendered-frame pump; bootstrap never advances ticks.
            return view;
        }
    }
}
