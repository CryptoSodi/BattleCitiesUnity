using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuApiClient
    {
        private static string purchaseWallet;
        public bool IsRestoringSession { get; private set; }
        public bool IsWalletReadyForPurchases => IsWalletAuthenticated && LastPlayer != null
            && purchaseWallet == LastPlayer.walletAddress;

        public IEnumerator RestoreWalletSession(Action<bool> completed)
        {
            if (IsRestoringSession || IsWalletLoginPending || IsSigningOut) { completed?.Invoke(false); yield break; }
            string mode = PlayerPrefs.GetString("battlecities.loginMode");
            if (mode == "signedout" || mode == "guest") { completed?.Invoke(false); yield break; }
            IsRestoringSession = true;
            try
            {
                NotifyStatus("RESTORING SESSION", "Signing you back in...");
                SavedWalletSession saved = null;
#if UNITY_ANDROID || UNITY_EDITOR
                var read = WalletSessionStorage.Load();
                while (!read.IsCompleted) yield return null;
                if (!read.IsFaulted && !read.IsCanceled) saved = read.Result;
                if (saved == null || !saved.IsValid(ApiOrigin))
                {
                    yield return ForgetSavedSession();
                    completed?.Invoke(false); yield break;
                }
                sessionCookie = saved.cookie; sessionCookieOrigin = saved.origin;
#elif !UNITY_WEBGL
                completed?.Invoke(false); yield break;
#endif
                long status = 0; JObject session = null;
                yield return Request("GET", "/api/session", null, (code, body, error) => { status = code; session = body; });
                if (SessionRejected(status, session))
                {
                    yield return ForgetSavedSession();
                    NotifyStatus("WALLET SESSION EXPIRED", "Please connect your wallet again.");
                    completed?.Invoke(false); yield break;
                }
                if (status >= 200 && status < 300 && (bool?)session?["authenticated"] == true
                    && ((string)session["provider"] != "wallet" || (saved != null && (string)session["walletAddress"] != saved.wallet)))
                {
                    yield return ForgetSavedSession();
                    NotifyStatus("WALLET SESSION EXPIRED", "Please connect your wallet again.");
                    completed?.Invoke(false); yield break;
                }
                if (status < 200 || status >= 300 || (bool?)session?["authenticated"] != true || (string)session?["provider"] != "wallet")
                {
                    NotifyStatus("SESSION RESTORE UNAVAILABLE", "Could not restore sign-in. Check your connection and try again.");
                    completed?.Invoke(false); yield break;
                }
                JObject playerBody = null;
                yield return Request("GET", "/api/player", null, (code, body, error) => { status = code; playerBody = body; });
                var player = ParsePlayer(playerBody?["player"] as JObject);
                if (SessionRejected(status, playerBody) || (status >= 200 && status < 300 && player != null
                    && (player.provider != "wallet" || (saved != null && player.walletAddress != saved.wallet))))
                {
                    yield return ForgetSavedSession();
                    NotifyStatus("WALLET SESSION EXPIRED", "Please connect your wallet again.");
                    completed?.Invoke(false); yield break;
                }
                if (status < 200 || status >= 300 || (bool?)playerBody?["authenticated"] != true || player == null
                    || player.provider != "wallet" || string.IsNullOrWhiteSpace(player.walletAddress))
                {
                    NotifyStatus("SESSION RESTORE UNAVAILABLE", "Could not load your player. Check your connection and try again.");
                    completed?.Invoke(false); yield break;
                }
                IsAuthenticated = true; IsWalletAuthenticated = true; IsLocalGuest = false;
                PlayerPrefs.SetString("battlecities.loginMode", "wallet"); PlayerPrefs.Save();
                NotifyStatus("SIGNED IN", ShortWallet(player.walletAddress));
                PublishPlayer(player);
                completed?.Invoke(true);
            }
            finally { IsRestoringSession = false; }
        }

        private static bool SessionRejected(long status, JObject body) => status == 401 || status == 403
            || (status >= 200 && status < 300 && body?["authenticated"]?.Type == JTokenType.Boolean
                && (bool)body["authenticated"] == false);

        private IEnumerator ForgetSavedSession()
        {
            sessionCookie = null; sessionCookieOrigin = null; purchaseWallet = null;
            IsAuthenticated = false; IsWalletAuthenticated = false; LastPlayer = null;
            var clear = WalletSessionStorage.Clear();
            while (!clear.IsCompleted) yield return null;
        }
    }
}
