#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using BattleCities.Core;
using Fusion;
using UnityEngine;

namespace BattleCities.Multiplayer
{
    public sealed class BattleTeamSmoke : MonoBehaviour
    {
        string role,folder;bool connecting,finished,hadFour,rejoinRequested;float start,last;int initialTick,initialSlot=-1;float soakSeconds,readyAt;bool adverse;
        BattleSession Session=>BattleSession.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--bc-team-test");
            if(i<0||i+2>=args.Length)return;
            var go=new GameObject("Team network check");DontDestroyOnLoad(go);
            var test=go.AddComponent<BattleTeamSmoke>();test.role=args[i+1];test.folder=args[i+2];test.start=Time.realtimeSinceStartup;
            test.adverse=Array.IndexOf(args,"--bc-adverse-network")>=0;
            int duration=Array.IndexOf(args,"--bc-soak-seconds");
            if(duration>=0&&duration+1<args.Length&&float.TryParse(args[duration+1],out var seconds))test.soakSeconds=Mathf.Clamp(seconds,30,7200);
            if(test.adverse&&test.role!="host")
            {
                var conditions=NetworkProjectConfig.Global.NetworkConditions;
                conditions.Enabled=true;conditions.DelayMin=.15f;conditions.DelayMax=.15f;
                conditions.AdditionalJitter=.05f;conditions.LossChanceMin=.05f;conditions.LossChanceMax=.05f;
                conditions.AdditionalLoss=0;
            }
        }
        async void Update()
        {
            if(finished||!Session)return;
            try
            {
                if(Time.realtimeSinceStartup-start>Mathf.Max(160,soakSeconds+120)){Finish(false,"timeout: "+Session.Status);return;}
                if(!connecting)
                {
                    string code=Path.Combine(folder,"room.txt");if(role!="host"&&!File.Exists(code))return;
                    connecting=true;Directory.CreateDirectory(folder);Session.SelectedMode=BattleMode.TeamBattle;Session.SelectedMap=1;
                    await Session.Connect(role=="host",role=="host"?null:File.ReadAllText(code));
                    if(!Session.Online){Finish(false,Session.Status);return;}
                    Session.TestInput=()=>
                    {
                        int move=-1;
                        if((adverse||soakSeconds>0)&&Session.Match&&Session.Match.Started)
                            move=((int)(Time.realtimeSinceStartup*2)%2==0)?0:2;
                        return new BattleNetworkInput {Move=move,Aim=move,Secondary=1};
                    };
                    if(role=="host")File.WriteAllText(code,Session.RoomCode);
                }
                var m=Session.Match;if(!m||m.Simulation==null)return;var s=m.Simulation;
                if(m.PlayerCount==m.RequiredPlayers){hadFour=true;if(Session.IsHost&&!m.Started)m.RequestStart();}
                if(Session.IsHost){s.DisableEnemyFire=true;s.Freeze=999;}
                if(!m.Started)return;
                if(Time.realtimeSinceStartup-last>1)
                {
                    last=Time.realtimeSinceStartup;
                    File.WriteAllText(Path.Combine(folder,role+"-status.json"),JsonUtility.ToJson(new Report {success=false,message=Session.Status,tick=s.Tick,players=m.PlayerCount,host=Session.IsHost,resume=Session.Runner.IsResume,slot=m.LocalSlot}));
                }
                if(hadFour&&s.Tick>360&&initialTick==0)
                {
                    if(s.Tanks.Count(t=>t.Player)!=m.RequiredPlayers||!s.BaseAlive||(s.IsTeamBattle&&!s.RivalBaseAlive))throw new Exception("Invalid player count or base state");
                    initialTick=s.Tick;initialSlot=m.LocalSlot;readyAt=Time.realtimeSinceStartup;File.WriteAllText(Path.Combine(folder,role+"-ready.txt"),s.Tick.ToString());
                }
                if(initialTick==0)return;
                if(m.LocalSlot>=0&&m.LocalSlot!=initialSlot)throw new Exception("Player slot changed during recovery");
                if(soakSeconds>0&&Time.realtimeSinceStartup-readyAt>=soakSeconds)
                    Finish(m.PlayerCount==4&&s.Tick>initialTick+600,"four-player sustained session completed");
                string action=File.Exists(Path.Combine(folder,"action.txt"))?File.ReadAllText(Path.Combine(folder,"action.txt")).Trim():"";
                if(action=="finish"&&s.Tick>initialTick+60)Finish(true,"configured player count and arena replicated");
                if(action=="migrate"&&Session.Runner.IsResume&&s.Tick>initialTick+180&&m.PlayerCount==3)
                    Finish(hadFour&&s.Tanks.Any(t=>!t.Player&&t.Slot==0)&&s.Tanks.Any(t=>!t.Player&&t.Slot==1),"host migration resumed both teams and AI");
                if(action=="rejoin"&&role=="client1"&&!rejoinRequested)
                {rejoinRequested=true;await Session.Runner.Shutdown(shutdownReason:ShutdownReason.ConnectionTimeout);}
                if(action=="rejoin"&&(role!="client1"||rejoinRequested)&&s.Tick>initialTick+420&&m.PlayerCount==4&&!Session.Recovering)
                    Finish(true,"all four slots retained after client reconnect");
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        void Finish(bool ok,string message)
        {
            if(finished)return;finished=true;
            var m=Session?Session.Match:null;
            File.WriteAllText(Path.Combine(folder,role+".json"),JsonUtility.ToJson(new Report {success=ok,message=message,tick=m?m.StateTick:0,players=m?m.PlayerCount:0,host=Session&&Session.IsHost,resume=Session&&Session.Runner&&Session.Runner.IsResume,slot=m?m.LocalSlot:-1},true));
            Debug.Log("TEAM CHECK "+ok+" "+message);
            // Keep successful peers alive until the orchestrator has collected every result.
        }
        [Serializable]sealed class Report {public bool success,host,resume;public string message;public int tick,players,slot;}
    }
}
#endif
