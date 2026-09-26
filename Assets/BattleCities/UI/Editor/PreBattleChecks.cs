using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.UI.Editor
{
    public static class PreBattleChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static string Result {get;private set;}="Not run";
        static double deadline;

        [MenuItem("Battle Cities/UI/Test Continue Starts Battle")]
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode and open MainMenu first.");
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            if(!menu)throw new InvalidOperationException("Open MainMenu first.");
            if(BattleCities.Multiplayer.BattleLaunchOptions.Mode!=BattleCities.Core.BattleMode.Offline)
                throw new InvalidOperationException("Use offline launch mode for this local-only test.");
            menu.StartBattle();
            var screen=UnityEngine.Object.FindFirstObjectByType<PreBattleScreen>();
            var type=screen.GetType();
            var tanks=(Button[])type.GetField("tankButtons",Private).GetValue(screen);
            tanks[2].onClick.Invoke();
            // No wallet and a pending inventory read must not block Continue.
            type.GetField("account",Private).SetValue(screen,null);
            type.GetField("busy",Private).SetValue(screen,true);
            var proceed=(Button)type.GetField("proceed",Private).GetValue(screen);
            if(!proceed.interactable)throw new InvalidOperationException("Continue is disabled.");
            Result="Running";deadline=EditorApplication.timeSinceStartup+30;
            EditorApplication.update-=Verify;EditorApplication.update+=Verify;
            proceed.onClick.Invoke();proceed.onClick.Invoke();
        }

        static void Verify()
        {
            if(SceneManager.GetActiveScene().name=="BattleCity")
            {
                var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();
                if(game&&game.Simulation!=null)
                {
                    bool selected=BattlePreparation.Ready&&BattlePreparation.TankTier==2;
                    Result=selected?"PASS: Continue loaded BattleCity without wallet/fuel gating and preserved tank tier 2.":"FAIL: selected tank was lost.";
                    EditorApplication.update-=Verify;Debug.Log("[PreBattle] "+Result);return;
                }
            }
            if(EditorApplication.timeSinceStartup<deadline)return;
            Result="FAIL: BattleCity did not initialize within 30 seconds.";
            EditorApplication.update-=Verify;Debug.LogError("[PreBattle] "+Result);
        }
    }
}
