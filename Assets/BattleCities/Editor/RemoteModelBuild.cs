using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BattleCities.Editor
{
    public sealed class RemoteModelBuild : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;
        static readonly string[] Fields = { "TankModels", "SpeedEnemyModel", "BrickModel", "SteelModel", "BushModel", "EagleModel", "RuinedEagleModel", "MineModel", "DroneModel", "GroundTurretPrefab", "BulletModels", "BrickSectionData", "GroundTextures", "PowerupAtlas" };
        public static void Capture(string path)
        {
            var scene = EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/BattleCity.unity", OpenSceneMode.Additive);
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleGame>(true)).Single();
                var models = ScriptableObject.CreateInstance<RemoteBattleModels>();
                foreach (var field in Fields)
                    typeof(RemoteBattleModels).GetField(field).SetValue(models, typeof(BattleGame).GetField(field).GetValue(game));
                if (models.TankModels == null || models.TankModels.Length == 0 || models.TankModels.Any(x => !x) || !models.BrickModel || !models.EagleModel)
                    throw new BuildFailedException("Battle model catalog is incomplete.");
                AssetDatabase.CreateAsset(models, path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !RemoteGameArtBuild.Active) return;
            foreach (var game in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleGame>(true)))
            {
                // Only the build's scene copy is changed; authored scenes retain their references.
                var serialized = new SerializedObject(game);
                foreach (var field in Fields)
                {
                    var property = serialized.FindProperty(field);
                    if (property.isArray) property.arraySize = 0;
                    else property.objectReferenceValue = null;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
