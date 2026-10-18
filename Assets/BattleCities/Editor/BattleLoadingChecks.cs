#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using BattleCities.Core;
using BattleCities.Tests;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    // Isolated UI and HTTP fixtures: no wallet connection, fuel debit or live match.
    public static class BattleLoadingChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Output="Builds/BattleLoadingChecks";
        static int checks;
        static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
        static void Tick(BattleConnectionNotice view)=>typeof(BattleConnectionNotice).GetMethod("Update",Private).Invoke(view,null);
        static void Layout(BattleConnectionNotice view)=>typeof(BattleConnectionNotice).GetMethod("Layout",Private).Invoke(view,null);

        [MenuItem("Battle Cities/Checks/Battle loading presentation")]
        public static void Run()
        {
            checks=0;Directory.CreateDirectory(Output);
            ReplayRecoveryChecks.Run();
            var gameHost=new GameObject("Inactive battle loading fixture");gameHost.SetActive(false);
            var game=gameHost.AddComponent<BattleGame>();
            var uiHost=new GameObject("Battle loading UI fixture");
            var previousFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            EventSystem fixtureEvents=null;
            try
            {
                var map=JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text);
                var simulation=new BattleSimulation(map);
                typeof(BattleGame).GetProperty("Simulation").SetValue(game,simulation);
                Set(game,"replayPreparing",true);
                Set(game,"replayArchive",new ReplayArchive{uploadStatus="Connecting score recording; battle is paused"});
                var view=uiHost.AddComponent<BattleConnectionNotice>();view.Initialize(game);
                // Edit-mode fixtures do not run EventSystem's normal play-mode activation.
                if(!EventSystem.current)
                {
                    fixtureEvents=uiHost.GetComponentInChildren<EventSystem>();
                    typeof(EventSystem).GetMethod("OnEnable",Private).Invoke(fixtureEvents,null);
                }
                Check(EventSystem.current,"Loading view has no input event system");
                var surface=uiHost.transform.Find("Battle connection");
                var loading=surface.Find("Safe area/Preparing battle");
                var frame=surface.Find("Safe area/TV frame");
                var back=frame.GetComponentInChildren<Button>(true);
                Check(surface.gameObject.activeSelf&&loading.gameObject.activeSelf&&!frame.gameObject.activeSelf,"Initial wait exposed the connection menu instead of loading immediately");
                Check(loading.GetComponentInChildren<TMP_Text>().text=="Preparing battle...","Normal wait displayed recording internals");
                Check(!back.gameObject.activeInHierarchy&&EventSystem.current.currentSelectedGameObject!=back.gameObject,"Normal loading exposed or focused the recovery action");
                var logo=loading.Find("Battle Cities Logo").GetComponent<Image>();
                Check(logo.sprite&&logo.preserveAspect&&logo.type==Image.Type.Simple,"Loading logo is missing or distorted");
                Check(surface.Find("Backdrop").GetComponent<Image>().color.a==1,"Loading backdrop does not fully cover battle setup");
                Check(!game.ReplayReady&&simulation.Tick==0,"Loading presentation bypassed battle preparation");

                var canvas=surface.GetComponent<Canvas>();
                foreach(var size in new[]{(1600,900,"web"),(940,1672,"seeker-portrait"),(1871,841,"seeker-landscape"),(1240,1080,"psg1")})
                {
                    Capture(canvas,view,size.Item1,size.Item2,"loading-"+size.Item3);
                    var label=loading.GetComponentInChildren<TMP_Text>();label.ForceMeshUpdate();
                    Check(label.rectTransform.rect.width>0&&label.rectTransform.rect.height>0&&!label.isTextOverflowing,"Loading caption clips on "+size.Item3);
                }

                Set(view,"waitingSince",Time.unscaledTime-7f);Tick(view);
                Check(!frame.gameObject.activeSelf,"Recovery menu appeared before the long-wait threshold");
                Set(view,"waitingSince",Time.unscaledTime-8.1f);Tick(view);
                Check(frame.gameObject.activeSelf&&!loading.gameObject.activeSelf&&back.gameObject.activeInHierarchy,"Long wait did not reveal recovery controls");
                Check(EventSystem.current.currentSelectedGameObject==back.gameObject,"Recovery action did not receive controller focus");
                Check(frame.GetComponentInChildren<TMP_Text>().text=="STILL PREPARING BATTLE","Long wait has no clear user-facing heading");
                Set(game,"replayPreparing",false);Tick(view);
                Check(!surface.gameObject.activeSelf&&EventSystem.current.currentSelectedGameObject!=back.gameObject,"Ready battle retained the loading surface or hidden button focus");

                Set(game,"replayPreparing",true);Set(game,"replayPreparationIssue","Wallet session unavailable. Return to the menu and sign in again.");
                view.Initialize(game);
                Check(frame.gameObject.activeSelf&&back.gameObject.activeInHierarchy,"Preparation failure waited for the timeout before enabling recovery");
                Check(frame.Find("Connection status/Message").GetComponent<TMP_Text>().text==game.PreparationIssue,"Recovery lost the actual connection error");
                foreach(var size in new[]{(1600,900,"web"),(940,1672,"seeker-portrait"),(1871,841,"seeker-landscape"),(1240,1080,"psg1")})
                {
                    Capture(canvas,view,size.Item1,size.Item2,"recovery-"+size.Item3);
                    var label=frame.Find("Connection status/Message").GetComponent<TMP_Text>();label.ForceMeshUpdate();
                    Check(!label.isTextOverflowing,"Recovery detail clips on "+size.Item3);
                }
                Check(!game.ReplayReady&&simulation.Tick==0,"Error UI silently resumed an unverified battle");
                Set(game,"loadingReplay",true);Tick(view);
                Check(!surface.gameObject.activeSelf,"Replay playback exposed live-match preparation UI");
                Set(game,"loadingReplay",false);Set(game,"replayPreparationIssue",null);view.Initialize(game);
                Check(loading.gameObject.activeSelf&&!frame.gameObject.activeSelf,"A new preparation retained the previous error panel");
                Set(game,"replayPreparing",false);Tick(view);
                var result="PASS "+checks+" presentation checks; replay preparation/recovery fixtures passed; 8 platform previews";
                File.WriteAllText(Output+"/result.txt",result);Debug.Log("BATTLE LOADING CHECKS: "+result);
            }
            finally
            {
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(previousFocus);
                if(fixtureEvents)typeof(EventSystem).GetMethod("OnDisable",Private).Invoke(fixtureEvents,null);
                UnityEngine.Object.DestroyImmediate(uiHost);UnityEngine.Object.DestroyImmediate(gameHost);
            }
        }
        static void Capture(Canvas canvas,BattleConnectionNotice view,int width,int height,string name)
        {
            var host=new GameObject("Loading preview camera");var camera=host.AddComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(6,29,54,255);camera.cullingMask=1<<30;
            foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var oldActive=RenderTexture.active;var target=new RenderTexture(width,height,24);Texture2D texture=null;
            try
            {
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();Layout(view);camera.Render();RenderTexture.active=target;
                texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=oldActive;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;
                if(texture)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
