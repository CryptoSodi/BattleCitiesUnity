using System;
using System.Collections;
using System.Linq;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BattleCities.Editor
{
    public static class SettingsPageChecks
    {
        public static string Status {get;private set;}="Not started";
        static int count,failures;
        static void Check(bool ok,string message){count++;if(!ok){failures++;Debug.LogError("Settings check: "+message);}}
        public static void Run(){var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.StartCoroutine(RunChecks(menu));}
        public static void RunRows(){var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.StartCoroutine(RunChecks(menu,true));}
        static IEnumerator RunChecks(MainMenuScene menu,bool rowsOnly=false)
        {
            Status="Running";count=failures=0;
            var input=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();bool inputEnabled=input&&input.enabled;if(input)input.enabled=false;
            var previousPlatform=menu.Platform;
            bool muteExists=PlayerPrefs.HasKey(GamePreferences.MuteKey),linesExists=PlayerPrefs.HasKey(GamePreferences.ScanlineKey);
            bool muted=GamePreferences.Muted,lines=GamePreferences.Scanlines;
            var clientObject=new GameObject("Settings fixture client");clientObject.SetActive(false);
            var api=clientObject.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);api.Configure("http://127.0.0.1:18763");
            var controllerObject=new GameObject("Settings phone fixture");var phone=controllerObject.AddComponent<PhoneControllerHost>();
            try
            {
                menu.OpenShop();menu.OpenSettings();var screen=menu.GetComponent<SettingsScreen>();
                foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
                {
                    menu.Platform=platform;
                    menu.ApplyLayout(platform,platform==MainMenuPlatform.Web?new Vector2(1583,924):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(844,390));Canvas.ForceUpdateCanvases();
                    // Let a newly opened screen enter the rendered canvas before simulating input.
                    yield return null;
                    yield return null;
                    Check(menu.IsSettingsOpen&&!menu.IsShopOpen&&!menu.IsRankingOpen&&!menu.IsOperationsOpen,platform+" exclusive screen");
                    var header=(RectTransform)screen.Root.Find("Title plate");var footer=(RectTransform)screen.Root.Find("Status");
                    Check(Math.Abs(header.rect.height-Mathf.Clamp(screen.Root.rect.height*.083f,46,64))<.1f,platform+" shared title height");
                    Check(Math.Abs(footer.rect.height-Mathf.Clamp(screen.Root.rect.height*.078f,50,64))<.1f,platform+" shared footer height");
                    var row=(RectTransform)screen.Root.Find("Rows/Row 3");Check(row.rect.height>80,platform+" phone row readable");
                    Check(screen.RowButtons[0].navigation.selectOnDown==screen.RowButtons[1],platform+" whole row navigation");
                    Check(screen.RowButtons[1].navigation.selectOnDown==screen.LogoutButton,platform+" account reachable");
                    Check(screen.ToggleButtons.All(button=>!button.enabled&&!button.image.raycastTarget),platform+" indicators are not separate selectable controls");
                    Check(!screen.Root.Find("Rows/Row 2").GetComponent<UnityEngine.UI.Button>(),platform+" account row never signs out");
                    Check(!screen.Root.Find("Rows/Row 3/Pair").gameObject.activeSelf,platform+" unavailable pairing action hidden");
                    screen.SetPreference(0,false);EventSystem.current.SetSelectedGameObject(screen.RowButtons[0].gameObject);
                    Check(!GamePreferences.Muted,platform+" focus does not activate toggle");
                    ExecuteEvents.Execute(screen.RowButtons[0].gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                    Check(GamePreferences.Muted&&AudioListener.volume==0,platform+" submit mutes game audio");
                    Check(screen.ToggleButtons[1].transform.Find("Active").GetComponent<UnityEngine.UI.Image>().enabled,platform+" active ON gold");
                    Check(screen.ToggleButtons[1].transform.Find("Caption").GetComponent<TMP_Text>().outlineWidth==0,platform+" clean caption");
                    screen.SetPreference(0,false);Check(AudioListener.volume==1,platform+" unmute restores audio");
                    foreach(float x in new[]{.10f,.46f,.84f})
                    {
                        // Raycast depth is assigned on the rendered frame after preference feedback.
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        var target=(RectTransform)screen.RowButtons[0].transform;var canvas=menu.GetComponent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(new Vector3(target.rect.xMin+target.rect.width*x,target.rect.center.y)))};
                        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                        var receiver=hits.Count>0?ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject):null;
                        Check(receiver==target.gameObject,platform+" click at "+x+" routes to complete row");
                        bool before=GamePreferences.Muted;if(receiver)ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerClickHandler);
                        Check(GamePreferences.Muted!=before,platform+" row click toggles exactly once");
                    }
                    screen.SetPreference(1,false);ExecuteEvents.Execute(screen.RowButtons[1].gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                    Check(GamePreferences.Scanlines,platform+" Scanline row submit toggles on");
                    ExecuteEvents.Execute(screen.RowButtons[1].gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);Check(!GamePreferences.Scanlines,platform+" Scanline row submit toggles off");
                }
                if(rowsOnly){Status=$"Settings rows: {count-failures}/{count} checks passed.";Debug.Log(Status);yield break;}
                screen.SetPreference(1,true);Check(GamePreferences.Scanlines&&GameObject.Find("Battle Cities scanlines").GetComponent<Canvas>().enabled,"scanline overlay enabled");
                screen.SetPreference(1,false);Check(!GameObject.Find("Battle Cities scanlines").GetComponent<Canvas>().enabled,"scanline overlay disabled");
                menu.Back();Check(!menu.IsSettingsOpen&&menu.IsShopOpen,"Back returns to originating Shop");
                menu.OpenSettings();menu.OpenQuarters();Check(!menu.IsSettingsOpen&&menu.IsOperationsOpen&&!menu.IsShopOpen,"main navigation leaves settings exclusively");
                // Fault fixtures exercise the real asynchronous DELETE path without touching a live session.
                string guestBefore=MainMenuApiClient.CurrentGuestId;
                foreach(var response in new[]{"offline","malformed","still-signed-in"})
                {
                    int calls=0;bool? completed=null;
                    api.EditorRequestOverride=(method,path,payload,reply)=>Reply(response,method,path,reply,()=>calls++);
                    clientObject.SetActive(true);api.SignOut((ok,error)=>completed=ok);
                    Check(api.IsSigningOut,"logout marks busy");api.SignOut((ok,error)=>calls+=100);
                    float deadline=Time.realtimeSinceStartup+3;while(completed==null&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(completed==false&&!api.IsSigningOut,response+" fails safely");Check(calls==1,response+" duplicate logout blocked");
                    Check(MainMenuApiClient.CurrentGuestId==guestBefore,response+" preserves existing session");
                }
                int gamepadCount=Gamepad.all.Count;
                phone.OnPhoneEvent("{\"type\":\"state\",\"axes\":[0.75,0.5],\"buttons\":[true,false,false,false,false,false,true]}");
                InputSystem.Update();var device=Gamepad.all.Last();
                Check(Gamepad.all.Count==gamepadCount+1&&device.buttonSouth.isPressed,"phone creates working virtual gamepad");
                Check(Vector2.Distance(device.leftStick.ReadValue(),new Vector2(.75f,-.5f))<.2f,"phone axes map to Unity coordinates");
                Check(device.leftTrigger.isPressed&&!device.dpad.up.isPressed,"triggers do not become dpad buttons");
                phone.OnPhoneEvent("{\"type\":\"closed\"}");InputSystem.Update();Check(!device.buttonSouth.isPressed&&device.leftStick.ReadValue()==Vector2.zero,"disconnect releases all controls");
                phone.OnPhoneEvent("{\"type\":\"state\",\"axes\":[0,1],\"buttons\":[true]}");InputSystem.Update();
                yield return new WaitForSecondsRealtime(1.15f);InputSystem.Update();Check(!device.buttonSouth.isPressed&&device.leftStick.ReadValue()==Vector2.zero,"stale packets release controls");
                phone.OnPhoneEvent("{\"type\":\"state\",\"axes\":[\"invalid\",1],\"buttons\":[true]}");InputSystem.Update();Check(!device.buttonSouth.isPressed,"malformed input ignored");
                phone.Stop();Check(Gamepad.all.Count==gamepadCount,"phone cleanup removes virtual device");
            }
            finally
            {
                UnityEngine.Object.Destroy(clientObject);UnityEngine.Object.Destroy(controllerObject);
                if(muteExists)PlayerPrefs.SetInt(GamePreferences.MuteKey,muted?1:0);else PlayerPrefs.DeleteKey(GamePreferences.MuteKey);
                if(linesExists)PlayerPrefs.SetInt(GamePreferences.ScanlineKey,lines?1:0);else PlayerPrefs.DeleteKey(GamePreferences.ScanlineKey);
                PlayerPrefs.Save();GamePreferences.Apply();menu.Platform=previousPlatform;menu.OpenSettings();menu.RefreshLayout();if(input)input.enabled=inputEnabled;
            }
            Status=$"Settings: {count-failures}/{count} checks passed.";Debug.Log(Status);
        }
        static IEnumerator Reply(string response,string method,string path,Action<long,JObject,string> reply,Action call)
        {
            call();Check(method=="DELETE"&&path=="/api/session","logout calls session DELETE");yield return null;
            if(response=="offline")reply(0,null,"offline");else reply(200,response=="malformed"?new JObject():new JObject{["authenticated"]=true},null);
        }
    }
}
