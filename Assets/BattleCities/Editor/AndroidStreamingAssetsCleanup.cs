using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace BattleCities.Editor
{
    // These files are loaded only by the WebGL wallet and phone-controller bridges.
    // Remove them from the generated Android project, never from StreamingAssets.
    public sealed class AndroidStreamingAssetsCleanup : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string assets = Path.Combine(unityLibraryPath, "src", "main", "assets");
            string[] webOnlyFiles =
            {
                "BattleCitiesCheckout/phantom-core.mjs",
                "BattleCitiesCheckout/phantom-entry.js",
                "BattleCitiesCheckout/phantom.js",
                "BattleCitiesCheckout/wallet-entry.js",
                "BattleCitiesCheckout/wallet.js",
                "BattleCitiesPhone/BarlowCondensed-Bold.ttf",
                "BattleCitiesPhone/index.html"
            };

            int removed = 0;
            foreach (string relativePath in webOnlyFiles)
            {
                string path = Path.Combine(assets, relativePath);
                if (!File.Exists(path)) continue;
                File.Delete(path);
                removed++;
            }
            Debug.Log($"Excluded {removed} WebGL-only files from Android assets.");
        }
    }
}
