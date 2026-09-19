using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace BattleCities.Editor
{
    [InitializeOnLoad]
    public static class ShowcaseCaptureLauncher
    {
        const string Trigger = @"C:\repos\Battle Cities Game\video\unity-capture.trigger";
        static ShowcaseCaptureLauncher()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BATTLE_CITIES_CAPTURE_DIR")) && !System.IO.File.Exists(Trigger)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Begin;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !System.IO.File.Exists(Trigger)) return;
            if (UnityEngine.Object.FindFirstObjectByType<ShowcaseCapture>() == null)
                new UnityEngine.GameObject("Battle Cities showcase capture").AddComponent<ShowcaseCapture>();
        }

        static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/BattleCity.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
