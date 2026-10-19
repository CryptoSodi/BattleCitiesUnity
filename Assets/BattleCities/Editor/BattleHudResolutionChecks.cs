using System;
using System.IO;
using System.Linq;
using BattleCities.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class BattleHudResolutionChecks
    {
        static int checks;
        static void Check(bool ok,string name){checks++;if(!ok)throw new Exception("HUD RESOLUTION: "+name);}
        static void Near(float a,float b,string name)=>Check(Mathf.Abs(a-b)<.05f,name+" ("+a+", "+b+")");
        static readonly string Output="C:/Users/tassa/OneDrive/Documents/ChatGPT/BattleCitiesUnity/HudResolutionPreviews";
        public static void Run()
        {
            checks=0;Directory.CreateDirectory(Output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var state=new BattleSimulation(new MapData());
            var hud=new BattleHud();
            var atlas=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>().PowerupAtlas;
            var camera=new GameObject("HUD check camera").AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.20f,.22f);
            try
            {
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(3840,2160),new Vector2Int(3440,1440),new Vector2Int(844,390),new Vector2Int(1688,780),new Vector2Int(1240,1080),new Vector2Int(720,1280)})
                {
                    var layout=new BattleHudLayout(size.x,size.y,size.x==1688?2:1);
                    var target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;camera.aspect=(float)size.x/size.y;
                    hud.DrawPreview(state,atlas,layout,PowerupUiMode.WebTopLeft);
                    var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                    Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();
                    var header=canvas.GetComponentsInChildren<RawImage>().First();
                    var corners=new Vector3[4];header.rectTransform.GetWorldCorners(corners);
                    var bottom=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
                    var top=RectTransformUtility.WorldToScreenPoint(camera,corners[1]);
                    Near(top.y-bottom.y,layout.TopHeightPixels,"actual scaled header height "+size);
                    Near(header.rectTransform.rect.width*canvas.scaleFactor,size.x,"header spans viewport "+size);
                    var score=canvas.GetComponentsInChildren<Text>().Single(t=>t.text=="SCORE");
                    Check(score.fontSize*canvas.scaleFactor>=16,"score label remains readable "+size);
                    Near(canvas.scaleFactor,layout.Scale,"canvas uses one scale "+size);
                    camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                    var picture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    picture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);picture.Apply();
                    File.WriteAllBytes(Output+"/hud-"+size.x+"x"+size.y+".png",picture.EncodeToPNG());
                    RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(picture);
                    camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);
                }
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(3840,2160)})
                {
                    var target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;
                    foreach(bool orthographic in new[]{true,false})foreach(float zoom in new[]{.7f,1f,2f})
                    {
                        camera.orthographic=orthographic;camera.orthographicSize=7/zoom;camera.fieldOfView=65;
                        var position=Vector3.zero;float worldSize=BattleHudLayout.PickupWorldSize(camera,position);
                        var top=camera.WorldToScreenPoint(position+camera.transform.up*worldSize*.5f);
                        var bottom=camera.WorldToScreenPoint(position-camera.transform.up*worldSize*.5f);
                        Near(top.y-bottom.y,new BattleHudLayout(size.x,size.y).PickupSizePixels,"pickup projection stays constant through zoom "+size+" "+orthographic+" "+zoom);
                    }
                    camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);
                }
                var phone=new BattleHudLayout(844,390);var retina=new BattleHudLayout(1688,780,2);
                Near(phone.Width,retina.Width,"retina keeps logical phone width");
                Check(phone.Narrow&&retina.Narrow,"retina keeps compact two-row header");
                Near(phone.TopHeightPixels,retina.TopHeightPixels/2,"retina keeps visible header size");
                Near(phone.PickupSizePixels,retina.PickupSizePixels/2,"retina keeps visible pickup size");
                var hd=new BattleHudLayout(1920,1080);var uhd=new BattleHudLayout(3840,2160);
                Near(hd.Width,uhd.Width,"4K keeps logical HUD layout");
                Near(uhd.TopHeightPixels,hd.TopHeightPixels*2,"4K doubles HUD pixels");
                Near(uhd.PickupSizePixels,hd.PickupSizePixels*2,"4K doubles pickup pixels");
                Debug.Log("HUD resolution checks passed: "+checks+". Previews: "+Output);
            }
            finally {hud.Dispose();UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
