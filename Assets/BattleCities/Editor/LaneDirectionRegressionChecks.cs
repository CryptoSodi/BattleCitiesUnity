using System;
using BattleCities.Core;
internal static class LaneDirectionRegressionChecks
{
 [UnityEditor.MenuItem("Battle Cities/Validate Lane Directions")] public static void Run()
 {
  foreach(Facing direction in Enum.GetValues(typeof(Facing)))foreach(int offset in new[]{-3,3,-15,15})
  {
   var s=new BattleSimulation(new MapData());s.Terrain.Clear();s.Freeze=999;s.DisableEnemyFire=true;for(int i=0;i<122;i++)s.Step(default);
   bool horizontal=direction==Facing.Left||direction==Facing.Right;
   bool negative=direction==Facing.Left||direction==Facing.Up;
   if(horizontal){s.AddRegion("brick",448,128,64,160);s.AddRegion("brick",448,352,64,160);s.Player.X=negative?542:418;s.Player.Y=320+offset;}
   else{s.AddRegion("brick",128,448,160,64);s.AddRegion("brick",352,448,160,64);s.Player.X=320+offset;s.Player.Y=negative?542:418;}
   float before=horizontal?s.Player.X:s.Player.Y;
   for(int i=0;i<20;i++)s.Step(new Command{Move=direction});
   float after=horizontal?s.Player.X:s.Player.Y;
   if(negative?after>=before:after<=before)throw new Exception("Blocked entering "+direction+" offset "+offset);
   foreach(var w in s.Terrain)if(w.Alive&&w.Solid&&s.Player.MovementBounds.Overlaps(w.Bounds))throw new Exception("Wall penetration");
   UnityEngine.Debug.Log("PASS "+direction+" offset "+offset);
  }
 }
}
