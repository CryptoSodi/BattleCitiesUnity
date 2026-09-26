using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class ArenaMenuVerification
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            var view = Object.FindFirstObjectByType<MainMenuScene>();
            if (!view) throw new System.InvalidOperationException("Main menu scene did not load.");
            MainMenuChecks.Validate();
            MainMenuChecks.CaptureAll();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ARENA_MENU_VERIFIED");
        }
    }
}
