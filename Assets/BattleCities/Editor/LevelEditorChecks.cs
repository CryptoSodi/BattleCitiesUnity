using System;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class LevelEditorChecks
    {
        static int count;
        static void Check(bool okay, string name) { count++; if (!okay) throw new Exception("LEVEL EDITOR: " + name); }
        [MenuItem("Battle Cities/Level Editor/Validate editor and damage rules")]
        public static void Run()
        {
            count = 0;
            var go = new GameObject("Level editor isolated checks") { hideFlags = HideFlags.HideAndDontSave };
            var doc = go.AddComponent<LevelEditorDocument>(); doc.BuildPreviews = false; doc.TrackUndo = false;
            doc.MapRoot = new GameObject("MapRoot").transform; doc.MapRoot.SetParent(go.transform);
            try
            {
                for (int stage = 1; stage <= 35; stage++)
                {
                    string json = Resources.Load<TextAsset>("Maps/" + stage.ToString("00")).text;
                    var original = new BattleSimulation(JsonConvert.DeserializeObject<MapData>(json), stage);
                    doc.Import(json); var saved = doc.ToJson(); var restored = new BattleSimulation(JsonConvert.DeserializeObject<MapData>(saved), stage);
                    Check(original.Width == restored.Width && original.Height == restored.Height, "map dimensions " + stage);
                    Check(original.BaseBounds.X == restored.BaseBounds.X && original.BaseBounds.Y == restored.BaseBounds.Y && original.Player.X == restored.Player.X && original.Player.Y == restored.Player.Y, "spawn defaults " + stage);
                    Check(original.Terrain.Count == restored.Terrain.Count, "terrain count " + stage);
                    for (int i = 0; i < original.Terrain.Count; i++)
                    { var a = original.Terrain[i]; var b = restored.Terrain[i]; if (a.Type != b.Type || a.Bounds.X != b.Bounds.X || a.Bounds.Y != b.Bounds.Y || a.Bounds.W != b.Bounds.W || a.Bounds.H != b.Bounds.H) throw new Exception("Roundtrip changed map " + stage + " cell " + i); }
                }
                doc.Import("{}");
                doc.Tool = "Paint"; doc.BrushKind = LevelElementKind.Terrain; doc.Brush = "brick"; doc.Snap = 64;
                doc.ApplyBrush(128, 128); var element = doc.At(150, 150); element.OverrideDamage = true; element.Damage.hitPoints = 3;
                doc.Brush = "water"; doc.Snap = 32; doc.ApplyBrush(128, 128);
                Check(doc.Elements.Where(e => e.Kind == LevelElementKind.Terrain).Sum(e => e.Size.x * e.Size.y) == 4096, "paint replaces only its footprint");
                Check(doc.Elements.Where(e => e.Tile == "brick").All(e => e.OverrideDamage && e.Damage.hitPoints == 3), "split preserves damage overrides");
                doc.ApplyBrush(128, 128, true);
                Check(doc.Elements.Where(e => e.Kind == LevelElementKind.Terrain).Sum(e => e.Size.x * e.Size.y) == 3072, "erase preserves the remainder");
                string draft = doc.ToJson(); doc.Import(draft);
                Check(doc.Elements.Where(e => e.Tile == "brick").All(e => e.Damage.hitPoints == 3), "damage survives save and reopen");
                doc.Create(LevelElementKind.Terrain, "lava", 0, 0, 64, 64);
                Check(doc.Validate().Any(e => e.Contains("blocked")), "blocked enemy spawn rejected");
                foreach (var e in doc.Elements.Where(e => e.Kind == LevelElementKind.PlayerSpawn || e.Kind == LevelElementKind.Base).ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
                string incomplete = doc.ToJson(); doc.Import(incomplete);
                Check(!doc.Elements.Any(e => e.Kind == LevelElementKind.Base || e.Kind == LevelElementKind.PlayerSpawn), "recovery preserves an incomplete draft without resurrecting deleted spawns/base");
                string beforeInvalid = doc.ToJson();
                try { doc.Import("{\"field\":{\"widthTiles\":99999}}"); throw new Exception("Malformed map accepted"); } catch (ArgumentException) { }
                Check(doc.ToJson() == beforeInvalid, "invalid import leaves the current draft intact");
                doc.Import("{}");
                var originalBase = doc.Elements.First(e => e.Kind == LevelElementKind.Base).Bounds;
                Check(doc.ResizeMap(17,15), "resize supports rectangular maps");
                Check(doc.WidthTiles == 17 && doc.HeightTiles == 15 && doc.Elements.First(e => e.Kind == LevelElementKind.Base).X == originalBase.X, "resize preserves placed elements");
                string resized = doc.ToJson(); doc.Import(resized);
                Check(doc.WidthTiles == 17 && doc.HeightTiles == 15, "dimensions persist in JSON");
                Check(!doc.ResizeMap(4,4) && doc.ToJson() == resized, "shrinking cannot silently discard elements");
                Check(!doc.ResizeMap(41,15), "oversized dimensions rejected");
                doc.SetTileSize(32);
                for(int i=0;i<LevelEditorCatalog.SurfaceTypes.Length;i++)
                {
                    var type=LevelEditorCatalog.SurfaceTypes[i];doc.ChooseBrush("Terrain:water");doc.SetSurfaceType(type);
                    doc.ApplyBrush(128+(i%2)*32,128+(i/2)*32);
                }
                var small=doc.Elements.Where(e=>e.Kind==LevelElementKind.Terrain).ToArray();
                Check(small.Length==4 && small.All(e=>e.Size==new Vector2(32,32)) && small.Sum(e=>e.Size.x*e.Size.y)==64*64, "four small tiles fit one large tile");
                Check(small.Select(e=>e.Tile).Distinct().Count()==4, "Water family paints all four types");
                doc.Select(small.First(e=>e.Tile=="water"));doc.SetSurfaceType("lava");
                Check(doc.Selected.Tile=="lava" && doc.Selected.Size==new Vector2(32,32), "selected surface type changes without changing its footprint");
                string surfaces=doc.ToJson();doc.Import(surfaces);
                Check(doc.ToJson()==surfaces, "surface variants survive save and reopen");
                doc.Tool="Erase";doc.SetTileSize(32);doc.ApplyBrush(128,128);
                Check(doc.Elements.Count(e=>e.Kind==LevelElementKind.Terrain)==3, "erase removes one small tile only");
                TestDamage();
                ShootingHealthChecks.Run();
                Debug.Log("[BattleCities] PASS: " + count + " level editor checks, all 35 campaign map roundtrips, and charged shooting/health regression checks.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void TestDamage()
        {
            var sim = new BattleSimulation(new MapData()); sim.Terrain.Clear();
            var rule = new MapDamage { hitPoints = 3 };
            sim.AddRegion("brick", 128, 128, 16, 16, rule); var wall = sim.Terrain[0]; int destroyed = 0; sim.WallDestroyed += _ => destroyed++;
            var normal = new ShotState { X = 136, Y = 145, Direction = Facing.Up, Damage = 1, WallDamage = 1 };
            sim.DestroyWall(wall, normal); Check(wall.Alive && wall.Health == 2, "normal hit removes one HP");
            sim.DestroyWall(wall, normal); Check(wall.Alive && wall.Health == 1, "second hit retains wall");
            sim.DestroyWall(wall, normal); Check(!wall.Alive && destroyed == 1, "third hit destroys once");
            sim.DestroyWall(wall, normal); Check(destroyed == 1, "destroy event not repeated");
            Check(rule.hitPoints == 3, "simulation never mutates authored damage profile");
            sim.Terrain.Clear(); sim.AddRegion("steel", 128, 128, 32, 32, new MapDamage { invulnerable = true }); wall = sim.Terrain[0];
            var power = new ShotState { X = 144, Y = 160, Direction = Facing.Up, PowerShot = true, Damage = 3, WallDamage = 2 };
            sim.DestroyWall(wall, normal); sim.DestroyWall(wall, power); Check(wall.Alive && wall.Health == 1, "invulnerability resists both attack types");
            wall.Damage.invulnerable = false; wall.Damage.normalShots = false; wall.Health = 4;
            sim.DestroyWall(wall, normal); Check(wall.Health == 4, "normal immunity");
            sim.DestroyWall(wall, power); Check(wall.Alive && wall.Health == 1, "power shot does three HP damage");
            wall.Damage.powerShots = false; sim.DestroyWall(wall, power); Check(wall.Health == 1, "power immunity includes splash");
            wall.Damage.normalShots = true; sim.DestroyWall(wall, normal); Check(!wall.Alive, "authored steel can break to a normal shot");
            sim = new BattleSimulation(new MapData { objects = new[] { new MapObjectData { type = "city_crate", role = "destructibleObstacle", x = 128, y = 128, width = 64, height = 64, damage = new MapDamage { hitPoints = 2 } } } });
            wall = sim.Terrain.First(w => w.PropKey != null); sim.DestroyWall(wall, normal); Check(wall.Alive && wall.Health == 1, "prop health pool"); sim.DestroyWall(wall, normal); Check(!wall.Alive, "prop destruction");
            // Real projectile collision, not only the damage helper.
            sim = new BattleSimulation(new MapData()) { Freeze = 9999, DisableEnemyFire = true }; sim.Terrain.Clear();
            for (int i = 0; i < 122; i++) sim.Step(default);
            sim.Player.X = 144; sim.Player.Y = 240; sim.Player.Aim = Facing.Up;
            sim.AddRegion("steel", 128, 128, 32, 32, new MapDamage { hitPoints = 2 }); wall = sim.Terrain[0];
            sim.Fire(sim.Player); for (int i = 0; i < 20; i++) sim.Step(default);
            Check(wall.Alive && wall.Health == 1, "projectile collision routes authored health");
        }
    }
}
