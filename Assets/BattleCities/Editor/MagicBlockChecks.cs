using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using BattleCities.Core;
using BattleCities.UI;
using UnityEngine;
namespace BattleCities.Tests
{
    public static class MagicBlockChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Drain(IEnumerator routine)
        {
            while(routine.MoveNext())if(routine.Current is IEnumerator nested)Drain(nested);
        }
        static IEnumerator Respond(Action<long,JObject,string> done,long code,JObject body)
        {done(code,body,null);yield break;}
        static void CheckPreMatchSetup()
        {
            var host=new GameObject("MagicBlock setup checks");host.SetActive(false);
            try
            {
                var api=host.AddComponent<MainMenuApiClient>();
                foreach(long code in new long[]{200,201,401,503})
                {
                    api.EditorRequestOverride=(method,path,body,done)=>{
                        Check(method=="POST"&&path=="/api/session"&&(string)body["provider"]=="guest","Login endpoint");
                        return Respond(done,code,new JObject{{"authenticated",code<400}});
                    };
                    bool authenticated=false;
                    Drain(api.PrepareMagicAuthentication(value=>authenticated=value));
                    Check(authenticated==(code<400),"Existing/new guest authentication and login errors");
                }
                var drops=host.AddComponent<MagicBlockDrops>();
                var routines=new Queue<IEnumerator>();drops.EditorRoutineOverride=routines.Enqueue;
                int sessions=0,requests=0;long setupStatus=201;
                drops.EditorRequestOverride=(method,path,body,done)=>{
                    if(path=="/api/magicblock/session")
                    {
                        sessions++;Check((int)body["width"]==832,"Map-bound session width");
                        return Respond(done,setupStatus,new JObject{{"token","test-token"},{"expires",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+3600000}});
                    }
                    Check(path=="/api/magicblock/drop","Drops must never perform authentication");
                    requests++;return Respond(done,200,new JObject{{"status","ready"},{"value","life|64|64|8zNbfcBUVCcNkMs4rEKkPv9UsfEwemEdd2bfMerNwd52"}});
                };
                var map=new MapData{field=new FieldData{widthTiles=13,heightTiles=13}};
                var sim=new BattleSimulation(map);int callbacks=0;
                drops.Prepare(sim);
                Check(!drops.Ready&&sim.Tick==0,"Match must wait for setup");
                drops.Enqueue(sim,_=>callbacks++);Check(routines.Count==1,"No lazy first-drop setup");
                Drain(routines.Dequeue());Check(drops.Ready&&sessions==1,"Session ready before first drop");
                drops.Enqueue(sim,_=>callbacks++);Drain(routines.Dequeue());
                Check(requests==1&&callbacks==1&&sessions==1,"First drop only requests VRF");
                drops.Prepare(new BattleSimulation(map));
                drops.Enqueue(sim,_=>callbacks++);Check(routines.Count==1,"Previous simulation cannot enqueue");
                setupStatus=401;Drain(routines.Dequeue());
                Check(!drops.Ready&&callbacks==1,"Failed setup keeps combat blocked");
                setupStatus=201;
                drops.Prepare(sim);var stale=routines.Dequeue();
                var replacement=new BattleSimulation(map);drops.Prepare(replacement);
                Drain(stale);Check(!drops.Ready,"Stale setup cannot unlock replacement match");
                Drain(routines.Dequeue());Check(drops.Ready,"Replacement setup completes");
                drops.StopForRecovery(replacement);drops.Enqueue(replacement,_=>callbacks++);
                Check(drops.Ready&&routines.Count==0,"Recovery cannot restart session during combat");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);}
            Debug.Log("MAGICBLOCK_PREMATCH_CHECKS_PASSED: login, loading gate, first drop, failures and stale sessions");
        }
        public static void Run()
        {
            Check(MainMenuApiClient.IsMagicWebUrl("https://magic.battlecities.com/game"),"Magic host");
            Check(MainMenuApiClient.IsTestWebUrl("https://magic.battlecities.com"),"Magic must use test API");
            Check(MainMenuApiClient.IsTestWebUrl("https://test.battlecities.com"),"Test API retained");
            foreach(var url in new[]{"https://test.battlecities.com","https://play.battlecities.com","https://magic.battlecities.com.evil.org","http://magic.battlecities.com","https://magic.battlecities.com:444","https://play.battlecities.com/?magic.battlecities.com"})
                Check(!MainMenuApiClient.IsMagicWebUrl(url),"Unexpected activation: "+url);
            var data=ReplayChecks.Fixture();
            var e=data.events.Find(item=>item.kind=="pickup");
            e.kind="magicpickup";e.value="shield|64|64|8zNbfcBUVCcNkMs4rEKkPv9UsfEwemEdd2bfMerNwd52";
            ReplayJson.Validate(data);
            Check(!MagicDropEvent.Valid("shield|999999|64|8zNbfcBUVCcNkMs4rEKkPv9UsfEwemEdd2bfMerNwd52",832,832),"Out of bounds drop");
            var map=new MapData{field=new FieldData{widthTiles=13,heightTiles=13}};
            var sim=new BattleSimulation(map);var recorder=new ReplayRecorder(sim,map,"test+magic");
            recorder.RecordEvent(e);ReplayPlayer.ApplyEvent(sim,e);
            for(int i=0;i<120;i++){recorder.BeforeStep(default);sim.Step(default);recorder.AfterStep();}
            recorder.Finish("aborted");var player=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(recorder.Data)));
            while(!player.Complete&&player.Error==null)player.Step();
            Check(player.Error==null,"Magic drop replay diverged: "+player.Error);
            CheckPreMatchSetup();
            ReplayChecks.Run();
            Debug.Log("MAGICBLOCK_CHECKS_PASSED: origins, devnet routing, explicit drops and replay determinism");
        }
    }
}
