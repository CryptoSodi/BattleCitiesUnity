using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using BattleCities.Core;
using TerrainData=BattleCities.Core.TerrainData;
namespace BattleCities
{
    public static class LandDroneChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("LAND DRONE: "+message);}
        static BattleSimulation Sim()
        {
            var map=new MapData{field=new FieldData{widthTiles=20,heightTiles=20},terrain=new TerrainData{regions=Array.Empty<Region>()},spawn=new SpawnData{player=new SpawnGroup{locations=new[]{new Point{x=568,y=568}}},enemy=new SpawnGroup{locations=new[]{new Point{x=0,y=0}},list=new[]{new EnemySpec()}}},@base=new BaseData{x=1120,y=1120}};
            var sim=new BattleSimulation(map){Freeze=1000,DisableEnemyFire=true};
            Step(sim,140);foreach(var enemy in sim.Tanks.Where(t=>!t.Player)){enemy.X=-10000;enemy.Y=-10000;}sim.EquippedSecondary=SecondaryAttack.LandDrone;return sim;
        }
        static void Step(BattleSimulation sim,int count){for(int i=0;i<count;i++)sim.Step(default);}
        [MenuItem("Battle Cities/Validate Land Drone")]
        public static void Run()
        {
            var sim=Sim();Check(sim.UseSecondary(),"can deploy");
            var drone=sim.LandDrones.Single();
            Check(!drone.Bounds.Overlaps(sim.Player.Bounds),"unoccupied spawn");
            Step(sim,241);Check(!sim.UseSecondary(),"one active per player");
            sim=Sim();sim.Terrain.Add(new Wall{Type="water",Bounds=new Box(400,400,400,400)});
            Check(!sim.UseSecondary()&&sim.LandDrones.Count==0,"no deployment on blocked terrain");
            foreach(var terrain in new[]{"water","steel","brick","bush","grass"})
            {
                sim=Sim();Check(sim.UseSecondary(),"terrain placement");drone=sim.LandDrones.Single();
                sim.Player.X=400;sim.Player.Y=400;drone.X=600;drone.Y=600;
                var target=new TankState{Id=9000,X=840,Y=600,Health=4};sim.Tanks.Add(target);Face(drone,target);
                var wall=new Wall{Type="steel",Bounds=new Box(710,540,32,120)};sim.Terrain.Add(wall);
                Step(sim,1);Check(drone.TargetId==0&&drone.Distance>0,"cover blocks acquisition "+terrain);
                wall.Alive=false;Face(drone,target);Step(sim,31);Check(drone.TargetId==target.Id,"acquires visible enemy");
                wall.Type=terrain;wall.Alive=true;
                bool detour=false;int detonations=0;sim.LandDroneExploded+=d=>detonations++;
                for(int i=0;i<240&&drone.Alive;i++)
                {
                    sim.Step(default);Check(!drone.Bounds.Overlaps(wall.Bounds),"route avoids "+terrain);
                    detour|=Math.Abs(drone.Y-600)>80;
                }
                Check(detour&&!target.Alive&&detonations==1,"replan around "+terrain+" and detonate once");
                Check(sim.Player.Health==sim.Player.MaxHealth&&sim.BaseAlive,"no friendly/base damage");
            }
            sim=Sim();Check(sim.UseSecondary(),"player obstacle placement");drone=sim.LandDrones.Single();
            drone.X=500;drone.Y=600;sim.Player.X=620;sim.Player.Y=600;
            var victim=new TankState{Id=9100,X=800,Y=600,Health=4};sim.Tanks.Add(victim);Face(drone,victim);
            bool wentAround=false;
            for(int i=0;i<240&&drone.Alive;i++)
            {
                sim.Step(default);
                Check(!drone.Bounds.Overlaps(sim.Player.MovementBounds),"never pushes or crosses player");
                wentAround|=Math.Abs(drone.Y-600)>55;
            }
            Check(wentAround&&!victim.Alive,"goes around stationary player");
            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
            for(int hit=0;hit<2;hit++)
            {
                sim.Shots.Add(new ShotState{Id=9300+hit,Player=false,X=drone.X,Y=drone.Y-40,Direction=Facing.Down,Speed=600,Damage=1});
                Step(sim,3);Check(hit==0?drone.Alive:!drone.Alive,"two enemy bullet hits");
            }
            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();sim.LandDroneSettings.RouteTimeout=.2f;sim.Terrain.Add(new Wall{Type="steel",Bounds=new Box(0,0,sim.Width,sim.Height)});
            Step(sim,20);Check(!drone.Alive,"no target timeout");
            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();sim.LandDroneSettings.Lifetime=.2f;
            Step(sim,20);Check(!drone.Alive,"lifetime");

            // A 58-unit vehicle fits a 64-unit corridor. Offset grid nodes used to
            // land at 600/616 instead of its valid center at 608 and reject it.
            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
            sim.Player.X=400;sim.Player.Y=400;drone.X=608;drone.Y=400;
            sim.Terrain.Add(new Wall{Type="steel",Bounds=new Box(512,320,64,580)});
            sim.Terrain.Add(new Wall{Type="steel",Bounds=new Box(640,320,64,580)});
            var corridorEnemy=new TankState{Id=9500,X=608,Y=720,Health=4};sim.Tanks.Add(corridorEnemy);Face(drone,corridorEnemy);
            for(int i=0;i<240&&drone.Alive;i++)
            {
                sim.Step(default);
                Check(sim.LandDroneGroundFree(drone.Bounds),"stays inside one-tile corridor");
            }
            Check(!corridorEnemy.Alive&&drone.Distance>200,"navigates one-tile corridor");
            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
            Check(sim.LandDroneSettings.Lifetime==60&&sim.LandDroneSettings.RouteTimeout==60,"60-second defaults");
            Step(sim,3540);Check(drone.Alive,"keeps searching after 59 seconds without target");
            Step(sim,62);Check(!drone.Alive,"expires at one minute");


            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
            var startX=drone.X;var startY=drone.Y;float maxTravel=0;
            for(int i=0;i<600;i++)
            {
                sim.Step(default);
                Check(drone.Alive&&sim.LandDroneGroundFree(drone.Bounds),"patrol avoids player and terrain");
                maxTravel=Math.Max(maxTravel,Math.Abs(drone.X-startX)+Math.Abs(drone.Y-startY));
            }
            Check(drone.TargetId==0&&drone.Distance>600&&maxTravel>300&&drone.PatrolledTiles.Count>8,"patrol explores stage without enemy");
            var patrolTarget=new TankState{Id=9600,X=drone.X,Y=drone.Y>640?drone.Y-160:drone.Y+160,Health=4};
            sim.Tanks.Add(patrolTarget);Face(drone,patrolTarget);Step(sim,180);
            Check(!patrolTarget.Alive,"patrol switches to visible enemy pursuit");


            foreach(Facing facing in Enum.GetValues(typeof(Facing)))
            {
                sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
                BattleSimulation.Vector(facing,out var dx,out var dy);
                sim.Player.X=600;sim.Player.Y=600;drone.X=600+dx*100;drone.Y=600+dy*100;
                bool gaveSpace=false;
                for(int i=0;i<100;i++)
                {
                    sim.Step(new Command{Move=facing});
                    Check(!drone.Bounds.Overlaps(sim.Player.MovementBounds),"yield never overlaps player "+facing);
                    gaveSpace|=Math.Abs((drone.X-600)*dy-(drone.Y-600)*dx)>60;
                }
                float progress=(sim.Player.X-600)*dx+(sim.Player.Y-600)*dy;
                Check(gaveSpace&&progress>240,"player passes yielding drone "+facing);
                float before=drone.Distance;Step(sim,90);
                Check(drone.YieldRemaining==0&&drone.Distance>before,"resumes patrol after yielding");
            }
            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
            sim.Player.X=608;sim.Player.Y=700;drone.X=608;drone.Y=600;
            sim.Terrain.Add(new Wall{Type="steel",Bounds=new Box(512,0,64,1280)});
            sim.Terrain.Add(new Wall{Type="steel",Bounds=new Box(640,0,64,1280)});
            for(int i=0;i<100;i++)
            {
                sim.Step(new Command{Move=Facing.Up});
                Check(!drone.Bounds.Overlaps(sim.Player.MovementBounds)&&sim.LandDroneGroundFree(drone.Bounds),"retreat safely in narrow corridor");
            }
            Check(sim.Player.Y<440&&drone.Y<400,"backs away to give player room");

            sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
            Step(sim,60);
            float patrolLimit=sim.Player.Speed*sim.LandDroneSettings.PatrolSpeedMultiplier;
            Check(drone.TargetId==0&&drone.Speed>0&&drone.Speed<=patrolLimit+.01f,"slow patrol speed");
            var speedTarget=new TankState{Id=9700,X=drone.X+400,Y=drone.Y,Health=4};
            sim.Tanks.Add(speedTarget);Face(drone,speedTarget);Step(sim,60);
            Check(drone.TargetId==speedTarget.Id&&drone.Speed>patrolLimit*2,"accelerates after target lock");
            speedTarget.Alive=false;Step(sim,32);
            Check(drone.TargetId==0&&drone.Speed<=patrolLimit+.01f,"returns to slow roaming after target lost");
            foreach(float heading in new[]{0f,90f,180f,270f})
            foreach(float offset in new[]{0f,44f,-44f,46f,-46f,90f,180f})
            {
                sim=Sim();sim.UseSecondary();drone=sim.LandDrones.Single();
                sim.Player.X=300;sim.Player.Y=300;drone.X=640;drone.Y=640;drone.Heading=heading;
                float radians=(heading+offset)*Mathf.Deg2Rad;
                var sightTarget=new TankState{Id=9800,X=drone.X+Mathf.Sin(radians)*200,Y=drone.Y-Mathf.Cos(radians)*200,Health=4};
                sim.Tanks.Add(sightTarget);Step(sim,1);
                Check((drone.TargetId==sightTarget.Id)==(Math.Abs(offset)<45),"front sight cone "+heading+" offset "+offset);
                if(Math.Abs(offset)>=45)
                {
                    Face(drone,sightTarget);Step(sim,1);
                    Check(drone.TargetId==sightTarget.Id,"acquire only after facing enemy");
                }
            }
            VisualChecks();
            Debug.Log("LAND DRONE PASS: placement, limit, terrain/visibility, replanning, player avoidance, hostile impact, two-hit health, expiry and visuals.");
        }
        static void Face(LandDroneState drone,TankState target)
        {
            drone.Heading=Mathf.Atan2(target.X-drone.X,drone.Y-target.Y)*Mathf.Rad2Deg;
            drone.Repath=0;
        }
        static void VisualChecks()
        {
            var prefab=Resources.Load<GameObject>("Deployables/LandAttackDrone");
            Check(prefab,"resource prefab exists");var root=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view=root.GetComponent<LandAttackDroneView>();view.Initialize();
                Check(view.Drive&&view.Idle,"both imported clips");
                Check(root.transform.localScale==Vector3.one&&view.Visual.transform.localScale==Vector3.one,"true scale");
                Check(!view.Visual.GetComponentsInChildren<Collider>().Any(),"separate gameplay collider");
                Check(root.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader&&!m.shader.name.Contains("Unlit"))),"lit PBR materials");
                var state=new LandDroneState{Distance=32,Age=1};view.Tick(state);
                var wheels=view.Visual.GetComponentsInChildren<Transform>().Where(t=>new[]{"Wheel_FL","Wheel_FR","Wheel_RL","Wheel_RR"}.Contains(t.name)).ToArray();
                Check(wheels.Length==4,"four wheels");var angles=wheels.Select(w=>w.localRotation).ToArray();
                state.Age+=1;view.Tick(state);
                Check(wheels.Select((w,i)=>Quaternion.Angle(w.localRotation,angles[i])<.001f).All(x=>x),"no stationary wheel spin");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
