using System;
using BattleCities.Core;
internal static class MineRegressionChecks
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);UnityEngine.Debug.Log("PASS "+name);}
 static BattleSimulation Ready(){var s=new BattleSimulation(new MapData());s.Terrain.Clear();s.Freeze=999;s.DisableEnemyFire=true;for(int i=0;i<122;i++)s.Step(default);s.Player.X=400;s.Player.Y=400;return s;}
 [UnityEditor.MenuItem("Battle Cities/Validate Mine Secondary Attack")] public static void Run()
 {
  var s=Ready();s.Step(new Command{Fire=true,SecondaryFire=true});Check(s.Mines.Count==1&&s.Shots.Count==1,"primary and secondary attacks can fire together");
  Check(!s.UseSecondary(),"cooldown blocks duplicate placement");s.Shots.Clear();
  int blasts=0,deaths=0,drops=0;s.MineDetonated+=m=>blasts++;s.TankDestroyed+=t=>deaths++;s.DropRequested+=()=>drops++;
  var enemy=new TankState{Id=999,X=400,Y=400,Tier=3,Health=4,Drop=true};s.Tanks.Add(enemy);
  for(int i=0;i<20;i++)s.Step(default);Check(enemy.Alive&&s.Mines.Count==1,"enemy cannot trigger a mine before arming");
  for(int i=0;i<35;i++)s.Step(default);Check(!enemy.Alive&&s.Mines.Count==0&&blasts==1&&deaths==1&&drops==1&&s.Score==400,"armed mine kills armored enemy exactly once with score and drop");
  Check(s.Player.Alive&&s.Lives==3,"owner survives the mine explosion");
  s=Ready();s.UseSecondary();for(int i=0;i<130;i++)s.Step(default);
  Check(s.Mines.Count==1&&s.Mines[0].Armed&&s.Player.Alive,"player can stay on or drive over armed mine");Check(!s.UseSecondary(),"cannot stack mines in one spot");
  for(int n=1;n<5;n++){s.Player.X=400+n*48;Check(s.UseSecondary(),"place mine "+(n+1));for(int i=0;i<121;i++)s.Step(default);}
  s.Player.Y+=64;Check(!s.UseSecondary()&&s.Mines.Count==5,"active mine cap enforced");
  var fresh=Ready();Check(fresh.Mines.Count==0&&fresh.SecondaryCooldown==0,"new stage starts with a clean mine state");
  fresh.EquippedSecondary=SecondaryAttack.None;Check(!fresh.UseSecondary(),"unequipped secondary attack cannot fire");
  fresh.EquippedSecondary=SecondaryAttack.Mine;fresh.AddRegion("water",384,384,64,64);Check(!fresh.UseSecondary(),"cannot bury a mine in blocked terrain");
 }
}
