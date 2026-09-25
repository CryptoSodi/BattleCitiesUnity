using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class StageZeroChecks
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("[Stage 0] " + message); }

        private static bool Free(BattleSimulation sim, int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= sim.Width / 64 || ty >= sim.Height / 64) return false;
            var bounds = TankState.MovementBox(tx * 64 + 32, ty * 64 + 32);
            return !bounds.Overlaps(sim.BaseBounds) &&
                   !sim.Terrain.Any(w => w.Alive && w.Solid && w.Bounds.Overlaps(bounds));
        }

        private static bool CanReach(BattleSimulation sim, int sx, int sy, Func<int,int,bool> goal)
        {
            var queue = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            var start = new Vector2Int(sx, sy);
            if (!Free(sim, sx, sy)) return false;
            queue.Enqueue(start); seen.Add(start);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (goal(p.x, p.y)) return true;
                foreach (var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                {
                    var next = p + d;
                    if (Free(sim, next.x, next.y) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }

        [MenuItem("Battle Cities/Validate experimental Stage 0")]
        public static void Validate()
        {
            var asset = Resources.Load<TextAsset>("Maps/00");
            Check(asset, "map resource is missing");
            var map = JsonConvert.DeserializeObject<MapData>(asset.text);
            var sim = new BattleSimulation(map, 0);
            Check(sim.Stage == 0 && sim.Width == 1536 && sim.Height == 1536, "24 x 24 field");
            Check(sim.Terrain.Count(w => w.PropKey != null) == map.objects.Length, "every object has simulation state");
            Check(map.objects.Where(o => o.type.StartsWith("city_building"))
                .All(o => o.width == 64 && o.height == 64), "buildings must be one tank tile wide");
            Check(map.objects.Where(o => o.role != "groundDetail" && !o.type.StartsWith("city_building"))
                .All(o => o.width <= 21 && o.height <= 21), "small props must be about one-third tank size");
            Check(map.ground.regions.Where(r => r.type == "road" || r.type == "forestPath")
                .All(r => r.width == 64 || r.height == 64), "authored travel lanes must be one tile wide");
            Check(map.objects.Any(o => o.role == "solidCover") &&
                  map.objects.Any(o => o.role == "destructibleObstacle") &&
                  map.objects.Any(o => o.role == "passableCover") &&
                  map.objects.Any(o => o.role == "groundDetail"), "environment role coverage");
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
            Check(palette, "environment palette is missing");
            var prefabs = palette.Lookup();
            foreach (var obj in map.objects)
            {
                Check(prefabs.TryGetValue(obj.type, out var prefab), "missing prefab " + obj.type);
                Check(prefab.Role == obj.role, "role differs between map and prefab: " + obj.type);
                Check(prefab.FootprintWidth == obj.width && prefab.FootprintHeight == obj.height,
                    "visual footprint differs from simulation: " + obj.type);
                Check(prefab.IntactVisual && prefab.GetComponentsInChildren<Collider>(true).Length == 0,
                    "visual or stray Physics collider: " + obj.type);
                if (obj.role == "destructibleObstacle") Check(prefab.DestroyedVisual,
                    "destructible needs a broken model: " + obj.type);
            }

            bool NearBase(int x, int y) => x >= 9 && x <= 14 && y >= 19 && y <= 22;
            foreach (var point in map.spawn.enemy.locations.Concat(map.spawn.player.locations))
            {
                int tx = (int)point.x / 64, ty = (int)point.y / 64;
                Check(Free(sim, tx, ty), "spawn is blocked: " + tx + "," + ty);
                Check(CanReach(sim, tx, ty, NearBase), "spawn lacks route to base approach: " + tx + "," + ty);
            }
            Check(CanReach(sim, 9, 22, (x,y) => x == 5 && y == 15), "city future flag route");
            Check(CanReach(sim, 9, 22, (x,y) => x == 18 && y == 14), "forest future flag route");

            var crate = sim.Terrain.First(w => w.DestructibleProp);
            int destroyed = 0;
            sim.WallDestroyed += w => { if (w == crate) destroyed++; };
            Check(crate.Solid && crate.StopsBullet, "crate collision before hit");
            sim.DestroyWall(crate, new ShotState { X = crate.Bounds.X + 32, Y = crate.Bounds.Y - 8,
                Direction = Facing.Down, WallDamage = 1 });
            Check(!crate.Alive && destroyed == 1, "normal shot removes crate collision and emits visual event");
            Check(sim.Terrain.Any(w => w.PropRole == "passableCover" && !w.Solid && !w.StopsBullet),
                "bush permits tank and bullet passage");

            var wave = new BattleSimulation(map, 0) { DisableEnemyFire = true, Freeze = 999 };
            for (int i = 0; i < 4500 && !wave.Won; i++)
            {
                wave.Step(default);
                foreach (var enemy in wave.Tanks.Where(t => !t.Player && t.Alive).ToArray()) wave.Kill(enemy);
            }
            Check(wave.Won && wave.BaseAlive && !wave.Lost, "Stage 0 completes when the wave is cleared");
            Debug.Log("[Stage 0] PASS: 24x24 load, " + map.objects.Length + " proportional prop states, 64-unit routes and buildings, prefab roles, safe reachable spawns, city/forest routes, destructible collision, passable cover, stage clear.");
        }
    }
}
