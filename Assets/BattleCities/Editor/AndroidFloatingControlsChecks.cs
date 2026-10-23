using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BattleCities.Core;
using BattleCities.LevelEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class AndroidFloatingControlsChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            BattleControlChecks.Run();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var materials=new List<Material>();var meshes=new List<Mesh>();
            var prefab=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>();
            var documentObject=new GameObject("Isolated preview map",typeof(LevelEditorDocument));
            var document=documentObject.GetComponent<LevelEditorDocument>();document.BuildPreviews=false;document.TrackUndo=false;
            document.MapRoot=new GameObject("Imported layout").transform;document.MapRoot.SetParent(documentObject.transform);
            document.BrickModel=prefab.BrickModel;document.SteelModel=prefab.SteelModel;document.BushModel=prefab.BushModel;document.EagleModel=prefab.EagleModel;
            document.TankModel=prefab.TankModels[0];document.BrickSectionData=prefab.BrickSectionData;document.GroundTextures=prefab.GroundTextures;
            document.Import(Resources.Load<TextAsset>("Maps/01").text);
            var world=new GameObject("Gameplay geometry preview");BattleGame.BuildEditorMap(world.transform,document,materials,meshes);
            documentObject.SetActive(false);
            var camera=new GameObject("Android preview camera",typeof(Camera)).GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;
            camera.orthographicSize=4.05f;camera.transform.rotation=Quaternion.Euler(70,0,0);
            camera.transform.position=new Vector3(6.5f,0,-8.7f)-camera.transform.forward*28;
            camera.backgroundColor=new Color(.13f,.18f,.20f);camera.clearFlags=CameraClearFlags.SolidColor;
            var sun=new GameObject("Preview sunlight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(55,-35,0);sun.intensity=1.4f;sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.75f,.85f);RenderSettings.ambientEquatorColor=new Color(.32f,.4f,.46f);RenderSettings.ambientGroundColor=new Color(.22f,.19f,.14f);
            var carrier=new GameObject("Isolated Android control preview");carrier.SetActive(false);
            var game=carrier.AddComponent<BattleGame>();
            var sim=new BattleSimulation(JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text)){Freeze=999,DisableEnemyFire=true};
            for(int i=0;i<240;i++)sim.Step(default);sim.EquippedSecondary=SecondaryAttack.Mine;
            typeof(BattleGame).GetProperty("Simulation").GetSetMethod(true).Invoke(game,new object[]{sim});
            typeof(BattleGame).GetMethod("ConfigureGameplayInput",Private).Invoke(game,null);
            game.PowerupAtlas=prefab.PowerupAtlas;
            // Isolated sample stock for the editor preview; never contacts an account/API.
            var economy=carrier.AddComponent<EconomyClient>();
            typeof(EconomyClient).GetField("account",Private).SetValue(economy,JObject.Parse("{\"loadout\":{\"active-one\":\"shield\",\"active-two\":\"freeze\",\"active-three\":\"speed\",\"active-four\":\"upgrade\"},\"inventory\":{\"shield\":3,\"freeze\":2,\"speed\":1,\"upgrade\":4}}"));
            typeof(EconomyClient).GetProperty("Authenticated").GetSetMethod(true).Invoke(economy,new object[]{true});
            typeof(BattleGame).GetField("economy",Private).SetValue(game,economy);
            var controls=carrier.AddComponent<BattleTouchControls>();controls.Initialize(game);
            var touchCanvas=carrier.GetComponentInChildren<Canvas>(true);touchCanvas.renderMode=RenderMode.ScreenSpaceCamera;touchCanvas.worldCamera=camera;touchCanvas.planeDistance=1;
            var scaler=touchCanvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            var hud=new BattleHud();var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;camera.aspect=1280f/720;
            int checks=0;void Check(bool value,string message){if(!value)throw new Exception("ANDROID FLOATING: "+message);checks++;}
            try
            {
                carrier.SetActive(true);Canvas.ForceUpdateCanvases();
                typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);Canvas.ForceUpdateCanvases();
                hud.DrawPreview(sim,prefab.PowerupAtlas,new BattleHudLayout(1280,720),PowerupUiMode.ExternalController);
                var hudCanvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c=>c.name=="Battle HUD");
                hudCanvas.renderMode=RenderMode.ScreenSpaceCamera;hudCanvas.worldCamera=camera;hudCanvas.planeDistance=.8f;
                foreach(var fps in hudCanvas.GetComponentsInChildren<BattleFrameRateDisplay>(true))fps.gameObject.SetActive(false);
                var all=carrier.GetComponentsInChildren<BattleTouchControl>(true);var move=all.Single(c=>c.name=="Movement");var fire=all.Single(c=>c.name=="Fire");
                Check(move.GetComponent<CanvasGroup>().alpha==0&&fire.GetComponent<CanvasGroup>().alpha==0,"idle sticks hidden");
                foreach(var slot in all.Where(c=>c.name.StartsWith("Powerup ")))
                {
                    Check(slot.GetComponent<Image>().color.a==0&&!slot.transform.Find("Inset"),"power-up has no button ring");
                    var icon=slot.transform.Find("Icon").GetComponent<RawImage>();
                    Check(icon.texture&&Mathf.Approximately(icon.color.a,.3f),"gold-container icon is 70 percent transparent");
                    Check(slot.transform.Find("Count badge/Count"),"count badge present");
                    var hit=(RectTransform)slot.transform;
                    Check(hit.rect.width>=48&&hit.rect.height>=48,"HUD power-up retains a padded touch target");
                    var position=RectTransformUtility.WorldToScreenPoint(camera,hit.position);
                    Check(position.y>=720-48&&position.y<=720,"power-up is positioned inside top HUD");
                }
                Capture(camera,target,"android-floating-idle.png");
                // Preview the live drop's exact atlas art and Android logical size.
                // Keep it separate from sample inventory and simulation/account state.
                var drop=new GameObject("Android battlefield drop preview",typeof(RectTransform),typeof(RawImage));
                var dropRect=drop.GetComponent<RectTransform>();dropRect.SetParent(touchCanvas.transform,false);
                dropRect.anchorMin=dropRect.anchorMax=new Vector2(.5f,.5f);
                dropRect.anchoredPosition=new Vector2(220,-80);
                dropRect.sizeDelta=Vector2.one*BattleHudLayout.TouchPickupSize;
                var dropImage=drop.GetComponent<RawImage>();dropImage.texture=prefab.PowerupAtlas;
                BattleHud.TryPowerupUv("shield",out var dropUv);dropImage.uvRect=dropUv;dropImage.raycastTarget=false;
                Capture(camera,target,"android-larger-powerup-drop.png");
                UnityEngine.Object.DestroyImmediate(drop);
                var special=carrier.GetComponentsInChildren<Button>().Single(b=>b.name=="Special Mine");
                Check(!special.GetComponentsInChildren<TMPro.TMP_Text>().Any(),"special uses only model and animated ring");
                var specialPosition=RectTransformUtility.WorldToScreenPoint(camera,special.transform.position);
                Check(specialPosition.x>1100&&specialPosition.y<550&&specialPosition.y>450,"special sits below minimap at upper right");
                typeof(BattleGame).GetField("vanguardSpecials",Private).SetValue(game,new[]{SecondaryAttack.Mine,SecondaryAttack.PatrolDrone});
                typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);Canvas.ForceUpdateCanvases();
                var other=carrier.GetComponentsInChildren<Button>().Single(b=>b.name=="Special PatrolDrone");
                var firstRect=(RectTransform)special.transform;var secondRect=(RectTransform)other.transform;
                Check(Mathf.Approximately(secondRect.anchoredPosition.y,firstRect.anchoredPosition.y)&&Mathf.Abs(secondRect.anchoredPosition.x-firstRect.anchoredPosition.x)>=60,"owned specials have separate adjacent targets");
                Capture(camera,target,"android-specials-multiple.png");
                var queued=typeof(BattleGame).GetField("secondaryQueued",Private);
                other.onClick.Invoke();
                Check(sim.EquippedSecondary==SecondaryAttack.PatrolDrone&&(bool)queued.GetValue(game),"drone button selects and queues its own ability");
                special.onClick.Invoke();Check(sim.EquippedSecondary==SecondaryAttack.PatrolDrone,"second tap cannot replace a queued ability");
                queued.SetValue(game,false);special.onClick.Invoke();
                Check(sim.EquippedSecondary==SecondaryAttack.Mine&&(bool)queued.GetValue(game),"mine button selects and queues its own ability");
                queued.SetValue(game,false);
                typeof(BattleGame).GetMethod("DeployTouchSpecial",Private).Invoke(game,new object[]{SecondaryAttack.GroundTurret});
                Check(!(bool)queued.GetValue(game),"cannot deploy an unowned ability");
                int beforeMines=sim.Mines.Count;
                special.onClick.Invoke();
                sim.Step(new Command{SecondaryFire=(bool)queued.GetValue(game)});queued.SetValue(game,false);
                Check(sim.Mines.Count==beforeMines+1&&sim.SecondaryCooldown>0,"direct special request deploys through normal simulation");
                other.onClick.Invoke();Check(!(bool)queued.GetValue(game),"shared cooldown blocks a different special too");
                for(int i=0;i<60;i++)sim.Step(default);
                typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);
                var cooldown=special.transform.Find("Cooldown").GetComponent<Image>();
                Check(cooldown.fillAmount>0&&cooldown.fillAmount<1&&!special.interactable&&!other.interactable,"both buttons show shared cooldown and block deployment");
                Capture(camera,target,"android-specials-cooldown.png");
                for(int i=0;i<65;i++)sim.Step(default);
                typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);
                Check(special.interactable&&other.interactable,"both owned abilities become available after cooldown");
                typeof(BattleGame).GetField("vanguardSpecials",Private).SetValue(game,new[]{SecondaryAttack.Mine});
                typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);
                var raycaster=touchCanvas.GetComponent<GraphicRaycaster>();
                var events=UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                var left=new PointerEventData(events){pointerId=101,position=new Vector2(190,205),pointerPressRaycast=new RaycastResult{module=raycaster}};
                var right=new PointerEventData(events){pointerId=202,position=new Vector2(970,205),pointerPressRaycast=new RaycastResult{module=raycaster}};
                var zones=carrier.GetComponentsInChildren<BattleTouchZone>();
                zones.Single(z=>z.name.StartsWith("Movement")).OnPointerDown(left);zones.Single(z=>z.name.StartsWith("Turret")).OnPointerDown(right);
                left.position+=new Vector2(34,48);right.position+=new Vector2(38,25);move.OnDrag(left);fire.OnDrag(right);
                var charge=(ChargedFireInput)typeof(BattleGame).GetField("primaryCharge",Private).GetValue(game);
                charge.Sample(true,false,true,0,true);charge.Sample(false,false,true,.65f,true);
                typeof(BattleTouchControls).GetMethod("Refresh",Private).Invoke(controls,null);
                Check(move.Held&&fire.Held,"both thumb controls held independently");
                Check(Mathf.Approximately(move.GetComponent<CanvasGroup>().alpha,.3f)&&Mathf.Approximately(fire.GetComponent<CanvasGroup>().alpha,.3f),"active sticks are 70 percent transparent");
                Canvas.ForceUpdateCanvases();Capture(camera,target,"android-floating-active.png");
                controls.CancelInput();Check(!move.Held&&!fire.Held,"cancellation clears both controls");
                Debug.Log("ANDROID FLOATING PREVIEW CHECKS PASSED: "+checks+". Screenshots: Builds/AndroidControlPreviews");
            }
            finally
            {
                controls.CancelInput();hud.Dispose();camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                if(!Application.isPlaying)foreach(var control in carrier.GetComponentsInChildren<UnityEngine.InputSystem.OnScreen.OnScreenControl>(true))control.GetType().GetMethod("OnDisable",Private).Invoke(control,null);
                UnityEngine.Object.DestroyImmediate(carrier);UnityEngine.Object.DestroyImmediate(world);UnityEngine.Object.DestroyImmediate(documentObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);
                foreach(var material in materials)if(material)UnityEngine.Object.DestroyImmediate(material);foreach(var mesh in meshes)if(mesh)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        static void Capture(Camera camera,RenderTexture target,string name)
        {
            Directory.CreateDirectory("Builds/AndroidControlPreviews");Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes("Builds/AndroidControlPreviews/"+name,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
