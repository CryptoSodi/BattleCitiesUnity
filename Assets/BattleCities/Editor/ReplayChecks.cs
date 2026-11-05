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
        public static BattleReplay Fixture(BattleMode mode=BattleMode.Offline,int startControlVersion=1)
        {
            var map=Map();var sim=new BattleSimulation(map);sim.SetReplaySeed(87654321);
            var configuration=sim.ReplayConfiguration();configuration.startControlVersion=startControlVersion;sim.ApplyReplayConfiguration(configuration);
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
            var immediate=new BattleSimulation(Map());
            float startY=immediate.Player.Y;int shots=0;immediate.ShotFired+=_=>shots++;
            Check(immediate.CanAcceptPlayerFire&&immediate.CanUseSecondary,"New solo battles accept fire and special input immediately");
            immediate.Step(new Command{Move=Facing.Up,Fire=true});
            Check(immediate.Player.Y<startY&&shots==1,"New solo battles move and fire on their first tick");
            var old=new BattleSimulation(Map());var oldConfig=old.ReplayConfiguration();oldConfig.startControlVersion=0;old.ApplyReplayConfiguration(oldConfig);
            startY=old.Player.Y;for(int i=0;i<60;i++)old.Step(new Command{Move=Facing.Up,Fire=true});
            Check(old.Player.Y==startY&&!old.CanAcceptPlayerFire,"Old recordings retain the original start delay");
            var online=new BattleSimulation(Map());online.ConfigureMultiplayer(BattleMode.Coop);online.SetParticipant(0,true);online.SetParticipant(1,true);
            Check(!online.CanAcceptPlayerFire,"Multiplayer input remains gated until the match begins");
            online.BeginMatch();startY=online.Player.Y;shots=0;online.ShotFired+=_=>shots++;
            online.StepMultiplayer(new Dictionary<int,Command>{{0,new Command{Move=Facing.Up,Fire=true}}});
            Check(online.Player.Y<startY&&shots==1,"Multiplayer battles accept input on the first started tick");
            var lifeMap=Map();var lifeSimulation=new BattleSimulation(lifeMap);var lifeRecorder=new ReplayRecorder(lifeSimulation,lifeMap,"extra-life-check");
            var lifeEvent=new ReplayEvent{kind="powerup",value="life"};lifeRecorder.RecordEvent(lifeEvent);ReplayPlayer.ApplyEvent(lifeSimulation,lifeEvent);
            lifeRecorder.BeforeStep(default);lifeSimulation.Step(default);lifeRecorder.AfterStep();lifeRecorder.Finish("aborted");
            Check(lifeSimulation.Lives==4&&!lifeRecorder.Data.debugUsed,"A consumed Extra Life grants one life without marking the match as debug");
            var lifeReplay=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(lifeRecorder.Data)));lifeReplay.Step();
            Check(lifeReplay.Complete&&lifeReplay.Simulation.Lives==4,"Extra Life use reproduces in the recorded match");
            foreach(string type in new[]{"shield","defence","freeze","speed","upgrade","zoomout","wipeout","life","healmax","heal2","rapid","rapidlong"})
            {
                var powerMap=Map();var powerSimulation=new BattleSimulation(powerMap);var powerRecorder=new ReplayRecorder(powerSimulation,powerMap,"powerup-check");
                var powerEvent=new ReplayEvent{kind="powerup",value=type};powerRecorder.RecordEvent(powerEvent);ReplayPlayer.ApplyEvent(powerSimulation,powerEvent);
                powerRecorder.BeforeStep(default);powerSimulation.Step(default);powerRecorder.AfterStep();powerRecorder.Finish("aborted");
                Check(!powerRecorder.Data.debugUsed,"Owned "+type+" must remain eligible for normal submission");
                var powerReplay=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(powerRecorder.Data)));powerReplay.Step();
                Check(powerReplay.Complete,"Recorded "+type+" must reproduce: "+powerReplay.Error);
            }
            foreach(var mode in new[]{BattleMode.Coop,BattleMode.Versus})
            {
                var powerMap=Map();var powerSimulation=new BattleSimulation(powerMap);powerSimulation.ConfigureMultiplayer(mode);
                var powerRecorder=new ReplayRecorder(powerSimulation,powerMap,"multiplayer-powerup-check");
                powerSimulation.SetParticipant(0,true);powerSimulation.SetParticipant(1,true);powerSimulation.BeginMatch();
                var other=powerSimulation.Tanks.Find(t=>t.Player&&t.Slot==1);
                var local=powerSimulation.Tanks.Find(t=>t.Player&&t.Slot==0);
                float localShield=local.Shield;
                var shield=new ReplayEvent{kind="powerup",value="shield",slot=1};powerRecorder.RecordEvent(shield);ReplayPlayer.ApplyEvent(powerSimulation,shield);
                Check(other.Shield==10&&local.Shield==localShield,"Multiplayer shield affects its owner only");
                int lives0=powerSimulation.Participants[0].Lives,lives1=powerSimulation.Participants[1].Lives;
                var life=new ReplayEvent{kind="powerup",value="life",slot=1};powerRecorder.RecordEvent(life);ReplayPlayer.ApplyEvent(powerSimulation,life);
                Check(powerSimulation.Participants[0].Lives==lives0&&powerSimulation.Participants[1].Lives==lives1+1,"Multiplayer Extra Life affects its owner only");
                var commands=new Dictionary<int,Command>();powerRecorder.BeforeStep(default,commands);powerSimulation.StepMultiplayer(commands);powerRecorder.AfterStep();powerRecorder.Finish("aborted");
                var playback=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(powerRecorder.Data)));playback.Step();
                Check(playback.Complete,"Multiplayer player-scoped powerups replay correctly ("+mode+"): "+playback.Error);
            }
            var teamMap=Map();teamMap.rivalBase=new BaseData{x=352,y=0};
            var team=new BattleSimulation(teamMap);team.ConfigureMultiplayer(BattleMode.TeamBattle);
            var teamRecorder=new ReplayRecorder(team,teamMap,"team-powerup-check");
            for(int i=0;i<4;i++)team.SetParticipant(i,true);team.BeginMatch();
            foreach(string type in new[]{"freeze","zoomout","defence"})
            {var ev=new ReplayEvent{kind="powerup",slot=1,value=type};teamRecorder.RecordEvent(ev);ReplayPlayer.ApplyEvent(team,ev);}
            Check(team.Participants[1].InventoryFreeze>0&&team.Participants[0].InventoryFreeze==0&&team.Freeze==0,"Team freeze targets opposing NPCs without freezing allies");
            Check(team.Participants[1].InventoryZoom>0&&team.Participants[0].InventoryZoom==0&&team.ZoomOut==0,"Inventory zoom affects only its owner");
            Check(team.Participants[1].InventoryDefenceWalls?.Count==8&&team.Participants[1].InventoryDefence>0,"Rival team defence protects its own base");
            var teamCommands=new Dictionary<int,Command>();teamRecorder.BeforeStep(default,teamCommands);team.StepMultiplayer(teamCommands);teamRecorder.AfterStep();teamRecorder.Finish("aborted");
            var teamReplay=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(teamRecorder.Data)));teamReplay.Step();
            Check(teamReplay.Complete,"Team powerups must replay deterministically: "+teamReplay.Error);
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
            foreach(var mode in new[]{BattleMode.Offline,BattleMode.Coop})
            {
                var legacy=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(Fixture(mode,0))));
                while(!legacy.Complete&&legacy.Error==null)legacy.Step();
                Check(legacy.Complete,"Legacy start timing replay mismatch ("+mode+"): "+legacy.Error);
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
