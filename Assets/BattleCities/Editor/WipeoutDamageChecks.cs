using System;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class WipeoutDamageChecks
    {
        static int checks;
        static void Check(bool ok,string message){checks++;if(!ok)throw new Exception("WIPEOUT: "+message);}
        public static void Run()
        {
            checks=0;var map=new MapData{field=new FieldData{widthTiles=20,heightTiles=20},rivalBase=new BaseData{x=576,y=0}};
            var sim=new BattleSimulation(map);sim.Tanks.Clear();
            var light=new TankState{Id=1,Tier=0,Health=1};var heavy=new TankState{Id=2,Tier=3,Health=5};
            var player=new TankState{Id=3,Player=true,Health=5};var shielded=new TankState{Id=4,Health=3,Shield=1};
            var dropper=new TankState{Id=5,Tier=3,Health=5,Drop=true};sim.Tanks.AddRange(new[]{light,heavy,player,shielded,dropper});
            int drops=0,kills=0;sim.DropRequested+=()=>drops++;sim.TankDestroyed+=_=>kills++;
            sim.ApplyPowerup("wipeout");
            Check(!light.Alive&&light.Health==0,"one-health enemy destroyed");Check(heavy.Alive&&heavy.Health==4,"heavy tank loses exactly one health");
            Check(player.Health==5&&player.Alive,"player unaffected");Check(shielded.Health==3,"normal shield protection respected");
            Check(dropper.Health==4&&!dropper.Drop&&drops==1,"damaged carrier drops its power-up once");Check(kills==1&&sim.Score==100,"only actual destruction awards kills/score");
            sim.ApplyPowerup("wipeout");Check(heavy.Health==3&&heavy.Alive&&drops==1,"repeat Wipeout removes one additional health without duplicate carrier drop");
            var config=sim.ReplayConfiguration();config.wipeoutDamageVersion=0;sim.ApplyReplayConfiguration(config);sim.ApplyPowerup("wipeout");
            Check(!heavy.Alive&&!shielded.Alive,"legacy recordings retain original instant destruction");
            foreach(var mode in new[]{BattleMode.TeamBattle,BattleMode.CaptureFlag,BattleMode.CaptureFlagDuel})
            {
                var team=new BattleSimulation(map);team.ConfigureMultiplayer(mode);team.LocalPlayerSlot=1;team.Tanks.Clear();
                var ally=new TankState{Id=1,Slot=1,Health=4};var enemy=new TankState{Id=2,Slot=0,Health=4};team.Tanks.AddRange(new[]{ally,enemy});team.ApplyPowerup("wipeout");
                Check(ally.Health==4&&enemy.Health==3,"only opposing faction NPCs damaged: "+mode);
                var restored=new BattleSimulation(map);restored.RestoreNetworkCheckpoint(team.SaveNetworkCheckpoint());
                Check(restored.WipeoutDamageVersion==1&&restored.ReplayStateHash()==team.ReplayStateHash(),"checkpoint preserves new damage rule");
            }
            BattleCities.Tests.ReplayChecks.RunEditor();
            Debug.Log("WIPEOUT DAMAGE PASS: "+checks+" targeted checks and replay suite.");
        }
    }
}
