using System;
using Newtonsoft.Json;

namespace BattleCities.Core
{
    public readonly struct TankStats
    {
        public readonly int Health, Rounds, Speed, Power;
        public readonly float ShellSpeed;
        public TankStats(int health,int rounds,int speed,int power,float shellSpeed=600)
        { Health=health;Rounds=rounds;Speed=speed;Power=power;ShellSpeed=shellSpeed; }
        public float MovementSpeed => 120+60*Speed;
        public int Rating(int row) => row==0?Health:row==1?Rounds:row==2?Speed:Power;
    }

    /// <summary>The roster and simulation share these fixed, deterministic profiles.</summary>
    public static class TankProfiles
    {
        public const int Version=1;
        static readonly TankStats[] Players={
            new TankStats(1,1,1,1), // Vanguard
            new TankStats(2,1,1,1), // Striker
            new TankStats(1,2,1,1), // Twin Fang: half normal reload time
            new TankStats(5,1,1,2)  // Siegebreaker
        };
        static readonly TankStats[] Enemies={
            new TankStats(1,1,1,1),     // Normal
            new TankStats(1,1,3,1),     // Wheeled Scout
            new TankStats(1,1,1,2,900), // Fast-shell / firepower
            new TankStats(5,1,1,2)      // Armored
        };
        public static TankStats Get(int tier,bool player) => (player?Players:Enemies)[Math.Max(0,Math.Min(3,tier))];
    }

    public sealed partial class BattleSimulation
    {
        public int PowerShotDamageVersion { get; private set; } = 1;
        public int TankStatsVersion { get; private set; }
        TankState NewTank(int tier,bool player) => new TankState {
            Tier=tier,Player=player,StatsVersion=TankStatsVersion,
            Health=TankStatsVersion==0?TankState.StartingHealth(tier,player):TankProfiles.Get(tier,player).Health
        };

        // Selection is locked when combat starts. A later round may choose again.
        public bool SetParticipantTankTier(int slot,int tier)
        {
            if(!IsMultiplayer||TankStatsVersion==0||MatchStarted||slot<0||slot>=MaxPlayers||tier<0||tier>3)return false;
            var participant=Participants[slot];
            if(participant.TankTier==tier)return true;
            ReplayLifecycle?.Invoke(new ReplayEvent{kind="tank",slot=slot,value=tier.ToString(System.Globalization.CultureInfo.InvariantCulture)});
            participant.TankTier=tier;
            var tank=Tanks.Find(t=>t.Player&&t.Alive&&t.Slot==slot);
            if(tank!=null){tank.Tier=tier;tank.Health=tank.MaxHealth;tank.UpgradeLevel=0;}
            return true;
        }
    }
}
