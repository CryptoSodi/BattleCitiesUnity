using System;
using System.Collections;
using System.Linq;
using BattleCities.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Newtonsoft.Json.Linq;

namespace BattleCities.Editor
{
    /// <summary>Focused UI and local-fixture checks. Never submits social tasks to production.</summary>
    public static class OperationsPageChecks
    {
        public static string NetworkStatus {get;private set;}="Not started";
        public static string NavigationStatus {get;private set;}="Not started";
        static int failures,checks;
        static void Check(bool okay,string what){checks++;if(!okay){failures++;Debug.LogError("Operations check failed: "+what);}}
        static string Label(OperationsScreen screen,string path)=>screen.Root.Find(path)?.GetComponent<TMP_Text>()?.text;
        public static void CheckNavigation()
        {
            failures=checks=0;var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();bool enabled=module&&module.enabled;if(module)module.enabled=false;
            try
            {
                foreach(var p in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    menu.OpenQuarters();menu.ApplyLayout(p,p==MainMenuPlatform.Psg1?new Vector2(1240,1080):p==MainMenuPlatform.Web?new Vector2(1583,924):new Vector2(844,390));Canvas.ForceUpdateCanvases();
                    var screen=menu.GetComponent<OperationsScreen>();Check(screen.ItemCount==7,p+" seven Quarters cards");
                    var scroll=screen.Scroll;var last=scroll.content.Find("Card 6") as RectTransform;
                    screen.FocusCard(6,true,false);Canvas.ForceUpdateCanvases();
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,last);
                    Check(bounds.min.y>=scroll.viewport.rect.yMin-1&&bounds.max.y<=scroll.viewport.rect.yMax+1,p+" complete focused card reveal");
                    Check(last.GetComponent<UnityEngine.UI.Image>().sprite==Resources.Load<PreBattleArt>("PreBattleArt").tankCardUnavailable,p+" focus does not activate a locked card");
                    scroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
                    ExecuteEvents.ExecuteHierarchy(last.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-3)},ExecuteEvents.scrollHandler);
                    Check(scroll.verticalNormalizedPosition<.999f,p+" mouse wheel reaches parent scroll");
                    foreach(var b in screen.Root.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.IsInteractable()))Check(b.navigation.mode==UnityEngine.UI.Navigation.Mode.Explicit,p+" explicit navigation "+b.name);
                    var first=scroll.content.Find("Card 0").gameObject;EventSystem.current.SetSelectedGameObject(first);ExecuteEvents.Execute(first,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                    Check(screen.CurrentPage==OperationsScreen.Page.Treasury,p+" submit opens Treasury");screen.Back();Check(screen.CurrentPage==OperationsScreen.Page.Quarters,p+" Treasury Back");
                    screen.OpenManual();Check(screen.ItemCount==4,p+" tank entries");screen.SelectManualCategory(2);Check(screen.ItemCount==8,p+" all powerups");
                    screen.OpenManualEntry("extra-life");Check(screen.CurrentPage==OperationsScreen.Page.ManualDetail,p+" manual detail");screen.Back();Check(screen.ItemCount==8,p+" detail returns to category");screen.Back();Check(screen.CurrentPage==OperationsScreen.Page.Quarters,p+" manual Back");
                    menu.OpenShop();Check(!screen.IsOpen&&menu.IsShopOpen,p+" Shop closes operations");menu.OpenRanking();Check(!menu.IsShopOpen&&menu.IsRankingOpen,p+" Ranking closes Shop");menu.OpenSocials();Check(!menu.IsRankingOpen&&menu.IsSocialsOpen&&screen.ItemCount==6,p+" Socials opens exclusively");
                    screen.Close();
                }
            }
            finally{menu.OpenQuarters();menu.RefreshLayout();if(module)module.enabled=enabled;}
            NavigationStatus="Operations navigation: "+(checks-failures)+"/"+checks+" passed.";Debug.Log(NavigationStatus);
        }
        public static void StartNetworkChecks(string fixtureBase)
        {
            if(!Uri.TryCreate(fixtureBase,UriKind.Absolute,out var uri)||!uri.IsLoopback)throw new ArgumentException("Fixtures must use loopback; never run mutation checks on live APIs.");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();if(Application.isPlaying)menu.StartCoroutine(NetworkChecks(menu,fixtureBase,null));else new SocialFixturePump().Run(pump=>NetworkChecks(menu,fixtureBase,pump));
        }
        static IEnumerator WaitForData(OperationsScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+25;while(screen.IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!screen.IsLoading,"network request completed");yield return null;
        }
        static IEnumerator NetworkChecks(MainMenuScene menu,string endpoint,SocialFixturePump pump)
        {
            NetworkStatus="Running";failures=checks=0;menu.OpenQuarters();var screen=menu.GetComponent<OperationsScreen>();
            var realApi=menu.GetComponent<MainMenuApiClient>();var frame=(RectTransform)screen.Root.parent;
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MenuTheme")[0]));
            var go=new GameObject("Operations local fixture client");go.SetActive(false);var api=go.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);api.Configure(endpoint+"/populated");
            bool claimed=false;
            api.EditorRequestOverride=(method,path,payload,reply)=>Fixture(api.BaseUrl,method,path,reply,()=>claimed,()=>claimed=true);go.SetActive(true);if(pump!=null){screen.EditorStartRoutineOverride=pump.Start;api.EditorStartRoutineOverride=pump.Start;}
            try
            {
                screen.Configure(menu,theme,api,frame);screen.Open();screen.OpenTreasury();yield return WaitForData(screen);
                Check(screen.ItemCount==8,"all owned items from API");Check(Label(screen,"Body/Account summary/Stat 0/Label")=="BATC","legacy currency remains BATC");Check(Label(screen,"Body/Account summary/Stat 2/Value")=="42","live fuel balance");
                MainMenuChecks.Capture(MainMenuPlatform.Web,1583,924,"treasury-holdings-web");
                screen.SelectTreasuryHistory(true);var lastLedger=screen.Scroll.content.Find("Ledger 19");Check(lastLedger&&lastLedger.gameObject.activeSelf,"recent twenty ledger rows");
                if(lastLedger){var rect=(RectTransform)lastLedger;Check(rect.rect.width>screen.Scroll.viewport.rect.width*.9f&&rect.rect.height>=50&&rect.rect.height<=65,"history uses full-width readable rows");}
                MainMenuChecks.Capture(MainMenuPlatform.Web,1583,924,"treasury-history-web");
                api.Configure(endpoint+"/empty");screen.Refresh();yield return WaitForData(screen);Check(Label(screen,"Body/Message/Title")=="NO TRANSACTIONS YET","valid empty history");
                api.Configure(endpoint+"/offline");screen.Refresh();yield return WaitForData(screen);Check(Label(screen,"Body/Message/Title")=="TREASURY UNAVAILABLE","API error is not a login prompt");
                api.Configure(endpoint+"/malformed");screen.Refresh();yield return WaitForData(screen);Check(Label(screen,"Body/Message/Title")=="TREASURY UNAVAILABLE","malformed response is unavailable");
                api.Configure(endpoint+"/anonymous");screen.Refresh();yield return WaitForData(screen);Check(Label(screen,"Status/Action/Label")=="CONNECT WALLET","anonymous connection action");
                screen.Open(true);yield return WaitForData(screen);Check(Label(screen,"Body/Scroll/Viewport/Content/Card 1/Action/Caption")=="CONNECT WALLET","social login state");
                api.Configure(endpoint+"/unlinked");screen.Refresh();yield return WaitForData(screen);screen.Scroll.content.Find("Card 1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return WaitForData(screen);Check(screen.IsLinkNoticeOpen,"native social linking instructions");screen.Back();Check(!screen.IsLinkNoticeOpen&&screen.IsSocials,"Back closes linking instructions first");
                api.Configure(endpoint+"/populated");screen.Refresh();yield return WaitForData(screen);Check(Label(screen,"Body/Scroll/Viewport/Content/Card 1/Action/Caption")=="FOLLOWED","server verified follow");Check(Label(screen,"Body/Scroll/Viewport/Content/Card 4/Action/Caption")=="COMPLETED","server completed repost");
                screen.Scroll.content.Find("Card 3").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return WaitForData(screen);Check(Label(screen,"Body/Scroll/Viewport/Content/Card 3/Action/Caption")=="VERIFIED","Discord claim uses server confirmation");
                api.Configure(endpoint+"/offline");screen.Refresh();yield return WaitForData(screen);Check(screen.Status.Contains("UNAVAILABLE"),"social API failure stays unavailable");
                api.Configure(endpoint+"/malformed");screen.Refresh();yield return WaitForData(screen);Check(screen.Status.Contains("UNAVAILABLE"),"malformed social status stays unavailable");
                NetworkStatus=(failures==0?"PASS":"FAIL")+": "+(checks-failures)+"/"+checks+" fixture checks";
            }
            finally
            {
                screen.Close(false);screen.EditorStartRoutineOverride=null;api.EditorStartRoutineOverride=null;screen.Configure(menu,theme,realApi,frame);screen.Open();if(Application.isPlaying)UnityEngine.Object.Destroy(go);else UnityEngine.Object.DestroyImmediate(go);menu.RefreshLayout();
                if(NetworkStatus=="Running")NetworkStatus="Aborted; inspect Unity errors";
            }
            Debug.Log(NetworkStatus);
        }
        static IEnumerator Fixture(string mode,string method,string path,Action<long,JObject,string> reply,Func<bool> claimed,Action claim)
        {
            yield return null;yield return null;
            if(mode.EndsWith("offline")){reply(503,new JObject{["error"]="Fixture unavailable"},"Fixture unavailable");yield break;}
            if(mode.EndsWith("malformed")){reply(200,new JObject(),null);yield break;}
            if(mode.EndsWith("anonymous")){reply(path.EndsWith("account")?200:401,new JObject{["authenticated"]=false},null);yield break;}
            bool empty=mode.EndsWith("empty");JObject body;
            if(path=="/api/economy/account")body=JObject.Parse("{\"authenticated\":true,\"account\":{\"tokenBalance\":1234567,\"solBalance\":1.2345,\"fuelBalance\":42,\"inventory\":{\"shield\":2,\"base-defence\":12,\"freeze\":3,\"speed\":4,\"upgrade\":5,\"zoom-out\":6,\"wipeout\":7,\"extra-life\":8}}}");
            else if(path=="/api/economy/ledger")
            {
                var rows=new JArray();if(!empty)for(int i=0;i<20;i++)rows.Add(new JObject{["currency"]=i%2==0?"FUEL":"BATC",["amount"]=i%2==0?5:-1250,["reason"]=i==0?"A VERY LONG REWARD DESCRIPTION TO VERIFY READABLE WRAPPING":"SOCIAL REWARD",["createdAt"]=DateTime.Parse("2026-10-06T08:00:00Z").ToUniversalTime()});
                body=new JObject{["authenticated"]=true,["entries"]=rows};
            }
            else if(path=="/api/integrations/x/status")body=JObject.Parse("{\"authenticated\":true,\"connected\":true,\"follows\":true,\"repostTask\":{\"id\":\"repost-test\",\"postId\":\"1\",\"rewardFuel\":5,\"claimed\":true},\"commentTask\":{\"id\":\"comment-test\",\"postId\":\"2\",\"rewardFuel\":5,\"claimed\":false}}");
            else if(path=="/api/integrations/discord/verification")body=new JObject{["authenticated"]=true,["verified"]=true,["rewardClaimed"]=claimed()};
            else if(path=="/api/integrations/discord/claim-reward"&&method=="POST"){claim();body=new JObject{["ok"]=true,["granted"]=true};}
            else{reply(404,new JObject{["error"]="Unsupported fixture endpoint"},"Unsupported fixture endpoint");yield break;}
            if(empty&&body["account"] is JObject account)account["inventory"]=new JObject();
            if(mode.EndsWith("unlinked")&&path=="/api/integrations/x/status")body=new JObject{["authenticated"]=true,["connected"]=false,["follows"]=false};
            reply(200,body,null);
        }
    }
}
