using System;
using System.Collections.Generic;
using System.Reflection;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleCities.Editor
{
    public static class TurretAimChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            int checks = 0;
            void Check(bool valid, string message) { if(!valid) throw new Exception("TURRET AIM: " + message); checks++; }
            var root = new GameObject("Isolated turret aiming"); root.SetActive(false);
            InputActionMap input = null;
            try
            {
                var game = root.AddComponent<BattleGame>();
                typeof(BattleGame).GetMethod("ConfigureGameplayInput", Private).Invoke(game, null);
                input = (InputActionMap)typeof(BattleGame).GetField("input", Private).GetValue(game);
                var sim = new BattleSimulation(new MapData()) { DisableEnemyFire = true, Freeze = 999 };
                sim.Terrain.Clear(); for(int i=0;i<122;i++) sim.Step(default);
                typeof(BattleGame).GetProperty("Simulation").GetSetMethod(true).Invoke(game,new object[]{sim});
                var tank=sim.Player; tank.X=400; tank.Y=400; tank.Cooldown=0;
                typeof(BattleGame).GetField("chargingTankId", Private).SetValue(game,tank.Id);
                var charge=(ChargedFireInput)typeof(BattleGame).GetField("primaryCharge", Private).GetValue(game);
                var aim=(ShotAimLatch)typeof(BattleGame).GetField("shotAim", Private).GetValue(game);
                var shots=new List<ShotState>(); sim.ShotFired+=shot=>{if(shot.Owner==tank.Id)shots.Add(shot);};
                Command Queue(Facing movement)
                {
                    object[] args={new Command{Move=movement}};
                    typeof(BattleGame).GetMethod("QueuePrimaryCommand", Private).Invoke(game,args);
                    return (Command)args[0];
                }

                var command=Queue(Facing.Right); sim.Step(command);
                Check(!command.Aim.HasValue&&tank.Aim==Facing.Right,"idle aim follows movement");
                charge.Sample(true,false,true,.01f,true); aim.ObserveAim(Facing.Up);
                command=Queue(Facing.Right); sim.Step(command);
                Check(!command.Fire&&tank.Direction==Facing.Right&&tank.Aim==Facing.Up,"charging aim stays independent of movement");
                charge.Sample(false,true,false,.1f,true); aim.Release(null,null,Facing.Right,tank.Aim);
                Check(charge.HasPendingShot&&aim.Resolve(Facing.Left)==Facing.Up,"released direction outranks later aim changes");
                command=Queue(Facing.Right);
                Check(command.Fire&&!command.PowerShot&&command.Aim==Facing.Up,"normal firing command keeps release direction");
                sim.Step(command);
                Check(shots.Count==1&&shots[0].Direction==Facing.Up&&tank.Direction==Facing.Right,"normal shell actually leaves in release direction");
                command=Queue(Facing.Down); sim.Step(command);
                Check(!command.Fire&&!command.Aim.HasValue&&tank.Aim==Facing.Down,"next step follows movement even in the same render frame");

                tank.Cooldown=.5f;
                charge.Sample(true,false,true,.01f,false); aim.ObserveAim(Facing.Left);
                charge.Sample(false,false,true,1.1f,false);
                charge.Sample(false,true,false,.01f,false); aim.Release(null,null,Facing.Right,tank.Aim);
                command=Queue(Facing.Right); sim.Step(command);
                Check(!command.Fire&&command.Aim==Facing.Left&&charge.HasPendingShot,"charged release stays aimed while waiting for reload");
                Check(aim.Resolve(Facing.Down)==Facing.Left,"waiting shot cannot be redirected by new aim input");
                tank.Cooldown=0;
                command=Queue(Facing.Right); sim.Step(command);
                Check(command.Fire&&command.PowerShot&&shots.Count==2&&shots[1].Direction==Facing.Left,"charged shell fires left before movement can realign turret");
                command=Queue(Facing.Right); sim.Step(command);
                Check(!command.Aim.HasValue&&tank.Aim==Facing.Right,"turret follows movement after charged shot");

                tank.Cooldown=0;
                charge.Sample(true,true,false,.01f,true); aim.Release(null,Facing.Down,Facing.Right,tank.Aim);
                command=Queue(Facing.Right); sim.Step(command);
                Check(command.Fire&&shots.Count==3&&shots[2].Direction==Facing.Down,"same-frame touch tap/release retains its final aim");
                Check(!aim.Resolve(null).HasValue,"emitted shot leaves no persistent aim latch");

                charge.Sample(true,false,true,.01f,false); aim.ObserveAim(Facing.Left);
                charge.Sample(false,false,true,1.1f,false);
                charge.Sample(false,true,false,.01f,false); aim.Release(null,null,Facing.Down,tank.Aim);
                typeof(BattleGame).GetMethod("ResetPrimaryFire", Private).Invoke(game,null);
                Check(!charge.HasPendingShot&&!aim.Resolve(null).HasValue,"cancellation clears pending shot and aim together");
                command=Queue(Facing.Up);
                Check(!command.Fire&&!command.Aim.HasValue,"cancellation cannot emit a stale shot or stale direction");
                var fresh=new ShotAimLatch();
                fresh.Release(null,null,Facing.Right,Facing.Up);
                Check(fresh.Resolve(null)==Facing.Right,"tap without turret input uses movement at release");
                fresh.Reset(); fresh.Release(null,null,null,Facing.Left);
                Check(fresh.Resolve(null)==Facing.Left,"stationary shot uses the existing turret direction");
                fresh.Reset();
                Check(!fresh.Resolve(null).HasValue&&fresh.Resolve(Facing.Down)==Facing.Down,"new charge starts without stale aim and accepts explicit aim");
                Debug.Log("TURRET AIM CHECKS PASSED: " + checks);
            }
            finally { input?.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
            BattleControlChecks.Run();
        }
    }
}
