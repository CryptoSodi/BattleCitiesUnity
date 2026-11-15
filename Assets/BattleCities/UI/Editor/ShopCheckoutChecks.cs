using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
            'items':[{'id':'fuel-one','solPrice':'0.01','skrPrice':'150','fuel':1,'inventory':{}},
                     {'id':'starter-pack','solPrice':'0.08','skrPrice':'1200','fuel':5,'inventory':{'shield':1,'base-defence':1}}],
            'currency':{'skr':{'decimals':6}},
            'seasonPass':{'enabled':true,'purchaseAvailable':true,'owned':false,
                'season':{'id':'fixture-season','name':'Season 2'},'expiresAt':'2099-01-01T00:00:00Z',
                'scoringPolicy':'all_matches_in_season','prices':{'sol':'0.025000001','skr':'250.125001'}}}");
        [MenuItem("Battle Cities/Shop/Run isolated checkout checks")]
        public static void RunFromMenu()=>Debug.Log(Run());
        public static int RunPurchaseConfirmationChecks()
        {
            int checks=0;void Check(bool okay,string message){checks++;if(!okay)throw new Exception("Shop check failed: "+message);}
            var parsed=ShopLiveCatalog.Parse(Catalog());
            var confirmation=typeof(ShopScreen).GetMethod("PurchaseConfirmation",BindingFlags.Static|BindingFlags.NonPublic);
            string Confirm(JObject receipt)=>(string)confirmation.Invoke(null,new object[]{receipt});
            var pack=new JObject{{"itemId","starter-pack"},{"contents",parsed.PurchaseContents("starter-pack")}};
            Check(Confirm(pack)=="PURCHASE CONFIRMED\nSTARTER PACK\n5 FUEL\n1 SHIELD\n1 BASE DEFENCE","pack confirmation lists each API catalog item");
            Check(Confirm(JObject.Parse(pack.ToString()))==Confirm(pack),"pending receipt keeps pack contents after serialization");
            Check(Confirm(new JObject{{"itemId","fuel-one"},{"contents",parsed.PurchaseContents("fuel-one")}})=="PURCHASE CONFIRMED\n1 FUEL","fuel confirmation shows quantity");
            Check(Confirm(new JObject{{"itemId","shield"},{"contents",JObject.Parse("{'fuel':0,'inventory':{'shield':1}}")}})=="PURCHASE CONFIRMED\n1 SHIELD","power-up confirmation shows quantity and item");
            Check(Confirm(new JObject{{"itemId","season-pass"}})=="PURCHASE CONFIRMED\n1 SEASON PASS","season pass confirmation identifies pass");
            Check(Confirm(new JObject{{"itemId","shield"}})=="PURCHASE CONFIRMED\nSHIELD","older pending receipt shows its purchased item");
            Check(Confirm(new JObject()).Contains("Your purchase was added"),"missing receipt does not invent items");
            return checks;
        }
        public static string Run()
        {
            int checks=RunPurchaseConfirmationChecks();void Check(bool okay,string message){checks++;if(!okay)throw new Exception("Shop check failed: "+message);}
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
                Check(((TMP_Text)Get(screen,"skrBalance")).text=="500","SKR balance comes from wallet endpoint and displays whole tokens");
                Check((string)Call(screen,"LivePrice","season-pass")=="250 SKR","card uses rounded live catalog price");
                screen.SetCategory(ShopCategory.SeasonPass,false);Check(screen.VisibleProductCount==1,"Season Pass filter shows one product");
                Call(screen,"InspectCheckout",12);
                Check(((TMP_Text)Get(screen,"noticeBody")).text.Contains("including earlier matches"),"checkout explains full-season retroactive scoring");
                Check(((Button)Get(screen,"noticeConfirm")).gameObject.activeSelf,"available wallet checkout action reachable");
                var transaction=new byte[66];transaction[0]=1;int quoteCalls=0,otherCalls=0;
                api.EditorRequestOverride=(method,path,payload,callback)=>
                {
                    if(path!="/api/economy/purchase/quote"){otherCalls++;return Reply(callback,new JObject());}
                    quoteCalls++;
                    return Reply(callback,new JObject{{"ok",true},{"amountAtomic","25000001"},{"currency","sol"},
                        {"network",MainMenuApiClient.WalletNetwork},{"transaction",Convert.ToBase64String(transaction)},
                        {"quoteToken","fixture-quote"},{"seasonId","fixture-season"},{"expiresAt","2099-01-01T00:00:00Z"}});
                };
                screen.SetCurrency(ShopCurrency.Solana,false);
                Drain((IEnumerator)Call(screen,"PrepareQuote"));
                Check(quoteCalls==1&&otherCalls==0,"quote request does not submit a purchase");
                Check(((TMP_Text)Get(screen,"noticeConfirmText")).text=="APPROVE","approval button uses the concise caption");
                Check(((TMP_Text)Get(screen,"noticeBody")).text.Contains("0.025000001 SOL"),"exact server quote is shown above the approval button");
                Check(((TMP_Text)Get(screen,"noticeBody")).text.Contains("network fees"),"network fees remain disclosed");
                fixtureKey=(string)typeof(ShopScreen).GetProperty("PendingKey",Private).GetValue(screen);
                var receipt=new JObject{{"wallet","fixture-wallet"},{"api",api.BaseUrl},{"itemId","starter-pack"},{"contents",parsed.PurchaseContents("starter-pack")},{"quoteToken","fixture-quote"},{"signature","fixture-signature"},{"signedTransaction","fixture-bytes"}};
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
                Check(((TMP_Text)Get(screen,"noticeBody")).text.Contains("5 FUEL\n1 SHIELD\n1 BASE DEFENCE"),"verified recovered payment displays its saved contents");
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
        public static void CapturePurchaseCard()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.OpenShop();var screen=menu.GetComponent<ShopScreen>();
            foreach(var product in ShopCatalog.Products)
            {
                Call(screen,"ShowItemArtwork",product.Id);
                if(!((Image)Get(screen,"purchaseArtwork")).gameObject.activeSelf&&!((RawImage)Get(screen,"purchaseArtworkFallback")).transform.parent.gameObject.activeSelf)
                    throw new Exception("Shop artwork missing for "+product.Id);
            }
            Call(screen,"ShowNotice","MAX HEALER","FULL HEALTH\n\n0.02 SOL + network fees\nApprove this exact price in your wallet.");
            Call(screen,"ShowItemArtwork","max-healer");Call(screen,"ConfigureNoticeAction","APPROVE");
            Canvas.ForceUpdateCanvases();
            if(((TMP_Text)Get(screen,"noticeTitle")).fontSizeMax!=44)throw new Exception("Purchase card title must use the larger size.");
            if(((TMP_Text)Get(screen,"noticeConfirmText")).fontSizeMax!=34||((Button)Get(screen,"noticeClose")).GetComponentInChildren<TMP_Text>().fontSizeMax!=34)throw new Exception("Purchase button captions must use the larger size.");
            var title=(TMP_Text)Get(screen,"noticeTitle");
            foreach(var layout in new[]{(MainMenuPlatform.Web,1600,1067),(MainMenuPlatform.Psg1,1240,1080),(MainMenuPlatform.AndroidLandscape,844,390)})
            {
                menu.ApplyLayout(layout.Item1,new Vector2(layout.Item2,layout.Item3));
                title.text="+2 HEALTH HEALER";Canvas.ForceUpdateCanvases();title.ForceMeshUpdate(true,true);
                if(title.isTextOverflowing)throw new Exception("Purchase card title overflows on "+layout.Item1);
            }
            title.text="MAX HEALER";
            if(Mathf.Abs(((Button)Get(screen,"noticeConfirm")).GetComponent<RectTransform>().rect.width-((Button)Get(screen,"noticeClose")).GetComponent<RectTransform>().rect.width)>.1f)throw new Exception("Purchase buttons must have equal width.");
            System.IO.Directory.CreateDirectory("C:/repos/Unity/ui assets/main-menu/previews");
            MainMenuChecks.Capture(MainMenuPlatform.Web,1600,1067,"purchase-quote-max-healer");
            MainMenuChecks.Capture(MainMenuPlatform.Psg1,1240,1080,"purchase-quote-max-healer-psg1");
            MainMenuChecks.Capture(MainMenuPlatform.AndroidLandscape,844,390,"purchase-quote-max-healer-mobile");
            var controls=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/BattleCities/UI/Settings/MainMenuInput.asset");
            if(controls==null||!System.Linq.Enumerable.Any(controls.FindAction("PSG1/Submit").bindings,b=>b.path=="<Gamepad>/buttonEast")||
               !System.Linq.Enumerable.Any(controls.FindAction("PSG1/Cancel").bindings,b=>b.path=="<Gamepad>/buttonSouth"))throw new Exception("PSG1 A/B bindings missing.");
            var es=EventSystem.current;if(!es){es=new GameObject("Checkout test EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();typeof(EventSystem).GetMethod("OnEnable",Private).Invoke(es,null);}
            var approve=(Button)Get(screen,"noticeConfirm");var close=(Button)Get(screen,"noticeClose");
            es.SetSelectedGameObject(approve.gameObject);
            approve.OnSelect(new BaseEventData(es));screen.RefreshPresentation();
            if(approve.image.overrideSprite!=approve.spriteState.selectedSprite)throw new Exception("Focused Approve button did not use its selected skin.");
            MainMenuChecks.Capture(MainMenuPlatform.Psg1,1240,1080,"purchase-psg1-approve-focused");
            approve.OnMove(new AxisEventData(es){moveDir=MoveDirection.Right});
            if(es.currentSelectedGameObject!=close.gameObject)throw new Exception("D-pad right did not focus Close.");
            close.OnSelect(new BaseEventData(es));screen.RefreshPresentation();
            if(close.image.overrideSprite!=close.spriteState.selectedSprite)throw new Exception("Focused Close button did not use its selected skin.");
            MainMenuChecks.Capture(MainMenuPlatform.Psg1,1240,1080,"purchase-psg1-close-focused");
            close.OnMove(new AxisEventData(es){moveDir=MoveDirection.Left});
            if(es.currentSelectedGameObject!=approve.gameObject)throw new Exception("D-pad left did not focus Approve.");
            var originalClick=approve.onClick;var fakeClick=new Button.ButtonClickedEvent();int submitted=0;fakeClick.AddListener(()=>submitted++);approve.onClick=fakeClick;
            try{approve.OnSubmit(new BaseEventData(es));if(submitted!=1)throw new Exception("A/submit did not activate the focused action.");}
            finally{approve.onClick=originalClick;}
            approve.interactable=false;close.interactable=false;screen.KeepControllerFocus();
            if(es.currentSelectedGameObject)throw new Exception("Focus escaped the busy purchase dialog: "+es.currentSelectedGameObject.name+", same event system="+(EventSystem.current==es)+", current present="+(EventSystem.current!=null)+", selecting="+es.alreadySelecting);
            screen.Back();if(!((RectTransform)Get(screen,"notice")).gameObject.activeSelf)throw new Exception("B closed a locked payment dialog.");
            approve.interactable=true;close.interactable=true;Call(screen,"LinkNoticeButtons");
            screen.Back();if(((RectTransform)Get(screen,"notice")).gameObject.activeSelf)throw new Exception("B did not close the purchase dialog.");
            Call(screen,"ResetNoticeActions");
            var receipt=new JObject{{"itemId","starter-pack"},{"contents",ShopLiveCatalog.Parse(Catalog()).PurchaseContents("starter-pack")}};
            string confirmation=(string)typeof(ShopScreen).GetMethod("PurchaseConfirmation",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{receipt});
            Call(screen,"ShowNotice","STARTER PACK",confirmation);Call(screen,"ShowPurchaseCard",receipt,confirmation);
            if(((TMP_Text)Get(screen,"noticeTitle")).text!="PURCHASE CONFIRMED"||!((TMP_Text)Get(screen,"noticeBody")).text.Contains("1 SHIELD"))throw new Exception("Purchase card does not show the confirmed items.");
            if(!((Image)Get(screen,"purchaseArtwork")).gameObject.activeSelf&&!((RawImage)Get(screen,"purchaseArtworkFallback")).transform.parent.gameObject.activeSelf)throw new Exception("Purchase card artwork is missing.");
            MainMenuChecks.Capture(MainMenuPlatform.Web,1600,1067,"purchase-card-preview");
            MainMenuChecks.Capture(MainMenuPlatform.AndroidLandscape,844,390,"purchase-card-preview-mobile");
            MainMenuChecks.Capture(MainMenuPlatform.Psg1,1240,1080,"purchase-card-preview-psg1");
            Call(screen,"ResetNoticeActions");
            if(((Image)Get(screen,"purchaseArtwork")).gameObject.activeSelf||((RawImage)Get(screen,"purchaseArtworkFallback")).transform.parent.gameObject.activeSelf)throw new Exception("Purchase card artwork leaked into the next checkout notice.");
            if(((TMP_Text)Get(screen,"noticeTitle")).fontSizeMax!=34)throw new Exception("Regular notices must retain their original title size.");
            Debug.Log("Purchase card checks passed: "+RunPurchaseConfirmationChecks()+" confirmation checks and card artwork.");
            Debug.Log(Run());
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
