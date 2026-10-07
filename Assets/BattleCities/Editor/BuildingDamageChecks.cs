using System;
using System.IO;
using System.Linq;
using BattleCities.Core;
using BattleCities.LevelEditor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class BuildingDamageChecks
    {
        public static int Assertions { get; private set; }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("Building damage: "+message);Assertions++;}
        static MapData Map(string key="draft_bld_harbor_warehouse_blue_3x2",float rotation=0,float width=192,float height=128)
            =>new MapData{field=new FieldData(),terrain=new Core.TerrainData{regions=Array.Empty<Region>()},
                objects=new[]{new MapObjectData{type=key,role="solidCover",x=256,y=128,width=width,height=height,rotation=rotation}},
                spawn=new SpawnData{enemy=new SpawnGroup{locations=new[]{new Point{x=0,y=0}},list=new[]{new EnemySpec()}},player=new SpawnGroup{locations=new[]{new Point{x=64,y=704}}}},@base=new BaseData{x=384,y=736}};
        static Wall[] Cells(BattleSimulation sim)=>sim.Terrain.Where(w=>w.IsBuildingSection).ToArray();
        static ShotState Shot(Wall cell,bool power=false)=>new ShotState{Direction=Facing.Right,X=cell.Bounds.X,Y=cell.Bounds.Y+cell.Bounds.H/2,Damage=1,WallDamage=power?2:1,PowerShot=power};
        [MenuItem("Battle Cities/Level Editor/Validate building damage and lights")]
        public static void Run()
        {
            Assertions=0;var sim=new BattleSimulation(Map());var cells=Cells(sim);var hit=cells[0];
            Check(cells.Length==24&&cells.All(w=>w.Health==3),"24 independent small sections for a 3x2 building");
            sim.DestroyWall(hit,Shot(hit));Check(hit.Alive&&hit.Health==2&&cells.Skip(1).All(w=>w.Health==3),"first hit damages only its section");
            sim.DestroyWall(hit,Shot(hit));Check(hit.Health==1&&hit.Alive,"second hit is partial damage");
            sim.DestroyWall(hit,Shot(hit));Check(!hit.Alive&&hit.Health==0&&cells.Count(w=>w.Alive)==23,"third hit opens only the struck section");
            Check(!sim.Terrain.Any(w=>w.Alive&&w.Solid&&w.Bounds.Overlaps(hit.Bounds)),"destroyed section has no invisible collision");
            int deaths=0;sim.WallDestroyed+=_=>deaths++;sim.DestroyWall(hit,Shot(hit));Check(deaths==0,"dead sections do not emit destruction again");
            var power=new BattleSimulation(Map());var pc=Cells(power);power.DestroyWall(pc[0],Shot(pc[0],true));
            Check(pc.Count(w=>w.Health<3)>1&&pc.Any(w=>w.Health==3),"power splash is local to the impact radius");
            var protectedMap=Map();protectedMap.objects[0].damage=new MapDamage{hitPoints=7,invulnerable=true};var immune=new BattleSimulation(protectedMap);var ic=Cells(immune)[0];immune.DestroyWall(ic,Shot(ic));Check(ic.Health==7,"authored invulnerability remains available");
            protectedMap.objects[0].damage.invulnerable=false;protectedMap.objects[0].damage.normalShots=false;var selective=new BattleSimulation(protectedMap);var sc=Cells(selective)[0];selective.DestroyWall(sc,Shot(sc));Check(sc.Health==7,"normal shot immunity honored");selective.DestroyWall(sc,Shot(sc,true));Check(sc.Health==6,"power susceptibility honored");
            // Exercise collision-driven bullets after the stage intro, not only direct damage calls.
            var bullets=new BattleSimulation(Map());bullets.DisableEnemyFire=true;for(int i=0;i<122;i++)bullets.Step(new Command());
            var shot=new ShotState{Id=90000,Owner=-1,Player=true,Direction=Facing.Right,X=230,Y=144,Speed=600,Damage=1,WallDamage=1};bullets.Shots.Add(shot);
            for(int i=0;i<6;i++)bullets.Step(new Command());Check(!shot.Alive&&Cells(bullets)[0].Health==2,"real projectile collision selects the edge section");
            // A complete two-cell-wide breach admits the actual 64-unit tank footprint.
            var breach=new BattleSimulation(Map());foreach(var w in Cells(breach).Where(w=>w.Bounds.Y<192))for(int i=0;i<3;i++)breach.DestroyWall(w,Shot(w));
            Check(!breach.Terrain.Any(w=>w.Alive&&w.Solid&&w.Bounds.Overlaps(new Box(256,128,192,64))),"complete breach admits a full tank while other rows survive");
            Check(Cells(breach).Where(w=>w.Bounds.Y>=192).All(w=>w.Alive),"breach preserves adjacent structure");
            Roundtrip();Replica();
            foreach(var prefab in Resources.Load<EnvironmentPalette>("EnvironmentPalette").Prefabs.Where(p=>BattleBuildings.IsBuilding(p.Key)))Visual(prefab,0,prefab.FootprintWidth,prefab.FootprintHeight);
            var blue=Resources.Load<EnvironmentPalette>("EnvironmentPalette").Prefabs.First(p=>p.Key=="draft_bld_harbor_warehouse_blue_3x2");
            Visual(blue,90,128,192);Visual(blue,0,160,96);Visual(blue,45,192,192);
            Debug.Log("Building damage PASS: "+Assertions+" assertions (simulation, bullets, local blast, breach, settings, replica, all model families, rotation, resize, lights).");
        }
        static void Roundtrip()
        {
            var map=Map();map.objects[0].building=new MapBuildingSettings{lights=false,lightIntensity=2.3f,lightRange=3.1f};map.objects[0].damage=new MapDamage{hitPoints=5};
            var go=new GameObject("Building roundtrip check");go.SetActive(false);
            try{var doc=go.AddComponent<LevelEditorDocument>();doc.BuildPreviews=false;doc.TrackUndo=false;doc.MapRoot=new GameObject("Elements").transform;doc.MapRoot.SetParent(go.transform,false);doc.Import(JsonConvert.SerializeObject(map));doc.Import(doc.ToJson());var p=doc.Export().objects.Single();Check(!p.building.lights&&p.building.lightIntensity==2.3f&&p.building.lightRange==3.1f&&p.damage.hitPoints==5,"health and light settings survive editor save/open");}
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Replica()
        {
            var host=new BattleSimulation(Map());var client=new BattleSimulation(Map());host.RecordVisualEvents();client.RecordVisualEvents();var hc=Cells(host);host.DestroyWall(hc[0],Shot(hc[0]));for(int i=0;i<3;i++)host.DestroyWall(hc[1],Shot(hc[1]));
            var frame=new BattleFrame{TerrainAlive=host.Terrain.Take(host.InitialTerrainCount).Select(w=>w.Alive).ToArray(),TerrainHealth=host.Terrain.Take(host.InitialTerrainCount).Select(w=>w.Health).ToArray(),ExtraWalls=Array.Empty<Wall>(),Participants=host.Participants.ToArray(),Tanks=host.Tanks.ToArray(),Shots=Array.Empty<ShotState>(),Mines=Array.Empty<MineState>(),Drones=Array.Empty<DroneState>(),Turrets=Array.Empty<TurretState>(),LandDrones=Array.Empty<LandDroneState>(),BaseAlive=true};
            client.ApplyFrame(frame);var cc=Cells(client);Check(cc[0].Health==2&&cc[0].Alive&&!cc[1].Alive&&cc[1].Health==0,"late-join snapshot restores partial and destroyed building sections");
        }
        static void Visual(EnvironmentPiece prefab,float rotation,float width,float height)
        {
            var map=Map(prefab.Key,rotation,width,height);var sim=new BattleSimulation(map);var cells=Cells(sim);
            var p=UnityEngine.Object.Instantiate(prefab,new Vector3((256+width/2)/64,0,-(128+height/2)/64),Quaternion.Euler(0,rotation,0));
            bool quarter=Mathf.Abs(Mathf.Repeat(rotation,180)-90)<.01f;p.transform.localScale=new Vector3((quarter?height:width)/prefab.FootprintWidth,1,(quarter?width:height)/prefab.FootprintHeight);
            try
            {
                p.BindBuilding(cells);var view=p.GetComponent<BuildingDamageView>();Check(view.WindowLights.Count==2&&view.WindowLights.All(l=>l.enabled&&l.intensity>0),prefab.Key+" warm lights on");
                var light=view.WindowLights[0];float intensity=light.intensity,otherIntensity=view.WindowLights[1].intensity;
                var owner=cells.OrderBy(w=>(new Vector3((w.Bounds.X+w.Bounds.W/2)/64,light.transform.position.y,-(w.Bounds.Y+w.Bounds.H/2)/64)-light.transform.position).sqrMagnitude).First();
                sim.DestroyWall(owner,Shot(owner));p.RefreshVisual();Check(light.intensity<intensity&&view.WindowLights[1].intensity==otherIntensity,prefab.Key+" light dims only at the damaged window");
                owner.Health=3;p.RefreshVisual();
                sim.DestroyWall(cells[0],Shot(cells[0]));p.RefreshVisual();Check(view.DamageMesh&&view.DamageMesh.vertexCount>0,prefab.Key+" partial damage generates textured geometry");
                Check(view.DamageMesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.y)),prefab.Key+" finite section geometry");
                for(int i=0;i<2;i++)sim.DestroyWall(cells[0],Shot(cells[0]));p.RefreshVisual();Check(view.AliveSections==cells.Length-1,prefab.Key+" one visible section removed");
                var vertices=view.DamageMesh.vertices;var b=cells[0].Bounds;
                Check(!vertices.Select(v=>p.transform.TransformPoint(v)).Any(v=>v.y>.03f&&v.x>b.X/64+.001f&&v.x<b.Right/64-.001f&&v.z>-b.Bottom/64+.001f&&v.z<-b.Y/64-.001f),prefab.Key+" no roof or wall left over the destroyed cell");
                foreach(var w in cells.Where(w=>w.Alive))for(int i=0;i<3;i++)sim.DestroyWall(w,Shot(w));p.RefreshVisual();Check(view.AliveSections==0&&view.WindowLights.All(l=>!l.enabled),prefab.Key+" all lights go out after destruction");
            }
            finally{UnityEngine.Object.DestroyImmediate(p.gameObject);}
        }
    }
}
