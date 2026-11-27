using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BattleCities.Editor
{
    // Temporary asset moves keep ordinary Android, Web and Editor workflows unchanged.
    public sealed class RemoteGameArtBuild : IDisposable
    {
        const string Staging = "Assets/BattleCities/RemoteArtBuildStaging";
        const string Config = "Assets/BattleCities/Resources/RemoteGameArtManifest.json";
        const string Journal = "Builds/RemoteArtRecovery.json";
        static readonly string[] Names = { "SettingsArt", "PlayerProfileArt", "OperationsArt", "LoadoutArt", "ShopArt", "ExtendedPowerupArt", "PreBattleArt", "BattleResultsArt", "EnvironmentPalette" };
        [Serializable] sealed class Move { public string source, destination; }
        [Serializable] sealed class Moves { public Move[] entries; public string[] sceneTemplates; }
        public static bool Active { get; private set; }
        public static System.Collections.Generic.HashSet<string> RemoteTexturePaths { get; private set; }
        const string Models = Staging + "/RemoteBattleModels.asset";
        bool disposed;
        public static string ManifestPath { get; private set; }

        internal static string[] ImageResourceRoots() => AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/BattleCities/", StringComparison.Ordinal)
                && path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path))
            .Where(path => AssetDatabase.GetDependencies(path, true).Any(dependency =>
                dependency.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.GetMainAssetTypeAtPath(dependency) == typeof(Texture2D)))
            .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();

        public static IDisposable Prepare(string baseUrl, string outputDirectory)
        {
            ManifestPath = null;
            if (File.Exists(Journal) || AssetDatabase.IsValidFolder(Staging) || File.Exists(Config))
                throw new BuildFailedException("Recover the previous artwork staging operation before building again: Battle Cities/Content/Recover interrupted build.");
            if (string.IsNullOrEmpty(baseUrl)) return null;
            if (!Application.isBatchMode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Remote artwork builds require the batch Android build path.");
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new BuildFailedException("Remote artwork needs an HTTPS base URL without credentials, query or fragment.");

            var existingRoots = Names.Select(name => {
                var candidates = AssetDatabase.FindAssets("t:ScriptableObject " + name)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith("/" + name + ".asset", StringComparison.Ordinal) && p.Contains("/Resources/")).ToArray();
                if (candidates.Length != 1) throw new BuildFailedException("Expected one Resources asset: " + name);
                return candidates[0];
            }).Concat(new[] { "Assets/BattleCities/Resources/TvReplayRig.prefab", "Assets/BattleCities/Resources/Deployables/LandAttackDrone.prefab", "Assets/BattleCities/Resources/PowerupCube.prefab",
                    "Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsRoot.prefab",
                    "Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset" }
                .Concat(Directory.GetFiles("Assets/BattleCities/Resources/RuntimeShaders/Terrain", "*.mat"))).Select(path => path.Replace('\\', '/'));
            // Any Resources root that references a project texture would otherwise pull
            // that texture back into the APK, even when its catalog is already remote.
            var imageRoots = ImageResourceRoots();
            var sources = existingRoots.Concat(imageRoots).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var collisions = sources.GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).ToArray();
            if (collisions.Length != 0) throw new BuildFailedException("Remote Resources have duplicate filenames: " + string.Join(", ", collisions.Select(group => group.Key)));
            var moves = sources.Select(path => new Move { source = path, destination = Staging + "/" + Path.GetFileName(path) }).ToArray();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            var templates = scenes.Select(path => Staging + "/RemoteScene_" + Path.GetFileNameWithoutExtension(path) + ".prefab").ToArray();
            if (templates.Distinct(StringComparer.OrdinalIgnoreCase).Count() != templates.Length)
                throw new BuildFailedException("Remote scene names must be unique.");
            Directory.CreateDirectory("Builds");
            File.WriteAllText(Journal, JsonUtility.ToJson(new Moves { entries = moves, sceneTemplates = templates }, true));
            var scope = new RemoteGameArtBuild();
            try
            {
                AssetDatabase.CreateFolder("Assets/BattleCities", "RemoteArtBuildStaging");
                RemoteModelBuild.Capture(Models);
                for (int i = 0; i < scenes.Length; i++) RemoteSceneCapture.Capture(scenes[i], templates[i]);
                foreach (var move in moves)
                {
                    var error = AssetDatabase.MoveAsset(move.source, move.destination);
                    if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
                }
                AssetDatabase.SaveAssets();
                RemoteTexturePaths = new System.Collections.Generic.HashSet<string>(
                    AssetDatabase.GetDependencies(moves.Select(move => move.destination).Concat(new[] { Models }).Concat(templates).ToArray(), true)
                        .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D)), StringComparer.Ordinal);
                Directory.CreateDirectory(outputDirectory);
                var manifest = BuildPipeline.BuildAssetBundles(outputDirectory, new[] {
                    new AssetBundleBuild { assetBundleName = "game-art", assetNames = moves.Select(m => m.destination).Concat(new[] { Models }).Concat(templates).ToArray() }
                }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.Android);
                if (!manifest) throw new BuildFailedException("Remote artwork bundle failed to build.");
                ManifestPath = Path.Combine(outputDirectory, new DirectoryInfo(outputDirectory).Name + ".manifest");
                var source = Path.Combine(outputDirectory, "game-art");
                string hash;
                using (var stream = File.OpenRead(source))
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                var name = hash + ".bundle";
                File.Copy(source, Path.Combine(outputDirectory, name), true);
                var config = new RemoteGameArt.Manifest { url = baseUrl.TrimEnd('/') + "/" + name, sha256 = hash,
                    bytes = new FileInfo(source).Length, unityVersion = Application.unityVersion, sceneTemplates = true };
                var json = JsonUtility.ToJson(config, true);
                File.WriteAllText(Path.Combine(outputDirectory, "content-manifest.json"), json);
                File.WriteAllText(Config, json);
                AssetDatabase.ImportAsset(Config, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("REMOTE ART READY: " + json);
                Active = true;
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        [MenuItem("Battle Cities/Content/Recover interrupted build")]
        public static void Recover()
        {
            if (!File.Exists(Journal)) return;
            var moves = JsonUtility.FromJson<Moves>(File.ReadAllText(Journal));
            foreach (var move in moves.entries)
            {
                bool allowedSource = move.source.StartsWith("Assets/BattleCities/", StringComparison.Ordinal) ||
                    move.source.StartsWith("Assets/Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/", StringComparison.Ordinal);
                if (!move.destination.StartsWith(Staging + "/", StringComparison.Ordinal) || !allowedSource || !move.source.Contains("/Resources/"))
                    throw new BuildFailedException("Invalid artwork recovery journal.");
                if (!File.Exists(move.destination)) continue;
                if (File.Exists(move.source)) throw new BuildFailedException("Both artwork paths exist; preserve both and recover manually: " + move.source);
                var error = AssetDatabase.MoveAsset(move.destination, move.source);
                if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
            }
            if (File.Exists(Models)) AssetDatabase.DeleteAsset(Models);
            foreach (var template in moves.sceneTemplates ?? Array.Empty<string>())
            {
                if (!template.StartsWith(Staging + "/RemoteScene_", StringComparison.Ordinal) || !template.EndsWith(".prefab", StringComparison.Ordinal))
                    throw new BuildFailedException("Invalid remote scene recovery path.");
                if (File.Exists(template)) AssetDatabase.DeleteAsset(template);
            }
            if (File.Exists(Config)) AssetDatabase.DeleteAsset(Config);
            if (AssetDatabase.IsValidFolder(Staging))
            {
                if (Directory.EnumerateFiles(Staging, "*", SearchOption.AllDirectories).Any(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
                    throw new BuildFailedException("Unexpected files remain in artwork staging; preserve and inspect them before recovery.");
                AssetDatabase.DeleteAsset(Staging);
            }
            AssetDatabase.SaveAssets();
            File.Delete(Journal);
        }
        public void Dispose() { if (!disposed) { Active = false; Recover(); ManifestPath = null; RemoteTexturePaths = null; disposed = true; } }
    }
}
