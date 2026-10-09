using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCities.Core;
using BattleCities.Multiplayer;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    public static class BattleResultsChecks
    {
        static int flowPhase;
        static double flowAt,flowDeadline;
        static BattleGame flowGame;
        public static string FlowResult=>SessionState.GetString("BattleCities.ResultsFlow","Not run");
        public static void StartFlow()
        {
            Check(Application.isPlaying,"start Play mode in BattleCity for flow checks");
            flowGame=UnityEngine.Object.FindFirstObjectByType<BattleGame>();Check(flowGame&&!flowGame.IsOnline,"offline gameplay fixture required");
            flowPhase=0;flowAt=EditorApplication.timeSinceStartup;flowDeadline=flowAt+60;
            SessionState.SetString("BattleCities.ResultsFlow","Running");EditorApplication.update-=FlowTick;EditorApplication.update+=FlowTick;
        }
        static void FlowTick()
        {
            if(EditorApplication.timeSinceStartup<flowAt)return;
            try
            {
                Check(Application.isPlaying&&EditorApplication.timeSinceStartup<flowDeadline,"runtime flow timed out");
                if(flowPhase==0){flowGame.LoadStage(1);EndMatch(flowGame,true);}
                else if(flowPhase==1)
                {
                    Check(flowGame.ResultsVisible,"victory takes over the screen");
                    var screen=UnityEngine.Object.FindFirstObjectByType<BattleResultsScreen>();var canvas=screen.GetComponentInChildren<Canvas>();
                    Check(canvas.renderMode==RenderMode.ScreenSpaceOverlay&&canvas.sortingOrder==600,"result canvas replaces the gameplay view");
                    var next=screen.GetComponentsInChildren<Button>().Single(b=>b.name=="Continue");
                    Check(next.IsInteractable()&&next.navigation.selectOnUp.name=="Share results","Continue and Share navigation linked after input gate");
                    next.onClick.Invoke();Check(flowGame.Stage==2&&!flowGame.HasMatchResult&&!flowGame.ResultsVisible,"Continue starts next stage and closes results");
                    EndMatch(flowGame,false);
                }
                else if(flowPhase==2)
                {
                    Check(flowGame.ResultsVisible,"defeat opens the same results screen");
                    UnityEngine.Object.FindFirstObjectByType<BattleResultsScreen>().GetComponentsInChildren<Button>().Single(b=>b.name=="Continue").onClick.Invoke();
                }
                else
                {
                    Check(SceneManager.GetActiveScene().name=="MainMenu","defeat Continue returns to main menu");
                    EditorApplication.update-=FlowTick;SessionState.SetString("BattleCities.ResultsFlow","PASS: victory takeover, input gate, navigation, next stage, defeat takeover and Continue to menu.");
                    Debug.Log(FlowResult);EditorApplication.isPlaying=false;return;
                }
                flowPhase++;flowAt=EditorApplication.timeSinceStartup+.75;
            }
            catch(Exception error){EditorApplication.update-=FlowTick;SessionState.SetString("BattleCities.ResultsFlow","FAIL: "+error);Debug.LogError(FlowResult);EditorApplication.isPlaying=false;}
        }
        static void EndMatch(BattleGame game,bool won)
        {
            var s=game.Simulation;
            s.ApplyFrame(new BattleFrame{Tick=s.Tick,Score=s.Score,Started=true,Won=won,Lost=!won,BaseAlive=s.BaseAlive,
                Participants=s.Participants.ToArray(),ResultStats=s.ResultStats.Select(p=>p.Copy()).ToArray(),
                Tanks=s.Tanks.ToArray(),Shots=s.Shots.ToArray(),Mines=s.Mines.ToArray(),Drones=s.Drones.ToArray(),Turrets=s.Turrets.ToArray(),LandDrones=s.LandDrones.ToArray(),
                TerrainAlive=s.Terrain.Take(s.InitialTerrainCount).Select(w=>w.Alive).ToArray(),ExtraWalls=s.Terrain.Skip(s.InitialTerrainCount).ToArray()});
        }
        static void Check(bool ok,string name){if(!ok)throw new Exception("RESULTS: "+name);}
        [MenuItem("Battle Cities/Validate Battle Results")]
        public static void Run()
        {
            var sim=new BattleSimulation(new MapData());
            for(int tier=0;tier<4;tier++){var enemy=new TankState{Alive=true,Tier=tier};sim.Kill(enemy);sim.Kill(enemy);}
            var stat=sim.ResultStats[0];
            Check(stat.Kills==4&&stat.Points==1000&&sim.Score==1000,"tier points and duplicate kill protection");
            Check(Enumerable.Range(0,4).All(t=>stat.ForTier(t)==1),"four tank columns");
            sim.Kill(new TankState{Alive=true,Player=true});Check(stat.Kills==4,"own death excluded");
            sim=new BattleSimulation(new MapData()){DisableEnemyFire=true,Freeze=999};sim.Terrain.Clear();sim.ConfigureMultiplayer(BattleMode.Coop);
            sim.SetParticipant(0,true);sim.SetParticipant(1,true);sim.BeginMatch();
            for(int i=0;i<122;i++)sim.StepMultiplayer(new Dictionary<int,Command>());
            sim.Kill(new TankState{Alive=true,Tier=2},1);
            Check(sim.ResultStats[1].Tier2==1&&sim.ResultStats[1].Points==300&&sim.ResultStats[0].Kills==0,"co-op owner attribution");
            var player=sim.Tanks.First(t=>t.Player&&t.Slot==1);sim.PickupType="shield";sim.PickupTime=30;sim.PickupX=player.X;sim.PickupY=player.Y;
            sim.StepMultiplayer(new Dictionary<int,Command>());
            Check(sim.ResultStats[1].Bonus==500&&sim.ResultStats[1].Points==800,"pickup bonus belongs to collector");
            var copy=NetBattleResultStats.From(sim.ResultStats[1]).ToState();
            Check(copy.Participated&&copy.Tier2==1&&copy.Bonus==500&&copy.Points==800,"network statistics round trip");
            var before=ReplayJson.Hash(sim.ReplayStateData());sim.ResultStats[1].Tier0++;
            Check(before==ReplayJson.Hash(sim.ReplayStateData()),"presentation statistics do not change replay hashes");
            sim.SetParticipant(1,false);
            var report=BattleResultsData.Capture(sim,"LOCAL",700);
            Check(report.Players.Length==2&&report.Players[0].Slot==1&&report.Players[1].Local,"rank order and departed participant retained");
            Check(report.ShareText.Contains("PLAYER 2")&&report.ShareText.Contains("800"),"share uses match data");
            sim.ResultStats[1].Points=0;Check(report.Players[0].Stats.Points==800,"report is an immutable snapshot");
            Debug.Log("RESULTS PASS: tier counts, points, duplicate kills, owner attribution, bonuses, network snapshot, replay hash isolation, ranks and share data.");
        }

        public static BattleResultsData Fixture(int count=4)
        {
            string[] names={"IRONWOLF","GUEST PLAYER","STEELFOX","TANKACE"};int[,] tiers={{4,2,1,1},{3,2,1,0},{2,1,0,0},{1,0,0,0}};
            return new BattleResultsData{Stage=1,Outcome="MISSION\nFAILED",HighScore=25700,Defeated=18,TotalEnemies=20,Seconds=166,TeamKills=18,
                Players=Enumerable.Range(0,count).Select(i=>new BattleResultsData.PlayerRow{Slot=i,Rank=i+1,Name=names[i],Local=i==1||count==1,
                    Stats=new BattleResultStats{Participated=true,Tier0=tiers[i,0],Tier1=tiers[i,1],Tier2=tiers[i,2],Tier3=tiers[i,3],Points=new[]{1500,1000,400,100}[i]}}).ToArray()};
        }

        public static string Capture(int width,int height,string name,int count=4)
        {
            var host=new GameObject("Results layout preview"){hideFlags=HideFlags.DontSave};var screen=host.AddComponent<BattleResultsScreen>();
            var cameraRoot=new GameObject("Results capture camera",typeof(Camera)){hideFlags=HideFlags.HideAndDontSave};var camera=cameraRoot.GetComponent<Camera>();
            camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;
            var target=new RenderTexture(width,height,24);var oldActive=RenderTexture.active;Texture2D image=null;
            try
            {
                screen.Show(Fixture(count),()=>{},()=>{});var canvas=host.GetComponentInChildren<Canvas>();
                foreach(var t in host.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                canvas.GetComponent<CanvasScaler>().enabled=false;canvas.scaleFactor=height/900f;
                Canvas.ForceUpdateCanvases();screen.RefreshLayout();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                string directory=Path.GetFullPath("Temp/ResultsPreviews");Directory.CreateDirectory(directory);string path=Path.Combine(directory,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());return path;
            }
            finally{RenderTexture.active=oldActive;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(cameraRoot);UnityEngine.Object.DestroyImmediate(target);if(image)UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
