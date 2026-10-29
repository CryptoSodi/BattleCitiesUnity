using System;
using System.Linq;

namespace BattleCities.Core
{
    [Serializable] public sealed class CampaignTankState
    {
        public int slot,lives,tier,health,upgrades,startingTier;
        public float speedBoost,rapidFire;
    }
    public sealed partial class BattleSimulation
    {
        CampaignTankState[] campaignStart;
        readonly bool[] campaignJoined=new bool[MaxPlayers],campaignSpawned=new bool[MaxPlayers];
        public void ContinueCampaignFrom(BattleSimulation previous)
        {
            if(previous==null||!previous.Won||previous.Lost||previous.Stage+1!=Stage||previous.IsPvp||previous.Mode!=Mode||Tick!=0)
                throw new InvalidOperationException("Campaign continuation requires the next stage of a won run.");
            var slots=IsMultiplayer?Enumerable.Range(0,MaxPlayers).Where(slot=>previous.Participants[slot].Connected).ToArray():new[]{-1};
            SetCampaignStart(slots.Select(slot=>{
                var tank=previous.Tanks.FirstOrDefault(t=>t.Player&&t.Alive&&t.Slot==slot);
                return new CampaignTankState{slot=slot,lives=slot<0?previous.Lives:previous.Participants[slot].Lives,
                    startingTier=slot<0?previous.SelectedTankTier:previous.Participants[slot].TankTier,
                    tier=tank?.Tier??(slot<0?previous.SelectedTankTier:previous.Participants[slot].TankTier),
                    health=tank?.Health??0,upgrades=tank?.UpgradeLevel??0,speedBoost=tank?.SpeedBoost??0,rapidFire=tank?.RapidFire??0};
            }).ToArray());
        }
        void SetCampaignStart(CampaignTankState[] states)
        {
            campaignStart=ReplayJson.Copy(states);
            if(states==null)return;
            if(states.Length>MaxPlayers)throw new ArgumentException("Too many carried players.");
            if(states.Select(s=>s.slot).Distinct().Count()!=states.Length)throw new ArgumentException("Duplicate carried player.");
            foreach(var state in states){
                if(state.slot< -1||state.slot>=MaxPlayers||state.lives<0||state.lives>999||state.tier<0||state.tier>3||state.startingTier<0||state.startingTier>3||state.health<0||state.health>5||state.upgrades<0||state.upgrades>3||
                    float.IsNaN(state.speedBoost)||float.IsInfinity(state.speedBoost)||state.speedBoost<0||state.speedBoost>120||float.IsNaN(state.rapidFire)||float.IsInfinity(state.rapidFire)||state.rapidFire<0||state.rapidFire>120)
                    throw new ArgumentException("Invalid carried tank state.");
                if(state.slot==-1){offlineLives=state.lives;ApplyCampaignTank(Player,state);}
            }
        }
        static void ApplyCampaignTank(TankState tank,CampaignTankState state)
        {
            if(tank==null||state.health<=0)return;
            tank.Tier=state.tier;tank.Health=Math.Min(state.health,tank.MaxHealth);tank.UpgradeLevel=state.upgrades;
            tank.SpeedBoost=state.speedBoost;tank.RapidFire=state.rapidFire;
        }
        void RestoreCampaignLives(int slot)
        {
            if(campaignJoined[slot])return;campaignJoined[slot]=true;
            var state=campaignStart?.FirstOrDefault(s=>s.slot==slot);
            if(state!=null){Participants[slot].Lives=state.lives;Participants[slot].TankTier=state.startingTier;}
        }
        void RestoreCampaignTank(TankState tank)
        {
            if(campaignSpawned[tank.Slot])return;campaignSpawned[tank.Slot]=true;
            var state=campaignStart?.FirstOrDefault(s=>s.slot==tank.Slot);
            if(state!=null)ApplyCampaignTank(tank,state);
        }
    }
}
