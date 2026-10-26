using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class SpecialIconChecks
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath("artifacts/special-icons");Directory.CreateDirectory(folder);
            var cacheType=typeof(BattleHud).Assembly.GetType("BattleCities.BattleSpecialIconCache");
            var cache=Activator.CreateInstance(cacheType,true);int checks=0;
            Texture Get(SecondaryAttack special,int slot=0)=>(Texture)cacheType.GetMethod("Get").Invoke(cache,new object[]{special,slot});
            Texture Cached(SecondaryAttack special,int slot=0)=>(Texture)cacheType.GetMethod("Cached").Invoke(cache,new object[]{special,slot});
            void Check(bool ok,string message){checks++;if(!ok)throw new Exception("SPECIAL ICON: "+message);}
            void Pixels(Texture texture,string file)
            {
                var target=texture as RenderTexture;Check(target&&target.IsCreated(),"created preview "+file);
                var previous=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                try
                {
                    RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                    var pixels=image.GetPixels32();int visible=pixels.Count(p=>p.a>64&&(p.r>20||p.g>20||p.b>20));
                    Check(visible>256,"visible actual model pixels "+file+": "+visible);
                    Check(pixels.Count(p=>p.a<16)>pixels.Length/20,"transparent image margin "+file);
                    File.WriteAllBytes(Path.Combine(folder,file+".png"),image.EncodeToPNG());
                }
                finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
            }
            try
            {
                foreach(var special in new[]{SecondaryAttack.Mine,SecondaryAttack.PatrolDrone,SecondaryAttack.GroundTurret,SecondaryAttack.LandDrone})
                {
                    Check(!Cached(special),"draw-only lookup does not create preview "+special);
                    var icon=Get(special);Pixels(icon,special.ToString());
                    Check(Cached(special)==icon,"draw-only lookup returns prepared preview "+special);
                    Check(Get(special)==icon,"one render reused "+special);
                    ((RenderTexture)icon).Release();
                    Check(!Cached(special),"draw-only lookup rejects lost preview "+special);
                    var recovered=Get(special);Check(recovered!=icon,"released target regenerated "+special);Pixels(recovered,special+"-recovered");
                    Check(Get(special,-1)==recovered,"offline and first multiplayer player share their colour preview");
                    for(int slot=1;slot<4;slot++)
                    {
                        var coloured=Get(special,slot);Pixels(coloured,special+"-P"+(slot+1));
                        Check(coloured!=recovered&&Cached(special,slot)==coloured,"owner-specific cached preview "+special+" slot "+slot);
                        Check(Get(special)==recovered,"switching owners retains original preview");
                    }
                }
                var hud=new BattleHud();
                try
                {
                    var state=new BattleSimulation(new MapData());var layout=new BattleHudLayout(1920,1080,1);
                    var draw=typeof(BattleHud).GetMethod("DrawLayout",BindingFlags.Instance|BindingFlags.NonPublic);
                    var atlas=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>().PowerupAtlas;
                    void Draw()=>draw.Invoke(hud,new object[]{state,null,atlas,false,layout,PowerupUiMode.WebTopLeft,0,true,(SecondaryAttack?)SecondaryAttack.Mine});
                    Draw();
                    var canvas=(Canvas)typeof(BattleHud).GetField("canvas",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hud);
                    RawImage Icon()=>canvas.GetComponentsInChildren<RawImage>().Single(i=>Mathf.Abs(i.rectTransform.rect.width-60f*68/82)<.01f);
                    Check(!Icon().texture&&Icon().color.a==0,"unprepared HUD icon is transparent, never a white square");
                    hud.PrepareSpecialIcon(SecondaryAttack.Mine);Draw();
                    Check(Icon().texture&&Icon().color.a>0,"Update-prepared model appears in draw-only HUD");Pixels(Icon().texture,"HUD-Mine");
                }
                finally{hud.Dispose();}
                Check(!UnityEngine.Object.FindObjectsByType<Camera>().Any(c=>c.name=="Icon camera"),"temporary preview cameras released");
                File.WriteAllText(Path.Combine(folder,"validation.txt"),"PASS: "+checks+" checks. Actual model pixels and lost-target recovery for Mine, PatrolDrone, GroundTurret and LandDrone. Unity Editor/URP validation; Web browser visual verification pending.\n");
                Debug.Log("SPECIAL ICON PASS: "+checks+" checks. Captures: "+folder);
            }
            finally{cacheType.GetMethod("Dispose").Invoke(cache,null);}
        }
    }
}
