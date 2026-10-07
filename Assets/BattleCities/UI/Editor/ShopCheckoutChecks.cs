using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    /// <summary>Isolated checkout checks. Never sign or submit a real wallet payment.</summary>
    public static class ShopCheckoutChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Get(object value,string field)=>value.GetType().GetField(field,Private).GetValue(value);
        static void Set(object value,string field,object content)=>value.GetType().GetField(field,Private).SetValue(value,content);
        static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
        static void Drain(IEnumerator value)
        {
            var stack=new Stack<IEnumerator>();stack.Push(value);int ticks=0;
            while(stack.Count>0)
            {
                if(++ticks>100)throw new Exception("Shop fixture did not finish.");
                var step=stack.Peek();if(!step.MoveNext()){(step as IDisposable)?.Dispose();stack.Pop();}
                else if(step.Current is IEnumerator nested)stack.Push(nested);
            }
        }
        static IEnumerator Reply(Action<long,JObject,string> callback,JObject value){yield return null;callback(200,value,null);}
        static JObject Catalog()=>JObject.Parse(@"{
            'items':[{'id':'fuel-one','solPrice':'0.01','skrPrice':'150'}],
            'currency':{'skr':{'decimals':6}},
            'seasonPass':{'enabled':true,'purchaseAvailable':true,'owned':false,
                'season':{'id':'fixture-season','name':'Season 2'},'expiresAt':'2099-01-01T00:00:00Z',
                'scoringPolicy':'all_matches_in_season','prices':{'sol':'0.025000001','skr':'250.125001'}}}");
        [MenuItem("Battle Cities/Shop/Run isolated checkout checks")]
        public static void RunFromMenu()=>Debug.Log(Run());
        public static string Run()
        {
            int checks=0;void Check(bool okay,string message){checks++;if(!okay)throw new Exception("Shop check failed: "+message);}
            var parsed=ShopLiveCatalog.Parse(Catalog());
            Check(parsed.Available("season-pass",ShopCurrency.Skr)&&parsed.Price("season-pass",ShopCurrency.Skr)=="250.125001","exact SKR price");
            Check(parsed.Price("season-pass",ShopCurrency.Solana)=="0.025000001","exact SOL price");
            Check(ShopLiveCatalog.FormatAtomic("250125001",6)=="250.125001"&&ShopLiveCatalog.FormatAtomic("1",9)=="0.000000001","small atomic amounts are exact");
            Check(ShopLiveCatalog.FormatAtomic("18446744073709551615",6)=="18446744073709.551615","large atomic amount has no float rounding");
            foreach(string invalid in new[]{"0","-1","1e3","1,000","0.0000001","01","NaN"})Check(!ShopLiveCatalog.ValidPrice(invalid,6),"reject malformed price "+invalid);
            parsed.Pass["owned"]=true;Check(!parsed.Available("season-pass",ShopCurrency.Solana),"owned pass cannot be purchased again");
            parsed.Pass["owned"]=false;parsed.Pass["enabled"]=false;Check(!parsed.Available("season-pass",ShopCurrency.Skr),"disabled sales unavailable");
            var tokenless=Catalog();tokenless["currency"]["skr"]=null;Check(!ShopLiveCatalog.Parse(tokenless).Available("fuel-one",ShopCurrency.Skr),"missing token configuration disables SKR");
            var dated=JObject.Parse("{ 'expiresAt':'2099-01-01T00:00:00Z' }");
            Check((bool)typeof(ShopScreen).GetMethod("QuoteIsCurrent",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{dated}),"JSON date token preserves quote expiry");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();if(!menu)throw new Exception("Open MainMenu before running Shop checks.");
            menu.OpenShop();var screen=menu.GetComponent<ShopScreen>();var api=menu.GetComponent<MainMenuApiClient>();
            var priorPlayer=api.LastPlayer;bool priorWallet=api.IsWalletAuthenticated;var priorOverride=api.EditorRequestOverride;
            var priorCatalog=Get(screen,"liveCatalog");var priorAccount=Get(screen,"account");var priorBalances=Get(screen,"walletBalances");var priorOwner=Get(screen,"loadedOwner");var priorPending=Get(screen,"pendingPayment");
            string fixtureKey=null,otherKey=null;
            try
            {
                typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,new MainMenuApiClient.PlayerSnapshot{id="checkout-fixture",walletAddress="fixture-wallet",provider="wallet"});
                typeof(MainMenuApiClient).GetProperty("IsWalletAuthenticated").SetValue(api,true);
                api.EditorRequestOverride=(method,path,payload,callback)=>Reply(callback,path=="/api/economy/account"?
                    new JObject{{"authenticated",true},{"account",new JObject{{"fuelBalance",7},{"inventory",new JObject{{"shield",2}}}}}}:
                    path=="/api/economy/catalog"?Catalog():new JObject{{"ok",true},{"walletAddress","fixture-wallet"},{"solBalance","1.25"},{"skrBalance","500.000001"}});
                Drain((IEnumerator)Call(screen,"LoadLiveAccount"));
                Check((string)((JObject)Get(screen,"account"))["fuelBalance"]=="7","inventory loads from authenticated account");
                Check(((TMP_Text)Get(screen,"skrBalance")).text=="500.000001","SKR balance comes from wallet endpoint");
                Check((string)Call(screen,"LivePrice","season-pass")=="250.125001 SKR","card uses live catalog price");
                screen.SetCategory(ShopCategory.SeasonPass,false);Check(screen.VisibleProductCount==1,"Season Pass filter shows one product");
                Call(screen,"InspectCheckout",12);
                Check(((TMP_Text)Get(screen,"noticeBody")).text.Contains("including earlier matches"),"checkout explains full-season retroactive scoring");
                Check(((Button)Get(screen,"noticeConfirm")).gameObject.activeSelf,"available wallet checkout action reachable");
                fixtureKey=(string)typeof(ShopScreen).GetProperty("PendingKey",Private).GetValue(screen);
                var receipt=new JObject{{"wallet","fixture-wallet"},{"api",api.BaseUrl},{"quoteToken","fixture-quote"},{"signature","fixture-signature"},{"signedTransaction","fixture-bytes"}};
                Set(screen,"pendingPayment",receipt);Call(screen,"SavePending");Set(screen,"pendingPayment",null);Call(screen,"LoadPending");
                Check(Get(screen,"pendingPayment")!=null,"receipt survives reopening checkout");
                Call(screen,"ApplyPaymentResult",new JObject{{"ok",true},{"submitted",true}});
                Check(Get(screen,"pendingPayment")!=null,"submission alone cannot grant or clear receipt");
                Call(screen,"CancelCheckout");Check(Get(screen,"pendingPayment")!=null,"closing checkout preserves pending payment");
                typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,new MainMenuApiClient.PlayerSnapshot{id="other-checkout-fixture",walletAddress="other-wallet",provider="wallet"});
                otherKey=(string)typeof(ShopScreen).GetProperty("PendingKey",Private).GetValue(screen);Call(screen,"LoadPending");
                Check(Get(screen,"pendingPayment")==null&&otherKey!=fixtureKey,"another account cannot retry someone else's receipt");
                typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,new MainMenuApiClient.PlayerSnapshot{id="checkout-fixture",walletAddress="fixture-wallet",provider="wallet"});Call(screen,"LoadPending");
                Call(screen,"ApplyPaymentResult",new JObject{{"ok",true},{"account",new JObject{{"fuelBalance",8},{"inventory",new JObject()}}}});
                Check(Get(screen,"pendingPayment")==null&&!PlayerPrefs.HasKey(fixtureKey),"verified account clears receipt");
                Set(screen,"pendingPayment",receipt);Call(screen,"SavePending");Call(screen,"ApplyPaymentResult",new JObject{{"ok",false},{"terminal",true}});
                Check(Get(screen,"pendingPayment")==null&&!PlayerPrefs.HasKey(fixtureKey),"final unpaid expiry clears receipt");
            }
            finally
            {
                if(fixtureKey!=null)PlayerPrefs.DeleteKey(fixtureKey);if(otherKey!=null)PlayerPrefs.DeleteKey(otherKey);
                api.EditorRequestOverride=priorOverride;typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,priorPlayer);
                typeof(MainMenuApiClient).GetProperty("IsWalletAuthenticated").SetValue(api,priorWallet);
                Call(screen,"CancelCheckout");Set(screen,"liveCatalog",priorCatalog);Set(screen,"account",priorAccount);Set(screen,"walletBalances",priorBalances);Set(screen,"loadedOwner",priorOwner);Set(screen,"pendingPayment",priorPending);
                screen.Back();screen.SetCategory(ShopCategory.All,false);Call(screen,"RefreshAccount");
            }
            return "Shop checkout checks: "+checks+" passed.";
        }
        public static void Preview()
        {
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.OpenShop();var screen=menu.GetComponent<ShopScreen>();
            Set(screen,"liveCatalog",ShopLiveCatalog.Parse(Catalog()));Call(screen,"RefreshAccount");screen.SetCategory(ShopCategory.SeasonPass,false);
        }
        public static void Capture()
        {
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            var cameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var states=new Dictionary<Camera,bool>();
            foreach(var camera in cameras){states[camera]=camera.enabled;if(camera.gameObject.scene!=menu.gameObject.scene)camera.enabled=false;}
            try
            {
                Preview();
                foreach(var layout in new[]{(MainMenuPlatform.Web,1600,1067,"web"),(MainMenuPlatform.Psg1,1240,1080,"psg1"),(MainMenuPlatform.AndroidLandscape,844,390,"android")})
                    MainMenuChecks.Capture(layout.Item1,layout.Item2,layout.Item3,"season-pass-"+layout.Item4);
                Call(menu.GetComponent<ShopScreen>(),"InspectCheckout",12);
                MainMenuChecks.Capture(MainMenuPlatform.AndroidLandscape,844,390,"season-pass-checkout-android");
                menu.GetComponent<ShopScreen>().Back();menu.GetComponent<ShopScreen>().Close(false);
            }
            finally{foreach(var entry in states)if(entry.Key)entry.Key.enabled=entry.Value;}
        }
    }
}
