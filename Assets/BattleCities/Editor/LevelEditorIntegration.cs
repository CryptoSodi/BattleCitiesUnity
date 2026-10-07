using System;
using System.Linq;
using BattleCities.LevelEditor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.EditorTools
{
    [InitializeOnLoad]
    public static class LevelEditorIntegration
    {
        const string Key = LevelEditorPlaytest.Key;
        const string ScenePath = "Assets/BattleCities/Scenes/LevelEditor.unity";
        static int stroke;
        static Vector2 lastCell;
        static LevelEditorIntegration()
        {
            LevelEditorDocument.TestRequested = Test;
            EditorApplication.playModeStateChanged += PlayChanged;
            SceneView.duringSceneGui += SceneGui;
        }
        [MenuItem("Battle Cities/Level Editor/Open editor")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before opening the level editor."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(ScenePath)) LevelEditorSceneFactory.Create();
            else EditorSceneManager.OpenScene(ScenePath);
            var doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>();
            if (doc) { Selection.activeGameObject = doc.gameObject; FrameScene(doc); }
        }
        [MenuItem("Battle Cities/Level Editor/Recover last Play-mode draft")]
        public static void Recover()
        {
            var doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>(); if (!doc) { Open(); doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>(); }
            string file = "Library/BattleCitiesLevelEditorRecovery.json";
            if (doc && System.IO.File.Exists(file)) { doc.Import(System.IO.File.ReadAllText(file)); doc.Status("Recovered the last authoring draft. Save JSON to keep a named copy."); }
        }
        public static void Test(LevelEditorDocument doc)
        {
            if (doc.Validate().Count > 0) { doc.ValidateAction(); return; }
            SessionState.SetString(Key + "Draft", doc.ToJson());
            SessionState.SetInt(Key + "Stage", doc.PreviewStage);
            SessionState.SetString(Key + "SavePath", doc.SavePath);
            SessionState.SetBool(Key + "Launch", true);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else Launch(doc);
        }
        static void Launch(LevelEditorDocument doc)
        {
            if (!doc) return;
            EditorSceneManager.SaveScene(doc.gameObject.scene);
            SessionState.SetString(Key + "OriginScene", doc.gameObject.scene.path);
            SessionState.SetString(Key + "PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/BattleCities/Scenes/BattleCity.unity");
            SessionState.SetBool(Key + "Launch", false); SessionState.SetBool(Key + "Testing", true);
            EditorApplication.isPlaying = true;
        }
        static void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && !SessionState.GetBool(Key + "Testing", false))
            {
                var doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>();
                if (doc) EditorSceneManager.SaveScene(doc.gameObject.scene);
            }
            if (state == PlayModeStateChange.ExitingPlayMode && !SessionState.GetBool(Key + "Testing", false))
            {
                var doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>();
                if (doc)
                {
                    string json = doc.ToJson(); SessionState.SetString(Key + "Restore", json); SessionState.SetString(Key + "SavePath", doc.SavePath);
                    LevelEditorDocument.WriteJson("Library/BattleCitiesLevelEditorRecovery.json", json);
                }
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            bool tested = SessionState.GetBool(Key + "Testing", false);
            if (tested)
            {
                SessionState.SetBool(Key + "Testing", false);
                string previous = SessionState.GetString(Key + "PreviousStart", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            }
            EditorApplication.delayCall += () =>
            {
                if (tested)
                {
                    string origin = SessionState.GetString(Key + "OriginScene", ScenePath);
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != origin) EditorSceneManager.OpenScene(origin);
                }
                var doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>(); if (!doc) return;
                string json = SessionState.GetString(Key + "Restore", "");
                if (!string.IsNullOrEmpty(json))
                {
                    doc.Import(json); var metadata = JObject.Parse(json)["editor"];
                    doc.LevelName = (string)metadata?["name"] ?? doc.LevelName; doc.PreviewStage = (int?)metadata?["previewStage"] ?? doc.PreviewStage;
                    doc.SavePath = SessionState.GetString(Key + "SavePath", ""); SessionState.EraseString(Key + "Restore");
                    EditorSceneManager.SaveScene(doc.gameObject.scene);
                }
                if (SessionState.GetBool(Key + "Launch", false)) Launch(doc);
                else { Selection.activeGameObject = doc.gameObject; doc.Status(tested ? "Returned from local playtest. Your draft is intact." : "Play-mode map edits retained. Save JSON to keep a named draft."); }
            };
        }
        public static void FrameScene(LevelEditorDocument doc)
        {
            var view = SceneView.lastActiveSceneView;
            if (view) { view.in2DMode = false; view.LookAt(new Vector3(doc.WidthTiles * .5f, 0, -doc.HeightTiles * .5f), Quaternion.Euler(90, 0, 0), Mathf.Max(doc.WidthTiles, doc.HeightTiles) * .65f, true, true); }
        }
        static void SceneGui(SceneView view)
        {
            if (EditorApplication.isPlaying) return;
            var doc = UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>(); if (!doc) return;
            var e = Event.current;
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12, 12, 470, 64), GUI.skin.box);
            GUILayout.Label("LEVEL EDITOR   |   " + doc.Tool + " / " + doc.Brush + " / " + doc.Snap + " units");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select")) doc.SetTool("Select"); if (GUILayout.Button("Paint")) doc.SetTool("Paint"); if (GUILayout.Button("Erase")) doc.SetTool("Erase");
            if (GUILayout.Button("Frame")) FrameScene(doc); if (GUILayout.Button("Test Level")) doc.TestLevel();
            GUILayout.EndHorizontal(); GUILayout.EndArea(); Handles.EndGUI();
            if(doc.IsMoving && (e.type == EventType.MouseUp && e.button == 0 || e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
            { doc.EndMove(e.type == EventType.KeyDown); GUIUtility.hotControl = 0; e.Use(); view.Repaint(); return; }
            if (e.alt || new Rect(12, 12, 470, 64).Contains(e.mousePosition)) return;
            int control = GUIUtility.GetControlID(FocusType.Passive); if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition); if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            var world = ray.GetPoint(distance); float x = world.x * 64, y = -world.z * 64;
            if (doc.Tool == "Select")
            {
                if(e.type == EventType.MouseDown && e.button == 0 && x >= 0 && y >= 0 && x < doc.WidthTiles*64 && y < doc.HeightTiles*64)
                { if(doc.BeginMove(new Vector2(x,y)))GUIUtility.hotControl = control; e.Use(); }
                if(e.type == EventType.MouseDrag && e.button == 0 && doc.IsMoving)
                { doc.DragMove(new Vector2(x,y)); e.Use(); view.Repaint(); }
                return;
            }
            var cell = new Vector2(Mathf.Floor(x / doc.Snap), Mathf.Floor(y / doc.Snap));
            Handles.color = Color.cyan; Handles.DrawWireCube(new Vector3((cell.x * doc.Snap + doc.Snap * .5f) / 64, .06f, -(cell.y * doc.Snap + doc.Snap * .5f) / 64), new Vector3(doc.Snap / 64f, .1f, doc.Snap / 64f));
            if (e.type == EventType.MouseDown && e.button == 0)
            { Undo.IncrementCurrentGroup(); stroke = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Paint level stroke"); lastCell = new Vector2(float.NaN, float.NaN); GUIUtility.hotControl = control; }
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            { if (cell != lastCell) doc.ApplyBrush(x, y); lastCell = cell; e.Use(); }
            if (e.type == EventType.MouseUp && e.button == 0) { Undo.CollapseUndoOperations(stroke); GUIUtility.hotControl = 0; e.Use(); }
            view.Repaint();
        }
    }
    [CustomEditor(typeof(LevelEditorDocument))]
    public sealed class LevelEditorDocumentInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var doc = (LevelEditorDocument)target;
            EditorGUILayout.HelpBox("Edit map elements in the Hierarchy and Inspector. Paint in Scene view here, or press Play to use the saved Canvas controls in Game view. Map edits are retained when you stop. Canvas layout edits should be made outside Play mode.", MessageType.Info);
            GUILayout.BeginHorizontal(); if (GUILayout.Button("Maps")) doc.OpenLibrary(); if (GUILayout.Button("Open JSON")) doc.OpenMap(); if (GUILayout.Button("Save JSON")) doc.Save(); if (GUILayout.Button("Validate")) doc.ValidateAction(); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); if (GUILayout.Button("Frame map")) LevelEditorIntegration.FrameScene(doc); if (GUILayout.Button("Test Level")) doc.TestLevel(); GUILayout.EndHorizontal();
            DrawDefaultInspector();
        }
    }
    [CustomEditor(typeof(LevelElement)), CanEditMultipleObjects]
    public sealed class LevelElementInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var element = (LevelElement)target;
            EditorGUILayout.HelpBox(element.SupportsDamage ? "Enable Override Damage to set health, invulnerability and normal/power shot susceptibility. Brick health applies to each 16-unit fragment; steel to each 32-unit fragment; props have one health pool. Turn the override off to restore original game rules." : "This element has no damage collision. Damage overrides are available on brick, steel and bullet-blocking props.", MessageType.Info);
            if (GUILayout.Button("Select in editor")) element.GetComponentInParent<LevelEditorDocument>()?.Select(element);
        }
    }
}
