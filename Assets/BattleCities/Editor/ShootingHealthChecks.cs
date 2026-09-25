using System;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;

namespace BattleCities
{
    public static class ShootingHealthChecks
    {
        static void Check(bool condition,string label)
        { if(!condition)throw new Exception("SHOOTING/HEALTH: "+label); }

        static BattleSimulation Ready(float normalReloadSeconds=.12f,float upgradedNormalReloadSeconds=.08f)
        {
            var sim=new BattleSimulation(new MapData(),normalReloadSeconds:normalReloadSeconds,upgradedNormalReloadSeconds:upgradedNormalReloadSeconds){Freeze=999,DisableEnemyFire=true};
            sim.Terrain.Clear();
            for(int i=0;i<122;i++)sim.Step(default);
            sim.Player.X=400;sim.Player.Y=400;sim.Player.Aim=Facing.Up;
            return sim;
        }
        static void Step(BattleSimulation sim,int count)
        {for(int i=0;i<count;i++)sim.Step(default);}

        [MenuItem("Battle Cities/Validate Charged Shooting and Health")]
        public static void Run()
        {
            var input=new ChargedFireInput();
            Check(input.Tick(true,false,true,.016f)==null,"press begins charging without firing");
            Check(input.Tick(false,true,false,.08f)==false,"quick release produces small shot");
            Check(input.Tick(false,true,false,.01f)==null,"release cannot duplicate a shot");
            input.Tick(true,false,true,.01f);
            Check(input.Tick(false,false,true,1)==null&&input.Ready,"long hold waits for release at full charge");
            Check(input.Tick(false,true,false,.01f)==true,"charged release produces power shot");
            Check(input.Tick(true,true,false,.016f)==false,"tap entirely inside one render frame is preserved");
            input.Tick(true,false,true,.01f);input.Tick(false,false,true,.3f);input.Reset();
            Check(input.Tick(false,true,false,.4f)==null,"pause/focus/death cancellation cannot shoot on release");
            input.Tick(true,false,true,.01f);input.Tick(false,false,false,.8f);
            Check(!input.IsCharging,"lost held state cancels safely");
            input.Tick(true,false,true,.01f);input.Tick(false,false,true,ChargedFireInput.ChargeDuration-.02f);
            Check(input.Tick(false,true,false,.01f)==false,"just below charge threshold stays small");

            var sim=Ready();
            Check(sim.Player.Health==5&&sim.Player.MaxHealth==5,"player starts at five HP");
            Check(sim.Tanks.Where(t=>!t.Player).All(t=>t.Health==t.MaxHealth),"spawned enemies start at full armor");
            Check(TankState.StartingHealth(0,false)==3&&TankState.StartingHealth(1,false)==3&&TankState.StartingHealth(2,false)==4&&TankState.StartingHealth(3,false)==6,"enemy armor tiers");
            Check(sim.Fire(sim.Player),"small shot fires");
            var small=sim.Shots.Last();Check(!small.PowerShot&&small.Damage==1&&small.WallDamage==1,"small shot damage and wall strength");
            Check(!sim.Fire(sim.Player),"cooldown rejects duplicate shot");
            Step(sim,8);Check(sim.Fire(sim.Player)&&sim.Shots.Count(s=>s.Owner==sim.Player.Id)==2,"rapid taps allow simultaneous small bullets");
            sim.Shots.Clear();sim.Player.Cooldown=0;sim.Player.Tier=3;
            sim.Fire(sim.Player);Check(sim.Shots.Last().WallDamage==1,"upgrade does not turn taps into power shots");
            sim.Shots.Clear();sim.Player.Cooldown=0;sim.Fire(sim.Player,true);
            Check(sim.Shots.Last().PowerShot&&sim.Shots.Last().Damage==3&&sim.Shots.Last().WallDamage==2,"charged shot does triple damage and breaks steel");

            sim=Ready();var target=new TankState{Id=99001,Tier=3,Health=6,X=400,Y=280};sim.Tanks.Add(target);
            sim.Fire(sim.Player);Step(sim,15);Check(target.Alive&&target.Health==5,"small hit subtracts one HP");
            sim.Player.Cooldown=0;sim.Player.Aim=Facing.Up;sim.Fire(sim.Player,true);Step(sim,15);
            Check(target.Alive&&target.Health==2,"power hit subtracts three HP without premature death");
            int kills=0;sim.TankDestroyed+=t=>{if(t==target)kills++;};
            sim.Player.Cooldown=0;sim.Player.Aim=Facing.Up;sim.Fire(sim.Player,true);Step(sim,15);
            Check(!target.Alive&&kills==1&&sim.Score==400,"lethal power hit scores exactly once");

            sim=Ready();target=new TankState{Id=99002,Health=3,Shield=10,X=400,Y=280};sim.Tanks.Add(target);
            sim.Fire(sim.Player,true);Step(sim,15);Check(target.Health==3,"shield blocks power damage");
            sim=Ready();sim.Player.Shield=0;var enemy=new TankState{Id=99003,X=400,Y=280,Aim=Facing.Down};sim.Tanks.Add(enemy);
            sim.Fire(enemy);Step(sim,15);Check(sim.Player.Health==4&&sim.Lives==3,"nonlethal hit preserves player life");
            var oldPlayer=sim.Player;sim.Kill(oldPlayer);Step(sim,100);
            Check(sim.Player!=oldPlayer&&sim.Player.Health==5&&sim.Lives==2,"respawn restores HP and consumes one life");

            sim=Ready();sim.AddRegion("steel",384,280,32,32);var steel=sim.Terrain.Single();
            sim.Fire(sim.Player);Step(sim,15);Check(steel.Alive,"tap cannot destroy steel");
            sim.Player.Cooldown=0;sim.Player.Aim=Facing.Up;sim.Fire(sim.Player,true);Step(sim,15);Check(!steel.Alive,"power shot destroys steel");
            int[] removed=new int[2];
            for(int mode=0;mode<2;mode++)
            {
                sim=Ready();sim.AddRegion("brick",336,272,128,32);int before=sim.Terrain.Count;
                sim.Fire(sim.Player,mode==1);Step(sim,15);removed[mode]=before-sim.Terrain.Count(w=>w.Alive);
            }
            Check(removed[1]>removed[0],"power shot destroys a wider brick section");
            PowerShotRadiusChecks();
            PowerShotSplashChecks();
            IndirectWallBlastChecks();
            PowerBulletInterceptionChecks();
            NormalReloadChecks();
            FireBufferChecks();
            Debug.Log("SHOOTING/HEALTH PASS: tap/hold/release, cancellation, threshold, cooldown, rapid rounds, tier upgrades, health, shields, kills, respawn and wall destruction.");
        }
        static void FireBufferChecks()
        {
            var input=new ChargedFireInput();var sim=Ready(.3f,.2f);
            input.Sample(true,true,false,.01f,sim.CanFire(sim.Player));
            Check(input.TryTakeShot(sim.CanFire(sim.Player),out var power)&&!power&&sim.Fire(sim.Player,power),"ready tap fires one normal shot");
            for(int i=0;i<5;i++)
            {
                Step(sim,2);input.Sample(true,true,false,.01f,sim.CanFire(sim.Player));
                Check(!input.TryTakeShot(sim.CanFire(sim.Player),out power),"rapid tap during reload is ignored");
            }
            Step(sim,20);Check(!input.TryTakeShot(true,out power),"reload completion cannot drain old rapid taps");
            input.Sample(true,true,false,.01f,true);
            input.Sample(true,false,true,.01f,true);
            Check(!input.TryTakeShot(true,out power),"new hold cancels unconsumed normal tap between fixed steps");
            for(int i=0;i<Math.Ceiling(ChargedFireInput.ChargeDuration/BattleSimulation.StepSeconds)+2;i++)
            {
                input.Sample(false,false,true,BattleSimulation.StepSeconds,sim.CanFire(sim.Player));
                Check(!input.TryTakeShot(sim.CanFire(sim.Player),out power),"no shots while charging even after reload finishes");
                Step(sim,1);
            }
            Check(input.Ready,"hold still reaches full power");
            input.Sample(false,true,false,.01f,sim.CanFire(sim.Player));
            Check(input.TryTakeShot(sim.CanFire(sim.Player),out power)&&power&&sim.Fire(sim.Player,power),"charged release fires power shot");
            Check(!input.TryTakeShot(true,out power),"charged release fires only once across fixed steps");
            input.Sample(true,true,true,.01f,true);
            Check(input.IsCharging&&!input.TryTakeShot(true,out power),"release and repress in one render frame stays charging");
            input.Reset();input.Sample(true,true,false,.01f,true);input.Reset();
            Check(!input.TryTakeShot(true,out power),"reset cancels a pending shot");
            foreach(float interval in new[]{.3f,.2f})
            {
                sim=Ready(interval,interval);input.Reset();int shots=0,lastTick=-1000;
                sim.ShotFired+=shot=>{Check((sim.Tick-lastTick)*BattleSimulation.StepSeconds+.00001f>=interval,"rapid taps respect configured minimum shot interval");lastTick=sim.Tick;shots++;};
                for(int i=0;i<120;i++)
                {
                    input.Sample(true,true,false,BattleSimulation.StepSeconds,sim.CanFire(sim.Player));
                    var command=new Command();command.Fire=input.TryTakeShot(sim.CanFire(sim.Player),out power);command.PowerShot=power;sim.Step(command);
                }
                Check(shots>=5&&shots<=11,"continuous taps produce reload-limited shots");
            }
            Debug.Log("FIRE BUFFER PASS: rapid taps respect reload, no queued bursts, charging suppresses normal shots, one power shot on release, same-frame input and reset.");
        }
        static void NormalReloadChecks()
        {
            var charge=new ChargedFireInput();charge.Tick(true,false,true,0);
            charge.Tick(false,false,true,.99f);
            Check(!charge.Ready&&Math.Abs(charge.Progress-.99f)<.00001f,"power shot is not charged before one second");
            Check(charge.Tick(false,true,false,.01f)==true,"one-second charge releases power shot");
            var progressSim=Ready(.5f,.35f);
            Check(progressSim.Player.ReloadProgress==1,"cooldown bar starts full");
            progressSim.Fire(progressSim.Player);Check(progressSim.Player.ReloadProgress==0,"normal shot empties cooldown bar");
            Step(progressSim,15);Check(Math.Abs(progressSim.Player.ReloadProgress-.5f)<.001f,"cooldown bar half full halfway through reload");
            Step(progressSim,16);Check(progressSim.Player.ReloadProgress==1,"cooldown bar full when ready");
            progressSim.Fire(progressSim.Player,true);Check(progressSim.Player.ReloadProgress==0,"power-shot recovery empties cooldown bar");
            Step(progressSim,8);Check(progressSim.Player.ReloadProgress>.5f&&progressSim.Player.ReloadProgress<.6f,"power-shot bar uses its own recovery duration");
            var sim=Ready(.31f,.11f);
            Check(sim.Fire(sim.Player)&&Math.Abs(sim.Player.Cooldown-.31f)<.00001f,"selected tank normal reload applied");
            Step(sim,18);Check(!sim.Fire(sim.Player),"normal shot blocked until selected reload completes");
            Step(sim,1);Check(sim.Fire(sim.Player),"normal shot allowed after selected reload completes");
            sim.Player.Cooldown=0;sim.Player.Tier=2;
            Check(sim.Fire(sim.Player)&&Math.Abs(sim.Player.Cooldown-.11f)<.00001f,"upgraded reload comes from selected tank");
            Step(sim,6);Check(!sim.Fire(sim.Player),"upgraded reload still blocks early shots");
            Step(sim,1);Check(sim.Fire(sim.Player),"upgraded reload permits next shot");
            sim.Player.Cooldown=0;sim.Fire(sim.Player,true);
            Check(Math.Abs(sim.Player.Cooldown-.25f)<.00001f&&sim.Shots.Last().Damage==3,"normal reload setting does not alter power-shot recovery or damage");
            var enemy=new TankState{Id=99700,X=700,Y=400,Aim=Facing.Up};sim.Tanks.Add(enemy);
            Check(sim.Fire(enemy)&&Math.Abs(enemy.Cooldown-.16f)<.00001f,"player selection does not alter enemy reload");
            var old=sim.Player;sim.Kill(old);Step(sim,100);
            Check(sim.Player!=old&&Math.Abs(sim.NormalReloadSeconds(sim.Player)-.31f)<.00001f,"selected reload survives respawn");
            var defaults=Ready();defaults.Fire(defaults.Player);
            Check(Math.Abs(defaults.Player.Cooldown-.12f)<.00001f,"default tank retains current normal reload and is independent of other simulations");
            defaults.Player.Tier=2;defaults.Player.Cooldown=0;defaults.Fire(defaults.Player);
            Check(Math.Abs(defaults.Player.Cooldown-.08f)<.00001f,"default tank retains upgraded normal reload");
            var invalid=Ready(float.NaN,-1);
            Check(invalid.PlayerNormalReloadSeconds==.12f&&invalid.PlayerUpgradedNormalReloadSeconds==BattleSimulation.StepSeconds,"invalid settings cannot disable cooldown");
            Debug.Log("NORMAL RELOAD PASS: configurable cadence, early-shot rejection, upgrades, respawn, default values, independent tanks and unchanged power shots/enemies.");
        }
        static void IndirectWallBlastChecks()
        {
            foreach(Facing direction in Enum.GetValues(typeof(Facing)))
            foreach(bool power in new[]{false,true})
            {
                var sim=Ready();BattleSimulation.Vector(direction,out var dx,out var dy);
                var target=new TankState{Id=99600,X=400+dx*120,Y=400+dy*120,Health=6};sim.Tanks.Add(target);
                sim.Player.Aim=direction;
                // Impact occurs 81 pixels ahead: projectile edge touches target.
                float x=400+dx*81,y=400+dy*81;
                sim.AddRegion("brick",x-dy*40-8,y+dx*40-8,16,16);var inside=sim.Terrain.Last();
                sim.AddRegion("brick",x-dy*72-8,y+dx*72-8,16,16);var outside=sim.Terrain.Last();
                int events=0;sim.WallDestroyed+=w=>events++;
                sim.Fire(sim.Player,power);Step(sim,15);
                Check(inside.Alive==!power&&outside.Alive,"tank impact damages only walls inside power radius "+direction);
                Check(events==(power?1:0)&&target.Health==(power?3:5),"indirect destruction emits one wall event and preserves direct tank damage");
            }
            foreach(bool powerMovesFirst in new[]{false,true})
            {
                var sim=Ready();sim.AddRegion("brick",432,312,16,16);var brick=sim.Terrain.Last();
                sim.AddRegion("steel",408,320,32,32);var innerSteel=sim.Terrain.Last();
                sim.AddRegion("steel",432,304,32,32);var outerSteel=sim.Terrain.Last();
                var power=new ShotState{Id=99610,Player=true,PowerShot=true,Damage=3,WallDamage=2,X=400,Y=320,Speed=600,Direction=Facing.Up};
                var normal=new ShotState{Id=99611,Player=false,Damage=1,WallDamage=1,X=400,Y=300,Speed=600,Direction=Facing.Down};
                sim.Shots.Add(powerMovesFirst?power:normal);sim.Shots.Add(powerMovesFirst?normal:power);
                int events=0;sim.WallDestroyed+=w=>events++;Step(sim,3);
                Check(power.Alive&&power.Damage==2&&!normal.Alive,"power shot survives a bullet interception in either update order");
                Check(brick.Alive&&innerSteel.Alive&&outerSteel.Alive&&events==0,"bullet interception does not detonate the power shot near walls");
            }
            {
                var sim=Ready();sim.AddRegion("brick",432,0,16,16);var brick=sim.Terrain.Last();
                sim.Shots.Add(new ShotState{Id=99620,Player=true,PowerShot=true,Damage=3,WallDamage=2,X=400,Y=9,Speed=600,Direction=Facing.Up});
                Step(sim,2);Check(!brick.Alive,"map-edge explosion damages a nearby wall without a direct hit");
            }
            {
                var sim=Ready();sim.AddRegion("brick",384,280,32,32);int events=0;
                sim.WallDestroyed+=w=>events++;sim.Fire(sim.Player,true);Step(sim,15);
                Check(events==4&&sim.Terrain.All(w=>!w.Alive),"direct wall impact applies radial damage exactly once");
            }
            Debug.Log("INDIRECT WALL BLAST PASS: tank impacts in all four directions, boundary impacts, steel resistance, outside walls, normal shots and single destruction events.");
        }
        static void PowerShotSplashChecks()
        {
            foreach(Facing direction in Enum.GetValues(typeof(Facing)))
            foreach(string material in new[]{"brick","steel"})
            {
                var sim=Ready();sim.AddRegion(material,384,280,32,32);
                BattleSimulation.Vector(direction,out var dx,out var dy);
                float x=direction==Facing.Right?384:direction==Facing.Left?416:400;
                float y=direction==Facing.Down?280:direction==Facing.Up?312:296;
                sim.Player.X=x-dx*100;sim.Player.Y=y-dy*100;sim.Player.Aim=direction;
                sim.Player.Shield=0;
                var close=new TankState{Id=99100,X=x-dy*50,Y=y+dx*50,Health=3};
                var outer=new TankState{Id=99101,X=x+dy*70,Y=y-dx*70,Health=3};
                var outside=new TankState{Id=99102,X=x-dy*100,Y=y+dx*100,Health=3};
                sim.Tanks.Add(close);sim.Tanks.Add(outer);sim.Tanks.Add(outside);
                sim.Fire(sim.Player,true);Step(sim,15);
                Check(close.Health==2,"nearby enemy takes one splash damage "+direction+" "+material);
                Check(outer.Health==(material=="brick"?2:3),"steel reduces splash reach "+direction);
                Check(outside.Health==3&&sim.Player.Health==5,"outside enemies and player unaffected "+direction);
            }
            {
                var sim=Ready();var direct=new TankState{Id=99200,X=400,Y=280,Tier=3,Health=6};
                var nearby=new TankState{Id=99201,X=470,Y=319,Health=3};
                var shielded=new TankState{Id=99202,X=330,Y=319,Health=3,Shield=10};
                sim.Tanks.Add(direct);sim.Tanks.Add(nearby);sim.Tanks.Add(shielded);
                sim.Fire(sim.Player,true);Step(sim,15);
                Check(direct.Health==3,"direct hit retains three damage without stacking splash");
                Check(nearby.Health==2&&shielded.Health==3,"tank collision splashes nearby enemy and respects shields");
            }
            {
                var sim=Ready();sim.AddRegion("brick",384,280,32,32);
                var victim=new TankState{Id=99300,X=450,Y=312,Health=1,Drop=true};sim.Tanks.Add(victim);
                var boundary=new TankState{Id=99301,X=488,Y=312,Health=3};sim.Tanks.Add(boundary);
                var beyond=new TankState{Id=99302,X=489,Y=312,Health=3};sim.Tanks.Add(beyond);
                int kills=0,drops=0,impacts=0;
                sim.TankDestroyed+=t=>{if(t==victim)kills++;};sim.DropRequested+=()=>drops++;sim.ShotImpact+=s=>impacts++;
                sim.Fire(sim.Player,true);Step(sim,30);
                Check(!victim.Alive&&kills==1&&drops==1&&impacts==1&&sim.Score==100,"splash kill scores and drops exactly once");
                Check(boundary.Health==2&&beyond.Health==3,"splash includes tank edge on radius but excludes beyond");
            }
            {
                var sim=Ready();sim.AddRegion("brick",384,280,32,32);
                var nearby=new TankState{Id=99400,X=450,Y=312,Health=3};sim.Tanks.Add(nearby);
                sim.Fire(sim.Player);Step(sim,15);
                Check(nearby.Health==3,"normal shots have no splash damage");
            }
            foreach(bool powerMovesFirst in new[]{false,true})
            {
                var sim=Ready();var nearby=new TankState{Id=99500,X=470,Y=320,Health=3};sim.Tanks.Add(nearby);
                var power=new ShotState{Id=99501,Player=true,PowerShot=true,Damage=3,WallDamage=2,X=400,Y=320,Speed=600,Direction=Facing.Up};
                var normal=new ShotState{Id=99502,Player=false,Damage=1,WallDamage=1,X=400,Y=300,Speed=600,Direction=Facing.Down};
                sim.Shots.Add(powerMovesFirst?power:normal);sim.Shots.Add(powerMovesFirst?normal:power);
                int impacts=0;sim.ShotImpact+=s=>impacts++;
                Step(sim,3);
                Check(power.Alive&&power.Damage==2&&!normal.Alive&&nearby.Health==3&&impacts==1,"intercepted power shot continues without splash in either update order");
            }
            Debug.Log("POWER SHOT SPLASH PASS: wall/tank impacts, 1 damage, direct-hit exclusion, shields, steel absorption, bounds, player safety, normal shots and kill/drop/score uniqueness.");
        }
        static void PowerBulletInterceptionChecks()
        {
            foreach(bool powerMovesFirst in new[]{false,true})
            {
                var sim=Ready();var target=new TankState{Id=99750,Tier=3,Health=6,X=400,Y=225};
                sim.Tanks.Add(target);
                var power=new ShotState{Id=99751,Player=true,PowerShot=true,Damage=3,WallDamage=2,X=400,Y=320,Speed=600,Direction=Facing.Up};
                var normal=new ShotState{Id=99752,Player=false,Damage=1,WallDamage=1,X=400,Y=300,Speed=600,Direction=Facing.Down};
                sim.Shots.Add(powerMovesFirst?power:normal);sim.Shots.Add(powerMovesFirst?normal:power);
                int powerImpacts=0;sim.ShotImpact+=shot=>{if(shot==power)powerImpacts++;};
                Step(sim,3);
                Check(power.Alive&&power.Damage==2&&!normal.Alive&&powerImpacts==0,"one opposing bullet costs one attack power without consuming or detonating the power shot");
                Step(sim,12);
                Check(!power.Alive&&target.Health==4&&powerImpacts==1,"weakened power shot continues to its target and deals two direct damage");
            }
            {
                var sim=Ready();
                var power=new ShotState{Id=99760,Player=true,PowerShot=true,Damage=3,WallDamage=2,X=600,Y=500,Speed=600,Direction=Facing.Up};
                sim.Shots.Add(power);
                for(int i=0;i<3;i++)sim.Shots.Add(new ShotState{Id=99761+i,Player=false,Damage=1,WallDamage=1,X=600,Y=470-i*50,Speed=0,Direction=Facing.Down});
                int powerImpacts=0;sim.ShotImpact+=shot=>{if(shot==power)powerImpacts++;};
                for(int i=0;i<20&&power.Damage>1;i++)sim.Step(default);
                Check(power.Alive&&power.Damage==1&&sim.Shots.Count(s=>!s.Player)==1&&powerImpacts==0,"two interceptions reduce power from three to one without detonating it");
                for(int i=0;i<20&&power.Alive;i++)sim.Step(default);
                Check(!power.Alive&&sim.Shots.All(s=>s.Player)&&powerImpacts==1,"third interception spends the last attack point and ends the power shot once");
            }
            Debug.Log("POWER BULLET INTERCEPTION PASS: both update orders, retained travel, reduced target damage, no early explosion and depletion after three hits.");
        }
        static void PowerShotRadiusChecks()
        {
            foreach(Facing direction in Enum.GetValues(typeof(Facing)))
            {
                foreach(bool power in new[]{false,true})
                {
                    var sim=Ready();sim.AddRegion("brick",96,96,128,128);
                    bool vertical=direction==Facing.Up||direction==Facing.Down;
                    var hit=sim.Terrain.First(w=>(vertical?w.Bounds.X:w.Bounds.Y)==144&&(direction==Facing.Up?w.Bounds.Y==208:direction==Facing.Down?w.Bounds.Y==96:direction==Facing.Left?w.Bounds.X==208:w.Bounds.X==96));
                    var destroyed=new System.Collections.Generic.HashSet<int>();int events=0;
                    sim.WallDestroyed+=w=>{destroyed.Add(w.Id);events++;};
                    sim.DestroyWall(hit,new ShotState{X=160,Y=160,Direction=direction,PowerShot=power,WallDamage=power?2:1});
                    Check(sim.Terrain.Count(w=>!w.Alive)==(power?16:4),"reduced circular blast size "+direction+" power="+power);
                    Check(events==destroyed.Count&&events==(power?16:4),"one debris event per destroyed brick "+direction);
                    if(power)
                    {
                        for(int row=0;row<4;row++)
                        {
                            float along=direction==Facing.Up||direction==Facing.Left?208-row*16:96+row*16;
                            var cells=sim.Terrain.Where(w=>(vertical?w.Bounds.Y:w.Bounds.X)==along).ToArray();
                            int expected=row<2?6:row==2?4:0;
                            float min=160-expected*8,max=160+expected*8;
                            Check(cells.Count(w=>!w.Alive)==expected,"round crater row "+row+" "+direction);
                            Check(cells.All(w=>w.Alive==((vertical?w.Bounds.X:w.Bounds.Y)<min||(vertical?w.Bounds.Right:w.Bounds.Bottom)>max)),"circle stays centered and preserves outside bricks "+direction);
                        }
                    }
                    Check(sim.Terrain.Where(w=>direction==Facing.Up?w.Bounds.Y<=144:direction==Facing.Down?w.Bounds.Y>=160:direction==Facing.Left?w.Bounds.X<=144:w.Bounds.X>=160).All(w=>w.Alive),"bricks beyond blast radius survive "+direction);
                }
            }
            var radial=Ready();radial.AddRegion("brick",120,128,16,16);var impact=radial.Terrain[0];
            foreach(var center in new[]{new Vector2(80,128),new Vector2(176,128),new Vector2(128,80),new Vector2(128,176),new Vector2(164,164),new Vector2(176,176),new Vector2(208,128)})
                radial.AddRegion("brick",center.x-8,center.y-8,16,16);
            radial.AddRegion("steel",144,96,32,32);var nearbySteel=radial.Terrain.Last();
            radial.AddRegion("steel",128,112,32,32);var innerSteel=radial.Terrain.Last();
            radial.AddRegion("water",112,112,32,32);var water=radial.Terrain.Last();
            radial.DestroyWall(impact,new ShotState{X=128,Y=120,Direction=Facing.Down,PowerShot=true,WallDamage=2});
            Check(radial.Terrain.Where(w=>w.Brick&&w.Bounds.X<160&&w.Bounds.Y<160).All(w=>!w.Alive),"circle reaches nearby bricks across gaps and behind the projectile");
            Check(!radial.Terrain.Single(w=>w.Brick&&w.Bounds.X==156&&w.Bounds.Y==156).Alive,"diagonal brick inside radius destroyed");
            Check(radial.Terrain.Single(w=>w.Bounds.X==168&&w.Bounds.Y==168).Alive,"diagonal outside circle survives despite being inside its bounding square");
            Check(radial.Terrain.Single(w=>w.Bounds.X==200).Alive,"distant brick survives");
            Check(nearbySteel.Alive&&!innerSteel.Alive&&water.Alive,"steel survives outer splash but breaks in the inner radius; water survives");
            var offset=Ready();offset.AddRegion("brick",128,96,16,16);var offsetHit=offset.Terrain[0];
            offset.AddRegion("brick",184,88,16,16);var edge=offset.Terrain.Last();
            offset.AddRegion("brick",48,88,16,16);var outside=offset.Terrain.Last();
            offset.DestroyWall(offsetHit,new ShotState{X=136,Y=80,Direction=Facing.Down,PowerShot=true,WallDamage=2});
            Check(!edge.Alive&&outside.Alive,"radius uses the exact unsnapped contact and includes its boundary");
            foreach(Facing direction in Enum.GetValues(typeof(Facing)))
            {
                var armored=Ready();armored.AddRegion("steel",96,96,128,128);
                bool vertical=direction==Facing.Up||direction==Facing.Down;
                var hit=armored.Terrain.First(w=>(vertical?w.Bounds.X:w.Bounds.Y)==128&&(direction==Facing.Up?w.Bounds.Y==192:direction==Facing.Down?w.Bounds.Y==96:direction==Facing.Left?w.Bounds.X==192:w.Bounds.X==96));
                armored.DestroyWall(hit,new ShotState{X=160,Y=160,Direction=direction,PowerShot=true,WallDamage=2});
                Check(!hit.Alive&&armored.Terrain.Count(w=>!w.Alive)==2,"steel impact has a smaller crater "+direction);
            }
            foreach(string material in new[]{"brick","steel"})
            {
                var mixed=Ready();mixed.AddRegion(material,128,128,32,32);var hit=mixed.Terrain[0];
                mixed.AddRegion("brick",112,120,16,16);var close=mixed.Terrain.Last();
                mixed.AddRegion("brick",88,120,16,16);var far=mixed.Terrain.Last();
                mixed.DestroyWall(hit,new ShotState{X=144,Y=120,Direction=Facing.Down,PowerShot=true,WallDamage=2});
                Check(!close.Alive&&far.Alive==(material=="steel"),"steel absorbs blast reach into surrounding bricks");
            }
            Debug.Log("POWER SHOT RADIUS PASS: reduced round crater, smaller steel crater in all directions, steel splash resistance and absorption, exact contact, normal shots and unique destruction events.");
        }
    }
}
