using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace BattleCities.UI
{
    [DisallowMultipleComponent]
    public sealed class CheckoutWalletSigner : MonoBehaviour
    {
        readonly ConcurrentQueue<string> responses=new ConcurrentQueue<string>();
        string attempt;
        byte[] message;
        Action<JObject> completed;
        double deadline;
        public bool IsPending=>attempt!=null;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BattleCitiesSignCheckout(string target,string callback,string attempt,string transaction,string wallet,string expiry,string scriptUrl);
        [DllImport("__Internal")] static extern void BattleCitiesCancelCheckout(string attempt);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject operation;
        Callback callback;
        [Preserve] sealed class Callback : AndroidJavaProxy
        {
            readonly ConcurrentQueue<string> queue;
            public Callback(ConcurrentQueue<string> value):base("com.battlecities.wallet.MobileWalletCheckout$Callback"){queue=value;}
            [Preserve] public void onComplete(string json){queue.Enqueue(json);}
        }
#endif
        public void Sign(string transaction,string wallet,string expiry,Action<JObject> onCompleted)
        {
            Cancel();completed=onCompleted;attempt=Guid.NewGuid().ToString("N");deadline=Time.realtimeSinceStartupAsDouble+150;
            try
            {
                var bytes=Convert.FromBase64String(transaction);
                if(bytes.Length<=65||bytes.Length>1232||bytes[0]!=1||string.IsNullOrEmpty(wallet)
                    ||!DateTimeOffset.TryParse(expiry,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var end)||end<=DateTimeOffset.UtcNow)
                    throw new FormatException();
                message=new byte[bytes.Length-65];Array.Copy(bytes,65,message,0,message.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
                BattleCitiesSignCheckout(gameObject.name,nameof(OnWalletSigned),attempt,transaction,wallet,expiry,
                    Application.streamingAssetsPath.TrimEnd('/')+"/BattleCitiesCheckout/wallet.js");
#elif UNITY_ANDROID && !UNITY_EDITOR
                callback=new Callback(responses);
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var bridge=new AndroidJavaClass("com.battlecities.wallet.MobileWalletCheckout"))
                    operation=bridge.CallStatic<AndroidJavaObject>("sign",activity,transaction,wallet,expiry,RuntimePlatformInfo.IsPsg1,attempt,callback);
#else
                Finish(new JObject{{"ok",false},{"error","Wallet checkout runs in the web build or on an Android device."}});
#endif
            }
            catch {Finish(new JObject{{"ok",false},{"error","Could not open wallet checkout. Refresh the price and try again."}});}
        }
        [Preserve] public void OnWalletSigned(string json){responses.Enqueue(json);}
        void Update()
        {
            while(responses.TryDequeue(out var json))
            {
                JObject value;
                try
                {
                    value=JObject.Parse(json);
                    if((string)value["attemptId"]!=attempt)continue;
                    if((bool?)value["ok"]==true)
                    {
                        var bytes=Convert.FromBase64String((string)value["signedTransaction"]??"");
                        if(bytes.Length!=message.Length+65||bytes[0]!=1)throw new FormatException();
                        for(int i=0;i<message.Length;i++)if(bytes[i+65]!=message[i])throw new FormatException();
                        string signature=Base58(bytes,1,64);
                        bool nonzero=false;for(int i=1;i<65;i++)nonzero|=bytes[i]!=0;
                        if(!nonzero)throw new FormatException();value["signature"]=signature;
                    }
                }
                catch {value=new JObject{{"ok",false},{"error","The wallet returned an invalid signed transaction."}};}
                Finish(value);
            }
            if(IsPending&&Time.realtimeSinceStartupAsDouble>=deadline)Finish(new JObject{{"ok",false},{"error","Wallet signing timed out. No payment was submitted."}});
        }
        void Finish(JObject value){var callbackAction=completed;Cancel();callbackAction?.Invoke(value);}
        public void Cancel()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(attempt!=null)BattleCitiesCancelCheckout(attempt);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if(operation!=null){try{operation.Call("cancel");}catch(AndroidJavaException){}operation.Dispose();operation=null;}callback=null;
#endif
            attempt=null;completed=null;message=null;while(responses.TryDequeue(out _)){}
        }
        internal static string Base58(byte[] bytes,int offset,int count)
        {
            const string alphabet="123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            var input=new byte[count];Array.Copy(bytes,offset,input,0,count);
            int zeros=0;while(zeros<count&&input[zeros]==0)zeros++;
            var chars=new char[count*2];int at=chars.Length,start=zeros;
            while(start<count)
            {
                int remainder=0;
                for(int i=start;i<count;i++){int value=remainder*256+input[i];input[i]=(byte)(value/58);remainder=value%58;}
                chars[--at]=alphabet[remainder];while(start<count&&input[start]==0)start++;
            }
            while(zeros-->0)chars[--at]='1';return new string(chars,at,chars.Length-at);
        }
        void OnDisable()=>Cancel();
    }
}
