using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    /// <summary>Marshals native MWA results back onto Unity's main thread.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MobileWalletLogin : MonoBehaviour
    {
        private readonly ConcurrentQueue<string> responses = new ConcurrentQueue<string>();
        private Action<string> completed;
        private CancellationTokenSource cancellation;
        public void Connect(string baseUrl, string attemptId, Action<string> onCompleted)
        {
            Cancel();
            completed = onCompleted;
#if UNITY_ANDROID && !UNITY_EDITOR
            cancellation = new CancellationTokenSource();
            _ = ConnectAndroid(baseUrl, attemptId, cancellation.Token);
#else
            throw new PlatformNotSupportedException("Mobile wallet sign-in requires an Android device build.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task ConnectAndroid(string baseUrl, string attemptId, CancellationToken token)
        {
            string stage = "provider_detection";
            Action<string> reportStage = value => { stage = value; Debug.Log("[BattleCitiesWallet] login stage=" + value); };
            try
            {
                reportStage(stage);
                if (!await SeekerGameWallet.Client.IsAvailable(token))
                    throw new WalletAdapterException("NO_WALLET_FOUND", "No compatible wallet is available.");
                // Request a fresh grant; an earlier wallet authorization may have been revoked.
                var result = await SeekerWalletLoginFlow.AuthenticateFresh(SeekerGameWallet.Client,
                    new SeekerLoginHttp(baseUrl), attemptId, token, reportStage);
                if (!token.IsCancellationRequested) responses.Enqueue(result.ToString());
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                string code = exception is WalletAdapterException sdkError ? sdkError.Code : exception.GetType().Name;
                Debug.LogWarning("[BattleCitiesWallet] login failed stage=" + stage + " code=" + code);
                if (code == "INVALID_INPUT" || code == "INVALID_RESPONSE" || code == "ACCOUNT_CHANGED" || code == "AUTHORIZATION_DECLINED")
                {
                    // A subsequent explicit Connect must not be trapped in another wallet's cached grant.
                    // Clear local authorization only; never retry signing automatically.
                    try { await SeekerGameWallet.Client.Disconnect(); }
                    catch { Debug.LogWarning("[BattleCitiesWallet] failed authorization cleanup unavailable"); }
                }
                if (!token.IsCancellationRequested) responses.Enqueue(new JObject { { "ok", false },
                    { "attemptId", attemptId }, { "error", "Wallet sign-in failed at " + stage.Replace('_',' ') + " (" + code + "). Please try again." } }.ToString());
            }
        }
#endif

        private void Update()
        {
            while (responses.TryDequeue(out var json)) completed?.Invoke(json);
        }

        public void Cancel()
        {
            completed = null;
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            while (responses.TryDequeue(out _)) { }
        }

        private void OnDisable() => Cancel();
    }
}
