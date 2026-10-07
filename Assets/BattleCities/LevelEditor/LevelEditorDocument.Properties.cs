using System;
using System.Linq;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BattleCities.LevelEditor
{
    public sealed partial class LevelEditorDocument
    {
        [Header("Map settings and contextual properties")]
        public TMP_InputField WidthField, HeightField;
        public TMP_Text PropertyHeading, TileSizeHelp, DamageHelp;
        public GameObject DeleteControl;
        public GameObject SurfacePanel, DamagePanel;
        public string SurfaceBrush = "water";
        [HideInInspector] public bool BrushProperties;
        public TextAsset BrickSectionData;
        public Texture2D[] GroundTextures;
        public LevelEditorGamePreview GamePreview;

        public string ActiveSurface => !BrushProperties && Selected && LevelEditorCatalog.IsSurface(Selected.Tile)
            ? Selected.Tile : SurfaceBrush;
        public void ApplyMapSize()
        {
            if (!WidthField || !HeightField || !int.TryParse(WidthField.text, out int width) || !int.TryParse(HeightField.text, out int height))
            { Status("Enter whole numbers for map width and height."); return; }
            ResizeMap(width, height);
        }
        public bool ResizeMap(int width, int height)
        {
            if (width < 4 || width > 40 || height < 4 || height > 40)
            { Status("Map width and height must each be 4 to 40 large tiles."); return false; }
            var outside = Elements.FirstOrDefault(e => e.gameObject.activeSelf && (e.Bounds.Right > width * 64 + .01f || e.Bounds.Bottom > height * 64 + .01f));
            if (outside)
            { Status("Cannot shrink: move or erase elements outside the new boundary first."); return false; }
#if UNITY_EDITOR
            if (TrackUndo) Undo.RecordObject(this, "Resize level");
#endif
            WidthTiles = width; HeightTiles = height; FrameMap(); Dirty();
            if (GamePreview) GamePreview.Invalidate();
            RefreshSelection(); Status("Map size: " + width + " x " + height + " large tiles."); return true;
        }
        public void SetTileSize(int size)
        {
            if (size != 64 && size != 32 && size != 16) return;
            Snap = size; RefreshSelection();
            Status(size == 32 ? "Small tiles: four 32 x 32 tiles fit inside one large tile." : size == 16 ? "Fine tiles: sixteen 16 x 16 cells fit inside one large tile." : "Large tiles: 64 x 64 units.");
        }
        public void SetSurfaceType(string type)
        {
            if (!LevelEditorCatalog.IsSurface(type)) return;
            bool selectedSurface = !BrushProperties && Selected && Selected.Kind == LevelElementKind.Terrain && LevelEditorCatalog.IsSurface(Selected.Tile);
#if UNITY_EDITOR
            if (TrackUndo) { Undo.RecordObject(this, "Change surface type"); if (selectedSurface) Undo.RecordObject(Selected, "Change surface type"); }
#endif
            SurfaceBrush = type; BrushKind = LevelElementKind.Terrain; Brush = type;
            if (selectedSurface) { Selected.Tile = type; Selected.name = "Terrain - " + type; RefreshVisual(Selected); }
            else { BrushProperties = true; Tool = "Paint"; }
            Dirty(); RefreshSelection(); Status(type.ToUpperInvariant() + (selectedSurface ? " applied to the selected surface." : " brush selected. Paint on the map."));
        }
        void RefreshPropertyContext()
        {
            if (ToolLabel) ToolLabel.text = Tool.ToUpperInvariant() + "  /  " + LevelEditorCatalog.DisplayLabel(Brush) + "  /  " + Snap + " UNITS";
            bool building = BrushProperties ? BrushKind == LevelElementKind.Prop && LevelEditorCatalog.IsBuilding(Brush) : Selected && Selected.IsBuilding && LevelEditorCatalog.IsBuilding(Selected.Tile);
            if (BuildingPanel) { BuildingPanel.gameObject.SetActive(building); if (building) BuildingPanel.Refresh(); }
            bool tree = BrushProperties ? BrushKind == LevelElementKind.Prop && LevelEditorCatalog.IsTree(Brush) : Selected && Selected.Kind == LevelElementKind.Prop && LevelEditorCatalog.IsTree(Selected.Tile);
            if (TreePanel) TreePanel.SetActive(tree);
            if (DamageHelp) DamageHelp.gameObject.SetActive(!building && !tree && !BrushProperties && Selected && Selected.SupportsDamage);
            if (DamageHelp) DamageHelp.text = Selected && Selected.IsBuilding
                ? "HP is per small building section.\nWindow lights fade with damage.\nLight settings: Unity Inspector."
                : "Terrain HP is per fragment.\nOther props have one health pool.\nMore properties: Unity Inspector.";
            if (DeleteControl) DeleteControl.SetActive(!BrushProperties && Selected);
            if (WidthField && !WidthField.isFocused) WidthField.SetTextWithoutNotify(WidthTiles.ToString());
            if (HeightField && !HeightField.isFocused) HeightField.SetTextWithoutNotify(HeightTiles.ToString());
            bool surface = BrushProperties ? BrushKind == LevelElementKind.Terrain && LevelEditorCatalog.IsSurface(Brush)
                : Selected && Selected.Kind == LevelElementKind.Terrain && LevelEditorCatalog.IsSurface(Selected.Tile);
            if (SurfacePanel) SurfacePanel.SetActive(surface);
            if (DamagePanel) DamagePanel.SetActive(!surface && !building && !tree && !BrushProperties && Selected && Selected.SupportsDamage);
            if (PropertyHeading) PropertyHeading.text = surface ? "WATER / SURFACE" : tree ? "TREE PROPERTIES" : building ? "BUILDING PROPERTIES" : BrushProperties ? "BRUSH PROPERTIES" : "ELEMENT PROPERTIES";
            if (TileSizeHelp) TileSizeHelp.text = Snap == 32 ? "SMALL: 2 x 2 per large tile" : Snap == 16 ? "FINE: 4 x 4 per large tile" : "LARGE: one full tile";
            if (!SelectionLabel) return;
            if (building)
            {
                var prefab = BuildingPrefab(ActiveBuilding);
                Vector2 size = !BrushProperties && Selected ? Selected.Size : prefab ? new Vector2(prefab.FootprintWidth, prefab.FootprintHeight) : Vector2.zero;
                SelectionLabel.text = LevelEditorCatalog.BuildingLabel(ActiveBuilding) + (BrushProperties ? " BRUSH" : "") + "\n" + size.x + " x " + size.y + " units" + (BrushProperties ? "  /  Paint on the map" : "  /  Position: " + Mathf.RoundToInt(Selected.X) + ", " + Mathf.RoundToInt(Selected.Y));
            }
            else if (tree)
            {
                var prefab = TreePrefab(ActiveTree);
                Vector2 size = !BrushProperties && Selected ? Selected.Size : prefab ? new Vector2(prefab.FootprintWidth, prefab.FootprintHeight) : Vector2.zero;
                SelectionLabel.text = LevelEditorCatalog.TreeLabel(ActiveTree) + (BrushProperties ? " BRUSH" : "") + "  /  " + size.x + " x " + size.y + " units\n" + (BrushProperties ? "Choose a type, then paint on the map." : "Position: " + Mathf.RoundToInt(Selected.X) + ", " + Mathf.RoundToInt(Selected.Y));
            }
            else if (BrushProperties) SelectionLabel.text = Brush.ToUpperInvariant() + " BRUSH\nPaint size: " + Snap + " x " + Snap + " units";
            else if (Selected) SelectionLabel.text = Selected.Tile.ToUpperInvariant() + "  /  " + Selected.Size.x + " x " + Selected.Size.y + " units\nPosition: " + Mathf.RoundToInt(Selected.X) + ", " + Mathf.RoundToInt(Selected.Y);
            else SelectionLabel.text = "Choose an element from the palette\nor select one on the map.";
        }
    }
}
