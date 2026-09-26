#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Multiplayer
{
    // Opt-in development-build harness. No automation runs in normal games.
    public sealed class BattleMultiplayerSmoke : MonoBehaviour
    {
        private string role,folder,mode;
        private float startedAt,lastReport;
        private bool connecting,fixture,finished,stageProgression;
        private int sequence;
        private int nextShotTick=160;
        private float initialX,initialY,maxTravel,maxRemoteCharge;
        private int observedShots;
        private BattleSession Session=>BattleSession.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--bc-smoke");
            if(index<0||index+3>=args.Length)return;
            var go=new GameObject("Photon smoke test");DontDestroyOnLoad(go);
            var test=go.AddComponent<BattleMultiplayerSmoke>();test.role=args[index+1];test.folder=args[index+2];test.mode=args[index+3];test.startedAt=Time.realtimeSinceStartup;
            test.stageProgression=Array.IndexOf(args,"--bc-stage-smoke")>=0;
        }
        private async void Update()
        {
            if(finished||!Session)return;
            try
            {
                if(Time.realtimeSinceStartup-startedAt>110){Finish(false,"Timed out: "+Session.Status);return;}
                if(!connecting)
                {
                    string codeFile=Path.Combine(folder,"room.txt");
                    if(role=="client"&&!File.Exists(codeFile))return;
                    connecting=true;Directory.CreateDirectory(folder);Session.SelectedMap=1;Session.SelectedMode=mode=="coop"?BattleMode.Coop:BattleMode.Versus;
                    var menu=SceneManager.LoadSceneAsync("MainMenu");while(!menu.isDone)await Task.Yield();
                    await Session.Connect(role=="host",role=="client"?File.ReadAllText(codeFile):"");
                    if(!Session.Online){Finish(false,Session.Status);return;}
                    Session.TestInput=Input;
                    if(role=="host")File.WriteAllText(codeFile,Session.RoomCode);
                }
                var match=Session.Match;if(!match||match.Simulation==null)return;
                var s=match.Simulation;
                if(role=="host"&&match.PlayerCount>=2&&!s.MatchStarted&&(!stageProgression||match.Map==1))
                {
                    match.RequestStart();Session.Lobby.Hide();
                }
                if(!s.MatchStarted)return;
                if(role=="host"&&!fixture)
                {
                    fixture=true;
                    // Use a controlled arena to isolate transport from AI and map collision.
                    s.DisableEnemyFire=true;s.Freeze=999;
                    foreach(var wall in s.Terrain)wall.Alive=false;
                    var p=s.Tanks.Single(t=>t.Slot==0);p.X=160;p.Y=500;
                    var q=s.Tanks.Single(t=>t.Slot==1);q.X=500;q.Y=500;
                    initialX=q.X;initialY=q.Y;
                    s.ShotFired+=shot=>{if(shot.OwnerSlot==1)observedShots++;};
                }
                if(stageProgression)
                {
                    if(role=="host"&&match.Map==1&&s.Tick>180)
                    {
                        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                        typeof(BattleSimulation).GetField("wave",flags).SetValue(s,Array.Empty<EnemySpec>());
                        typeof(BattleSimulation).GetField("spawned",flags).SetValue(s,0);
                        s.Tanks.RemoveAll(t=>!t.Player);
                    }
                    if(match.Map==2&&s.Stage==2&&s.Tick>30)
                        Finish(match.Round==2&&match.PlayerCount==2&&s.MatchStarted&&s.Terrain.Any(w=>w.Alive)&&!Session.Lobby.Visible,
                            "automatic stage="+match.Map+", round="+match.Round+", players="+match.PlayerCount+", started="+s.MatchStarted+", menu="+Session.Lobby.Visible);
                    return;
                }
                var remote=s.Tanks.FirstOrDefault(t=>t.Slot==1);
                if(role=="host"&&remote!=null)maxTravel=Mathf.Max(maxTravel,Vector2.Distance(new Vector2(initialX,initialY),new Vector2(remote.X,remote.Y)));
                if(role=="client")maxRemoteCharge=Mathf.Max(maxRemoteCharge,match.ChargeProgress[0]);
                if(Time.realtimeSinceStartup-lastReport>1)
                {
                    lastReport=Time.realtimeSinceStartup;
                    Report(false,"running",s.Tick,match.PlayerCount,remote?.X??0,remote?.Y??0);
                }
                if(s.Tick>420)
                {
                    bool success=role=="host"?maxTravel>30&&observedShots>0:
                        match.PlayerCount==2&&s.Tanks.Count(t=>t.Player)==2&&s.Terrain.All(w=>!w.Alive)&&match.EventSequence>0&&maxRemoteCharge>.3f;
                    Finish(success,role=="host"?"remote travel="+maxTravel+", remote shots="+observedShots:"received tanks, terrain destruction, events and charge="+maxRemoteCharge);
                }
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        private BattleNetworkInput Input()
        {
            int tick=Session.Match?Session.Match.StateTick:0;
            if(role=="host")return new BattleNetworkInput{Move=-1,Aim=-1,Secondary=1,ChargeHeld=tick>=250&&tick<320};
            if(tick>=nextShotTick){sequence++;nextShotTick=tick+20;}
            return new BattleNetworkInput{Move=tick>130&&tick<230?(int)Facing.Up:-1,Aim=(int)Facing.Right,ShotSequence=sequence,Secondary=1};
        }
        private void Report(bool success,string message,int tick,int players,float x,float y)
        {
            File.WriteAllText(Path.Combine(folder,role+".json"),JsonUtility.ToJson(new SmokeReport{role=role,mode=mode,success=success,message=message,tick=tick,players=players,x=x,y=y},true));
        }
        private void Finish(bool success,string message)
        {
            if(finished)return;finished=true;
            var match=Session?Session.Match:null;
            Report(success,message,match?match.StateTick:0,match?match.PlayerCount:0,0,0);
            Debug.Log("PHOTON SMOKE "+(success?"PASS":"FAIL")+" "+role+" "+message);
            if(Session)Session.TestInput=null;
            _=ExitLater(success);
        }
        private async Task ExitLater(bool success)
        {
            // Give the other peer time to record its last snapshot before disconnecting.
            await Task.Delay(3500);
            try{if(Session)await Session.Leave();}finally{Application.Quit(success?0:1);}
        }
        [Serializable] private sealed class SmokeReport {public string role,mode,message;public bool success;public int tick,players;public float x,y;}
    }
}
#endif
