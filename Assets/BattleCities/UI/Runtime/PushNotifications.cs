using System;
using System.Collections;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.SceneManagement;

namespace BattleCities.UI
{
    /// <summary>Native FCM registration and deferred notification navigation across scenes.</summary>
    [DisallowMultipleComponent]
    public sealed class PushNotifications : MonoBehaviour
    {
        const string Bridge = "com.battlecities.notifications.BattleCitiesNotifications";
        static PushNotifications instance;
        readonly ConcurrentQueue<string> responses = new ConcurrentQueue<string>();
        MainMenuApiClient api;
        MainMenuScene menu;
        Callback callback;
        string token, permission = "unavailable", syncedState, pending, sessionOwner;
        int sessionRevision;
        bool syncBusy, holdSync;
        float nextRefresh, nextSync, nextNavigation;
        public static bool IsNativeAndroid => Application.platform == RuntimePlatform.Android;
        public bool Supported { get; private set; }
        public bool Enabled { get; private set; }
        public bool Receiving => Supported && Enabled && permission == "granted" && !string.IsNullOrEmpty(token);
        public string Status { get; private set; } = "Checking notifications...";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize() { if (IsNativeAndroid) Ensure(); }
        public static PushNotifications Ensure()
        {
            if (instance) return instance;
            var go = new GameObject("Battle Cities notifications");
            go.SetActive(false);
            instance = go.AddComponent<PushNotifications>();
            instance.api = go.AddComponent<MainMenuApiClient>();
            instance.api.ConfigureAutomaticRefresh(false);
            instance.api.ConfigureGuestFallback(false);
            DontDestroyOnLoad(go);
            go.SetActive(true);
            return instance;
        }
        void Awake() { if (IsNativeAndroid) callback = new Callback(responses); }
        void Start() { SceneManager.sceneLoaded += OnSceneLoaded; menu = FindAnyObjectByType<MainMenuScene>(); nextRefresh = Time.unscaledTime + 60; RefreshNative(); }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) { menu = FindAnyObjectByType<MainMenuScene>(); nextNavigation = Time.unscaledTime + .5f; }
        void Update()
        {
            while (responses.TryDequeue(out string value)) ApplyRegistration(value);
            if (!IsNativeAndroid) return;
            float now = Time.unscaledTime;
            if (now >= nextRefresh) { nextRefresh = now + 60; RefreshNative(); }
            if (!holdSync && !syncBusy && !string.IsNullOrEmpty(token) && now >= nextSync && StateKey != syncedState)
                StartCoroutine(SyncRegistration());
            if (now >= nextNavigation) { nextNavigation = now + .5f; NavigatePending(); }
        }
        void OnApplicationFocus(bool focus) { if (focus) { nextRefresh = 0; nextNavigation = 0; } }
        void OnApplicationPause(bool paused) { if (!paused) { nextRefresh = 0; nextNavigation = 0; } }
        string StateKey => token + "|" + ApiPermission + "|" + sessionRevision;
        string ApiPermission => Receiving ? "granted" : "denied";
        public void Toggle() { SetEnabled(!Enabled); }
        public void SetEnabled(bool enabled)
        {
            if (!IsNativeAndroid) return;
            Enabled = enabled;
            Status = enabled ? "Allow notifications in Android" : "Notifications off";
            nextSync = 0;
            Native((bridge, activity) => bridge.CallStatic("setEnabled", activity, enabled, callback));
        }
        void RefreshNative()
        {
            if (!IsNativeAndroid) return;
            Native((bridge, activity) => bridge.CallStatic("refresh", activity, callback));
        }
        void ApplyRegistration(string json)
        {
            try
            {
                var value = JObject.Parse(json);
                Supported = (bool?)value["supported"] == true;
                Enabled = (bool?)value["enabled"] == true;
                token = (string)value["token"];
                permission = (string)value["permission"] ?? "unavailable";
                string error = (string)value["error"];
                Status = !Supported ? "Notifications unavailable on this device" : !Enabled ? "Notifications off" :
                    !string.IsNullOrEmpty(error) ? error : permission == "granted" ? "Notifications on" :
                    permission == "prompt" ? "Allow notifications in Android" : "Notifications blocked in Android settings";
                nextSync = 0;
            }
            catch (Exception) { Status = "Could not check notifications. Please retry."; }
        }
        IEnumerator SyncRegistration()
        {
            syncBusy = true;
            try
            {
                string state = StateKey;
                bool ok = false;
                var payload = new JObject { ["token"] = token, ["platform"] = "android", ["permission"] = ApiPermission };
                yield return api.Request("POST", "/api/notifications/devices", payload,
                    (code, body, error) => ok = code >= 200 && code < 300 && (bool?)body?["ok"] == true && string.IsNullOrEmpty(error));
                if (ok && state == StateKey) syncedState = state;
                nextSync = Time.unscaledTime + (ok ? 0 : 30);
            }
            finally { syncBusy = false; }
        }
        internal static void SessionChanged(MainMenuApiClient source)
        {
            if (!IsNativeAndroid || !Application.isPlaying) return;
            var service = Ensure();
            string owner = source.IsLocalGuest ? "" : source.LastPlayer?.id ?? "";
            if (service.sessionOwner == owner && service.api.BaseUrl == source.BaseUrl) return;
            service.api.Configure(source.BaseUrl);
            service.sessionOwner = owner;
            service.sessionRevision++;
            service.nextSync = 0;
        }
        internal static IEnumerator FlushSignOut(MainMenuApiClient source)
        {
            if (!IsNativeAndroid || !instance) yield break;
            SessionChanged(source);
            var service = instance;
            service.holdSync = true;
            try
            {
                // Drain an old authenticated registration before posting the anonymous ownership update.
                while (service && service.syncBusy) yield return null;
                if (service && !string.IsNullOrEmpty(service.token)) yield return service.SyncRegistration();
            }
            finally { if (service) service.holdSync = false; }
        }
        void NavigatePending()
        {
            if (string.IsNullOrEmpty(pending)) Native((bridge, activity) => pending = bridge.CallStatic<string>("peekPending", activity));
            if (string.IsNullOrEmpty(pending)) return;
            if (!menu || !menu.isActiveAndEnabled) return;
            try
            {
                string route = NormalizeRoute((string)JObject.Parse(pending)["route"]);
                switch (route)
                {
                    case "play": menu.StartBattle(); break;
                    case "shop": menu.OpenShop(); break;
                    case "rewards": menu.OpenQuarters(); break;
                    case "social": menu.OpenSocials(); break;
                    default: menu.PlayTab(); break;
                }
            }
            catch (Exception) { /* Bad payloads are discarded without affecting login or the game. */ }
            string acknowledged = pending;
            Native((bridge, activity) => bridge.CallStatic("acknowledgePending", activity, acknowledged));
            pending = null;
        }
        public static string NormalizeRoute(string route)
        {
            switch (route)
            {
                case "play": case "shop": case "rewards": case "social": return route;
                default: return "home";
            }
        }
        void Native(Action<AndroidJavaClass, AndroidJavaObject> action)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(Bridge)) action(bridge, activity);
            }
            catch (Exception) { Supported = false; Status = "Notifications unavailable on this device"; }
#endif
        }
        void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; callback = null; if (instance == this) instance = null; }
        [Preserve]
        sealed class Callback : AndroidJavaProxy
        {
            readonly ConcurrentQueue<string> queue;
            public Callback(ConcurrentQueue<string> queue) : base(Bridge + "$Callback") { this.queue = queue; }
            [Preserve] public void onComplete(string json) { queue.Enqueue(json); }
        }
    }
}
