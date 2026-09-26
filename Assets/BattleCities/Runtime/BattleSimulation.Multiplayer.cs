using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCities.Core
{
    public enum BattleMode { Offline, Coop, Versus }

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
        public bool IsPvp => Mode == BattleMode.Versus;
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
            Tanks.Clear();Shots.Clear();Mines.Clear();Drones.Clear();Turrets.Clear();LandDrones.Clear();
            foreach(var p in Participants){p.Connected=false;p.Lives=3;p.Respawn=0;p.SecondaryCooldown=0;}
            if(IsPvp){wave=Array.Empty<EnemySpec>();BaseAlive=false;BaseBounds=new Box(-1024,-1024,64,64);}
            // Search clear, non-overlapping spawns near opposite corners for PvP,
            // and near the original player spawn for co-op. Never remove cover.
            for(int slot=0;slot<MaxPlayers;slot++)
            {
                float leftSpawnX=playerSpawn.x+32;
                float tx=IsPvp?(slot%2==0?64:Width-64):
                    slot==1?2*(BaseBounds.X+BaseBounds.W/2)-leftSpawnX:leftSpawnX;
                float ty=IsPvp?(slot<2?Height-64:64):playerSpawn.y+32;
                var candidates=new List<Point>();
                for(int y=32;y<=Height-32;y+=16)for(int x=32;x<=Width-32;x+=16)
                    if(Free(TankState.MovementBox(x,y),new TankState())&&
                       !onlineSpawns.Take(slot).Any(p=>TankState.MovementBox(x,y).Overlaps(TankState.MovementBox(p.x,p.y))))
                        candidates.Add(new Point{x=x,y=y});
                onlineSpawns[slot]=candidates.OrderBy(p=>DistanceSquared(p.x,p.y,tx,ty)).FirstOrDefault()
                    ?? throw new InvalidOperationException("Map has insufficient clear space for four players.");
            }
        }

        public void SetParticipant(int slot,bool connected)
        {
            if(slot<0||slot>=MaxPlayers)throw new ArgumentOutOfRangeException(nameof(slot));
            var p=Participants[slot];
            if(p.Connected==connected)return;
            p.Connected=connected;
            if(!connected)
            {
                Tanks.RemoveAll(t=>t.Player&&t.Slot==slot);
                // Departing players cannot leave autonomous weapons in the match.
                Mines.RemoveAll(m=>m.OwnerSlot==slot);Drones.RemoveAll(d=>d.OwnerSlot==slot);
                Turrets.RemoveAll(t=>t.OwnerSlot==slot);LandDrones.RemoveAll(d=>d.OwnerSlot==slot);
            }
            else {p.Lives=3;p.Respawn=0;TrySpawnParticipant(slot);}
        }

        public void BeginMatch()
        {
            if(Participants.Count(p=>p.Connected)<2)throw new InvalidOperationException("At least two players are required.");
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
            var tank=new TankState{Id=nextId+1,Slot=slot,Player=true,Health=5,Shield=3.5f};
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
                    p.Slide=Terrain.Any(w=>w.Alive&&w.Type=="ice"&&w.Bounds.Overlaps(p.Bounds))?.5f:0;
                }
                else if(p.Slide>0){p.Slide-=dt;Move(p);}
                p.Aim=command.Aim.HasValue&&(int)command.Aim.Value>=0&&(int)command.Aim.Value<4?command.Aim.Value:p.Direction;
                if(command.Fire)Fire(p,command.PowerShot);
                if(command.SecondaryFire)UseSecondary();
                if(PickupType!=null&&p.Bounds.Overlaps(new Box(PickupX-32,PickupY-32,64,64)))
                {ApplyPowerup(PickupType);Score+=500;PickupType=null;PickupClaim=null;}
            }
            actingSlot=-1;
        }

        private void UpdateMatchResult()
        {
            var remaining=Enumerable.Range(0,MaxPlayers).Where(i=>Participants[i].Connected&&Participants[i].Lives>0).ToArray();
            if(IsPvp&&remaining.Length<=1){WinnerSlot=remaining.Length==1?remaining[0]:-1;Won=true;}
            else if(!IsPvp&&remaining.Length==0)Lost=true;
        }

        private bool OwnedSecondary(int slot)=>!IsMultiplayer||slot==(actingSlot>=0?actingSlot:LocalPlayerSlot);
        private bool IsSecondaryTarget(TankState t,int ownerSlot)=>IsPvp?t.Player&&t.Slot!=ownerSlot:!t.Player;
        private int ShotSlot(ShotState s)
        {
            var tank=Tanks.Find(t=>t.Id==s.Owner);
            if(tank!=null)return tank.Slot;
            var turret=Turrets.Find(t=>t.Id==s.Owner);
            return turret?.OwnerSlot??s.OwnerSlot;
        }
        private bool ShotTargetsTank(ShotState s,TankState t)=>IsPvp?t.Player&&t.Slot!=ShotSlot(s):t.Player!=s.Player;
        private bool ShotTargetsSecondary(ShotState s,int ownerSlot)=>IsPvp?ShotSlot(s)!=ownerSlot:!s.Player;
        private bool ShotsOppose(ShotState a,ShotState b)=>IsPvp?ShotSlot(a)!=ShotSlot(b):a.Player!=b.Player;
    }
}
