using BattleCities.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.LevelEditor
{
    [ExecuteAlways]
    public sealed class LevelEditorButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public LevelEditorDocument Document;
        public string BrushKey, ToolKey, SurfaceKey, TreeKey, BuildingKey, BuildingPageKey;
        public int SizeKey;
        public UnityEngine.UI.Image Background;
        public TMP_Text Caption;
        public Sprite Blue, Gold;
        public bool Flat, Thumbnail;
        public UnityEngine.UI.Outline Border;
        private bool hover, focus;
        private void Update() => Refresh();
        public void Refresh()
        {
            bool active = Document && ((!string.IsNullOrEmpty(BuildingKey) && BuildingKey == Document.ActiveBuilding) || (!string.IsNullOrEmpty(BuildingPageKey) && BuildingPageKey == Document.BuildingPage) || (BrushKey == "Prop:" + LevelEditorCatalog.DefaultBuilding && Document.Tool == "Paint" && Document.BrushKind == LevelElementKind.Prop && LevelEditorCatalog.IsBuilding(Document.Brush)) || (!string.IsNullOrEmpty(TreeKey) && TreeKey == Document.ActiveTree) || (BrushKey == "Prop:forest_tree_a" && Document.Tool == "Paint" && Document.BrushKind == LevelElementKind.Prop && LevelEditorCatalog.IsTree(Document.Brush)) || (!string.IsNullOrEmpty(SurfaceKey) && SurfaceKey == Document.ActiveSurface) || (SizeKey > 0 && SizeKey == Document.Snap) || (BrushKey == "Terrain:water" && Document.Tool == "Paint" && LevelEditorCatalog.IsSurface(Document.Brush)) || (!string.IsNullOrEmpty(BrushKey) && Document.Tool == "Paint" && BrushKey == Document.BrushKind + ":" + Document.Brush) || (!string.IsNullOrEmpty(ToolKey) && Document.Tool == ToolKey));
            if (Flat)
            {
                if (Background) { Background.sprite = null; Background.color = active ? new Color32(239,189,67,255) : Thumbnail ? new Color32(226,225,211,255) : new Color32(30,65,90,255); }
                if (Border) Border.effectColor = active ? new Color32(179,127,30,255) : hover || focus ? new Color32(71,196,226,255) : Thumbnail ? new Color32(170,181,179,255) : new Color32(64,100,125,255);
                if (Caption) Caption.color = active || Thumbnail ? ArcadeTextStyles.PrizeAmountFace : Color.white;
                return;
            }
            if (Background) { Background.sprite = active ? Gold : Blue; Background.color = !active && (hover || focus) ? new Color(.65f, .93f, 1) : Color.white; }
            if (Caption) Caption.color = active ? ArcadeTextStyles.PrizeAmountFace : Color.white;
        }
        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;
        public void OnSelect(BaseEventData e) => focus = true;
        public void OnDeselect(BaseEventData e) => focus = false;
    }
}
