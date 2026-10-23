using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace BattleCities.Editor
{
    public static class BattleGuideChecks
    {
        public static void Run()
        {
            int checks=0;void Check(bool valid,string message){if(!valid)throw new Exception("BATTLE GUIDE: "+message);checks++;}
            var oldPlatform=ScreenContent.EditorPlatform;int oldPreference=PlayerPrefs.GetInt(BattleGuideScreen.PreferenceKey,-1);
            GameObject canvasObject=null,events=null;
            try
            {
                ScreenContent.EditorPlatform="web";PlayerPrefs.DeleteKey(BattleGuideScreen.PreferenceKey);
                Check(BattleGuideScreen.ShouldShow,"Web sees initial guide");
                ScreenContent.EditorPlatform="seeker";Check(!BattleGuideScreen.ShouldShow,"no guide on Seeker");
                ScreenContent.EditorPlatform="psg1";Check(BattleGuideScreen.IsSupportedPlatform,"PSG1 has its own guide");
                ScreenContent.EditorPlatform="web";
                string path=AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MenuTheme").First());
                var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(path);var skin=Resources.Load<PreBattleArt>("PreBattleArt");
                Check(theme&&skin,"reuses existing theme and button skins");
                var guideArt=Resources.Load<BattleGuideArt>("BattleGuideArt");
                if(!guideArt){guideArt=ScriptableObject.CreateInstance<BattleGuideArt>();AssetDatabase.CreateAsset(guideArt,"Assets/BattleCities/Resources/BattleGuideArt.asset");}
                // Generated art is assigned only after the supplying chat finishes writing it.
                string dir="Assets/BattleCities/Art/controls-guide/";
                foreach(var field in typeof(BattleGuideArt).GetFields())
                {
                    string file=field.Name=="normalShell"?"shell-normal":field.Name=="powerShell"?"shell-power":field.Name=="keycap"?"keycap-cream":field.Name=="tap"?"hand-tap-release":"hand-charge-release";
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(dir+file+".png");if(sprite)field.SetValue(guideArt,sprite);
                }
                EditorUtility.SetDirty(guideArt);AssetDatabase.SaveAssets();
                VerifyWebOta();Check(ScreenUpdates.ReleaseId=="web-guide-check","signed Web OTA activates with all five PNGs verified");
                foreach(string key in BattleGuideScreen.ArtKeys)Check(ScreenUpdates.Image("guide-"+key),"downloaded illustration "+key);
                canvasObject=new GameObject("Guide preview",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                events=new GameObject("Guide events",typeof(EventSystem));
                var frame=new GameObject("TV frame",typeof(RectTransform),typeof(Image));frame.transform.SetParent(canvasObject.transform,false);
                var frameRect=(RectTransform)frame.transform;frameRect.anchorMin=Vector2.zero;frameRect.anchorMax=Vector2.one;frameRect.offsetMin=frameRect.offsetMax=Vector2.zero;
                frame.GetComponent<Image>().sprite=theme.BlueFrame;frame.GetComponent<Image>().type=Image.Type.Sliced;
                var go=new GameObject("Battle station",typeof(RectTransform));go.transform.SetParent(frame.transform,false);
                var root=(RectTransform)go.transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=Vector2.one*12;root.offsetMax=-Vector2.one*12;
                var guide=go.AddComponent<BattleGuideScreen>();guide.Configure(theme,skin);
                int continued=0;
                foreach(var size in new[]{new Vector2(1600,900),new Vector2(1280,720),new Vector2(844,390),new Vector2(560,620)})
                {
                    ((RectTransform)canvasObject.transform).sizeDelta=size;Canvas.ForceUpdateCanvases();
                    guide.Open(0,()=>continued++);Canvas.ForceUpdateCanvases();
                    typeof(BattleGuideScreen).GetMethod("Layout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);
                    Canvas.ForceUpdateCanvases();
                    Check(guide.IsOpen&&continued==0,"opening never launches or charges");
                    var scroll=go.GetComponentInChildren<ScrollRect>();
                    Check(scroll.content.rect.width>0&&scroll.content.rect.height>0&&scroll.viewport.rect.height>0&&scroll.vertical,"instructions have usable scroll geometry");
                    if(size.x==1600)Check(scroll.content.rect.height<=scroll.viewport.rect.height+1,"desktop guide shows Q and powerups without scrolling");
                    var button=go.GetComponentInChildren<Button>();
                    Check(button.image.sprite==skin.tankCostButton,"Continue uses approved blue skin");
                    Check(button.image.raycastTarget&&button.interactable,"Continue accepts pointer input");
                    Check(go.transform.Find("Instructions/Viewport/Content/Special/Key Q").gameObject.activeSelf,"guide visibly documents Q switching");
                    Check(go.transform.Find("Instructions/Viewport/Content/Special/Switch availability").GetComponent<TMPro.TMP_Text>().text.Contains("multiple"),"Q explains multi-ability requirement");
                    if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        Capture(canvas,(int)size.x,(int)size.y);
                        scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                        Capture(canvas,(int)size.x,(int)size.y,"-bottom");
                    }
                    guide.Cancel();Check(continued==0,"cancel never launches");
                }
                guide.Open(0,()=>continued++);guide.Accept();guide.Accept();Check(continued==1,"Continue invokes launch exactly once");
                guide.Open(0,()=>continued++);go.GetComponentInChildren<Toggle>().isOn=true;
                Check(!BattleGuideScreen.ShouldShow,"Don't show again persists immediately");guide.Cancel();
                go.GetComponentInChildren<Toggle>(true).isOn=false;Check(BattleGuideScreen.ShouldShow,"unchecked option restores guide");
                var authored=JObject.Parse(File.ReadAllText("Content/ScreenUpdates/web-v1.json"));
                foreach(var image in authored["images"].OfType<JObject>())
                {
                    string source=(string)image["source"];if(source==null)continue;
                    var bytes=File.ReadAllBytes(source);string hash=ScreenUpdates.Hash(bytes);
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(source);
                    image.Remove("source");image["width"]=texture.width;image["height"]=texture.height;
                    image["file"]=new JObject{{"url",ScreenUpdates.WebBaseUrl+hash+".png"},{"sha256",hash},{"bytes",bytes.Length}};
                }
                var document=ScreenUpdates.ReadDocument(authored.ToString());
                Check(document.screens.Any(s=>s.id==BattleGuideScreen.ScreenId),"guide is in Web OTA document");
                foreach(string key in BattleGuideScreen.ArtKeys)Check(ScreenContent.Knows("battle-guide","art",key),"OTA art binding "+key);
                ScreenUpdateChecks.Run();
                Debug.Log("BATTLE GUIDE CHECKS PASSED: "+checks);
            }
            finally
            {
                if(canvasObject)UnityEngine.Object.DestroyImmediate(canvasObject);if(events)UnityEngine.Object.DestroyImmediate(events);
                ScreenContent.EditorPlatform=oldPlatform;
                if(oldPreference<0)PlayerPrefs.DeleteKey(BattleGuideScreen.PreferenceKey);else PlayerPrefs.SetInt(BattleGuideScreen.PreferenceKey,oldPreference);
                PlayerPrefs.Save();
            }
        }
        static void VerifyWebOta()
            => VerifyOta("Content/ScreenUpdates/web-v1.json",true,"web-guide-check");
        internal static void VerifyOta(string sourcePath,bool web,string releaseId)
        {
            string baseUrl=web?ScreenUpdates.WebBaseUrl:ScreenUpdates.BaseUrl;
            var doc=JObject.Parse(File.ReadAllText(sourcePath));
            var files=new System.Collections.Generic.Dictionary<string,byte[]>();
            foreach(var image in doc["images"].OfType<JObject>())
            {
                string path=(string)image["source"];var bytes=File.ReadAllBytes(path);string hash=ScreenUpdates.Hash(bytes);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);string url=baseUrl+hash+".png";
                image.Remove("source");image["width"]=texture.width;image["height"]=texture.height;
                image["file"]=JObject.FromObject(new ScreenFile{url=url,sha256=hash,bytes=bytes.Length});files[url]=bytes;
            }
            byte[] document=Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));string documentHash=ScreenUpdates.Hash(document);
            string documentUrl=baseUrl+documentHash+".json";files[documentUrl]=document;
            using(var key=new RSACryptoServiceProvider(2048){PersistKeyInCsp=false})
            {
                var parameters=key.ExportParameters(false);
                var config=new ScreenUpdateConfig{manifestUrl=baseUrl+"latest.json",channel=web?"web-ui-v1":"android-ui-v1",modulus=Convert.ToBase64String(parameters.Modulus),exponent=Convert.ToBase64String(parameters.Exponent)};
                var release=new ScreenRelease{sequence=1,releaseId=releaseId,channel=config.channel,runtimeId=ScreenUpdates.RuntimeId,unityVersion=Application.unityVersion,
                    minimumAppVersion="1.0.0",maximumAppVersion="2.0.0",document=new ScreenFile{url=documentUrl,sha256=documentHash,bytes=document.Length}};
                byte[] payload=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(release));
                byte[] envelope=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new SignedScreenRelease{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))}));
                string cache=Path.Combine(Application.temporaryCachePath,"BattleGuideOTA-"+Guid.NewGuid().ToString("N"));
                try
                {
                    ScreenUpdates.EditorInitialize(config,cache,"1.0.7",(url,limit,timeout,token)=>Task.FromResult(url.StartsWith(config.manifestUrl)?envelope:files[url])).GetAwaiter().GetResult();
                    ScreenUpdates.EditorInitialize(config,cache,"1.0.7",(url,limit,timeout,token)=>throw new IOException("Fixture offline")).GetAwaiter().GetResult();
                    if(ScreenUpdates.ReleaseId!=releaseId)throw new Exception("Guide OTA cache fallback failed");
                }
                finally{if(Directory.Exists(cache))Directory.Delete(cache,true);}
            }
        }
        internal static void Capture(Canvas canvas,int width,int height,string suffix="",string prefix="web")
        {
            Directory.CreateDirectory("Builds/BattleGuideChecks");
            var cameraObject=new GameObject("Guide capture",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
            var target=new RenderTexture(width,height,24);Texture2D texture=null;var old=RenderTexture.active;
            try
            {
                camera.transform.position=new Vector3(0,0,-100);camera.orthographic=true;camera.orthographicSize=height*.5f;
                camera.nearClipPlane=.1f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(3,21,41,255);
                target.Create();camera.targetTexture=target;canvas.worldCamera=camera;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                File.WriteAllBytes($"Builds/BattleGuideChecks/{prefix}-{width}x{height}{suffix}.png",texture.EncodeToPNG());
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);if(texture)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
    }
}
