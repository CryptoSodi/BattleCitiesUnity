using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace BattleCities.UI
{
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenUpdateConfig
    {
        public string manifestUrl, modulus, exponent;
        public string channel = "android-ui-v1";
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenFile
    {
        public string url, sha256;
        public int bytes;
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenRelease
    {
        public int schemaVersion = 1;
        public long sequence;
        public string releaseId, channel, runtimeId, unityVersion, minimumAppVersion, maximumAppVersion;
        public ScreenFile document;
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class SignedScreenRelease { public string payload, signature; }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenImage
    {
        public string id;
        public ScreenFile file;
        public int width, height;
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenPatch
    {
        public string path, text, image;
        // Normalized top-left x/y/width/height, relative to the existing parent.
        public float[] rect;
        public float? fontSize;
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenFlowItem { public string path; public float weight = 1; }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenFlow
    {
        public string parent;
        public float gap = 8;
        public ScreenFlowItem[] children = Array.Empty<ScreenFlowItem>();
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenDefinition
    {
        public string id, platform = "all";
        public ScreenPatch[] patches = Array.Empty<ScreenPatch>();
        public ScreenFlow[] flows = Array.Empty<ScreenFlow>();
        public Dictionary<string,string> copy = new Dictionary<string,string>();
        public Dictionary<string,float[]> regions = new Dictionary<string,float[]>();
        public Dictionary<string,float> layout = new Dictionary<string,float>();
        public Dictionary<string,string> art = new Dictionary<string,string>();
    }
    [Serializable, UnityEngine.Scripting.Preserve] public sealed class ScreenDocument
    {
        public int schemaVersion = 1;
        public string runtimeId = ScreenUpdates.RuntimeId;
        public ScreenImage[] images = Array.Empty<ScreenImage>();
        public ScreenDefinition[] screens = Array.Empty<ScreenDefinition>();
    }

    /// <summary>Signed display-only releases, activated once at startup with cached/built-in fallback.</summary>
    public static class ScreenUpdates
    {
        public const string RuntimeId = "battlecities-screens-v1";
        public const string BaseUrl = "https://assets.battlecities.com/android/screens/v1/";
        const int MaxDocument = 262144, MaxEnvelope = 65536, MaxImage = 8388608;
        static readonly HashSet<string> ScreenIds = new HashSet<string> {
            "home", "login", "settings", "shop", "ranking", "profile", "quarters", "treasury",
            "manual", "manualdetail", "socials", "select-tank", "loadout", "battle-results", "replay", "lobby", "battle-hud", "pause" };
        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {
            MaxDepth = 24, TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error };
        static ScreenDocument current;
        static Task pending;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static string ReleaseId { get; private set; } = "built-in";
        public static int Revision { get; private set; }
        public static ScreenDocument Current
        {
            get
            {
                if(current!=null)return current;
                try { var resource=Resources.Load<TextAsset>("ScreenUpdates/default-screens");current=resource?ReadDocument(resource.text):new ScreenDocument(); }
                catch { current=new ScreenDocument(); }
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var item in sprites.Values) { if (item) { DisposeSprite(item); } }
            sprites.Clear(); current = null; pending = null; Revision = 0; ReleaseId = "built-in";ScreenContent.ResetCache();
        }
        static void DisposeSprite(Sprite item)
        {
            if(Application.isPlaying){UnityEngine.Object.Destroy(item.texture);UnityEngine.Object.Destroy(item);}
            else{UnityEngine.Object.DestroyImmediate(item.texture);UnityEngine.Object.DestroyImmediate(item);}
        }
        public static Sprite Image(string id) => id != null && sprites.TryGetValue(id, out var sprite) ? sprite : null;
        public static ScreenDefinition Definition(string id, string platform)
        {
            var definitions = Current.screens;
            var baseline=definitions.FirstOrDefault(x=>x.id==id&&x.platform=="all");
            var specific=definitions.FirstOrDefault(x=>x.id==id&&x.platform==platform);
            if(specific==null)return baseline;if(baseline==null||ReferenceEquals(baseline,specific))return specific;
            var patches=baseline.patches.ToDictionary(x=>x.path,x=>x);
            foreach(var patch in specific.patches)
            {
                patches.TryGetValue(patch.path,out var old);
                patches[patch.path]=new ScreenPatch{path=patch.path,text=patch.text??old?.text,rect=patch.rect??old?.rect,
                    image=patch.image??old?.image,fontSize=patch.fontSize??old?.fontSize};
            }
            var flows=baseline.flows.ToDictionary(x=>x.parent,x=>x);
            foreach(var flow in specific.flows)flows[flow.parent]=flow;
            return new ScreenDefinition{id=id,platform=platform,patches=patches.Values.ToArray(),flows=flows.Values.ToArray(),
                copy=Merge(baseline.copy,specific.copy),regions=Merge(baseline.regions,specific.regions),layout=Merge(baseline.layout,specific.layout),art=Merge(baseline.art,specific.art)};
        }
        static Dictionary<string,T> Merge<T>(Dictionary<string,T> baseline,Dictionary<string,T> specific)
        { var result=new Dictionary<string,T>(baseline);foreach(var pair in specific)result[pair.Key]=pair.Value;return result; }
        public static Task EnsureReady(Action<string> status, CancellationToken cancellation)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (pending == null) pending = Initialize(status, cancellation);
            return pending;
#else
            return Task.CompletedTask;
#endif
        }
        public static ScreenDocument ReadDocument(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxDocument) throw new InvalidDataException("Screen document is too large.");
            var value = JsonConvert.DeserializeObject<ScreenDocument>(json, JsonSettings);
            ValidateDocument(value); return value;
        }
        public static void ValidateDocument(ScreenDocument document)
        {
            if (document == null || document.schemaVersion != 1 || document.runtimeId != RuntimeId || document.screens == null
                || document.images == null || document.screens.Length > 64 || document.images.Length > 16)
                throw new InvalidDataException("Unsupported screen document.");
            var ids = new HashSet<string>(); long pixels = 0;
            foreach (var image in document.images)
            {
                if (image == null || !Token(image.id) || !ids.Add(image.id) || image.width < 1 || image.height < 1
                    || image.width > 4096 || image.height > 4096) throw new InvalidDataException("Invalid screen image.");
                ValidateFile(image.file, ".png", MaxImage); pixels += (long)image.width * image.height;
            }
            if (pixels > 16777216) throw new InvalidDataException("Screen images exceed the memory budget.");
            var variants = new HashSet<string>(); int patches = 0;
            foreach (var screen in document.screens)
            {
                if (screen == null || !ScreenIds.Contains(screen.id) || !new[] { "all", "web", "seeker", "psg1" }.Contains(screen.platform)
                    || !variants.Add(screen.id + ":" + screen.platform) || screen.patches == null || screen.flows == null || screen.flows.Length > 16)
                    throw new InvalidDataException("Invalid screen variant.");
                ValidateContent(screen,ids);
                var paths = new HashSet<string>();var flowParents=new HashSet<string>();
                foreach (var patch in screen.patches)
                {
                    if (patch == null || !PathIsSafe(patch.path) || !paths.Add(patch.path)) throw new InvalidDataException("Invalid screen target.");
                    if (patch.text != null && (!AllowsText(screen.id, patch.path) || patch.text.Length > 240 || patch.text.Contains('<') || patch.text.Contains('>')))
                        throw new InvalidDataException("Only declared static screen text can be updated.");
                    if (patch.fontSize.HasValue && (!Finite(patch.fontSize.Value) || patch.fontSize < 12 || patch.fontSize > 80))
                        throw new InvalidDataException("Invalid screen font size.");
                    if (patch.rect != null && (patch.rect.Length != 4 || patch.rect.Any(x => !Finite(x) || x < 0 || x > 1)
                        || patch.rect[2] <= 0 || patch.rect[3] <= 0 || patch.rect[0] + patch.rect[2] > 1.0001f || patch.rect[1] + patch.rect[3] > 1.0001f))
                        throw new InvalidDataException("Invalid normalized screen bounds.");
                    if (patch.image != null && !ids.Contains(patch.image)) throw new InvalidDataException("Unknown screen image.");
                    patches++;
                }
                foreach (var flow in screen.flows)
                {
                    if (flow == null || !PathIsSafe(flow.parent) || !flowParents.Add(flow.parent) || !Finite(flow.gap) || flow.gap < 0 || flow.gap > 64
                        || flow.children == null || flow.children.Length < 1 || flow.children.Length > 16)
                        throw new InvalidDataException("Invalid screen flow.");
                    var children = new HashSet<string>();
                    foreach (var child in flow.children)
                        if (child == null || !PathIsSafe(child.path) || child.path.Contains('/') || !children.Add(child.path)
                            || !Finite(child.weight) || child.weight < .25f || child.weight > 4)
                            throw new InvalidDataException("Invalid screen row.");
                }
            }
            if (patches > 512) throw new InvalidDataException("Too many screen overrides.");
        }
        static void ValidateContent(ScreenDefinition screen,HashSet<string> documentImageIds)
        {
            if(screen.copy==null||screen.regions==null||screen.layout==null||screen.art==null||screen.art.Count>256||screen.copy.Count>256||screen.regions.Count>256||screen.layout.Count>64)
                throw new InvalidDataException("Invalid screen content collections.");
            foreach(var pair in screen.copy)
                if(!ScreenContent.Knows(screen.id,"copy",pair.Key)||pair.Value==null||pair.Value.Length>4096||System.Text.RegularExpressions.Regex.IsMatch(pair.Value,"<[^>]*>")
                    ||pair.Value.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\t'))throw new InvalidDataException("Unknown or invalid static screen copy: "+screen.id+"/"+pair.Key);
            foreach(var pair in screen.regions)
            {
                var bounds=pair.Value;
                if(bounds==null&&ScreenContent.Knows(screen.id,"regions",pair.Key))continue;
                if(!ScreenContent.Knows(screen.id,"regions",pair.Key)||bounds==null||bounds.Length!=4||bounds.Any(x=>!Finite(x)||x<0||x>1)
                    ||bounds[2]<=0||bounds[3]<=0||bounds[0]+bounds[2]>1.001f||bounds[1]+bounds[3]>1.001f)
                    throw new InvalidDataException("Unknown or invalid screen region.");
            }
            foreach(var pair in screen.art)
                if(!ScreenContent.Knows(screen.id,"art",pair.Key)||(pair.Value!=null&&!documentImageIds.Contains(pair.Value)))
                    throw new InvalidDataException("Unknown screen artwork binding.");
            foreach(var pair in screen.layout)
                if(!ScreenContent.Knows(screen.id,"layout",pair.Key)||!Finite(pair.Value)||!ScreenContent.InRange(screen.id,pair.Key,pair.Value))
                    throw new InvalidDataException("Unknown or invalid screen layout setting.");
        }
        public static bool AllowsText(string screen, string path)
        {
            if (screen == "settings") return new[] { "Title plate/Title", "Rows/Row 0/Name", "Rows/Row 1/Name", "Rows/Row 2/Name", "Rows/Row 3/Name",
                "Rows/Row 0/Description", "Rows/Row 1/Description" }.Contains(path);
            if (screen == "battle-results" || screen == "login") return false;
            return path == "Title plate/Title" || path == "Header/Title" || path == "Title/Title";
        }
        static bool Token(string value) => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "\\A[a-z0-9][a-z0-9-]{0,63}\\z");
        static bool Finite(float value) => !float.IsInfinity(value) && !float.IsNaN(value);
        static bool PathIsSafe(string path) => !string.IsNullOrWhiteSpace(path) && path.Length <= 240 && !path.StartsWith('/')
            && !path.EndsWith('/') && path.Split('/').All(x => x.Length > 0 && x != "." && x != ".." && !x.Any(char.IsControl));
        public static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static void ValidateFile(ScreenFile file, string extension, int maximum)
        {
            if (file == null || file.bytes < 1 || file.bytes > maximum || !System.Text.RegularExpressions.Regex.IsMatch(file.sha256 ?? "", "\\A[a-f0-9]{64}\\z")
                || file.url != BaseUrl + file.sha256 + extension) throw new InvalidDataException("Invalid screen content reference.");
        }
        public static ScreenRelease VerifyRelease(string envelopeJson, ScreenUpdateConfig config, string appVersion)
        {
            if (string.IsNullOrEmpty(envelopeJson) || Encoding.UTF8.GetByteCount(envelopeJson) > MaxEnvelope || config == null
                || config.manifestUrl != BaseUrl + "latest.json" || config.channel != "android-ui-v1") throw new InvalidDataException("Invalid screen release configuration.");
            var envelope = JsonConvert.DeserializeObject<SignedScreenRelease>(envelopeJson, JsonSettings);
            byte[] payload = Convert.FromBase64String(envelope.payload), signature = Convert.FromBase64String(envelope.signature);
            if (payload.Length > 32768) throw new InvalidDataException("Screen release payload is too large.");
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters { Modulus = Convert.FromBase64String(config.modulus), Exponent = Convert.FromBase64String(config.exponent) });
                if (rsa.KeySize < 2048 || !rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    throw new InvalidDataException("Screen release signature did not verify.");
            }
            var release = JsonConvert.DeserializeObject<ScreenRelease>(Encoding.UTF8.GetString(payload), JsonSettings);
            if (release == null || release.schemaVersion != 1 || release.sequence < 1 || !Token(release.releaseId) || release.channel != config.channel
                || release.runtimeId != RuntimeId || release.unityVersion != Application.unityVersion
                || !Version.TryParse(appVersion, out var version) || !Version.TryParse(release.minimumAppVersion, out var minimum)
                || !Version.TryParse(release.maximumAppVersion, out var maximum) || version < minimum || version >= maximum)
                throw new InvalidDataException("Screen release is not compatible with this game version.");
            ValidateFile(release.document, ".json", MaxDocument); return release;
        }
        public static bool CanReplace(ScreenRelease next, ScreenRelease previous) => previous == null || next.sequence > previous.sequence
            || (next.sequence == previous.sequence && next.document.sha256 == previous.document.sha256 && next.releaseId == previous.releaseId);

        static async Task Initialize(Action<string> status, CancellationToken cancellation, ScreenUpdateConfig supplied = null, string cacheDirectory = null, string appVersion = null)
        {
            var fallback = Current;
            string directory = cacheDirectory ?? Path.Combine(Application.persistentDataPath, "ScreenUpdates-v1");
            string version = appVersion ?? Application.version;
            ScreenRelease saved = null; string savedEnvelope = null;
            var resource = Resources.Load<TextAsset>("ScreenUpdates/config");
            if (!resource && supplied == null) return;
            ScreenUpdateConfig config;
            try { config = supplied ?? JsonConvert.DeserializeObject<ScreenUpdateConfig>(resource.text, JsonSettings); }
            catch { return; }
            try
            {
                Directory.CreateDirectory(directory);
                var active = Path.Combine(directory, "active.json");
                if (File.Exists(active))
                {
                    if(new FileInfo(active).Length>MaxEnvelope)throw new InvalidDataException("Saved screen release is too large.");
                    savedEnvelope = File.ReadAllText(active); saved = VerifyRelease(savedEnvelope, config, version);
                    var cached = ReadCached(directory, saved.document, ".json");
                    if (cached == null) { saved = null; savedEnvelope = null; }
                    else { var document = ReadDocument(Encoding.UTF8.GetString(cached)); Activate(document, saved.releaseId, directory); }
                }
            }
            catch { saved = null; savedEnvelope = null; current = fallback; ReleaseId = "built-in"; }
            try
            {
                status?.Invoke("Checking for screen updates...");
                var envelope = Encoding.UTF8.GetString(await Fetch(config.manifestUrl + "?check=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(), MaxEnvelope, 8, cancellation));
                var release = VerifyRelease(envelope, config, version);
                if (!CanReplace(release, saved)) throw new InvalidDataException("Stale screen release rejected.");
                var bytes = await GetFile(directory, release.document, ".json", cancellation);
                var next = ReadDocument(Encoding.UTF8.GetString(bytes));
                foreach (var image in next.images) await GetFile(directory, image.file, ".png", cancellation);
                cancellation.ThrowIfCancellationRequested();
                Activate(next, release.releaseId, directory);
                WriteAtomic(Path.Combine(directory, "active.json"), Encoding.UTF8.GetBytes(envelope));
                Debug.Log("Screen update ready: " + release.releaseId);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { Debug.Log("Screen update check unavailable; using " + ReleaseId + "."); }
        }
        static void Activate(ScreenDocument document, string releaseId, string directory)
        {
            var prepared = new Dictionary<string, Sprite>();
            try
            {
                foreach (var image in document.images)
                {
                    var bytes = ReadCached(directory, image.file, ".png");
                    if (bytes == null || bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                        throw new InvalidDataException("Screen image is unavailable.");
                    uint Width(int at) => (uint)(bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3]);
                    if (Width(16) != image.width || Width(20) != image.height) throw new InvalidDataException("Screen image dimensions do not match.");
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags=HideFlags.HideAndDontSave, name="Screen update/"+image.id };
                    if (!texture.LoadImage(bytes, true) || texture.width != image.width || texture.height != image.height)
                    { if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture); throw new InvalidDataException("Screen image could not be decoded."); }
                    var sprite=Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    sprite.name=image.id;sprite.hideFlags=HideFlags.HideAndDontSave;prepared.Add(image.id,sprite);
                }
            }
            catch { foreach (var item in prepared.Values) { DisposeSprite(item); } throw; }
            foreach (var item in sprites.Values) { DisposeSprite(item); }
            sprites.Clear(); foreach (var item in prepared) sprites.Add(item.Key, item.Value);
            current = document; ReleaseId = releaseId; Revision++;
        }
        static byte[] ReadCached(string directory, ScreenFile file, string extension)
        {
            var path = Path.Combine(directory, file.sha256 + extension);
            if (!File.Exists(path) || new FileInfo(path).Length != file.bytes) return null;
            var bytes = File.ReadAllBytes(path); return Hash(bytes) == file.sha256 ? bytes : null;
        }
        static async Task<byte[]> GetFile(string directory, ScreenFile file, string extension, CancellationToken cancellation)
        {
            var bytes = ReadCached(directory, file, extension);
            if (bytes != null) return bytes;
            bytes = await Fetch(file.url, file.bytes, extension == ".png" ? 45 : 15, cancellation);
            if (bytes.Length != file.bytes || Hash(bytes) != file.sha256) throw new InvalidDataException("Screen download failed verification.");
            WriteAtomic(Path.Combine(directory, file.sha256 + extension), bytes); return bytes;
        }
        static void WriteAtomic(string path, byte[] bytes)
        {
            string temp = path + ".partial";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        static async Task<byte[]> Fetch(string url, int limit, int timeout, CancellationToken cancellation)
        {
#if UNITY_EDITOR
            if(EditorFetch!=null)return await EditorFetch(url,limit,timeout,cancellation);
#endif
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = timeout; request.redirectLimit = 0; request.SetRequestHeader("Cache-Control", "no-cache");
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellation.IsCancellationRequested) { request.Abort(); cancellation.ThrowIfCancellationRequested(); }
                    if (request.downloadedBytes > (ulong)limit) { request.Abort(); throw new InvalidDataException("Screen download is too large."); }
                    await Task.Yield();
                }
                if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data.Length > limit)
                    throw new IOException("Screen update download unavailable.");
                return request.downloadHandler.data;
            }
        }
#if UNITY_EDITOR
        public static Func<string,int,int,CancellationToken,Task<byte[]>> EditorFetch;
        public static async Task EditorInitialize(ScreenUpdateConfig config,string directory,string version,Func<string,int,int,CancellationToken,Task<byte[]>> fetch)
        {
            EditorFetch=fetch;
            try { await Initialize(null,CancellationToken.None,config,directory,version); }
            finally { EditorFetch=null; }
        }
        public static void EditorUseDocument(ScreenDocument document, string releaseId)
        { ValidateDocument(document); current = document; ReleaseId = releaseId; Revision++; }
        public static void EditorReset() => Reset();
#endif
    }
}
