using System;

namespace BattleCities.LevelEditor
{
    /// <summary>Campaign elements and the user-approved surface variants.</summary>
    public static class LevelEditorCatalog
    {
        public static readonly string[] Terrain = { "brick", "steel", "jungle", "water" };
        public static readonly string[] SurfaceTypes = { "water", "lava", "ice", "grease" };
        public static readonly string[] TreeTypes = { "forest_tree_a", "forest_tree_b", "draft_tree_2x2", "draft_tree_2x3", "draft_tree_3x2", "draft_tree_2x4" };
        public static readonly string[] TreeLabels = { "CLASSIC A", "CLASSIC B", "GROVE 2 x 2", "GROVE 2 x 3", "GROVE 3 x 2", "GROVE 2 x 4" };
        public const string DefaultBuilding = "draft_bld_harbor_warehouse_blue_3x2";
        public static readonly string[] BuildingTypes = {
            DefaultBuilding, "draft_bld_harbor_warehouse_coral_3x2", "draft_bld_harbor_store_2x3",
            "draft_bld_ranger_cabin_2x2", "draft_bld_logging_hut_2x2", "draft_bld_sawmill_3x2",
            "draft_bld_factory_workshop_3x2", "draft_bld_factory_store_2x3", "draft_bld_utility_workshop_2x2",
            "city_building_a", "city_building_e" };
        public static readonly string[] BuildingLabels = { "BLUE WAREHOUSE", "CORAL WAREHOUSE", "HARBOR STORE", "RANGER CABIN", "LOGGING HUT", "SAWMILL", "FACTORY WORKSHOP", "FACTORY STORE", "UTILITY WORKSHOP", "CITY A", "CITY E" };
        public static string CanonicalBuilding(string key) => key == "draft_building_blue" ? DefaultBuilding : key == "draft_building_coral" ? BuildingTypes[1] : key;
        public static bool IsBuilding(string key) => Array.IndexOf(BuildingTypes, CanonicalBuilding(key)) >= 0;
        public static string BuildingLabel(string key) { int index = Array.IndexOf(BuildingTypes, CanonicalBuilding(key)); return index >= 0 ? BuildingLabels[index] : key; }
        public static string DisplayLabel(string key) => IsTree(key) ? TreeLabel(key) : IsBuilding(key) ? BuildingLabel(key) : (key ?? "").ToUpperInvariant();
        public static bool IsTree(string key) => Array.IndexOf(TreeTypes, key) >= 0;
        public static string TreeLabel(string key) { int index = Array.IndexOf(TreeTypes, key); return index >= 0 ? TreeLabels[index] : key; }
        public static bool IsSurface(string key) => Array.IndexOf(SurfaceTypes, key) >= 0;
        public static bool Allows(LevelElementKind kind, string key) =>
            kind == LevelElementKind.Terrain ? (Array.IndexOf(Terrain, key) >= 0 || IsSurface(key)) :
            kind == LevelElementKind.Prop ? IsTree(key) || IsBuilding(key) :
            kind == LevelElementKind.PlayerSpawn ? key == "player" :
            kind == LevelElementKind.EnemySpawn ? key == "enemy" :
            kind == LevelElementKind.Base && key == "base";
    }
}
