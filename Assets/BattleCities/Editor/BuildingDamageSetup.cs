using System.IO;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class BuildingDamageSetup
    {
        public static void Apply()
        {
            foreach(var prefab in Resources.Load<EnvironmentPalette>("EnvironmentPalette").Prefabs.Where(p=>BattleBuildings.IsBuilding(p.Key)))
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh||filter.sharedMesh.isReadable)continue;
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(filter.sharedMesh)) as ModelImporter;
                if(!importer)throw new System.InvalidOperationException("Cannot enable readable building mesh: "+prefab.Key);
                importer.isReadable=true;importer.SaveAndReimport();
            }
            Directory.CreateDirectory("Library/BuildingDamageBackups");
            foreach(var slug in LevelEditorMapLibrary.Slugs)
            {
                var path=LevelEditorMapLibrary.Folder+"/"+slug+".json";
                var text=File.ReadAllText(path);var json=JObject.Parse(text);
                var backup="Library/BuildingDamageBackups/"+slug+".json";if(!File.Exists(backup))File.WriteAllText(backup,text);
                foreach(JObject prop in json["objects"])
                {
                    if(!BattleBuildings.IsBuilding((string)prop["type"]))continue;
                    var rule=prop["damage"] as JObject??JObject.FromObject(BattleBuildings.DefaultDamage());
                    if((int?)rule["hitPoints"]<=1)rule["hitPoints"]=BattleBuildings.DefaultHealth;
                    rule["invulnerable"]=false;prop["damage"]=rule;
                    if(prop["building"]==null||prop["building"].Type==JTokenType.Null)prop["building"]=JObject.FromObject(new MapBuildingSettings());
                }
                foreach(JObject e in json["editor"]["elements"])
                    if((string)e["kind"]=="Prop"&&BattleBuildings.IsBuilding((string)json["objects"][(int)e["index"]]["type"]))e["notes"]=Note;
                json["editor"]["buildingDamageVersion"]=1;
                File.WriteAllText(path,json.ToString());
            }
            var doc=Object.FindAnyObjectByType<LevelEditorDocument>();
            if(doc)
            {
                File.WriteAllText("Library/BuildingDamageBackups/current-authoring-before-migration.json",doc.ToJson());
                foreach(var e in doc.Elements.Where(e=>e.IsBuilding))
                {
                    Undo.RecordObject(e,"Enable building section damage and lights");
                    e.OverrideDamage=true;e.Damage.invulnerable=false;
                    if(e.Damage.hitPoints<=1)e.Damage.hitPoints=BattleBuildings.DefaultHealth;
                    if(!e.OverrideBuildingLighting)e.BuildingLighting=new MapBuildingSettings();
                    e.OverrideBuildingLighting=true;e.DesignNotes=Note;
                }
                doc.Dirty();doc.RefreshSelection();if(doc.GamePreview)doc.GamePreview.Invalidate();
                EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);EditorSceneManager.SaveScene(doc.gameObject.scene);
            }
            AssetDatabase.Refresh();
            Debug.Log("Buildings enabled: damage per 32-unit section and warm window lights. Current draft preserved.");
        }
        const string Note="Destructible building: HP is per 32-unit section. Normal and power-shot damage affect the struck area; removed sections open passages. Warm window lights dim with damage and go out with their section. Lighting overrides are in the Inspector.";
    }
}
