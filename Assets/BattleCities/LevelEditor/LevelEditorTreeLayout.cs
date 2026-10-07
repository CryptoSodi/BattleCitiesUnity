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
    /// <summary>Explicit, additive migration of the authored editor controls.</summary>
    public static class LevelEditorTreeLayout
    {
        [MenuItem("Battle Cities/Level Editor/Add tree family controls")]
        public static void ApplyCurrent()
        {
            var doc = UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();
            if (!doc || EditorApplication.isPlaying) throw new InvalidOperationException("Open the LevelEditor scene outside Play mode.");
            Apply(doc); EditorSceneManager.SaveScene(doc.gameObject.scene);
        }

        public static void Apply(LevelEditorDocument doc)
        {
            string before = doc.ToJson();
            var canvas = doc.gameObject.scene.GetRootGameObjects().First(g => g.name == "EditorCanvas");
            var content = (RectTransform)canvas.transform.Find("Workspace/ElementPalette/ScrollView/Viewport/Content");
            if (!content || !doc.SurfacePanel) throw new InvalidOperationException("Existing palette and Water controls are required.");
            EnsurePaletteItem(doc, content);
            var grid = content.Find("TerrainItems");
            Undo.RecordObject(grid.GetComponent<UnityEngine.UI.LayoutElement>(), "Make room for tree card");
            grid.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 292;
            var panel = doc.SurfacePanel.transform.parent.Find("TreeProperties");
            if (!panel)
            {
                var clone = UnityEngine.Object.Instantiate(doc.SurfacePanel, doc.SurfacePanel.transform.parent);
                clone.name = "TreeProperties"; panel = clone.transform;
                Undo.RegisterCreatedObjectUndo(clone, "Add tree type controls");
                foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true)) Undo.DestroyObjectImmediate(button.gameObject);
            }
            Undo.RecordObject(doc, "Bind tree type controls"); doc.TreePanel = panel.gameObject;
            ((RectTransform)panel).sizeDelta = new Vector2(274, 240);
            panel.Find("TypeHeading").GetComponent<TMP_Text>().text = "TREE TYPE";
            var source = doc.SurfacePanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < LevelEditorCatalog.TreeTypes.Length; i++)
            {
                string key = LevelEditorCatalog.TreeTypes[i];
                var found = panel.Find(key);
                var button = found ? found.GetComponent<UnityEngine.UI.Button>() : UnityEngine.Object.Instantiate(source, panel);
                if (!found) Undo.RegisterCreatedObjectUndo(button.gameObject, "Add tree type");
                button.name = key;
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(i % 2 * 142, -(36 + i / 2 * 48)); rect.sizeDelta = new Vector2(132, 40);
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                UnityEventTools.AddStringPersistentListener(button.onClick, doc.SetTreeType, key);
                var visual = button.GetComponent<LevelEditorButton>(); visual.Document = doc;
                visual.SurfaceKey = visual.BrushKey = visual.ToolKey = ""; visual.SizeKey = 0; visual.TreeKey = key;
                visual.Caption.text = LevelEditorCatalog.TreeLabels[i]; visual.Refresh();
            }
            var help = panel.Find("Help").GetComponent<TMP_Text>();
            help.text = "Changes the selected tree or the brush.\nDamage settings: Unity Inspector.";
            help.rectTransform.anchoredPosition = new Vector2(0, -180); help.rectTransform.sizeDelta = new Vector2(274, 56);
            foreach (var t in panel.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            doc.RefreshSelection(); EditorUtility.SetDirty(doc); EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);
            if (before != doc.ToJson()) throw new InvalidOperationException("Tree controls changed the map document.");
        }

        internal static void EnsurePaletteItem(LevelEditorDocument doc, RectTransform content)
        {
            const string id = "Prop:forest_tree_a";
            var card = content.GetComponentsInChildren<LevelEditorButton>(true).FirstOrDefault(b => b.BrushKey == id);
            if (card) return;
            var original = content.GetComponentsInChildren<LevelEditorButton>(true).First(b => b.BrushKey == "Terrain:jungle");
            card = UnityEngine.Object.Instantiate(original, original.transform.parent);
            card.name = "Tree"; Undo.RegisterCreatedObjectUndo(card.gameObject, "Add tree palette item");
            card.BrushKey = id; card.SurfaceKey = card.TreeKey = card.ToolKey = ""; card.SizeKey = 0; card.Document = doc;
            card.Caption.text = "TREE";
            var button = card.GetComponent<UnityEngine.UI.Button>(); button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            UnityEventTools.AddStringPersistentListener(button.onClick, doc.ChooseBrush, id);
            // Older scene factories have plain cards; full palette styling adds artwork afterward.
            var art = card.transform.Find("Element");
            if (art) art.GetComponent<UnityEngine.UI.Image>().sprite = LevelEditorPaletteAppearance.Thumbnail(doc, "forest_tree_a", false);
            card.Refresh();
        }
    }
}
#endif
