#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.LevelEditor
{
    /// <summary>Explicit additive scene migration; never runs on scene entry.</summary>
    public static class LevelEditorBuildingLayout
    {
        [MenuItem("Battle Cities/Level Editor/Add building family controls")]
        public static void ApplyCurrent()
        {
            var doc = UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();
            if (!doc || EditorApplication.isPlaying) throw new InvalidOperationException("Open LevelEditor outside Play mode.");
            Apply(doc); EditorSceneManager.SaveScene(doc.gameObject.scene);
        }
        public static void Apply(LevelEditorDocument doc)
        {
            string before = doc.ToJson();
            var canvas = doc.gameObject.scene.GetRootGameObjects().First(g => g.name == "EditorCanvas");
            var content = (RectTransform)canvas.transform.Find("Workspace/ElementPalette/ScrollView/Viewport/Content");
            if (!content || !doc.SurfacePanel || !doc.DamagePanel) throw new InvalidOperationException("Existing palette and property controls required.");
            EnsurePaletteItem(doc, content);
            var parent = doc.SurfacePanel.transform.parent;
            var panel = Rect(parent, "BuildingProperties", 18, 330, 274, 240);
            var view = panel.GetComponent<LevelEditorBuildingPanel>();
            if (!view) view = Undo.AddComponent<LevelEditorBuildingPanel>(panel.gameObject);
            view.Document = doc;
            var types = Rect(panel, "Types", 0, 40, 274, 200); view.Types = types.gameObject;
            var source = doc.SurfacePanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < 2; i++)
            {
                string page = i == 0 ? "TYPE" : "DAMAGE";
                var button = Button(source, panel, page, page, i * 142, 0, 32, 20);
                UnityEventTools.AddStringPersistentListener(button.onClick, doc.SetBuildingPage, page);
                button.GetComponent<LevelEditorButton>().BuildingPageKey = page;
                if (i == 1) view.DamageTab = button.gameObject;
            }
            for (int i = 0; i < LevelEditorCatalog.BuildingTypes.Length; i++)
            {
                string key = LevelEditorCatalog.BuildingTypes[i];
                var button = Button(source, types, key, LevelEditorCatalog.BuildingLabels[i], i % 2 * 142, i / 2 * 34, 29, 18);
                UnityEventTools.AddStringPersistentListener(button.onClick, doc.SetBuildingType, key);
                button.GetComponent<LevelEditorButton>().BuildingKey = key;
            }
            var damage = panel.Find("DamageControls");
            if (!damage)
            {
                var clone = UnityEngine.Object.Instantiate(doc.DamagePanel, panel); clone.name = "DamageControls";
                Undo.RegisterCreatedObjectUndo(clone, "Add building damage controls"); damage = clone.transform;
            }
            Place((RectTransform)damage, 0, 40, 274, 200); view.Damage = damage.gameObject;
            view.Override = damage.Find("OverrideDamage").GetComponent<UnityEngine.UI.Toggle>();
            view.Health = damage.Find("Health").GetComponent<TMP_InputField>();
            view.Invulnerable = damage.Find("Invulnerable").GetComponent<UnityEngine.UI.Toggle>();
            view.NormalShots = damage.Find("NormalShots").GetComponent<UnityEngine.UI.Toggle>();
            view.PowerShots = damage.Find("PowerShots").GetComponent<UnityEngine.UI.Toggle>();
            Place((RectTransform)view.Override.transform, 0, 0, 274, 30);
            Place((RectTransform)damage.Find("HealthLabel"), 0, 34, 170, 30);
            Place((RectTransform)view.Health.transform, 182, 34, 92, 30);
            Place((RectTransform)view.Invulnerable.transform, 0, 68, 274, 30);
            Place((RectTransform)view.NormalShots.transform, 0, 102, 274, 30);
            Place((RectTransform)view.PowerShots.transform, 0, 136, 274, 30);
            var help = damage.Find("Help");
            if (!help) { var clone = UnityEngine.Object.Instantiate(doc.DamageHelp, damage); clone.name = "Help"; help = clone.transform; Undo.RegisterCreatedObjectUndo(clone.gameObject, "Add building damage help"); }
            var text = help.GetComponent<TMP_Text>(); text.text = "HP per small building section.\nLights: Unity Inspector.";
            text.fontSizeMax = 16; text.fontSizeMin = 15; Place(text.rectTransform, 0, 170, 274, 30); help.gameObject.SetActive(true);
            Undo.RecordObject(doc, "Bind building controls"); doc.BuildingPanel = view;
            foreach (var t in panel.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            doc.RefreshSelection(); UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            EditorUtility.SetDirty(doc); EditorUtility.SetDirty(view); EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);
            if (before != doc.ToJson()) throw new InvalidOperationException("Building controls altered the draft.");
        }
        internal static void EnsurePaletteItem(LevelEditorDocument doc, RectTransform content)
        {
            string id = "Prop:" + LevelEditorCatalog.DefaultBuilding;
            if (content.GetComponentsInChildren<LevelEditorButton>(true).Any(b => b.BrushKey == id)) return;
            var source = content.GetComponentsInChildren<LevelEditorButton>(true).First(b => b.BrushKey == "Terrain:jungle");
            var card = UnityEngine.Object.Instantiate(source, source.transform.parent); card.name = "Building";
            Undo.RegisterCreatedObjectUndo(card.gameObject, "Add building palette item");
            card.BrushKey = id; card.SurfaceKey = card.TreeKey = card.ToolKey = card.BuildingKey = card.BuildingPageKey = ""; card.SizeKey = 0; card.Document = doc;
            card.Caption.text = "BUILDING";
            var button = card.GetComponent<UnityEngine.UI.Button>(); button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEventTools.AddStringPersistentListener(button.onClick, doc.ChooseBrush, id);
            var art = card.transform.Find("Element");
            if (art) art.GetComponent<UnityEngine.UI.Image>().sprite = LevelEditorPaletteAppearance.Thumbnail(doc, LevelEditorCatalog.DefaultBuilding, false);
            card.Refresh();
        }
        static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var found = parent.Find(name);
            if (!found) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(go, "Add building controls"); found = go.transform; }
            var rect = (RectTransform)found; Place(rect, x, y, w, h); return rect;
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h); }
        static UnityEngine.UI.Button Button(UnityEngine.UI.Button source, Transform parent, string name, string label, float x, float y, float h, float font)
        {
            var found = parent.Find(name); var button = found ? found.GetComponent<UnityEngine.UI.Button>() : UnityEngine.Object.Instantiate(source, parent);
            if (!found) Undo.RegisterCreatedObjectUndo(button.gameObject, "Add building choice"); button.name = name;
            Place((RectTransform)button.transform, x, y, 132, h);
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            var visual = button.GetComponent<LevelEditorButton>();
            visual.BrushKey = visual.SurfaceKey = visual.TreeKey = visual.ToolKey = visual.BuildingKey = visual.BuildingPageKey = ""; visual.SizeKey = 0;
            visual.Caption.text = label; visual.Caption.fontSizeMax = font; visual.Caption.fontSizeMin = font - 2;
            Place(visual.Caption.rectTransform, 4, 2, 124, h - 4); visual.Refresh(); return button;
        }
    }
}
#endif
