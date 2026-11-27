using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    // Report indirect texture references that can cause OTA artwork to remain in a player build.
    public static class RemoteArtDependencyAudit
    {
        public static void Run()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);
            var resources = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/BattleCities/", StringComparison.Ordinal)
                && path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path));
            var roots = scenes.Concat(resources).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var rows = new StringBuilder("root,texture\n");
            int count = 0;
            foreach (string root in roots)
            {
                foreach (string path in AssetDatabase.GetDependencies(root, true).Distinct().OrderBy(path => path, StringComparer.Ordinal))
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(Texture2D) || !path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    rows.Append('"').Append(root.Replace("\"", "\"\"")).Append("\",\"")
                        .Append(path.Replace("\"", "\"\"")).Append("\"\n");
                    count++;
                }
            }
            Directory.CreateDirectory("Builds/RemoteArtChecks");
            File.WriteAllText("Builds/RemoteArtChecks/texture-dependencies.csv", rows.ToString());
            Debug.Log($"REMOTE ART DEPENDENCY AUDIT: {count} texture references from {roots.Length} scene and Resources roots.");
        }
    }
}
