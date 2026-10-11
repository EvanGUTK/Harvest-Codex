using Harvest.Application;
using Harvest.Composition;
using Harvest.Domain;
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

        public static ApplicationSessionView Bind(HarvestWorkspace workspace)
        {
            var initial = FirstFarm.Create(SessionMode.Campaign);
            var controller = new SessionController(new Simulation(initial.World), new LanguageProgramFactory(),
                FirstFarm.SimulationId, FirstFarm.ContentId, FirstFarm.TractorId);
            controller.RestoreStopped(initial);
            var view = new ApplicationSessionView(controller, FirstFarm.Create);
            workspace.Bind(view);
            // Workspace is the sole rendered-frame pump; bootstrap never advances ticks.
            return view;
        }
    }
}
