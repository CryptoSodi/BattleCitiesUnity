using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class SavedSessionChecks
    {
        const string Wallet = "11111111111111111111111111111111";
        const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly Type Storage = typeof(MainMenuApiClient).Assembly.GetType("BattleCities.UI.WalletSessionStorage");
        static int count;
        static void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
        static void RunEnumerator(IEnumerator routine)
        {
            int steps=0;
            while(routine.MoveNext())
            {
                if(++steps>1000)throw new Exception("Fixture unexpectedly waited for external work");
                if(routine.Current is IEnumerator nested)RunEnumerator(nested);
            }
        }
        static JObject Saved() => new JObject { {"origin",MainMenuApiClient.LiveApiBaseUrl},
            {"cookie","battlecity_session=sess-fixture-only"}, {"wallet",Wallet},
            {"expiresAt",DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()} };
        static JObject Session() => new JObject{{"authenticated",true},{"provider","wallet"},{"walletAddress",Wallet}};
        static JObject Player() => new JObject{{"authenticated",true},{"player",new JObject{{"id","fixture"},
            {"provider","wallet"},{"displayName","Fixture commander"},{"walletAddress",Wallet}}}};
        static IEnumerator Reply(long code,JObject body,Action<long,JObject,string> completed) { completed(code,body,null); yield break; }
        public static void Run()
        {
            count=0;
            var go=new GameObject("Saved-session fixtures");go.SetActive(false);
            var api=go.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);
            api.Configure(MainMenuApiClient.LiveApiBaseUrl);go.SetActive(true);
            var prefs=new System.Collections.Generic.Dictionary<string,string>();
            foreach(var name in new[]{"battlecities.loginMode","battlecities.guestId","battlecities.guestName","battlecities.playerName"})
                prefs[name]=PlayerPrefs.GetString(name,null);
            var oldGuest=typeof(MainMenuApiClient).GetProperty("CurrentGuestId").GetValue(null);
            var oldGuestName=typeof(MainMenuApiClient).GetField("currentGuestName",StaticPrivate).GetValue(null);
            string savedJson=null;int clears=0,calls=0,published=0;
            long sessionCode=200,playerCode=200;JObject session=Session(),player=Player();
            var oldCookie=typeof(MainMenuApiClient).GetField("sessionCookie",StaticPrivate).GetValue(null);
            var oldOrigin=typeof(MainMenuApiClient).GetField("sessionCookieOrigin",StaticPrivate).GetValue(null);
            var oldPurchase=typeof(MainMenuApiClient).GetField("purchaseWallet",StaticPrivate).GetValue(null);
            void Reset()
            {
                PlayerPrefs.SetString("battlecities.loginMode","wallet");savedJson=Saved().ToString();
                clears=calls=published=0;sessionCode=playerCode=200;session=Session();player=Player();
                foreach(var name in new[]{"IsAuthenticated","IsWalletAuthenticated","IsLocalGuest"})typeof(MainMenuApiClient).GetProperty(name).SetValue(api,false);
                typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,null);
                typeof(MainMenuApiClient).GetField("purchaseWallet",StaticPrivate).SetValue(null,null);
            }
            bool Restore() { bool? result=null;RunEnumerator(api.RestoreWalletSession(ok=>result=ok));Check(result.HasValue,"restore completes");return result.Value; }
            try
            {
                Storage.GetField("LoadOverride",StaticPrivate).SetValue(null,new Func<Task<string>>(()=>Task.FromResult(savedJson)));
                Storage.GetField("ClearOverride",StaticPrivate).SetValue(null,new Func<Task>(()=>{savedJson=null;clears++;return Task.CompletedTask;}));
                api.EditorRequestOverride=(method,path,payload,reply)=>
                {calls++;return Reply(path=="/api/session"?sessionCode:playerCode,path=="/api/session"?session:player,reply);};
                api.PlayerLoaded+=value=>published++;
                Reset();Check(Restore()&&api.IsWalletAuthenticated&&published==1&&calls==2,"valid session restores server player");
                Check(!api.IsWalletReadyForPurchases,"restored game session requires explicit purchase connection");
                Check(!api.IsWalletLoginPending&&!api.IsRestoringSession,"restore never opens wallet and releases busy state");
                foreach(string invalid in new[]{"missing","malformed","expired","origin","cookie","address"})
                {
                    Reset();var data=Saved();
                    if(invalid=="expired")data["expiresAt"]=DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();
                    if(invalid=="origin")data["origin"]="https://test.battlecities.com";
                    if(invalid=="cookie")data["cookie"]="battlecity_session=sess-test\r\nInjected: true";
                    if(invalid=="address")data["wallet"]="bad-wallet";
                    savedJson=invalid=="missing"?null:invalid=="malformed"?"not-json":data.ToString();
                    Check(!Restore()&&calls==0&&clears==1&&published==0,invalid+" stored session rejected before HTTP");
                }
                foreach(string denied in new[]{"revoked","unauthorized","forbidden","wrong-provider","wrong-session-wallet","wrong-player-wallet","guest-player"})
                {
                    Reset();
                    if(denied=="revoked")session=new JObject{{"authenticated",false}};
                    if(denied=="unauthorized")sessionCode=401;
                    if(denied=="forbidden")sessionCode=403;
                    if(denied=="wrong-provider")session["provider"]="guest";
                    if(denied=="wrong-session-wallet")session["walletAddress"]="22222222222222222222222222222222";
                    if(denied=="wrong-player-wallet")player["player"]["walletAddress"]="22222222222222222222222222222222";
                    if(denied=="guest-player")player["player"]["provider"]="guest";
                    Check(!Restore()&&clears==1&&published==0&&!api.IsWalletAuthenticated,denied+" cannot authenticate");
                }
                foreach(string transient in new[]{"offline","server-error","malformed-session","player-offline","malformed-player"})
                {
                    Reset();
                    if(transient=="offline"){sessionCode=0;session=null;}
                    if(transient=="server-error"){sessionCode=503;session=null;}
                    if(transient=="malformed-session")session=new JObject();
                    if(transient=="player-offline"){playerCode=0;player=null;}
                    if(transient=="malformed-player")player=new JObject();
                    Check(!Restore()&&clears==0&&savedJson!=null&&published==0&&!api.IsWalletAuthenticated,transient+" preserves saved credentials without granting access");
                }
                foreach(string mode in new[]{"signedout","guest"})
                {Reset();PlayerPrefs.SetString("battlecities.loginMode",mode);Check(!Restore()&&calls==0&&clears==0,mode+" suppresses auto sign-in");}
                Reset();Check(Restore(),"prepare logout fixture");
                typeof(MainMenuApiClient).GetField("purchaseWallet",StaticPrivate).SetValue(null,Wallet);
                Check(api.IsWalletReadyForPurchases,"explicit wallet connection enables purchase preparation");
                bool? loggedOut=null;
                api.EditorRequestOverride=(method,path,payload,reply)=>Reply(200,new JObject{{"authenticated",false}},reply);
                var logout=typeof(MainMenuApiClient).GetMethod("SignOutRoutine",BindingFlags.Instance|BindingFlags.NonPublic);
                RunEnumerator((IEnumerator)logout.Invoke(api,new object[]{new Action<bool,string>((ok,error)=>loggedOut=ok)}));
                Check(loggedOut==true&&clears==1&&savedJson==null&&!api.IsWalletReadyForPurchases,"confirmed logout clears stored session and purchase connection");
                Check(!Restore(),"logout does not immediately sign in again");
                Directory.CreateDirectory("Builds/SavedSessionChecks");
                File.WriteAllText("Builds/SavedSessionChecks/result.txt","PASS "+count);
                Debug.Log("SAVED SESSION CHECKS PASSED: "+count);
            }
            finally
            {
                Storage.GetField("LoadOverride",StaticPrivate).SetValue(null,null);Storage.GetField("ClearOverride",StaticPrivate).SetValue(null,null);
                typeof(MainMenuApiClient).GetField("sessionCookie",StaticPrivate).SetValue(null,oldCookie);
                typeof(MainMenuApiClient).GetField("sessionCookieOrigin",StaticPrivate).SetValue(null,oldOrigin);
                typeof(MainMenuApiClient).GetField("purchaseWallet",StaticPrivate).SetValue(null,oldPurchase);
                typeof(MainMenuApiClient).GetProperty("CurrentGuestId").SetValue(null,oldGuest);
                typeof(MainMenuApiClient).GetField("currentGuestName",StaticPrivate).SetValue(null,oldGuestName);
                foreach(var entry in prefs){if(entry.Value==null)PlayerPrefs.DeleteKey(entry.Key);else PlayerPrefs.SetString(entry.Key,entry.Value);}
                PlayerPrefs.Save();UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
