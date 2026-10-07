using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BattleCities.LevelEditor
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LevelEditorGamePreview : MonoBehaviour
    {
        public LevelEditorDocument Document;
        [NonSerialized] GameObject visualRoot;
        [NonSerialized] readonly List<Material> materials = new List<Material>();
        [NonSerialized] readonly List<Mesh> meshes = new List<Mesh>();
        [NonSerialized] BattleWaterSurface[] surfaces = Array.Empty<BattleWaterSurface>();
        [NonSerialized] string previous;
        [NonSerialized] bool dirty = true;
        [NonSerialized] double nextCheck;
        public void Invalidate() => dirty = true;
        void OnEnable() { previous = null; dirty = true; }
        void OnDisable() => Clear();
        string Signature()
        {
            var key = new StringBuilder().Append(Document.WidthTiles).Append('|').Append(Document.HeightTiles).Append('|').Append(Document.PreviewStage);
            foreach (var e in Document.Elements)
                key.Append('|').Append(e.GetEntityId().ToString()).Append(':').Append(e.gameObject.activeSelf).Append(':').Append(e.Kind).Append(':').Append(e.Tile).Append(':').Append(e.transform.localPosition).Append(':').Append(e.Size).Append(':').Append(e.Rotation).Append(':').Append(e.PropRole).Append(':').Append(e.BridgeSurface).Append(':').Append(e.LightColor).Append(':').Append(e.LightHeight).Append(':').Append(e.LightRange).Append(':').Append(e.LightIntensity);
            return key.ToString();
        }
        void Update()
        {
#if UNITY_EDITOR
            if (!Document || !Document.BuildPreviews || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup >= nextCheck)
            {
                nextCheck = EditorApplication.timeSinceStartup + .12;
                var signature = Signature();
                if (dirty || previous != signature || !visualRoot) { Rebuild(); previous = signature; }
            }
            if (Application.isPlaying) foreach (var surface in surfaces) if (surface) surface.Tick(Mathf.Min(Time.unscaledDeltaTime,.1f));
#endif
        }
        public void Rebuild()
        {
#if UNITY_EDITOR
            if (!Document || !Document.BuildPreviews || !Document.BrickSectionData) return;
            Clear(); dirty = false;
            visualRoot = new GameObject("Game appearance (preview)") { hideFlags = HideFlags.DontSave };
            visualRoot.transform.SetParent(transform,false);
            try
            {
                BattleGame.BuildEditorMap(visualRoot.transform,Document,materials,meshes);
                foreach(var child in visualRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.DontSave;
                foreach(var collider in visualRoot.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
                surfaces = visualRoot.GetComponentsInChildren<BattleWaterSurface>();
                if(Document.PreviewCamera)Document.PreviewCamera.Render();
            }
            catch(Exception error) { Clear(); Debug.LogException(error,this); Document.Status("Preview: " + error.Message); }
#endif
        }
        void Clear()
        {
#if UNITY_EDITOR
            if (visualRoot) DestroyImmediate(visualRoot); visualRoot=null;
            foreach(var material in materials)if(material)DestroyImmediate(material);
            foreach(var mesh in meshes)if(mesh)DestroyImmediate(mesh);
            materials.Clear();meshes.Clear();surfaces=Array.Empty<BattleWaterSurface>();
#endif
        }
    }
}
