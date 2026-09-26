using BattleCities.Core;
using BattleCities.Multiplayer;
using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        public BattleNetworkMatch NetworkMatch {get;private set;}
        public bool IsOnline=>NetworkMatch;
        private bool loadingOnline;
        private int shotSequence,secondarySequence;
        private bool latestPowerShot;
        private SecondaryAttack onlineSecondary=SecondaryAttack.Mine;

        public void PrepareOnline(BattleNetworkMatch match,BattleMode mode,int stage)
        {
            NetworkMatch=match;loadingOnline=true;
            try
            {
                LoadStage(stage);Simulation.ConfigureMultiplayer(mode);Simulation.RecordVisualEvents();
                EnemyFire=true;Simulation.DisableEnemyFire=false;
                if(mode==BattleMode.Versus&&eagle)eagle.SetActive(false);
                shotSequence=secondarySequence=0;latestPowerShot=false;onlineSecondary=SecondaryAttack.Mine;
                ResetPrimaryFire();paused=false;
            }
            finally{loadingOnline=false;}
        }
        public void EndOnline()
        {
            NetworkMatch=null;loadingOnline=false;ResetPrimaryFire();LoadStage(Stage);paused=true;
        }
        public BattleNetworkInput ReadOnlineInput()
        {
            var result=new BattleNetworkInput{Move=-1,Aim=-1,ShotSequence=shotSequence,SecondarySequence=secondarySequence,Secondary=(int)onlineSecondary,PowerRequested=latestPowerShot};
            if(!IsOnline||paused||!Simulation.MatchStarted||NetworkMatch.LocalSlot<0||Simulation.Lost||Simulation.Won)return result;
            result.Move=(int?)Latest(moveKeys,moveOrder)??-1;result.Aim=(int?)Latest(aimKeys,aimOrder)??-1;
            result.ChargeHeld=fire.IsPressed();
            var command=default(Command);QueuePrimaryCommand(ref command);
            if(command.Fire){result.ShotSequence=++shotSequence;result.PowerRequested=latestPowerShot=command.PowerShot;}
            if(secondaryQueued){result.SecondarySequence=++secondarySequence;secondaryQueued=false;}
            return result;
        }
    }
}
