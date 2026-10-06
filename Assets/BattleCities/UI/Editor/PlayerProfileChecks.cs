using System;
using System.Collections;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class PlayerProfileChecks
    {
        public static string Status {get;private set;}="Not started";
        static int count,failures;static string response="populated",lastPath;static int requests;
        const string Player="ply-profile-fixture";
        static void Check(bool ok,string message){count++;if(!ok){failures++;Debug.LogError("Profile check: "+message);}}
        public static void Run(){var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.StartCoroutine(Verify(menu));}
        static IEnumerator Verify(MainMenuScene menu)
        {
            Status="Running";count=failures=requests=0;
            var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();bool was=module&&module.enabled;if(module)module.enabled=false;
            var go=new GameObject("Profile fixture API");go.SetActive(false);var api=go.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);api.Configure("http://127.0.0.1:18763");api.EditorRequestOverride=Reply;
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(MainMenuBuilder.Root+"Settings/ArcadeMenuTheme.asset");PlayerProfileScreen page=null;
            try
            {
                menu.OpenShop();menu.OpenOwnProfile();page=menu.GetComponent<PlayerProfileScreen>();
                if(menu.GetComponent<MainMenuApiClient>().IsLocalGuest)Check(page.State==PlayerProfileScreen.ViewState.Guest&&!page.ShareButton.interactable,"local guest has no shareable online record");
                menu.Back();Check(menu.IsShopOpen&&!menu.IsPlayerProfileOpen,"Back restores Shop");
                menu.OpenSettings();menu.OpenOwnProfile();menu.Back();Check(menu.IsSettingsOpen&&!menu.IsPlayerProfileOpen,"Back restores Settings");
                menu.OpenOwnProfile();menu.OpenQuarters();Check(menu.IsOperationsOpen&&!menu.IsPlayerProfileOpen&&!menu.IsSettingsOpen,"main navigation closes profile");
                menu.OpenRanking();menu.OpenPlayerProfile(Player);page.Configure(menu,theme,api,menu.Content.Find("Main Display") as RectTransform);
                response="populated";page.Open(Player);Check(page.IsLoading&&!page.ShareButton.interactable,"loading blocks stale sharing");yield return Wait(page);
                Check(page.State==PlayerProfileScreen.ViewState.Ready&&page.Data.Battles.Count==12,"valid profile and paginated rows loaded");
                Check(page.Data.Points==950&&page.Data.SeasonRank==null&&page.Data.BestScore==5100,"API statistics preserved");
                Check(page.Data.Joined=="08 JUL 2026","UTC JSON date parsed");
                Check(page.ShareButton.interactable,"ready profile can be shared");
                Check(lastPath=="/api/players/"+Player+"/profile?page=1","public profile endpoint and pagination");
                var links=Resources.Load<ProfileLinks>("ProfileLinks");Check(links.ShareUrl(Player)=="https://battlecities.com/?playerProfile="+Player,"share domain and incoming profile parameter");
                Check(links.ReplayUrl(Player,"mtc-123")=="https://battlecities.com/?profileReplayPlayer="+Player+"&profileReplayMatch=mtc-123","existing browser replay parameters");
                Check(links.ShareUrl("../not-player")==null&&links.ReplayUrl(Player,"bad")==null,"invalid IDs never create links");
                Check(ProfileLinks.IncomingPlayer(links.ShareUrl(Player))==Player,"shared URL resolves into Unity profile");
                Check(ProfileLinks.IncomingPlayer("https://battlecities.com/?playerProfile=%2E%2E%2Fbad")==null,"invalid incoming profile is ignored");
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    var size=platform==MainMenuPlatform.Web?new Vector2(1583,924):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(844,390);
                    menu.ApplyLayout(platform,size);Canvas.ForceUpdateCanvases();
                    Check(menu.IsPlayerProfileOpen&&!menu.IsRankingOpen&&!menu.IsShopOpen&&!menu.IsOperationsOpen,platform+" exclusive profile");
                    var header=(RectTransform)page.Root.Find("Title plate");var footer=(RectTransform)page.Root.Find("Status");
                    Check(Math.Abs(header.rect.height-Mathf.Clamp(page.Root.rect.height*.083f,46,64))<.1f,platform+" shared title height");
                    Check(Math.Abs(footer.rect.height-Mathf.Clamp(page.Root.rect.height*.078f,50,64))<.1f,platform+" shared footer height");
                    var stats=footer.Find("Statistics");
                    Check(stats&&stats.Find("Stat 1/Value").GetComponent<TMP_Text>().text=="950"&&stats.Find("Stat 2/Value").GetComponent<TMP_Text>().text=="13",platform+" live statistics moved into white footer");
                    Check(footer.GetComponentsInChildren<Button>().Length==0&&(!footer.Find("Label")||!footer.Find("Label").gameObject.activeSelf),platform+" footer has no status or wallet action");
                    var log=(RectTransform)page.Root.Find("Battle log");var commander=(RectTransform)page.Root.Find("Commander");
                    Check(Math.Abs(-log.anchoredPosition.y-(-commander.anchoredPosition.y+commander.rect.height+7))<.2f,platform+" battles reclaim removed statistics row");
                    float compactCommanderHeight=commander.rect.height;var playerId=commander.Find("Paper/Player ID");
                    var portrait=commander.Find("Paper/Portrait") as RectTransform;
                    Check(portrait.rect.height>=compactCommanderHeight*.65f&&commander.Find("Paper/Name").GetComponent<TMP_Text>().fontSize>=24f,platform+" compact frame preserves a readable portrait and name");
                    var oldDetails=commander.Find("Paper/Details");
                    Check((!oldDetails||!oldDetails.gameObject.activeSelf)&&(!playerId||!playerId.gameObject.activeSelf),platform+" no Details control or player ID");
                    Check(page.Root.Find("Title plate/Navigation/Back").GetComponent<Button>().navigation.selectOnRight==page.ShareButton,platform+" controller moves directly from Back to Share");
                    var logHeader=log.Find("Heading");Check(logHeader.Find("Title").GetComponent<TMP_Text>().color==ArcadeTextStyles.PrizeAmountFace&&logHeader.Find("RESULT")&&logHeader.Find("POINTS")&&logHeader.Find("Divider").GetComponent<Image>().color.a>0,platform+" cream table heading, columns and separator");
                    var scroller=page.Scroll;scroller.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
                    scroller.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-3)});Canvas.ForceUpdateCanvases();Check(scroller.verticalNormalizedPosition<.99f,platform+" mouse wheel moves rows");
                    var last=page.BattleButtons[11];EventSystem.current.SetSelectedGameObject(last.gameObject);scroller.Reveal((RectTransform)last.transform);Canvas.ForceUpdateCanvases();
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroller.viewport,last.transform);Check(bounds.min.y>=scroller.viewport.rect.yMin-1&&bounds.max.y<=scroller.viewport.rect.yMax+1,platform+" selected row fully revealed");
                    Check(last.GetComponent<Outline>().enabled,platform+" cursor row has visible cyan focus");
                    Check(page.BattleButtons[0].navigation.selectOnDown==page.BattleButtons[1],platform+" controller row navigation");
                    Check(page.BattleButtons[1].transform.Find("Watch/Caption").GetComponent<TMP_Text>().text=="NO REPLAY",platform+" unavailable replay labelled honestly");
                    Check(page.Root.Find("Commander/Paper/Name").GetComponent<TMP_Text>().richText==false,platform+" player names treated as data");
                    scroller.verticalNormalizedPosition=1;EventSystem.current.SetSelectedGameObject(page.Root.Find("Title plate/Navigation/Back").gameObject);
                    MainMenuChecks.Capture(platform,(int)size.x,(int)size.y,"profile-"+(platform==MainMenuPlatform.Web?"web":platform==MainMenuPlatform.Psg1?"psg1":"seeker")+"-fixture");
                }
                var feedbackCommander=page.Root.Find("Commander") as RectTransform;float feedbackHeight=feedbackCommander.rect.height;
                page.OnProfileShareResult("cancelled");Canvas.ForceUpdateCanvases();
                Check(page.Root.Find("Commander/Paper/Identity").GetComponent<TMP_Text>().text=="SHARE CANCELLED"&&Math.Abs(feedbackCommander.rect.height-feedbackHeight)<.1f,"share feedback stays in the existing identity line without expanding the card");
                response="page2";page.SelectPage(2);yield return Wait(page);Check(lastPath.EndsWith("page=2")&&page.Data.Page==2&&page.BattleButtons.Count==1,"next page loads remaining battle");
                bool fits=page.Scroll.content.rect.height<=page.Scroll.viewport.rect.height;
                Check(fits?Math.Abs(page.Scroll.content.anchoredPosition.y)<1:page.Scroll.verticalNormalizedPosition>.99f,"new page resets scroll to top");
                response="long";page.Open(Player);yield return Wait(page);Check(page.Data.Points==999999999999L,"large scores remain precise");
                Check(page.Root.Find("Commander/Paper/Name").GetComponent<TMP_Text>().overflowMode==TextOverflowModes.Ellipsis,"long names have safe overflow");
                foreach(var scenario in new[]{"empty","malformed","offline","notfound","signedout"})
                {
                    response=scenario;if(scenario=="signedout")page.Open();else page.Open(Player);yield return Wait(page);
                    var expected=scenario=="empty"?PlayerProfileScreen.ViewState.Ready:scenario=="notfound"?PlayerProfileScreen.ViewState.NotFound:scenario=="signedout"?PlayerProfileScreen.ViewState.SignIn:PlayerProfileScreen.ViewState.Unavailable;
                    Check(page.State==expected&&page.BattleButtons.Count==0,scenario+" has distinct state and no stale rows");
                    if(scenario!="empty")Check(!page.ShareButton.interactable&&page.Data==null,scenario+" cannot share stale profile");
                    Check(page.Root.Find("Battle log/Empty/Retry").gameObject.activeSelf==(scenario=="malformed"||scenario=="offline"),scenario+" retry only appears for unavailable records");
                }
                response="populated";page.Open();yield return Wait(page);Check(page.State==PlayerProfileScreen.ViewState.Ready&&page.Data.Id==Player,"own profile resolves authenticated player");
                response="slow";page.Open(Player);menu.Back();yield return new WaitForSecondsRealtime(.25f);Check(!menu.IsPlayerProfileOpen&&menu.IsRankingOpen,"Back cancels loading and restores Ranking");
                int before=requests;menu.OpenPlayerProfile("bad-id");page.Configure(menu,theme,api,menu.Content.Find("Main Display") as RectTransform);page.Open("bad-id");yield return Wait(page);Check(page.State==PlayerProfileScreen.ViewState.NotFound&&requests==before,"invalid public ID does not request API");
            }
            finally
            {
                if(page){page.Configure(menu,theme,menu.GetComponent<MainMenuApiClient>(),menu.Content.Find("Main Display") as RectTransform);page.Open();}
                UnityEngine.Object.Destroy(go);menu.RefreshLayout();if(module)module.enabled=was;
            }
            Status=$"Player Profile: {count-failures}/{count} checks passed.";Debug.Log(Status);
        }
        static IEnumerator Wait(PlayerProfileScreen page){float limit=Time.realtimeSinceStartup+3;while(page.IsLoading&&Time.realtimeSinceStartup<limit)yield return null;Check(!page.IsLoading,"profile request completes");}
        static IEnumerator Reply(string method,string path,JObject payload,Action<long,JObject,string> done)
        {
            requests++;lastPath=path;Check(method=="GET","profile API uses read-only requests");string scenario=response;
            if(scenario=="slow")yield return new WaitForSecondsRealtime(.2f);else yield return null;
            if(path=="/api/player"){done(200,new JObject{["authenticated"]=scenario!="signedout",["player"]=new JObject{["id"]=Player}},null);yield break;}
            if(scenario=="offline"){done(0,null,"offline");yield break;}if(scenario=="notfound"){done(404,null,"not found");yield break;}
            if(scenario=="malformed"){done(200,new JObject{["item"]=new JObject{["id"]=Player,["displayName"]="Malformed",["provider"]="wallet",["stats"]="bad"}},null);yield break;}
            var body=Fixture(scenario=="empty"?0:scenario=="page2"?1:12,scenario=="page2"?2:1);
            if(scenario=="long"){body["item"]["displayName"]="<color=red>A VERY LONG COMMANDER NAME WITH MARKUP</color>";body["item"]["stats"]["allTime"]["totalPoints"]=999999999999L;}
            done(200,body,null);
        }
        public static JObject Fixture(int rows=12,int page=1)
        {
            var battles=new JArray();for(int i=0;i<rows;i++)battles.Add(new JObject{["id"]="mtc-fixture-"+i,["mode"]=i%3==0?"multi":"single",["levelNumber"]=i+1,["score"]=i==0?5100:400,["gamePoints"]=i==0?610:40,["won"]=i%3==0,["createdAt"]=new DateTime(2026,10,1,12,0,0,DateTimeKind.Utc),["replayAvailable"]=i%2==0});
            return new JObject{["item"]=new JObject{["id"]=Player,["displayName"]="COMMANDER PREVIEW",["provider"]="wallet",["joinedAt"]=new DateTime(2026,7,8,0,0,0,DateTimeKind.Utc),["highscores"]=new JObject{["primary"]=5100},["stats"]=new JObject{["allTime"]=new JObject{["totalPoints"]=950,["matches"]=13},["currentSeason"]=new JObject{["name"]="SEASON 3",["rank"]=null}},["recentMatchesPage"]=new JObject{["page"]=page,["pageSize"]=12,["total"]=rows==0?0:13},["recentMatches"]=battles}};
        }
    }
}
