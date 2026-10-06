using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.UI.Editor
{
    /// <summary>Isolated registration fixtures: never register fake tokens with the live server.</summary>
    public static class FirebaseNotificationChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception("Firebase check failed: " + message); }
        static void Field(PushNotifications push, string name, object value) => typeof(PushNotifications).GetField(name, Private).SetValue(push, value);
        static object Field(PushNotifications push, string name) => typeof(PushNotifications).GetField(name, Private).GetValue(push);
        static void Registration(PushNotifications push, bool enabled, string permission, string token = "fixture-notification-token-123456789")
        {
            typeof(PushNotifications).GetMethod("ApplyRegistration", Private).Invoke(push, new object[] { new JObject {
                ["supported"] = true, ["enabled"] = enabled, ["permission"] = permission, ["token"] = token }.ToString() });
        }
        static void Sync(PushNotifications push)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push((IEnumerator)typeof(PushNotifications).GetMethod("SyncRegistration", Private).Invoke(push, null));
            int ticks = 0;
            while (stack.Count > 0)
            {
                if (++ticks > 100) throw new Exception("Registration fixture did not finish.");
                var step = stack.Peek();
                if (!step.MoveNext()) { (step as IDisposable)?.Dispose(); stack.Pop(); }
                else if (step.Current is IEnumerator nested) stack.Push(nested);
            }
        }
        static IEnumerator Reply(Action<long,JObject,string> callback, bool okay, Action beforeReply = null)
        {
            yield return null;
            beforeReply?.Invoke();
            callback(okay ? 201 : 503, new JObject { ["ok"] = okay }, okay ? null : "Fixture offline");
        }
        [MenuItem("Battle Cities/Notifications/Run isolated Firebase checks")]
        public static void RunFromMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            checks = 0;
            FirebaseAndroidConfiguration.Generate();
            var config = JObject.Parse(File.ReadAllText("Assets/BattleCities/Notifications/google-services.json"));
            Check((string)config["client"]?[0]?["client_info"]?["android_client_info"]?["package_name"] == "com.battlecities.solanaconsole", "real client matches the requested package");
            foreach (string route in new[] {"home", "play", "shop", "rewards", "social"}) Check(PushNotifications.NormalizeRoute(route) == route, "supported route " + route);
            foreach (string route in new[] {null, "", "https://example.com", "external", "share", "buy", "claim"}) Check(PushNotifications.NormalizeRoute(route) == "home", "unsupported routes cannot start external or financial actions");
            var scene = EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Firebase isolated fixture") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go, scene);
            try
            {
                var api = go.AddComponent<MainMenuApiClient>(); api.ConfigureAutomaticRefresh(false); api.ConfigureGuestFallback(false);
                var push = go.AddComponent<PushNotifications>(); Field(push, "api", api);
                JObject request = null; string requestPath = null, methodName = null;
                api.EditorRequestOverride = (method,path,payload,reply) => { request = payload; requestPath = path; methodName = method; return Reply(reply, true); };
                Registration(push, false, "granted"); Sync(push);
                Check((string)request["permission"] == "denied" && !push.Receiving, "local opt-out overrides Android permission");
                Check(requestPath == "/api/notifications/devices" && methodName == "POST" && (string)request["platform"] == "android", "existing registration API contract");
                Check(request["playerId"] == null && request["walletAddress"] == null, "ownership uses the authenticated session rather than a supplied player identifier");
                Registration(push, true, "granted"); Sync(push);
                Check((string)request["permission"] == "granted" && push.Receiving, "enabled and granted device is registered");
                Check(Field(push, "syncedState") != null && !(bool)Field(push, "syncBusy"), "successful request acknowledged and released");
                Registration(push, true, "denied"); Sync(push);
                Check((string)request["permission"] == "denied" && !push.Receiving, "OS revocation disables server delivery");
                Registration(push, true, "prompt"); Sync(push);
                Check((string)request["permission"] == "denied", "unanswered permission is not registered as granted");
                Registration(push, true, "granted", "fixture-rotated-notification-token-123456789"); Sync(push);
                Check((string)request["token"] == "fixture-rotated-notification-token-123456789", "rotated token replaces the cached registration");
                Field(push, "syncedState", null);
                api.EditorRequestOverride = (method,path,payload,reply) => Reply(reply, false);
                Sync(push);
                Check(Field(push, "syncedState") == null && !(bool)Field(push, "syncBusy"), "offline response remains retryable");
                Check((float)Field(push, "nextSync") >= Time.unscaledTime + 29, "offline retries are throttled");
                api.EditorRequestOverride = (method,path,payload,reply) => Reply(reply, true, () => Field(push, "sessionRevision", 1));
                Sync(push);
                Check(Field(push, "syncedState") == null, "old-account response cannot acknowledge a new session");
                api.EditorRequestOverride = (method,path,payload,reply) => Reply(reply, true); Sync(push);
                Check(Field(push, "syncedState") != null, "changed session resynchronizes successfully");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); EditorSceneManager.ClosePreviewScene(scene); }
            return "Firebase notification checks: " + checks + " passed.";
        }
    }
}
