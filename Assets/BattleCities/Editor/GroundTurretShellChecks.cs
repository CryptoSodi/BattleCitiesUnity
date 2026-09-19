using System;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEditor;
using TerrainData=BattleCities.Core.TerrainData;

namespace BattleCities
{
    public static class GroundTurretShellChecks
    {
        const string AssetPath="Assets/BattleCities/Art/Deployables/GroundTurretShell/ground-turret.glb";
        const string PrefabPath="Assets/BattleCities/Prefabs/GroundTurret.prefab";
        [MenuItem("Battle Cities/Validate Armored Shell Turret")]
        public static void Run()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab,"prefab");
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            var clips=AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<AnimationClip>().ToArray();
            Check(new[]{"Deploy","Undeploy","GunFire","ReloadCycle","TurretCardinal4Way"}.All(n=>clips.Any(c=>c.name==n)),"all five clips preserved");
            var root=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view=root.GetComponent<GroundTurret>();view.Initialize();
                var state=new TurretState();
                view.Tick(state,0);
                Check(root.transform.position==Vector3.zero&&root.transform.localScale==Vector3.one,"root at Y=0 scale 1");
                var visual=root.transform.Find("VisualModel");
                var conversion=Find(root,"GroundTurret_Root");
                var sourceConversion=Find(imported,"GroundTurret_Root");
                Check(Vector3.Distance(visual.localScale,Vector3.one*TurretState.ModelScale)<.00001f&&conversion.localScale==sourceConversion.localScale,"imported conversion preserved");
                Check(root.GetComponentsInChildren<Light>(true).Length==imported.GetComponentsInChildren<Light>(true).Length,"embedded lights preserved");
                Check(!visual.GetComponentsInChildren<Collider>(true).Any(),"separate colliders");
                var originalMaterials=imported.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                Check(root.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m&&originalMaterials.Contains(m))),"original PBR materials");
                Check(originalMaterials.All(m=>m.shader&&!m.shader.name.ToLowerInvariant().Contains("unlit")),"day/night lit materials");
                Check(HighestVertex(root)<=.003f,"stowed geometry flush with ground");
                Check(Find(root,"StowedGround").GetComponent<Collider>().enabled,"flat ground when stowed");
                for(int cycle=0;cycle<3;cycle++)
                {
                    state.Phase=TurretPhase.Deploying;state.TransitionTime=view.DeployDuration*.5f;view.Tick(state,.016f);
                    Check(Find(root,"TransitionClearance").GetComponent<Collider>().enabled,"transition blocks");
                    Check(root.GetComponentsInChildren<Light>(true).All(l=>!l.enabled),"no lights while deploying");
                    state.Phase=TurretPhase.Deployed;state.Deployment=1;state.TransitionTime=view.DeployDuration;view.Tick(state,.016f);
                    var top=HighestVertex(root);Check(top>.2f,"head deployed");
                    view.Tick(state,10);Check(Mathf.Abs(HighestVertex(root)-top)<.0001f,"deployed pose held");
                    var baseRotation=Find(root,"Base_Static").localRotation;
                    foreach(Facing facing in Enum.GetValues(typeof(Facing)))
                    {
                        state.Heading=facing;state.TurnRemaining=0;view.Tick(state,.2f);
                        var yaw=Find(root,"TurretYaw");Check(Mathf.Abs(Mathf.DeltaAngle(yaw.localEulerAngles.y,(int)facing*90))<.001f,"exact cardinal "+facing);
                        Check(Quaternion.Angle(baseRotation,Find(root,"Base_Static").localRotation)<.001f,"fixed base");
                        var direction=Quaternion.Euler(0,(int)facing*90,0)*Vector3.forward;
                        var muzzle=view.MuzzlePosition;var planar=new Vector3(muzzle.x,0,muzzle.z);
                        Check(Vector3.Dot(planar.normalized,direction)>.999f,"muzzle matches heading");
                        Check(Find(root,"AttackRange_Light").IsChildOf(yaw),"light follows yaw");
                    }
                    state.FireSequence++;state.ShotAge=.08f;state.ReloadRemaining=view.ReloadDuration;view.Tick(state,.01f);Nuts(root,0);
                    for(int i=1;i<=4;i++){state.ShotAge=2;state.ReloadRemaining=i==4?0:view.ReloadDuration*(1-(i+.1f)/4);view.Tick(state,.01f);Nuts(root,i);}
                    state.Phase=TurretPhase.Retracting;state.TransitionTime=view.DeployDuration*.5f;view.Tick(state,.01f);
                    Check(Find(root,"TransitionClearance").GetComponent<Collider>().enabled,"retraction still blocked");
                    Check(root.GetComponentsInChildren<Light>(true).All(l=>!l.enabled),"no lights while retracting");
                    state.Phase=TurretPhase.Stowed;state.Deployment=0;view.Tick(state,.01f);
                    Check(HighestVertex(root)<=.003f,"repeat retraction is flush");
                    view.Tick(state,20);Check(HighestVertex(root)<=.003f,"stowed endpoint held");
                }
                SimulationChecks(view);
                SightChecks(view);
                ProximityChecks(view);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            Debug.Log("ARMORED SHELL PASS: scale/root, original materials/lights/clips, endpoints and repeated playback, cardinal muzzle headings, reload sequence, clearance, transition blocking, crossing, combat and destruction.");
        }
        static BattleSimulation Sim(GroundTurret settings)
        {
            var map=new MapData{field=new FieldData{widthTiles=20,heightTiles=20},terrain=new TerrainData{regions=Array.Empty<Region>()},spawn=new SpawnData{player=new SpawnGroup{locations=new[]{new Point{x=568,y=568}}},enemy=new SpawnGroup{locations=new[]{new Point{x=0,y=0}},list=new[]{new EnemySpec()}}},@base=new BaseData{x=1120,y=1120}};
            var sim=new BattleSimulation(map){Freeze=1000,DisableEnemyFire=true};
            sim.ConfigureTurrets(settings.AttackRange*64,settings.Damage,settings.FireCooldown,settings.ReloadDuration,settings.CardinalTurnDuration,settings.MuzzleDistance*64,settings.Health,settings.DeployDuration);
            Step(sim,122);sim.EquippedSecondary=SecondaryAttack.GroundTurret;return sim;
        }
        static void SimulationChecks(GroundTurret view)
        {
            var map=Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text);
            var real=new BattleSimulation(map){Freeze=1000,DisableEnemyFire=true};
            real.ConfigureTurrets(192,1,1.2f,4.033333f,.18f,view.MuzzleDistance*64,4,view.DeployDuration);
            Step(real,122);real.Player.X=479;real.Player.Y=706;real.EquippedSecondary=SecondaryAttack.GroundTurret;
            Check(real.UseSecondary(),"near-wall placement fits mine-sized shell");
            var placed=real.Turrets.Single();
            Check(placed.X==real.Player.X&&placed.Y==real.Player.Y,"spawns exactly under player");
            Step(real,200);Check(placed.Phase==TurretPhase.Stowed,"stays stowed beneath tank");
            real.Player.X+=230;Check(real.TurretClearanceEmpty(placed),"clear after tank leaves");
            Step(real,200);Check(placed.Active,"player outside safety radius allows deployment");
            var blocked=Sim(view);
            blocked.AddRegion("steel",0,0,blocked.Width,blocked.Height);
            Check(!blocked.UseSecondary()&&blocked.Turrets.Count==0&&!string.IsNullOrEmpty(blocked.SecondaryStatus),"blocked site reports reason without consuming a slot");
            var sim=Sim(view);Check(sim.UseSecondary(),"place stowed turret");
            var turret=sim.Turrets.Single();var player=sim.Player;turret.X=player.X;turret.Y=player.Y;
            Step(sim,240);Check(turret.Phase==TurretPhase.Stowed&&turret.FireSequence==0,"does not deploy under player");
            player.X=900;player.Y=900;
            var visitor=new TankState{Id=9001,X=turret.X+50,Y=turret.Y,Health=1000};
            sim.Tanks.Add(visitor);Step(sim,200);Check(turret.Phase==TurretPhase.Stowed,"clearance vehicle blocks deployment");
            visitor.X=1000;visitor.Y=500;
            Step(sim,1);Check(turret.Phase==TurretPhase.Deploying,"start deploy after clearance");
            Check(!sim.CanTankOccupy(player,turret.X,turret.Y)&&!sim.CanTankOccupy(visitor,turret.X,turret.Y),"both tanks blocked during deploy");
            Step(sim,200);Check(turret.Active,"finish deployment");
            player.X=turret.X+80;player.Y=turret.Y;
            sim.Step(new Command{Move=Facing.Left});Check(turret.Phase==TurretPhase.Retracting,"approach requests retract");
            for(int i=0;i<100;i++)sim.Step(new Command{Move=Facing.Left});Check(!sim.CanTankOccupy(player,turret.X,turret.Y)&&turret.FireSequence==0,"cannot cross mid-retraction");
            for(int i=0;i<100;i++)sim.Step(new Command{Move=Facing.Left});Check(turret.Phase==TurretPhase.Stowed,"retraction finished");
            player.X=turret.X+80;
            Check(sim.CanTankOccupy(player,turret.X,turret.Y)&&sim.CanTankOccupy(visitor,turret.X,turret.Y),"both teams cross stowed tile");
            player.X=turret.X;Step(sim,240);Check(turret.Phase==TurretPhase.Stowed,"holds under crossing player");
            player.X=900;player.Y=900;
            var wall=new Wall{Id=9003,Type="steel",Bounds=new Box(turret.X+20,turret.Y-8,4,16)};sim.Terrain.Add(wall);
            Step(sim,220);Check(turret.Phase==TurretPhase.Stowed,"new terrain blocks surrounding clearance");
            wall.Alive=false;Step(sim,210);Check(turret.Active,"redeploy once empty");
            foreach(Facing facing in Enum.GetValues(typeof(Facing)))
            {
                var combat=Sim(view);Check(combat.UseSecondary(),"combat place");
                var gun=combat.Turrets.Single();combat.Player.X=1000;combat.Player.Y=1000;
                BattleSimulation.Vector(facing,out var dx,out var dy);
                var enemy=new TankState{Id=9100,X=gun.X+dx*160,Y=gun.Y+dy*160,Health=1000};
                combat.Tanks.Add(enemy);int fired=0;
                combat.TurretFired+=t=>{var shot=combat.Shots.Last();Check(shot.Direction==facing,"shot heading");Check(Mathf.Abs(shot.X-(gun.X+dx*view.MuzzleDistance*64))<.01f&&Mathf.Abs(shot.Y-(gun.Y+dy*view.MuzzleDistance*64))<.01f,"authoritative muzzle");fired++;};
                Step(combat,850);Check(fired>=3&&enemy.Health<1000,"repeated firing damages target");
                Check(gun.FireSequence==fired,"single authoritative fire sequence");
            }
            sim.Shots.Add(new ShotState{Id=9991,Player=false,X=turret.X,Y=turret.Y-45,Direction=Facing.Down,Speed=600,Damage=20});
            Step(sim,3);Check(!sim.Turrets.Any(),"enemy destroys deployed turret");
        }

        static void SightChecks(GroundTurret view)
        {
            Check(Mathf.Approximately(view.AttackRange,3),"three tile range");
            foreach(Facing facing in Enum.GetValues(typeof(Facing)))
            foreach(string cover in new[]{"brick","steel","base","diagonal","range","grass","water"})
            {
                var sim=Sim(view);Check(sim.UseSecondary(),"sight placement");
                var gun=sim.Turrets.Single();sim.Player.X=400;sim.Player.Y=400;
                BattleSimulation.Vector(facing,out var dx,out var dy);
                var enemy=new TankState{Id=9200,X=gun.X+dx*160,Y=gun.Y+dy*160,Health=1000};
                sim.Tanks.Add(enemy);
                var wall=new Wall{Id=9201,Type=cover,Bounds=new Box(gun.X+dx*80-12,gun.Y+dy*80-12,24,24)};
                if(cover=="base"){gun.X=sim.BaseBounds.X+sim.BaseBounds.W/2-dx*80;gun.Y=sim.BaseBounds.Y+sim.BaseBounds.H/2-dy*80;enemy.X=gun.X+dx*160;enemy.Y=gun.Y+dy*160;}
                else if(cover=="diagonal"){enemy.X+=dy*80;enemy.Y+=dx*80;}
                else if(cover=="range"){enemy.X=gun.X+dx*193;enemy.Y=gun.Y+dy*193;}
                else sim.Terrain.Add(wall);
                Step(sim,400);
                bool passThrough=cover=="grass"||cover=="water";
                Check(passThrough?gun.FireSequence>0:gun.FireSequence==0,"cardinal sight "+facing+" "+cover);
                if(cover=="brick"||cover=="steel")
                {
                    wall.Alive=false;Step(sim,100);
                    Check(gun.FireSequence>0,"acquire after cover removed");
                    sim.Shots.Clear();wall.Alive=true;int previous=gun.FireSequence;
                    Step(sim,400);Check(gun.FireSequence==previous,"drop blocked retained target");
                }
            }
        }

        static void ProximityChecks(GroundTurret view)
        {
            var sim=Sim(view);Check(sim.UseSecondary(),"proximity placement");
            var gun=sim.Turrets.Single();var player=sim.Player;
            player.X=gun.X+150;
            Step(sim,600);Check(gun.Phase==TurretPhase.Stowed,"stationary player in radius keeps stowed");
            player.X=gun.X+210;Step(sim,240);Check(gun.Active,"deploy after leaving radius");
            player.X=gun.X+190;Step(sim,1);Check(gun.Phase==TurretPhase.Retracting,"entry retracts without movement input");
            Step(sim,600);Check(gun.Phase==TurretPhase.Stowed&&gun.FireSequence==0,"no repeated deployment near player");
            player.X=gun.X+196;Step(sim,300);Check(gun.Phase==TurretPhase.Stowed,"boundary exit margin prevents cycling");
            player.X=gun.X+210;Step(sim,50);Check(gun.Phase==TurretPhase.Deploying,"deployment starts outside margin");
            player.X=gun.X+150;Step(sim,1);Check(!gun.Active&&gun.Phase!=TurretPhase.Deploying,"entry interrupts deployment");
            Step(sim,600);Check(gun.Phase==TurretPhase.Stowed,"interrupted deployment remains stowed");
            player.X=gun.X+210;Step(sim,240);Check(gun.Active,"redeploy after player leaves again");
        }
        static void Step(BattleSimulation sim,int ticks){for(int i=0;i<ticks;i++)sim.Step(default);}
        static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static float HighestVertex(GameObject root)
        {
            float max=float.MinValue;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>()&&f.GetComponent<Renderer>().enabled))
                foreach(var v in f.sharedMesh.vertices)max=Mathf.Max(max,f.transform.TransformPoint(v).y);
            return max;
        }
        static void Nuts(GameObject root,int count){for(int i=1;i<=4;i++)Check(Find(root,"Reload_"+i+"_Green").gameObject.activeSelf==(i<=count)&&Find(root,"Reload_"+i+"_Red").gameObject.activeSelf==(i>count),"nut "+i);}
        static void Check(bool okay,string label){if(!okay)throw new Exception("ARMORED SHELL FAILED: "+label);}
    }
}
