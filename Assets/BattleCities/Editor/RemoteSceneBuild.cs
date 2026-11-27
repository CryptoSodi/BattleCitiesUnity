using System;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    // Preserve the scene names/build indices, but keep authored visual roots in the OTA bundle.
    public sealed class RemoteSceneBuild : IProcessSceneWithReport
    {
        public int callbackOrder => 2000;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !RemoteGameArtBuild.Active) return;
            ReplaceScene(scene);
        }
        internal static void ReplaceScene(Scene scene)
        {
            if (!EditorBuildSettings.scenes.Any(item => item.enabled && item.path == scene.path))
                throw new BuildFailedException("Unexpected scene in OTA player: " + scene.path);
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            var splash = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleCities/UI/Art/main-menu/branding/battle-cities-splash-logo.png");
            if (scene.name == "Login" && (!theme || !splash || !theme.HeadingFont))
                throw new BuildFailedException("The local startup logo or font is missing.");
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            var loader = new GameObject("Remote Scene Loader");
            SceneManager.MoveGameObjectToScene(loader, scene);
            loader.AddComponent<RemoteSceneLoader>().Configure(scene.name,
                scene.name == "Login" ? splash : null, scene.name == "Login" ? theme.HeadingFont : null);
        }
    }
}
