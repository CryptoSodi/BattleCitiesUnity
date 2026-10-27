using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace BattleCities.UI
{
    // One login owns its HTTP cookie jar. A cancelled/late result cannot authenticate the game.
    internal sealed class SeekerLoginHttp : ISeekerLoginApi
    {
        readonly string origin;
        public string Origin => origin;
        public string Cookie { get; private set; }
        public long ExpiresAt { get; private set; }
        internal SeekerLoginHttp(string baseUrl)
        {
            if (baseUrl.TrimEnd('/') != MainMenuApiClient.DefaultApiBaseUrl)
                throw new ArgumentException("Wallet sign-in requires the API selected by this game build.");
            origin = MainMenuApiClient.DefaultApiBaseUrl;
        }
        public async Task<JObject> Request(string method, string path, JObject body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (path != "/api/session" && path != "/api/player") throw new ArgumentException("Unexpected sign-in endpoint.");
            using var request = new UnityWebRequest(origin + path, method);
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15; request.redirectLimit = 0;
            request.SetRequestHeader("Accept", "application/json"); request.SetRequestHeader("Cache-Control", "no-store");
            if (Cookie != null) request.SetRequestHeader("Cookie", Cookie);
            if (body != null) {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            var operation = request.SendWebRequest();
            try {
                while (!operation.isDone) {
                    token.ThrowIfCancellationRequested();
                    if (request.downloadedBytes > 65536) throw new InvalidOperationException("The API returned an invalid response.");
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested();
                UnityEngine.Debug.Log("[BattleCitiesWallet] HTTP " + method + " " + path + " status=" + request.responseCode + " result=" + request.result);
                if (request.result != UnityWebRequest.Result.Success || request.downloadedBytes > 65536)
                    throw new InvalidOperationException("The Battle Cities API could not complete sign-in. Check your connection and try again.");
                var header = request.GetResponseHeader("Set-Cookie");
                var match = Regex.Match(header ?? "", @"(?:^|[,\r\n])\s*(battlecity_session=[^;,\r\n]*)");
                if (match.Success) {
                    Cookie = match.Groups[1].Value;
                    var age = Regex.Match(header, @"(?:^|;)\s*Max-Age=(\d+)", RegexOptions.IgnoreCase);
                    long seconds = age.Success && long.TryParse(age.Groups[1].Value, out var parsed) ? parsed : 2592000;
                    ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Min(seconds, 2592000);
                }
                return JObject.Parse(request.downloadHandler.text);
            }
            finally { if (!operation.isDone) request.Abort(); }
        }
    }
}
