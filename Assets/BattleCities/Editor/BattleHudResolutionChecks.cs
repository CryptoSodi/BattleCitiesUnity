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
                    bool psg1=size.x==1240;
                    state.EquippedSecondary=SecondaryAttack.LandDrone;
                    hud.DrawPreview(state,atlas,layout,psg1?PowerupUiMode.MergedTopHud:PowerupUiMode.WebTopLeft);
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
                    var special=canvas.GetComponentsInChildren<RawImage>().SingleOrDefault(t=>t.texture&&t.texture.name=="Special icon LandDrone");
                    Check((special!=null)==psg1,"special indicator only appears on PSG1 "+size);
                    if(psg1)
                    {
                        Check(!special.raycastTarget,"special indicator does not block input");
                        float specialTop=-special.rectTransform.anchoredPosition.y;
                        Check(specialTop>=0&&specialTop+special.rectTransform.rect.height<=layout.TopHeight,"special indicator fits inside header");
                        var fourth=canvas.GetComponentsInChildren<Text>().Single(t=>t.text=="Y");
                        Check(special.rectTransform.anchoredPosition.x>fourth.rectTransform.anchoredPosition.x+fourth.rectTransform.rect.width,"special follows fourth power-up");
                    }
                    Check(score.fontSize*canvas.scaleFactor>=16,"score label remains readable "+size);
                    Near(layout.TopHeight,48,"single-row header at every resolution "+size);
                    var enemies=canvas.GetComponentsInChildren<Text>().Single(t=>t.text=="ENEMIES");
                    if(psg1)Check(enemies.rectTransform.anchoredPosition.x<=360,"PSG1 metrics use compact spacing");
                    Check(Mathf.Abs(enemies.rectTransform.anchoredPosition.y-score.rectTransform.anchoredPosition.y)<=2,"enemies remain alongside score "+size);
                    var enemyTexture=Resources.Load<Texture2D>("HudEnemy");
                    var roster=canvas.GetComponentsInChildren<RawImage>().Where(image=>image.texture==enemyTexture).ToArray();
                    Check(roster.Length==state.TotalEnemies&&roster.All(image=>Mathf.Abs(image.rectTransform.rect.width/image.rectTransform.rect.height-(float)enemyTexture.width/enemyTexture.height)<.05f),"enemy roster keeps artwork proportions "+size);
                    if(psg1)Check(roster.All(image=>image.rectTransform.rect.width>=26),"PSG1 enemies use reclaimed header space");
                    Near(canvas.scaleFactor,layout.Scale,"canvas uses one scale "+size);
                    camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                    var picture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    picture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);picture.Apply();
                    File.WriteAllBytes(Output+"/hud-"+size.x+"x"+size.y+".png",picture.EncodeToPNG());
                    if(psg1)
                    {
                        var strip=new Texture2D(size.x,60,TextureFormat.RGB24,false);
                        strip.ReadPixels(new Rect(0,size.y-60,size.x,60),0,0);strip.Apply();
                        File.WriteAllBytes(Output+"/psg-compact-header.png",strip.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(strip);
                        foreach(var ability in new[]{SecondaryAttack.Mine,SecondaryAttack.PatrolDrone,SecondaryAttack.GroundTurret,SecondaryAttack.LandDrone})
                        foreach(bool cooling in new[]{false,true})
                        {
                            state.EquippedSecondary=ability;
                            float duration=ability==SecondaryAttack.Mine?BattleSimulation.SecondaryCooldownSeconds:4;
                            typeof(BattleSimulation).GetProperty("SecondaryCooldown").SetValue(state,cooling?duration*.5f:0f);
                            hud.DrawPreview(state,atlas,layout,PowerupUiMode.MergedTopHud);
                            var rings=canvas.GetComponentsInChildren<Image>().Where(i=>i.type==Image.Type.Filled).ToArray();
                            Check(rings.Length==3&&Mathf.Abs(rings.Last().fillAmount-(cooling?.5f:1f))<.001f,"cooldown sweep tracks simulation "+ability+" "+cooling);
                            Check(canvas.GetComponentsInChildren<RawImage>().Any(i=>i.texture&&i.texture.name=="Special icon "+ability),"actual model icon loaded "+ability);
                            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                            picture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);picture.Apply();
                            File.WriteAllBytes(Output+"/psg-special-"+ability+(cooling?"-cooldown":"-ready")+".png",picture.EncodeToPNG());
                            var detail=new Texture2D(240,130,TextureFormat.RGB24,false);
                            detail.ReadPixels(new Rect(size.x-240,size.y-130,240,130),0,0);detail.Apply();
                            File.WriteAllBytes(Output+"/psg-special-"+ability+(cooling?"-cooldown":"-ready")+"-detail.png",detail.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(detail);
                        }
                    }
                    RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(picture);
                    camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);
                }
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(3840,2160)})
                {
                    var target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;camera.aspect=(float)size.x/size.y;
                    foreach(bool orthographic in new[]{true,false})foreach(float zoom in new[]{.7f,1f,2f})
                    {
                        camera.orthographic=orthographic;camera.orthographicSize=7/zoom;camera.fieldOfView=65;
                        var position=Vector3.zero;float worldSize=BattlePickupSizing.WorldSize;
                        var top=camera.WorldToScreenPoint(position+camera.transform.up*worldSize*.5f);
                        var bottom=camera.WorldToScreenPoint(position-camera.transform.up*worldSize*.5f);
                        Near(top.y-bottom.y,BattlePickupSizing.ScreenSize(camera,position),"live and replay pickup sizes match "+size+" "+orthographic+" "+zoom);
                        Near(worldSize,.9f,"pickup remains 90 percent of a world tile "+size+" "+orthographic+" "+zoom);
                    }
                    camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);
                }
                var phone=new BattleHudLayout(844,390);var retina=new BattleHudLayout(1688,780,2);
                Near(phone.Width,retina.Width,"retina keeps logical phone width");
                Check(phone.Narrow&&retina.Narrow,"retina keeps compact single-row spacing");
                Near(phone.TopHeightPixels,retina.TopHeightPixels/2,"retina keeps visible header size");
                var hd=new BattleHudLayout(1920,1080);var uhd=new BattleHudLayout(3840,2160);
                Near(hd.Width,uhd.Width,"4K keeps logical HUD layout");
                Near(uhd.TopHeightPixels,hd.TopHeightPixels*2,"4K doubles HUD pixels");
                Debug.Log("HUD resolution checks passed: "+checks+". Previews: "+Output);
            }
            finally {hud.Dispose();UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
