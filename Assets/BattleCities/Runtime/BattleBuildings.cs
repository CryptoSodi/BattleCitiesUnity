using System;

namespace BattleCities.Core
{
    [Serializable]
    public sealed class MapBuildingSettings
    {
        public bool lights = true;
        public float lightIntensity = 1f;
        public float lightRange = 2.2f;
        public MapBuildingSettings Copy() => (MapBuildingSettings)MemberwiseClone();
    }

    public static class BattleBuildings
    {
        public const int SectionSize = 32, DefaultHealth = 3;
        public static bool IsBuilding(string key) => key != null &&
            (key.StartsWith("draft_bld_", StringComparison.Ordinal) ||
             key.StartsWith("draft_building_", StringComparison.Ordinal) ||
             key.StartsWith("city_building_", StringComparison.Ordinal));
        public static MapDamage DefaultDamage() => new MapDamage { hitPoints = DefaultHealth };
    }
}
