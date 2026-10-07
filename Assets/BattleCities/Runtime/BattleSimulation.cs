using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCities.Core
{
    [Serializable] public sealed class MapData { public FieldData field; public TerrainData terrain; public GroundData ground; public MapObjectData[] objects; public MapLightData[] lights; public SpawnData spawn; public BaseData @base; }
    [Serializable] public sealed class GroundData { public Region[] regions; }
    [Serializable] public sealed class MapObjectData { public bool bridge; public string type, role; public float x, y, width, height, rotation; public MapDamage damage; public MapBuildingSettings building; }
    [Serializable] public sealed class MapLightData { public float x, y, height, r, g, b, range, intensity; }
    [Serializable] public sealed class FieldData { public int widthTiles = 13, heightTiles = 13; }
    [Serializable] public sealed class TerrainData { public Region[] regions; }
    [Serializable] public sealed class Region { public string type; public float x, y, width, height; public MapDamage damage; }
    [Serializable] public sealed class SpawnData { public SpawnGroup enemy, player; }
    [Serializable] public sealed class SpawnGroup { public Point[] locations; public EnemySpec[] list; }
    [Serializable] public sealed class EnemySpec { public string tier = "a"; public bool drop; }
    [Serializable] public sealed class Point { public float x, y; }
    [Serializable] public sealed class BaseData { public float x, y; }
    public enum Facing { Up, Right, Down, Left }
    public enum SecondaryAttack { None, Mine, PatrolDrone, GroundTurret, LandDrone }
    public sealed class DroneState
    {
        public int Id, TargetId, OwnerSlot = -1;
        public float X,Y,AnchorX,AnchorY,Age;
        public bool Alive=true;
        public bool Armed => Age>=1;
    }
    public enum TurretPhase { Stowed, Deploying, Deployed, Retracting }
    public sealed class TurretState
    {
        public int OwnerSlot = -1;
        public int Id, TargetId, Health = 4, Damage = 1, FireSequence;
        public float X, Y, Deployment, FireCooldownRemaining, ReloadRemaining, TurnRemaining;
        public Facing Heading = Facing.Up;
        public bool Alive = true;
        public TurretPhase Phase;
        public float TransitionTime, ShotAge=100;
        public bool Active => Alive && Phase==TurretPhase.Deployed;
        public bool BlocksPath => Alive && Phase!=TurretPhase.Stowed;
        public const float ModelScale=.5773064f;
        public Box Clearance => new Box(X-43.52f*ModelScale,Y-43.52f*ModelScale,87.04f*ModelScale,87.04f*ModelScale);
        public Box BlockingBounds => Phase==TurretPhase.Deployed?Bounds:Clearance;
        public Box Bounds => new Box(X-32*ModelScale,Y-32*ModelScale,64*ModelScale,64*ModelScale);
    }
    public struct Command { public Facing? Move, Aim; public bool Fire, PowerShot, SecondaryFire; }
    public sealed class MineState
    {
        public int Id, OwnerSlot = -1;
        public float X, Y, Age;
        public bool Alive = true;
        public bool Armed => Age >= BattleSimulation.MineArmSeconds;
        public Box Bounds => new Box(X-12,Y-12,24,24);
    }
    public struct Box
    {
        public float X, Y, W, H;
        public float Right => X + W;
        public float Bottom => Y + H;
        public Box(float x, float y, float w, float h) { X=x; Y=y; W=w; H=h; }
        public bool Overlaps(Box b) => X < b.Right && Right > b.X && Y < b.Bottom && Bottom > b.Y;
    }
    public sealed class Wall
    {
        public int Id;
        public string Type;
        public Box Bounds;
        public bool Alive = true;
        public MapDamage Damage;
        public int Health;
        public string PropKey, PropRole;
        public float PropRotation;
        public int BuildingId;
        public Box BuildingBounds;
        public MapBuildingSettings BuildingSettings;
        public bool IsBuildingSection => BuildingId != 0;
        public bool Brick => Type.Contains("brick");
        public bool DestructibleProp => PropKey != null && PropRole == "destructibleObstacle";
        public bool Solid => Brick || Type == "steel" || BattleTerrain.BlocksTank(Type) ||
            (PropKey != null && (PropRole == "solidCover" || PropRole == "destructibleObstacle"));
        public bool StopsBullet => Brick || Type == "steel" ||
            (PropKey != null && (PropRole == "solidCover" || PropRole == "destructibleObstacle" || PropRole == "bulletBlocker"));
    }
    public sealed class TankState
    {
        public const float MovementHalfSize = 32;
        public int Slot = -1;
        public int Id, Tier, Health = 3;
        public static int StartingHealth(int tier,bool player) => player?5:tier==3?6:tier==2?4:3;
        public int MaxHealth => StartingHealth(Tier,Player);
        public bool Player, Drop, Alive = true, Moving;
        public float X, Y, Shield, Cooldown, Think, FireDelay, Slide, SpeedBoost;
        public float SinkDepth;
        public bool InQuicksand;
        public float TerrainSpeedMultiplier => InQuicksand ? .62f-.50f*SinkDepth : 1f;
        public float ReloadDuration { get; internal set; }
        public float ReloadProgress => Cooldown<=0?1:ReloadDuration>0?Math.Max(0,Math.Min(1,1-Cooldown/ReloadDuration)):0;
        public int AiState;
        public Facing Direction, Aim;
        public Box Bounds => new Box(X-32, Y-32, 64, 64);
        public Box MovementBounds => MovementBox(X,Y);
        public static Box MovementBox(float x,float y) => new Box(x-MovementHalfSize,y-MovementHalfSize,MovementHalfSize*2,MovementHalfSize*2);
        public float Speed => Player ? 180*(SpeedBoost>0?1.5f:1) : Tier == 1 ? 240 : 120;
    }
    public sealed class ShotState
    {
        public int Id, Owner, OwnerSlot = -1;
        public bool Player, Alive = true, PowerShot;
        public int WallDamage, Damage = 1;
        public float X, Y, Speed;
        public Facing Direction;
        public Box Bounds => Direction == Facing.Up || Direction == Facing.Down ? new Box(X-6,Y-8,12,16) : new Box(X-8,Y-6,16,12);
    }
    public sealed partial class BattleSimulation
    {
        public const float StepSeconds = 1f / 60f;
        public readonly List<Wall> Terrain = new List<Wall>();
        public readonly List<TankState> Tanks = new List<TankState>();
        public readonly List<ShotState> Shots = new List<ShotState>();
        public readonly List<MineState> Mines = new List<MineState>();
        public readonly List<DroneState> Drones = new List<DroneState>();
        public readonly List<TurretState> Turrets = new List<TurretState>();
        public const float DronePatrolRadius=128,DroneLifetime=45;
        public const int MaximumTurrets=2;
        private float turretAttackRange=192, turretFireCooldown=1.2f, turretReloadDuration=2.4f, turretTurnDuration=.18f, turretMuzzleDistance=55, turretDeployDuration=3.033333f;
        private int turretDamage=1, turretHealth=4;
        public const int MaximumDrones=3;
        public event Action<DroneState> DroneDetonated;
        public event Action<TurretState> TurretFired;
        public event Action<TurretState> TurretHit;
        public event Action<TurretState> TurretDestroyed;
        public int SecondaryCount => EquippedSecondary==SecondaryAttack.LandDrone?LandDrones.Count(d=>d.Alive&&OwnedSecondary(d.OwnerSlot)):EquippedSecondary==SecondaryAttack.PatrolDrone?Drones.Count(d=>OwnedSecondary(d.OwnerSlot)):EquippedSecondary==SecondaryAttack.GroundTurret?Turrets.Count(t=>OwnedSecondary(t.OwnerSlot)):Mines.Count(m=>OwnedSecondary(m.OwnerSlot));
        public int SecondaryLimit => EquippedSecondary==SecondaryAttack.LandDrone?1:EquippedSecondary==SecondaryAttack.PatrolDrone?MaximumDrones:EquippedSecondary==SecondaryAttack.GroundTurret?MaximumTurrets:MaximumMines;
        public const float MineArmSeconds = .8f, SecondaryCooldownSeconds = 2f;
        public const int MaximumMines = 5;
        private SecondaryAttack offlineSecondary = SecondaryAttack.Mine;
        public SecondaryAttack EquippedSecondary { get => IsMultiplayer ? Participants[actingSlot >= 0 ? actingSlot : LocalPlayerSlot].Secondary : offlineSecondary; set { if(IsMultiplayer) Participants[actingSlot >= 0 ? actingSlot : LocalPlayerSlot].Secondary=value; else offlineSecondary=value; } }
        private float offlineSecondaryCooldown;
        public float SecondaryCooldown { get => IsMultiplayer ? Participants[actingSlot >= 0 ? actingSlot : LocalPlayerSlot].SecondaryCooldown : offlineSecondaryCooldown; private set { if(IsMultiplayer) Participants[actingSlot >= 0 ? actingSlot : LocalPlayerSlot].SecondaryCooldown=value; else offlineSecondaryCooldown=value; } }
        public string SecondaryStatus { get; private set; } = "";
        private Facing? playerMoveIntent;
        public bool CanUseSecondary => !Lost && !Won && intro<=0 && Player!=null && EquippedSecondary!=SecondaryAttack.None && SecondaryCooldown<=0 && SecondaryCount<SecondaryLimit;
        public event Action<MineState> MineDetonated;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stage { get; private set; }
        public int Tick { get; private set; }
        public int Score { get; private set; }
        private int offlineLives = 3;
        public int Lives { get => IsMultiplayer ? Participants[actingSlot >= 0 ? actingSlot : LocalPlayerSlot].Lives : offlineLives; private set { if(IsMultiplayer) Participants[actingSlot >= 0 ? actingSlot : LocalPlayerSlot].Lives=value; else offlineLives=value; } }
        public bool BaseAlive { get; private set; } = true;
        public bool Lost { get; private set; }
        public bool Won { get; private set; }
        public bool DisableEnemyFire;
        public float Freeze, ZoomOut;
        private float defenceTimer;
        private readonly List<Wall> defenceWalls=new List<Wall>();
        private readonly List<Wall> defenceBricks=new List<Wall>();
        public string PickupType, PickupClaim;
        public float PickupX, PickupY, PickupTime;
        public event Action<string> CurrencyClaimed;
        public event Action TerrainChanged;
        public Box BaseBounds { get; private set; }
        public TankState Player => Tanks.Find(t => t.Player && t.Alive && (!IsMultiplayer || t.Slot == (actingSlot >= 0 ? actingSlot : LocalPlayerSlot)));
        public bool CanAcceptPlayerFire => !Lost&&!Won&&intro<=0&&Player!=null;
        public int Remaining => wave.Length - spawned + Tanks.Count(t => !t.Player && t.Alive);
        public int TotalEnemies => wave.Length;
        public event Action<Wall> WallDestroyed;
        public event Action<Wall> WallDamaged;
        public event Action<TankState> TankDestroyed;
        public event Action<ShotState> ShotFired;
        public event Action<ShotState> ShotImpact;
        public event Action BaseDestroyed;
        public event Action DropRequested;
        private EnemySpec[] wave;
        private Point[] enemySpawns;
        private Point playerSpawn;
        private int spawned, nextId, spawnIndex;
        private float spawnTimer = .16f, respawnTimer, intro = 2;
        private uint random = 12345;
        public float PlayerNormalReloadSeconds { get; }
        public float PlayerUpgradedNormalReloadSeconds { get; }

        private readonly int initialPlayerTier;
        public BattleSimulation(MapData map, int stage = 1, float normalReloadSeconds = .12f, float upgradedNormalReloadSeconds = .08f, int playerTier = 0)
        {
            initialPlayerTier=Math.Max(0,Math.Min(3,playerTier));
            PlayerNormalReloadSeconds=ValidReloadSeconds(normalReloadSeconds,.12f);
            PlayerUpgradedNormalReloadSeconds=ValidReloadSeconds(upgradedNormalReloadSeconds,.08f);
            Stage=stage; Width=(map.field?.widthTiles ?? 13)*64; Height=(map.field?.heightTiles ?? 13)*64;
            foreach (var r in BattleBridgeLayout.TerrainRegions(map)) AddRegion(r.type, r.x,r.y,r.width,r.height,r.damage);
            foreach (var item in map.objects ?? Array.Empty<MapObjectData>()) AddEnvironmentObject(item);
            float bx=map.@base?.x ?? Width/2-64, by=map.@base?.y ?? Height-96;
            BaseBounds = new Box(bx+32,by+32,64,64);
            AddRegion("brick",bx,by,128,32); AddRegion("brick",bx,by+32,32,64); AddRegion("brick",bx+96,by+32,32,64);
            enemySpawns=map.spawn?.enemy?.locations;
            if (enemySpawns == null || enemySpawns.Length == 0) enemySpawns=new[]{new Point{x=Width/2-32,y=0},new Point{x=Width-64,y=0},new Point{x=0,y=0}};
            playerSpawn=map.spawn?.player?.locations?.FirstOrDefault() ?? new Point{x=Width/2-160,y=Height-64};
            wave=map.spawn?.enemy?.list ?? Enumerable.Range(0,20).Select(_=>new EnemySpec()).ToArray();
            SpawnPlayer();
        }
        public void ConfigureTurrets(float range, int damage, float cooldown, float reload, float turn, float muzzle, int health, float deploy)
        {
            turretAttackRange=Math.Max(64,range);turretDamage=Math.Max(1,damage);turretFireCooldown=Math.Max(.05f,cooldown);
            turretReloadDuration=Math.Max(.05f,reload);turretTurnDuration=Math.Max(0,turn);turretMuzzleDistance=Math.Max(16,muzzle);
            turretHealth=Math.Max(1,health);turretDeployDuration=Math.Max(.05f,deploy);
        }
        public void AddRegion(string type, float x,float y,float w,float h,MapDamage damage=null)
        {
            type=BattleTerrain.Normalize(type);
            int size=type.Contains("brick")?16:32;
            for(float py=y;py<y+h;py+=size) for(float px=x;px<x+w;px+=size)
            {
                var bounds=new Box(px,py,Math.Min(size,x+w-px),Math.Min(size,y+h-py));
                // Some legacy maps contain repeated regions. Stacking identical logical
                // cells makes one shot spend its damage budget on invisible duplicates.
                if(Terrain.Any(existing=>existing.Type==type&&existing.Bounds.X==bounds.X&&existing.Bounds.Y==bounds.Y&&existing.Bounds.W==bounds.W&&existing.Bounds.H==bounds.H))continue;
                Terrain.Add(new Wall{Id=++nextId,Type=type,Bounds=bounds,Damage=damage?.Copy(),Health=damage==null?1:Math.Max(1,damage.hitPoints)});
            }
        }
        private void AddEnvironmentObject(MapObjectData item)
        {
            if (item == null || string.IsNullOrEmpty(item.type) ||
                !(item.role == "solidCover" || item.role == "destructibleObstacle" ||
                  item.role == "bulletBlocker" || item.role == "passableCover" || item.role == "groundDetail"))
                throw new ArgumentException("Invalid map environment object or role");
            if (item.width <= 0 || item.height <= 0 || item.x < 0 || item.y < 0 ||
                item.x + item.width > Width || item.y + item.height > Height)
                throw new ArgumentException("Environment object outside the map: " + item.type);
            if (BattleBuildings.IsBuilding(item.type) && item.role == "solidCover")
            {
                int buildingId = nextId + 1;
                var bounds = new Box(item.x, item.y, item.width, item.height);
                var damage = item.damage ?? BattleBuildings.DefaultDamage();
                var settings = item.building?.Copy() ?? new MapBuildingSettings();
                for (float y = 0; y < item.height; y += BattleBuildings.SectionSize)
                for (float x = 0; x < item.width; x += BattleBuildings.SectionSize)
                    Terrain.Add(new Wall { Id = ++nextId, Type = "environment", PropKey = item.type,
                        PropRole = item.role, PropRotation = item.rotation, BuildingId = buildingId,
                        BuildingBounds = bounds, BuildingSettings = settings, Damage = damage.Copy(),
                        Health = Math.Max(1, damage.hitPoints),
                        Bounds = new Box(item.x+x,item.y+y,Math.Min(BattleBuildings.SectionSize,item.width-x),Math.Min(BattleBuildings.SectionSize,item.height-y)) });
                return;
            }
            Terrain.Add(new Wall { Id = ++nextId, Type = "environment", PropKey = item.type,
                PropRole = item.role, PropRotation = item.rotation,
                Damage=item.damage?.Copy(), Health=item.damage==null?1:Math.Max(1,item.damage.hitPoints),
                Bounds = new Box(item.x, item.y, item.width, item.height) });
        }
        private int Next(int min,int max)
        {
            // Same mulberry32 and exclusive upper bound as the original engine.
            unchecked { random+=0x6d2b79f5;uint t=random;t=(t^(t>>15))*(t|1);t^=t+(t^(t>>7))*(t|61);return min+(int)(((t^(t>>14))/4294967296.0)*(max-min)); }
        }
        public static void Vector(Facing d,out float x,out float y) { x=d==Facing.Right?1:d==Facing.Left?-1:0; y=d==Facing.Down?1:d==Facing.Up?-1:0; }
        private void SpawnPlayer() { Tanks.Add(new TankState{Id=++nextId,Player=true,Tier=initialPlayerTier,Health=TankState.StartingHealth(initialPlayerTier,true),X=playerSpawn.x+32,Y=playerSpawn.y+32,Shield=3.5f}); }
        public void Step(Command command)
        {
            if(Lost || Won) return;
            Tick++; const float dt=StepSeconds;
            playerMoveIntent=command.Move;
            if(intro>0) {intro-=dt;return;}
            if(!IsMultiplayer)SecondaryCooldown=Math.Max(0,SecondaryCooldown-dt);
            Freeze=Math.Max(0,Freeze-dt);ZoomOut=Math.Max(0,ZoomOut-dt);
            if(defenceTimer>0)
            {
                defenceTimer-=dt;
                if(defenceTimer<=0)
                {
                    foreach(var w in defenceBricks)w.Alive=true;
                    Terrain.RemoveAll(w=>defenceWalls.Contains(w));
                    defenceWalls.Clear();defenceBricks.Clear();TerrainChanged?.Invoke();
                }
            }
            if(PickupType!=null){PickupTime-=dt;if(PickupTime<=0)PickupType=null;}
            foreach(var t in Tanks) { t.Shield=Math.Max(0,t.Shield-dt);t.Cooldown=Math.Max(0,t.Cooldown-dt);t.SpeedBoost=Math.Max(0,t.SpeedBoost-dt);t.Moving=false;PrepareTerrain(t); }
            if(IsMultiplayer) UpdateParticipants(dt);
            else
            {
            var p=Player;
            if(p != null)
            {
                bool icy=TouchesSlipperyGround(p)&&!p.InQuicksand;
                if(command.Move.HasValue) { Rotate(p,command.Move.Value);Move(p);p.Slide=icy?.5f:0; }
                else if(p.Slide>0) {p.Slide-=dt;Move(p);}
                p.Aim=command.Aim ?? p.Direction;
                if(command.Fire) Fire(p,command.PowerShot);
                if(command.SecondaryFire) UseSecondary();
                if(PickupType!=null&&p.Bounds.Overlaps(new Box(PickupX-32,PickupY-32,64,64))){ApplyPowerup(PickupType);Score+=500;if(PickupClaim!=null)CurrencyClaimed?.Invoke(PickupClaim);PickupType=null;PickupClaim=null;}
            }
            else if(Lives>0) {respawnTimer-=dt;if(respawnTimer<=0) SpawnPlayer();}
            else Lost=true;
            }
            spawnTimer-=dt;
            if(!IsPvp && spawnTimer<=0 && spawned<wave.Length && Tanks.Count(t=>!t.Player&&t.Alive)<4)
            {
                var point=enemySpawns[spawnIndex%enemySpawns.Length];
                var box=new Box(point.x,point.y,64,64);
                if(!Tanks.Any(t=>t.Alive&&box.Overlaps(t.Bounds))&&!Turrets.Any(t=>t.BlocksPath&&box.Overlaps(t.BlockingBounds)))
                {
                    var spec=wave[spawned++];int tier=Math.Max(0,"abcd".IndexOf(spec.tier ?? "a"));
                    Tanks.Add(new TankState{Id=++nextId,Tier=tier,Health=TankState.StartingHealth(tier,false),Drop=spec.drop,X=point.x+32,Y=point.y+32,Direction=Facing.Down,Aim=Facing.Down});
                    spawnIndex++;spawnTimer=3;if(spec.drop)PickupType=null;
                }
            }
            foreach(var t in Tanks) if(t.Alive&&!t.Player&&Freeze<=0) UpdateEnemy(t);
            foreach(var t in Tanks)if(t.Alive)UpdateQuicksand(t,dt);
            UpdateMines(dt);
            UpdateTurrets(dt);
            UpdateDrones(dt);
            UpdateLandDrones(dt);
            foreach(var shot in Shots.ToArray()) if(shot.Alive) MoveShot(shot);
            Shots.RemoveAll(s=>!s.Alive); Tanks.RemoveAll(t=>!t.Alive); Turrets.RemoveAll(t=>!t.Alive);
            if(!IsPvp&&spawned==wave.Length&&!Tanks.Any(t=>!t.Player&&t.Alive)) Won=true;
            if(IsMultiplayer) UpdateMatchResult();
        }
        public bool UseSecondary()
        {
            if(!CanUseSecondary)return false;
            var player=Player;
            if(EquippedSecondary==SecondaryAttack.LandDrone)return PlaceLandDrone(player);
            if(EquippedSecondary==SecondaryAttack.GroundTurret)
            {
                var turret=new TurretState{OwnerSlot=player.Slot,X=player.X,Y=player.Y,Health=turretHealth,Damage=turretDamage};
                var area=turret.Clearance;
                if(area.X<0||area.Y<0||area.Right>Width||area.Bottom>Height||area.Overlaps(BaseBounds)
                    ||Terrain.Any(w=>w.Alive&&w.Solid&&w.Bounds.Overlaps(area))
                    ||Turrets.Any(t=>t.Alive&&t.Clearance.Overlaps(area))
                    ||Tanks.Any(t=>t!=player&&t.Alive&&t.MovementBounds.Overlaps(area)))
                {
                    SecondaryStatus="Not enough clear space under the tank for the turret.";
                    return false;
                }
                turret.Id=++nextId;Turrets.Add(turret);SecondaryCooldown=4;
                SecondaryStatus="Turret placed under tank. Move away to deploy.";
                return true;
            }
            var mine=new MineState{OwnerSlot=player.Slot,Id=++nextId,X=player.X,Y=player.Y};
            if(mine.Bounds.X<0||mine.Bounds.Y<0||mine.Bounds.Right>Width||mine.Bounds.Bottom>Height||mine.Bounds.Overlaps(BaseBounds))return false;
            if(Terrain.Any(w=>w.Alive&&w.Solid&&w.Bounds.Overlaps(mine.Bounds)))return false;
            if(EquippedSecondary==SecondaryAttack.PatrolDrone)
            {
                Drones.Add(new DroneState{OwnerSlot=player.Slot,Id=mine.Id,X=player.X,Y=player.Y,AnchorX=player.X,AnchorY=player.Y});
                SecondaryCooldown=4;return true;
            }
            if(Mines.Any(m=>m.Alive&&m.Bounds.Overlaps(mine.Bounds)))return false;
            Mines.Add(mine);SecondaryCooldown=SecondaryCooldownSeconds;return true;
        }        private void UpdateMines(float dt)
        {
            foreach(var mine in Mines)
            {
                mine.Age+=dt;
                if(!mine.Armed)continue;
                var victim=Tanks.Find(t=>t.Alive&&IsSecondaryTarget(t,mine.OwnerSlot)&&t.MovementBounds.Overlaps(mine.Bounds));
                if(victim==null)continue;
                mine.Alive=false;MineDetonated?.Invoke(mine);
                if(victim.Drop){victim.Drop=false;DropRequested?.Invoke();}
                Kill(victim);
            }
            Mines.RemoveAll(m=>!m.Alive);
        }
        private void UpdateTurrets(float dt)
        {
            foreach(var turret in Turrets)
            {
                turret.FireCooldownRemaining=Math.Max(0,turret.FireCooldownRemaining-dt);
                turret.ReloadRemaining=Math.Max(0,turret.ReloadRemaining-dt);
                turret.ShotAge+=dt;
                var player=IsMultiplayer ? Tanks.Where(t=>t.Player&&t.Alive&&(!IsPvp||t.Slot==turret.OwnerSlot)).OrderBy(t=>DistanceSquared(t.X,t.Y,turret.X,turret.Y)).FirstOrDefault() : Player;
                // Keep the entire player safety radius stowed, even when stationary.
                // A small exit margin prevents toggling at the radius boundary.
                float playerDistance=player!=null&&player.Alive
                    ?DistanceSquared(player.X,player.Y,turret.X,turret.Y):float.MaxValue;
                const float playerSafetyRadius=1.5f*64;
                bool playerNearby=playerDistance<=playerSafetyRadius*playerSafetyRadius;
                float releaseRadius=playerSafetyRadius+8;
                if((turret.Phase==TurretPhase.Deployed||turret.Phase==TurretPhase.Deploying)&&playerNearby)
                {
                    float retractTime=turret.Phase==TurretPhase.Deploying
                        ?(1-turret.Deployment)*turretDeployDuration:0;
                    turret.Phase=TurretPhase.Retracting;turret.TransitionTime=retractTime;
                    turret.ShotAge=100;turret.TargetId=0;
                }
                else if(turret.Phase==TurretPhase.Stowed&&playerDistance>releaseRadius*releaseRadius&&TurretClearanceEmpty(turret))
                {
                    turret.Phase=TurretPhase.Deploying;turret.TransitionTime=0;
                }
                if(turret.Phase==TurretPhase.Deploying||turret.Phase==TurretPhase.Retracting)
                {
                    turret.TransitionTime=Math.Min(turretDeployDuration,turret.TransitionTime+dt);
                    float fraction=turret.TransitionTime/turretDeployDuration;
                    turret.Deployment=turret.Phase==TurretPhase.Deploying?fraction:1-fraction;
                    if(turret.TransitionTime>=turretDeployDuration)
                        turret.Phase=turret.Phase==TurretPhase.Deploying?TurretPhase.Deployed:TurretPhase.Stowed;
                    turret.TargetId=0;
                    continue;
                }
                if(!turret.Active){turret.TargetId=0;continue;}
                turret.TurnRemaining=Math.Max(0,turret.TurnRemaining-dt);
                var target=turret.TargetId==0?null:Tanks.Find(t=>t.Id==turret.TargetId&&t.Alive&&IsSecondaryTarget(t,turret.OwnerSlot)&&HasTurretClearShot(turret,t));
                if(target==null)
                {
                    turret.TargetId=0;float closest=turretAttackRange*turretAttackRange;
                    foreach(var tank in Tanks)
                    {
                        if(!tank.Alive||!IsSecondaryTarget(tank,turret.OwnerSlot))continue;
                        float distance=DistanceSquared(tank.X,tank.Y,turret.X,turret.Y);
                        if(distance<=closest&&HasTurretClearShot(turret,tank)){target=tank;closest=distance;}
                    }
                    if(target!=null)turret.TargetId=target.Id;
                }
                if(target==null)continue;
                var desired=Cardinal(target.X-turret.X,target.Y-turret.Y);
                if(turret.Heading!=desired){turret.Heading=desired;turret.TurnRemaining=turretTurnDuration;continue;}
                if(turret.TurnRemaining<=0&&turret.FireCooldownRemaining<=0&&turret.ReloadRemaining<=0&&HasTurretClearShot(turret,target))FireTurret(turret);
            }
        }

        // A cardinal shell must actually intersect the enemy, with no cover in its lane.
        // Check from the pivot so a barrel protruding through cover cannot bypass it.
        private bool HasTurretClearShot(TurretState turret,TankState target)
        {
            float dx=target.X-turret.X,dy=target.Y-turret.Y;
            if(dx*dx+dy*dy>turretAttackRange*turretAttackRange)return false;
            var heading=Cardinal(dx,dy);
            bool horizontal=heading==Facing.Left||heading==Facing.Right;
            var enemy=target.Bounds;
            Box lane;
            if(horizontal)
            {
                if(enemy.Y>=turret.Y+6||enemy.Bottom<=turret.Y-6)return false;
                lane=new Box(Math.Min(turret.X,target.X),turret.Y-6,Math.Abs(dx),12);
            }
            else
            {
                if(enemy.X>=turret.X+6||enemy.Right<=turret.X-6)return false;
                lane=new Box(turret.X-6,Math.Min(turret.Y,target.Y),12,Math.Abs(dy));
            }
            if(BaseAlive&&lane.Overlaps(BaseBounds))return false;
            if(Terrain.Any(w=>w.Alive&&w.StopsBullet&&lane.Overlaps(w.Bounds)))return false;
            return !Turrets.Any(other=>other!=turret&&other.BlocksPath&&lane.Overlaps(other.BlockingBounds));
        }

        public bool TurretClearanceEmpty(TurretState turret)
        {
            var box=turret.Clearance;
            return box.X>=0&&box.Y>=0&&box.Right<=Width&&box.Bottom<=Height&&!box.Overlaps(BaseBounds)
                &&!Terrain.Any(w=>w.Alive&&w.Solid&&w.Bounds.Overlaps(box))
                &&!Tanks.Any(t=>t.Alive&&t.MovementBounds.Overlaps(box))
                &&!Turrets.Any(t=>t!=turret&&t.Alive&&t.Clearance.Overlaps(box));
        }
        private static Facing Cardinal(float dx,float dy)
        {
            return Math.Abs(dx)>Math.Abs(dy)?(dx>=0?Facing.Right:Facing.Left):(dy>=0?Facing.Down:Facing.Up);
        }
        private void FireTurret(TurretState turret)
        {
            Vector(turret.Heading,out var dx,out var dy);
            var shot=new ShotState{Id=++nextId,Owner=turret.Id,OwnerSlot=turret.OwnerSlot,Player=true,X=turret.X+dx*turretMuzzleDistance,Y=turret.Y+dy*turretMuzzleDistance,Direction=turret.Heading,Speed=900,WallDamage=1,Damage=turret.Damage};
            Shots.Add(shot);turret.ShotAge=0;turret.FireCooldownRemaining=turretFireCooldown;turret.ReloadRemaining=turretReloadDuration;turret.FireSequence++;TurretFired?.Invoke(turret);
        }        private static float DistanceSquared(float x,float y,float tx,float ty){float dx=x-tx,dy=y-ty;return dx*dx+dy*dy;}
        private void UpdateDrones(float dt)
        {
            foreach(var drone in Drones)
            {
                drone.Age+=dt;
                if(!drone.Armed)continue;
                var target=drone.TargetId==0?null:Tanks.Find(t=>t.Id==drone.TargetId&&t.Alive&&IsSecondaryTarget(t,drone.OwnerSlot));
                if(target==null)
                {
                    drone.TargetId=0;float closest=float.MaxValue;
                    foreach(var tank in Tanks)
                    {
                        if(!tank.Alive||!IsSecondaryTarget(tank,drone.OwnerSlot)||DistanceSquared(tank.X,tank.Y,drone.AnchorX,drone.AnchorY)>DronePatrolRadius*DronePatrolRadius)continue;
                        float distance=DistanceSquared(tank.X,tank.Y,drone.X,drone.Y);
                        if(distance<closest){target=tank;closest=distance;}
                    }
                    if(target!=null)drone.TargetId=target.Id;
                }
                if(target==null&&drone.Age>=DroneLifetime){drone.Alive=false;continue;}
                // Evaluate the patrol calculation in explicit double precision and round
                // only when storing positions. Mono otherwise retains float temporaries
                // differently from CoreCLR, causing replay drift when patrol resumes.
                double phase=((double)drone.Age-1)*.9f+(drone.Id%8)*(double).785398f;
                double tx=target!=null?target.X:Math.Max(12,Math.Min(Width-12,drone.AnchorX+Math.Cos(phase)*64));
                double ty=target!=null?target.Y:Math.Max(12,Math.Min(Height-12,drone.AnchorY+Math.Sin(phase)*64));
                double dx=tx-drone.X,dy=ty-drone.Y,length=Math.Sqrt(dx*dx+dy*dy);
                double travel=Math.Min(length,(target==null?75:240)*(double)dt);
                if(length>.001){drone.X=(float)(drone.X+dx/length*travel);drone.Y=(float)(drone.Y+dy/length*travel);}
                // The patrol radius limits acquisition only. Once locked, the drone commits
                // to the living target until impact, even outside its patrol circle or lifetime.
                if(target==null||DistanceSquared(drone.X,drone.Y,target.X,target.Y)>14*14)continue;
                drone.Alive=false;DroneDetonated?.Invoke(drone);
                if(target.Drop){target.Drop=false;DropRequested?.Invoke();}
                Kill(target);
            }
            Drones.RemoveAll(d=>!d.Alive);
        }
        private void UpdateEnemy(TankState t)
        {
            t.FireDelay-=StepSeconds;
            if(t.FireDelay<=0) {if(!DisableEnemyFire) Fire(t);t.FireDelay=Next(0,1500)/1000f;if(t.AiState==3)t.AiState=0;}
            if(t.AiState==3) return;
            if(t.AiState!=0)
            {
                t.Think-=StepSeconds;if(t.Think>0)return;
                if(t.AiState==1&&Next(1,100)<=30&&!DisableEnemyFire){t.AiState=3;return;}
                Facing d;
                if(Next(1,100)<=30) {float dx=BaseBounds.X-t.X,dy=BaseBounds.Y-t.Y;d=Math.Abs(dx)>=Math.Abs(dy)?(dx>0?Facing.Right:Facing.Left):Facing.Down;}
                else if(Next(1,100)<=10)d=Facing.Up;else d=new[]{Facing.Down,Facing.Left,Facing.Right}[Next(0,3)];
                Rotate(t,d);t.Aim=d;t.AiState=0;return;
            }
            if(!Move(t)){t.AiState=1;t.Think=.3f;}
            else if(Next(1,100)<=5 && ((t.Direction==Facing.Up||t.Direction==Facing.Down)?Math.Abs(t.Y%32)<.01:Math.Abs(t.X%32)<.01)){t.AiState=2;t.Think=.3f;}
        }
        private bool Free(Box b,TankState self)
        {
            if(b.X<0||b.Y<0||b.Right>Width||b.Bottom>Height||b.Overlaps(BaseBounds))return false;
            foreach(var wall in Terrain) if(wall.Alive&&wall.Solid&&b.Overlaps(wall.Bounds))return false;
            if(Turrets.Any(t=>t.BlocksPath&&b.Overlaps(t.BlockingBounds)))return false;
            if(self.Player&&LandDrones.Any(d=>d.Alive&&b.Overlaps(d.Bounds)))return false;
            return !Tanks.Any(t=>t!=self&&t.Alive&&b.Overlaps(t.MovementBounds));
        }
        private static float OverlapArea(Box a,Box b)
        {
            float w=Math.Min(a.Right,b.Right)-Math.Max(a.X,b.X),h=Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Y,b.Y);
            return w>0&&h>0?w*h:0;
        }
        private float BlockedArea(Box b,TankState self)
        {
            float area=0;
            if(b.X<0)area+=-b.X*b.H;if(b.Y<0)area+=-b.Y*b.W;
            if(b.Right>Width)area+=(b.Right-Width)*b.H;if(b.Bottom>Height)area+=(b.Bottom-Height)*b.W;
            area+=OverlapArea(b,BaseBounds);
            foreach(var wall in Terrain)if(wall.Alive&&wall.Solid)area+=OverlapArea(b,wall.Bounds);
            foreach(var turret in Turrets)if(turret.BlocksPath)area+=OverlapArea(b,turret.BlockingBounds);
            foreach(var tank in Tanks)if(tank!=self&&tank.Alive)area+=OverlapArea(b,tank.MovementBounds);
            if(self.Player)foreach(var drone in LandDrones)if(drone.Alive)area+=OverlapArea(b,drone.Bounds);
            return area;
        }
        public bool CanTankOccupy(TankState tank,float x,float y) => Free(TankState.MovementBox(x,y),tank);
        private bool CanAdvance(Box from,Box to,TankState self)
        {
            if(Free(to,self))return true;
            float current=BlockedArea(from,self);
            return current>.001f&&BlockedArea(to,self)<current-.001f;
        }
        private static Box Sweep(Box from,Box to)
        {
            float x=Math.Min(from.X,to.X),y=Math.Min(from.Y,to.Y);
            return new Box(x,y,Math.Max(from.Right,to.Right)-x,Math.Max(from.Bottom,to.Bottom)-y);
        }
        private void Rotate(TankState t,Facing d)
        {
            if(!t.Player&&t.Direction!=d)
            {
                float x=t.X,y=t.Y;
                if(d==Facing.Up||d==Facing.Down)x=(float)Math.Floor(x/32+.5)*32;else y=(float)Math.Floor(y/32+.5)*32;
                if(Free(TankState.MovementBox(x,y),t)){t.X=x;t.Y=y;}
            }
            t.Direction=d;
        }
        private bool Move(TankState t)
        {
            Vector(t.Direction,out var dx,out var dy);float distance=t.Speed*t.TerrainSpeedMultiplier*StepSeconds;
            // Small steps preserve exact wall contact even during boosted movement.
            float moved=0;
            while(distance>0)
            {
                float s=Math.Min(1,distance);var from=t.MovementBounds;var to=TankState.MovementBox(t.X+dx*s,t.Y+dy*s);
                // A tank can occasionally begin inside another blocker after a
                // respawn or terrain state change. Permit only steps that reduce
                // that overlap, preventing a permanent lock without tunnelling.
                if(!CanAdvance(from,to,t))break;
                t.X+=dx*s;t.Y+=dy*s;moved+=s;distance-=s;
            }
            // Player turns retain their exact position. Check only the three
            // neighbouring grid lanes. The old pixel-by-pixel search repeated
            // thousands of terrain queries every tick while held against a wall
            // or tank, which felt like input lag at contact.
            if(t.Player&&moved==0)
            {
                // Lane offsets are world-axis differences, independent of facing.
                // A rotated perpendicular reverses them for Left and Down.
                float sx=dx!=0?0:1,sy=dx!=0?1:0;
                if(!TryOpeningShift(t,dx,dy,sx,sy,ref moved))
                {
                    float cross=dx!=0?t.Y:t.X;
                    float snap=(float)Math.Floor(cross/32+.5)*32-cross;
                    if(!TryLaneShift(t,dx,dy,sx,sy,snap,ref moved))
                    {
                        float near=snap>0?snap-32:snap+32;
                        if(!TryLaneShift(t,dx,dy,sx,sy,near,ref moved))
                        {
                            float far=snap>0?snap+32:snap-32;
                            TryLaneShift(t,dx,dy,sx,sy,far,ref moved);
                        }
                    }
                }
            }
            t.Moving=moved>0;return t.Moving;
        }
        private bool TryOpeningShift(TankState t,float dx,float dy,float sx,float sy,ref float moved)
        {
            // An exact 64-unit gap may be centered between 32-unit grid lines.
            // Assist only from close to the opening so contact farther away
            // cannot drag the player sideways across a street.
            var from=t.MovementBounds;
            var ahead=TankState.MovementBox(t.X+dx*8,t.Y+dy*8);
            var sweep=Sweep(from,ahead);
            float cross=dx!=0?t.Y:t.X;
            var offsets=new List<float>();
            foreach(var wall in Terrain)
            {
                if(!wall.Alive||!wall.Solid||!wall.Bounds.Overlaps(sweep))continue;
                float near=(dx!=0?wall.Bounds.Bottom:wall.Bounds.Right)+32-cross;
                float far=(dx!=0?wall.Bounds.Y:wall.Bounds.X)-32-cross;
                if(Math.Abs(near)<=16)offsets.Add(near);
                if(Math.Abs(far)<=16)offsets.Add(far);
            }
            foreach(var offset in offsets.OrderBy(Math.Abs))
                if(TryLaneShift(t,dx,dy,sx,sy,offset,ref moved))return true;
            return false;
        }
        private bool TryLaneShift(TankState t,float dx,float dy,float sx,float sy,float shift,ref float moved)
        {
            float offset=Math.Abs(shift);
            if(offset<.01f||offset>32.01f)return false;
            var from=t.MovementBounds;
            var aligned=TankState.MovementBox(t.X+sx*shift,t.Y+sy*shift);
            if(!Free(Sweep(from,aligned),t))return false;
            var ahead=TankState.MovementBox(t.X+sx*shift+dx*8,t.Y+sy*shift+dy*8);
            if(!Free(Sweep(aligned,ahead),t))return false;
            float step=t.Speed*t.TerrainSpeedMultiplier*StepSeconds;
            float correction=t.InQuicksand?Math.Min(offset,step):offset<=step+1?offset:Math.Min(offset,step),sign=Math.Sign(shift);
            t.X+=sx*sign*correction;t.Y+=sy*sign*correction;moved=correction;
            return true;
        }
        public bool CanFire(TankState tank)
        {return !Lost&&!Won&&intro<=0&&tank!=null&&tank.Alive&&tank.Cooldown<=0&&Shots.Count(s=>s.Alive&&s.Owner==tank.Id)<(tank.Player?16:1);}
        private static float ValidReloadSeconds(float seconds,float fallback)
        {return float.IsNaN(seconds)||float.IsInfinity(seconds)?fallback:Math.Max(StepSeconds,seconds);}
        public float NormalReloadSeconds(TankState tank)
        {return tank.Player?(tank.Tier>=2?PlayerUpgradedNormalReloadSeconds:PlayerNormalReloadSeconds):.16f;}
        public bool Fire(TankState tank,bool powerShot=false)
        {
            if(!CanFire(tank))return false;
            Vector(tank.Aim,out var x,out var y);
            var s=new ShotState{Id=++nextId,Owner=tank.Id,OwnerSlot=tank.Slot,Player=tank.Player,PowerShot=powerShot,Damage=powerShot?3:1,X=tank.X+x*32,Y=tank.Y+y*32,Direction=tank.Aim,Speed=powerShot||(tank.Player?tank.Tier>=1:tank.Tier==2)?900:600,WallDamage=powerShot?2:1};
            Shots.Add(s);tank.Cooldown=tank.ReloadDuration=powerShot?.25f:NormalReloadSeconds(tank);ShotFired?.Invoke(s);return true;
        }
        private void MoveShot(ShotState s)
        {
            Vector(s.Direction,out var dx,out var dy);float left=s.Speed*StepSeconds;
            while(left>0&&s.Alive)
            {
                float step=Math.Min(1,left);s.X+=dx*step;s.Y+=dy*step;left-=step;var box=s.Bounds;
                if(box.X<0||box.Y<0||box.Right>Width||box.Bottom>Height){ImpactShot(s);break;}
                var wall=Terrain.FirstOrDefault(w=>w.Alive&&w.StopsBullet&&box.Overlaps(w.Bounds));
                if(wall!=null){if(!s.PowerShot&&(wall.Damage!=null||wall.Brick||wall.DestructibleProp||s.WallDamage==2))DestroyWall(wall,s);ImpactShot(s,null,wall);break;}
                if(!IsPvp&&BaseAlive&&box.Overlaps(BaseBounds)){BaseAlive=false;Lost=true;ImpactShot(s);BaseDestroyed?.Invoke();break;}
                var other=Shots.Find(b=>b!=s&&b.Alive&&ShotsOppose(s,b)&&box.Overlaps(b.Bounds));
                if(other!=null)
                {
                    if(s.PowerShot!=other.PowerShot)
                    {
                        // A charged shot absorbs an opposing normal bullet and
                        // keeps traveling. Resolve identically whichever shot
                        // happened to move first this simulation step.
                        var power=s.PowerShot?s:other;
                        ImpactShot(s.PowerShot?other:s);
                        power.Damage--;
                        if(power.Damage<=0)ImpactShot(power);
                        if(s.PowerShot&&s.Alive)continue;
                        break;
                    }
                    other.Alive=false;ImpactShot(s);
                    break;
                }
                var landDrone=LandDrones.Find(d=>d.Alive&&ShotTargetsSecondary(s,d.OwnerSlot)&&box.Overlaps(d.Bounds));
                if(landDrone!=null)
                {
                    ImpactShot(s);landDrone.Health-=Math.Max(1,s.Damage);
                    if(landDrone.Health<=0){landDrone.Alive=false;LandDroneExploded?.Invoke(landDrone);}
                    break;
                }
                var turret=Turrets.Find(t=>t.BlocksPath&&ShotTargetsSecondary(s,t.OwnerSlot)&&box.Overlaps(t.Bounds));
                if(turret!=null)
                {
                    ImpactShot(s);turret.Health-=Math.Max(1,s.Damage);TurretHit?.Invoke(turret);
                    if(turret.Health<=0){turret.Alive=false;TurretDestroyed?.Invoke(turret);}break;
                }
                var tank=Tanks.Find(t=>t.Alive&&ShotTargetsTank(s,t)&&box.Overlaps(t.Bounds));
                if(tank!=null){ImpactShot(s,tank);DamageTank(tank,Math.Max(1,s.Damage));}
            }
        }
        private void ImpactShot(ShotState shot,TankState directHit=null,Wall wall=null)
        {
            if(!shot.Alive)return;
            shot.Alive=false;
            ShotImpact?.Invoke(shot);
            if(!shot.PowerShot)return;
            float x=shot.X,y=shot.Y,radius=PowerShotBlastRadius;
            if(wall!=null)
            {
                WallImpactPoint(wall,shot,out x,out y);
                if(wall.Type=="steel")radius=PowerShotSteelBlastRadius;
            }
            // Every power-shot explosion damages nearby walls, even when the
            // projectile collided with a tank, another bullet or the map edge.
            DestroyWallsInBlast(shot,x,y,radius);
            if(!shot.Player)return;
            // Include a tank when any part of its footprint enters the circle.
            // Direct hits retain their existing damage; splash never hits twice.
            foreach(var tank in Tanks.Where(t=>t.Alive&&ShotTargetsTank(shot,t)&&t!=directHit).ToArray())
            {
                var bounds=tank.Bounds;
                float nearestX=Math.Max(bounds.X,Math.Min(bounds.Right,x));
                float nearestY=Math.Max(bounds.Y,Math.Min(bounds.Bottom,y));
                if(DistanceSquared(x,y,nearestX,nearestY)<=radius*radius)DamageTank(tank,1);
            }
        }
        private void DamageTank(TankState tank,int damage)
        {
            if(!tank.Alive||tank.Shield>0)return;
            tank.Health-=damage;
            if(tank.Drop){tank.Drop=false;DropRequested?.Invoke();}
            if(tank.Health<=0)Kill(tank);
        }
        public void Kill(TankState t)
        {
            if(!t.Alive)return;t.Alive=false;TankDestroyed?.Invoke(t);
            if(t.Player){if(IsMultiplayer){var participant=Participants[t.Slot];participant.Lives=Math.Max(0,participant.Lives-1);participant.Respawn=1.5f;}else {Lives--;respawnTimer=1.5f;}}else Score+=(t.Tier+1)*100;
        }
        public const float PowerShotBlastRadius=56; // Slightly smaller than one map tile (64).
        public const float PowerShotSteelBlastRadius=32; // Steel absorbs half the original blast reach.
        // Normal rounds remove a connected front strip. Power shots use a circle
        // centered on the wall contact, independent of the direction of travel.
        public void DestroyWall(Wall hit,ShotState shot)
        {
            if(shot.PowerShot){DestroyPowerShotRadius(hit,shot);return;}
            if(hit.Damage!=null){ApplyAuthoredDamage(hit,shot);return;}
            if(hit.DestructibleProp){hit.Alive=false;WallDestroyed?.Invoke(hit);return;}
            bool vertical=shot.Direction==Facing.Up||shot.Direction==Facing.Down;
            float axis=vertical?shot.X:shot.Y;
            float bandWidth=64;
            float min=(float)Math.Floor((axis-bandWidth/2)/16+.5)*16;
            float face=Face(hit.Bounds,shot.Direction);
            var candidates=Terrain.Where(w=>w.Alive&&(w.Brick||w.Type=="steel")&&Math.Abs(Face(w.Bounds,shot.Direction)-face)<.01f&&(vertical?w.Bounds.X:w.Bounds.Y)<min+bandWidth&&(vertical?w.Bounds.Right:w.Bounds.Bottom)>min&&!Covered(w,shot.Direction)).ToList();
            if(candidates.Count==0)return;
            var seed=candidates.OrderBy(w=>Math.Abs((vertical?w.Bounds.X+w.Bounds.W/2:w.Bounds.Y+w.Bounds.H/2)-axis)).First();
            var group=new List<Wall>{seed};candidates.Remove(seed);
            bool added=true;
            while(added){added=false;for(int i=candidates.Count-1;i>=0;i--){var w=candidates[i];if(group.Any(g=>vertical?(g.Bounds.Right==w.Bounds.X||w.Bounds.Right==g.Bounds.X):(g.Bounds.Bottom==w.Bounds.Y||w.Bounds.Bottom==g.Bounds.Y))){group.Add(w);candidates.RemoveAt(i);added=true;}}}
            foreach(var w in group.OrderBy(w=>Math.Abs((vertical?w.Bounds.X+w.Bounds.W/2:w.Bounds.Y+w.Bounds.H/2)-axis)).Take(Math.Min(shot.WallDamage,2)*4))
                if(w.Damage!=null) ApplyAuthoredDamage(w,shot);
                else if(w.Brick||shot.WallDamage==2)
                {
                    w.Alive=false;
                    WallDestroyed?.Invoke(w);
                }
        }
        private void DestroyPowerShotRadius(Wall hit,ShotState shot)
        {
            // Project the projectile center onto the contacted face so the circle
            // starts at the wall surface, rather than short of it or on a grid snap.
            WallImpactPoint(hit,shot,out var x,out var y);
            float radius=hit.Type=="steel"?PowerShotSteelBlastRadius:PowerShotBlastRadius;
            DestroyWallsInBlast(shot,x,y,radius);
        }
        private void DestroyWallsInBlast(ShotState shot,float x,float y,float radius)
        {
            float radiusSquared=radius*radius;
            float steelRadiusSquared=PowerShotSteelBlastRadius*PowerShotSteelBlastRadius;
            // Cell-center inclusion approximates a circle on the destructible grid
            // without removing a whole block for a tiny overlap at the outer edge.
            var affected=Terrain.Where(w=>w.Alive&&w.StopsBullet
                &&(w.Damage!=null||w.Brick||w.DestructibleProp||(w.Type=="steel"&&shot.WallDamage==2))
                // A steel impact shrinks the whole blast; steel caught in a brick
                // impact also resists destruction outside its smaller inner radius.
                &&DistanceSquared(x,y,w.Bounds.X+w.Bounds.W/2,w.Bounds.Y+w.Bounds.H/2)<=(w.Type=="steel"?steelRadiusSquared:radiusSquared)).ToArray();
            foreach(var wall in affected){if(wall.Damage!=null)ApplyAuthoredDamage(wall,shot);else{wall.Alive=false;WallDestroyed?.Invoke(wall);}}
        }
        private static void WallImpactPoint(Wall hit,ShotState shot,out float x,out float y)
        {
            bool vertical=shot.Direction==Facing.Up||shot.Direction==Facing.Down;
            x=vertical?Math.Max(hit.Bounds.X,Math.Min(hit.Bounds.Right,shot.X)):Face(hit.Bounds,shot.Direction);
            y=vertical?Face(hit.Bounds,shot.Direction):Math.Max(hit.Bounds.Y,Math.Min(hit.Bounds.Bottom,shot.Y));
        }
        private static float Face(Box b,Facing d) => d==Facing.Up?b.Bottom:d==Facing.Down?b.Y:d==Facing.Left?b.Right:b.X;
        private bool Covered(Wall w,Facing d)
        {
            var b=w.Bounds;
            foreach(var wall in Terrain){if(wall==w||!wall.Alive||!wall.StopsBullet)continue;var o=wall.Bounds;
                if(d==Facing.Up&&o.X<b.Right&&o.Right>b.X&&o.Y>=b.Bottom&&o.Y<b.Bottom+64)return true;
                if(d==Facing.Down&&o.X<b.Right&&o.Right>b.X&&o.Bottom<=b.Y&&o.Bottom>b.Y-64)return true;
                if(d==Facing.Left&&o.Y<b.Bottom&&o.Bottom>b.Y&&o.X>=b.Right&&o.X<b.Right+64)return true;
                if(d==Facing.Right&&o.Y<b.Bottom&&o.Bottom>b.Y&&o.Right<=b.X&&o.Right>b.X-64)return true;
            }return false;
        }
        public void SpawnPickup(string type=null,string claim=null)
        {
            string[] types={"defence","freeze","life","shield","speed","upgrade","zoomout","wipeout"};
            PickupType=type??types[Next(0,types.Length)];PickupClaim=claim;PickupTime=30;
            var positions=new List<Point>();
            for(int y=32;y<Height-32;y+=32)for(int x=32;x<Width-32;x+=32)
            {
                var box=new Box(x-32,y-32,64,64);
                if(box.Overlaps(new Box(BaseBounds.X-32,BaseBounds.Y-32,128,96)))continue;
                if(Terrain.Any(w=>w.Alive&&(w.Type=="steel"||BattleTerrain.BlocksTank(w.Type)||(w.PropKey!=null&&w.Solid))&&box.Overlaps(w.Bounds)))continue;
                if(Tanks.Any(t=>t.Player&&box.Overlaps(new Box(t.X-96,t.Y-96,192,192))))continue;
                if(enemySpawns.Any(s=>box.Overlaps(new Box(s.x,s.y,64,64)))||box.Overlaps(new Box(playerSpawn.x,playerSpawn.y,64,64)))continue;
                positions.Add(new Point{x=x,y=y});
            }
            if(positions.Count==0){ApplyPowerup(PickupType);if(claim!=null)CurrencyClaimed?.Invoke(claim);PickupType=null;return;}
            var point=positions[Next(0,positions.Count)];PickupX=point.x;PickupY=point.y;
        }
        public void ApplyPowerup(string type)
        {
            var p=Player;
            switch(type)
            {
                case "shield":if(p!=null)p.Shield=10;break;
                case "upgrade":if(p!=null)p.Tier=Math.Min(3,p.Tier+1);break;
                case "speed":if(p!=null)p.SpeedBoost=10;break;
                case "life":Lives++;break;
                case "freeze":Freeze=10;break;
                case "wipeout":foreach(var t in Tanks.Where(t=>!t.Player&&t.Alive).ToArray())Kill(t);break;
                case "zoomout":ZoomOut=10;break;
                case "defence":
                    if(defenceTimer>0)
                    {
                        foreach(var w in defenceWalls)w.Alive=true;
                        defenceTimer=17;TerrainChanged?.Invoke();break;
                    }
                    var region=new Box(BaseBounds.X-32,BaseBounds.Y-32,128,96);
                    foreach(var w in Terrain.Where(w=>w.Brick&&w.Bounds.Overlaps(region)&&!w.Bounds.Overlaps(BaseBounds)).ToArray())
                    {w.Alive=false;defenceBricks.Add(w);}
                    float left=BaseBounds.X-32,top=BaseBounds.Y-32;
                    for(int x=0;x<4;x++)defenceWalls.Add(new Wall{Id=++nextId,Type="steel",Bounds=new Box(left+x*32,top,32,32)});
                    for(int y=1;y<3;y++)
                    {
                        defenceWalls.Add(new Wall{Id=++nextId,Type="steel",Bounds=new Box(left,top+y*32,32,32)});
                        defenceWalls.Add(new Wall{Id=++nextId,Type="steel",Bounds=new Box(left+96,top+y*32,32,32)});
                    }
                    Terrain.AddRange(defenceWalls);
                    defenceTimer=17;TerrainChanged?.Invoke();break;
            }
        }
    }
}
