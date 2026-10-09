using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCities.Core
{
    public sealed partial class BattleSimulation
    {
        public event Action<ReplayEvent> ReplayLifecycle;
        public uint ReplaySeed => random;
        public void SetReplaySeed(uint seed)
        { if(Tick!=0||seed==0)throw new InvalidOperationException("Set a nonzero seed before gameplay.");random=seed; }
        public ReplayConfig ReplayConfiguration()=>new ReplayConfig {
            normalReload=PlayerNormalReloadSeconds,upgradedReload=PlayerUpgradedNormalReloadSeconds,playerTier=initialPlayerTier,
            turretRange=turretAttackRange,turretDamage=turretDamage,turretCooldown=turretFireCooldown,turretReload=turretReloadDuration,
            turretTurn=turretTurnDuration,turretMuzzle=turretMuzzleDistance,turretHealth=turretHealth,turretDeploy=turretDeployDuration,
            landDrone=ReplayJson.Copy(LandDroneSettings)
        };
        public void ApplyReplayConfiguration(ReplayConfig c)
        { ConfigureTurrets(c.turretRange,c.turretDamage,c.turretCooldown,c.turretReload,c.turretTurn,c.turretMuzzle,c.turretHealth,c.turretDeploy);LandDroneSettings=ReplayJson.Copy(c.landDrone); }
        // Include hidden timers, RNG, entity order, pathfinding history, terrain damage and
        // participant state. Local camera slot, UI text and visual event buffers are excluded.
        public string ReplayStateHash()=>ReplayJson.Hash(ReplayStateData());
        public object ReplayStateData()=>IsCaptureFlag?(object)new {state=ReplayStateData(false),RivalBaseAlive,RivalBaseBounds,teamSpawnCounts,teamSpawnTimers,Flags,FlagScores}:IsTeamBattle?(object)new {state=ReplayStateData(false),RivalBaseAlive,RivalBaseBounds,teamSpawnCounts,teamSpawnTimers}:ReplayStateData(false);
        // Capture on the simulation thread. The returned graph is exclusively owned
        // by the checkpoint worker; it contains no mutable simulation references.
        public object CaptureReplayStateData()=>IsCaptureFlag?(object)new {state=ReplayStateData(true),RivalBaseAlive,RivalBaseBounds,teamSpawnCounts=(int[])teamSpawnCounts.Clone(),teamSpawnTimers=(float[])teamSpawnTimers.Clone(),Flags=Flags.Select(f=>f.Copy()).ToArray(),FlagScores=(int[])FlagScores.Clone()}:IsTeamBattle?(object)new {state=ReplayStateData(true),RivalBaseAlive,RivalBaseBounds,teamSpawnCounts=(int[])teamSpawnCounts.Clone(),teamSpawnTimers=(float[])teamSpawnTimers.Clone()}:ReplayStateData(true);
        private object ReplayStateData(bool detached)=>new {
            Tick,Stage,Width,Height,Score,offlineLives,BaseAlive,Lost,Won,Mode,MatchStarted,WinnerSlot,DisableEnemyFire,
            Freeze,ZoomOut,defenceTimer,offlineSecondary,offlineSecondaryCooldown,playerMoveIntent,
            PickupType,PickupX,PickupY,PickupTime,BaseBounds,spawned,nextId,spawnIndex,spawnTimer,respawnTimer,intro,random,
            wave=detached?SnapshotArray(wave,Snapshot):wave,
            enemySpawns=detached?SnapshotArray(enemySpawns,Snapshot):enemySpawns,
            playerSpawn=detached?Snapshot(playerSpawn):playerSpawn,
            onlineSpawns=detached?SnapshotArray(onlineSpawns,Snapshot):onlineSpawns,
            Participants=detached?SnapshotArray(Participants,Snapshot):Participants,
            defenceWalls=defenceWalls.Select(w=>w.Id).ToArray(),defenceBricks=defenceBricks.Select(w=>w.Id).ToArray(),
            Terrain=detached?SnapshotList(Terrain,Snapshot):Terrain,
            Tanks=detached?SnapshotList(Tanks,Snapshot):Tanks,
            Shots=detached?SnapshotList(Shots,Snapshot):Shots,
            Mines=detached?SnapshotList(Mines,Snapshot):Mines,
            Drones=detached?SnapshotList(Drones,Snapshot):Drones,
            Turrets=detached?SnapshotList(Turrets,Snapshot):Turrets,
            LandDrones=LandDrones.Select(d=>new {d.Id,d.TargetId,d.Health,d.OwnerSlot,d.X,d.Y,d.Age,d.Speed,d.Distance,d.Heading,d.NoRouteTime,d.Repath,d.Alive,
                d.YieldRemaining,d.YieldX,d.YieldY,d.HasYieldPoint,Route=detached?SnapshotList(d.Route,Snapshot):d.Route,
                PatrolledTiles=d.PatrolledTiles.OrderBy(v=>v).ToArray()}).ToArray()
        };

        private static T[] SnapshotArray<T>(T[] source,Func<T,T> copy) where T:class
        {
            if(source==null)return null;
            var result=new T[source.Length];
            for(int i=0;i<source.Length;i++)result[i]=copy(source[i]);
            return result;
        }
        private static List<T> SnapshotList<T>(List<T> source,Func<T,T> copy) where T:class
        {
            if(source==null)return null;
            var result=new List<T>(source.Count);
            for(int i=0;i<source.Count;i++)result.Add(copy(source[i]));
            return result;
        }
        private static Point Snapshot(Point p)=>p==null?null:new Point{x=p.x,y=p.y};
        private static EnemySpec Snapshot(EnemySpec s)=>s==null?null:new EnemySpec{tier=s.tier,drop=s.drop};
        private static BattleParticipant Snapshot(BattleParticipant p)=>p==null?null:new BattleParticipant{
            Connected=p.Connected,Lives=p.Lives,Respawn=p.Respawn,SecondaryCooldown=p.SecondaryCooldown,Secondary=p.Secondary
        };
        private static Wall Snapshot(Wall w)=>w==null?null:new Wall{
            Id=w.Id,Type=w.Type,Bounds=w.Bounds,Alive=w.Alive,Damage=w.Damage?.Copy(),Health=w.Health,
            PropKey=w.PropKey,PropRole=w.PropRole,PropRotation=w.PropRotation,BuildingId=w.BuildingId,
            BuildingBounds=w.BuildingBounds,BuildingSettings=w.BuildingSettings?.Copy()
        };
        private static TankState Snapshot(TankState t)=>t==null?null:new TankState{
            Slot=t.Slot,Id=t.Id,Tier=t.Tier,Health=t.Health,Player=t.Player,Drop=t.Drop,Alive=t.Alive,Moving=t.Moving,
            X=t.X,Y=t.Y,Shield=t.Shield,Cooldown=t.Cooldown,Think=t.Think,FireDelay=t.FireDelay,Slide=t.Slide,
            SpeedBoost=t.SpeedBoost,SinkDepth=t.SinkDepth,InQuicksand=t.InQuicksand,ReloadDuration=t.ReloadDuration,
            AiState=t.AiState,Direction=t.Direction,Aim=t.Aim
        };
        private static ShotState Snapshot(ShotState s)=>s==null?null:new ShotState{
            Id=s.Id,Owner=s.Owner,OwnerSlot=s.OwnerSlot,Player=s.Player,Alive=s.Alive,PowerShot=s.PowerShot,
            WallDamage=s.WallDamage,Damage=s.Damage,X=s.X,Y=s.Y,Speed=s.Speed,Direction=s.Direction
        };
        private static MineState Snapshot(MineState m)=>m==null?null:new MineState{
            Id=m.Id,OwnerSlot=m.OwnerSlot,X=m.X,Y=m.Y,Age=m.Age,Alive=m.Alive
        };
        private static DroneState Snapshot(DroneState d)=>d==null?null:new DroneState{
            Id=d.Id,TargetId=d.TargetId,OwnerSlot=d.OwnerSlot,X=d.X,Y=d.Y,AnchorX=d.AnchorX,AnchorY=d.AnchorY,Age=d.Age,Alive=d.Alive
        };
        private static TurretState Snapshot(TurretState t)=>t==null?null:new TurretState{
            OwnerSlot=t.OwnerSlot,Id=t.Id,TargetId=t.TargetId,Health=t.Health,Damage=t.Damage,FireSequence=t.FireSequence,
            X=t.X,Y=t.Y,Deployment=t.Deployment,FireCooldownRemaining=t.FireCooldownRemaining,ReloadRemaining=t.ReloadRemaining,
            TurnRemaining=t.TurnRemaining,Heading=t.Heading,Alive=t.Alive,Phase=t.Phase,TransitionTime=t.TransitionTime,ShotAge=t.ShotAge
        };
    }
}
