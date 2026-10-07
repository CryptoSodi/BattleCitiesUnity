using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        readonly List<BattleWaterSurface> terrainSurfaces=new List<BattleWaterSurface>();
        readonly Dictionary<string,Material> terrainMaterials=new Dictionary<string,Material>();
        public static float TerrainSurfaceHeight(string type)=>type==BattleTerrain.Quicksand?-.065f:
            BattleTerrain.IsSlippery(type)?-.006f:BattleWaterSurface.WaterHeight;

        void BuildTerrainSurfaces()
        {
            terrainSurfaces.Clear();
            var ground=stageRoot.GetComponentsInChildren<BattleGroundSurface>();
            foreach(var type in Simulation.Terrain.Where(w=>w.Alive&&BattleTerrain.IsSurface(w.Type)).Select(w=>w.Type).Distinct())
            {
                Material material=water;
                if(type!=BattleTerrain.Water&&!terrainMaterials.TryGetValue(type,out material))
                {
                    var shader=Resources.Load<Shader>("BattleTerrainSurfaces");
                    material=new Material(shader){name="Cartoon "+type};
                    material.SetTexture("_RippleMap",water.GetTexture("_RippleMap"));
                    material.SetFloat("_Kind",type==BattleTerrain.Lava?0:type==BattleTerrain.MuddyWater?1:
                        type==BattleTerrain.Quicksand?2:type==BattleTerrain.Ice?3:4);
                    terrainMaterials.Add(type,material);ownedMaterials.Add(material);
                }
                var root=new GameObject(type+" joined surface");root.transform.SetParent(stageRoot,false);
                var surface=root.AddComponent<BattleWaterSurface>();
                surface.Build(Simulation.Terrain,material,BattleTerrain.IsBasin(type)?ground:null,type,TerrainSurfaceHeight(type));
                terrainSurfaces.Add(surface);
            }
        }

#if UNITY_EDITOR
        // A repeatable playable palette, kept separate from the authored campaign maps.
        public void PreviewTerrainSample()
        {
            if(IsOnline)throw new System.InvalidOperationException("Terrain preview requires an offline game.");
            var asset=Resources.Load<TextAsset>("TerrainSamples/TerrainPalette");
            AutomaticCamera=false;Zoom=1;
            LoadStageMap(9,Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(asset.text));
            Simulation.DisableEnemyFire=true;Simulation.Freeze=999999;EnemyFire=false;
        }
#endif
    }
}
