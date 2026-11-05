using System;
using System.IO;
using System.Reflection;
using BattleCities.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ResponsiveWebChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("RESPONSIVE WEB: "+why);}
            var samples=new[]{(1920,1080,MainMenuPlatform.Web,"desktop"),(1024,768,MainMenuPlatform.AndroidLandscape,"compact"),
                (1080,1240,MainMenuPlatform.Psg1,"portrait-compact"),(844,390,MainMenuPlatform.AndroidLandscape,"phone-landscape"),
                (390,844,MainMenuPlatform.Psg1,"phone-portrait")};
            foreach(var v in samples)Check(ResponsiveWebLayout.ForViewport(v.Item1,v.Item2)==v.Item3,"viewport selection "+v.Item4);
            Check(ResponsiveWebLayout.ForViewport(844,390)==ResponsiveWebLayout.ForViewport(1688/2f,780/2f),"CSS viewport selection is independent of pixel density");
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity",OpenSceneMode.Single);
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();Check(menu,"menu available");
            ScreenContent.EditorPlatform="android";
            var savedFullscreen=menu.Content.Find("Web fullscreen");
            Check(savedFullscreen&&!savedFullscreen.gameObject.activeSelf,"saved fullscreen is hidden by default");
            savedFullscreen.gameObject.SetActive(true);
            menu.ApplyLayout(MainMenuPlatform.AndroidLandscape,new Vector2(1920,1080));
            Check(!savedFullscreen.gameObject.activeSelf,"native layout hides an active saved fullscreen object before its button is initialized");
            ScreenContent.EditorPlatform="web";
            var savedSettings=typeof(MainMenuScene).GetField("settingsButton",Private);
            var sceneSettings=menu.Content.Find("Status Bar/Player").GetComponent<Button>();
            savedSettings.SetValue(menu,null);
            menu.ApplyLayout(MainMenuPlatform.Web,new Vector2(1920,1080));
            Check(savedSettings.GetValue(menu)==sceneSettings,"missing saved Settings reference is recovered from the scene");
            menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(1080,1240));
            Check(savedSettings.GetValue(menu)==sceneSettings,"web preview does not replace the persistent Settings reference");
            typeof(MainMenuScene).GetMethod("ResetStartupView",Private).Invoke(menu,null);
            var cam=Camera.main;if(!cam)cam=new GameObject("Responsive preview camera",typeof(Camera)).GetComponent<Camera>();
            var canvas=menu.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;
            Directory.CreateDirectory("Builds/ResponsiveWebPreviews");
            foreach(var v in samples){
                BattleHudLayout.PreviewMobileBrowser=v.Item4.StartsWith("phone");
                menu.ApplyLayout(v.Item3,new Vector2(v.Item1,v.Item2));Canvas.ForceUpdateCanvases();
                var status=menu.Content.Find("Status Bar");var main=menu.Content.Find("Main Display");var navigation=menu.Content.Find("Navigation");
                Check(status&&status.gameObject.activeInHierarchy&&main&&main.gameObject.activeInHierarchy&&navigation&&navigation.gameObject.activeInHierarchy,v.Item4+" home controls visible");
                var full=menu.Content.Find("Web fullscreen");var settings=menu.Content.Find("Web settings");
                Check(full&&full.gameObject.activeInHierarchy&&settings&&settings.gameObject.activeInHierarchy,v.Item4+" web actions remain available");
                Check(!menu.Content.Find("PSG1 Controls").gameObject.activeInHierarchy,v.Item4+" no device-specific controller legend");
                if(v.Item3!=MainMenuPlatform.Web){
                    Check(!menu.Content.Find("Leaderboard").gameObject.activeInHierarchy,v.Item4+" compact home uses Ranking navigation");
                    float actual=((RectTransform)full).rect.width*Mathf.Abs(menu.Content.localScale.x);
                    Check(actual>=(v.Item4.StartsWith("phone")?63.9f:47.9f),v.Item4+" usable fullscreen target");
                    if(v.Item3==MainMenuPlatform.Psg1){
                        var opening=main.Find("TV Background Viewport") as RectTransform;
                        var background=opening.Find("TV Background") as RectTransform;
                        var o=new Vector3[4];var b=new Vector3[4];opening.GetWorldCorners(o);background.GetWorldCorners(b);
                        Check(b[0].x<=o[0].x+.1f&&b[0].y<=o[0].y+.1f&&b[2].x>=o[2].x-.1f&&b[2].y>=o[2].y-.1f,v.Item4+" background covers the entire TV opening");
                        var f=new Vector3[4];var s=new Vector3[4];((RectTransform)full).GetWorldCorners(f);((RectTransform)settings).GetWorldCorners(s);
                        Check(f[2].x<o[2].x&&s[0].x>=o[0].x,v.Item4+" header actions inset from the TV frame");
                    }
                    if(v.Item3==MainMenuPlatform.AndroidLandscape){
                        var frame=main.Find("TV Background Viewport") as RectTransform;
                        var f=new Vector3[4];var s=new Vector3[4];var h=new Vector3[4];var o=new Vector3[4];
                        ((RectTransform)full).GetWorldCorners(f);((RectTransform)settings).GetWorldCorners(s);((RectTransform)status).GetWorldCorners(h);frame.GetWorldCorners(o);
                        Check(f[2].x<=o[2].x+.1f&&s[0].x>=o[0].x-.1f,v.Item4+" actions inside TV opening");
                        Check(s[0].x>=h[2].x-.1f,v.Item4+" actions do not overlap the status header");
                        Check(Mathf.Abs(f[2].y-h[2].y)<.1f||Mathf.Abs(f[2].y-o[2].y)<5f,v.Item4+" actions align with the status header");
                    }
                }
                Capture(cam,v.Item1,v.Item2,"home-"+v.Item4);
            }
            menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(1024,768));menu.OpenShop();Canvas.ForceUpdateCanvases();
            var shop=menu.GetComponent<ShopScreen>();Check(shop&&shop.IsConfigured,"shop opened");
            var usePlay=typeof(ShopScreen).GetField("usePlay",Private);
            Check(!(bool)usePlay.GetValue(shop),"compact web keeps SKR currency");
            menu.ApplyLayout(MainMenuPlatform.AndroidLandscape,new Vector2(844,390));Canvas.ForceUpdateCanvases();
            Check(!(bool)usePlay.GetValue(shop),"phone web keeps SKR currency after resize");
            Capture(cam,844,390,"shop-phone-landscape");
            menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(390,844));Canvas.ForceUpdateCanvases();
            Check(shop.IsOpen&&!(bool)usePlay.GetValue(shop),"portrait resize preserves the open shop and SKR");
            Capture(cam,390,844,"shop-phone-portrait");
            ScreenContent.EditorPlatform="psg1";menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(1080,1240));
            Check((bool)usePlay.GetValue(shop),"native PSG1 retains PLAY currency");
            ScreenContent.EditorPlatform="web";menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(1024,768));
            Check(!(bool)usePlay.GetValue(shop),"return to web restores SKR");
            menu.OpenRanking();menu.ApplyLayout(MainMenuPlatform.AndroidLandscape,new Vector2(844,390));Canvas.ForceUpdateCanvases();
            Check(menu.IsRankingOpen,"Ranking remains available without home leaderboard");Capture(cam,844,390,"ranking-phone-landscape");
            menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(390,844));Canvas.ForceUpdateCanvases();
            Check(menu.IsRankingOpen,"Ranking remains open through portrait resize");Capture(cam,390,844,"ranking-phone-portrait");
            menu.StartBattle();menu.ApplyLayout(MainMenuPlatform.AndroidLandscape,new Vector2(844,390));Canvas.ForceUpdateCanvases();
            var preparation=menu.GetComponent<PreBattleScreen>();Check(preparation&&preparation.IsOpen,"tank selection remains available on compact web");
            Capture(cam,844,390,"tank-selection-phone-landscape");
            menu.ApplyLayout(MainMenuPlatform.Psg1,new Vector2(390,844));Canvas.ForceUpdateCanvases();
            Check(preparation.IsOpen,"tank selection survives portrait resize");Capture(cam,390,844,"tank-selection-phone-portrait");
            File.Copy(BattleResultsChecks.Capture(844,390,"responsive-web-results-landscape",1),"Builds/ResponsiveWebPreviews/results-phone-landscape.png",true);
            File.Copy(BattleResultsChecks.Capture(390,844,"responsive-web-results-portrait",1),"Builds/ResponsiveWebPreviews/results-phone-portrait.png",true);
            BattleHudLayout.PreviewMobileBrowser=false;
            Debug.Log("RESPONSIVE WEB PASS: five viewport profiles, web actions, controller legend and SKR/PLAY identity. Previews: Builds/ResponsiveWebPreviews.");
        }
        static void Capture(Camera camera,int width,int height,string name)
        {
            var old=camera.targetTexture;var previous=RenderTexture.active;
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Builds/ResponsiveWebPreviews/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
