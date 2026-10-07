using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private readonly Dictionary<int, EnvironmentPiece> environmentViews = new Dictionary<int, EnvironmentPiece>();
        private Material forestPath, cityConcrete, cityPlaza, forestMeadow, dryDirt;
        private void BuildGroundSurfaces(MapData map)
        {
            if (!forestPath) forestPath = Material(new Color(.37f, .30f, .20f));
            if (!cityConcrete) cityConcrete = Material(new Color(.39f, .42f, .42f));
            if (!cityPlaza) cityPlaza = Material(new Color(.47f, .49f, .45f));
            if (!forestMeadow) forestMeadow = Material(new Color(.25f, .42f, .22f));
            if (!dryDirt) dryDirt = Material(new Color(.59f, .48f, .31f));
            int layer = 0;
            foreach (var region in map.ground?.regions ?? Array.Empty<Region>())
            {
                Material material;
                switch (region.type)
                {
                    case "city": material = cityConcrete; break;
                    case "dirt": material = dryDirt; break;
                    case "plaza": material = cityPlaza; break;
                    case "road": material = street; break;
                    case "forestPath": material = forestPath; break;
                    case "meadow": material = forestMeadow; break;
                    default: throw new InvalidOperationException("Unknown ground surface: " + region.type);
                }
                float level = -.023f + layer++ * .001f;
                BattleGroundSurface.Create(stageRoot,"Surface "+region.type,
                    new Rect(region.x/64,-(region.y+region.height)/64,region.width/64,region.height/64),
                    level+.006f,material,Simulation.Terrain);
                if (region.type != "road") continue;
                bool vertical = region.height > region.width;
                float length = vertical ? region.height : region.width;
                for (float d = 96; d < length - 48; d += 128)
                {
                    var stripe=new Box(region.x+(vertical?region.width/2:d)-(vertical?.8f:17.3f),
                        region.y+(vertical?d:region.height/2)-(vertical?17.3f:.8f),vertical?1.6f:34.6f,vertical?34.6f:1.6f);
                    if(Simulation.Terrain.Exists(t=>BattleTerrain.IsSurface(t.Type)&&t.Bounds.Overlaps(stripe)))continue;
                    Cube("Road center stripe",
                        World(region.x + (vertical ? region.width / 2 : d),
                              region.y + (vertical ? d : region.height / 2), -.001f),
                        vertical ? new Vector3(.025f, .006f, .54f) : new Vector3(.54f, .006f, .025f),
                        paint);
                }
            }
        }

        private void BuildEnvironment(MapData map)
        {
            environmentViews.Clear();
            if (map.objects == null || map.objects.Length == 0) return;
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
            if (!palette) throw new InvalidOperationException("Missing EnvironmentPalette resource");
            var prefabs = palette.Lookup();
            foreach (var group in Simulation.Terrain.Where(w => w.PropKey != null).GroupBy(w => w.IsBuildingSection ? w.BuildingId : w.Id))
            {
                var walls = group.ToArray();
                var wall = walls[0];
                if (!prefabs.TryGetValue(wall.PropKey, out var prefab))
                    throw new InvalidOperationException("Unknown environment prefab: " + wall.PropKey);
                if (prefab.Role != wall.PropRole)
                    throw new InvalidOperationException("Environment role mismatch: " + wall.PropKey);
                var b = wall.IsBuildingSection ? wall.BuildingBounds : wall.Bounds;
                var piece = Instantiate(prefab, World(b.X + b.W / 2, b.Y + b.H / 2),
                    Quaternion.Euler(0, wall.PropRotation, 0), stageRoot);
                bool quarterTurn = Mathf.Abs(Mathf.Repeat(wall.PropRotation, 180) - 90) < .01f;
                piece.transform.localScale = new Vector3(
                    (quarterTurn ? b.H : b.W) / prefab.FootprintWidth, 1,
                    (quarterTurn ? b.W : b.H) / prefab.FootprintHeight);
                if (wall.IsBuildingSection) piece.BindBuilding(walls); else piece.Bind(wall);
                foreach (var section in walls) environmentViews.Add(section.Id, piece);
            }
        }

        private void BuildStageLights(MapData map)
        {
            foreach (var item in map.lights ?? Array.Empty<MapLightData>())
            {
                var root = new GameObject("District light");
                root.transform.SetParent(stageRoot);
                root.transform.position = World(item.x, item.y, item.height <= 0 ? 1.7f : item.height);
                var light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(item.r, item.g, item.b);
                light.range = item.range <= 0 ? 5 : item.range;
                light.intensity = item.intensity <= 0 ? 1 : item.intensity;
                light.shadows = LightShadows.None;
            }
        }
    }
}
