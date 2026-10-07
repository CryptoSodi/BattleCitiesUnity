using System;
using System.Collections;
using BattleCities.Core;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.Editor
{
    public static class PlayerProfileReplayChecks
    {
        public static string Status {get;private set;}="Not started";
        const string Player="ply-profile-fixture",Viewer="ply-viewer-fixture";
        static int count,failures,replayRequests,launches;
        static string scenario,lastPath;
        static JObject replay;
        static void Check(bool ok,string message){count++;if(!ok){failures++;Debug.LogError("Public replay: "+message);}}
        [MenuItem("Battle Cities/Checks/Public replay WATCH (Play mode)")]
        public static void Run()
        {
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            if(!Application.isPlaying||!menu)throw new InvalidOperationException("Run in MainMenu Play mode.");
            menu.StartCoroutine(Verify(menu));
        }
        static IEnumerator Verify(MainMenuScene menu)
        {
            Status="Running";count=failures=replayRequests=launches=0;scenario="native";
            var map=Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text);
            var simulation=new BattleSimulation(map);var recorder=new ReplayRecorder(simulation,map,"test");
            recorder.BeforeStep(default);simulation.Step(default);recorder.AfterStep();recorder.Finish("aborted");
            replay=JObject.Parse(ReplayJson.Write(recorder.Data));
            var go=new GameObject("Public replay fixture API");go.SetActive(false);
            var api=go.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);
            api.Configure("http://127.0.0.1:18763");api.EditorRequestOverride=Reply;
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(MainMenuBuilder.Root+"Settings/ArcadeMenuTheme.asset");
            PlayerProfileScreen page=null;
            try
            {
                menu.OpenRanking();menu.OpenPlayerProfile(Player);page=menu.GetComponent<PlayerProfileScreen>();
                page.Configure(menu,theme,api,menu.Content.Find("Main Display") as RectTransform);
                page.EditorReplayLaunchOverride=data=>{launches++;var player=new ReplayPlayer(data);player.Step();Check(player.Complete,"downloaded public recording reproduces");};
                page.Open(Player);yield return WaitProfile(page);
                Check(!api.IsAuthenticated&&!page.IsOwnProfile&&page.State==PlayerProfileScreen.ViewState.Ready,"anonymous viewer loads another player's profile");
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    menu.ApplyLayout(platform,platform==MainMenuPlatform.Web?new Vector2(1583,924):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(844,390));
                    Canvas.ForceUpdateCanvases();
                    var heading=page.Root.Find("Battle log/Heading");
                    var library=heading.Find("Local replays");Check((!library||!library.gameObject.activeSelf)&&heading.Find("Records").gameObject.activeSelf,platform+" profile shows record count and match WATCH only");
                    Check(page.BattleButtons[0].transform.Find("Watch/Caption").GetComponent<TMP_Text>().text=="WATCH",platform+" public replay can be watched");
                    Check(page.BattleButtons[1].transform.Find("Watch/Caption").GetComponent<TMP_Text>().text=="NO REPLAY",platform+" missing recording stays unavailable");
                    EventSystem.current.SetSelectedGameObject(page.BattleButtons[0].gameObject);
                    Check(launches==0&&replayRequests==0,platform+" focus never requests or starts playback");
                    foreach(var control in page.Root.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                    {
                        var nav=control.navigation;
                        foreach(var target in new[]{nav.selectOnUp,nav.selectOnDown,nav.selectOnLeft,nav.selectOnRight})
                            Check(!target||target.gameObject.activeInHierarchy,platform+" navigation skips hidden library");
                    }
                }
                page.Watch(1);yield return null;Check(replayRequests==0,"unavailable row sends no request");
                ExecuteEvents.Execute(page.BattleButtons[0].gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return WaitRequest(page);
                Check(launches==1&&lastPath=="/api/players/"+Player+"/profile/matches/mtc-fixture-0/replay","anonymous WATCH uses selected player's public endpoint");
                scenario="array";page.Watch(0);yield return WaitRequest(page);Check(launches==2,"replays array response is supported");
                foreach(var failure in new[]{"missing","offline","empty","bad-item","bad-array","damaged","future","legacy"})
                {
                    scenario=failure;int before=launches;page.Watch(0);yield return WaitRequest(page);
                    string message=page.Root.Find("Commander/Paper/Identity").GetComponent<TMP_Text>().text;
                    Check(launches==before&&message!="LOADING REPLAY...",failure+" gives feedback without playback");
                }
                scenario="slow";int previous=launches;page.Watch(0);page.Close();yield return new WaitForSecondsRealtime(.25f);
                Check(launches==previous,"closing profile cancels pending playback");
                scenario="native";page.Open();yield return WaitProfile(page);
                var ownLibrary=page.Root.Find("Battle log/Heading/Local replays");Check(page.Data.Id==Viewer&&(!ownLibrary||!ownLibrary.gameObject.activeSelf)&&page.Root.Find("Battle log/Heading/Records").gameObject.activeSelf,"own profile also uses match WATCH only");
                scenario="slow";page.Watch(0);page.Open(Player);yield return WaitProfile(page);yield return new WaitForSecondsRealtime(.25f);
                Check(launches==previous&&page.Data.Id==Player,"switching players cancels stale playback");
                Check(!page.ReplayViewerOpen,"public WATCH fixture leaves no active playback");
            }
            finally
            {
                if(page){page.EditorReplayLaunchOverride=null;page.Close();page.Configure(menu,theme,menu.GetComponent<MainMenuApiClient>(),menu.Content.Find("Main Display") as RectTransform);}
                UnityEngine.Object.Destroy(go);menu.RefreshLayout();
            }
            Status=$"Public replay WATCH: {count-failures}/{count} checks passed.";Debug.Log(Status);
        }
        static IEnumerator WaitProfile(PlayerProfileScreen page)
        {float until=Time.realtimeSinceStartup+3;while(page.IsLoading&&Time.realtimeSinceStartup<until)yield return null;Check(!page.IsLoading,"profile request completes");}
        static IEnumerator WaitRequest(PlayerProfileScreen page)
        {
            float until=Time.realtimeSinceStartup+3;int before=launches;
            var label=page.Root.Find("Commander/Paper/Identity").GetComponent<TMP_Text>();
            while(label.text=="LOADING REPLAY..."&&launches==before&&Time.realtimeSinceStartup<until)
            {
                yield return null;
            }
            Check(launches!=before||label.text!="LOADING REPLAY...","WATCH request completes");
        }
        static IEnumerator Reply(string method,string path,JObject payload,Action<long,JObject,string> done)
        {
            Check(method=="GET"&&payload==null,"spectating uses read-only requests");lastPath=path;var choice=scenario;
            if(path.EndsWith("/replay"))
            {
                replayRequests++;
                if(choice=="slow")yield return new WaitForSecondsRealtime(.2f);else yield return null;
                if(choice=="missing"){done(404,null,"not found");yield break;}
                if(choice=="offline"){done(200,null,"invalid response");yield break;}
                if(choice=="empty"){done(200,new JObject(),null);yield break;}
                if(choice=="bad-item"){done(200,new JObject{["item"]="bad"},null);yield break;}
                if(choice=="bad-array"){done(200,new JObject{["item"]=new JObject{["replays"]="bad"}},null);yield break;}
                var data=(JObject)replay.DeepClone();
                if(choice=="damaged")data["simulationVersion"]="incompatible";
                if(choice=="future")data["format"]="battlecities-unity-input-v2";
                if(choice=="legacy")data["format"]="battlecities-web-replay-v1";
                done(200,new JObject{["item"]=choice=="array"?new JObject{["replays"]=new JArray(data)}:new JObject{["replay"]=data}},null);yield break;
            }
            yield return null;
            if(path=="/api/player"){done(200,new JObject{["authenticated"]=true,["player"]=new JObject{["id"]=Viewer}},null);yield break;}
            var profile=PlayerProfileChecks.Fixture();profile["item"]["id"]=path.Contains(Viewer)?Viewer:Player;done(200,profile,null);
        }
    }
}
