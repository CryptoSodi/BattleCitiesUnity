using Fusion;
using BattleCities.Core;
namespace BattleCities.Multiplayer
{
    public struct NetTankState : INetworkStruct
    {
        public int Id;
        public int Slot;
        public int Tier;
        public int Health;
        public NetworkBool Player;
        public NetworkBool Drop;
        public NetworkBool Alive;
        public NetworkBool Moving;
        public float X;
        public float Y;
        public float Shield;
        public float Cooldown;
        public float Slide;
        public float SpeedBoost;
        public float ReloadDuration;
        public Facing Direction;
        public Facing Aim;
        public static NetTankState From(TankState s) => new NetTankState { Id=s.Id, Slot=s.Slot, Tier=s.Tier, Health=s.Health, Player=s.Player, Drop=s.Drop, Alive=s.Alive, Moving=s.Moving, X=s.X, Y=s.Y, Shield=s.Shield, Cooldown=s.Cooldown, Slide=s.Slide, SpeedBoost=s.SpeedBoost, ReloadDuration=s.ReloadDuration, Direction=s.Direction, Aim=s.Aim };
        public TankState ToState() => new TankState { Id=Id, Slot=Slot, Tier=Tier, Health=Health, Player=Player, Drop=Drop, Alive=Alive, Moving=Moving, X=X, Y=Y, Shield=Shield, Cooldown=Cooldown, Slide=Slide, SpeedBoost=SpeedBoost, ReloadDuration=ReloadDuration, Direction=Direction, Aim=Aim };
    }
    public struct NetShotState : INetworkStruct
    {
        public int Id;
        public int Owner;
        public int OwnerSlot;
        public int Damage;
        public int WallDamage;
        public NetworkBool Player;
        public NetworkBool Alive;
        public NetworkBool PowerShot;
        public float X;
        public float Y;
        public float Speed;
        public Facing Direction;
        public static NetShotState From(ShotState s) => new NetShotState { Id=s.Id, Owner=s.Owner, OwnerSlot=s.OwnerSlot, Damage=s.Damage, WallDamage=s.WallDamage, Player=s.Player, Alive=s.Alive, PowerShot=s.PowerShot, X=s.X, Y=s.Y, Speed=s.Speed, Direction=s.Direction };
        public ShotState ToState() => new ShotState { Id=Id, Owner=Owner, OwnerSlot=OwnerSlot, Damage=Damage, WallDamage=WallDamage, Player=Player, Alive=Alive, PowerShot=PowerShot, X=X, Y=Y, Speed=Speed, Direction=Direction };
    }
    public struct NetMineState : INetworkStruct
    {
        public int Id;
        public int OwnerSlot;
        public NetworkBool Alive;
        public float X;
        public float Y;
        public float Age;
        public static NetMineState From(MineState s) => new NetMineState { Id=s.Id, OwnerSlot=s.OwnerSlot, Alive=s.Alive, X=s.X, Y=s.Y, Age=s.Age };
        public MineState ToState() => new MineState { Id=Id, OwnerSlot=OwnerSlot, Alive=Alive, X=X, Y=Y, Age=Age };
    }
    public struct NetDroneState : INetworkStruct
    {
        public int Id;
        public int TargetId;
        public int OwnerSlot;
        public NetworkBool Alive;
        public float X;
        public float Y;
        public float AnchorX;
        public float AnchorY;
        public float Age;
        public static NetDroneState From(DroneState s) => new NetDroneState { Id=s.Id, TargetId=s.TargetId, OwnerSlot=s.OwnerSlot, Alive=s.Alive, X=s.X, Y=s.Y, AnchorX=s.AnchorX, AnchorY=s.AnchorY, Age=s.Age };
        public DroneState ToState() => new DroneState { Id=Id, TargetId=TargetId, OwnerSlot=OwnerSlot, Alive=Alive, X=X, Y=Y, AnchorX=AnchorX, AnchorY=AnchorY, Age=Age };
    }
    public struct NetTurretState : INetworkStruct
    {
        public int Id;
        public int TargetId;
        public int OwnerSlot;
        public int Health;
        public int Damage;
        public int FireSequence;
        public NetworkBool Alive;
        public float X;
        public float Y;
        public float Deployment;
        public float FireCooldownRemaining;
        public float ReloadRemaining;
        public float TurnRemaining;
        public float TransitionTime;
        public float ShotAge;
        public Facing Heading;
        public TurretPhase Phase;
        public static NetTurretState From(TurretState s) => new NetTurretState { Id=s.Id, TargetId=s.TargetId, OwnerSlot=s.OwnerSlot, Health=s.Health, Damage=s.Damage, FireSequence=s.FireSequence, Alive=s.Alive, X=s.X, Y=s.Y, Deployment=s.Deployment, FireCooldownRemaining=s.FireCooldownRemaining, ReloadRemaining=s.ReloadRemaining, TurnRemaining=s.TurnRemaining, TransitionTime=s.TransitionTime, ShotAge=s.ShotAge, Heading=s.Heading, Phase=s.Phase };
        public TurretState ToState() => new TurretState { Id=Id, TargetId=TargetId, OwnerSlot=OwnerSlot, Health=Health, Damage=Damage, FireSequence=FireSequence, Alive=Alive, X=X, Y=Y, Deployment=Deployment, FireCooldownRemaining=FireCooldownRemaining, ReloadRemaining=ReloadRemaining, TurnRemaining=TurnRemaining, TransitionTime=TransitionTime, ShotAge=ShotAge, Heading=Heading, Phase=Phase };
    }
    public struct NetLandDroneState : INetworkStruct
    {
        public int Id;
        public int TargetId;
        public int OwnerSlot;
        public int Health;
        public NetworkBool Alive;
        public float X;
        public float Y;
        public float Age;
        public float Speed;
        public float Distance;
        public float Heading;
        public static NetLandDroneState From(LandDroneState s) => new NetLandDroneState { Id=s.Id, TargetId=s.TargetId, OwnerSlot=s.OwnerSlot, Health=s.Health, Alive=s.Alive, X=s.X, Y=s.Y, Age=s.Age, Speed=s.Speed, Distance=s.Distance, Heading=s.Heading };
        public LandDroneState ToState() => new LandDroneState { Id=Id, TargetId=TargetId, OwnerSlot=OwnerSlot, Health=Health, Alive=Alive, X=X, Y=Y, Age=Age, Speed=Speed, Distance=Distance, Heading=Heading };
    }
    public struct NetBattleParticipant : INetworkStruct
    {
        public NetworkBool Connected;
        public int Lives;
        public float Respawn;
        public float SecondaryCooldown;
        public SecondaryAttack Secondary;
        public static NetBattleParticipant From(BattleParticipant s) => new NetBattleParticipant { Connected=s.Connected, Lives=s.Lives, Respawn=s.Respawn, SecondaryCooldown=s.SecondaryCooldown, Secondary=s.Secondary };
        public BattleParticipant ToState() => new BattleParticipant { Connected=Connected, Lives=Lives, Respawn=Respawn, SecondaryCooldown=SecondaryCooldown, Secondary=Secondary };
    }
    public struct NetBattleVisualEvent : INetworkStruct
    {
        public int Sequence;
        public int Kind;
        public int Id;
        public int Owner;
        public int Direction;
        public int Power;
        public float X;
        public float Y;
        public static NetBattleVisualEvent From(BattleVisualEvent s) => new NetBattleVisualEvent { Sequence=s.Sequence, Kind=s.Kind, Id=s.Id, Owner=s.Owner, Direction=s.Direction, Power=s.Power, X=s.X, Y=s.Y };
        public BattleVisualEvent ToState() => new BattleVisualEvent { Sequence=Sequence, Kind=Kind, Id=Id, Owner=Owner, Direction=Direction, Power=Power, X=X, Y=Y };
    }
    public struct NetWall : INetworkStruct { public int Id; public NetworkBool Alive; public float X,Y,W,H; public static NetWall From(Wall w)=>new NetWall{Id=w.Id,Alive=w.Alive,X=w.Bounds.X,Y=w.Bounds.Y,W=w.Bounds.W,H=w.Bounds.H}; public Wall ToState()=>new Wall{Id=Id,Alive=Alive,Type="steel",Bounds=new Box(X,Y,W,H)}; }
}
