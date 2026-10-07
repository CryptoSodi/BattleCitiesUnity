using System;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;

namespace BattleCities
{
    public static class BattleWaterBasinChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("WATER BASIN: "+message);}
        [MenuItem("Battle Cities/Validate Water Basins")]
        public static void Run()
        {
            Check(EditorApplication.isPlaying,"Run in gameplay Play mode");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                int waterStages=0;
                for(int stage=1;stage<=35;stage++)
                {
                    var map=Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/"+stage.ToString("00")).text);
                    var sim=new BattleSimulation(map,stage);
                    var root=new GameObject("Basin validation "+stage);
                    try
                    {
                        float width=sim.Width/64f,depth=sim.Height/64f;
                        var ground=BattleGroundSurface.Create(root.transform,"Ground",new Rect(0,-depth,width,depth),-.025f,material,sim.Terrain);
                        ValidateGround(ground,sim,"stage "+stage);
                        int layer=0;
                        foreach(var region in map.ground?.regions??Array.Empty<Region>())
                        {
                            var overlay=BattleGroundSurface.Create(root.transform,"Ground region",new Rect(region.x/64,-(region.y+region.height)/64,region.width/64,region.height/64),
                                -.017f+layer++*.001f,material,sim.Terrain);
                            ValidateGround(overlay,sim,"overlay in stage "+stage);
                        }
                        if(!sim.Terrain.Any(w=>w.Type=="water"))continue;
                        var surface=new GameObject("Water");surface.transform.SetParent(root.transform,false);
                        surface.AddComponent<BattleWaterSurface>().Build(sim.Terrain,Resources.Load<BattleVisualAssets>("BattleVisualAssets").WaterMaterial,
                            root.GetComponentsInChildren<BattleGroundSurface>());
                        ValidateBanks(surface);
                        Check(surface.GetComponent<MeshFilter>().sharedMesh.vertices.All(v=>v.y<ground.Height-.12f),"Water is visibly below the ground");
                        Check(root.GetComponentsInChildren<Collider>().Length==0,"Visual basins do not add gameplay colliders");
                        waterStages++;
                    }
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
                ValidateTopology(material);
                Debug.Log("WATER BASIN PASS: all 35 ground maps and overlays; "+waterStages+" recessed water stages; closed banks, rounded corners, islands and diagonal contacts.");
            }
            finally{UnityEngine.Object.DestroyImmediate(material);}
        }

        static void ValidateGround(BattleGroundSurface ground,BattleSimulation sim,string label)
        {
            var bounds=ground.Footprint;
            var wet=sim.Terrain.Where(w=>w.Type=="water").Select(w=>new Rect(w.Bounds.X/64,-w.Bounds.Bottom/64,w.Bounds.W/64,w.Bounds.H/64)).ToArray();
            var mesh=ground.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            double area=0;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                var cross=Vector3.Cross(b-a,c-a);Check(cross.y>0,"Ground faces upward: "+label);area+=cross.y*.5;
                var triangleBounds=Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x),Mathf.Min(a.z,b.z,c.z),Mathf.Max(a.x,b.x,c.x),Mathf.Max(a.z,b.z,c.z));
                Check(wet.All(w=>Overlap(triangleBounds,w)<.000001f),"No ground triangle covers the recessed water: "+label);
            }
            double expected=bounds.width*bounds.height-wet.Sum(w=>Overlap(bounds,w));
            Check(Math.Abs(area-expected)<.002,"Ground cutouts preserve every dry area: "+label);
            for(int i=0;i<vertices.Length;i++)
                Check(Vector2.Distance(mesh.uv[i],ground.UV(new Vector2(vertices[i].x,vertices[i].z)))<.00001f,"Original ground texture alignment: "+label);
        }
        static float Overlap(Rect a,Rect b)=>Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*
            Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));

        static void ValidateBanks(GameObject surface)
        {
            var banks=surface.GetComponent<BattleWaterBanks>();Check(banks&&banks.LoopCount>0,"Joined water has closed shoreline loops");
            var filters=surface.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject!=surface).ToArray();
            Check(filters.Length>0,"Basin has visible bank geometry");
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;var points=mesh.vertices;var indices=mesh.triangles;
                Check(points.All(p=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)),"Bank vertices are finite");
                Check(mesh.bounds.min.y<BattleWaterSurface.WaterHeight&&mesh.bounds.max.y>-.025f,"Bank extends from land below the waterline");
                for(int i=0;i<indices.Length;i+=3)
                {
                    var normal=Vector3.Cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]);
                    Check(normal.y>0,"Bank faces upward and into the basin without folded corners");
                }
                var shader=filter.GetComponent<Renderer>().sharedMaterial.shader;
                Check(shader&&!ShaderUtil.ShaderHasError(shader),"Bank shader compiles");
            }
        }

        static void ValidateTopology(Material material)
        {
            for(int scenario=0;scenario<3;scenario++)
            {
                var sim=new BattleSimulation(new MapData());sim.Terrain.Clear();
                if(scenario==0){sim.AddRegion("water",32,32,128,32);sim.AddRegion("water",64,64,32,64);}
                if(scenario==1){sim.AddRegion("water",32,32,128,32);sim.AddRegion("water",32,128,128,32);sim.AddRegion("water",32,64,32,64);sim.AddRegion("water",128,64,32,64);}
                if(scenario==2){sim.AddRegion("water",32,32,32,32);sim.AddRegion("water",64,64,32,32);}
                var root=new GameObject("Basin topology "+scenario);
                try
                {
                    var ground=BattleGroundSurface.Create(root.transform,"Ground",new Rect(0,-4,4,4),-.025f,material,sim.Terrain);
                    var surface=new GameObject("Water");surface.transform.SetParent(root.transform,false);
                    surface.AddComponent<BattleWaterSurface>().Build(sim.Terrain,Resources.Load<BattleVisualAssets>("BattleVisualAssets").WaterMaterial,new[]{ground});
                    ValidateBanks(surface);ValidateGround(ground,sim,"synthetic topology "+scenario);
                    Check(surface.GetComponent<BattleWaterBanks>().LoopCount==(scenario==0?1:2),"T-junctions join, dry islands keep an inner bank, diagonal pools remain separate");
                    var points=surface.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject!=surface).SelectMany(f=>f.sharedMesh.vertices).ToArray();
                    Check(points.Any(p=>p.x>.57f&&p.x<.66f&&p.z<-.57f&&p.z>-.66f&&p.y<-.2f),"Convex water corners are curved inside the tile footprint");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
    }
}
