#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using BattleCities.Core;
using BattleCities.Multiplayer;
using BattleCities.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    // Opt-in development player check for the real Login -> Start -> Photon flow.
    public sealed class BattleLoginSmoke : MonoBehaviour
    {
        private string reportPath, guestId;
        private bool expectTwo;
        private float startedAt;
        private bool loggedIn, started, finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--bc-login-smoke");
            if(index<0||index+1>=args.Length)return;
            var go=new GameObject("Login launch smoke test");DontDestroyOnLoad(go);
            var test=go.AddComponent<BattleLoginSmoke>();
            test.reportPath=args[index+1];test.expectTwo=Array.IndexOf(args,"--bc-expect-two")>=0;
            test.startedAt=Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if(finished)return;
            try
            {
                if(Time.realtimeSinceStartup-startedAt>70){Finish(false,"Timed out in "+SceneManager.GetActiveScene().name);return;}
                var scene=SceneManager.GetActiveScene().name;
                if(!loggedIn)
                {
                    if(scene!="Login"){Finish(false,"Startup scene was "+scene);return;}
                    var login=FindFirstObjectByType<LoginScene>();
                    var api=FindFirstObjectByType<MainMenuApiClient>();
                    if(!login||!api||!api.isActiveAndEnabled)return;
                    loggedIn=true;login.ContinueAsGuest();guestId=MainMenuApiClient.CurrentGuestId;
                    if(string.IsNullOrEmpty(guestId)||!guestId.StartsWith("guest-")){Finish(false,"Guest ID missing at login");return;}
                }
                if(!started&&scene=="MainMenu")
                {
                    if(MainMenuApiClient.CurrentGuestId!=guestId){Finish(false,"Guest ID changed between scenes");return;}
                    var menu=FindFirstObjectByType<MainMenuScene>();if(!menu)return;
                    started=true;menu.StartBattle();
                }
                var session=BattleSession.Instance;
                if(!started||!session||!session.Online||!session.Match)return;
                if(expectTwo&&(!session.Match.Started||session.Match.PlayerCount<2))return;
                var expected=BattleLaunchOptions.Mode;
                if(session.Match.Mode==BattleMode.Offline)return;
                bool success=scene=="BattleCity"&&session.Match.Mode==expected&&
                    session.Runner.UserId==guestId&&session.QuickMatching&&
                    (expectTwo?session.Match.Started&&session.Match.PlayerCount==2:session.Lobby.Visible);
                Finish(success,"scene="+scene+", expected="+expected+", actual="+session.Match.Mode+
                    ", players="+session.Match.PlayerCount+", room="+session.RoomCode+
                    ", photonUserId="+session.Runner.UserId+", guestId="+guestId);
            }
            catch(Exception error){Finish(false,error.ToString());}
        }

        [Serializable] private sealed class Report
        {
            public string scene,mode,guestId,photonUserId,room,message;
            public int players;
            public bool success;
        }
        private void Finish(bool success,string message)
        {
            if(finished)return;finished=true;
            var session=BattleSession.Instance;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath,JsonUtility.ToJson(new Report{
                scene=SceneManager.GetActiveScene().name,mode=BattleLaunchOptions.Mode.ToString(),
                guestId=guestId,photonUserId=session&&session.Runner?session.Runner.UserId:null,
                room=session?session.RoomCode:null,players=session&&session.Match?session.Match.PlayerCount:0,
                message=message,success=success},true));
            Debug.Log("LOGIN LAUNCH SMOKE "+(success?"PASS":"FAIL")+" "+message);
            var captureDir=Environment.GetEnvironmentVariable("BATTLE_CITIES_TERRAIN_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(captureDir)){StartCoroutine(CaptureFrame(captureDir,success));return;}
            _=Exit(success,expectTwo?1500:0);
        }
        private IEnumerator CaptureFrame(string directory,bool success)
        {
            yield return new WaitForSecondsRealtime(1);
            if(BattleSession.Instance&&BattleSession.Instance.Lobby)BattleSession.Instance.Lobby.Hide();
            var camera=Camera.main?Camera.main:FindFirstObjectByType<Camera>();
            if(camera)
            {
                var target=new RenderTexture(1024,768,24,RenderTextureFormat.ARGB32);
                var pixels=new Texture2D(1024,768,TextureFormat.RGB24,false);
                var oldTarget=camera.targetTexture;
                var oldActive=RenderTexture.active;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1024,768),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory,"terrain.png"),pixels.EncodeToPNG());
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                Destroy(target);Destroy(pixels);
            }
            yield return new WaitForSecondsRealtime(1);
            _=Exit(success,0);
        }
        private static async Task Exit(bool success,int graceMs)
        {
            if(graceMs>0)await Task.Delay(graceMs);
            try{if(BattleSession.Instance)await BattleSession.Instance.Leave();}
            finally{Application.Quit(success?0:1);}
        }
    }
}
#endif
