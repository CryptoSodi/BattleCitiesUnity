using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace BattleCities.UI
{
    [Serializable]
    internal sealed class SavedWalletSession
    {
        public string origin, cookie, wallet;
        public long expiresAt;
        internal bool IsValid(string expectedOrigin) => origin == expectedOrigin
            && origin == MainMenuApiClient.LiveApiBaseUrl
            && expiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            && Regex.IsMatch(cookie ?? "", @"\Abattlecity_session=sess-[a-zA-Z0-9-]{1,200}\z")
            && Regex.IsMatch(wallet ?? "", @"\A[1-9A-HJ-NP-Za-km-z]{32,44}\z");
    }

    internal static class WalletSessionStorage
    {
#if UNITY_EDITOR
        internal static Func<Task<string>> LoadOverride;
        internal static Func<string, Task<bool>> SaveOverride;
        internal static Func<Task> ClearOverride;
#endif
        internal static async Task<SavedWalletSession> Load()
        {
            string json;
#if UNITY_EDITOR
            json = LoadOverride == null ? null : await LoadOverride();
#elif UNITY_ANDROID
            json = await Native("load", null);
#else
            json = null;
#endif
            try { return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<SavedWalletSession>(json); }
            catch (JsonException) { return null; }
        }
        internal static async Task<bool> Save(SavedWalletSession session)
        {
            string json = JsonConvert.SerializeObject(session);
#if UNITY_EDITOR
            return SaveOverride != null && await SaveOverride(json);
#elif UNITY_ANDROID
            return await Native("save", json) == "true";
#else
            await Task.CompletedTask; return true;
#endif
        }
        internal static async Task Clear()
        {
#if UNITY_EDITOR
            if (ClearOverride != null) await ClearOverride();
#elif UNITY_ANDROID
            await Native("clear", null);
#else
            await Task.CompletedTask;
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        // KeyStore and disk access run off the Unity frame thread.
        private static Task<string> Native(string method, string value) => Task.Run(() =>
        {
            AndroidJNI.AttachCurrentThread();
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var store = new AndroidJavaClass("com.battlecities.wallet.WalletSessionStore"))
                {
                    if (method == "save") return store.CallStatic<bool>(method, activity, value) ? "true" : "false";
                    if (method == "load") return store.CallStatic<string>(method, activity);
                    store.CallStatic(method, activity); return "";
                }
            }
            catch (Exception) { return method == "save" ? "false" : ""; }
            finally { AndroidJNI.DetachCurrentThread(); }
        });
#endif
    }
}
