using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BattleCities.UI;
using Newtonsoft.Json;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class ScreenUpdateChecks
    {
        static int checks;
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
        static void Reject(Action action,string label){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,label);}
        static string Envelope(ScreenRelease release,RSA key)
        {
            var payload=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(release));
            return JsonConvert.SerializeObject(new SignedScreenRelease{payload=Convert.ToBase64String(payload),
                signature=Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))});
        }
        static ScreenRelease Release(byte[] bytes,long sequence,string id)=>new ScreenRelease{sequence=sequence,releaseId=id,
            runtimeId=ScreenUpdates.RuntimeId,channel="android-ui-v1",unityVersion=Application.unityVersion,
            minimumAppVersion="1.0.4",maximumAppVersion="2.0.0",document=new ScreenFile{sha256=ScreenUpdates.Hash(bytes),bytes=bytes.Length,
                url=ScreenUpdates.BaseUrl+ScreenUpdates.Hash(bytes)+".json"}};
        public static void ValidateSource()
        {
            string path=Environment.GetEnvironmentVariable("BATTLECITIES_SCREEN_DOCUMENT")
                ??"Assets/BattleCities/Resources/ScreenUpdates/default-screens.json";
            ScreenUpdates.ReadDocument(File.ReadAllText(path));
            string hash=ScreenUpdates.Hash(File.ReadAllBytes(path));
            var validator=new MemoryStream();
            foreach(var name in new[]{"Assets/BattleCities/UI/Runtime/ScreenUpdates.cs","Assets/BattleCities/UI/Runtime/ScreenContent.cs","Assets/BattleCities/UI/Runtime/BattleGuideScreen.cs","Assets/BattleCities/UI/Runtime/BattleGuideScreen.Psg1.cs","Assets/BattleCities/UI/Runtime/AuthoredScreenElement.cs","Assets/BattleCities/UI/Runtime/RemoteScreenView.cs","Assets/BattleCities/Editor/ScreenUpdateChecks.cs"}){var bytes=File.ReadAllBytes(name);validator.Write(bytes,0,bytes.Length);}
            Directory.CreateDirectory("Builds/ScreenUpdateChecks");
            File.WriteAllText("Builds/ScreenUpdateChecks/"+hash+"-validation.json",JsonConvert.SerializeObject(new{sha256=hash,validatorHash=ScreenUpdates.Hash(validator.ToArray())}));
            Debug.Log("SCREEN DOCUMENT VALIDATED: "+path);
        }
        public static void RunHosted()
        {
            ScreenUpdates.EditorReset();
            var config=JsonConvert.DeserializeObject<ScreenUpdateConfig>(Resources.Load<TextAsset>("ScreenUpdates/config").text);
            string directory=Path.Combine(Application.temporaryCachePath,"BattleCities-Hosted-ScreenProof-v1");
            System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
            ScreenUpdates.EditorInitialize(config,directory,"1.0.4",(url,limit,timeout,token)=>
            {
                using(var client=new System.Net.WebClient())
                {client.Headers["Cache-Control"]="no-cache";return Task.FromResult(client.DownloadData(url));}
            }).GetAwaiter().GetResult();
            if(ScreenUpdates.ReleaseId=="built-in")throw new Exception("Hosted release did not activate.");
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            typeof(MainMenuScene).GetMethod("EnsureSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,null);
            var screen=UnityEngine.Object.FindFirstObjectByType<SettingsScreen>();screen.Root.gameObject.SetActive(true);
            menu.ApplyLayout(MainMenuPlatform.AndroidLandscape,new Vector2(2316,1080));screen.ApplyLayout(MainMenuPlatform.AndroidLandscape);Canvas.ForceUpdateCanvases();
            var view=RemoteScreenView.Bind("settings",screen.Root);view.ApplyNow("seeker");
            if(!view.BindingValid)throw new Exception("Hosted Settings definition did not bind.");
            string title=screen.Root.Find("Title plate/Title").GetComponent<TMP_Text>().text;
            string apk=Environment.GetEnvironmentVariable("BATTLECITIES_SCREEN_TEST_APK"),apkHash=null;
            if(!string.IsNullOrEmpty(apk))using(var hash=SHA256.Create())using(var stream=File.OpenRead(apk))apkHash=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            Directory.CreateDirectory("Builds/ScreenUpdateChecks");
            File.WriteAllText("Builds/ScreenUpdateChecks/hosted-"+ScreenUpdates.ReleaseId+".json",JsonConvert.SerializeObject(new{releaseId=ScreenUpdates.ReleaseId,title,apkSha256=apkHash}));
            Debug.Log("HOSTED SCREEN CHECK PASSED: "+ScreenUpdates.ReleaseId+" title="+title+" APK="+apkHash);
        }
        public static void Run()
        {
            ValidateSource();
            checks=0;string directory=Path.Combine(Application.temporaryCachePath,"screen-update-fixtures-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            bool mutePresent=PlayerPrefs.HasKey(GamePreferences.MuteKey),muted=GamePreferences.Muted;
            using(var key=new RSACryptoServiceProvider(2048){PersistKeyInCsp=false})
            try
            {
                var publicKey=key.ExportParameters(false);
                Debug.Log("OTA fixture RSA bits="+key.KeySize+" modulus bytes="+publicKey.Modulus.Length);
                var config=new ScreenUpdateConfig{manifestUrl=ScreenUpdates.BaseUrl+"latest.json",modulus=Convert.ToBase64String(publicKey.Modulus),exponent=Convert.ToBase64String(publicKey.Exponent)};
                var source=ScreenUpdates.ReadDocument(File.ReadAllText("Assets/BattleCities/Resources/ScreenUpdates/default-screens.json"));
                var bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source));var first=Release(bytes,1,"fixture-one");string envelope=Envelope(first,key);
                Check(ScreenUpdates.VerifyRelease(envelope,config,"1.0.4").sequence==1,"valid release signature");
                var tampered=JsonConvert.DeserializeObject<SignedScreenRelease>(envelope);var payload=Convert.FromBase64String(tampered.payload);payload[5]^=1;tampered.payload=Convert.ToBase64String(payload);
                Reject(()=>ScreenUpdates.VerifyRelease(JsonConvert.SerializeObject(tampered),config,"1.0.4"),"tampered payload rejected");
                using(var other=RSA.Create())Reject(()=>ScreenUpdates.VerifyRelease(Envelope(first,other),config,"1.0.4"),"wrong signing key rejected");
                Reject(()=>ScreenUpdates.VerifyRelease(envelope,config,"1.0.3"),"old APK rejected");
                Reject(()=>ScreenUpdates.VerifyRelease(envelope,config,"2.0.0"),"new incompatible APK rejected");
                var wrong=Release(bytes,1,"wrong-runtime");wrong.runtimeId="unsupported";
                Reject(()=>ScreenUpdates.VerifyRelease(Envelope(wrong,key),config,"1.0.4"),"unsupported renderer rejected");
                wrong=Release(bytes,1,"wrong-engine");wrong.unityVersion="different";
                Reject(()=>ScreenUpdates.VerifyRelease(Envelope(wrong,key),config,"1.0.4"),"wrong engine rejected");
                wrong=Release(bytes,1,"wrong-origin");wrong.document.url="https://example.invalid/file.json";
                Reject(()=>ScreenUpdates.VerifyRelease(Envelope(wrong,key),config,"1.0.4"),"untrusted download origin rejected");
                Check(!ScreenUpdates.CanReplace(Release(bytes,1,"old"),Release(bytes,2,"new")),"stale release blocked");
                Check(ScreenUpdates.CanReplace(Release(bytes,3,"rollback"),Release(bytes,2,"new")),"explicit newer rollback release allowed");
                foreach(string bad in new[]{"path","bounds","text","action","image","font","flow","schema"})
                {
                    var doc=ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(source));var target=doc.screens[0].patches[0];
                    if(bad=="path")target.path="../Title";
                    if(bad=="bounds")target.rect=new[]{0f,0f,2f,1f};
                    if(bad=="text")target.path="Rows/Row 2/Description";
                    if(bad=="action")target.text="<script>";
                    if(bad=="image")target.image="not-declared";
                    if(bad=="font")target.fontSize=float.NaN;
                    if(bad=="flow")doc.screens[0].flows[0].children[0].weight=0;
                    if(bad=="schema")doc.schemaVersion=99;
                    Reject(()=>ScreenUpdates.ValidateDocument(doc),bad+" invalid definition rejected");
                }
                int requests=0;
                Task<byte[]> Fetch(string url,int limit,int timeout,System.Threading.CancellationToken token)
                {requests++;return Task.FromResult(url.Contains("latest.json")?Encoding.UTF8.GetBytes(envelope):bytes);}
                ScreenUpdates.EditorReset();ScreenUpdates.EditorInitialize(config,directory,"1.0.4",Fetch).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-one"&&requests==2,"first startup downloads and activates verified definition");
                ScreenUpdates.EditorReset();requests=0;ScreenUpdates.EditorInitialize(config,directory,"1.0.4",Fetch).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-one"&&requests==1,"cached definition avoids another content download");
                ScreenUpdates.EditorReset();ScreenUpdates.EditorInitialize(config,directory,"1.0.4",(u,l,t,c)=>Task.FromException<byte[]>(new IOException("fixture offline"))).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-one","offline startup uses verified cached release");
                var secondDoc=ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(source));secondDoc.screens[0].patches[0].text="SETTINGS UPDATE TEST";
                var secondBytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(secondDoc));var second=Release(secondBytes,2,"fixture-two");
                string secondEnvelope=Envelope(second,key);
                ScreenUpdates.EditorReset();ScreenUpdates.EditorInitialize(config,directory,"1.0.4",(u,l,t,c)=>Task.FromResult(u.Contains("latest.json")?Encoding.UTF8.GetBytes(secondEnvelope):new byte[secondBytes.Length])).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-one","corrupt new download preserves previous release");
                ScreenUpdates.EditorReset();ScreenUpdates.EditorInitialize(config,directory,"1.0.4",(u,l,t,c)=>Task.FromResult(u.Contains("latest.json")?Encoding.UTF8.GetBytes(secondEnvelope):secondBytes)).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-two","next startup activates newer release");
                ScreenUpdates.EditorReset();ScreenUpdates.EditorInitialize(config,directory,"1.0.4",Fetch).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-two","older server release cannot downgrade cached release");
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});texture.Apply();
                var png=texture.EncodeToPNG();UnityEngine.Object.DestroyImmediate(texture);
                var imageDoc=ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(source));string imageHash=ScreenUpdates.Hash(png);
                imageDoc.images=new[]{new ScreenImage{id="test-icon",width=2,height=2,file=new ScreenFile{bytes=png.Length,sha256=imageHash,url=ScreenUpdates.BaseUrl+imageHash+".png"}}};
                imageDoc.screens.First(x=>x.id=="settings").art["Title plate/Icon"]="test-icon";
                imageDoc.screens.First(x=>x.id=="manualdetail").art["entry-vanguard"]="test-icon";
                var imageBytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(imageDoc));string imageEnvelope=Envelope(Release(imageBytes,3,"fixture-image"),key);
                ScreenUpdates.EditorReset();ScreenUpdates.EditorInitialize(config,directory,"1.0.4",(u,l,t,c)=>Task.FromResult(u.Contains("latest.json")?Encoding.UTF8.GetBytes(imageEnvelope):u.EndsWith(".png")?png:imageBytes)).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="fixture-image"&&ScreenUpdates.Image("test-icon")&&ScreenUpdates.Image("test-icon").texture.width==2,"signed PNG image decoded before activation");
                Check(ScreenContent.Sprite("manualdetail","entry-vanguard",null)==ScreenUpdates.Image("test-icon"),"per-entry artwork resolves from signed content");
                EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
                var imageMenu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
                typeof(MainMenuScene).GetMethod("EnsureSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(imageMenu,null);
                var imageScreen=UnityEngine.Object.FindFirstObjectByType<SettingsScreen>();
                var imageView=imageScreen.Root.GetComponent<RemoteScreenView>();imageView.ApplyNow("seeker");
                Debug.Log("PNG transition: sprite="+(bool)ScreenUpdates.Image("test-icon")+" binding="+imageView.BindingValid+" current="+ScreenUpdates.ReleaseId);
                Check(imageView.BindingValid&&imageScreen.Root.Find("Title plate/Icon").GetComponent<UnityEngine.UI.Image>().sprite==ScreenUpdates.Image("test-icon"),"verified downloaded PNG replaces a declared illustration on the real screen");
                File.WriteAllText(Path.Combine(directory,"active.json"),"broken");ScreenUpdates.EditorReset();
                ScreenUpdates.EditorInitialize(config,directory,"1.0.4",(u,l,t,c)=>Task.FromException<byte[]>(new IOException())).GetAwaiter().GetResult();
                Check(ScreenUpdates.ReleaseId=="built-in","corrupt cache and offline host use built-in screen");

                EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity");
                var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
                typeof(MainMenuScene).GetMethod("EnsureSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,null);
                var screen=UnityEngine.Object.FindFirstObjectByType<SettingsScreen>();Check(screen&&screen.IsConfigured,"actual Settings view builds");
                screen.Root.gameObject.SetActive(true);
                var view=RemoteScreenView.Bind("settings",screen.Root);var originalRoot=screen.Root;
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.Android,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    var size=platform==MainMenuPlatform.Android?new Vector2(1080,2316):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(2316,1080);
                    menu.ApplyLayout(platform,size);screen.ApplyLayout(platform);Canvas.ForceUpdateCanvases();
                    ScreenUpdates.EditorUseDocument(source,"render-one");view.ApplyNow(platform==MainMenuPlatform.Psg1?"psg1":platform==MainMenuPlatform.Web?"web":"seeker");
                    Check(view.BindingValid,platform+" Settings definition binds");
                    var rows=(RectTransform)screen.Root.Find("Rows");var last=(RectTransform)rows.Find("Row 3");
                    Check(last.rect.height>0&&-last.anchoredPosition.y+last.rect.height<=rows.rect.height+.1f,platform+" all rows fit");
                }
                var reordered=ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(source));
                var ordered=reordered.screens[0].flows[0].children;var swap=ordered[0];ordered[0]=ordered[1];ordered[1]=swap;
                ScreenUpdates.EditorUseDocument(reordered,"reordered");view.ApplyNow("seeker");
                Check(screen.RowButtons[1].navigation.selectOnDown==screen.RowButtons[0]&&screen.BackButton.navigation.selectOnDown==screen.RowButtons[1],"downloaded row order preserves controller navigation");
                var variants=ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(source));
                variants.screens=variants.screens.Concat(new[]{new ScreenDefinition{id="settings",platform="psg1",patches=new[]{new ScreenPatch{path="Title plate/Title",fontSize=32}}}}).ToArray();
                ScreenUpdates.EditorUseDocument(variants,"platform-variant");
                var inherited=ScreenUpdates.Definition("settings","psg1").patches.First(x=>x.path=="Title plate/Title");
                Check(inherited.text=="SETTINGS"&&inherited.fontSize==32,"platform variant inherits baseline text and overrides font size");
                var title=screen.Root.Find("Title plate/Title").GetComponent<TMP_Text>();
                ScreenUpdates.EditorUseDocument(secondDoc,"render-two");view.ApplyNow("seeker");
                Check(title.text=="SETTINGS UPDATE TEST"&&screen.Root==originalRoot,"new definition updates same live screen without APK or hierarchy rebuild");
                bool before=GamePreferences.Muted;screen.RowButtons[0].onClick.Invoke();Check(GamePreferences.Muted!=before,"existing toggle action survives update");
                var badBinding=ScreenUpdates.ReadDocument(JsonConvert.SerializeObject(secondDoc));badBinding.screens[0].patches=badBinding.screens[0].patches.Concat(new[]{new ScreenPatch{path="Missing child",rect=new[]{0f,0f,.5f,.5f}}}).ToArray();
                title.text="SETTINGS";ScreenUpdates.EditorUseDocument(badBinding,"bad-binding");view.ApplyNow("seeker");
                Check(!view.BindingValid&&title.text=="SETTINGS","incompatible hierarchy applies no partial changes");
                Directory.CreateDirectory("Builds/ScreenUpdateChecks");File.WriteAllText("Builds/ScreenUpdateChecks/result.txt","PASS "+checks);
                Debug.Log("SCREEN UPDATE CHECKS PASSED: "+checks);
            }
            finally
            {
                ScreenUpdates.EditorReset();
                if(mutePresent)PlayerPrefs.SetInt(GamePreferences.MuteKey,muted?1:0);else PlayerPrefs.DeleteKey(GamePreferences.MuteKey);
                PlayerPrefs.Save();GamePreferences.Apply();
                // The directory is a newly created, unique fixture directory under Unity's temporary cache.
                string tempRoot=Path.GetFullPath(Application.temporaryCachePath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!Path.GetFullPath(directory).StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture cleanup path");
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
        }
    }
}
