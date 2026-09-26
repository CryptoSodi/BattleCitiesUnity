using UnityEngine;

namespace BattleCities
{
    public static class BattlePreparation
    {
        public static int TankTier { get; private set; }
        public static bool Ready { get; private set; }
        public static string ApiUrl { get; private set; }
        public static void Set(int tier, string apiUrl)
        { TankTier=Mathf.Clamp(tier,0,3);ApiUrl=apiUrl;Ready=true; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {TankTier=0;Ready=false;ApiUrl=null;}
    }
}
