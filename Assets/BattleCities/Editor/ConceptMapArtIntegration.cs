using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCities.LevelEditor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    /// <summary>Explicit import of reviewed source models. Never runs on scene entry.</summary>
    public static class ConceptMapArtIntegration
    {
        public const string Folder = "Assets/BattleCities/Art/ConceptMapArtV2";
        static readonly Dictionary<string,Material> sharedMaterials = new Dictionary<string,Material>();
        public static void Apply()
        {
            var plan = JObject.Parse(File.ReadAllText(Folder + "/bindings.json"));
            sharedMaterials.Clear();
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
            if (!palette) throw new InvalidOperationException("EnvironmentPalette missing");
            var entries = palette.Prefabs.ToList();
            Directory.CreateDirectory(Folder + "/GameplayPrefabs"); Directory.CreateDirectory(Folder + "/SharedMaterials"); AssetDatabase.Refresh();
            var report = new JArray();
            // Validate every source before replacing any existing visual.
            foreach (JObject row in plan["assets"])
                ValidateModel(row);
            foreach (JObject row in plan["assets"])
            {
                string key = (string)row["key"];
                Replace(row, key, entries, report);
                string alias = (string)row["legacyAlias"];
                if (!string.IsNullOrEmpty(alias)) Replace(row, alias, entries, report);
            }
            Undo.RecordObject(palette, "Replace concept map art");
            palette.Prefabs = entries.ToArray(); EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            File.WriteAllText(Folder + "/unity-import-report.json", report.ToString());
            var doc = UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();
            if (doc && doc.GamePreview) doc.GamePreview.Invalidate();
            Debug.Log("Concept map art integrated: " + report.Count + " prefab bindings. See unity-import-report.json.");
        }
        static GameObject Source(JObject row)
        {
            string path = (string)row["unityModelPath"];
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("No model export bound for " + row["assetId"]);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) throw new InvalidOperationException("Model import failed: " + path);
            return model;
        }
        static void ValidateModel(JObject row)
        {
            var instance = UnityEngine.Object.Instantiate(Source(row));
            try
            {
                instance.transform.position = Vector3.zero;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException(row["assetId"] + " has no renderers");
                float halfWidth = (float)row["width"] / 128, halfDepth = (float)row["height"] / 128;
                foreach (var renderer in renderers)
                {
                    var b = renderer.bounds;
                    if (b.min.x < -halfWidth-.005f || b.max.x > halfWidth+.005f || b.min.z < -halfDepth-.005f || b.max.z > halfDepth+.005f)
                        throw new InvalidOperationException(row["assetId"] + " exceeds its footprint: " + renderer.name + " " + b);
                    if (b.max.y > (float)row["maxHeight"]+.025f)
                        throw new InvalidOperationException(row["assetId"] + " exceeds visual height: " + b.max.y);
                    if (renderer.sharedMaterials.Any(m => !m || !m.shader || !m.shader.isSupported))
                        throw new InvalidOperationException(row["assetId"] + " has a missing or unsupported material");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        static void Replace(JObject row, string key, List<EnvironmentPiece> entries, JArray report)
        {
            var old = entries.FirstOrDefault(p => p && p.Key == key);
            string path = old ? AssetDatabase.GetAssetPath(old) : Folder + "/GameplayPrefabs/" + key + ".prefab";
            GameObject root = old ? PrefabUtility.LoadPrefabContents(path) : new GameObject(key);
            try
            {
                var piece = root.GetComponent<EnvironmentPiece>(); if (!piece) piece = root.AddComponent<EnvironmentPiece>();
                if (old && piece.Role != (string)row["role"]) throw new InvalidOperationException("Role mismatch: " + key);
                if (piece.IntactVisual) UnityEngine.Object.DestroyImmediate(piece.IntactVisual);
                var visual = new GameObject("Concept art v2 - " + (string)row["assetId"]); visual.transform.SetParent(root.transform, false);
                var model = UnityEngine.Object.Instantiate(Source(row), visual.transform, false); model.name = "Authored model";
                if (PrefabUtility.IsPartOfPrefabInstance(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                if ((int?)row["modelYaw"] != null) model.transform.localRotation = Quaternion.Euler(0, (float)row["modelYaw"], 0) * model.transform.localRotation;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var light in model.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
                piece.Key = key; piece.Role = (string)row["role"]; piece.FootprintWidth = (float)row["width"]; piece.FootprintHeight = (float)row["height"];
                piece.IntactVisual = visual;
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(ShareMaterial).ToArray();
                long triangles = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => (long)f.sharedMesh.GetIndexCount(i) / 3));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<EnvironmentPiece>();
                if (!old) entries.Add(saved);
                report.Add(new JObject { ["key"]=key,["assetId"]=row["assetId"],["source"]=row["unityModelPath"],["prefab"]=path,["triangles"]=triangles,["renderers"]=renderers.Length,["materials"]=new JArray(renderers.SelectMany(r=>r.sharedMaterials).Distinct().Select(m=>m.name)),["footprintWidth"]=piece.FootprintWidth,["footprintHeight"]=piece.FootprintHeight });
            }
            finally { if (old) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
        }
        static Material ShareMaterial(Material source)
        {
            // The pack uses a shared atlas per family. Only deduplicate materials
            // after comparing serialized values and texture content identities.
            string signature = source.shader.name + "|" + source.renderQueue + "|" + source.doubleSidedGI;
            for (int i=0;i<source.shader.GetPropertyCount();i++)
            {
                string property=source.shader.GetPropertyName(i);
                switch(source.shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color: signature += "|"+property+source.GetColor(property).ToString("R"); break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector: signature += "|"+property+source.GetVector(property).ToString("R"); break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range: signature += "|"+property+source.GetFloat(property).ToString("R",System.Globalization.CultureInfo.InvariantCulture); break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        var texture=source.GetTexture(property);signature += "|"+property+(texture?texture.imageContentsHash.ToString()+texture.width+"x"+texture.height+texture.wrapMode+texture.filterMode:"none")+source.GetTextureScale(property).ToString("R")+source.GetTextureOffset(property).ToString("R");break;
                }
            }
            signature += "|"+string.Join(",",source.shaderKeywords.OrderBy(k=>k));
            string hash=Hash128.Compute(signature).ToString();
            if(sharedMaterials.TryGetValue(hash,out var shared)&&shared)return shared;
            string path=Folder+"/SharedMaterials/"+source.name.Replace('/','_').Replace('\\','_')+"-"+hash.Substring(0,12)+".mat";
            shared=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!shared){shared=new Material(source){name=source.name,enableInstancing=true};AssetDatabase.CreateAsset(shared,path);}
            sharedMaterials.Add(hash,shared);return shared;
        }
        public static int UpgradeDocument(LevelEditorDocument doc)
        {
            if (!doc) return 0;
            var plan = JObject.Parse(File.ReadAllText(Folder + "/bindings.json"));
            var imported = JObject.Parse(doc.ImportedJson);
            string slug = null;
            foreach (var group in ((JObject)plan["buildingAssignments"]).Properties())
            {
                var template = JObject.Parse(File.ReadAllText(LevelEditorMapLibrary.Folder + "/" + group.Name + ".json"));
                if (imported["concept"]?["placements"] != null && JToken.DeepEquals(imported["concept"]["placements"],template["concept"]["placements"])) { slug=group.Name; break; }
            }
            if (slug == null) return 0;
            int changes=0;
            foreach(var element in doc.Elements.Where(e=>e.Kind==LevelElementKind.Prop && (e.Tile=="draft_building_blue" || e.Tile=="draft_building_coral")))
            {
                string assetId=(string)plan["buildingAssignments"][slug][element.SourceId??""];
                if (assetId==null)continue;
                var asset=plan["assets"].OfType<JObject>().Single(a=>(string)a["assetId"]==assetId);
                // Custom footprints or custom asset choices belong to the user.
                if(element.Size.x!=(float)asset["width"] || element.Size.y!=(float)asset["height"])continue;
                Undo.RecordObject(element,"Upgrade map building art");Undo.RecordObject(element.gameObject,"Name map building art");
                element.Tile=(string)asset["key"];element.name=element.SourceId+" - "+element.Tile;changes++;
            }
            if(changes>0){doc.Dirty();doc.RefreshSelection();}
            return changes;
        }
    }
}
