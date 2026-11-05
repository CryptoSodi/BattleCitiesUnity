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
    // Explicit opt-in; runs only with synthetic local accounts and an isolated API.
    public sealed class BattlePowerupSmoke : MonoBehaviour
    {
        [Serializable] private sealed class Account {public string playerId,cookie;}
        [Serializable] private sealed class Report {public string role,message;public bool success;public int hostLives,clientLives;}
        private string role,folder,url,playerId;private float started,successAt=-1,migrationAt=-1;private bool connecting,configured,activated,retried,finished,migrationTest,inflightTest,abandonTest,leavingMatch,migrated;
        private EconomyClient economy;private EconomyClient.Consumption receipt;private BattleSession Session=>BattleSession.Instance;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--bc-powerup-smoke");
            if(index<0||index+2>=args.Length)return;
            var host=new GameObject("Isolated multiplayer powerup test");DontDestroyOnLoad(host);
            var test=host.AddComponent<BattlePowerupSmoke>();test.role=args[index+1];test.folder=args[index+2];test.started=Time.realtimeSinceStartup;
            test.migrationTest=Array.IndexOf(args,"--bc-powerup-migrate")>=0;
            test.inflightTest=Array.IndexOf(args,"--bc-powerup-inflight")>=0;
            test.abandonTest=Array.IndexOf(args,"--bc-powerup-abandon")>=0;
        }
        private async void Update()
        {
            if(finished||!Session)return;
            try
            {
                if(Time.realtimeSinceStartup-started>(migrationTest||inflightTest?210:145)){Finish(false,"Timed out: "+Session.Status+"; economy="+(economy?economy.Status:"not configured"));return;}
                if(abandonTest&&role=="client"&&leavingMatch)
                {
                    string file=Path.Combine(folder,"ECONOMY",playerId+".json");
                    if(economy&&economy.Authenticated&&economy.PendingOnline==null&&File.Exists(file))
                    {
                        var account=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
                        if((int?)account["inventory"]?["extra-life"]==1){Finish(true,"An abandoned unactivated charge returned to inventory.");return;}
                    }
                    return;
                }
                if(!connecting)
                {
                    string room=Path.Combine(folder,"room.txt");if(role=="client"&&!File.Exists(room))return;
                    connecting=true;Session.SelectedMode=BattleMode.Coop;Session.SelectedMap=1;
                    var load=SceneManager.LoadSceneAsync("MainMenu");while(!load.isDone)await Task.Yield();
                    await Session.Connect(role=="host",role=="client"?File.ReadAllText(room):"");
                    if(!Session.Online){Finish(false,Session.Status);return;}
                    Session.TestInput=()=>new BattleNetworkInput{Move=-1,Aim=-1,Secondary=1};
                    if(role=="host")File.WriteAllText(room,Session.RoomCode);
                }
                var match=Session.Match;if(!match||!match.Game||match.Simulation==null)return;
                if(!configured)
                {
                    configured=true;url=File.ReadAllText(Path.Combine(folder,"api-url.txt"));
                    if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Host!="127.0.0.1")throw new Exception("Smoke API must be localhost.");
                    var account=JsonUtility.FromJson<Account>(File.ReadAllText(Path.Combine(folder,role+"-account.json")));
                    playerId=account.playerId;
                    economy=match.Game.GetComponent<EconomyClient>();if(!economy)throw new Exception("Missing EconomyClient");
                    // Set after EconomyClient.Start so the smoke fixture uses its direct cookie transport.
                    BattlePreparation.Set(0,url,account.playerId,"guest");await economy.Configure(url,account.cookie);
                    if(!economy.Authenticated||economy.SlotType(0)!="life")throw new Exception("Local test account not loaded: "+economy.Status);
                    File.WriteAllText(Path.Combine(folder,role+"-ready.txt"),"ready");
                }
                if(role=="host"&&match.PlayerCount>=2&&!match.Started&&File.Exists(Path.Combine(folder,"client-ready.txt")))match.RequestStart();
                if(!match.Started)return;
                Session.Lobby.Hide();var simulation=match.Simulation;
                if(role=="host"){simulation.DisableEnemyFire=true;simulation.Freeze=999;}
                if(inflightTest&&role=="host"&&File.Exists(Path.Combine(folder,"redemption-inflight.txt")))
                {File.WriteAllText(Path.Combine(folder,"host-killed.txt"),"Host stopped while receipt verification was in flight.");Environment.Exit(99);return;}
                if(!activated&&simulation.Tick>(inflightTest&&role=="client"?450:40)){activated=true;match.Game.UsePowerupSlot(0);}
                if(abandonTest&&role=="client"&&activated&&economy.PendingOnline!=null&&File.Exists(Path.Combine(folder,"dropped-response.txt")))
                {leavingMatch=true;await Session.Leave();return;}
                if(abandonTest&&role=="host"&&File.Exists(Path.Combine(folder,"client-powerup-result.json")))
                {
                    var client=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(folder,"client-powerup-result.json")));
                    Finish(client.success&&simulation.Participants[1].Lives==3,"Client left before activation; its unused item was refunded and no extra life was applied.");return;
                }
                if(receipt==null&&economy.PendingOnline!=null)receipt=economy.PendingOnline;
                int one=simulation.Participants[0].Lives,two=simulation.Participants[1].Lives;
                if(inflightTest&&role=="client"&&Session.IsHost&&File.Exists(Path.Combine(folder,"host-killed.txt"))&&!migrated)
                {migrated=true;migrationAt=Time.realtimeSinceStartup;}
                if(inflightTest&&migrated&&one==4&&two==4&&economy.PendingOnline==null)
                {Finish(true,"In-flight receipt recovered after host migration; each player has one additional life.");return;}
                if(one==4&&two==4&&receipt!=null&&!retried){retried=true;match.SendPowerup(receipt);successAt=Time.realtimeSinceStartup;}
                if(migrationTest&&retried&&role=="host"&&Time.realtimeSinceStartup-successAt>9)
                {
                    File.WriteAllText(Path.Combine(folder,"host-killed.txt"),"Abrupt host exit after a migration snapshot.");
                    Environment.Exit(99);return;
                }
                if(migrationTest&&retried&&role=="client"&&Session.IsHost&&File.Exists(Path.Combine(folder,"host-killed.txt"))&&!migrated)
                {migrated=true;migrationAt=Time.realtimeSinceStartup;match.SendPowerup(receipt);}
                if(migrationTest&&migrated&&Time.realtimeSinceStartup-migrationAt>5)
                {Finish(one==4&&two==4&&economy.PendingOnline==null,"Host migrated; retrying the previously applied receipt did not add another life.");return;}
                if(!migrationTest&&!inflightTest&&!abandonTest&&retried&&Time.realtimeSinceStartup-successAt>3)
                    Finish(one==4&&two==4&&economy.PendingOnline==null&&File.Exists(Path.Combine(folder,"dropped-response.txt")),
                        "Both players received one Extra Life; duplicate receipt sent; client consumption response was lost and retried.");
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        private void Finish(bool success,string message)
        {
            if(finished)return;finished=true;var simulation=Session&&Session.Match?Session.Match.Simulation:null;
            var result=new Report{role=role,success=success,message=message,hostLives=simulation?.Participants[0].Lives??-1,clientLives=simulation?.Participants[1].Lives??-1};
            File.WriteAllText(Path.Combine(folder,role+"-powerup-result.json"),JsonUtility.ToJson(result,true));
            Debug.Log("POWERUP SMOKE "+(success?"PASS ":"FAIL ")+role+" "+message);
            if(Session)Session.TestInput=null;_=Exit(success);
        }
        private async Task Exit(bool success){await Task.Delay(3500);if(Session)await Session.Leave();Application.Quit(success?0:1);}
    }
}
#endif
