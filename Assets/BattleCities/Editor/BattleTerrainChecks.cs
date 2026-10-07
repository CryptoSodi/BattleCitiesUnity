using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using BattleCities.Multiplayer;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class BattleTerrainChecks
    {
        static void Check(bool ok,string name){if(!ok)throw new Exception("TERRAIN: "+name);}
        static BattleSimulation Ready()
        {
            var s=new BattleSimulation(new MapData()){Freeze=99999,DisableEnemyFire=true};
            for(int i=0;i<122;i++)s.Step(default);
            var p=s.Player;s.Tanks.Clear();s.Tanks.Add(p);s.Terrain.Clear();p.X=256;p.Y=256;p.Shield=9999;
            return s;
        }
        static void Steps(BattleSimulation s,int count,Facing? move=null)
        {for(int i=0;i<count;i++)s.Step(new Command{Move=move});}

        [MenuItem("Battle Cities/Terrain/Validate terrain rules")]
        public static void Run()
        {
            foreach(var type in new[]{BattleTerrain.Water,BattleTerrain.Lava,BattleTerrain.MuddyWater})
            {
                var s=Ready();s.AddRegion(type,320,192,128,128);
                Steps(s,60,Facing.Right);
                Check(s.Player.X<=288.001f,type+" blocks a tank at the shore");
                Check(s.Terrain.All(w=>w.Solid&&!w.StopsBullet),type+" allows shots over its surface");
                Check(s.Player.Health==5,type+" does not damage a tank");
                var target=new TankState{Id=9001,X=496,Y=256,Health=3,FireDelay=999};s.Tanks.Add(target);
                s.Player.Aim=Facing.Right;s.Fire(s.Player);Steps(s,30);
                Check(target.Health==2,type+" bullet reaches a target across the basin");
            }
            var sand=Ready();sand.AddRegion("quicksand",192,192,384,384);var p=sand.Player;
            Steps(sand,360);Check(p.SinkDepth>=.999f&&p.Alive,"stationary tank sinks but survives");
            float x=p.X;Steps(sand,1,Facing.Right);
            Check(p.X>x&&p.X-x<1,"deep sand retains a slow escape movement");
            Steps(sand,180,Facing.Right);Check(p.SinkDepth<.07f,"sustained movement loosens deep sand");
            Check(p.X-x<180*3,"crossing sand requires more drive time");
            Steps(sand,240,Facing.Right);Check(!p.InQuicksand&&p.SinkDepth==0,"full recovery on firm ground");

            var blocked=Ready();blocked.AddRegion("quicksand",192,192,128,128);
            blocked.Player.X=288;blocked.AddRegion("steel",320,0,32,640);
            Steps(blocked,180,Facing.Right);
            Check(blocked.Player.SinkDepth>.5f&&blocked.Player.X==288,"holding against a wall cannot escape");
            Steps(blocked,180,Facing.Left);Check(blocked.Player.SinkDepth<.01f,"reverse direction escapes a wall-side trap");

            var edge=Ready();edge.AddRegion("quicksand",280,192,96,128);Steps(edge,180);
            Check(edge.Player.SinkDepth==0,"grazing the corner does not sink a grounded tank");
            var continuous=Ready();continuous.AddRegion("quicksand",0,192,768,128);Steps(continuous,100,Facing.Right);
            Check(continuous.Player.SinkDepth==0,"moving tank does not sink");
            var repeat=Ready();repeat.AddRegion("quicksand",0,192,768,128);Steps(repeat,100,Facing.Right);
            Check(repeat.Player.X==continuous.Player.X&&repeat.Player.SinkDepth==continuous.Player.SinkDepth,"deterministic movement");
            var ai=Ready();ai.AddRegion("quicksand",384,192,128,128);
            var enemy=new TankState{Id=9002,X=448,Y=256,FireDelay=999,AiState=3};ai.Tanks.Add(enemy);
            Steps(ai,360);Check(enemy.SinkDepth>.99f&&enemy.Alive,"enemy tanks use the same sinking rules");
            enemy.AiState=0;enemy.Direction=Facing.Down;ai.Freeze=0;Steps(ai,240);
            Check(enemy.SinkDepth<.01f&&!enemy.InQuicksand,"AI can drive out of sand");

            var net=NetTankState.From(new TankState{SinkDepth=.73f,InQuicksand=true,X=99,Y=91}).ToState();
            Check(net.InQuicksand&&Math.Abs(net.SinkDepth-.73f)<.0001f,"network sink state round-trip");
            var online=new BattleSimulation(new MapData()){Freeze=99999,DisableEnemyFire=true};
            online.Terrain.Clear();online.ConfigureMultiplayer(BattleMode.Versus);
            online.SetParticipant(0,true);online.SetParticipant(1,true);online.BeginMatch();
            for(int i=0;i<122;i++)online.StepMultiplayer(new Dictionary<int,Command>());
            var a=online.Tanks.Single(t=>t.Slot==0);var b=online.Tanks.Single(t=>t.Slot==1);
            a.X=256;a.Y=256;b.X=640;b.Y=640;online.AddRegion("quicksand",192,192,128,128);
            for(int i=0;i<180;i++)online.StepMultiplayer(new Dictionary<int,Command>());
            Check(a.SinkDepth>.5f&&b.SinkDepth==0,"independent multiplayer terrain contacts");
            for(int i=0;i<180;i++)online.StepMultiplayer(new Dictionary<int,Command>{{0,new Command{Move=Facing.Left}}});
            Check(a.SinkDepth==0&&!a.InQuicksand,"multiplayer escape");

            foreach(var type in new[]{"ice","grease"})
            {
                var slippery=Ready();slippery.AddRegion(type,192,192,384,128);Steps(slippery,1,Facing.Right);
                x=slippery.Player.X;Steps(slippery,15);
                Check(slippery.Player.X>x+20,type+" coasts after release");
                Steps(slippery,30);x=slippery.Player.X;Steps(slippery,5);
                Check(slippery.Player.X==x,type+" stops after slide expires");
            }
            var stage17=JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/17").text);
            Check(stage17.terrain.regions.Any(r=>r.type=="ice"),"stage 17 uses ice");
            var shader=Resources.Load<Shader>("BattleTerrainSurfaces");
            Check(shader&&!ShaderUtil.ShaderHasError(shader),"terrain shader compiles");
            ValidateGeometry();
            Debug.Log("TERRAIN PASS: blocked liquids, shots, sinking, recovery, corner contact, wall escape, determinism, replication, multiplayer, ice/grease, basin geometry and shaders.");
        }
        static void ValidateGeometry()
        {
            var sim=Ready();sim.Terrain.Clear();
            sim.AddRegion("lava",64,64,64,64);sim.AddRegion("muddyWater",192,64,64,64);sim.AddRegion("quicksand",320,64,64,64);
            var mesh=BattleGroundSurface.CreateMesh(new Rect(0,-4,8,4),-.025f,sim.Terrain);
            try
            {
                var v=mesh.vertices;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var center=(v[indices[i]]+v[indices[i+1]]+v[indices[i+2]])/3;
                    Check(!sim.Terrain.Any(w=>new Rect(w.Bounds.X/64,-w.Bounds.Bottom/64,w.Bounds.W/64,w.Bounds.H/64).Contains(new Vector2(center.x,center.z))),"ground cutout for every basin");
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [MenuItem("Battle Cities/Terrain/Play terrain sample",true)]
        static bool CanPreview()=>EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<BattleGame>();
        [MenuItem("Battle Cities/Terrain/Play terrain sample")]
        public static void Preview()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            if(!EditorApplication.isPlaying||!game)throw new InvalidOperationException("Enter Play mode in the battle scene first.");
            game.PreviewTerrainSample();
            Debug.Log("Terrain palette: top row water / lava / muddy water; lower row ice / grease / quicksand. Drive UP from spawn into quicksand, stop to sink, hold a direction to escape. Reload a stage to leave the sample.");
        }
    }
}
