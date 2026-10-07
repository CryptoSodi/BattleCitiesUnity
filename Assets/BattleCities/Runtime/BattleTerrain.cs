namespace BattleCities.Core
{
    // These stable keys are shared by map data, collision, weather and presentation.
    public static class BattleTerrain
    {
        public const string Water="water", Lava="lava", MuddyWater="muddyWater",
            Quicksand="quicksand", Ice="ice", Grease="grease";
        public static string Normalize(string type)
        {
            switch(type)
            {
                case "muddywater": case "muddy_water": return MuddyWater;
                case "quickSand": case "quick_sand": return Quicksand;
                default: return type;
            }
        }
        public static bool BlocksTank(string type)=>type==Water||type==Lava||type==MuddyWater;
        public static bool IsBasin(string type)=>BlocksTank(type)||type==Quicksand;
        public static bool IsSlippery(string type)=>type==Ice||type==Grease;
        public static bool IsSurface(string type)=>IsBasin(type)||IsSlippery(type);
        public static bool HasWaterRipples(string type)=>type==Water||type==MuddyWater;
    }
}
