using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace BattleCities
{
    public static class MultiplayerChecks
    {
        private static void Check(bool condition,string name){if(!condition)throw new Exception("MULTIPLAYER: "+name);}
        private static BattleSimulation Ready(BattleMode mode)
        {
            var s=new BattleSimulation(new MapData()){DisableEnemyFire=true,Freeze=999};
            s.Terrain.Clear();s.ConfigureMultiplayer(mode);s.RecordVisualEvents();s.SetParticipant(0,true);s.SetParticipant(1,true);s.BeginMatch();
            for(int i=0;i<122;i++)s.StepMultiplayer(new Dictionary<int,Command>());
            return s;
        }
        [MenuItem("Battle Cities/Multiplayer/Validate Rules")]
        public static void Run()
        {
            var s=Ready(BattleMode.Coop);var p=s.Tanks.Single(t=>t.Slot==0);var q=s.Tanks.Single(t=>t.Slot==1);
            p.X=320;p.Y=400;q.X=500;q.Y=400;
            s.StepMultiplayer(new Dictionary<int,Command>{{0,new Command{Move=Facing.Up}},{1,new Command{Move=Facing.Down}}});
            Check(p.Y<400&&q.Y>400,"independent player inputs");
            p.X=400;p.Y=400;p.Aim=Facing.Up;q.X=400;q.Y=280;q.Shield=0;
            s.Fire(p,true);for(int i=0;i<16;i++)s.StepMultiplayer(new Dictionary<int,Command>());
            Check(q.Health==5,"co-op friendly fire disabled");
            s.Kill(q);Check(s.Participants[1].Lives==2&&s.Participants[0].Lives==3,"independent lives");
            for(int i=0;i<100;i++)s.StepMultiplayer(new Dictionary<int,Command>());
            Check(s.Tanks.Any(t=>t.Alive&&t.Slot==1)&&s.Tanks.Count(t=>t.Slot==0)==1,"correct player respawns");
            s=Ready(BattleMode.Versus);p=s.Tanks.Single(t=>t.Slot==0);q=s.Tanks.Single(t=>t.Slot==1);
            Check(!s.Tanks.Any(t=>!t.Player),"PvP has no enemy waves");
            p.X=400;p.Y=400;p.Aim=Facing.Up;q.X=400;q.Y=280;q.Shield=0;
            s.Fire(p,true);for(int i=0;i<16;i++)s.StepMultiplayer(new Dictionary<int,Command>());
            Check(q.Health==2,"PvP charged shots damage opponent");
            Check(!s.Lost&&!s.Won,"PvP ignores empty enemy wave and base loss");
            s.Participants[1].Lives=1;s.Kill(q);s.StepMultiplayer(new Dictionary<int,Command>());
            Check(s.Won&&s.WinnerSlot==0,"last player with lives wins");
            s=Ready(BattleMode.Versus);s.SetParticipant(1,false);s.StepMultiplayer(new Dictionary<int,Command>());
            Check(s.Won&&s.WinnerSlot==0&&!s.Tanks.Any(t=>t.Slot==1),"disconnect removes player and completes round");
            // Validate all shipped maps have capacity and four usable spawn points.
            int count=0;
            foreach(var asset in Resources.LoadAll<TextAsset>("Maps"))
            {
                if(!int.TryParse(asset.name,out int stage)||stage<1||stage>35)continue;
                foreach(var mode in new[]{BattleMode.Coop,BattleMode.Versus})
                {
                    s=new BattleSimulation(JsonConvert.DeserializeObject<MapData>(asset.text),stage);
                    s.ConfigureMultiplayer(mode);s.RecordVisualEvents();
                    Check(s.InitialTerrainCount<=BattleCities.Multiplayer.BattleNetworkMatch.TerrainWords*32,"terrain capacity stage "+stage);
                    for(int i=0;i<4;i++)s.SetParticipant(i,true);
                    Check(s.Tanks.Count==4,"four spawns stage "+stage);
                    foreach(var tank in s.Tanks)Check(s.CanTankOccupy(tank,tank.X,tank.Y),"clear spawn stage "+stage);
                }
                count++;
            }
            Check(count==35,"exactly 35 playable maps");
            Debug.Log("MULTIPLAYER RULES PASS: ownership, movement, friendly fire, PvP damage, lives, respawn, results, departure, and "+count+" maps.");
        }
    }
}
