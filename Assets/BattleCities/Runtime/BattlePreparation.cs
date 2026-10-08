using UnityEngine;

namespace BattleCities
{
    public static class BattlePreparation
    {
        public static int TankTier { get; private set; }
        public static bool Ready { get; private set; }
        public static string ApiUrl { get; private set; }
        public static string OwnerId { get; private set; }
        public static string OwnerProvider { get; private set; }
        public static string FuelRequestId { get; private set; }
        public static int StartStage { get; private set; }=1;
        public static int RequestedRestartTier { get; private set; }=-1;
        public static int RequestedRestartStage { get; private set; }=1;
        public static void Set(int tier, string apiUrl, string ownerId=null, string ownerProvider=null, string fuelRequestId=null, int stage=1)
        { TankTier=Mathf.Clamp(tier,0,3);ApiUrl=apiUrl;OwnerId=ownerId;OwnerProvider=ownerProvider;FuelRequestId=fuelRequestId;StartStage=Mathf.Clamp(stage,1,35);Ready=true; }
        public static void RequestRestart(int tier,int stage)
        {RequestedRestartTier=Mathf.Clamp(tier,0,3);RequestedRestartStage=Mathf.Clamp(stage,1,35);}
        public static bool TryTakeRestart(out int tier,out int stage)
        {
            tier=RequestedRestartTier;stage=RequestedRestartStage;
            if(tier<0)return false;
            RequestedRestartTier=-1;RequestedRestartStage=1;return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {TankTier=0;Ready=false;ApiUrl=null;OwnerId=OwnerProvider=FuelRequestId=null;StartStage=RequestedRestartStage=1;RequestedRestartTier=-1;}
    }
}
