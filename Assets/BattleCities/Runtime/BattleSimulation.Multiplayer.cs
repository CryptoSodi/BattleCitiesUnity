using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCities.Core
{
    public enum BattleMode { Offline, Coop, Versus, TeamBattle, CaptureFlag, CaptureFlagDuel }

    public static class BattleModeRules
    {
        public static bool IsFlagMode(BattleMode mode)=>mode==BattleMode.CaptureFlag||mode==BattleMode.CaptureFlagDuel;
        public static bool IsTeamMode(BattleMode mode)=>mode==BattleMode.TeamBattle||IsFlagMode(mode);
        public static int PlayerLimit(BattleMode mode)=>mode==BattleMode.Offline?1:mode==BattleMode.Coop||mode==BattleMode.CaptureFlagDuel?2:4;
        public static string Label(BattleMode mode)=>mode==BattleMode.Offline?"SINGLE PLAYER":mode==BattleMode.Coop?"2 PLAYER CO-OP":mode==BattleMode.TeamBattle?"2V2 BRAWL":mode==BattleMode.CaptureFlagDuel?"CAPTURE THE FLAG 1V1":mode==BattleMode.CaptureFlag?"CAPTURE THE FLAG 2V2":"LEGACY PVP";
        public static BattleMode NextOnline(BattleMode mode)=>mode==BattleMode.Coop?BattleMode.TeamBattle:mode==BattleMode.TeamBattle?BattleMode.CaptureFlagDuel:mode==BattleMode.CaptureFlagDuel?BattleMode.CaptureFlag:BattleMode.Coop;
    }

    [Serializable]
    public sealed class BattleParticipant
    {
        public bool Connected;
        public int Lives = 3;
        public float Respawn, SecondaryCooldown;
        public SecondaryAttack Secondary = SecondaryAttack.Mine;
    }

    public sealed partial class BattleSimulation
    {
        public const int MaxPlayers = 4;
        public BattleMode Mode { get; private set; }
        public bool IsMultiplayer => Mode != BattleMode.Offline;
        public bool IsTeamBattle => Mode == BattleMode.TeamBattle || IsCaptureFlag;
        public bool IsPvp => Mode == BattleMode.Versus || IsTeamBattle;
        public static int TeamForSlot(int slot) => slot < 0 ? -1 : slot % 2;
        public bool AreAllies(int a,int b) => IsTeamBattle ? TeamForSlot(a)==TeamForSlot(b) : !IsPvp || a==b;
        public bool IsWinningSlot(int slot) => WinnerSlot>=0 && (IsTeamBattle ? AreAllies(slot,WinnerSlot) : slot==WinnerSlot);
        public bool MatchStarted { get; private set; }
        public int WinnerSlot { get; private set; } = -1;
        public int LocalPlayerSlot;
        public readonly BattleParticipant[] Participants = Enumerable.Range(0,MaxPlayers).Select(_=>new BattleParticipant()).ToArray();
        private int actingSlot = -1;
        private IReadOnlyDictionary<int,Command> onlineCommands;
        private readonly Point[] onlineSpawns = new Point[MaxPlayers];

        public void ConfigureMultiplayer(BattleMode mode)
        {
            if(mode == BattleMode.Offline)throw new ArgumentException("Online mode required.");
            Mode=mode;MatchStarted=false;WinnerSlot=-1;
            for(int i=0;i<MaxPlayers;i++)ResultStats[i]=new BattleResultStats();
            Tanks.Clear();Shots.Clear();Mines.Clear();Drones.Clear();Turrets.Clear();LandDrones.Clear();
            foreach(var p in Participants){p.Connected=false;p.Lives=3;p.Respawn=0;p.SecondaryCooldown=0;}
            if(mode==BattleMode.Versus){wave=Array.Empty<EnemySpec>();BaseAlive=false;BaseBounds=new Box(-1024,-1024,64,64);}
            // Search clear, non-overlapping spawns near opposite corners for PvP,
            // and near the original player spawn for co-op. Never remove cover.
            for(int slot=0;slot<MaxPlayers;slot++)
            {
                float leftSpawnX=playerSpawn.x+32;
                float tx=IsTeamBattle?(slot<2?Width/2-128:Width/2+128):IsPvp?(slot%2==0?64:Width-64):
                    slot==1?2*(BaseBounds.X+BaseBounds.W/2)-leftSpawnX:leftSpawnX;
                float ty=IsTeamBattle?(TeamForSlot(slot)==0?Height-32:32):IsPvp?(slot<2?Height-64:64):playerSpawn.y+32;
                var candidates=new List<Point>();
                for(int y=32;y<=Height-32;y+=16)for(int x=32;x<=Width-32;x+=16)
                    if(Free(TankState.MovementBox(x,y),new TankState())&&
                       !onlineSpawns.Take(slot).Any(p=>TankState.MovementBox(x,y).Overlaps(TankState.MovementBox(p.x,p.y))))
                        candidates.Add(new Point{x=x,y=y});
                onlineSpawns[slot]=candidates.OrderBy(p=>DistanceSquared(p.x,p.y,tx,ty)).FirstOrDefault()
                    ?? throw new InvalidOperationException("Map has insufficient clear space for four players.");
            }
            if(IsCaptureFlag)ResetFlags();
        }

        public void SetParticipant(int slot,bool connected)
        {
            if(slot<0||slot>=MaxPlayers)throw new ArgumentOutOfRangeException(nameof(slot));
            var p=Participants[slot];
            if(p.Connected==connected)return;
            ReplayLifecycle?.Invoke(new ReplayEvent {kind="participant",slot=slot,connected=connected});
            p.Connected=connected;
            if(connected)ResultStats[slot].Participated=true;
            if(!connected)
            {
                DropCarriedFlag(slot);
                Tanks.RemoveAll(t=>t.Player&&t.Slot==slot);
                // Departing players cannot leave autonomous weapons in the match.
                Mines.RemoveAll(m=>m.OwnerSlot==slot);Drones.RemoveAll(d=>d.OwnerSlot==slot);
                Turrets.RemoveAll(t=>t.OwnerSlot==slot);LandDrones.RemoveAll(d=>d.OwnerSlot==slot);
            }
            else {p.Lives=3;p.Respawn=0;TrySpawnParticipant(slot);}
        }

        public void BeginMatch()
        {
            if(Participants.Count(p=>p.Connected)<(Mode==BattleMode.Versus?2:BattleModeRules.PlayerLimit(Mode)))throw new InvalidOperationException("At least two players are required.");
            ReplayLifecycle?.Invoke(new ReplayEvent {kind="begin"});
            MatchStarted=true;intro=2;
        }

        public void StepMultiplayer(IReadOnlyDictionary<int,Command> commands)
        {
            if(!IsMultiplayer||!MatchStarted)return;
            onlineCommands=commands;
            try {Step(default);} finally {onlineCommands=null;actingSlot=-1;}
        }

        private bool TrySpawnParticipant(int slot)
        {
            var origin=onlineSpawns[slot];
            var tank=new TankState{Id=nextId+1,Slot=slot,Player=true,Health=5,Shield=3.5f,Direction=IsTeamBattle&&TeamForSlot(slot)==1?Facing.Down:Facing.Up,Aim=IsTeamBattle&&TeamForSlot(slot)==1?Facing.Down:Facing.Up};
            for(int radius=0;radius<=160;radius+=16)
            for(int y=-radius;y<=radius;y+=16)for(int x=-radius;x<=radius;x+=16)
            {
                if(Math.Max(Math.Abs(x),Math.Abs(y))!=radius)continue;
                float px=origin.x+x,py=origin.y+y;
                if(!Free(TankState.MovementBox(px,py),tank))continue;
                tank.X=px;tank.Y=py;nextId++;Tanks.Add(tank);return true;
            }
            return false;
        }

        private void UpdateParticipants(float dt)
        {
            for(int slot=0;slot<MaxPlayers;slot++)
            {
                var participant=Participants[slot];if(!participant.Connected)continue;
                actingSlot=slot;participant.SecondaryCooldown=Math.Max(0,participant.SecondaryCooldown-dt);
                var p=Player;
                if(p==null)
                {
                    if(participant.Lives>0){participant.Respawn-=dt;if(participant.Respawn<=0)TrySpawnParticipant(slot);}
                    continue;
                }
                Command command=default;
                onlineCommands?.TryGetValue(slot,out command);
                if(command.Move.HasValue&&(int)command.Move.Value>=0&&(int)command.Move.Value<4)
                {
                    Rotate(p,command.Move.Value);Move(p);
                    p.Slide=(TouchesSlipperyGround(p)&&!p.InQuicksand)?.5f:0;
                }
                else if(p.Slide>0){p.Slide-=dt;Move(p);}
                p.Aim=command.Aim.HasValue&&(int)command.Aim.Value>=0&&(int)command.Aim.Value<4?command.Aim.Value:p.Direction;
                if(command.Fire)Fire(p,command.PowerShot);
                if(command.SecondaryFire)UseSecondary();
                if(PickupType!=null&&p.Bounds.Overlaps(new Box(PickupX-32,PickupY-32,64,64)))
                {ApplyPowerup(PickupType);RecordResultBonus(500);PickupType=null;PickupClaim=null;}
            }
            actingSlot=-1;
        }

        private void UpdateMatchResult()
        {
            var remaining=Enumerable.Range(0,MaxPlayers).Where(i=>Participants[i].Connected&&Participants[i].Lives>0).ToArray();
            if(IsCaptureFlag)return;
            if(IsTeamBattle)
            {
                var teams=remaining.Select(TeamForSlot).Distinct().ToArray();
                if(!BaseAlive||!RivalBaseAlive||teams.Length<2){WinnerSlot=!BaseAlive?1:!RivalBaseAlive?0:teams.Length==1?teams[0]:-1;Won=true;}
            }
            else if(IsPvp&&remaining.Length<=1){WinnerSlot=remaining.Length==1?remaining[0]:-1;Won=true;}
            else if(!IsPvp&&remaining.Length==0)Lost=true;
        }

        private bool OwnedSecondary(int slot)=>!IsMultiplayer||slot==(actingSlot>=0?actingSlot:LocalPlayerSlot);
        private bool IsSecondaryTarget(TankState t,int ownerSlot)=>IsTeamBattle?!AreAllies(t.Slot,ownerSlot):IsPvp?t.Player&&t.Slot!=ownerSlot:!t.Player;
        private int ShotSlot(ShotState s)
        {
            var tank=Tanks.Find(t=>t.Id==s.Owner);
            if(tank!=null)return tank.Slot;
            var turret=Turrets.Find(t=>t.Id==s.Owner);
            return turret?.OwnerSlot??s.OwnerSlot;
        }
        private bool ShotTargetsTank(ShotState s,TankState t)=>IsTeamBattle?!AreAllies(ShotSlot(s),t.Slot):IsPvp?t.Player&&t.Slot!=ShotSlot(s):t.Player!=s.Player;
        private bool ShotTargetsSecondary(ShotState s,int ownerSlot)=>IsTeamBattle?!AreAllies(ShotSlot(s),ownerSlot):IsPvp?ShotSlot(s)!=ownerSlot:!s.Player;
        private bool ShotsOppose(ShotState a,ShotState b)=>IsTeamBattle?!AreAllies(ShotSlot(a),ShotSlot(b)):IsPvp?ShotSlot(a)!=ShotSlot(b):a.Player!=b.Player;
    }
}
