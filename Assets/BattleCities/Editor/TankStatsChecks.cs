using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace BattleCities.Tests
{
    public static class TankStatsChecks
    {
#if UNITY_EDITOR
        public static void RunEditor()
        {
            Run();
            BattleCities.ShootingHealthChecks.Run();
            BattleCities.MultiplayerChecks.Run();
        }
#endif
        static int checks;
        static void Check(bool ok,string name){checks++;if(!ok)throw new Exception("TANK STATS: "+name);}
        static void Near(float a,float b,string name)=>Check(Math.Abs(a-b)<.0001f,name);
        static MapData Map()=>new MapData {field=new FieldData{widthTiles=20,heightTiles=20},
            spawn=new SpawnData {enemy=new SpawnGroup {locations=new[]{new Point{x=64,y=0},new Point{x=320,y=0},new Point{x=640,y=0},new Point{x=960,y=0}},
                list=new[]{new EnemySpec{tier="a"},new EnemySpec{tier="b"},new EnemySpec{tier="c"},new EnemySpec{tier="d"}}}}};
        static BattleSimulation Ready(int tier)
        {
            var s=new BattleSimulation(Map(),playerTier:tier){DisableEnemyFire=true,Freeze=999};
            s.Terrain.Clear();for(int i=0;i<122;i++)s.Step(default);
            s.Player.X=400;s.Player.Y=800;s.Player.Shield=0;s.Player.Aim=Facing.Up;return s;
        }
        static void Replay(BattleMode mode,int tier)
        {
            var map=Map();var s=new BattleSimulation(map,playerTier:tier);
            if(mode!=BattleMode.Offline)s.ConfigureMultiplayer(mode);
            var recorder=new ReplayRecorder(s,map,"tank-stats-check");
            if(s.IsMultiplayer){s.SetParticipantTankTier(0,tier);s.SetParticipantTankTier(1,3);s.SetParticipant(0,true);s.SetParticipant(1,true);s.BeginMatch();}
            for(int i=0;i<240;i++)
            {
                if(i==140||i==160||i==180){var e=new ReplayEvent{kind="powerup",value="upgrade"};recorder.RecordEvent(e);ReplayPlayer.ApplyEvent(s,e);}
                var cmd=new Command{Fire=i%8==0};
                var commands=new Dictionary<int,Command>{{0,cmd},{1,default}};
                recorder.BeforeStep(cmd,s.IsMultiplayer?commands:null);
                if(s.IsMultiplayer)s.StepMultiplayer(commands);else s.Step(cmd);
                recorder.AfterStep();
            }
            recorder.Finish("aborted");
            var player=new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(recorder.Data)));
            while(!player.Complete&&player.Error==null)player.Step();
            Check(player.Complete,"record/replay "+mode+" tier "+tier+": "+player.Error);
            Check(ReplayJson.Hash(s.CaptureReplayStateData())==s.ReplayStateHash(),"detached snapshot includes stats and upgrades");
            var restored=new BattleSimulation(map);restored.RestoreNetworkCheckpoint(s.SaveNetworkCheckpoint());
            Check(restored.ReplayStateHash()==s.ReplayStateHash(),"checkpoint restores chassis and stats "+mode);
            for(int i=0;i<30;i++)
            {if(s.IsMultiplayer){s.StepMultiplayer(new Dictionary<int,Command>());restored.StepMultiplayer(new Dictionary<int,Command>());}else{s.Step(default);restored.Step(default);}}
            Check(restored.ReplayStateHash()==s.ReplayStateHash(),"checkpoint remains deterministic after resume");
            if(!s.IsMultiplayer)
            {
                s.Kill(s.Player);restored.Kill(restored.Player);
                for(int i=0;i<100;i++){s.Step(default);restored.Step(default);}
                Check(s.Player.Tier==tier&&restored.Player.Tier==tier&&restored.Player.Health==s.Player.Health,"checkpoint preserves selected chassis on later respawn");
                Check(restored.ReplayStateHash()==s.ReplayStateHash(),"post-checkpoint respawn remains deterministic");
            }
        }
        public static void Run()
        {
            checks=0;
            int[] health={1,2,1,5};
            for(int tier=0;tier<4;tier++)
            {
                var s=Ready(tier);var tank=s.Player;
                Check(tank.Tier==tier&&tank.Health==health[tier]&&tank.MaxHealth==health[tier],"selected health tier "+tier);
                Check(tank.FirePower==(tier==3?2:1),"power matches selected chassis");
                float y=tank.Y;s.Step(new Command{Move=Facing.Up});Near(y-tank.Y,tank.Speed*BattleSimulation.StepSeconds,"movement uses profile");
                Check(s.Fire(tank),"selected tank fires");Check(s.Shots.Last().Damage==(tier==3?2:1),"normal shell uses firepower");
                var old=tank;s.Kill(tank);for(int i=0;i<100;i++)s.Step(default);
                Check(s.Player!=old&&s.Player.Tier==tier&&s.Player.Health==health[tier],"respawn preserves chassis/health");
                Replay(BattleMode.Offline,tier);
            }
            var normal=Ready(0);var twin=Ready(2);
            Near(twin.NormalReloadSeconds(twin.Player),normal.NormalReloadSeconds(normal.Player)/2,"two rounds halves normal reload");
            normal.Fire(normal.Player);twin.Fire(twin.Player);
            for(int i=0;i<4;i++){normal.Step(default);twin.Step(default);}
            Check(!normal.CanFire(normal.Player)&&twin.CanFire(twin.Player),"Twin Fang becomes ready before Vanguard");
            normal.Shots.Clear();twin.Shots.Clear();normal.Player.Cooldown=twin.Player.Cooldown=0;
            normal.Fire(normal.Player,true);twin.Fire(twin.Player,true);
            Near(twin.Player.Cooldown,normal.Player.Cooldown/2,"two rounds halves charged recovery");
            twin.Shots.Clear();twin.ApplyPowerup("upgrade");twin.ApplyPowerup("upgrade");
            Near(twin.NormalReloadSeconds(twin.Player),twin.PlayerUpgradedNormalReloadSeconds/2,"upgrade keeps round multiplier");
            Check(twin.Player.Tier==2&&twin.Player.MaxHealth==1,"STAR preserves chosen chassis");
            var battle=Ready(3);var target=new TankState{Id=9999,Tier=3,StatsVersion=1,Health=5,X=400,Y=680};battle.Tanks.Add(target);
            battle.Fire(battle.Player);for(int i=0;i<15;i++)battle.Step(default);
            Check(target.Alive&&target.Health==3,"Siegebreaker shell deals two damage on impact");
            battle.Player.Cooldown=0;battle.Fire(battle.Player,true);for(int i=0;i<15;i++)battle.Step(default);
            Check(!target.Alive,"charged shot scales with firepower");
            var enemies=new BattleSimulation(Map()){DisableEnemyFire=true,Freeze=999};enemies.Player.Shield=999;
            for(int i=0;i<700;i++)enemies.Step(default);
            var spawned=enemies.Tanks.Where(t=>!t.Player).OrderBy(t=>t.Tier).ToArray();
            Check(spawned.Length==4,"all four enemy classes spawn");
            Check(spawned[0].Health==1&&spawned[1].Health==1&&spawned[2].Health==1&&spawned[3].Health==5,"enemy roles use profile health");
            Check(spawned[1].Speed>spawned[0].Speed&&spawned[1].Speed>spawned[3].Speed,"scout is faster than normal and armored enemies");
            Check(spawned[2].FirePower==2&&spawned[2].ShellSpeed>spawned[0].ShellSpeed,"firepower enemy has stronger faster shells");
            Near(spawned[3].Speed,battle.Player.Speed,"matching speed ratings share movement speed");
            var online=new BattleSimulation(Map());online.ConfigureMultiplayer(BattleMode.Coop);
            online.SetParticipant(0,true);online.SetParticipant(1,true);
            Check(online.SetParticipantTankTier(0,3)&&online.SetParticipantTankTier(1,2),"individual online selection");
            Check(online.Tanks.Single(t=>t.Player&&t.Slot==0).Health==5&&online.Tanks.Single(t=>t.Player&&t.Slot==1).Rounds==2,"online spawn applies individual profiles");
            online.BeginMatch();Check(!online.SetParticipantTankTier(1,3),"combat cannot change paid chassis");
            Replay(BattleMode.Coop,2);Replay(BattleMode.Versus,1);
#if UNITY_EDITOR
            var net=BattleCities.Multiplayer.NetTankState.From(battle.Player).ToState();
            Check(net.Tier==battle.Player.Tier&&net.MaxHealth==battle.Player.MaxHealth&&net.FirePower==battle.Player.FirePower,"network tank roundtrip preserves profile");
            var participant=BattleCities.Multiplayer.NetBattleParticipant.From(online.Participants[1]).ToState();
            Check(participant.TankTier==2,"network participant roundtrip preserves respawn chassis");
            Debug.Log("Tank stats checks passed: "+checks);
#else
            Console.WriteLine("Tank stats checks passed: "+checks);
#endif
        }
    }
}
