using System;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class CameraRespawnChecks
    {
        public static void Run()
        {
            var sim=new BattleSimulation(new MapData{field=new FieldData{widthTiles=20,heightTiles=20},
                spawn=new SpawnData{player=new SpawnGroup{locations=new[]{new Point{x=128,y=960}}},enemy=new SpawnGroup{list=new[]{new EnemySpec()}}}});
            void Check(bool ok,string label){if(!ok)throw new Exception("CAMERA FOCUS: "+label);}
            sim.Player.X=500;sim.Player.Y=700;
            Check(BattleGame.CameraFocus(sim)==BattleGame.World(500,700),"living player followed");
            sim.Kill(sim.Player);
            Check(BattleGame.CameraFocus(sim)==BattleGame.World(160,992),"dead player focuses actual map spawn instead of map centre");
            for(int i=0;i<360&&sim.Player==null&&!sim.Lost;i++)sim.Step(default);
            Check(sim.Player!=null&&BattleGame.CameraFocus(sim)==BattleGame.World(sim.Player.X,sim.Player.Y),"respawn resumes player following");
            typeof(BattleSimulation).GetProperty("Lost").SetValue(sim,true);
            var home=sim.BaseBounds;
            Check(BattleGame.CameraFocus(sim)==BattleGame.World(home.X+home.W/2,home.Y+home.H/2),"game over focuses base even if player survives base destruction");
            var bounded=BattleGame.ClampCameraToStage(new Vector3(10,0,-19.5f),20,20,6,4);
            Check(bounded==new Vector3(10,0,-16),"base near bottom stops four units inside the stage");
            Check(bounded.z-4==-20,"bottom viewport edge matches bottom stage edge");
            bounded=BattleGame.ClampCameraToStage(new Vector3(-10,0,10),20,20,6,4);
            Check(bounded.x==6&&bounded.z==-4,"opposite faction and corner focus remain inside stage");
            bounded=BattleGame.ClampCameraToStage(new Vector3(40,0,-40),20,20,10,10);
            Check(bounded==new Vector3(10,0,-10),"full-stage viewport stays centered without overshooting");
            Debug.Log("CAMERA RESPAWN PASS: living follow, custom respawn, follow after respawn and game-over base focus.");
        }
    }
}
