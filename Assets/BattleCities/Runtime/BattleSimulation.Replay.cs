using System;
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
        public object ReplayStateData()=>new {
            Tick,Stage,Width,Height,Score,offlineLives,BaseAlive,Lost,Won,Mode,MatchStarted,WinnerSlot,DisableEnemyFire,
            Freeze,ZoomOut,defenceTimer,offlineSecondary,offlineSecondaryCooldown,playerMoveIntent,
            PickupType,PickupX,PickupY,PickupTime,BaseBounds,spawned,nextId,spawnIndex,spawnTimer,respawnTimer,intro,random,
            wave,enemySpawns,playerSpawn,onlineSpawns,Participants,
            defenceWalls=defenceWalls.Select(w=>w.Id).ToArray(),defenceBricks=defenceBricks.Select(w=>w.Id).ToArray(),
            Terrain,Tanks,Shots,Mines,Drones,Turrets,
            LandDrones=LandDrones.Select(d=>new {d.Id,d.TargetId,d.Health,d.OwnerSlot,d.X,d.Y,d.Age,d.Speed,d.Distance,d.Heading,d.NoRouteTime,d.Repath,d.Alive,
                d.YieldRemaining,d.YieldX,d.YieldY,d.HasYieldPoint,d.Route,PatrolledTiles=d.PatrolledTiles.OrderBy(v=>v).ToArray()}).ToArray()
        };
    }
}
