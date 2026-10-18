using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BattleCities.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

namespace BattleCities.Editor
{
    public static class ScreenMigrationChecks
    {
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Call(object target,string method) => target.GetType().GetMethod(method,Private).Invoke(target,null);
        static string PathOf(Transform child,Transform root)
        { string path=child.name;while(child.parent&&child.parent!=root){child=child.parent;path=child.name+"/"+path;}return path; }
        static string ImageName(RectTransform rect) { var image=rect.GetComponent<Image>();return image&&image.sprite?image.sprite.name:null; }
        static object Dump(RectTransform root) => root.GetComponentsInChildren<RectTransform>(true).Where(x=>x!=root).Select(x=>new {
            path=PathOf(x,root),text=x.GetComponent<TMPro.TMP_Text>()?.text??x.GetComponent<Text>()?.text,
            font=x.GetComponent<TMPro.TMP_Text>()?.fontSize??x.GetComponent<Text>()?.fontSize??0,
            image=ImageName(x),imageType=x.GetComponent<Image>()?.type.ToString(),rect=new[]{x.anchorMin.x,1-x.anchorMax.y,x.anchorMax.x-x.anchorMin.x,x.anchorMax.y-x.anchorMin.y},
            offsetMin=new[]{x.offsetMin.x,x.offsetMin.y},offsetMax=new[]{x.offsetMax.x,x.offsetMax.y}
        }).ToArray();
        public static void Inventory()
        {
            ScreenUpdates.EditorReset();
            var all=new Dictionary<string,object>();
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            foreach(string method in new[]{"EnsureSettings","EnsureShop","EnsureRanking","EnsurePlayerProfile","EnsureOperations","EnsurePreBattle"})Call(menu,method);
            all["home"]=Dump(menu.Content);
            foreach(var view in menu.Content.GetComponentsInChildren<RemoteScreenView>(true))all[view.name]=Dump((RectTransform)view.transform);
            var profile=UnityEngine.Object.FindFirstObjectByType<PlayerProfileScreen>();Call(profile,"BuildReplayViewer");all["replay"]=Dump(profile.ReplayView);
            var result=new GameObject("Results fixture").AddComponent<BattleResultsScreen>();Call(result,"Build");
            var resultRoot=(RectTransform)typeof(BattleResultsScreen).GetField("root",Private).GetValue(result);all["battle-results"]=Dump(resultRoot);
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/Login.unity");
            var login=UnityEngine.Object.FindFirstObjectByType<LoginScene>();all["login"]=Dump((RectTransform)UnityEngine.Object.FindFirstObjectByType<LoginLayout>().transform);
            Directory.CreateDirectory("Builds/ScreenUpdateChecks");File.WriteAllText("Builds/ScreenUpdateChecks/inventory.json",JsonConvert.SerializeObject(all,Formatting.Indented));
            Debug.Log("SCREEN INVENTORY COMPLETE");
        }
        static int checks;
        static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
        static void Reject(Action action,string reason){try{action();}catch{checks++;return;}throw new Exception(reason);}
        static ScreenDocument Clone(ScreenDocument source)=>ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(source));
        static bool ContainsText(Transform root,string text)=>root.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(x=>x.text==text)||root.GetComponentsInChildren<Text>(true).Any(x=>x.text==text);
        static string Tag(string id)=>"OTA "+id.ToUpperInvariant();
        static readonly Dictionary<string,string> Titles=new Dictionary<string,string>{
            {"shop","SHOP"},{"ranking","RANKING"},{"profile","PLAYER PROFILE"},{"replay","MATCH REPLAY"},{"battle-results","MATCH REPORT"},
            {"lobby","BATTLE TOGETHER"},{"battle-hud","SCORE"},{"pause","PAUSED"}
        };
        public static void RunPlayerIdentityValidation() { SavedSessionChecks.Run(); Run(); }
        static void CheckLivePlayer(MainMenuScene menu,MainMenuPlatform platform,Vector2 size)
        {
            var savedName=PlayerPrefs.GetString("battlecities.playerName",null);
            bool hadHighScore=PlayerPrefs.HasKey("battlecities.highScore");int savedHighScore=PlayerPrefs.GetInt("battlecities.highScore");
            Call(menu,"EnsureApiClient");
            var api=(MainMenuApiClient)typeof(MainMenuScene).GetField("apiClient",Private).GetValue(menu);
            var oldOverride=api.EditorStartRoutineOverride;api.EditorStartRoutineOverride=_=>{};
            try
            {
                Call(menu,"BindApiClient");
                var label=(Text)typeof(MainMenuScene).GetField("playerLabel",Private).GetValue(menu);
                var high=(Text)typeof(MainMenuScene).GetField("highScoreLabel",Private).GetValue(menu);
                var publish=typeof(MainMenuApiClient).GetMethod("PublishPlayer",Private);
                foreach(var name in new[]{"Returned wallet player","Shop connected player"})
                {
                    var player=new MainMenuApiClient.PlayerSnapshot {id="fixture-wallet",provider="wallet",walletAddress="11111111111111111111111111111111",
                        displayName=name,highscorePrimary=12345,level=7,levelPoints=250,levelPointsRequired=1000};
                    publish.Invoke(api,new object[]{player});
                    Check(label.text==name,"wallet player event immediately refreshes Commander on "+platform);
                    menu.ApplyLayout(platform,size);ScreenContent.BindAuthored(menu.Content,"home");
                    for(int frame=0;frame<5;frame++)foreach(var binding in menu.Content.GetComponentsInChildren<AuthoredScreenElement>(true))binding.Apply();
                    Check(label.text==name,"layout and repeated OTA refreshes preserve live player name on "+platform);
                    Check(high.text=="012345","wallet profile high score refreshes on "+platform);
                    Check(menu.Content.Find("Status Bar/Player/Readout/Value").GetComponent<Text>().text=="LVL 7","wallet profile level refreshes on "+platform);
                    Check(Mathf.Approximately(menu.Content.Find("Status Bar/Player/Readout/Progress/Fill").GetComponent<Image>().fillAmount,.25f),"wallet profile progress refreshes on "+platform);
                    Check(PlayerPrefs.GetString("battlecities.playerName")==name,"latest player name is cached for reload on "+platform);
                }
                menu.SetPlayer("COMMANDER",0,0);Call(menu,"BindApiClient");
                foreach(var binding in menu.Content.GetComponentsInChildren<AuthoredScreenElement>(true))binding.Apply();
                Check(label.text=="Shop connected player","rebinding replays the latest verified player snapshot on "+platform);
            }
            finally
            {
                api.EditorStartRoutineOverride=oldOverride;
                if(savedName==null)PlayerPrefs.DeleteKey("battlecities.playerName");else PlayerPrefs.SetString("battlecities.playerName",savedName);
                if(hadHighScore)PlayerPrefs.SetInt("battlecities.highScore",savedHighScore);else PlayerPrefs.DeleteKey("battlecities.highScore");
                PlayerPrefs.Save();
            }
        }
        public static void Run()
        {
            checks=0;ScreenUpdates.EditorReset();
            var source=ScreenUpdates.ReadDocument(File.ReadAllText("Content/ScreenUpdates/android-v1.json"));
            Check(source.screens.Count(x=>x.platform=="all")==18,"all eighteen screens have a baseline definition");
            foreach(var definition in source.screens.Where(x=>x.platform=="all"))
            {
                Check(definition.copy.Count+definition.patches.Length>0,definition.id+" has real downloadable display content");
                var copy=definition.copy.First();var next=Clone(source);next.screens.First(x=>x.id==definition.id).copy[copy.Key]=Tag(definition.id);
                ScreenUpdates.EditorUseDocument(next,"copy-"+definition.id);
                Check(ScreenContent.Text(definition.id,copy.Key,"fallback")==Tag(definition.id),definition.id+" copy resolves from new release");
                Check(ScreenContent.Text(definition.id,"missing","native fallback")=="native fallback",definition.id+" preserves native fallback");
                foreach(var layout in definition.layout)
                {
                    Check(ScreenContent.Number(definition.id,layout.Key,-1)==layout.Value,definition.id+" layout setting resolves");
                    var bad=Clone(source);bad.screens.First(x=>x.id==definition.id).layout[layout.Key]=float.NaN;
                    Reject(()=>ScreenUpdates.ValidateDocument(bad),"invalid numeric layout accepted");
                }
                foreach(var region in definition.regions.Take(1))
                {
                    var regionDoc=Clone(source);regionDoc.screens.First(x=>x.id==definition.id).regions[region.Key]=new[]{.1f,.1f,.7f,.7f};
                    ScreenUpdates.EditorUseDocument(regionDoc,"region-"+definition.id);
                    Check(ScreenContent.Rect(definition.id,region.Key,new Rect()).width==.7f,definition.id+" region resolves from release");
                }
            }
            var unknown=Clone(source);unknown.screens[0].copy["wallet-balance"]="1000000";Reject(()=>ScreenUpdates.ValidateDocument(unknown),"undeclared data binding accepted");
            var flows=Clone(source);flows.screens[0].flows=flows.screens[0].flows.Concat(flows.screens[0].flows).ToArray();Reject(()=>ScreenUpdates.ValidateDocument(flows),"duplicate flow parent accepted");
            var art=Clone(source);art.screens.First(x=>x.id=="shop").art["Title plate/Shop icon"]="missing-image";Reject(()=>ScreenUpdates.ValidateDocument(art),"missing artwork accepted");
            var changed=Clone(source);
            changed.screens.First(x=>x.id=="home").copy["status-bar-player-readout-label"]="REMOTE COMMANDER";
            ScreenUpdates.EditorUseDocument(changed,"legacy-live-name-copy");
            Check(ScreenContent.Text("home","status-bar-player-readout-label","Live wallet player")=="Live wallet player","legacy signed display-name copy cannot replace account identity");
            foreach(var title in Titles)
            {
                var definition=changed.screens.First(x=>x.id==title.Key);var key=definition.copy.First(x=>x.Value==title.Value).Key;definition.copy[key]=Tag(title.Key);
            }
            foreach(string sid in new[]{"quarters","treasury","manual","manualdetail","socials","select-tank","loadout"})changed.screens.First(x=>x.id==sid).copy["title"]=Tag(sid);
            changed.screens.First(x=>x.id=="home").copy["navigation-0"]=Tag("home");
            var loginDefinition=changed.screens.First(x=>x.id=="login");foreach(var key in loginDefinition.copy.Where(x=>x.Value=="ENTER THE BATTLEFIELD").Select(x=>x.Key).ToArray())loginDefinition.copy[key]=Tag("login");
            changed.screens.First(x=>x.id=="settings").patches.First(x=>x.path=="Title plate/Title").text=Tag("settings");
            ScreenUpdates.EditorUseDocument(changed,"all-screen-render-fixture");
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            foreach(string method in new[]{"EnsureSettings","EnsureShop","EnsureRanking","EnsurePlayerProfile","EnsureOperations","EnsurePreBattle"})Call(menu,method);
            var settings=UnityEngine.Object.FindFirstObjectByType<SettingsScreen>();var shop=UnityEngine.Object.FindFirstObjectByType<ShopScreen>();
            var ranking=UnityEngine.Object.FindFirstObjectByType<RankingScreen>();var profile=UnityEngine.Object.FindFirstObjectByType<PlayerProfileScreen>();
            var operations=UnityEngine.Object.FindFirstObjectByType<OperationsScreen>();var prebattle=UnityEngine.Object.FindFirstObjectByType<PreBattleScreen>();
            try
            {
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.Android,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    string target=platform==MainMenuPlatform.Web?"web":platform==MainMenuPlatform.Psg1?"psg1":"seeker";ScreenContent.EditorPlatform=target;
                    var size=platform==MainMenuPlatform.Android?new Vector2(1080,2316):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(2316,1080);
                    menu.ApplyLayout(platform,size);settings.ApplyLayout(platform);shop.ApplyLayout(platform);ranking.ApplyLayout(platform);profile.ApplyLayout(platform);prebattle.ApplyPlatformSpacing(platform);
                    foreach(var view in menu.Content.GetComponentsInChildren<RemoteScreenView>(true)){view.ApplyNow(target);Check(view.BindingValid,view.name+" bindings valid on "+target);}
                    Check(ContainsText(settings.Root,Tag("settings")),"Settings OTA title renders");
                    Check(ContainsText(shop.Root,Tag("shop")),"Shop OTA title renders");
                    Check(ContainsText(ranking.Root,Tag("ranking")),"Ranking OTA title renders");
                    Check(ContainsText(profile.Root,Tag("profile")),"Profile OTA title renders");
                    Check(ContainsText(menu.Content,Tag("home")),"Home OTA navigation renders");
                    CheckLivePlayer(menu,platform,size);
                    foreach(var page in new[]{OperationsScreen.Page.Quarters,OperationsScreen.Page.Treasury,OperationsScreen.Page.Manual,OperationsScreen.Page.ManualDetail,OperationsScreen.Page.Socials})
                    {
                        typeof(OperationsScreen).GetField("page",Private).SetValue(operations,page);Call(operations,"Render");operations.ApplyLayout(platform);
                        string id=page==OperationsScreen.Page.ManualDetail?"manualdetail":page.ToString().ToLowerInvariant();
                        Check(ContainsText(operations.Root,Tag(id)),id+" OTA title renders on "+target);
                        var view=operations.Root.GetComponent<RemoteScreenView>();view.ApplyNow(target);Check(view.BindingValid,id+" hierarchy binds");
                    }
                    var showPage=typeof(PreBattleScreen).GetMethod("ShowPage",Private);
                    showPage.Invoke(prebattle,new object[]{false});Check(ContainsText(prebattle.Root,Tag("select-tank")),"authored tank selection OTA title renders");
                    showPage.Invoke(prebattle,new object[]{true});Check(ContainsText(prebattle.Root,Tag("loadout")),"loadout OTA title renders");
                    Check(shop.Root.Find("Catalog/Filters/Category bar/ALL")||shop.Root.GetComponentsInChildren<Button>(true).Any(x=>x.name=="ALL"),"remote category caption preserves ALL object/route");
                }
                Call(profile,"BuildReplayViewer");Check(ContainsText(profile.ReplayView,Tag("replay")),"replay OTA title renders");
                var result=new GameObject("OTA result fixture").AddComponent<BattleResultsScreen>();Call(result,"Build");
                var resultRoot=(RectTransform)typeof(BattleResultsScreen).GetField("root",Private).GetValue(result);Check(ContainsText(resultRoot,Tag("battle-results")),"results OTA heading renders");
                var lobby=new GameObject("OTA lobby fixture").AddComponent<BattleCities.Multiplayer.BattleLobbyUI>();Call(lobby,"Start");Check(ContainsText(lobby.transform,Tag("lobby")),"lobby OTA heading renders");
                // Legacy login labels bind to their Canvas, not the separate flow GameObject.
                EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/Login.unity");
                var login=UnityEngine.Object.FindFirstObjectByType<LoginLayout>();
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.Android,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    login.Preview(platform);ScreenContent.BindAuthored(login.transform,"login");
                    foreach(var binding in login.GetComponentsInChildren<AuthoredScreenElement>(true))binding.Apply();
                    Check(ContainsText(login.transform,Tag("login")),"login OTA legacy text renders on "+platform);
                }
                ScreenUpdates.EditorUseDocument(source,"baseline-restored");
                foreach(var binding in login.GetComponentsInChildren<AuthoredScreenElement>(true))binding.Apply();
                Check(ContainsText(login.transform,"ENTER THE BATTLEFIELD"),"login restores baseline content");
                File.WriteAllText("Builds/ScreenUpdateChecks/migrations.txt","PASS "+checks+" checks; 18 screens; 4 platforms");
                Debug.Log("ALL SCREEN MIGRATIONS PASSED: "+checks);
            }
            finally {ScreenContent.EditorPlatform=null;ScreenUpdates.EditorReset();}
        }

        static void CaptureCanvas(Canvas canvas,int width,int height,string name)
        {
            string directory="Builds/ScreenUpdateChecks/Previews";Directory.CreateDirectory(directory);
            var camera=Camera.main;bool own=!camera;
            if(own){camera=new GameObject("OTA preview camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(2,24,61,255);}
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var target=new RenderTexture(width,height,24);Texture2D texture=null;
            try
            {
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                camera.Render();RenderTexture.active=target;texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                File.WriteAllBytes(directory+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;if(texture)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);if(own)UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
        public static void CapturePreviews()
        {
            ScreenUpdates.EditorReset();EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();foreach(var method in new[]{"EnsureSettings","EnsureShop","EnsureRanking","EnsurePlayerProfile","EnsureOperations","EnsurePreBattle"})Call(menu,method);
            var settings=UnityEngine.Object.FindFirstObjectByType<SettingsScreen>();var shop=UnityEngine.Object.FindFirstObjectByType<ShopScreen>();var ranking=UnityEngine.Object.FindFirstObjectByType<RankingScreen>();
            var profile=UnityEngine.Object.FindFirstObjectByType<PlayerProfileScreen>();var operations=UnityEngine.Object.FindFirstObjectByType<OperationsScreen>();var prebattle=UnityEngine.Object.FindFirstObjectByType<PreBattleScreen>();
            var roots=new Dictionary<string,RectTransform>{{"settings",settings.Root},{"shop",shop.Root},{"ranking",ranking.Root},{"profile",profile.Root},{"quarters",operations.Root},{"select-tank",prebattle.Root}};
            try
            {
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    string target=platform==MainMenuPlatform.Web?"web":platform==MainMenuPlatform.Psg1?"psg1":"seeker";ScreenContent.EditorPlatform=target;
                    var size=platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(1600,900);
                    foreach(var entry in roots)
                    {
                        foreach(var r in roots.Values)r.gameObject.SetActive(false);
                        menu.SetHeroVisible(false,false);menu.SetTankSelectorBackdrop(true);entry.Value.gameObject.SetActive(true);
                        menu.ApplyLayout(platform,size);settings.ApplyLayout(platform);shop.ApplyLayout(platform);ranking.ApplyLayout(platform);profile.ApplyLayout(platform);prebattle.ApplyPlatformSpacing(platform);
                        if(entry.Key=="quarters"){typeof(OperationsScreen).GetField("page",Private).SetValue(operations,OperationsScreen.Page.Quarters);Call(operations,"Render");}
                        if(entry.Key=="select-tank")typeof(PreBattleScreen).GetMethod("ShowPage",Private).Invoke(prebattle,new object[]{false});
                        foreach(var view in menu.Content.GetComponentsInChildren<RemoteScreenView>(true))view.ApplyNow(target);
                        CaptureCanvas(menu.GetComponent<Canvas>(),(int)size.x,(int)size.y,entry.Key+"-"+target);
                        if(entry.Key=="select-tank")
                        {
                            typeof(PreBattleScreen).GetMethod("ShowPage",Private).Invoke(prebattle,new object[]{true});CaptureCanvas(menu.GetComponent<Canvas>(),(int)size.x,(int)size.y,"loadout-"+target);
                        }
                    }
                }
                EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/Login.unity");var layout=UnityEngine.Object.FindFirstObjectByType<LoginLayout>();
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {layout.Preview(platform);ScreenContent.BindAuthored(layout.transform,"login");foreach(var binding in layout.GetComponentsInChildren<AuthoredScreenElement>(true))binding.Apply();CaptureCanvas(layout.GetComponent<Canvas>(),1600,900,"login-"+platform);}
                Debug.Log("SCREEN PREVIEWS COMPLETE");
            }
            finally{ScreenContent.EditorPlatform=null;ScreenUpdates.EditorReset();}
        }
        public static void RunComplete()
        {
            ScreenUpdateChecks.Run();Run();CapturePreviews();
        }

    }
}
