using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class ConceptMapChecks
    {
        static int checks;
        static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message);checks++; }
        static bool Clear(BattleSimulation sim,Box box)=>box.X>=0&&box.Y>=0&&box.Right<=sim.Width&&box.Bottom<=sim.Height&&!sim.BaseBounds.Overlaps(box)&&!sim.Terrain.Any(w=>w.Alive&&w.Solid&&w.Bounds.Overlaps(box));
        static Box Tank(float x,float y)=>TankState.MovementBox(x+32,y+32);
        [MenuItem("Battle Cities/Level Editor/Validate concept maps")]
        public static void Run()
        {
            checks=0;
            for(int index=0;index<3;index++)
            {
                string json=File.ReadAllText(LevelEditorMapLibrary.Folder+"/"+LevelEditorMapLibrary.Slugs[index]+".json");
                var go=new GameObject("Isolated concept validation");go.SetActive(false);
                try
                {
                    var doc=go.AddComponent<LevelEditorDocument>();doc.BuildPreviews=false;doc.TrackUndo=false;
                    doc.MapRoot=new GameObject("MapRoot").transform;doc.MapRoot.SetParent(go.transform,false);doc.Import(json);
                    Require(doc.Validate().Count==0,doc.LevelName+": "+string.Join(", ",doc.Validate()));
                    Require(doc.WidthTiles==25&&doc.HeightTiles==25,"Map dimensions");
                    var map=doc.Export();var original=JsonConvert.DeserializeObject<MapData>(json);
                    Require(JsonConvert.SerializeObject(map.spawn.enemy.list)==JsonConvert.SerializeObject(JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text).spawn.enemy.list),"Original campaign roster and drops");
                    Require(map.@base.x==736&&map.@base.y==1504,"HQ footprint");
                    foreach(JObject placement in JObject.Parse(json)["concept"]["placements"])
                    {
                        string id=(string)placement["id"],type=(string)placement["type"];
                        var elements=doc.Elements.Where(e=>e.SourceId==id||e.SourceId.StartsWith(id+".")).OrderBy(e=>e.X).ToArray();
                        Require(elements.Length==(type=="log"?(int)placement["width"]:1),id+" element count");
                        Require(elements[0].X==(float)placement["x"]*64&&elements[0].Y==(float)placement["y"]*64,id+" origin");
                        Require(elements.Sum(e=>e.Size.x)==(float)placement["width"]*64&&elements.All(e=>e.Size.y==(float)placement["height"]*64),id+" dimensions");
                    }
                    var firstNotes=doc.Elements.Select(e=>e.SourceId+"|"+e.DesignNotes).ToArray();
                    doc.Import(doc.ToJson());Require(firstNotes.SequenceEqual(doc.Elements.Select(e=>e.SourceId+"|"+e.DesignNotes)),"Notes survive save/open");
                    Require(doc.Export().objects.Count(p=>p.bridge)==original.objects.Count(p=>p.bridge),"Bridge properties survive save/open");
                    var sim=new BattleSimulation(map,doc.PreviewStage);
                    Reachability(map,"intact");
                    foreach(var spawn in map.spawn.enemy.locations)
                        Require(Clear(sim,new Box(spawn.x,spawn.y,64,192)),"Enemy two-cell exit");
                    foreach(var p in map.objects)
                    {
                        var wall=sim.Terrain.Single(w=>w.PropKey==p.type&&w.Bounds.X==p.x&&w.Bounds.Y==p.y);
                        if(p.type=="draft_log")Require(wall.Health==2&&wall.DestructibleProp&&wall.StopsBullet&&wall.Solid,"Independent two-hit log");
                        if(p.type=="draft_barrel"||p.type=="draft_sandbag")Require(wall.Health==1&&wall.DestructibleProp,"One-hit temporary prop");
                        if(p.role=="solidCover")Require(wall.Solid&&wall.StopsBullet&&(BattleBuildings.IsBuilding(p.type)?wall.IsBuildingSection&&!wall.Damage.invulnerable:wall.Damage.invulnerable),"Building sections or static scenery island");
                        if(p.role=="groundDetail")Require(!wall.Solid&&!wall.StopsBullet,"Passable surface");
                    }
                    if(index==0)
                    {
                        Routes(sim,new[]{new Vector2(2,0),new Vector2(2,10),new Vector2(5,10),new Vector2(5,15),new Vector2(4,15),new Vector2(4,21),new Vector2(10,21)});
                        Routes(sim,new[]{new Vector2(22,0),new Vector2(22,9),new Vector2(19,9),new Vector2(19,16),new Vector2(20,16),new Vector2(20,21),new Vector2(14,21)});
                        Routes(sim,new[]{new Vector2(12,10),new Vector2(12,14)});
                        Reachability(CloneWithout(map,p=>p.type=="draft_bridge_timber"),"timber removed");
                        Reachability(CloneWithout(map,p=>p.bridge&&p.x!=4*64),"west only");
                        Reachability(CloneWithout(map,p=>p.bridge&&p.x!=19*64),"east only");
                        var bridge=doc.Elements.Single(e=>e.SourceId=="BR02");bridge.SetBounds(13*64,11*64,64,192);
                        var moved=new BattleSimulation(doc.Export());
                        Require(!Clear(moved,Tank(12*64,12*64))&&Clear(moved,Tank(13*64,12*64)),"Moving a bridge refills old water and opens its new footprint");
                        bridge.gameObject.SetActive(false);
                        Require(!Clear(new BattleSimulation(doc.Export()),Tank(13*64,12*64)),"Removing a bridge restores water");
                    }
                    else if(index==1)
                    {
                        Routes(sim,new[]{new Vector2(2,0),new Vector2(2,22),new Vector2(9,22),new Vector2(9,21),new Vector2(10,21)});
                        Routes(sim,new[]{new Vector2(22,0),new Vector2(22,22),new Vector2(15,22),new Vector2(15,21),new Vector2(14,21)});
                        Reachability(CloneWithout(map,p=>p.type=="draft_log"),"logs removed");
                    }
                    else
                    {
                        Routes(sim,new[]{new Vector2(2,0),new Vector2(2,21),new Vector2(10,21)});
                        Routes(sim,new[]{new Vector2(22,0),new Vector2(22,21),new Vector2(14,21)});
                        Reachability(CloneWithout(map,p=>p.type=="draft_barrel"),"barrels removed");
                        foreach(var point in new[]{new Vector2(7,7),new Vector2(7,12),new Vector2(17,5),new Vector2(17,10)})Require(Clear(sim,Tank(point.x*64,point.y*64)),"Belt end cell");
                        // Main routes remain available even when belts are treated as blocked.
                        var noBelts=JsonConvert.DeserializeObject<MapData>(JsonConvert.SerializeObject(map));
                        foreach(var p in noBelts.objects.Where(p=>p.type=="draft_conveyor"))p.role="solidCover";
                        Reachability(noBelts,"without conveyors");
                    }
                    Debug.Log(doc.LevelName+": geometry, full tank routes and roundtrip passed.");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            CheckVisualBounds();
            Debug.Log("Concept maps PASS: "+checks+" assertions. Static routes use the actual TankState.MovementBox at 32-unit steps; no live AI/timing claim.");
        }
        static MapData CloneWithout(MapData source,Func<MapObjectData,bool> removed)
        {var map=JsonConvert.DeserializeObject<MapData>(JsonConvert.SerializeObject(source));map.objects=map.objects.Where(p=>!removed(p)).ToArray();return map;}
        static void Reachability(MapData map,string state)
        {
            var sim=new BattleSimulation(map);const int n=49;var open=new bool[n,n];
            for(int x=0;x<n;x++)for(int y=0;y<n;y++)open[x,y]=Clear(sim,Tank(x*32,y*32));
            foreach(var spawn in map.spawn.enemy.locations.Concat(map.spawn.player.locations))
            {
                int sx=(int)spawn.x/32,sy=(int)spawn.y/32;
                Require(open[sx,sy],state+" spawn footprint");
                var seen=new bool[n,n];var queue=new Queue<Vector2Int>();queue.Enqueue(new Vector2Int(sx,sy));seen[sx,sy]=true;
                while(queue.Count>0)
                {
                    var cell=queue.Dequeue();
                    foreach(var delta in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                    {
                        var p=cell+delta;if(p.x<0||p.y<0||p.x>=n||p.y>=n||seen[p.x,p.y]||!open[p.x,p.y])continue;
                        var sweep=new Box(Math.Min(p.x,cell.x)*32,Math.Min(p.y,cell.y)*32,64+Math.Abs(delta.x)*32,64+Math.Abs(delta.y)*32);
                        if(!Clear(sim,sweep))continue;seen[p.x,p.y]=true;queue.Enqueue(p);
                    }
                }
                foreach(int x in new[]{20,24,28})Require(seen[x,42],state+" spawn "+sx+","+sy+" -> HQ approach "+x/2+",21");
            }
        }
        static void Routes(BattleSimulation sim,Vector2[] points)
        {
            for(int i=1;i<points.Length;i++)
            {
                var start=points[i-1]*64;var end=points[i]*64;var delta=end-start;
                Require(delta.x==0||delta.y==0,"Cardinal route segment");
                var sweep=new Box(Math.Min(start.x,end.x),Math.Min(start.y,end.y),Math.Abs(delta.x)+64,Math.Abs(delta.y)+64);
                Require(Clear(sim,sweep),"Specified route "+points[i-1]+" -> "+points[i]);
            }
        }
        static void CheckVisualBounds()
        {
            foreach(var prefab in Resources.Load<EnvironmentPalette>("EnvironmentPalette").Prefabs.Where(p=>p.Key.StartsWith("draft_")))
            {
                var instance=UnityEngine.Object.Instantiate(prefab);try
                {
                    foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        var b=renderer.bounds;float w=prefab.FootprintWidth/128,h=prefab.FootprintHeight/128;
                        Require(b.min.x>=-w-.001f&&b.max.x<=w+.001f&&b.min.z>=-h-.001f&&b.max.z<=h+.001f,prefab.Key+": visual overhang "+renderer.name);
                    }
                }finally{UnityEngine.Object.DestroyImmediate(instance.gameObject);}
            }
        }
        public static void RenderPreviews()
        {
            var source=UnityEngine.Object.FindFirstObjectByType<LevelEditorDocument>();if(!source)throw new InvalidOperationException("Open editor for its saved model references.");
            string folder="Docs/ConceptMaps/Previews";Directory.CreateDirectory(folder);
            bool async=EditorSettings.asyncShaderCompilation;EditorSettings.asyncShaderCompilation=false;
            try
            {
                for(int i=0;i<3;i++)
                {
                    var fixture=new GameObject("Isolated map preview");fixture.SetActive(false);var preview=new PreviewRenderUtility();
                    var materials=new List<Material>();var meshes=new List<UnityEngine.Mesh>();
                    try
                    {
                        var doc=fixture.AddComponent<LevelEditorDocument>();doc.TrackUndo=false;doc.BuildPreviews=false;doc.MapRoot=new GameObject("MapRoot").transform;doc.MapRoot.SetParent(fixture.transform,false);
                        doc.BrickModel=source.BrickModel;doc.SteelModel=source.SteelModel;doc.BushModel=source.BushModel;doc.EagleModel=source.EagleModel;doc.TankModel=source.TankModel;doc.BrickSectionData=source.BrickSectionData;doc.GroundTextures=source.GroundTextures;
                        doc.Import(File.ReadAllText(LevelEditorMapLibrary.Folder+"/"+LevelEditorMapLibrary.Slugs[i]+".json"));
                        var root=new GameObject("Map preview");preview.AddSingleGO(root);BattleGame.BuildEditorMap(root.transform,doc,materials,meshes);
                        preview.camera.transform.position=new Vector3(12.5f,35,-12.5f);preview.camera.transform.rotation=Quaternion.Euler(90,0,0);preview.camera.orthographic=true;preview.camera.orthographicSize=13.2f;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;preview.camera.backgroundColor=new Color(.06f,.10f,.14f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                        preview.ambientColor=new Color(.50f,.55f,.60f);preview.lights[0].intensity=1.35f;preview.lights[0].transform.rotation=Quaternion.Euler(55,-35,0);preview.lights[1].intensity=.35f;
                        preview.BeginPreview(new Rect(0,0,1200,1200),GUIStyle.none);preview.Render(true);preview.Render(true);var texture=preview.EndPreview();
                        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,1200,0,RenderTextureFormat.ARGB32);var png=new Texture2D(1200,1200,TextureFormat.RGBA32,false);
                        try{Graphics.Blit(texture,rt);RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,1200,1200),0,0);png.Apply();File.WriteAllBytes(folder+"/"+LevelEditorMapLibrary.Slugs[i]+".png",png.EncodeToPNG());}
                        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(png);}
                    }
                    finally{preview.Cleanup();UnityEngine.Object.DestroyImmediate(fixture);foreach(var m in materials)if(m)UnityEngine.Object.DestroyImmediate(m);foreach(var m in meshes)if(m)UnityEngine.Object.DestroyImmediate(m);}
                }
            }
            finally{EditorSettings.asyncShaderCompilation=async;}
            Debug.Log("Three gameplay-model preview images saved in "+folder);
        }
    }
}
