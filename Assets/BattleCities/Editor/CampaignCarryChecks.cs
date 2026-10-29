using System;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class CampaignCarryChecks
    {
        static MapData Map()=>new MapData{field=new FieldData{widthTiles=20,heightTiles=20},spawn=new SpawnData{enemy=new SpawnGroup{list=new[]{new EnemySpec()}}}};
        static void Won(BattleSimulation sim)=>typeof(BattleSimulation).GetProperty("Won").SetValue(sim,true);
        public static void Run()
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("CAMPAIGN CARRY: "+message);}
            var previous=new BattleSimulation(Map(),1,playerTier:2){DisableEnemyFire=true};
            previous.Kill(previous.Player);for(int i=0;i<360&&previous.Player==null;i++)previous.Step(default);
            previous.ApplyPowerup("upgrade");previous.Player.Health=3;previous.Player.RapidFire=7;previous.Player.SpeedBoost=4;Won(previous);
            var next=new BattleSimulation(Map(),2,playerTier:2);next.ContinueCampaignFrom(previous);
            Check(next.Lives==2&&next.Player.Tier==3&&next.Player.UpgradeLevel==1,"lost life and promoted chassis retained");
            Check(next.Player.Health==3&&next.Player.MaxHealth==5,"damage retained without stage healing");
            Check(next.Player.RapidFire==7&&next.Player.SpeedBoost==4,"remaining tank boosts retained");
            var replay=new ReplayRecorder(next,Map(),"campaign-check");
            var restored=ReplayPlayer.CreateSimulation(replay.Data);
            Check(restored.ReplayStateHash()==next.ReplayStateHash(),"recording reproduces carried stage starting state");
            next.Kill(next.Player);for(int i=0;i<360&&next.Player==null;i++)next.Step(default);
            Check(next.Lives==1&&next.Player.Tier==2&&next.Player.Health==1&&next.Player.UpgradeLevel==0,"death still resets to original selected tank and consumes life");
            var fresh=new BattleSimulation(Map(),2,playerTier:2);Check(fresh.Lives==3&&fresh.Player.Tier==2,"new matches do not inherit run state");
            previous=new BattleSimulation(Map(),1);previous.ConfigureMultiplayer(BattleMode.Coop);previous.SetParticipant(0,true);previous.SetParticipantTankTier(0,2);
            previous.SetParticipant(1,true);previous.ApplyPowerup("upgrade");previous.Player.Health=3;previous.Participants[0].Lives=2;Won(previous);
            next=new BattleSimulation(Map(),2);next.ConfigureMultiplayer(BattleMode.Coop);next.ContinueCampaignFrom(previous);next.SetParticipant(0,true);next.SetParticipant(1,true);
            Check(next.Player.Tier==3&&next.Player.Health==3&&next.Participants[0].Lives==2&&next.Participants[0].TankTier==2,"co-op join preserves promoted tank and original respawn selection");
            Debug.Log("CAMPAIGN CARRY PASS: upgrades, damaged health, remaining lives, timed boosts, deterministic replay, respawn, fresh match and co-op continuation.");
            BattleCities.Tests.TankStatsChecks.RunEditor();
        }
    }
}
