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
            var previous=Simulation!=null&&Simulation.Won&&!Simulation.IsPvp&&Simulation.Stage+1==stage?Simulation:null;
            FinishRecording();replayPlayer=null;NetworkMatch=match;loadingOnline=true;
            try
            {
                if(BattleModeRules.IsTeamMode(mode))LoadStageMap(1,Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01-team").text));
                else LoadStage(stage);Simulation.ConfigureMultiplayer(mode);Simulation.RecordVisualEvents();
                if(previous!=null)Simulation.ContinueCampaignFrom(previous);
                EnemyFire=true;Simulation.DisableEnemyFire=false;
                if(mode==BattleMode.Versus&&eagle)eagle.SetActive(false);
                shotSequence=secondarySequence=0;latestPowerShot=false;onlineSecondary=InitialSpecial;
                if(!match.Object.HasStateAuthority&&magicDrops)
                {
                    magicDrops.StopForRecovery(Simulation);magicDrops=null;
                }
                ResetPrimaryFire();ResetMovementPrediction();paused=false;StartOnlineRecording();
            }
            finally{loadingOnline=false;}
        }
        public void EndOnline()
        {
            FinishRecording();NetworkMatch=null;loadingOnline=false;ResetPrimaryFire();LoadStage(Stage);paused=true;
        }
        public BattleNetworkInput ReadOnlineInput()
        {
            var result=new BattleNetworkInput{TankTier=BattlePreparation.TankTier,Move=-1,Aim=-1,ShotSequence=shotSequence,SecondarySequence=secondarySequence,Secondary=(int)onlineSecondary,PowerRequested=latestPowerShot};
            if(!IsOnline||!gameplayFocused||paused||BlockCombat||!Simulation.MatchStarted||NetworkMatch.LocalSlot<0||Simulation.Lost||Simulation.Won)return result;
            result.Move=(int?)PlayerMove()??-1;
            result.ChargeHeld=fire.IsPressed();
            var command=default(Command);QueuePrimaryCommand(ref command);
            result.Aim=(int?)command.Aim??-1;
            if(command.Fire){result.ShotSequence=++shotSequence;result.PowerRequested=latestPowerShot=command.PowerShot;}
            if(secondaryQueued){result.SecondarySequence=++secondarySequence;secondaryQueued=false;}
            return result;
        }
    }
}
