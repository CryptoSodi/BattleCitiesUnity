using System;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class LevelEditorBuildingChecks
    {
        public static int Assertions { get; private set; }
        static void Require(bool value, string message) { Assertions++; if (!value) throw new InvalidOperationException("Building editor: " + message); }
        [MenuItem("Battle Cities/Level Editor/Validate building family")]
        public static void Run()
        {
            Assertions = 0;
            var root = new GameObject("Isolated building family checks"); root.SetActive(false);
            var doc = root.AddComponent<LevelEditorDocument>(); doc.BuildPreviews = false; doc.TrackUndo = false;
            doc.MapRoot = new GameObject("MapRoot").transform; doc.MapRoot.SetParent(root.transform, false);
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
            try
            {
                doc.Import("{\"field\":{\"widthTiles\":25,\"heightTiles\":25}}");
                for (int i = 0; i < LevelEditorCatalog.BuildingTypes.Length; i++)
                {
                    string key = LevelEditorCatalog.BuildingTypes[i]; var prefab = palette.Prefabs.Single(p => p && p.Key == key);
                    doc.ChooseBrush("Prop:" + LevelEditorCatalog.DefaultBuilding); doc.SetBuildingType(key);
                    Require(doc.Tool == "Paint" && doc.BrushProperties && doc.Brush == key && doc.ActiveBuilding == key, "type selects brush " + key);
                    int x = 128 + i % 5 * 256, y = 128 + i / 5 * 256;
                    doc.ApplyBrush(x + 7, y + 9); var building = doc.Selected;
                    Require(building && building.IsBuilding && building.Tile == key && building.PropRole == prefab.Role, "places chosen building " + key);
                    Require(building.X == x && building.Y == y && building.Size == new Vector2(prefab.FootprintWidth, prefab.FootprintHeight), "natural footprint and snap " + key);
                    Require(building.Damage.hitPoints == 3 && !building.OverrideDamage && building.BuildingLighting.lights, "default damage and lights " + key);
                    doc.BuildPreviews = true; doc.RefreshVisual(building); doc.BuildPreviews = false;
                    Require(building.Visual && building.Visual.GetComponentInChildren<EnvironmentPiece>().Key == key && building.Visual.GetComponentsInChildren<Renderer>().Length > 0, "actual model renders " + key);
                    int count = doc.Elements.Length; doc.ApplyBrush(x, y); Require(doc.Elements.Length == count, "overlap prevented " + key);
                }
                Require(doc.Validate().Count == 0, "placed buildings produce a valid map");
                string saved = doc.ToJson(); doc.Import(saved); Require(doc.ToJson() == saved, "building models roundtrip through JSON");
                var chosen = doc.Elements.First(e => e.IsBuilding);
                chosen.Rotation = 90; chosen.SourceId = "BUILDING_SOURCE"; chosen.DesignNotes = "Preserve building notes";
                chosen.OverrideDamage = true; chosen.Damage = new MapDamage { hitPoints = 7, normalShots = false };
                chosen.OverrideBuildingLighting = true; chosen.BuildingLighting = new MapBuildingSettings { lights = false, lightIntensity = .6f, lightRange = 3 };
                doc.Select(chosen); doc.SetTool("Select"); Vector2 size = chosen.Size; Vector3 position = chosen.transform.localPosition;
                foreach (string key in LevelEditorCatalog.BuildingTypes)
                {
                    doc.SetBuildingType(key);
                    Require(doc.Selected == chosen && chosen.Tile == key && doc.Tool == "Select" && chosen.transform.localPosition == position && chosen.Size == size && chosen.Rotation == 90, "replace in place " + key);
                    Require(chosen.OverrideDamage && chosen.Damage.hitPoints == 7 && !chosen.Damage.normalShots && chosen.OverrideBuildingLighting && !chosen.BuildingLighting.lights && chosen.BuildingLighting.lightIntensity == .6f && chosen.BuildingLighting.lightRange == 3, "preserve damage and lighting " + key);
                    Require(chosen.SourceId == "BUILDING_SOURCE" && chosen.DesignNotes == "Preserve building notes", "preserve source metadata " + key);
                }
                doc.SetBuildingPage("DAMAGE"); Require(doc.BuildingPage == "DAMAGE", "selected building opens damage page");
                doc.SetHealth("6"); doc.SetNormal(true); doc.SetPower(false);
                Require(chosen.Damage.hitPoints == 6 && chosen.Damage.normalShots && !chosen.Damage.powerShots, "damage controls remain editable");
                doc.SetBuildingPage("TYPE"); Require(doc.BuildingPage == "TYPE", "return to type choices");
                var sim = new BattleSimulation(doc.Export());
                Require(sim.Terrain.Any(w => w.IsBuildingSection && w.PropKey == chosen.Tile && w.Bounds.X == chosen.X && w.Bounds.Y == chosen.Y && w.Health == 6 && !w.BuildingSettings.lights), "gameplay receives building settings");
                foreach (string alias in new[] { "draft_building_blue", "draft_building_coral" })
                {
                    chosen.Tile = alias; doc.Select(chosen);
                    Require(doc.ActiveBuilding == LevelEditorCatalog.CanonicalBuilding(alias), "legacy model selects canonical type " + alias);
                }
                chosen.Tile = LevelEditorCatalog.DefaultBuilding;
                saved = doc.ToJson(); string brush = doc.Brush; doc.SetBuildingType("city_crate"); Require(doc.ToJson() == saved && doc.Brush == brush, "reject unrelated props");
                doc.ChooseBrush("Terrain:water"); doc.SetSurfaceType("ice"); doc.ApplyBrush(64, 64); var surface = doc.Selected;
                doc.ChooseBrush("Prop:" + LevelEditorCatalog.DefaultBuilding);
                Require(doc.Brush == "city_building_e" && doc.Selected == null && doc.BuildingPage == "TYPE", "family remembers type and opens type page");
                doc.SetBuildingPage("DAMAGE"); Require(doc.BuildingPage == "TYPE", "brush has no selected-object damage page");
                doc.SetBuildingType("draft_bld_ranger_cabin_2x2"); Require(surface.Tile == "ice", "brush changes leave previous selection intact");
                doc.Select(chosen); doc.SetTool("Select");
                if (!EditorApplication.isPlaying)
                {
                    string original = chosen.Tile; doc.TrackUndo = true; Undo.IncrementCurrentGroup();
                    doc.SetBuildingType("draft_bld_sawmill_3x2"); Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo(); Require(chosen.Tile == original, "type Undo");
                    Undo.PerformRedo(); Require(chosen.Tile == "draft_bld_sawmill_3x2", "type Redo");
                    Undo.ClearUndo(doc); Undo.ClearUndo(chosen); Undo.ClearUndo(chosen.gameObject); doc.TrackUndo = false;
                }
                saved = doc.ToJson(); doc.Import(saved); Require(doc.ToJson() == saved, "modified properties survive save/open");
                Debug.Log("[BattleCities] PASS: " + Assertions + " building family checks.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
