using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Harvest.Presentation.Editor
{
    public static class PresentationSmoke
    {
        [MenuItem("Harvest/Create workspace scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Harvest workspace").AddComponent<HarvestWorkspace>();
            EditorSceneManager.SaveScene(scene, "Assets/Harvest/Presentation/Workspace.unity");
            Debug.Log("HARVEST_PRESENTATION_SMOKE: scene created with workspace component; gameplay binding pending");
        }
    }
}
