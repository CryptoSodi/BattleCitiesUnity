using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

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
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject operation;
        private CallbackProxy callback;

        [Preserve]
        private sealed class CallbackProxy : AndroidJavaProxy
        {
            private readonly ConcurrentQueue<string> responses;
            public CallbackProxy(ConcurrentQueue<string> queue)
                : base("com.battlecities.wallet.MobileWalletLogin$Callback") { responses = queue; }
            [Preserve]
            public void onComplete(string json) { responses.Enqueue(json); }
        }
#endif
        public void Connect(string baseUrl, bool psg1, string attemptId, Action<string> onCompleted)
        {
            Cancel();
            completed = onCompleted;
#if UNITY_ANDROID && !UNITY_EDITOR
            cancellation = new CancellationTokenSource();
            _ = ConnectAndroid(baseUrl, psg1, attemptId, cancellation.Token);
#else
            throw new PlatformNotSupportedException("Mobile wallet sign-in requires an Android device build.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task ConnectAndroid(string baseUrl, bool psg1, string attemptId, CancellationToken token)
        {
            string stage = "provider_detection";
            Action<string> reportStage = value => { stage = value; Debug.Log("[BattleCitiesWallet] login stage=" + value); };
            try
            {
                reportStage(stage);
                if (!psg1 && await SeekerGameWallet.Client.IsAvailable(token))
                {
                    var result = await SeekerWalletLoginFlow.Authenticate(SeekerGameWallet.Client,
                        new SeekerLoginHttp(baseUrl), attemptId, token, reportStage);
                    if (!token.IsCancellationRequested) responses.Enqueue(result.ToString());
                    return;
                }
                token.ThrowIfCancellationRequested();
                reportStage("legacy_mwa"); ConnectLegacy(baseUrl, psg1, attemptId);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                string code = exception is Community.SeekerConnect.SeekerConnectException sdkError ? sdkError.Code : exception.GetType().Name;
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
        private void ConnectLegacy(string baseUrl, bool psg1, string attemptId)
        {
            callback = new CallbackProxy(responses);
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.battlecities.wallet.MobileWalletLogin"))
                operation = bridge.CallStatic<AndroidJavaObject>("connect", activity, baseUrl, psg1, attemptId, callback);
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
#if UNITY_ANDROID && !UNITY_EDITOR
            if (operation != null)
            {
                try { operation.Call("cancel"); }
                catch (AndroidJavaException) { /* Native operation may already be closed. */ }
                operation.Dispose();
                operation = null;
            }
            callback = null;
#endif
            while (responses.TryDequeue(out _)) { }
        }

        private void OnDisable() => Cancel();
    }
}
