using System;
using System.Collections;
using System.IO;
using System.Linq;
using BattleCities.Core;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class TvReplayChecks
    {
        public static string Status {get;private set;}="Not started";
        static int checks,failures,requests;
        static void Check(bool ok,string message){checks++;if(!ok){failures++;Debug.LogError("TV replay: "+message);}}
        [MenuItem("Battle Cities/Checks/TV match replay (Play mode)")]
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play mode.");
            checks=failures=requests=0;Status="Running";
            var liveScene=SceneManager.GetActiveScene();
            var priorFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            var preview=EditorSceneManager.OpenPreviewScene("Assets/BattleCities/Scenes/MainMenu.unity");
            GameObject clone=null;
            try
            {
                var source=preview.GetRootGameObjects().First(g=>g.GetComponent<MainMenuScene>());
                source.SetActive(false);clone=UnityEngine.Object.Instantiate(source);clone.name="Hidden TV replay UI fixture";
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            var scene=SceneManager.CreateScene("Hidden TV replay UI checks");SceneManager.MoveGameObjectToScene(clone,scene);
            var menu=clone.GetComponent<MainMenuScene>();menu.enabled=false;
            var api=clone.GetComponent<MainMenuApiClient>();if(!api)api=clone.AddComponent<MainMenuApiClient>();
            api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);api.Configure("http://127.0.0.1:18763");
            var map=Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text);
            var simulation=new BattleSimulation(map);var recorder=new ReplayRecorder(simulation,map,"tv-test");
            for(int i=0;i<180;i++){recorder.BeforeStep(default);simulation.Step(default);recorder.AfterStep();}
            recorder.Finish("aborted");var recording=recorder.Data;
            api.EditorRequestOverride=(method,path,payload,done)=>Reply(method,path,payload,done,recording);
            var cameraRoot=new GameObject("Hidden menu capture camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.17f,.25f);camera.cullingMask=1<<5;
            foreach(var child in clone.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            var canvas=clone.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            clone.GetComponent<GraphicRaycaster>().enabled=false;
            clone.SetActive(true);
            var runner=clone.AddComponent<TvReplayCheckRunner>();runner.StartCoroutine(Verify(menu,api,camera,recording,scene,liveScene,priorFocus));
        }
        static IEnumerator Verify(MainMenuScene menu,MainMenuApiClient api,Camera camera,BattleReplay recording,Scene fixtureScene,Scene liveScene,GameObject priorFocus)
        {
            var priorActive=RenderTexture.active;PlayerProfileScreen page=null;
            try
            {
                menu.OpenPlayerProfile("ply-profile-fixture");page=menu.GetComponent<PlayerProfileScreen>();
                float timeout=Time.realtimeSinceStartup+3;while(page.IsLoading&&Time.realtimeSinceStartup<timeout)yield return null;
                Check(page.State==PlayerProfileScreen.ViewState.Ready,"local public profile fixture loads");
                int actionCount=InputSystem.ListEnabledActions().Count;
                var masks=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).ToDictionary(c=>c,c=>c.cullingMask);
                var identity=page.Data;
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(page.BattleButtons[0].gameObject);
                page.Scroll.verticalNormalizedPosition=.42f;float scrollPosition=page.Scroll.verticalNormalizedPosition;
                page.Watch(0);timeout=Time.realtimeSinceStartup+3;while(!page.ReplayViewerOpen&&Time.realtimeSinceStartup<timeout)yield return null;
                Check(page.ReplayViewerOpen&&requests==1,"match WATCH downloads and opens the TV viewer");
                var game=page.ReplayGame;
                Check(game&&game.IsTvReplay&&game.IsReplaying,"TV uses deterministic replay renderer");
                Check(SceneManager.GetActiveScene()==liveScene,"WATCH keeps the active scene and menu surroundings");
                Check(InputSystem.ListEnabledActions().Count==actionCount,"playback enables no gameplay input actions");
                Check(!game.GetComponent<EconomyClient>()&&!game.GetComponent<BattleTouchControls>()&&!game.GetComponent<BattleCities.Multiplayer.BattleNetworkMatch>(),"replay has no inventory, touch, or network components");
                Check(game.GetComponentsInChildren<AudioListener>().Length==0,"replay creates no extra audio listener");
                Check(game.TvReplayCamera.targetTexture&&game.TvReplayCamera.cullingMask==(1<<BattleGame.TvReplayLayer)&&game.TvReplayCamera.rect==new Rect(0,0,1,1),"replay camera renders only to its full TV texture");
                game.Paused=true;int tick=game.Simulation.Tick;yield return null;yield return null;
                Check(game.Simulation.Tick==tick,"pause stops simulation");
                Check(game.GetComponentsInChildren<Transform>().All(t=>t.gameObject.layer==BattleGame.TvReplayLayer),"all replay models and effects are isolated from menu cameras");
                foreach(var size in new[]{new Vector2Int(1583,924),new Vector2Int(844,390),new Vector2Int(1240,1080)})
                {
                    var platform=size.x==1583?MainMenuPlatform.Web:size.x==844?MainMenuPlatform.AndroidLandscape:MainMenuPlatform.Psg1;
                    yield return Capture(menu,camera,size,platform,page);
                    Check(page.ReplayView.IsChildOf(page.Root)&&page.ReplayView.rect.size==page.Root.rect.size,platform+" replay stays in profile TV bounds");
                    var picture=page.ReplayView.Find("Viewport/Battle picture").GetComponent<RawImage>();
                    Check(picture.texture==game.TvReplayCamera.targetTexture,platform+" picture uses isolated replay camera");
                }
                game.RestartReplay();game.Paused=true;Check(game.Simulation.Tick==0,"restart resets the saved match");
                game.ReplaySpeed=4;game.Paused=false;
                timeout=Time.realtimeSinceStartup+4;while(!game.ReplayPlayback.Complete&&game.ReplayPlayback.Error==null&&Time.realtimeSinceStartup<timeout)yield return null;
                Check(game.ReplayPlayback.Complete&&game.ReplayPlayback.Error==null,"TV playback reproduces recorded final state");
                var rendererScene=game.gameObject.scene;var texture=game.TvReplayCamera.targetTexture;
                menu.Back();Check(!page.ReplayViewerOpen&&menu.IsPlayerProfileOpen,"cancel returns to profile, not Home/gameplay");
                Check(page.Data==identity&&Mathf.Abs(page.Scroll.verticalNormalizedPosition-scrollPosition)<.001f,"profile records and scroll position are preserved");
                Check(!EventSystem.current||EventSystem.current.currentSelectedGameObject==page.BattleButtons[0].gameObject,"Back restores selected match");
                yield return null;yield return null;
                Check(!texture&&(!rendererScene.IsValid()||!rendererScene.isLoaded),"Back releases render texture and renderer scene");
                Check(masks.All(pair=>!pair.Key||pair.Key.cullingMask==pair.Value),"Back restores all camera masks");
                Check(SceneManager.GetActiveScene()==liveScene,"closing playback preserves the user's current game scene");
                foreach(var control in page.ReplayView.GetComponentsInChildren<Selectable>(true))
                    if(control.gameObject.activeInHierarchy&&control.interactable)
                    {
                        var nav=control.navigation;
                        Check(new[]{nav.selectOnUp,nav.selectOnDown,nav.selectOnLeft,nav.selectOnRight}.All(s=>!s||s.interactable),"completed playback navigation skips disabled Pause");
                    }
                PlayerProfileReplayChecks.Run();timeout=Time.realtimeSinceStartup+8;
                while(PlayerProfileReplayChecks.Status=="Running"&&Time.realtimeSinceStartup<timeout)yield return null;
                var apiResult=System.Text.RegularExpressions.Regex.Match(PlayerProfileReplayChecks.Status,@": (\d+)/(\d+) checks passed\.");
                Check(apiResult.Success&&apiResult.Groups[1].Value==apiResult.Groups[2].Value,"public WATCH missing, malformed, incompatible, legacy and stale responses are handled");
            }
            finally
            {
                if(page)page.CloseReplayViewer();RenderTexture.active=priorActive;
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(priorFocus);
                SceneManager.UnloadSceneAsync(fixtureScene);
            }
            Status=$"TV match replay: {checks-failures}/{checks} checks passed.";Debug.Log(Status);
        }
        static IEnumerator Reply(string method,string path,JObject payload,Action<long,JObject,string> done,BattleReplay replay)
        {
            Check(method=="GET"&&payload==null,"spectating performs read-only requests");yield return null;
            if(path.EndsWith("/replay")){requests++;done(200,new JObject{["item"]=new JObject{["replay"]=JObject.Parse(ReplayJson.Write(replay))}},null);}
            else done(200,PlayerProfileChecks.Fixture(),null);
        }
        static IEnumerator Capture(MainMenuScene menu,Camera camera,Vector2Int size,MainMenuPlatform platform,PlayerProfileScreen page)
        {
            var target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;Texture2D image=null;
            try
            {
                Canvas.ForceUpdateCanvases();menu.ApplyLayout(platform,size);Canvas.ForceUpdateCanvases();
                yield return null;
                page.ReplayGame.TvReplayCamera.Render();camera.Render();RenderTexture.active=target;
                image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
                string directory="C:/Users/tassa/OneDrive/Documents/ChatGPT/BattleCitiesUnity/ReplayTvChecks";Directory.CreateDirectory(directory);File.WriteAllBytes(directory+"/tv-replay-"+platform+".png",image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(target);if(image)UnityEngine.Object.Destroy(image);}
        }
    }
    public sealed class TvReplayCheckRunner:MonoBehaviour {}
}