using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.Core;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class AndroidChromeHudChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            const string output="Builds/AndroidChromeHud";Directory.CreateDirectory(output);
            var camera=new GameObject("Chrome HUD preview",typeof(Camera)).GetComponent<Camera>();
            camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.20f,.22f);
            var host=new GameObject("Isolated stock fixture");host.SetActive(false);
            var game=host.AddComponent<BattleGame>();var state=new BattleSimulation(new MapData());
            typeof(BattleGame).GetProperty("Simulation").GetSetMethod(true).Invoke(game,new object[]{state});
            typeof(BattleGame).GetMethod("ConfigureGameplayInput",Private).Invoke(game,null);
            game.PowerupAtlas=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>().PowerupAtlas;
            var economy=host.AddComponent<EconomyClient>();
            typeof(EconomyClient).GetProperty("Authenticated").GetSetMethod(true).Invoke(economy,new object[]{true});
            typeof(BattleGame).GetField("economy",Private).SetValue(game,economy);
            var controls=host.AddComponent<BattleTouchControls>();controls.Initialize(game);
            var touchCanvas=host.GetComponentInChildren<Canvas>(true);
            touchCanvas.renderMode=RenderMode.ScreenSpaceCamera;touchCanvas.worldCamera=camera;touchCanvas.planeDistance=.8f;
            touchCanvas.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            // Editor-only fixtures never enter Play Mode or contact an account/API.
            host.SetActive(true);
            var hud=new BattleHud();int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception("CHROME HUD: "+why);checks++;}
            try{
                foreach(var viewport in new[]{new Vector3(800,373,1),new Vector3(1600,746,2),new Vector3(2400,1119,3),new Vector3(1688,780,2)})
                foreach(bool equipped in new[]{false,true}){
                    typeof(EconomyClient).GetField("account",Private).SetValue(economy,equipped?JObject.Parse("{\"loadout\":{\"active-one\":\"shield\",\"active-two\":\"freeze\",\"active-three\":\"speed\",\"active-four\":\"upgrade\"},\"inventory\":{\"shield\":3,\"freeze\":2,\"speed\":1,\"upgrade\":4}} "):new JObject());
                    BattleHudLayout.PreviewPixelRatio=viewport.z;
                    BattleHudLayout.PreviewMobileBrowser=true;
                    var layout=BattleHudLayout.ForViewport(viewport.x,viewport.y);
                    Check(Mathf.Abs(layout.Scale-new BattleHudLayout(viewport.x,viewport.y).Scale)<.01f,"Chrome HUD uses the same viewport scale as the APK");
                    var target=new RenderTexture((int)viewport.x,(int)viewport.y,24);target.Create();camera.targetTexture=target;
                    hud.DrawPreview(state,game.PowerupAtlas,layout,PowerupUiMode.ExternalController,economy);
                    var canvas=UnityEngine.Object.FindObjectsByType<Canvas>().Single(c=>c.name=="Battle HUD");
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                    Canvas.ForceUpdateCanvases();typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);Canvas.ForceUpdateCanvases();
                    foreach(var text in canvas.GetComponentsInChildren<Text>()){
                        Check(text.font==ArcadeTextStyles.HeadingFont,"all HUD text uses bundled Barlow");
                        var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
                        text.cachedTextGenerator.Populate(text.text,settings);
                        settings.fontSize=Mathf.RoundToInt(text.cachedTextGenerator.fontSizeUsedForBestFit/text.pixelsPerUnit);settings.resizeTextForBestFit=false;
                        float fittedWidth=text.cachedTextGenerator.GetPreferredWidth(text.text,settings)/text.pixelsPerUnit;
                        Check(fittedWidth<=text.rectTransform.rect.width+1,"text fits allocated width: "+text.text+" at "+viewport+" width="+fittedWidth+" cell="+text.rectTransform.rect.width+" font="+settings.fontSize+" scale="+text.pixelsPerUnit);
                    }
                    var enemy=Resources.Load<Texture2D>("HudEnemy");
                    var player=Resources.Load<Texture2D>("HudPlayer");
                    var lifeIcons=canvas.GetComponentsInChildren<RawImage>().Where(i=>i.texture==player).ToArray();
                    Check(lifeIcons.Length==4,"mobile has one score icon and three life tanks");
                    Check(lifeIcons.All(i=>Mathf.Abs(i.rectTransform.rect.width/i.rectTransform.rect.height-(float)player.width/player.height)<.01f),"tank artwork retains its original proportions");
                    var roster=canvas.GetComponentsInChildren<RawImage>().Where(i=>i.texture==enemy).ToArray();
                    Check(roster.Length==20,"all twenty enemy icons retained");
                    Check(roster.All(i=>i.rectTransform.rect.width>=(equipped?10:20)),"enemy artwork uses available header space");
                    var slots=controls.GetComponentsInChildren<BattleTouchControl>(true).Where(c=>c.name.StartsWith("Powerup ")).ToArray();
                    Check(slots.All(c=>c.transform.Find("Icon").GetComponent<RawImage>().enabled==equipped),"equipped slot visibility preserved");
                    if(equipped){
                        var rosterCorners=new Vector3[4];roster.Last().rectTransform.GetWorldCorners(rosterCorners);
                        var slotCorners=new Vector3[4];((RectTransform)slots.Single(c=>c.name=="Powerup 1").transform).GetWorldCorners(slotCorners);
                        Check(RectTransformUtility.WorldToScreenPoint(camera,rosterCorners[2]).x<RectTransformUtility.WorldToScreenPoint(camera,slotCorners[0]).x,"enemy roster stays clear of power-up touch targets");
                    }
                    Check(layout.TopHeight==48,"header remains a single row");
                    camera.Render();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                    var strip=new Texture2D(target.width,Mathf.CeilToInt(layout.TopHeightPixels+4),TextureFormat.RGB24,false);
                    strip.ReadPixels(new Rect(0,target.height-strip.height,strip.width,strip.height),0,0);strip.Apply();
                    File.WriteAllBytes(output+"/chrome-"+target.width+(equipped?"-equipped":"-empty")+".png",strip.EncodeToPNG());
                    RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(strip);camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                }
                Debug.Log("ANDROID CHROME HUD CHECKS PASSED: "+checks+". Previews: "+output);
            }finally{BattleHudLayout.PreviewPixelRatio=1;BattleHudLayout.PreviewMobileBrowser=false;hud.Dispose();UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
