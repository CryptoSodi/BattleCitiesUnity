using System;
using BattleCities.Core;
internal static class DroneRegressionChecks
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);UnityEngine.Debug.Log("PASS "+name);}
 static BattleSimulation Ready(){var s=new BattleSimulation(new MapData());s.Terrain.Clear();s.Freeze=999;s.DisableEnemyFire=true;for(int i=0;i<122;i++)s.Step(default);s.Player.X=400;s.Player.Y=400;s.EquippedSecondary=SecondaryAttack.PatrolDrone;return s;}
 static void Tick(BattleSimulation s,int frames){for(int i=0;i<frames;i++)s.Step(default);}
 [UnityEditor.MenuItem("Battle Cities/Validate Patrol Drones")] public static void Run()
 {
  var s=Ready();Check(s.UseSecondary()&&s.Drones.Count==1&&s.Mines.Count==0,"equipped drone deploys independently of mines");Check(!s.UseSecondary(),"drone cooldown enforced");
  var d=s.Drones[0];Tick(s,30);Check(d.X==400&&d.Y==400,"arming delay prevents early pursuit");Tick(s,100);Check(d.X!=400||d.Y!=400,"drone patrols while idle");
  var outside=new TankState{Id=991,X=540,Y=400,Tier=3,Health=4};s.Tanks.Add(outside);Tick(s,100);Check(outside.Alive&&d.TargetId==0,"outside enemy ignored");
  float r=(d.X-d.AnchorX)*(d.X-d.AnchorX)+(d.Y-d.AnchorY)*(d.Y-d.AnchorY);Check(r<=128*128,"patrol remains inside deployment boundary");
  outside.X=450;s.Step(default);Check(d.TargetId==991,"enemy entering area is acquired");
  int explosions=0,kills=0,drops=0;s.DroneDetonated+=x=>explosions++;s.TankDestroyed+=x=>kills++;s.DropRequested+=()=>drops++;
  outside.X=600;outside.Drop=true;s.Step(default);Check(d.TargetId==991&&outside.Alive,"locked target remains committed outside patrol boundary");
  Tick(s,120);Check(!outside.Alive&&s.Drones.Count==0&&explosions==1&&kills==1&&drops==1&&s.Score==400,"committed drone destroys escaped armored target exactly once with score and drop");Check(s.Player.Alive&&s.Lives==3,"player immune to drone");
  s=Ready();for(int i=0;i<3;i++){Check(s.UseSecondary(),"deploy patrol drone "+i);Tick(s,241);}Check(!s.UseSecondary()&&s.Drones.Count==3,"three-drone limit enforced");Tick(s,2800);Check(s.Drones.Count==0,"untriggered drones expire after 45 seconds");
  s=Ready();s.EquippedSecondary=SecondaryAttack.Mine;Check(s.UseSecondary()&&s.Mines.Count==1&&s.Drones.Count==0,"buried mine remains available");
 }
}
