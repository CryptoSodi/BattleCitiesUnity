using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BattleCities.Editor
{
    public static class RemoteModelChecks
    {
        public static void Run()
        {
            RemoteGameArtChecks.Run();
            const string path = "Assets/BattleCities/Editor/RemoteModelCheckTemporary.asset";
            const string scenePath = "Assets/BattleCities/Scenes/BattleCity.unity";
            if (File.Exists(path)) throw new Exception("Preserve existing temporary model catalog before testing.");
            var before = File.ReadAllBytes(scenePath);
            var root = new GameObject("Model binding check"); root.SetActive(false);
            int count = 0;
            void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
            try
            {
                RemoteModelBuild.Capture(path);
                var catalog = AssetDatabase.LoadAssetAtPath<RemoteBattleModels>(path);
                var game = root.AddComponent<BattleGame>(); catalog.Apply(game);
                foreach (var field in typeof(RemoteBattleModels).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    Check(Equals(field.GetValue(catalog), typeof(BattleGame).GetField(field.Name).GetValue(game)), "Model binding " + field.Name);
                Check(catalog.TankModels.Length >= 4 && catalog.TankModels.All(model => model && model.GetComponentsInChildren<MeshFilter>(true).Any()), "Tank meshes present");
                Check(catalog.BrickModel && catalog.SteelModel && catalog.BushModel && catalog.EagleModel && catalog.RuinedEagleModel, "Terrain and base models present");
                Check(RemoteGameArt.Load<GameObject>("TvReplayRig") != null, "Local replay fallback");
                Check(RemoteGameArt.Load<GameObject>("Deployables/LandAttackDrone") != null, "Local drone fallback");
                Check(RemoteGameArt.Load<EnvironmentPalette>("EnvironmentPalette").Prefabs.All(x => x), "Environment prefabs present");
                Check(RemoteGameArt.Load<Material>("RuntimeShaders/Terrain/BushLeaves_PBR") != null, "Local terrain material fallback");
                Check(before.SequenceEqual(File.ReadAllBytes(scenePath)), "Authored battle scene unchanged");
                File.WriteAllText("Builds/RemoteArtChecks/models.txt", "PASS " + count);
                Debug.Log("REMOTE MODEL CHECKS PASSED: " + count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
