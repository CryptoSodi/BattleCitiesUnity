using System;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleCities
{
    public static class BattlePresentationChecks
    {
        static void Check(bool value,string description)
        {if(!value)throw new Exception("BATTLE PRESENTATION: "+description);}

        [MenuItem("Battle Cities/Validate Combat Presentation")]
        public static void Run()
        {
            Check(EditorApplication.isPlaying,"Run in gameplay Play mode");
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            Check(game!=null,"Gameplay exists");
            int stage=game.Stage;bool wasPaused=game.Paused;
            var vfx=game.GetComponent<BattleCombatVfx>();
            var debris=game.GetComponent<BattleDebris>();
            try
            {
                game.LoadStage(1);game.Paused=true;
                vfx.Clear();
                Check(vfx.Muzzle(Vector3.up,Vector3.forward,.55f),"Authored muzzle asset is connected");
                for(int i=0;i<25;i++)vfx.Tick(.016f);
                Check(vfx.ActiveEffects==0,"Short muzzle flashes return to the pool within .4 seconds");
                vfx.Burst(Vector3.up,1);
                vfx.Tick(.06f);
                Check(vfx.ActiveEffects==1,"Explosion remains active for its delayed emitters");
                vfx.Tick(.70f);
                var smoke=game.GetComponentInChildren<BattleBlastSmoke>();
                Check(smoke&&smoke.ActiveParticles>0,"Blasts leave a visible smoke plume after the fire pulse");
                var system=smoke.GetComponent<ParticleSystem>();
                var particles=new ParticleSystem.Particle[128];
                int count=system.GetParticles(particles);
                float height=particles.Take(count).Average(p=>p.position.y);
                var position=particles[0].position;float lifetime=particles[0].remainingLifetime;
                vfx.Tick(0);system.GetParticles(particles);
                Check(particles[0].position==position&&particles[0].remainingLifetime==lifetime,"Pause freezes smoke position and lifetime");
                vfx.Tick(.4f);count=system.GetParticles(particles);
                Check(particles.Take(count).Average(p=>p.position.y)>height+.2f,"Smoke rises visibly from the blast location");
                vfx.Clear();
                Check(smoke.ActiveParticles==0&&smoke.ActivePlumes==0,"Clearing a stage removes particles and pending smoke bursts");
                var sim=game.Simulation;sim.Freeze=999;sim.DisableEnemyFire=true;
                for(int i=0;i<122;i++)sim.Step(default);
                sim.Terrain.Clear();sim.Tanks.RemoveAll(t=>!t.Player);
                sim.Player.X=400;sim.Player.Y=400;sim.Player.Aim=Facing.Up;sim.Player.Cooldown=0;
                var target=new TankState{Id=-92410,Tier=3,Health=6,X=400,Y=280};sim.Tanks.Add(target);
                vfx.Clear();debris.Clear();
                Check(sim.Fire(sim.Player),"Normal test round fires");
                for(int i=0;i<15;i++)sim.Step(default);
                Check(target.Alive&&target.Health==5,"A surviving armor hit retains normal damage");
                Check(vfx.ActiveSparks>0,"Surviving armor hit produces sparks");
                Check(debris.ActiveCount>0,"Surviving armor hit produces shrapnel");
                Check(vfx.ActiveSmoke==0,"Normal bullet impacts retain the approved fragmentation without added blast smoke");
                int sparkCount=vfx.ActiveSparks;
                vfx.Tick(0);
                Check(vfx.ActiveSparks==sparkCount,"Pause retains particle state");
                for(int i=0;i<100;i++)vfx.Impact(new Vector3(5,.5f,-5),Vector3.forward,i%3==0,1);
                Check(vfx.ActiveSparks<=384&&vfx.ActiveEffects<=24,"Burst load respects particle and instance caps");
                vfx.Tick(.8f);
                Check(smoke.ActiveParticles<=128&&smoke.ActivePlumes<=16,"Smoke load remains bounded during repeated blasts");
                for(int i=0;i<310;i++)vfx.Tick(1f/60);
                Check(vfx.ActiveEffects==0&&vfx.ActiveSparks==0&&vfx.ActiveSmoke==0,"Effects and lingering smoke expire and return to the pool");
                vfx.Clear();debris.Clear();
                Check(vfx.ActiveEffects==0&&debris.ActiveCount==0,"Stage cleanup clears cosmetic state");
                var camera=game.GetComponentInChildren<Camera>();
                var data=camera.GetUniversalAdditionalCameraData();
                Check(camera.allowHDR&&data.renderPostProcessing&&(data.volumeLayerMask.value&1)!=0,"Battle camera enables the lighting profile");
                var profile=Resources.Load<VolumeProfile>("BattleLook");
                Check(profile&&profile.TryGet<Bloom>(out var bloom)&&bloom.intensity.overrideState,"Bloom profile is referenced and enabled");
                ValidateWater();
                ValidateWaterJunction();
                BattleWaterBasinChecks.Run();
                Debug.Log("BATTLE PRESENTATION PASS: surviving armor impact, sparks, shrapnel, pause, bounded burst load, pool expiry, cleanup and camera/profile wiring.");
            }
            finally{game.LoadStage(stage);game.Paused=wasPaused;}
        }

        static void ValidateWater()
        {
            var assets=Resources.Load<BattleVisualAssets>("BattleVisualAssets");
            Check(assets&&assets.WaterMaterial,"Forest material is linked");
            Check(!ShaderUtil.ShaderHasError(assets.WaterMaterial.shader),"Water shader compiles");
            Check(assets.WaterMaterial.GetTexture("_BaseMap")!=null,"Forest texture is assigned");
            Check(assets.WaterMaterial.GetTexture("_RippleMap")!=null,"Cartoon white-ripple texture is assigned");
            int tested=0;
            for(int stage=1;stage<=35;stage++)
            {
                var map=Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/"+stage.ToString("00")).text);
                var sim=new BattleSimulation(map,stage);
                var water=sim.Terrain.Where(w=>w.Type=="water").ToArray();
                if(water.Length==0)continue;
                var root=new GameObject("Water validation "+stage);
                try
                {
                    root.AddComponent<BattleWaterSurface>().Build(water,assets.WaterMaterial);
                    var mesh=root.GetComponent<MeshFilter>().sharedMesh;
                    var points=mesh.vertices;var indices=mesh.triangles;
                    double area=0;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=points[indices[i]];var b=points[indices[i+1]];var c=points[indices[i+2]];
                        var cross=Vector3.Cross(b-a,c-a);Check(cross.y>0,"Water faces camera in stage "+stage);
                        area+=cross.magnitude*.5;
                        var center=(a+b+c)/3;
                        Check(water.Any(w=>center.x*64>=w.Bounds.X&&center.x*64<=w.Bounds.Right&&-center.z*64>=w.Bounds.Y&&-center.z*64<=w.Bounds.Bottom),"Water remains within its gameplay footprint");
                    }
                    double expected=water.Sum(w=>(double)w.Bounds.W*w.Bounds.H)/4096;
                    Check(Math.Abs(area-expected)<.001,"Joined water covers every tile exactly once in stage "+stage);
                    Check(root.GetComponentsInChildren<Collider>().Length==0,"Water adds no physics colliders");
                    tested++;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            Debug.Log("WATER PASS: "+tested+" water stages; exact coverage, upward faces, valid source texture/shader, no added colliders.");
        }

        static void ValidateWaterJunction()
        {
            var sim=new BattleSimulation(new MapData());sim.Terrain.Clear();
            sim.AddRegion("water",0,0,96,32);sim.AddRegion("water",32,32,32,64);
            var root=new GameObject("Water T-junction regression");
            try
            {
                root.AddComponent<BattleWaterSurface>().Build(sim.Terrain,Resources.Load<BattleVisualAssets>("BattleVisualAssets").WaterMaterial);
                var mesh=root.GetComponent<MeshFilter>().sharedMesh;
                var vertices=mesh.vertices;var shores=mesh.uv2;
                var junction=Enumerable.Range(0,vertices.Length).Where(i=>Mathf.Abs(vertices[i].x-.75f)<.001f&&Mathf.Abs(vertices[i].z+.5f)<.001f).ToArray();
                Check(junction.Length==1,"T-junction shares a single vertex across adjoining tiles");
                Check(Mathf.Abs(shores[junction[0]].x-.25f)<.001f,"Junction shading uses the nearest real shore, including concave corners");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
