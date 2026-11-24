using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Community.SeekerConnect;
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
        byte[] quotedTransaction;
        Action<JObject> completed;
        Action walletSubmissionStarting;
        double deadline;
        CancellationTokenSource cancellation;
        public bool IsPending=>attempt!=null;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BattleCitiesSignCheckout(string target,string callback,string attempt,string transaction,string wallet,string expiry,string scriptUrl,string label);
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
        public void Sign(string transaction,string wallet,string expiry,Action<JObject> onCompleted,string label="Buy game item",Action onWalletSubmissionStarting=null)
        {
            Cancel();completed=onCompleted;walletSubmissionStarting=onWalletSubmissionStarting;attempt=Guid.NewGuid().ToString("N");deadline=Time.realtimeSinceStartupAsDouble+150;
            try
            {
                var bytes=Convert.FromBase64String(transaction);
                if(bytes.Length<=65||bytes.Length>1232||bytes[0]!=1||string.IsNullOrEmpty(wallet)
                    ||!DateTimeOffset.TryParse(expiry,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var end)||end<=DateTimeOffset.UtcNow)
                    throw new FormatException();
                quotedTransaction=(byte[])bytes.Clone();
#if UNITY_WEBGL && !UNITY_EDITOR
                BattleCitiesSignCheckout(gameObject.name,nameof(OnWalletSigned),attempt,transaction,wallet,expiry,
                    Application.streamingAssetsPath.TrimEnd('/')+"/BattleCitiesCheckout/wallet.js",label);
#elif UNITY_ANDROID && !UNITY_EDITOR
                cancellation=new CancellationTokenSource();
                _=SignAndroid(transaction,bytes,wallet,expiry,attempt,cancellation.Token);
#else
                Finish(new JObject{{"ok",false},{"error","Wallet checkout runs in the web build or on an Android device."}});
#endif
            }
            catch {Finish(new JObject{{"ok",false},{"error","Could not open wallet checkout. Refresh the price and try again."}});}
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        async Task SignAndroid(string transaction,byte[] bytes,string wallet,string expiry,string attemptId,CancellationToken token)
        {
            try
            {
                if(!RuntimePlatformInfo.IsPsg1 && await SeekerGameWallet.Client.IsAvailable(token))
                {
                    var signed=await SeekerGameWallet.SendQuotedTransaction(bytes,wallet,token,
                        DateTimeOffset.Parse(expiry,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal),
                        ()=>{if(walletSubmissionStarting==null)throw new InvalidOperationException("Payment recovery is unavailable.");walletSubmissionStarting();});
                    if(!token.IsCancellationRequested) responses.Enqueue(new JObject{{"ok",true},{"attemptId",attemptId},
                        {"walletSubmitted",true},{"signature",Base58(signed.Signature,0,signed.Signature.Length)}}.ToString());
                    return;
                }
                token.ThrowIfCancellationRequested();
                callback=new Callback(responses);
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var bridge=new AndroidJavaClass("com.battlecities.wallet.MobileWalletCheckout"))
                    operation=bridge.CallStatic<AndroidJavaObject>("sign",activity,transaction,wallet,expiry,RuntimePlatformInfo.IsPsg1,attemptId,callback,MainMenuApiClient.WalletChain);
            }
            catch(OperationCanceledException) { }
            catch(Exception error)
            {
                bool definitelyNotSubmitted=SeekerGameWallet.DefinitelyNotSubmitted(error);
                bool reconnect=SeekerGameWallet.NeedsReconnect(error);
                await SeekerGameWallet.ClearRejectedAuthorization(error);
                if(!token.IsCancellationRequested) responses.Enqueue(new JObject{{"ok",false},{"attemptId",attemptId},
                    {"definitelyNotSubmitted",definitelyNotSubmitted},{"errorCode",reconnect?"wallet-not-ready":"wallet-error"},
                    {"error",DescribeSigningFailure(error)}}.ToString());
            }
        }
#endif
        internal static string DescribeSigningFailure(Exception error)
        {
            if(error is SeekerCheckoutException checkout)
            {
                switch(checkout.Reason)
                {
                    case "account-mismatch": return "The selected wallet does not match your Battle Cities account. Reconnect the wallet used to sign in.";
                    case "quote-expired": return "The price expired while the wallet was open. Request a fresh price and try again.";
                    case "transaction-changed": return "The wallet returned a transaction that differs from the purchase quote. No payment was submitted."
                        + (checkout.Detail != null ? " (" + checkout.Detail + ")" : "");
                }
            }
            if(error is SeekerConnectException sdk)
            {
                string code=sdk.Code;
                switch(code)
                {
                    case "NO_WALLET_FOUND": return "No compatible wallet is available. Install or open a wallet, then connect it.";
                    case "NOT_CONNECTED": case "AUTHORIZATION_DECLINED": return "Your wallet connection needs approval again. Reconnect the wallet used to sign in.";
                    case "UNSUPPORTED_OPERATION": return "This wallet cannot submit this purchase. Connect a wallet that supports sign and send.";
                    case "REQUEST_DECLINED": return "The wallet did not complete the request. Check payment status before trying again.";
                    case "CANCELLED": case "SESSION_CLOSED": case "TIMEOUT": case "ASSOCIATION_FAILED":
                        return "The wallet session ended before payment status was confirmed. Check payment status before trying again.";
                }
                if(!string.IsNullOrEmpty(code)&&code.Length<=40)
                {
                    bool safe=true;
                    foreach(char c in code)if(!((c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='_')){safe=false;break;}
                    if(safe)return "Wallet request did not complete ("+code+"). Check payment status before trying again.";
                }
            }
            return "The wallet request did not complete. Check payment status before trying again.";
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
                        if((bool?)value["walletSubmitted"]==true)
                        {
                            string submittedSignature=(string)value["signature"];
                            if(string.IsNullOrEmpty(submittedSignature)||submittedSignature.Length<80||submittedSignature.Length>90)throw new FormatException();
                            Finish(value);continue;
                        }
                        var bytes=Convert.FromBase64String((string)value["signedTransaction"]??"");
                        if(bytes.Length<=65||bytes.Length>1232||bytes[0]!=1)throw new FormatException();
#if !UNITY_WEBGL || UNITY_EDITOR
                        if(!ApprovedCheckoutTransaction.Matches(quotedTransaction,bytes,out var difference))
                            throw new SeekerCheckoutException("transaction-changed",difference);
#endif
                        string signature=Base58(bytes,1,64);
                        bool nonzero=false;for(int i=1;i<65;i++)nonzero|=bytes[i]!=0;
                        if(!nonzero)throw new FormatException();value["signature"]=signature;
                    }
                }
                catch(SeekerCheckoutException error) {value=new JObject{{"ok",false},{"error",DescribeSigningFailure(error)}};}
                catch {value=new JObject{{"ok",false},{"error","The wallet returned an invalid signed transaction."}};}
                Finish(value);
            }
            if(IsPending&&Time.realtimeSinceStartupAsDouble>=deadline)Finish(new JObject{{"ok",false},{"error","The wallet request timed out. Check payment status before trying again."}});
        }
        void Finish(JObject value){var callbackAction=completed;Cancel();callbackAction?.Invoke(value);}
        public void Cancel()
        {
            cancellation?.Cancel();cancellation?.Dispose();cancellation=null;
#if UNITY_WEBGL && !UNITY_EDITOR
            if(attempt!=null)BattleCitiesCancelCheckout(attempt);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if(operation!=null){try{operation.Call("cancel");}catch(AndroidJavaException){}operation.Dispose();operation=null;}callback=null;
#endif
            attempt=null;completed=null;walletSubmissionStarting=null;quotedTransaction=null;while(responses.TryDequeue(out _)){}
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
