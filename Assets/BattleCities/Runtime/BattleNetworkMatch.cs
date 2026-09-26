using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using Fusion;
using UnityEngine;

namespace BattleCities.Multiplayer
{
    public struct BattleNetworkInput : INetworkInput
    {
        public int Move, Aim, ShotSequence, SecondarySequence, Secondary;
        public NetworkBool ChargeHeld, PowerRequested;
    }

    public sealed class BattleNetworkMatch : NetworkBehaviour
    {
        public const int TerrainWords=512;
        [Networked] public int Map {get;set;}
        [Networked] public BattleMode Mode {get;set;}
        [Networked] public int Round {get;set;}
        [Networked] public int StateTick {get;set;}
        [Networked] public int Score {get;set;}
        [Networked] public int SpawnedEnemies {get;set;}
        [Networked] public int Winner {get;set;}
        [Networked] public NetworkBool Started {get;set;}
        [Networked] public NetworkBool Won {get;set;}
        [Networked] public NetworkBool Lost {get;set;}
        [Networked] public NetworkBool BaseAlive {get;set;}
        [Networked] public float Freeze {get;set;}
        [Networked] public float ZoomOut {get;set;}
        [Networked] public NetworkString<_16> Pickup {get;set;}
        [Networked] public Vector3 PickupPositionTime {get;set;}
        [Networked,Capacity(4)] public NetworkArray<PlayerRef> Players => default;
        [Networked,Capacity(4)] public NetworkArray<float> ChargeProgress => default;
        [Networked,Capacity(4)] public NetworkArray<NetBattleParticipant> Participants => default;
        [Networked,Capacity(16)] public NetworkArray<NetTankState> Tanks => default;
        [Networked,Capacity(96)] public NetworkArray<NetShotState> Shots => default;
        [Networked,Capacity(20)] public NetworkArray<NetMineState> Mines => default;
        [Networked,Capacity(12)] public NetworkArray<NetDroneState> Drones => default;
        [Networked,Capacity(8)] public NetworkArray<NetTurretState> Turrets => default;
        [Networked,Capacity(4)] public NetworkArray<NetLandDroneState> LandDrones => default;
        [Networked,Capacity(TerrainWords)] public NetworkArray<uint> TerrainBits => default;
        [Networked,Capacity(8)] public NetworkArray<NetWall> ExtraWalls => default;
        [Networked,Capacity(128)] public NetworkArray<NetBattleVisualEvent> Events => default;
        [Networked] public int EventSequence {get;set;}

        public BattleGame Game {get;private set;}
        public BattleSimulation Simulation => Game?Game.Simulation:null;
        public int LocalSlot {get;private set;}=-1;
        public int PlayerCount=>Enumerable.Range(0,4).Count(i=>Players[i]!=PlayerRef.None);
        private int appliedRound=-1,appliedTick=-1,appliedEvents;
        private bool beginRequested,rematchRequested;
        private float quickStartAt=-1;
        private float advanceStageAt=-1;
        private readonly Dictionary<int,Command> commands=new Dictionary<int,Command>();
        private readonly int[] shotSequences=new int[4],secondarySequences=new int[4];
        private readonly float[] chargeSeconds=new float[4];

        public override void Spawned()
        {
            BattleSession.Instance.Attach(this);
            if(Object.HasStateAuthority)
            {
                Map=BattleSession.Instance.SelectedMap;Mode=BattleSession.Instance.SelectedMode;Round=1;
            }
            BindGame();
        }

        private bool BindGame()
        {
            if(!Game)Game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            if(!Game||Round<=0)return false;
            if(appliedRound!=Round)
            {
                Game.PrepareOnline(this,Mode,Map);appliedRound=Round;appliedTick=-1;appliedEvents=0;
                if(Simulation.InitialTerrainCount>TerrainWords*32)throw new InvalidOperationException("Map exceeds the terrain replication budget.");
                Array.Clear(shotSequences,0,4);Array.Clear(secondarySequences,0,4);Array.Clear(chargeSeconds,0,4);
            }
            LocalSlot=-1;
            for(int i=0;i<4;i++)if(Players[i]==Runner.LocalPlayer){LocalSlot=i;Simulation.LocalPlayerSlot=i;break;}
            return true;
        }

        public void RequestStart(){if(Object.HasStateAuthority)beginRequested=true;}
        public void RequestRematch(){if(Object.HasStateAuthority)rematchRequested=true;}

        public override void FixedUpdateNetwork()
        {
            if(!Object.HasStateAuthority||!BindGame())return;
            if(Mode==BattleMode.Coop&&Simulation.Won&&Map<35)
            {
                if(advanceStageAt<0)advanceStageAt=Time.realtimeSinceStartup+2;
                if(Time.realtimeSinceStartup>=advanceStageAt)
                {
                    Map++;BattleSession.Instance.SelectedMap=Map;
                    Round++;EventSequence=0;advanceStageAt=-1;quickStartAt=-1;
                    rematchRequested=false;BindGame();beginRequested=true;
                }
            }
            else advanceStageAt=-1;
            if(rematchRequested)
            {
                rematchRequested=false;quickStartAt=-1;Round++;EventSequence=0;BindGame();Runner.SessionInfo.IsOpen=true;
            }
            var active=Runner.ActivePlayers.ToArray();
            for(int i=0;i<4;i++)
            {
                if(Players[i]!=PlayerRef.None&&!active.Contains(Players[i])){Players.Set(i,PlayerRef.None);Simulation.SetParticipant(i,false);}
            }
            foreach(var player in active)
            {
                if(Enumerable.Range(0,4).Any(i=>Players[i]==player))continue;
                for(int i=0;i<4;i++)if(Players[i]==PlayerRef.None){Players.Set(i,player);shotSequences[i]=secondarySequences[i]=0;chargeSeconds[i]=0;break;}
            }
            commands.Clear();
            for(int slot=0;slot<4;slot++)
            {
                if(Players[slot]==PlayerRef.None){chargeSeconds[slot]=0;ChargeProgress.Set(slot,0);continue;}
                Simulation.SetParticipant(slot,true);
                if(!Runner.TryGetInputForPlayer<BattleNetworkInput>(Players[slot],out var input)){chargeSeconds[slot]=0;ChargeProgress.Set(slot,0);continue;}
                if(input.ChargeHeld)chargeSeconds[slot]=Mathf.Min(ChargedFireInput.ChargeDuration,chargeSeconds[slot]+Runner.DeltaTime);
                bool fire=input.ShotSequence!=shotSequences[slot];
                bool secondary=input.SecondarySequence!=secondarySequences[slot];
                shotSequences[slot]=input.ShotSequence;secondarySequences[slot]=input.SecondarySequence;
                if(input.Secondary>=(int)SecondaryAttack.Mine&&input.Secondary<=(int)SecondaryAttack.LandDrone)
                    Simulation.Participants[slot].Secondary=(SecondaryAttack)input.Secondary;
                commands[slot]=new Command{Move=FacingValue(input.Move),Aim=FacingValue(input.Aim),Fire=fire,
                    PowerShot=fire&&input.PowerRequested&&chargeSeconds[slot]>=ChargedFireInput.ChargeDuration-.05f,SecondaryFire=secondary};
                if(!input.ChargeHeld)chargeSeconds[slot]=0;
                ChargeProgress.Set(slot,chargeSeconds[slot]/ChargedFireInput.ChargeDuration);
            }
            if(BattleSession.Instance.QuickMatching&&!Simulation.MatchStarted)
            {
                if(PlayerCount<2)quickStartAt=-1;
                else
                {
                    if(quickStartAt<0)quickStartAt=Time.realtimeSinceStartup+5;
                    if(PlayerCount>=4||Time.realtimeSinceStartup>=quickStartAt)beginRequested=true;
                }
            }
            if(beginRequested)
            {
                beginRequested=false;
                if(PlayerCount>=2){Simulation.BeginMatch();Runner.SessionInfo.IsOpen=false;}
            }
            Simulation.StepMultiplayer(commands);
            Publish();
        }
        private static Facing? FacingValue(int value)=>value>=0&&value<4?(Facing?)value:null;

        private void Publish()
        {
            var s=Simulation;
            StateTick=s.Tick;Score=s.Score;Winner=s.WinnerSlot;Started=s.MatchStarted;Won=s.Won;Lost=s.Lost;BaseAlive=s.BaseAlive;
            SpawnedEnemies=s.TotalEnemies-s.Remaining+s.Tanks.Count(t=>!t.Player&&t.Alive);
            Freeze=s.Freeze;ZoomOut=s.ZoomOut;Pickup=s.PickupType??"";PickupPositionTime=new Vector3(s.PickupX,s.PickupY,s.PickupTime);
            for(int i=0;i<4;i++)Participants.Set(i,NetBattleParticipant.From(s.Participants[i]));
            Fill(Tanks,s.Tanks,NetTankState.From);Fill(Shots,s.Shots,NetShotState.From);Fill(Mines,s.Mines,NetMineState.From);
            Fill(Drones,s.Drones,NetDroneState.From);Fill(Turrets,s.Turrets,NetTurretState.From);Fill(LandDrones,s.LandDrones,NetLandDroneState.From);
            for(int word=0;word<(s.InitialTerrainCount+31)/32;word++)
            {
                uint bits=0;
                for(int bit=0;bit<32&&word*32+bit<s.InitialTerrainCount;bit++)if(s.Terrain[word*32+bit].Alive)bits|=1u<<bit;
                TerrainBits.Set(word,bits);
            }
            Fill(ExtraWalls,s.Terrain.Skip(s.InitialTerrainCount).ToList(),NetWall.From);
            foreach(var e in s.VisualEvents)if(e.Sequence>EventSequence){Events.Set(e.Sequence%128,NetBattleVisualEvent.From(e));EventSequence=e.Sequence;}
        }

        private static void Fill<TNet,TState>(NetworkArray<TNet> target,List<TState> source,Func<TState,TNet> convert) where TNet:unmanaged
        {
            if(source.Count>target.Length)throw new InvalidOperationException("Network capacity exceeded for "+typeof(TState).Name);
            for(int i=0;i<target.Length;i++)target.Set(i,i<source.Count?convert(source[i]):default);
        }

        public override void Render()
        {
            if(!BindGame()||Object.HasStateAuthority)return;
            // Also apply lobby membership while the simulation clock is stopped.
            if(appliedTick==StateTick&&Started)return;
            var frame=new BattleFrame{Tick=StateTick,Score=Score,SpawnedEnemies=SpawnedEnemies,WinnerSlot=Winner,Started=Started,Won=Won,Lost=Lost,BaseAlive=BaseAlive,
                Freeze=Freeze,ZoomOut=ZoomOut,PickupType=Pickup.ToString().Length==0?null:Pickup.ToString(),PickupX=PickupPositionTime.x,PickupY=PickupPositionTime.y,PickupTime=PickupPositionTime.z,
                Participants=Enumerable.Range(0,4).Select(i=>Participants[i].ToState()).ToArray(),
                Tanks=Tanks.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),Shots=Shots.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),
                Mines=Mines.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),Drones=Drones.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),
                Turrets=Turrets.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),LandDrones=LandDrones.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),
                ExtraWalls=ExtraWalls.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),TerrainAlive=new bool[Simulation.InitialTerrainCount]};
            for(int i=0;i<frame.TerrainAlive.Length;i++)frame.TerrainAlive[i]=(TerrainBits[i/32]&(1u<<(i%32)))!=0;
            Simulation.ApplyFrame(frame);appliedTick=StateTick;
            for(int sequence=Math.Max(appliedEvents+1,EventSequence-127);sequence<=EventSequence;sequence++)
            {
                var e=Events[sequence%128];if(e.Sequence==sequence)Simulation.PlayVisualEvent(e.ToState());
            }
            appliedEvents=EventSequence;
        }
    }
}
