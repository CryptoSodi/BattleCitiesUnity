using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class StageZeroBuilder
    {
        private const string Root = "Assets/BattleCities/";
        private const string StageOneScene = Root + "Scenes/BattleCity.unity";
        private const string StageZeroScene = Root + "Scenes/Stage00.unity";

        private sealed class Entry
        {
            public string Key, Role, Pack, Model, Broken;
            public float Width, Height, ModelHeight;
            public Entry(string key, string role, string pack, string model, float width, float height, float modelHeight, string broken = null)
            { Key = key; Role = role; Pack = pack; Model = model; Width = width; Height = height; ModelHeight = modelHeight; Broken = broken; }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("city_building_a", "solidCover", "City", "building_A_withoutBase", 64, 64, .82f),
            new Entry("city_building_e", "solidCover", "City", "building_E_withoutBase", 64, 64, .86f),
            new Entry("city_car", "solidCover", "City", "car_sedan", 12, 21, .20f),
            new Entry("city_crate", "destructibleObstacle", "City", "box_A", 21, 21, .19f, "trash_A"),
            new Entry("city_dumpster", "destructibleObstacle", "City", "dumpster", 21, 21, .22f, "trash_B"),
            new Entry("city_bench", "solidCover", "City", "bench", 21, 21, .15f),
            new Entry("city_road_junction", "groundDetail", "City", "road_junction", 64, 64, .02f),
            new Entry("forest_tree_a", "solidCover", "Forest", "Tree_1_A_Color1", 21, 21, .55f),
            new Entry("forest_tree_b", "solidCover", "Forest", "Tree_2_B_Color1", 21, 21, .55f),
            new Entry("forest_rock", "solidCover", "Forest", "Rock_1_A_Color1", 21, 21, .18f),
            new Entry("forest_bush", "passableCover", "Forest", "Bush_1_B_Color1", 21, 21, .15f),
            new Entry("forest_grass", "groundDetail", "Forest", "Grass_1_A_Color1", 21, 21, .06f)
        };

        [MenuItem("Battle Cities/Build experimental Stage 0 assets and scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building Stage 0");
            AssetDatabase.Refresh();
            var city = MaterialFor("City", "citybits_texture.png");
            var forest = MaterialFor("Forest", "forest_texture.png");
            var prefabs = Entries.Select(entry => MakePrefab(entry, entry.Pack == "City" ? city : forest)).ToArray();
            string palettePath = Root + "Resources/EnvironmentPalette.asset";
            var palette = AssetDatabase.LoadAssetAtPath<EnvironmentPalette>(palettePath);
            if (!palette)
            {
                if (File.Exists(palettePath)) AssetDatabase.DeleteAsset(palettePath);
                palette = ScriptableObject.CreateInstance<EnvironmentPalette>();
                AssetDatabase.CreateAsset(palette, palettePath);
            }
            palette.Prefabs = prefabs;
            EditorUtility.SetDirty(palette);

            if (!File.Exists(StageZeroScene) && !AssetDatabase.CopyAsset(StageOneScene, StageZeroScene))
                throw new IOException("Could not copy the Stage 1 scene for Stage 0");
            var scene = EditorSceneManager.OpenScene(StageZeroScene, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            if (!game) throw new InvalidOperationException("Stage 0 scene has no BattleGame");
            game.Stage = 0;
            game.EnemyFire = true;
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(item => item.path == StageZeroScene)) scenes.Add(new EditorBuildSettingsScene(StageZeroScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BattleCities] Stage 0 palette, 12 gameplay prefabs, and separate scene built. Stage 1 scene was not edited.");
        }

        private static Material MaterialFor(string pack, string textureName)
        {
            string basePath = Root + "Art/Kitbash/" + pack + "/";
            string path = basePath + "Materials/" + pack + "Atlas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = pack + " Atlas";
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "Textures/" + textureName);
            if (!texture) throw new InvalidOperationException("Missing " + pack + " texture");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static EnvironmentPiece MakePrefab(Entry entry, Material material)
        {
            string basePath = Root + "Art/Kitbash/" + entry.Pack + "/";
            var root = new GameObject(entry.Key);
            try
            {
                var piece = root.AddComponent<EnvironmentPiece>();
                piece.Key = entry.Key; piece.Role = entry.Role;
                piece.FootprintWidth = entry.Width; piece.FootprintHeight = entry.Height;
                piece.IntactVisual = AddModel(root, "Intact", basePath + "SourceModels/" + entry.Model + ".fbx",
                    material, entry.Width / 64f, entry.Height / 64f, entry.ModelHeight);
                if (entry.Broken != null)
                {
                    piece.DestroyedVisual = AddModel(root, "Destroyed", basePath + "SourceModels/" + entry.Broken + ".fbx",
                        material, entry.Width / 64f * .7f, entry.Height / 64f * .7f, .18f);
                    piece.DestroyedVisual.SetActive(false);
                }
                string path = basePath + "GameplayPrefabs/" + entry.Key + ".prefab";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!saved) throw new InvalidOperationException("Could not create prefab " + path);
                return saved.GetComponent<EnvironmentPiece>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject AddModel(GameObject root, string label, string assetPath, Material material,
            float footprintX, float footprintZ, float maxHeight)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (!source) throw new InvalidOperationException("Missing imported FBX: " + assetPath);
            var holder = new GameObject(label);
            holder.transform.SetParent(root.transform, false);
            var model = PrefabUtility.InstantiatePrefab(source, holder.transform) as GameObject;
            if (!model) throw new InvalidOperationException("Could not instantiate " + assetPath);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("FBX has no renderers: " + assetPath);
            foreach (var renderer in renderers)
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            var bounds = BoundsOf(renderers);
            if (bounds.size.x <= .001f || bounds.size.y <= .001f || bounds.size.z <= .001f)
                throw new InvalidOperationException("FBX has zero-size bounds: " + assetPath);
            var scale = model.transform.localScale;
            model.transform.localScale = new Vector3(scale.x * footprintX * .92f / bounds.size.x,
                scale.y * maxHeight / bounds.size.y,
                scale.z * footprintZ * .92f / bounds.size.z);
            bounds = BoundsOf(renderers);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return holder;
        }

        private static Bounds BoundsOf(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
