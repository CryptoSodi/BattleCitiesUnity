using System;
using System.IO;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class MainMenuChecks
    {
        private const string PreviewDir="C:/repos/Unity/ui assets/main-menu/previews";
        [MenuItem("Battle Cities/UI/Capture Platform Previews")]
        public static void CaptureAll()
        {
            Directory.CreateDirectory(PreviewDir);
            Capture(MainMenuPlatform.Web,1600,1067,"web");
            Capture(MainMenuPlatform.Psg1,1240,1080,"psg1");
            Capture(MainMenuPlatform.Android,940,1672,"android-portrait");
            Capture(MainMenuPlatform.AndroidLandscape,844,390,"android-landscape");
            Debug.Log("[MainMenu] Captured platform previews at "+PreviewDir);
        }
        public static void Capture(MainMenuPlatform platform,int width,int height,string name)
        {
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            if(!menu)throw new InvalidOperationException("Open MainMenu first.");
            var canvas=menu.GetComponent<Canvas>();var camera=Camera.main;
            var priorMode=canvas.renderMode;var priorCamera=canvas.worldCamera;var priorTarget=camera.targetTexture;float priorDistance=canvas.planeDistance;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var priorActive=RenderTexture.active;Texture2D image=null;
            try
            {
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();menu.ApplyLayout(platform,new Vector2(width,height));Canvas.ForceUpdateCanvases();
                camera.Render();RenderTexture.active=target;
                image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(PreviewDir+"/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=priorActive;camera.targetTexture=priorTarget;canvas.renderMode=priorMode;canvas.worldCamera=priorCamera;canvas.planeDistance=priorDistance;
                UnityEngine.Object.DestroyImmediate(target);if(image)UnityEngine.Object.DestroyImmediate(image);menu.RefreshLayout();
            }
        }
        [MenuItem("Battle Cities/UI/Validate Main Menu")]
        public static void Validate()
        {
            var view=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();if(!view)throw new InvalidOperationException("Open MainMenu first.");
            int count=0;
            void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("[MainMenu] FAIL: "+what);count++;}
            Check(EditorBuildSettings.scenes[0].path=="Assets/BattleCities/Scenes/Login.unity","login is startup scene");
            Check(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path.EndsWith("BattleCity.unity")),"gameplay scene remains included");
            Check(view.StartButton.onClick.GetPersistentEventCount()==1,"Start has a persistent action");
            Check(view.Tabs.Length==5,"five navigation destinations");
            foreach(var tab in view.Tabs)Check(tab.onClick.GetPersistentEventCount()==1,tab.name+" has an action");
            var inputs=AssetDatabase.LoadAssetAtPath<InputActionAsset>(MainMenuBuilder.Root+"Settings/MainMenuInput.asset");
            Check(inputs.FindAction("PSG1/Submit").bindings.Any(b=>b.path=="<Gamepad>/buttonEast"),"PSG1 A is east");
            Check(inputs.FindAction("PSG1/Cancel").bindings.Any(b=>b.path=="<Gamepad>/buttonSouth"),"PSG1 B is south");
            Check(!inputs.actionMaps.SelectMany(m=>m.actions).SelectMany(a=>a.bindings).Any(b=>b.path.Contains("Trigger")),"no unavailable PSG1 triggers");
            var variants=new[]{(MainMenuPlatform.Web,new Vector2(1600,1067)),(MainMenuPlatform.Web,new Vector2(1920,1080)),(MainMenuPlatform.Psg1,new Vector2(1240,1080)),(MainMenuPlatform.Android,new Vector2(940,1672)),(MainMenuPlatform.Android,new Vector2(390,844)),(MainMenuPlatform.AndroidLandscape,new Vector2(844,390))};
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(MainMenuBuilder.Root+"Settings/ArcadeMenuTheme.asset");
            Check(theme && theme.Battlefield && theme.PsgBattlefield && theme.AndroidLandscapeBattlefield && theme.AndroidPortraitBattlefield,"four arena backgrounds are assigned");
            var suppliedContainer=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/panels/button-leaderboard-container.png");
            var originalContainer=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/panels/navigation-container.png");
            Check(suppliedContainer && suppliedContainer.border.x>0,"supplied button container imports as a sliced sprite");
            Check(view.Content.Find("Navigation").GetComponent<Image>().sprite==suppliedContainer,
                "navigation uses the supplied button container");
            Check(view.Content.Find("Leaderboard").GetComponent<Image>().sprite==originalContainer,
                "leaderboard keeps its original container");
            var blurMaterial=AssetDatabase.LoadAssetAtPath<Material>(MainMenuBuilder.Root+"Art/shared/panels/TVBackdropBlur.mat");
            Check(blurMaterial && blurMaterial.shader && blurMaterial.shader.name=="BattleCities/UI/TVBackdropBlur",
                "TV blur material and shader are available");
            Check(blurMaterial && blurMaterial.GetFloat("_BlurRadius")>=10f,
                "TV background uses a strong blur");
            var roundHeading=view.Content.Find("Leaderboard/Round status/Heading")?.GetComponent<Text>();
            Check(roundHeading && roundHeading.text=="TOP 10 EVERY 30 MINUTES","ranking round heading explains the 30 minute cycle");
            foreach(var v in variants)
            {
                view.ApplyLayout(v.Item1,v.Item2);Canvas.ForceUpdateCanvases();
                var backdrop=view.transform.Find("World backdrop").GetComponent<Image>();
                var expected=v.Item1==MainMenuPlatform.Psg1?theme.PsgBattlefield:
                    v.Item1==MainMenuPlatform.AndroidLandscape?theme.AndroidLandscapeBattlefield:
                    v.Item1==MainMenuPlatform.Android?theme.AndroidPortraitBattlefield:theme.Battlefield;
                Check(backdrop.sprite==expected,v.Item1+" uses its supplied arena background");
                var tv=view.Content.Find("Main Display/TV Frame");
                var viewport=view.Content.Find("Main Display/TV Background Viewport");
                var selector=view.Content.Find("Main Display/Pre-battle screens");
                bool androidHome=(v.Item1==MainMenuPlatform.Android || v.Item1==MainMenuPlatform.AndroidLandscape) &&
                    !view.IsModalOpen && !(selector && selector.gameObject.activeInHierarchy);
                var tvImage=tv?tv.GetComponent<Image>():null;
                Check(tv && tv.gameObject.activeSelf!=androidHome && tvImage && tvImage.type==Image.Type.Sliced && !tvImage.fillCenter,
                    v.Item1+" TV border visibility matches the menu page");
                var blurred=viewport?viewport.Find("TV Background") as RectTransform:null;
                var blurredImage=blurred?blurred.GetComponent<Image>():null;
                Check(viewport && viewport.gameObject.activeSelf!=androidHome && blurredImage && blurredImage.sprite==expected &&
                    blurredImage.material==blurMaterial && viewport.GetSiblingIndex()<tv.GetSiblingIndex(),
                    v.Item1+" masked blur uses the matching arena under the TV frame");
                var fog=viewport?viewport.Find("TV White Fog") as RectTransform:null;
                var fogImage=fog?fog.GetComponent<Image>():null;
                Check(fogImage && blurred && fogImage.color.a>=.58f && !fogImage.raycastTarget &&
                    fog.GetSiblingIndex()>blurred.GetSiblingIndex(),
                    v.Item1+" white fog covers only the masked TV background");
                var backdropCorners=new Vector3[4];var blurCorners=new Vector3[4];
                backdrop.rectTransform.GetWorldCorners(backdropCorners);blurred.GetWorldCorners(blurCorners);
                Check(Vector3.Distance(backdropCorners[0],blurCorners[0])<1 &&
                    Vector3.Distance(backdropCorners[2],blurCorners[2])<1,
                    v.Item1+" blur artwork aligns with the world backdrop");
                Check(!view.Content.Find("Main Display/Rewards"),v.Item1+" Rewards is absent");
                Check(!view.Content.Find("Main Display/Battlefield Hero/A Select hint"),v.Item1+" Select hint is absent");
                foreach(var button in view.Tabs)
                {
                    Check(button.navigation.mode==Navigation.Mode.Explicit,v.Item1+" explicit focus "+button.name);
                    var rt=(RectTransform)button.transform;
                    Check(rt.rect.width>0&&rt.rect.height>0,v.Item1+" positive tab dimensions");
                    Check(button.navigation.selectOnRight!=null,v.Item1+" reachable focus neighbor");
                }
                foreach(string name in new[]{"Status Bar","Main Display","Navigation"})
                {
                    var rt=(RectTransform)view.Content.Find(name);float x=rt.anchoredPosition.x,y=-rt.anchoredPosition.y;
                    Check(x>=0&&y>=0&&x+rt.sizeDelta.x<=view.Content.sizeDelta.x+.1f&&y+rt.sizeDelta.y<=view.Content.sizeDelta.y+.1f,v.Item1+" "+name+" within content");
                }
            }
            foreach(var g in view.GetComponentsInChildren<Graphic>(true))
                if(g is Text text)Check(text.font!=null,"portable bundled font: "+text.name);
            view.RefreshLayout();Debug.Log("[MainMenu] PASS: "+count+" scene, asset, layout and navigation checks.");
        }

        private static Gamepad testPad;
        private static int step;
        private static double next;
        private static MainMenuPlatform previousPlatform;
        public static string InteractionResult { get; private set; }="Not run";
        [MenuItem("Battle Cities/UI/Test PSG1 Navigation In Play Mode")]
        public static void TestInteraction()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
            var view=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();previousPlatform=view.Platform;view.Platform=MainMenuPlatform.Psg1;
            EventSystem.current.SetSelectedGameObject(view.StartButton.gameObject);
            testPad=InputSystem.AddDevice<Gamepad>();step=0;next=EditorApplication.timeSinceStartup+.5;InteractionResult="Running";
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.12;
            var view=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            try
            {
                void Assert(bool ok,string msg){if(!ok)throw new Exception(msg);}
                switch(step++)
                {
                    case 0:InputSystem.QueueStateEvent(testPad,new GamepadState().WithButton(GamepadButton.DpadDown));break;
                    case 1:Assert(EventSystem.current.currentSelectedGameObject==view.Tabs[0].gameObject,"D-pad down must focus Play");InputSystem.QueueStateEvent(testPad,new GamepadState());break;
                    case 2:InputSystem.QueueStateEvent(testPad,new GamepadState().WithButton(GamepadButton.DpadRight));break;
                    case 3:Assert(EventSystem.current.currentSelectedGameObject==view.Tabs[1].gameObject,"D-pad right must focus Shop");InputSystem.QueueStateEvent(testPad,new GamepadState());break;
                    case 4:InputSystem.QueueStateEvent(testPad,new GamepadState().WithButton(GamepadButton.East));break;
                    case 5:Assert(view.IsModalOpen,"PSG1 A must select");InputSystem.QueueStateEvent(testPad,new GamepadState());break;
                    case 6:InputSystem.QueueStateEvent(testPad,new GamepadState().WithButton(GamepadButton.South));break;
                    case 7:Assert(!view.IsModalOpen,"PSG1 B must go back");Assert(EventSystem.current.currentSelectedGameObject==view.Tabs[1].gameObject,"Back restores focus");InputSystem.QueueStateEvent(testPad,new GamepadState());break;
                    case 8:InputSystem.RemoveDevice(testPad);testPad=null;view.Platform=previousPlatform;InteractionResult="PASS: D-pad navigation, PSG1 A select, B back and focus restoration.";Debug.Log("[MainMenu] "+InteractionResult);EditorApplication.update-=Tick;break;
                }
            }
            catch(Exception e)
            {InteractionResult="FAIL: "+e.Message;Debug.LogError("[MainMenu] "+InteractionResult);if(testPad!=null)InputSystem.RemoveDevice(testPad);testPad=null;if(view)view.Platform=previousPlatform;EditorApplication.update-=Tick;}
        }
    }
}
