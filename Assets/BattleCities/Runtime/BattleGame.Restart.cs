using UnityEngine.SceneManagement;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private bool restartReturningToMenu;
#if UNITY_EDITOR
        public System.Action EditorRestartToMenuOverride;
#endif
        public void RequestBattleRestart()
        {
            if(restartReturningToMenu||IsOnline)return;
            if(IsReplaying){RestartReplay();return;}
            if(!BattlePreparation.Ready||LevelEditor.LevelEditorPlaytest.IsActive)
            {LoadStage(Stage);return;}

            // A new attempt goes through the same loadout and fuel confirmation as START.
            // The receipt for an attempt that never reached tick one remains reusable there.
            restartReturningToMenu=true;paused=true;CancelTouchGameplay();FinishRecording();
            BattlePreparation.RequestRestart(BattlePreparation.TankTier,Stage);
#if UNITY_EDITOR
            if(EditorRestartToMenuOverride!=null){EditorRestartToMenuOverride();return;}
#endif
            SceneManager.LoadScene("MainMenu");
        }
    }
}
