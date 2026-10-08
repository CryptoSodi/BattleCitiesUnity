#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using BattleCities.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Tests
{
    public static class GameplayStartChecks
    {
        const BindingFlags InstancePrivate=BindingFlags.Instance|BindingFlags.NonPublic;
        const string GuestHighScore="battlecities.guestHighScore";
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static FieldInfo GameField(string name)=>typeof(BattleGame).GetField(name,InstancePrivate)??throw new Exception("Missing gameplay fixture field: "+name);
        static string ScoreKey(string api,string owner)=>"battlecities.lastScore."+Hash128.Compute(api+"|"+owner);

        // Headless release verification; leaves runtime sources and authored scenes unchanged.
        public static void RunReleaseChecks()
        {
            Check(!Application.isPlaying,"Release checks must run outside Play mode.");
            var report=new JObject { ["suiteCount"]=3,["playing"]=Application.isPlaying,
                ["buildTarget"]=EditorUserBuildSettings.activeBuildTarget.ToString() };
            try
            {
                ReplayRecoveryChecks.Run();report["replayRecovery"]="passed";
                Run();report["gameplayStart"]="passed";
                var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var paths=AssetDatabase.FindAssets("MainMenu t:Scene");
                Check(paths.Length==1,"Expected exactly one MainMenu scene for loadout checks.");
                var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    AssetDatabase.GUIDToAssetPath(paths[0]),UnityEditor.SceneManagement.OpenSceneMode.Additive);
                try
                {
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                    BattleCities.Editor.LoadoutChecks.Run();
                    string status=BattleCities.Editor.LoadoutChecks.Status;
                    report["loadout"]=status;
                    Check(status.StartsWith("PASS:"),status);
                }
                finally
                {
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
                }
                report["result"]="passed";
            }
            catch(Exception error)
            {
                report["result"]="failed";report["error"]=error.ToString();throw;
            }
            finally
            {
                report["completedAt"]=DateTime.UtcNow.ToString("o");
                string folder=System.IO.Path.Combine(Application.dataPath,"..","Builds");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"gameplay-checks.json"),report.ToString());
                Debug.Log("Gameplay release checks: "+report.ToString(Newtonsoft.Json.Formatting.None));
            }
        }

        // Inactive fixture only: no scene loading, transport service, API request or real fuel key.
        [MenuItem("Battle Cities/Checks/Gameplay start and score persistence")]
        public static void Run()
        {
            Check(!Application.isPlaying,"Run gameplay start checks outside Play mode.");
            Check(!LevelEditor.LevelEditorPlaytest.IsActive,"Finish the current level-editor playtest before running gameplay start checks.");
            var preparation=new Dictionary<PropertyInfo,object>();
            foreach(var property in typeof(BattlePreparation).GetProperties(BindingFlags.Public|BindingFlags.Static))
                if(property.CanRead&&property.GetSetMethod(true)!=null)preparation.Add(property,property.GetValue(null));
            bool hadGuestScore=PlayerPrefs.HasKey(GuestHighScore);
            int previousGuestScore=PlayerPrefs.GetInt(GuestHighScore,0);
            string suffix=Guid.NewGuid().ToString("N"),api="https://gameplay-fixture-"+suffix+".invalid";
            string wallet="wallet-fixture-"+suffix,guest="guest-fixture-"+suffix,request="fuel-fixture-"+suffix;
            string receiptKey=BattleFuelReceipt.Key(api,wallet);
            var temporaryKeys=new[]{receiptKey,ScoreKey(api,wallet),ScoreKey(api,guest)};
            foreach(var key in temporaryKeys)Check(!PlayerPrefs.HasKey(key),"Fixture key unexpectedly already exists");
            GameObject host=null;
            try
            {
                host=new GameObject("Isolated gameplay start fixture");host.hideFlags=HideFlags.HideAndDontSave;host.SetActive(false);
                var game=host.AddComponent<BattleGame>();
                // Skip the runtime teardown for this never-activated editor-only component.
                GameField("authoringPreview").SetValue(game,true);
                game.Stage=9;
                BattlePreparation.Set(2,api,wallet,"wallet",request,9);
                PlayerPrefs.SetString(receiptKey,new JObject { ["requestId"]=request,["tier"]=2 }.ToString());
                int returns=0;game.EditorRestartToMenuOverride=()=>returns++;
                game.RequestBattleRestart();game.RequestBattleRestart();
                Check(returns==1&&game.Paused,"Repeated restart input did not route to the menu exactly once");
                Check(BattlePreparation.TryTakeRestart(out int tier,out int stage)&&tier==2&&stage==9,"Restart lost its selected tank or stage");
                Check(!BattlePreparation.TryTakeRestart(out _,out _),"Restart request could be consumed more than once");
                Check(BattlePreparation.StartStage==9&&BattlePreparation.FuelRequestId==request&&PlayerPrefs.HasKey(receiptKey),"Restart consumed an attempt before its first simulation tick");

                var replay=ReplayChecks.Fixture();
                var simulation=ReplayPlayer.CreateSimulation(replay);
                Check(simulation.Tick==0&&!simulation.Won&&!simulation.Lost,"First-tick fixture did not start from a fresh simulation");
                typeof(BattleGame).GetProperty("Simulation").GetSetMethod(true).Invoke(game,new object[]{simulation});
                GameField("replayArchive").SetValue(game,new ReplayArchive { replay=replay,apiUrl=api,ownerId=wallet,ownerProvider="wallet" });
                GameField("battleFuelRequestId").SetValue(game,request);
                GameField("replayPreparing").SetValue(game,true);
                var step=typeof(BattleGame).GetMethod("StepRecorded",InstancePrivate);
                Check(step!=null,"Missing recorded gameplay step");
                var stepArguments=new object[]{default(Command),null};
                step.Invoke(game,stepArguments);
                Check(simulation.Tick==0&&PlayerPrefs.HasKey(receiptKey),"Waiting for a ranked session consumed the paid attempt");
                GameField("replayPreparing").SetValue(game,false);
                step.Invoke(game,stepArguments);
                Check(simulation.Tick==1&&!PlayerPrefs.HasKey(receiptKey),"First simulation tick did not retire its matching receipt");
                Check(GameField("battleFuelRequestId").GetValue(game)==null,"Gameplay retained a receipt after starting");
                string newerRequest=request+"-new";
                PlayerPrefs.SetString(receiptKey,new JObject { ["requestId"]=newerRequest,["tier"]=2 }.ToString());
                step.Invoke(game,stepArguments);
                BattleFuelReceipt.MarkStarted(api,wallet,request);
                Check(simulation.Tick==2&&PlayerPrefs.HasKey(receiptKey)&&
                    (string)JObject.Parse(PlayerPrefs.GetString(receiptKey))["requestId"]==newerRequest,
                    "An older running attempt consumed a newer pending receipt");

                // This completed envelope exercises the local display cache only; no score is submitted.
                replay.completion="completed";replay.claimedResult.lost=true;replay.debugUsed=false;replay.claimedResult.score=9876;
                var walletArchive=new ReplayArchive { replay=replay,apiUrl=api,ownerId=wallet,ownerProvider="wallet" };
                PlayerPrefs.SetInt(GuestHighScore,314);
                BattleScoreCache.Record(walletArchive);
                Check(BattleScoreCache.Last(api,wallet)==9876,"Completed wallet score was not cached for its account");
                Check(PlayerPrefs.GetInt(GuestHighScore)==314,"Wallet score leaked into the local guest high score");
                Check(BattleScoreCache.Last(api,guest)==0&&BattleScoreCache.Last(api+"/other",wallet)==0,"Local scores leaked across account or API identity");
                replay.completion="aborted";replay.claimedResult.score=10000;BattleScoreCache.Record(walletArchive);
                Check(BattleScoreCache.Last(api,wallet)==9876,"Aborted attempt replaced the last completed wallet score");

                var guestArchive=new ReplayArchive { replay=ReplayJson.Copy(replay),apiUrl=api,ownerId=guest,ownerProvider="guest" };
                guestArchive.replay.claimedResult.score=9000;
                BattleScoreCache.Record(guestArchive);
                Check(BattleScoreCache.Last(api,guest)==0&&PlayerPrefs.GetInt(GuestHighScore)==314,"Aborted guest attempt changed score history");
                guestArchive.replay.completion="completed";guestArchive.replay.debugUsed=true;
                BattleScoreCache.Record(guestArchive);
                Check(BattleScoreCache.Last(api,guest)==0&&PlayerPrefs.GetInt(GuestHighScore)==314,"Debug guest attempt changed score history");
                guestArchive.replay.debugUsed=false;guestArchive.replay.claimedResult.score=617;
                BattleScoreCache.Record(guestArchive);
                Check(BattleScoreCache.Last(api,guest)==617&&PlayerPrefs.GetInt(GuestHighScore)==617,"Completed guest score was not retained locally");
                guestArchive.replay.claimedResult.score=600;BattleScoreCache.Record(guestArchive);
                Check(BattleScoreCache.Last(api,guest)==600&&PlayerPrefs.GetInt(GuestHighScore)==617,"A lower guest score replaced the guest high score");
                Debug.Log("Gameplay start checks passed: restart routing and stage retention, first-tick receipt lifetime, newer receipt protection, and isolated wallet/guest score persistence.");
            }
            finally
            {
                try {if(host)UnityEngine.Object.DestroyImmediate(host);}
                finally
                {
                    foreach(var saved in preparation)saved.Key.GetSetMethod(true).Invoke(null,new[]{saved.Value});
                    foreach(var key in temporaryKeys)PlayerPrefs.DeleteKey(key);
                    if(hadGuestScore)PlayerPrefs.SetInt(GuestHighScore,previousGuestScore);else PlayerPrefs.DeleteKey(GuestHighScore);
                    PlayerPrefs.Save();
                }
            }
        }
    }
}
#endif
