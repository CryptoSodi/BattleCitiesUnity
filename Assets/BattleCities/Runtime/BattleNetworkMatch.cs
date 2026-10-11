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
        public int Sequence, Move, Aim, ShotSequence, SecondarySequence, Secondary;
        public NetworkBool ChargeHeld, PowerRequested;
    }

    public sealed partial class BattleNetworkMatch : NetworkBehaviour
    {
        [Networked,Capacity(2)] public NetworkArray<NetBattleFlag> Flags => default;
        [Networked,Capacity(2)] public NetworkArray<int> FlagScores => default;
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
        [Networked] public NetworkBool RivalBaseAlive {get;set;}
        public int RequiredPlayers=>BattleModeRules.PlayerLimit(Mode);
        [Networked] public float Freeze {get;set;}
        [Networked] public float ZoomOut {get;set;}
        [Networked] public NetworkString<_16> Pickup {get;set;}
        [Networked] public Vector3 PickupPositionTime {get;set;}
        [Networked,Capacity(4)] public NetworkArray<PlayerRef> Players => default;
        [Networked,Capacity(4)] public NetworkArray<int> AcknowledgedInput => default;
        [Networked,Capacity(4)] public NetworkArray<float> ChargeProgress => default;
        [Networked,Capacity(4)] public NetworkArray<NetBattleParticipant> Participants => default;
        [Networked,Capacity(4)] public NetworkArray<NetBattleResultStats> ResultStats => default;
        [Networked,Capacity(16)] public NetworkArray<NetTankState> Tanks => default;
        [Networked,Capacity(96)] public NetworkArray<NetShotState> Shots => default;
        [Networked,Capacity(20)] public NetworkArray<NetMineState> Mines => default;
        [Networked,Capacity(12)] public NetworkArray<NetDroneState> Drones => default;
        [Networked,Capacity(8)] public NetworkArray<NetTurretState> Turrets => default;
        [Networked,Capacity(4)] public NetworkArray<NetLandDroneState> LandDrones => default;
        [Networked,Capacity(TerrainWords)] public NetworkArray<uint> TerrainBits => default;
        // Snapshot partial damage as well as destroyed cells, including for late joiners.
        
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
        private bool advanceStageRequested;
        private readonly Dictionary<int,Command> commands=new Dictionary<int,Command>();
        private readonly int[] shotSequences=new int[4],secondarySequences=new int[4];
        private readonly float[] chargeSeconds=new float[4];

        public override void Spawned()
        {
            BattleSession.Instance.Attach(this);
            if(Object.HasStateAuthority)
            {
                if(!Runner.IsResume){Map=BattleSession.Instance.SelectedMap;Mode=BattleSession.Instance.SelectedMode;Round=1;}
            }
            BindGame();
            if(Object.HasStateAuthority){if(Runner.IsResume)restorePending=true;else SpawnChunks();}
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
        public void RequestNextStage(){if(Object.HasStateAuthority)advanceStageRequested=true;}

        public override void FixedUpdateNetwork()
        {
            if(!Object.HasStateAuthority||!BindGame()||!Game.ReplayReady||!HasChunks)return;
            if(restorePending){restorePending=false;RestoreMigration();}
            if(advanceStageRequested&&Mode==BattleMode.Coop&&Simulation.Won&&Map<35)
            {
                Map++;BattleSession.Instance.SelectedMap=Map;
                Round++;EventSequence=0;quickStartAt=-1;
                rematchRequested=false;BindGame();beginRequested=true;
            }
            advanceStageRequested=false;
            if(rematchRequested)
            {
                rematchRequested=false;quickStartAt=-1;Round++;EventSequence=0;BindGame();Runner.SessionInfo.IsOpen=true;
            }
            UpdateMembership();
            if(Time.realtimeSinceStartup<recoveryReadyAt)return;
            commands.Clear();
            for(int slot=0;slot<4;slot++)
            {
                if(Players[slot]==PlayerRef.None){chargeSeconds[slot]=0;ChargeProgress.Set(slot,0);continue;}
                Simulation.SetParticipant(slot,true);
                if(!Runner.TryGetInputForPlayer<BattleNetworkInput>(Players[slot],out var input)){chargeSeconds[slot]=0;ChargeProgress.Set(slot,0);continue;}
                AcknowledgedInput.Set(slot,input.Sequence);
                if(input.ChargeHeld)chargeSeconds[slot]=Mathf.Min(ChargedFireInput.ChargeDuration,chargeSeconds[slot]+Runner.DeltaTime);
                if(!inputInitialized[slot]){shotSequences[slot]=input.ShotSequence;secondarySequences[slot]=input.SecondarySequence;inputInitialized[slot]=true;}
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
                if(PlayerCount<RequiredPlayers)quickStartAt=-1;
                else
                {
                    if(quickStartAt<0)quickStartAt=Time.realtimeSinceStartup+5;
                    if(PlayerCount>=4||Time.realtimeSinceStartup>=quickStartAt)beginRequested=true;
                }
            }
            if(beginRequested)
            {
                beginRequested=false;
                if(PlayerCount>=RequiredPlayers){Simulation.BeginMatch();Runner.SessionInfo.IsOpen=true;Runner.SessionInfo.IsVisible=false;}
            }
            Game.StepOnlineRecorded(commands);
            Publish();
            UpdateRecoverySnapshot();
        }
        private static Facing? FacingValue(int value)=>value>=0&&value<4?(Facing?)value:null;

        private void Publish()
        {
            var s=Simulation;
            for(int i=0;i<2;i++){Flags.Set(i,NetBattleFlag.From(s.Flags[i]));FlagScores.Set(i,s.FlagScores[i]);}
            StateTick=s.Tick;Score=s.Score;Winner=s.WinnerSlot;Started=s.MatchStarted;Won=s.Won;Lost=s.Lost;BaseAlive=s.BaseAlive;RivalBaseAlive=s.RivalBaseAlive;
            SpawnedEnemies=s.TotalEnemies-s.Remaining+s.Tanks.Count(t=>!t.Player&&t.Alive);
            Freeze=s.Freeze;ZoomOut=s.ZoomOut;Pickup=s.PickupType??"";PickupPositionTime=new Vector3(s.PickupX,s.PickupY,s.PickupTime);
            for(int i=0;i<4;i++){Participants.Set(i,NetBattleParticipant.From(s.Participants[i]));ResultStats.Set(i,NetBattleResultStats.From(s.ResultStats[i]));}
            Fill(Tanks,s.Tanks,NetTankState.From);Fill(Shots,s.Shots,NetShotState.From);Fill(Mines,s.Mines,NetMineState.From);
            Fill(Drones,s.Drones,NetDroneState.From);Fill(Turrets,s.Turrets,NetTurretState.From);Fill(LandDrones,s.LandDrones,NetLandDroneState.From);
            for(int i=0;i<s.InitialTerrainCount;i+=2)
            {
                uint first=(uint)Math.Max(0,Math.Min(65535,s.Terrain[i].Health));
                uint second=i+1<s.InitialTerrainCount?(uint)Math.Max(0,Math.Min(65535,s.Terrain[i+1].Health)):0;
                SetHealthPair(i/2,first|(second<<16));
            }
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
            if(!BindGame()||Object.HasStateAuthority||!HasChunks)return;
            // Also apply lobby membership while the simulation clock is stopped.
            if(appliedTick==StateTick&&Started)return;
            var frame=new BattleFrame{Flags=Enumerable.Range(0,2).Select(i=>Flags[i].ToState()).ToArray(),FlagScores=Enumerable.Range(0,2).Select(i=>FlagScores[i]).ToArray(),Tick=StateTick,Score=Score,SpawnedEnemies=SpawnedEnemies,WinnerSlot=Winner,Started=Started,Won=Won,Lost=Lost,BaseAlive=BaseAlive,RivalBaseAlive=RivalBaseAlive,
                Freeze=Freeze,ZoomOut=ZoomOut,PickupType=Pickup.ToString().Length==0?null:Pickup.ToString(),PickupX=PickupPositionTime.x,PickupY=PickupPositionTime.y,PickupTime=PickupPositionTime.z,
                Participants=Enumerable.Range(0,4).Select(i=>Participants[i].ToState()).ToArray(),
                ResultStats=Enumerable.Range(0,4).Select(i=>ResultStats[i].ToState()).ToArray(),
                Tanks=Tanks.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),Shots=Shots.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),
                Mines=Mines.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),Drones=Drones.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),
                Turrets=Turrets.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),LandDrones=LandDrones.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),
                ExtraWalls=ExtraWalls.Where(t=>t.Id!=0).Select(t=>t.ToState()).ToArray(),TerrainAlive=new bool[Simulation.InitialTerrainCount],TerrainHealth=new int[Simulation.InitialTerrainCount]};
            for(int i=0;i<frame.TerrainAlive.Length;i++){frame.TerrainAlive[i]=(TerrainBits[i/32]&(1u<<(i%32)))!=0;frame.TerrainHealth[i]=(int)((HealthPair(i/2)>>((i%2)*16))&65535u);}
            Simulation.ApplyFrame(frame);appliedTick=StateTick;
            if(LocalSlot>=0)Game.ReconcileMovement(AcknowledgedInput[LocalSlot]);
            for(int sequence=Math.Max(appliedEvents+1,EventSequence-127);sequence<=EventSequence;sequence++)
            {
                var e=Events[sequence%128];if(e.Sequence==sequence)Simulation.PlayVisualEvent(e.ToState());
            }
            appliedEvents=EventSequence;
        }
    }
}
