using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BattleCities.LevelEditor
{
    public sealed partial class LevelEditorDocument
    {
        [Header("Tree family")]
        public GameObject TreePanel;
        public string TreeBrush = "forest_tree_a";

        public string ActiveTree => !BrushProperties && Selected && Selected.Kind == LevelElementKind.Prop && LevelEditorCatalog.IsTree(Selected.Tile)
            ? Selected.Tile : TreeBrush;

        static EnvironmentPiece TreePrefab(string key) => Resources.Load<EnvironmentPalette>("EnvironmentPalette")?.Prefabs.FirstOrDefault(p => p && p.Key == key);

        public void SetTreeType(string type)
        {
            if (!LevelEditorCatalog.IsTree(type)) return;
            var prefab = TreePrefab(type);
            if (!prefab) { Status("Missing tree model: " + LevelEditorCatalog.TreeLabel(type)); return; }
            EndMove();
            bool selectedTree = !BrushProperties && Selected && Selected.Kind == LevelElementKind.Prop && LevelEditorCatalog.IsTree(Selected.Tile);
#if UNITY_EDITOR
            if (TrackUndo)
            {
                Undo.RecordObject(this, "Change tree type");
                if (selectedTree) { Undo.RecordObject(Selected, "Change tree type"); Undo.RecordObject(Selected.gameObject, "Change tree type"); }
            }
#endif
            TreeBrush = type; BrushKind = LevelElementKind.Prop; Brush = type;
            if (selectedTree)
            {
                Selected.Tile = type; Selected.PropRole = prefab.Role;
                Selected.name = "Prop - " + type;
                RefreshVisual(Selected);
            }
            else { BrushProperties = true; Tool = "Paint"; }
            Dirty(); RefreshSelection();
            Status(LevelEditorCatalog.TreeLabel(type) + (selectedTree ? " applied. Position and size preserved." : " brush selected. Paint on the map."));
        }
    }
}
