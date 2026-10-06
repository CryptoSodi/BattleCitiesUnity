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
    public sealed partial class MainMenuApiClient : MonoBehaviour
    {
        public const string DefaultApiBaseUrl = "https://api.battlecities.com";

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

        public sealed class RankingRow
        {
            public string playerId, displayName;
            public int rank, totalPoints, matches;
        }

        public sealed class RankingsSnapshot
        {
            public string seasonName;
            public RankingRow[] rows;
            public RankingRow me;
        }

        public sealed class RoundSnapshot
        {
            public bool payoutsEnabled;
            public string startsAt, endsAt;
            public int intervalMinutes;
            public RankingRow[] rows;
            public RankingRow currentPlayer;
        }

        [Header("Battle Cities API")]
        [SerializeField] private string baseUrl = DefaultApiBaseUrl;
        [SerializeField] private bool loginAsGuestWhenAnonymous = true;
        [SerializeField] private bool automaticRefresh = true;
        [SerializeField, Min(5)] private float leaderboardRefreshSeconds = 30;
        [SerializeField, Min(1)] private int requestTimeoutSeconds = 10;

        public event Action<PlayerSnapshot> PlayerLoaded;
        public event Action<string[]> LeaderboardLoaded;
        public event Action<RankingsSnapshot> RankingsLoaded;
        public event Action<RoundSnapshot> RoundLoaded;
        public event Action<string, string> StatusChanged;

        public bool IsAuthenticated { get; private set; }
        public bool IsWalletAuthenticated { get; private set; }
        public bool IsLocalGuest { get; private set; }
        public string BaseUrl => baseUrl;
        public string LastStatusTitle { get; private set; }
        public string LastStatusDetail { get; private set; }
        public RankingsSnapshot LastRankings { get; private set; }
        public RoundSnapshot LastRound { get; private set; }

        private static string sessionCookie;
        private static string sessionCookieOrigin;
        public static string CurrentGuestId { get; private set; }
        private static string currentGuestName;
        private readonly System.Collections.Generic.Dictionary<string, JObject> browserResponses = new System.Collections.Generic.Dictionary<string, JObject>();
        private Coroutine refreshRoutine;
        private Coroutine menuDataRoutine;
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
            CancelWalletLogin(false);
            StopAllCoroutines();
            refreshRoutine = null;
            requestInFlight = false;
            browserResponses.Clear();
            IsSigningOut = false;
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

#if UNITY_EDITOR
        public Action<IEnumerator> EditorStartRoutineOverride;
#endif
        private Coroutine StartMenuRefresh(IEnumerator routine)
        {
#if UNITY_EDITOR
            if(EditorStartRoutineOverride!=null){EditorStartRoutineOverride(routine);return null;}
#endif
            return StartCoroutine(routine);
        }
        public void RefreshNow()
        {
            if (isActiveAndEnabled && !requestInFlight && !IsWalletLoginPending)
                menuDataRoutine = StartMenuRefresh(RefreshMenuData());
        }

        public void ConnectWallet()
        {
            if (!isActiveAndEnabled || IsWalletLoginPending) return;
            if (menuDataRoutine != null) { StopCoroutine(menuDataRoutine); menuDataRoutine = null; }
            requestInFlight = false;
            walletAttemptId = Guid.NewGuid().ToString("N");
            walletDeadline = Time.realtimeSinceStartupAsDouble + 200;
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
            NotifyStatus("CONNECTING WALLET", "Approve the Phantom connection and message signature.");
            BattleCitiesWalletBridge.Connect(gameObject.name, nameof(OnWalletLoginResult), baseUrl.TrimEnd('/'), walletAttemptId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            NotifyStatus("CONNECTING WALLET", RuntimePlatformInfo.IsPsg1
                ? "Approve sign-in in Jupiter Wallet, then return to Battle Cities."
                : "Choose your Seeker wallet and approve the sign-in message.");
            if (!mobileWallet) mobileWallet = gameObject.AddComponent<MobileWalletLogin>();
            mobileWallet.Connect(baseUrl.TrimEnd('/'), RuntimePlatformInfo.IsPsg1, walletAttemptId, OnWalletLoginResult);
#else
            CancelWalletLogin(false);
            NotifyStatus("WALLET LOGIN UNAVAILABLE", "Wallet sign-in runs in the web build or on an Android device. Guest play is available in the Editor.");
#endif
            }
            catch (Exception)
            {
                CancelWalletLogin(false);
                NotifyStatus("WALLET LOGIN FAILED", "Could not open the wallet. Check that a compatible wallet is installed and try again.");
            }
        }

        public void ContinueAsGuest()
        {
            if (!isActiveAndEnabled) return;
            CancelWalletLogin(false);
            BeginGuestLogin();
            PlayerPrefs.SetString("battlecities.loginMode", "guest");
            UseLocalGuest();
        }

        private static void BeginGuestLogin()
        {
            CurrentGuestId = "guest-" + Guid.NewGuid().ToString("N");
            currentGuestName = "GUEST-" + CurrentGuestId.Substring(CurrentGuestId.Length - 4).ToUpperInvariant();
            PlayerPrefs.SetString("battlecities.guestId", CurrentGuestId);
            PlayerPrefs.SetString("battlecities.guestName", currentGuestName);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGuestLogin()
        {
            CurrentGuestId = null;
            currentGuestName = null;
            sessionCookie = null;
            sessionCookieOrigin = null;
        }

        public void OnWalletLoginResult(string json)
        {
            if (!isActiveAndEnabled || !IsWalletLoginPending) return;
            try
            {
                var body = JObject.Parse(json);
                if ((string)body["attemptId"] != walletAttemptId) return;
                if ((bool?)body["ok"] != true)
                {
                    CancelWalletLogin(false);
                    NotifyStatus("WALLET LOGIN FAILED", (string)body["error"] ?? "The wallet did not complete sign-in.");
                    return;
                }

                var player = ParsePlayer(body["player"] as JObject);
                if (player == null || player.provider != "wallet" || string.IsNullOrWhiteSpace(player.walletAddress))
                    throw new InvalidOperationException("Wallet player response was invalid.");
#if UNITY_ANDROID && !UNITY_EDITOR
                CaptureSessionCookie((string)body["sessionCookie"]);
                if (string.IsNullOrEmpty(sessionCookie) || sessionCookieOrigin != ApiOrigin)
                    throw new InvalidOperationException("The server did not return a wallet session.");
#endif
                CancelWalletLogin(false);
                PlayerPrefs.SetString("battlecities.loginMode", "wallet");
                PlayerPrefs.Save();
                IsAuthenticated = true;
                IsWalletAuthenticated = true;
                IsLocalGuest = false;
                NotifyStatus("WALLET CONNECTED", ShortWallet(player.walletAddress));
                PublishPlayer(player);
                if (automaticRefresh) RefreshNow();
            }
            catch (Exception exception)
            {
                CancelWalletLogin(false);
                NotifyStatus("WALLET LOGIN FAILED", exception.Message);
            }
        }

        private IEnumerator RefreshLoop()
        {
            while (enabled)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(5, leaderboardRefreshSeconds));
                if (!requestInFlight && !IsWalletLoginPending) RefreshNow();
            }
        }

        private IEnumerator RefreshMenuData()
        {
            requestInFlight = true;
            if (loginAsGuestWhenAnonymous && PlayerPrefs.GetString("battlecities.loginMode") == "guest")
            {
                yield return CreateGuestSession();
                yield return LoadRound();
                yield return LoadRankings();
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
            else if (session != null && (bool?)session["authenticated"] != true)
            {
                IsAuthenticated = false;
                IsWalletAuthenticated = false;
                NotifyStatus("WALLET SESSION EXPIRED", "Please connect your wallet again.");
            }

            yield return LoadRound();
            yield return LoadRankings();
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
            PublishPlayer(new PlayerSnapshot
            {
                id = CurrentGuestId,
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
            if (player != null) PublishPlayer(player);
        }

        private IEnumerator LoadRound()
        {
            JObject body = null;
            string error = null;
            yield return Request("GET", "/api/leaderboard/rewards", null, (_, json, requestError) =>
            {
                body = json;
                error = requestError;
            });

            var values = body?["rows"] as JArray;
            var endsAt = (string)body?["nextRewardAt"];
            if (values == null || string.IsNullOrWhiteSpace(endsAt))
            {
                LastRound = null;
                RoundLoaded?.Invoke(null);
                NotifyStatus("ROUND UNAVAILABLE", string.IsNullOrWhiteSpace(error) ? "The live round response was invalid." : error);
                yield break;
            }

            var roundRows = new RankingRow[Math.Min(10, values.Count)];
            for (var index = 0; index < roundRows.Length; index++)
                roundRows[index] = ParseRankingRow(values[index] as JObject, index + 1);
            LastRound = new RoundSnapshot
            {
                payoutsEnabled = (bool?)body["enabled"] == true,
                startsAt = (string)body["intervalStartedAt"],
                endsAt = endsAt,
                intervalMinutes = Math.Max(1, (int?)body["rewardIntervalMinutes"] ?? 30),
                rows = roundRows,
                currentPlayer = ParseRankingRow(body["currentPlayer"] as JObject)
            };
            RoundLoaded?.Invoke(LastRound);
        }

        private IEnumerator LoadRankings()
        {
            JObject body = null;
            string error = null;
            yield return Request("GET", "/api/rankings?scope=gaming", null, (_, json, requestError) =>
            {
                body = json;
                error = requestError;
            });

            var values = body?["rows"] as JArray;
            if (values == null)
            {
                LastRankings = null;
                RankingsLoaded?.Invoke(null);
                LeaderboardLoaded?.Invoke(Array.Empty<string>());
                NotifyStatus("RANKINGS UNAVAILABLE", string.IsNullOrWhiteSpace(error) ? "The player rankings response was invalid." : error);
                yield break;
            }

            var rows = new RankingRow[Math.Min(10, values.Count)];
            var formatted = new string[rows.Length];
            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = ParseRankingRow(values[index] as JObject, index + 1);
                formatted[index] = string.Format("{0}. {1}: {2:N0} points", rows[index].rank, Trim(rows[index].displayName, 18), rows[index].totalPoints);
            }
            var season = body["currentSeason"] as JObject;
            LastRankings = new RankingsSnapshot
            {
                seasonName = (string)season?["name"] ?? "CURRENT SEASON",
                rows = rows,
                me = ParseRankingRow(body["me"] as JObject)
            };
            RankingsLoaded?.Invoke(LastRankings);
            LeaderboardLoaded?.Invoke(formatted);
            NotifyStatus("LIVE RANKINGS", LastRankings.seasonName + " standings loaded.");
        }

#if UNITY_EDITOR
        // Editor verification can exercise response handling without live requests or rewards.
        public Func<string,string,JObject,Action<long,JObject,string>,IEnumerator> EditorRequestOverride;
#endif
        public IEnumerator Request(string method, string path, JObject payload, Action<long, JObject, string> completed)
        {
#if UNITY_EDITOR
            if(EditorRequestOverride!=null){yield return EditorRequestOverride(method,path,payload,completed);yield break;}
#endif
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
                if (!string.IsNullOrEmpty(sessionCookie) && sessionCookieOrigin == ApiOrigin)
                    request.SetRequestHeader("Cookie", sessionCookie);
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
            var match = System.Text.RegularExpressions.Regex.Match(setCookie,
                @"(?:^|[,\r\n])\s*(battlecity_session=([^;,\r\n]*))");
            if (!match.Success) return;
            sessionCookie = string.IsNullOrEmpty(match.Groups[2].Value) ? null : match.Groups[1].Value;
            sessionCookieOrigin = ApiOrigin;
        }

        private void NotifyStatus(string title, string detail)
        {
            LastStatusTitle = title ?? string.Empty;
            LastStatusDetail = detail ?? string.Empty;
            StatusChanged?.Invoke(LastStatusTitle, LastStatusDetail);
        }

        private static RankingRow ParseRankingRow(JObject source, int fallbackRank = 0)
        {
            if (source == null) return null;
            return new RankingRow
            {
                playerId = (string)source["playerId"],
                displayName = (string)source["displayName"] ?? "PLAYER",
                rank = Math.Max(0, (int?)source["rank"] ?? fallbackRank),
                totalPoints = Math.Max(0, (int?)source["totalPoints"] ?? 0),
                matches = Math.Max(0, (int?)source["matches"] ?? 0)
            };
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
                ? "Wallet authenticated."
                : value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
        }

        private static string GetOrCreateGuestName()
        {
            if (string.IsNullOrEmpty(CurrentGuestId)) BeginGuestLogin();
            return currentGuestName;
        }
    }

    internal static class BattleCitiesWalletBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void BattleCitiesConnectWallet(string gameObjectName, string callbackMethod, string baseUrl, string attemptId);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void BattleCitiesCancelWallet(string attemptId);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void BattleCitiesApiRequest(string target, string callback, string id, string url, string method, string payload, int timeout);
#endif
        public static void Connect(string gameObjectName, string callbackMethod, string baseUrl, string attemptId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesConnectWallet(gameObjectName, callbackMethod, baseUrl, attemptId);
#endif
        }
        public static void Cancel(string attemptId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesCancelWallet(attemptId);
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
