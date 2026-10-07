using System;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class LevelEditorTreeChecks
    {
        public static int Assertions { get; private set; }
        static void Require(bool value, string message) { Assertions++; if (!value) throw new InvalidOperationException("Tree editor: " + message); }

        [MenuItem("Battle Cities/Level Editor/Validate tree family")]
        public static void Run()
        {
            Assertions = 0;
            var root = new GameObject("Isolated tree editor checks"); root.SetActive(false);
            var doc = root.AddComponent<LevelEditorDocument>(); doc.BuildPreviews = false; doc.TrackUndo = false;
            doc.MapRoot = new GameObject("MapRoot").transform; doc.MapRoot.SetParent(root.transform, false);
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
            try
            {
                doc.Import("{\"field\":{\"widthTiles\":25,\"heightTiles\":25}}");
                for (int i = 0; i < LevelEditorCatalog.TreeTypes.Length; i++)
                {
                    string key = LevelEditorCatalog.TreeTypes[i];
                    var prefab = palette.Prefabs.Single(p => p && p.Key == key);
                    doc.ChooseBrush("Prop:forest_tree_a"); doc.SetTreeType(key);
                    Require(doc.Tool == "Paint" && doc.BrushProperties && doc.Brush == key && doc.ActiveTree == key, "type configures painting " + key);
                    doc.ApplyBrush(128 + i * 224 + 7, 128 + 9);
                    var tree = doc.Selected;
                    Require(tree && tree.Kind == LevelElementKind.Prop && tree.Tile == key && tree.PropRole == prefab.Role, "places correct tree " + key);
                    Require(tree.X == 128 + i * 224 && tree.Y == 128 && tree.Size == new Vector2(prefab.FootprintWidth, prefab.FootprintHeight), "natural size and snap " + key);
                    doc.BuildPreviews = true; doc.RefreshVisual(tree); doc.BuildPreviews = false;
                    Require(tree.Visual && tree.Visual.GetComponentInChildren<EnvironmentPiece>().Key == key && tree.Visual.GetComponentsInChildren<Renderer>().Length > 0, "actual model renders " + key);
                    int count = doc.Elements.Length; doc.ApplyBrush(tree.X, tree.Y);
                    Require(doc.Elements.Length == count, "overlap does not duplicate trees " + key);
                }
                string saved = doc.ToJson(); doc.Import(saved);
                Require(doc.ToJson() == saved, "all tree models survive JSON roundtrip");
                var chosen = doc.Elements.First(e => e.Tile == "draft_tree_2x3");
                chosen.Rotation = 90; chosen.SourceId = "TREE_SOURCE"; chosen.DesignNotes = "Preserve me";
                chosen.OverrideDamage = true; chosen.Damage = new MapDamage { hitPoints = 5, normalShots = false };
                doc.Select(chosen); doc.SetTool("Select");
                Vector2 size = chosen.Size; Vector3 position = chosen.transform.localPosition;
                foreach (string type in LevelEditorCatalog.TreeTypes)
                {
                    doc.SetTreeType(type);
                    Require(doc.Selected == chosen && chosen.Tile == type && doc.Tool == "Select", "selected type changes in place " + type);
                    Require(chosen.Size == size && chosen.transform.localPosition == position && chosen.Rotation == 90, "selected dimensions and rotation preserved " + type);
                    Require(chosen.SourceId == "TREE_SOURCE" && chosen.DesignNotes == "Preserve me" && chosen.OverrideDamage && chosen.Damage.hitPoints == 5 && !chosen.Damage.normalShots, "notes and damage preserved " + type);
                }
                var sim = new BattleSimulation(doc.Export());
                Require(sim.Terrain.Any(w => w.PropKey == chosen.Tile && w.Bounds.X == chosen.X && w.Bounds.Y == chosen.Y && w.Health == 5), "gameplay receives tree and damage settings");
                saved = doc.ToJson(); string brush = doc.Brush; doc.SetTreeType("city_crate");
                Require(doc.ToJson() == saved && doc.Brush == brush, "unrequested prop types rejected");
                doc.ChooseBrush("Terrain:water"); doc.SetSurfaceType("ice"); doc.ApplyBrush(64, 64);
                var water = doc.Selected; Require(water.Tile == "ice", "Water family still works");
                doc.ChooseBrush("Prop:forest_tree_a");
                Require(doc.Brush == "draft_tree_2x4" && doc.Selected == null, "family remembers last tree type");
                doc.SetTreeType("forest_tree_b"); Require(water.Tile == "ice", "choosing tree brush leaves prior selection intact");
                doc.Select(chosen); doc.SetTool("Select");
                if (!EditorApplication.isPlaying)
                {
                    string original = chosen.Tile;
                    doc.TrackUndo = true; Undo.IncrementCurrentGroup(); doc.SetTreeType("forest_tree_b"); Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo(); Require(chosen.Tile == original, "tree type Undo");
                    Undo.PerformRedo(); Require(chosen.Tile == "forest_tree_b", "tree type Redo");
                    Undo.ClearUndo(doc); Undo.ClearUndo(chosen); Undo.ClearUndo(chosen.gameObject); doc.TrackUndo = false;
                }
                Debug.Log("[BattleCities] PASS: " + Assertions + " tree family checks.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
