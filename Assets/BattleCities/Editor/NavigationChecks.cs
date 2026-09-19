using System;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class NavigationChecks
    {
        static BattleSimulation Corridor(float height,float y)
        {
            var s=new BattleSimulation(new MapData());s.DisableEnemyFire=true;
            for(int i=0;i<130;i++)s.Step(new Command());
            var p=s.Player;s.Tanks.Clear();s.Tanks.Add(p);s.Terrain.Clear();
            p.X=64;p.Y=y;p.Direction=Facing.Up;
            s.AddRegion("brick",96,0,96,112);
            s.AddRegion("brick",96,112+height,96,400);
            return s;
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        [MenuItem("Battle Cities/Validate navigation")]
        public static void Validate()
        {
            var exact=Corridor(64,144);
            for(int i=0;i<25;i++)exact.Step(new Command{Move=Facing.Right});
            Check(exact.Player.X>96&&exact.Player.Y==144,"16-unit destruction gap inaccessible");
            var assisted=Corridor(64,140);
            for(int i=0;i<30;i++)assisted.Step(new Command{Move=Facing.Right});
            Check(assisted.Player.X>96&&assisted.Player.Y==144,"Corner assistance failed");
            var narrow=Corridor(63,144);
            for(int i=0;i<60;i++)narrow.Step(new Command{Move=Facing.Right});
            Check(narrow.Player.X<=64,"Tank entered undersized gap");
            var far=Corridor(64,120);
            for(int i=0;i<30;i++)far.Step(new Command{Move=Facing.Right});
            Check(far.Player.X==64&&far.Player.Y==120,"Assistance exceeded range");
            var stopped=assisted.Player.X;assisted.Step(new Command());
            Check(assisted.Player.X==stopped,"Tank moves without input");
            Debug.Log("PASS: exact 64-unit gap, gentle alignment, undersized gap blocking, limited assistance, stop on release.");
        }
    }
}
