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
            Capture(MainMenuPlatform.Android,940,1672,"android");
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
            Check(EditorBuildSettings.scenes[0].path==MainMenuBuilder.ScenePath,"main menu is startup scene");
            Check(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path.EndsWith("BattleCity.unity")),"gameplay scene remains included");
            Check(view.StartButton.onClick.GetPersistentEventCount()==1,"Start has a persistent action");
            Check(view.Tabs.Length==5,"five navigation destinations");
            foreach(var tab in view.Tabs)Check(tab.onClick.GetPersistentEventCount()==1,tab.name+" has an action");
            var inputs=AssetDatabase.LoadAssetAtPath<InputActionAsset>(MainMenuBuilder.Root+"Settings/MainMenuInput.asset");
            Check(inputs.FindAction("PSG1/Submit").bindings.Any(b=>b.path=="<Gamepad>/buttonEast"),"PSG1 A is east");
            Check(inputs.FindAction("PSG1/Cancel").bindings.Any(b=>b.path=="<Gamepad>/buttonSouth"),"PSG1 B is south");
            Check(!inputs.actionMaps.SelectMany(m=>m.actions).SelectMany(a=>a.bindings).Any(b=>b.path.Contains("Trigger")),"no unavailable PSG1 triggers");
            var variants=new[]{(MainMenuPlatform.Web,new Vector2(1600,1067)),(MainMenuPlatform.Web,new Vector2(1920,1080)),(MainMenuPlatform.Psg1,new Vector2(1240,1080)),(MainMenuPlatform.Android,new Vector2(940,1672)),(MainMenuPlatform.Android,new Vector2(390,844)),(MainMenuPlatform.Android,new Vector2(844,390))};
            foreach(var v in variants)
            {
                view.ApplyLayout(v.Item1,v.Item2);Canvas.ForceUpdateCanvases();
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
                    case 3:Assert(EventSystem.current.currentSelectedGameObject==view.Tabs[1].gameObject,"D-pad right must focus Quarters");InputSystem.QueueStateEvent(testPad,new GamepadState());break;
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
