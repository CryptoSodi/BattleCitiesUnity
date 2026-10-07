using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BattleCities.LevelEditor
{
    public sealed partial class LevelEditorDocument
    {
        [Header("Building family")]
        public LevelEditorBuildingPanel BuildingPanel;
        public string BuildingBrush = LevelEditorCatalog.DefaultBuilding;
        [HideInInspector] public string BuildingPage = "TYPE";
        public string ActiveBuilding => LevelEditorCatalog.CanonicalBuilding(!BrushProperties && Selected && Selected.IsBuilding ? Selected.Tile : BuildingBrush);
        static EnvironmentPiece BuildingPrefab(string key) => Resources.Load<EnvironmentPalette>("EnvironmentPalette")?.Prefabs.FirstOrDefault(p => p && p.Key == key);

        public void SetBuildingPage(string page)
        {
            if (page != "TYPE" && page != "DAMAGE") return;
            if (page == "DAMAGE" && (BrushProperties || !Selected || !Selected.IsBuilding)) return;
            BuildingPage = page; RefreshSelection();
        }

        public void SetBuildingType(string type)
        {
            type = LevelEditorCatalog.CanonicalBuilding(type);
            if (!LevelEditorCatalog.IsBuilding(type)) return;
            var prefab = BuildingPrefab(type);
            if (!prefab) { Status("Missing building model: " + LevelEditorCatalog.BuildingLabel(type)); return; }
            EndMove();
            bool selectedBuilding = !BrushProperties && Selected && Selected.IsBuilding;
#if UNITY_EDITOR
            if (TrackUndo)
            {
                Undo.RecordObject(this, "Change building type");
                if (selectedBuilding) { Undo.RecordObject(Selected, "Change building type"); Undo.RecordObject(Selected.gameObject, "Change building type"); }
            }
#endif
            BuildingBrush = type; BrushKind = LevelElementKind.Prop; Brush = type;
            if (selectedBuilding)
            {
                Selected.Tile = type; Selected.PropRole = prefab.Role; Selected.name = "Prop - " + type;
                RefreshVisual(Selected);
            }
            else { BrushProperties = true; Tool = "Paint"; BuildingPage = "TYPE"; }
            Dirty(); RefreshSelection();
            Status(LevelEditorCatalog.BuildingLabel(type) + (selectedBuilding ? " applied. Placement and properties preserved." : " brush selected. Paint on the map."));
        }
    }
}
