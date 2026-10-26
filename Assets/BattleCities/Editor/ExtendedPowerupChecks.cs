using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using BattleCities.Multiplayer;
using BattleCities.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class ExtendedPowerupChecks
    {
        static int checks;
        static void Check(bool ok,string name){checks++;if(!ok)throw new Exception("EXTENDED POWERUP: "+name);}
        static void Near(float a,float b,string name)=>Check(Math.Abs(a-b)<.0001f,name);
        static MapData Map()=>new MapData{field=new FieldData{widthTiles=20,heightTiles=20},spawn=new SpawnData{enemy=new SpawnGroup{locations=new[]{new Point{x=32,y=0}},list=new[]{new EnemySpec{tier="a"}}}}};
        static BattleSimulation Ready(int tier)
        {
            var s=new BattleSimulation(Map(),playerTier:tier){DisableEnemyFire=true,Freeze=999};
            for(int i=0;i<122;i++)s.Step(default);return s;
        }
        public static void Run()
        {
            checks=0;
            for(int tier=0;tier<4;tier++)
            {
                var s=Ready(tier);var p=s.Player;int max=p.MaxHealth;p.Health=1;s.ApplyPowerup("healmax");Check(p.Health==max,"max healer respects chassis maximum");
                p.Health=1;s.ApplyPowerup("heal2");Check(p.Health==Math.Min(max,3),"+2 healer caps health");s.ApplyPowerup("heal2");Check(p.Health<=max,"repeat healing cannot overheal");
                foreach(string power in new[]{"rapid","rapidlong"})
                {
                    p.RapidFire=0;float normal=s.NormalReloadSeconds(p);s.ApplyPowerup(power);Near(s.NormalReloadSeconds(p),normal/4,"fourfold rate stacks with rounds");
                    Near(p.RapidFire,power=="rapid"?10:60,"approved shooter duration");
                    var replica=NetTankState.From(p).ToState();Near(replica.RapidFire,p.RapidFire,"network timer retained");
                    Check(ReplayJson.Hash(s.CaptureReplayStateData())==s.ReplayStateHash(),"detached snapshot preserves active boost");
                    var restored=new BattleSimulation(Map());restored.RestoreNetworkCheckpoint(s.SaveNetworkCheckpoint());Near(restored.Player.RapidFire,p.RapidFire,"checkpoint preserves duration");
                    Check(restored.ReplayStateHash()==s.ReplayStateHash(),"checkpoint state hash preserved");
                    if(tier==0)
                    {
                        int ticks=power=="rapid"?602:3602;
                        for(int i=0;i<ticks;i++)s.Step(default);
                        Near(p.RapidFire,0,"boost expires in simulation time");Near(s.NormalReloadSeconds(p),normal,"normal rate restored");
                    }
                    p.RapidFire=0;
                }
                s.ApplyPowerup("rapidlong");s.ApplyPowerup("rapid");Near(p.RapidFire,60,"short pickup does not truncate long boost");
                p.Alive=false;s.ApplyPowerup("healmax");Check(!p.Alive,"healing cannot resurrect destroyed tank");
            }
            var map=Map();var simulation=new BattleSimulation(map,playerTier:3);
            var recorder=new ReplayRecorder(simulation,map,"extended-powerup-check");
            for(int tick=1;tick<=240;tick++)
            {
                if(tick==125||tick==145||tick==165||tick==185)
                {
                    string type=tick==125?"healmax":tick==145?"heal2":tick==165?"rapid":"rapidlong";
                    var e=new ReplayEvent{kind="powerup",value=type};recorder.RecordEvent(e);ReplayPlayer.ApplyEvent(simulation,e);
                }
                recorder.BeforeStep(default);simulation.Step(default);recorder.AfterStep();
            }
            recorder.Finish("aborted");var recording=ReplayJson.Read(ReplayJson.Write(recorder.Data));var playback=new ReplayPlayer(recording);
            while(!playback.Complete&&playback.Error==null)playback.Step();Check(playback.Complete,"new power-up recording reproduces: "+playback.Error);
            var legacy=new BattleSimulation(Map());var config=legacy.ReplayConfiguration();config.powerupRosterVersion=0;legacy.ApplyReplayConfiguration(config);
            var current=new BattleSimulation(Map());var seen=new HashSet<string>();
            for(int i=0;i<300;i++){legacy.SpawnPickup();Check(!new[]{"healmax","heal2","rapid","rapidlong"}.Contains(legacy.PickupType),"legacy replay drop pool retained");current.SpawnPickup();seen.Add(current.PickupType);}
            Check(new[]{"healmax","heal2","rapid","rapidlong"}.All(seen.Contains),"new drop pool reaches all additions");
            var atlas=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>().PowerupAtlas;
            foreach(var entry in new[]{("max-healer","healmax",450,.02m),("health-healer","heal2",450,.01m),("rapid-shooter","rapid",450,.01m),("rapid-shooter-long","rapidlong",900,.02m)})
            {
                var product=ShopCatalog.Products.Single(p=>p.Id==entry.Item1);Check(product.SkrPrice==entry.Item3&&product.SolPrice==entry.Item4,"approved catalog prices");
                Check(ShopCatalog.PowerupType(entry.Item1)==entry.Item2,"API item mapping");Check(ExtendedPowerupArt.Illustration(entry.Item1),"inventory illustration exists");
                Check(ExtendedPowerupArt.TryImage(atlas,entry.Item2,out var texture,out var uv)&&texture&&uv.width>0&&uv.height>0,"HUD and world-drop artwork exists");
            }
            Check(ShopCatalog.InventoryIds.Length==12,"all twelve inventory items");Check(!JsonConvert.SerializeObject(new TankState()).Contains("RapidFire"),"inactive boost does not change old replay JSON");
            BattleCities.Tests.ReplayChecks.RunEditor();BattleCities.ShootingHealthChecks.Run();
            Debug.Log("EXTENDED POWERUP PASS: "+checks+" checks, replay suite and shooting suite.");
        }
    }
}
