using System;
using System.Collections.Concurrent;
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
            callback = new CallbackProxy(responses);
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.battlecities.wallet.MobileWalletLogin"))
                operation = bridge.CallStatic<AndroidJavaObject>("connect", activity, baseUrl, psg1, attemptId, callback);
#else
            throw new PlatformNotSupportedException("Mobile wallet sign-in requires an Android device build.");
#endif
        }

        private void Update()
        {
            while (responses.TryDequeue(out var json)) completed?.Invoke(json);
        }

        public void Cancel()
        {
            completed = null;
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
