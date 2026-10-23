using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BattleCities
{
    // Phase one: immutable, APK-pinned artwork. No downloaded executable code.
    public static class RemoteGameArt
    {
        [Serializable] public sealed class Manifest
        {
            public string url, sha256, unityVersion;
            public long bytes;
        }
        public sealed class DownloadException : IOException
        {
            public bool IsTransient { get; }
            public DownloadException(UnityWebRequest.Result result, long responseCode, string detail, ulong received)
                : base($"Game content request failed: {result}, HTTP {responseCode}, received {received} bytes. {detail}")
            {
                IsTransient = result == UnityWebRequest.Result.ConnectionError || responseCode == 408
                    || responseCode == 429 || responseCode >= 500;
            }
        }
        static AssetBundle bundle;
        static Task pending;
        const string ConfigKey = "RemoteGameArtManifest";
        public static bool Required => Resources.Load<TextAsset>(ConfigKey) != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { if (bundle) bundle.Unload(true); bundle = null; pending = null; }

        public static T Load<T>(string key) where T : UnityEngine.Object
        {
            if (bundle)
            {
                var remote = bundle.LoadAsset<T>(Path.GetFileName(key) + (typeof(T) == typeof(GameObject) ? ".prefab" : typeof(T) == typeof(Material) ? ".mat" : ".asset"));
                if (remote) return remote;
            }
            return Resources.Load<T>(key);
        }

        public static Task EnsureReady(Action<string> status, CancellationToken cancellation, Action<float> progress = null)
        {
            if (bundle || !Required) { progress?.Invoke(1); return Task.CompletedTask; }
            if (pending == null || pending.IsCompleted) pending = Download(status, cancellation, progress);
            return pending;
        }

        public static void Validate(Manifest config)
        {
            if (config == null || !Uri.TryCreate(config.url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Fragment) || config.bytes <= 0 || config.bytes > 1024L * 1024 * 1024 ||
                !System.Text.RegularExpressions.Regex.IsMatch(config.sha256 ?? "", "^[a-f0-9]{64}$") ||
                config.unityVersion != Application.unityVersion)
                throw new InvalidDataException("The game content configuration is invalid.");
        }

        public static bool VerifyFile(string path, Manifest config)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != config.bytes) return false;
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == config.sha256;
        }

        static async Task Download(Action<string> status, CancellationToken cancellation, Action<float> progress)
        {
            var config = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>(ConfigKey).text);
            Validate(config);
            var directory = Path.Combine(Application.persistentDataPath, "GameContent");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, config.sha256 + ".bundle");
            var partial = path + ".partial";
            status?.Invoke("Checking saved game content...");
            progress?.Invoke(0);
            var valid = await Task.Run(() => VerifyFile(path, config), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!valid)
            {
                try
                {
                    if (File.Exists(partial)) File.Delete(partial);
                    // Android can enter the scene before its network interface is ready.
                    // Stay on startup instead of reporting an asset outage while offline.
                    while (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        status?.Invoke("Waiting for an internet connection...");
                        await Task.Delay(500, cancellation);
                    }
                    using (var request = UnityWebRequest.Get(config.url))
                    {
                        request.downloadHandler = new DownloadHandlerFile(partial) { removeFileOnAbort = true };
                        request.timeout = 180;
                        request.redirectLimit = 0;
                        var operation = request.SendWebRequest();
                        while (!operation.isDone)
                        {
                            if (cancellation.IsCancellationRequested) { request.Abort(); cancellation.ThrowIfCancellationRequested(); }
                            if (request.downloadedBytes > (ulong)config.bytes)
                            { request.Abort(); throw new InvalidDataException("Game content exceeds the expected size."); }
                            status?.Invoke($"Downloading game content: {request.downloadedBytes / 1048576f:0.0} / {config.bytes / 1048576f:0.0} MB");
                            progress?.Invoke(.8f * Mathf.Clamp01((float)request.downloadedBytes / config.bytes));
                            await Task.Yield();
                        }
                        if (request.result != UnityWebRequest.Result.Success)
                            throw new DownloadException(request.result, request.responseCode, request.error, request.downloadedBytes);
                    }
                    status?.Invoke("Verifying game content...");
                    progress?.Invoke(.8f);
                    if (!await Task.Run(() => VerifyFile(partial, config), cancellation))
                        throw new InvalidDataException("Game content failed verification.");
                    cancellation.ThrowIfCancellationRequested();
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(partial, path);
                }
                finally { if (File.Exists(partial)) File.Delete(partial); }
            }
            status?.Invoke("Loading game content...");
            progress?.Invoke(.85f);
            var load = AssetBundle.LoadFromFileAsync(path);
            while (!load.isDone) { progress?.Invoke(.85f + .05f * load.progress); await Task.Yield(); }
            if (cancellation.IsCancellationRequested)
            { if (load.assetBundle) load.assetBundle.Unload(true); cancellation.ThrowIfCancellationRequested(); }
            var loaded = load.assetBundle;
            if (!loaded) { File.Delete(path); throw new InvalidDataException("Game content could not be loaded."); }
            try
            {
                // Warm the complete pack, including referenced textures/models, before login.
                var assets = loaded.LoadAllAssetsAsync();
                while (!assets.isDone)
                {
                    progress?.Invoke(.9f + .09f * assets.progress);
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();
                if (!loaded.LoadAsset<GameObject>("TvReplayRig.prefab") || !loaded.LoadAsset<GameObject>("LandAttackDrone.prefab"))
                    throw new InvalidDataException("Game models are incomplete.");
                foreach (var key in new[] { "SettingsArt", "PlayerProfileArt", "OperationsArt", "LoadoutArt", "ShopArt", "PreBattleArt", "BattleResultsArt", "EnvironmentPalette", "RemoteBattleModels" })
                    if (!loaded.LoadAsset<ScriptableObject>(key + ".asset"))
                        throw new InvalidDataException("Game content is incomplete.");
                bundle = loaded;
                progress?.Invoke(1);
            }
            catch
            {
                loaded.Unload(true);
                if (!cancellation.IsCancellationRequested) File.Delete(path);
                throw;
            }
        }
    }
}
