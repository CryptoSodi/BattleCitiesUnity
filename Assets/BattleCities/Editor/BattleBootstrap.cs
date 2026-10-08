using System;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json;

namespace BattleCities.Editor
{
    public static class BattleBootstrap
    {
        const string Root="Assets/BattleCities/";
        [MenuItem("Battle Cities/Create playable stage")]
        public static void CreateStage()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before building the stage");
            if(!System.IO.Directory.Exists(Root+"Scenes"))System.IO.Directory.CreateDirectory(Root+"Scenes");
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Battle Cities");var game=root.AddComponent<BattleGame>();
            root.AddComponent<EconomyClient>();
            string[] tanks={"image-built-tank","tracked-upgrade","twin-cannon-ground-up","heavy-fourview"};
            game.TankModels=tanks.Select(t=>Model("Art/Tanks/"+t+"/tank.glb")).ToArray();
            game.SpeedEnemyModel=Model("Art/Tanks/wheeled-scout/tank.glb");
            game.BrickModel=Model("Art/Terrain/Brick/brick-block.glb");game.SteelModel=Model("Art/Terrain/Steel/steel-brick.glb");game.BushModel=Model("Art/Terrain/Bush/bush.glb");
            game.MineModel=Model("Art/Deployables/ArcadeMine/mine.glb");
            game.DroneModel=Model("Art/Deployables/WingDrone/wing-drone.glb");
            game.GroundTurretPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/GroundTurret.prefab");
            if(!game.GroundTurretPrefab)throw new InvalidOperationException("Missing GroundTurret prefab");
            game.BrickSectionData=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"Art/Terrain/Brick/collision-sections.json");
            game.EagleModel=Model("Art/Base/eagle-photo.glb");game.RuinedEagleModel=Model("Art/Base/eagle-destroyed-photo.glb");
            game.BulletModels=new[]{"normal","rapid","power"}.Select(t=>Model("Art/Projectiles/"+t+".glb")).ToArray();
            game.GroundTextures=new[]{"grass","dirt","mud","forest"}.Select(t=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Art/Ground/"+t+"-albedo.png")).ToArray();
            game.PowerupAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Art/Powerups/atlas.png");
            game.Stage=1;
            EditorSceneManager.SaveScene(scene,Root+"Scenes/BattleCity.unity");
            var battlePath=Root+"Scenes/BattleCity.unity";
            var loginPath=Root+"Scenes/Login.unity";
            var menuPath=Root+"Scenes/MainMenu.unity";
            var remaining=EditorBuildSettings.scenes.Where(s=>s.path!=battlePath&&s.path!=loginPath&&s.path!=menuPath);
            var ordered=new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(loginPath))ordered.Add(new EditorBuildSettingsScene(loginPath,true));
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(menuPath))ordered.Add(new EditorBuildSettingsScene(menuPath,true));
            ordered.Add(new EditorBuildSettingsScene(battlePath,true));
            ordered.AddRange(remaining);
            EditorBuildSettings.scenes=ordered.ToArray();
            Selection.activeGameObject=root;AssetDatabase.SaveAssets();
            Debug.Log("[BattleCities] Playable stage scene created. Press Play. F1 opens stage and lighting controls.");
        }
        static GameObject Model(string path)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+path);
            if(!model)throw new InvalidOperationException("GLB has not imported: "+path);
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)
                if(!m||!m.shader||m.shader.name=="Hidden/InternalErrorShader")throw new InvalidOperationException("Missing GLB material: "+path);
            return model;
        }
        [MenuItem("Battle Cities/Validate simulation")]
        public static void Validate()
        {
            int count=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("[BattleCities] FAIL: "+label);count++;};
            MapData Empty()=>new MapData{terrain=new BattleCities.Core.TerrainData{regions=Array.Empty<Region>()},spawn=new SpawnData{enemy=new SpawnGroup{list=Enumerable.Range(0,20).Select(_=>new EnemySpec()).ToArray()}}};
            var powerups=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Art/Powerups/atlas.png");
            check(powerups&&powerups.width==1619&&powerups.height==971,"approved 5x3 power-up atlas imported at original resolution");
            foreach(var type in new[]{"shield","defence","freeze","life","upgrade","wipeout","speed","batc100","batc200","zoomout"})
                check(BattleHud.TryPowerupUv(type,out var uv)&&uv.xMin>=0&&uv.yMin>=0&&uv.xMax<=1&&uv.yMax<=1,"power-up icon mapped: "+type);
            foreach(Facing direction in Enum.GetValues(typeof(Facing)))
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();sim.AddRegion("brick",96,96,64,64);
                var hit=sim.Terrain.First(w=>direction==Facing.Up?w.Bounds.Y==144:direction==Facing.Down?w.Bounds.Y==96:direction==Facing.Left?w.Bounds.X==144:w.Bounds.X==96);
                sim.DestroyWall(hit,new ShotState{X=128,Y=128,Direction=direction,WallDamage=1});
                check(sim.Terrain.Count(w=>!w.Alive)==4,"one exposed brick row "+direction);
                check(sim.Terrain.Count(w=>w.Alive)==12,"back rows preserved "+direction);
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();sim.AddRegion("brick",192,576,64,64);sim.AddRegion("brick",192,576,64,64);
                check(sim.Terrain.Count==16,"duplicate brick regions collapse to unique cells");
                var hit=sim.Terrain.First(w=>w.Bounds.Y==576&&w.Bounds.X==192);
                sim.DestroyWall(hit,new ShotState{X=224,Y=540,Direction=Facing.Down,WallDamage=1});
                check(sim.Terrain.Count(w=>!w.Alive)==4,"duplicate source region still clears four unique front bricks");
                check(sim.Terrain.Count(w=>w.Alive)==12,"duplicate source region preserves rear rows");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();sim.AddRegion("brick",96,0,32,96);sim.AddRegion("brick",144,0,32,96);
                sim.DestroyWall(sim.Terrain[0],new ShotState{X=127,Y=-20,Direction=Facing.Down,WallDamage=1});
                check(sim.Terrain.Any(w=>!w.Alive),"pillar was damaged");check(sim.Terrain.Where(w=>w.Bounds.X>=144).All(w=>w.Alive),"damage cannot jump gap");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();sim.AddRegion("brick",96,0,64,32);
                foreach(var w in sim.Terrain.Where(w=>w.Bounds.Y==0&&w.Bounds.X<128))w.Alive=false;
                var hit=sim.Terrain.First(w=>w.Bounds.X==96&&w.Bounds.Y==16);
                sim.DestroyWall(hit,new ShotState{X=104,Y=-20,Direction=Facing.Down,WallDamage=1});
                check(!hit.Alive,"crater target removed");check(sim.Terrain.Where(w=>w.Bounds.X>=128).All(w=>w.Alive),"covered bricks preserved");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();sim.AddRegion("steel",96,96,32,32);var hit=sim.Terrain[0];
                sim.DestroyWall(hit,new ShotState{X=112,Y=90,Direction=Facing.Down,WallDamage=1});check(hit.Alive,"normal shot cannot destroy steel");
                sim.DestroyWall(hit,new ShotState{X=112,Y=90,Direction=Facing.Down,WallDamage=2});check(!hit.Alive,"power shot destroys steel");
            }
            {
                var sim=new BattleSimulation(Empty());sim.ApplyPowerup("defence");
                var shield=sim.Terrain.Where(w=>w.Alive&&w.Type=="steel"&&w.Bounds.Overlaps(new Box(sim.BaseBounds.X-32,sim.BaseBounds.Y-32,128,96))).ToArray();
                check(shield.Length==8&&shield.All(w=>w.Bounds.W==32&&w.Bounds.H==32),"base defence uses one 32px steel block of thickness");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();var player=sim.Player;player.X=200;player.Y=200;
                sim.Tanks.Add(new TankState{Id=999,X=200,Y=200,Alive=true});sim.Freeze=999;
                for(int i=0;i<121;i++)sim.Step(default);
                float before=player.Y;sim.Step(new Command{Move=Facing.Down});
                check(player.Y>before,"overlapping tank can move out instead of remaining permanently stuck");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();var player=sim.Player;player.X=224;player.Y=136;sim.Freeze=999;
                sim.AddRegion("brick",256,0,64,128);sim.AddRegion("brick",256,192,64,128);
                for(int i=0;i<121;i++)sim.Step(default);
                for(int i=0;i<24;i++)sim.Step(new Command{Move=Facing.Right});
                check(player.X>224&&Math.Abs(player.Y-160)<.01f,"player aligns through an exact one-tile Stage 2 opening");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();var player=sim.Player;player.X=224;player.Y=158;sim.Freeze=999;
                sim.AddRegion("brick",256,0,64,128);sim.AddRegion("brick",256,192,64,128);
                for(int i=0;i<121;i++)sim.Step(default);
                float before=player.X;sim.Step(new Command{Move=Facing.Right});
                check(player.X>before,"small movement inset prevents invisible corner snag while visuals remain full size");
            }
            {
                var sim=new BattleSimulation(Empty());sim.Terrain.Clear();var player=sim.Player;player.X=224;player.Y=160;sim.Freeze=999;
                sim.AddRegion("brick",256,0,64,320);
                for(int i=0;i<121;i++)sim.Step(default);
                for(int i=0;i<120;i++)sim.Step(new Command{Move=Facing.Right});
                check(player.MovementBounds.Right<=256.01f&&Math.Abs(player.Y-160)<.01f,"held contact stays blocked without lateral jitter");
            }
            var map=JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text);
            check(new BattleSimulation(map).BaseBounds.Y==768,"legacy base defaults to bottom centre");
            var a=new BattleSimulation(map);var b=new BattleSimulation(map);
            for(int i=0;i<1200;i++){var cmd=new Command{Move=i%240<120?Facing.Up:(Facing?)null,Aim=Facing.Right,Fire=true};a.Step(cmd);b.Step(cmd);}
            check(a.Score==b.Score&&a.Terrain.Count(w=>w.Alive)==b.Terrain.Count(w=>w.Alive)&&a.Tanks.Count==b.Tanks.Count,"repeatable fixed-step execution");
            for(int stage=1;stage<=35;stage++){var text=Resources.Load<TextAsset>("Maps/"+stage.ToString("00"));check(text!=null,"map "+stage);var sim=new BattleSimulation(JsonConvert.DeserializeObject<MapData>(text.text),stage);check(sim.Width>=512&&sim.Height>=512,"map bounds "+stage);}
            Debug.Log("[BattleCities] PASS: "+count+" simulation and map checks. Original-engine scenario comparison remains a separate migration check.");
        }
    }
}
