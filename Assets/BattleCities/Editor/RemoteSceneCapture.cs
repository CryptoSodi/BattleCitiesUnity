using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    // Copies scene roots together so references between roots remain internal to the prefab.
    internal static class RemoteSceneCapture
    {
        internal static string Capture(string scenePath, string destination)
        {
            if (!scenePath.StartsWith("Assets/BattleCities/Scenes/", StringComparison.Ordinal) ||
                !destination.StartsWith("Assets/BattleCities/RemoteArtBuildStaging/", StringComparison.Ordinal) ||
                File.Exists(destination)) throw new BuildFailedException("Invalid remote scene capture path.");
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                var roots = scene.GetRootGameObjects().Where(root => root).ToArray();
                if (roots.Length == 0) throw new BuildFailedException("Remote scene has no roots: " + scenePath);
                var parent = new GameObject(Path.GetFileNameWithoutExtension(scenePath) + " Remote Scene");
                SceneManager.MoveGameObjectToScene(parent, scene);
                foreach (var root in roots) root.transform.SetParent(parent.transform, true);
                var prefab = PrefabUtility.SaveAsPrefabAsset(parent, destination, out bool success);
                if (!success || !prefab) throw new BuildFailedException("Could not capture remote scene: " + scenePath);
                return destination;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
