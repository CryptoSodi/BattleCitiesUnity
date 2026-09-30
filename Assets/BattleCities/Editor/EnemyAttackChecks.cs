using System;
using System.Linq;
using BattleCities.Core;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class EnemyAttackChecks
    {
        [MenuItem("Battle Cities/Validate Enemy Attacks")]
        public static void Run()
        {
            int checks=0;
            void Check(bool pass,string message)
            {
                if(!pass)throw new InvalidOperationException("[EnemyAttack] FAIL: "+message);
                checks++;
            }

            var defaults=new GameObject("Enemy attack defaults check");
            defaults.SetActive(false);
            try { Check(defaults.AddComponent<BattleGame>().EnemyFire,"new games enable enemy shooting"); }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }

            var scene=EditorSceneManager.OpenPreviewScene("Assets/BattleCities/Scenes/BattleCity.unity");
            try
            {
                var games=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleGame>(true)).ToArray();
                Check(games.Length==1&&games[0].EnemyFire,"shipped gameplay scene enables enemy shooting");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }

            for(int stage=1;stage<=35;stage++)
            {
                var asset=Resources.Load<TextAsset>("Maps/"+stage.ToString("00"));
                var sim=new BattleSimulation(JsonConvert.DeserializeObject<MapData>(asset.text),stage);
                int fired=0;
                sim.ShotFired+=shot=>{if(!shot.Player)fired++;};
                for(int tick=0;tick<600&&fired==0;tick++)sim.Step(default);
                Check(fired>0,"stage "+stage+" enemies fire through their AI");
            }

            var lane=Lane();
            int shots=0;
            lane.ShotFired+=shot=>{if(!shot.Player)shots++;};
            Step(lane,20);
            Check(shots>0&&lane.Player.Health<5,"AI shots reach and damage the player");

            lane=Lane();shots=0;lane.DisableEnemyFire=true;
            lane.ShotFired+=shot=>{if(!shot.Player)shots++;};
            Step(lane,20);
            Check(shots==0&&lane.Player.Health==5,"debug shooting switch still suppresses attacks");

            lane=Lane();shots=0;lane.Freeze=5;
            lane.ShotFired+=shot=>{if(!shot.Player)shots++;};
            Step(lane,20);
            Check(shots==0,"freeze powerup prevents new enemy shots");
            lane.Freeze=0;Step(lane,20);
            Check(shots>0,"enemy attacks resume after freeze ends");
            Debug.Log("[EnemyAttack] PASS: "+checks+" checks, including all 35 stages and AI damage.");
        }

        static BattleSimulation Lane()
        {
            var sim=new BattleSimulation(new MapData()){Freeze=100};
            Step(sim,122);
            sim.Terrain.Clear();sim.Tanks.RemoveAll(t=>!t.Player);
            sim.Player.X=400;sim.Player.Y=400;sim.Player.Shield=0;
            sim.Tanks.Add(new TankState{Id=99003,X=400,Y=280,Aim=Facing.Down,Direction=Facing.Down});
            sim.Freeze=0;
            return sim;
        }
        static void Step(BattleSimulation sim,int count)
        {for(int tick=0;tick<count;tick++)sim.Step(default);}
    }
}
