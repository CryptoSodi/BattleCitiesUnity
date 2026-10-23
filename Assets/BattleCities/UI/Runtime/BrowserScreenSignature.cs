using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
namespace BattleCities.UI
{
    internal static class BrowserScreenSignature
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        delegate void Completed(int request,int valid);
        static readonly Completed callback=Finish;
        static readonly Dictionary<int,TaskCompletionSource<bool>> requests=new Dictionary<int,TaskCompletionSource<bool>>();
        static int next;
        [DllImport("__Internal")] static extern void BattleCitiesVerifyScreenSignature(int id,string modulus,string exponent,string payload,string signature,Completed done);
        internal static async Task<bool> Verify(string modulus,string exponent,string payload,string signature)
        {
            int id=++next;var completion=new TaskCompletionSource<bool>();requests[id]=completion;
            try
            {
                BattleCitiesVerifyScreenSignature(id,modulus,exponent,payload,signature,callback);
                double deadline=UnityEngine.Time.realtimeSinceStartupAsDouble+8;
                while(!completion.Task.IsCompleted&&UnityEngine.Time.realtimeSinceStartupAsDouble<deadline)await Task.Yield();
                return completion.Task.IsCompleted&&await completion.Task;
            }
            finally{requests.Remove(id);}
        }
        [AOT.MonoPInvokeCallback(typeof(Completed))]
        static void Finish(int id,int valid){if(requests.TryGetValue(id,out var request))request.TrySetResult(valid==1);}
#endif
    }
}
