using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BattleCities.UI
{
    /// <summary>
    /// Main-menu adapter for the Battle Cities HTTP API.
    /// Supports wallet sessions and guest play. Google authentication is intentionally excluded.
    /// </summary>
    public sealed class MainMenuApiClient : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerSnapshot
        {
            public string id;
            public string provider;
            public string displayName;
            public string walletAddress;
            public int highscorePrimary;
            public int highscoreSecondary;
            public int level = 1;
            public int levelPoints;
            public int levelPointsRequired = 1000;
        }

        [Header("Battle Cities API")]
        [SerializeField] private string baseUrl = "http://localhost:3001";
        [SerializeField] private bool loginAsGuestWhenAnonymous = true;
        [SerializeField] private bool automaticRefresh = true;
        [SerializeField, Min(5)] private float leaderboardRefreshSeconds = 30;
        [SerializeField, Min(1)] private int requestTimeoutSeconds = 10;

        public event Action<PlayerSnapshot> PlayerLoaded;
        public event Action<string[]> LeaderboardLoaded;
        public event Action<string, string> StatusChanged;

        public bool IsAuthenticated { get; private set; }
        public bool IsWalletAuthenticated { get; private set; }
        public bool IsLocalGuest { get; private set; }
        public string BaseUrl => baseUrl;
        public string LastStatusTitle { get; private set; }
        public string LastStatusDetail { get; private set; }

        private static string sessionCookie;
        private readonly System.Collections.Generic.Dictionary<string, JObject> browserResponses = new System.Collections.Generic.Dictionary<string, JObject>();
        private Coroutine refreshRoutine;
        private bool requestInFlight;
        private bool guestLoginAttempted;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (automaticRefresh)
            {
                RefreshNow();
                refreshRoutine = StartCoroutine(RefreshLoop());
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            refreshRoutine = null;
            requestInFlight = false;
            browserResponses.Clear();
        }

        public void ConfigureAutomaticRefresh(bool value) { automaticRefresh = value; }

        public void ConfigureGuestFallback(bool enabled)
        {
            loginAsGuestWhenAnonymous = enabled;
        }

        public void Configure(string apiBaseUrl)
        {
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("API URL must be an absolute HTTP or HTTPS URL.", nameof(apiBaseUrl));
            baseUrl = apiBaseUrl.TrimEnd('/');
        }

        public void RefreshNow()
        {
            if (isActiveAndEnabled && !requestInFlight) StartCoroutine(RefreshMenuData());
        }

        public void ConnectWallet()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NotifyStatus("CONNECTING WALLET", "Approve the Phantom connection and message signature.");
            BattleCitiesWalletBridge.Connect(gameObject.name, nameof(OnWalletLoginResult), baseUrl.TrimEnd('/'));
#else
            NotifyStatus("WALLET LOGIN UNAVAILABLE", "Open the WebGL build to sign in with Phantom. Guest play is available here.");
#endif
        }

        public void ContinueAsGuest()
        {
            if (!isActiveAndEnabled) return;
            PlayerPrefs.SetString("battlecities.loginMode", "guest");
            UseLocalGuest();
        }

        public void OnWalletLoginResult(string json)
        {
            try
            {
                var body = JObject.Parse(json);
                if ((bool?)body["ok"] != true)
                {
                    NotifyStatus("WALLET LOGIN FAILED", (string)body["error"] ?? "Phantom did not complete login.");
                    return;
                }

                var player = ParsePlayer(body["player"] as JObject);
                if (player == null || player.provider != "wallet") throw new InvalidOperationException("Wallet player response was invalid.");
                PlayerPrefs.SetString("battlecities.loginMode", "wallet");
                IsAuthenticated = true;
                IsWalletAuthenticated = true;
                IsLocalGuest = false;
                PlayerLoaded?.Invoke(player);
                NotifyStatus("WALLET CONNECTED", ShortWallet(player.walletAddress));
                if (automaticRefresh) RefreshNow();
            }
            catch (Exception exception)
            {
                NotifyStatus("WALLET LOGIN FAILED", exception.Message);
            }
        }

        private IEnumerator RefreshLoop()
        {
            while (enabled)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(5, leaderboardRefreshSeconds));
                if (!requestInFlight) yield return RefreshMenuData();
            }
        }

        private IEnumerator RefreshMenuData()
        {
            requestInFlight = true;
            if (loginAsGuestWhenAnonymous && PlayerPrefs.GetString("battlecities.loginMode") == "guest")
            {
                yield return CreateGuestSession();
                yield return LoadLeaderboard();
                requestInFlight = false;
                yield break;
            }
            NotifyStatus("CONNECTING", "Loading live Battle Cities data.");

            JObject session = null;
            string sessionError = null;
            yield return Request("GET", "/api/session", null, (code, body, error) =>
            {
                session = body;
                sessionError = error;
            });

            if (session != null && (bool?)session["authenticated"] == true && (string)session["provider"] == "wallet")
            {
                IsAuthenticated = true;
                IsWalletAuthenticated = string.Equals((string)session["provider"], "wallet", StringComparison.OrdinalIgnoreCase);
                IsLocalGuest = false;
                yield return LoadPlayer();
            }
            else if (loginAsGuestWhenAnonymous && (!IsLocalGuest || !guestLoginAttempted))
            {
                yield return CreateGuestSession();
            }
            else if (!string.IsNullOrEmpty(sessionError))
            {
                NotifyStatus("API OFFLINE", sessionError);
            }

            yield return LoadLeaderboard();
            requestInFlight = false;
        }

        private IEnumerator CreateGuestSession()
        {
            yield return null;
            UseLocalGuest();
        }

        private void UseLocalGuest()
        {
            guestLoginAttempted = true;
            // The live API intentionally excludes guest accounts. Local play never claims server auth.
            IsAuthenticated = false;
            IsWalletAuthenticated = false;
            IsLocalGuest = true;
            var guestName = GetOrCreateGuestName();
            PlayerLoaded?.Invoke(new PlayerSnapshot
            {
                id = PlayerPrefs.GetString("battlecities.guestId"),
                provider = "guest",
                displayName = guestName,
                highscorePrimary = PlayerPrefs.GetInt("battlecities.guestHighScore", 0)
            });
            NotifyStatus("PLAYING AS " + guestName, "Guest progress is local. Connect a wallet for ranked rewards.");
        }

        private IEnumerator LoadPlayer()
        {
            JObject body = null;
            yield return Request("GET", "/api/player", null, (_, json, __) => body = json);
            if ((bool?)body?["authenticated"] != true) yield break;
            var player = ParsePlayer(body["player"] as JObject);
            if (player != null) PlayerLoaded?.Invoke(player);
        }

        private IEnumerator LoadLeaderboard()
        {
            JObject body = null;
            string error = null;
            yield return Request("GET", "/api/leaderboard/rewards", null, (_, json, requestError) =>
            {
                body = json;
                error = requestError;
            });

            var values = body?["rows"] as JArray;
            if (values == null)
            {
                LeaderboardLoaded?.Invoke(Array.Empty<string>());
                NotifyStatus("LIVE BOARD UNAVAILABLE", string.IsNullOrWhiteSpace(error) ? "The rewards API returned an invalid response." : error);
                yield break;
            }

            var formatted = new string[Math.Min(10, values.Count)];
            for (var index = 0; index < formatted.Length; index++)
            {
                var row = values[index] as JObject;
                var rank = Math.Max(1, (int?)row?["rank"] ?? index + 1);
                var name = ((string)row?["displayName"] ?? "PLAYER").ToUpperInvariant();
                var points = Math.Max(0, (int?)row?["totalPoints"] ?? 0);
                formatted[index] = string.Format("{0,2}.  {1,-18}  {2:N0}", rank, Trim(name, 18), points);
            }

            LeaderboardLoaded?.Invoke(formatted.Length == 0 ? new[] { "NO SCORES YET — PLAY TO RANK" } : formatted);
            var enabled = (bool?)body["enabled"] == true;
            var nextRewardAt = (string)body["nextRewardAt"];
            NotifyStatus(enabled ? "LIVE REWARD ROUND" : "LIVE SCORE ROUND",
                string.IsNullOrWhiteSpace(nextRewardAt) ? "Top 10 standings loaded." : "Next round: " + nextRewardAt);
        }

        private IEnumerator Request(string method, string path, JObject payload, Action<long, JObject, string> completed)
        {
            var url = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path.TrimStart('/')).ToString();
#if UNITY_WEBGL && !UNITY_EDITOR
            var id = Guid.NewGuid().ToString("N");
            BattleCitiesWalletBridge.Request(gameObject.name, nameof(OnApiResponse), id, url, method,
                payload == null ? "" : payload.ToString(Formatting.None), requestTimeoutSeconds);
            var deadline = Time.realtimeSinceStartup + requestTimeoutSeconds + 2;
            while (!browserResponses.ContainsKey(id) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!browserResponses.TryGetValue(id, out var response))
            { completed(0, null, "The API request timed out. Please retry."); yield break; }
            browserResponses.Remove(id);
            completed((long?)response["status"] ?? 0, response["body"] as JObject, (string)response["error"]);
            yield break;
#else
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(1, requestTimeoutSeconds);
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Cache-Control", "no-store");
                if (payload != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
#if !UNITY_WEBGL || UNITY_EDITOR
                if (!string.IsNullOrEmpty(sessionCookie)) request.SetRequestHeader("Cookie", sessionCookie);
#endif
                yield return request.SendWebRequest();

                CaptureSessionCookie(request.GetResponseHeader("Set-Cookie"));
                JObject body = null;
                var text = request.downloadHandler?.text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    try { body = JObject.Parse(text); }
                    catch { }
                }

                var error = request.result == UnityWebRequest.Result.ConnectionError ||
                            request.result == UnityWebRequest.Result.DataProcessingError
                    ? "Cannot reach " + baseUrl
                    : request.responseCode >= 400
                        ? (string)body?["error"] ?? ("API request failed (" + request.responseCode + ").")
                        : null;
                completed?.Invoke(request.responseCode, body, error);
            }
#endif
        }

        public void OnApiResponse(string json)
        {
            try { var value = JObject.Parse(json); var id = (string)value["id"]; if (!string.IsNullOrEmpty(id)) browserResponses[id] = value; }
            catch (JsonException) { NotifyStatus("API REQUEST FAILED", "The server returned an invalid response."); }
        }

        private void CaptureSessionCookie(string setCookie)
        {
            if (string.IsNullOrWhiteSpace(setCookie)) return;
            var separator = setCookie.IndexOf(';');
            sessionCookie = separator >= 0 ? setCookie.Substring(0, separator) : setCookie;
        }

        private void NotifyStatus(string title, string detail)
        {
            LastStatusTitle = title ?? string.Empty;
            LastStatusDetail = detail ?? string.Empty;
            StatusChanged?.Invoke(LastStatusTitle, LastStatusDetail);
        }

        private static PlayerSnapshot ParsePlayer(JObject source)
        {
            if (source == null || string.IsNullOrWhiteSpace((string)source["displayName"])) return null;
            var progression = source["progression"] as JObject;
            return new PlayerSnapshot
            {
                id = (string)source["id"],
                provider = (string)source["provider"],
                displayName = (string)source["displayName"],
                walletAddress = (string)source["walletAddress"],
                highscorePrimary = Math.Max(0, (int?)source["highscorePrimary"] ?? 0),
                highscoreSecondary = Math.Max(0, (int?)source["highscoreSecondary"] ?? 0),
                level = Math.Max(1, (int?)progression?["level"] ?? 1),
                levelPoints = Math.Max(0, (int?)progression?["points"] ?? 0),
                levelPointsRequired = Math.Max(1, (int?)progression?["pointsRequired"] ?? 1000)
            };
        }

        private static string Trim(string value, int maximum)
        {
            if (string.IsNullOrEmpty(value)) return "PLAYER";
            return value.Length <= maximum ? value : value.Substring(0, maximum);
        }

        private static string ShortWallet(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Length < 9
                ? "Phantom wallet authenticated."
                : value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
        }

        private static string GetOrCreateGuestName()
        {
            var id = PlayerPrefs.GetString("battlecities.guestId", "");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = "guest-" + Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString("battlecities.guestId", id);
            }
            var name = PlayerPrefs.GetString("battlecities.guestName", "");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "GUEST-" + id.Substring(id.Length - 4).ToUpperInvariant();
                PlayerPrefs.SetString("battlecities.guestName", name);
            }
            PlayerPrefs.Save();
            return name;
        }
    }

    internal static class BattleCitiesWalletBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void BattleCitiesConnectWallet(string gameObjectName, string callbackMethod, string baseUrl);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void BattleCitiesApiRequest(string target, string callback, string id, string url, string method, string payload, int timeout);
#endif
        public static void Connect(string gameObjectName, string callbackMethod, string baseUrl)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesConnectWallet(gameObjectName, callbackMethod, baseUrl);
#endif
        }
        public static void Request(string target, string callback, string id, string url, string method, string payload, int timeout)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesApiRequest(target, callback, id, url, method, payload, timeout);
#endif
        }
    }
}
