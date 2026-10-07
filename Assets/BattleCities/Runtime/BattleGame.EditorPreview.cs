#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        bool authoringPreview;

        // Calls the same ground, shoreline, region-fragment and prop builders as gameplay.
        // The inactive carrier never runs Awake, input, economy, combat or the HUD.
        public static void BuildEditorMap(Transform root, LevelEditorDocument document, List<Material> materials, List<Mesh> meshes)
        {
            var carrier = new GameObject("Editor geometry builder") { hideFlags = HideFlags.HideAndDontSave };
            carrier.SetActive(false);
            var builder = carrier.AddComponent<BattleGame>(); builder.authoringPreview = true;
            try
            {
                var map = document.Export();
                builder.Stage = Mathf.Clamp(document.PreviewStage, 1, 35);
                builder.Simulation = new BattleSimulation(map, builder.Stage);
                if (map.@base == null)
                    builder.Simulation.Terrain.RemoveAll(w => w.Brick && !map.terrain.regions.Any(r => r.type.Contains("brick") && r.x <= w.Bounds.X && r.y <= w.Bounds.Y && r.x+r.width >= w.Bounds.Right && r.y+r.height >= w.Bounds.Bottom));
                builder.stageRoot = root;
                builder.BrickModel = document.BrickModel; builder.SteelModel = document.SteelModel; builder.BushModel = document.BushModel;
                builder.BrickSectionData = document.BrickSectionData; builder.GroundTextures = document.GroundTextures;
                builder.street = builder.Material(new Color(.24f,.28f,.29f));
                builder.pavement = builder.Material(new Color(.58f,.58f,.51f));
                builder.paint = builder.Material(new Color(.82f,.81f,.65f));
                builder.water = Resources.Load<BattleVisualAssets>("BattleVisualAssets").WaterMaterial;
                builder.BuildParts(); builder.BuildGround(map); builder.BuildEnvironment(map); builder.BuildStageLights(map); builder.RebuildTerrain();
                foreach (var batch in builder.batches)
                {
                    var mesh = new Mesh { name = "Gameplay terrain fragments", indexFormat = IndexFormat.UInt32 };
                    meshes.Add(mesh);
                    mesh.CombineMeshes(batch.Matrices.Select(matrix => new CombineInstance { mesh = batch.Part.Mesh, subMeshIndex = batch.Part.Submesh, transform = matrix }).ToArray(), true, true);
                    var go = new GameObject("Terrain fragments"); go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = batch.Part.Material;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                if (map.@base != null)
                    Instantiate(document.EagleModel, World(builder.Simulation.BaseBounds.X+32,builder.Simulation.BaseBounds.Y+32,.1f),Quaternion.Euler(0,180,0),root).name = "Eagle base";
                foreach (var element in document.Elements.Where(e => e.gameObject.activeSelf && (e.Kind == LevelElementKind.PlayerSpawn || e.Kind == LevelElementKind.EnemySpawn)))
                {
                    var tank = Instantiate(document.TankModel,World(element.X+32,element.Y+32),Quaternion.Euler(0,element.Kind == LevelElementKind.PlayerSpawn ? 0 : 180,0),root);
                    tank.name = element.Kind == LevelElementKind.PlayerSpawn ? "Player spawn" : "Enemy spawn"; tank.transform.localScale = Vector3.one*.86f;
                    foreach (var renderer in tank.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(source => {
                            if (source.name != "PrimaryPaint" && source.name != "SecondaryArmor") return source;
                            var material = new Material(source); builder.ownedMaterials.Add(material);
                            Color color = element.Kind == LevelElementKind.PlayerSpawn ? new Color(.93f,.66f,.09f) : new Color(.65f,.19f,.12f);
                            if (source.name == "SecondaryArmor") color *= .68f; color.a=1; material.color=color; return material;
                        }).ToArray();
                }
            }
            finally
            {
                materials.AddRange(builder.ownedMaterials); meshes.AddRange(builder.ownedMeshes);
                DestroyImmediate(carrier);
            }
        }
    }
}
#endif
