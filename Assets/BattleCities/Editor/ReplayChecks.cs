using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace BattleCities.Tests
{
    public static class ReplayChecks
    {
#if UNITY_EDITOR
        [MenuItem("Battle Cities/Checks/Input replay")]
        public static void RunEditor()
        {
            Run();
            for(int stage=1;stage<=35;stage++)
            {
                var asset=Resources.Load<TextAsset>("Maps/"+stage.ToString("00"));
                var map=Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(asset.text);
                var simulation=new BattleSimulation(map,stage);var recorder=new ReplayRecorder(simulation,map,"campaign-check");
                recorder.BeforeStep(default);simulation.Step(default);recorder.AfterStep();recorder.Finish("aborted");
                var replay=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(recorder.Data)));replay.Step();
                Check(replay.Complete,"Campaign stage "+stage+" failed: "+replay.Error);
            }
            Debug.Log("Replay checks passed: 35 campaign maps, determinism, events, multiplayer, serialization, malformed inputs and tampering.");
        }
#endif
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static MapData Map()=>new MapData {field=new FieldData{widthTiles=13,heightTiles=13},terrain=new BattleCities.Core.TerrainData { regions=new[]{new Region{type="ice",x=64,y=320,width=192,height=64},new Region{type="brick",x=300,y=200,width=64,height=32}} },
            spawn=new SpawnData {player=new SpawnGroup{locations=new[]{new Point{x=128,y=720}}},enemy=new SpawnGroup{locations=new[]{new Point{x=352,y=0}},list=Enumerable.Range(0,40).Select(i=>new EnemySpec{tier="abcd"[i%4].ToString(),drop=i%3==0}).ToArray()}},@base=new BaseData{x=352,y=736}};
        public static BattleReplay Fixture(BattleMode mode=BattleMode.Offline)
        {
            var map=Map();var sim=new BattleSimulation(map);sim.SetReplaySeed(87654321);
            if(mode!=BattleMode.Offline)sim.ConfigureMultiplayer(mode);
            var recorder=new ReplayRecorder(sim,map,"test");
            if(mode!=BattleMode.Offline){sim.SetParticipant(0,true);sim.SetParticipant(1,true);sim.BeginMatch();}
            var pending=new Queue<ReplayEvent>();sim.DropRequested+=()=>pending.Enqueue(new ReplayEvent{kind="pickup"});
            for(int tick=1;tick<=2400&&!sim.Won&&!sim.Lost;tick++)
            {
                if(tick==200||tick==360||tick==1200)pending.Enqueue(new ReplayEvent{kind="powerup",value=tick==360?"defence":tick==1200?"wipeout":"shield"});
                if(tick==280)pending.Enqueue(new ReplayEvent{kind="pickup",value="freeze"});
                if(tick==500)pending.Enqueue(new ReplayEvent{kind="pickup"});
                while(pending.Count>0){var e=pending.Dequeue();recorder.RecordEvent(e);ReplayPlayer.ApplyEvent(sim,e);}
                if(mode!=BattleMode.Offline&&tick==800){sim.SetParticipant(1,false);sim.SetParticipant(1,true);}
                var cmd=new Command{Move=tick%180<90?Facing.Up:Facing.Right,Aim=tick%240<120?Facing.Up:Facing.Left,Fire=tick%17==0,PowerShot=tick%51==0,SecondaryFire=tick%150==0};
                if(mode==BattleMode.Offline)sim.EquippedSecondary=(SecondaryAttack)(1+(tick/300)%4);
                var online=new Dictionary<int,Command>{{0,cmd},{1,new Command{Move=Facing.Left,Aim=Facing.Up,Fire=tick%13==0}}};
                recorder.BeforeStep(cmd,mode==BattleMode.Offline?null:online);
                if(mode==BattleMode.Offline)sim.Step(cmd);else sim.StepMultiplayer(online);
                recorder.AfterStep();
            }
            recorder.Finish("aborted");return recorder.Data;
        }
        public static void Run()
        {
            ReplayHashChecks.Run();
            for(int bits=0;bits<=511;bits++)if((bits&7)<=4&&((bits>>3)&7)<=4)Check(ReplayRecorder.Pack(ReplayRecorder.Unpack(bits))==bits,"Input bit packing changed");
            foreach(var mode in new[]{BattleMode.Offline,BattleMode.Coop,BattleMode.Versus})
            {
                var recorded=Fixture(mode);var data=ReplayJson.Read(ReplayJson.Write(recorded));
                var player=new ReplayPlayer(data);
                while(!player.Complete&&player.Error==null)player.Step();
                Check(player.Complete,"Replay mismatch ("+mode+"): "+player.Error);
                var second=new ReplayPlayer(ReplayJson.Copy(data));while(!second.Complete&&second.Error==null)second.Step();
                Check(second.Complete,"Second playback mismatch: "+second.Error);
            }
            var source=Fixture();
            var changed=ReplayJson.Copy(source);changed.checkpoints[1].stateHash=new string('0',64);
            var tampered=new ReplayPlayer(changed);while(!tampered.Complete&&tampered.Error==null)tampered.Step();
            Check(tampered.Error!=null,"Tampered checkpoint accepted");
            changed=ReplayJson.Copy(source);changed.claimedResult.score++;
            tampered=new ReplayPlayer(changed);while(!tampered.Complete&&tampered.Error==null)tampered.Step();Check(tampered.Error!=null,"Forged result accepted");
            changed=ReplayJson.Copy(source);changed.inputs[0].commands[0]=7;Reject(changed,"Invalid facing accepted");
            changed=ReplayJson.Copy(source);changed.inputs[0].tick=2;Reject(changed,"Input gap accepted");
            changed=ReplayJson.Copy(source);changed.events[0].tick=0;Reject(changed,"Tick zero event accepted");
            changed=ReplayJson.Copy(source);changed.checkpoints.RemoveAt(1);Reject(changed,"Missing checkpoint accepted");
            changed=ReplayJson.Copy(source);changed.map.field.widthTiles++;Reject(changed,"Changed map accepted");
            changed=ReplayJson.Copy(source);changed.config.normalReload=.001f;Reject(changed,"Changed weapon configuration accepted");
            changed=ReplayJson.Copy(source);changed.simulationVersion="future";Reject(changed,"Unsupported version accepted");
            changed=ReplayJson.Copy(source);changed.map.terrain.regions[0].width=float.PositiveInfinity;changed.levelHash=ReplayJson.Hash(changed.map);Reject(changed,"Unbounded map accepted");
            var sim=new BattleSimulation(Map());var rec=new ReplayRecorder(sim,Map(),"test");rec.Reseed(33);rec.BeforeStep(default);sim.Step(default);rec.AfterStep();rec.Finish("aborted");
            var shortReplay=new ReplayPlayer(rec.Data);shortReplay.Step();Check(shortReplay.Complete,"Single tick recording failed");
            Check(rec.Data.checkpoints.Count==2,"Single tick terminal checkpoint missing");
        }
        static void Reject(BattleReplay data,string message)
        {try{ReplayJson.Validate(data);}catch(FormatException){return;}throw new Exception(message);}
    }
}
