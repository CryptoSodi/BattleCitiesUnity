using BattleCities.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        BattleResultsScreen resultsScreen;
        Core.BattleSimulation reportedSimulation;
        bool leavingResults;
        public bool ResultsVisible => resultsScreen && resultsScreen.gameObject.activeSelf;
        public bool HasMatchResult => !tvReplay && !IsReplaying && Simulation != null && (Simulation.Won || Simulation.Lost);

        bool UpdateResults()
        {
            if (!HasMatchResult) { if (ResultsVisible) CloseResults(); return false; }
            if (reportedSimulation == Simulation && ResultsVisible) return true;
            reportedSimulation = Simulation;
            CancelTouchGameplay(); ResetPrimaryFire(); showDebug = false;
            if (Multiplayer.BattleSession.Instance) Multiplayer.BattleSession.Instance.Lobby.Hide();
            hud.SetVisible(false);
            if (touchControls) touchControls.enabled = false;
            if (gameCamera) gameCamera.enabled = false;
            if (!resultsScreen) resultsScreen = new GameObject("Full-screen battle results").AddComponent<BattleResultsScreen>();
            string name = PlayerPrefs.GetString("battlecities.playerName", PlayerPrefs.GetString("battlecities.guestName", "GUEST PLAYER"));
            int high = PlayerPrefs.GetInt(BattlePreparation.OwnerProvider == "guest" ? "battlecities.guestHighScore" : "battlecities.highScore", 0);
            resultsScreen.Show(BattleResultsData.Capture(Simulation, name, high), ContinueResults, LeaveResults);
            return true;
        }

        void CloseResults()
        {
            if (resultsScreen) resultsScreen.Hide();
            reportedSimulation = null;
            leavingResults = false;
            if (gameCamera) gameCamera.enabled = true;
            if (touchControls && !IsReplaying) touchControls.enabled = true;
            BlockControllerTransition();
        }

        void ContinueResults()
        {
            if (!HasMatchResult) return;
            if (LevelEditor.LevelEditorPlaytest.IsActive) { LeaveResults(); return; }
            if (IsOnline && Simulation.Won && !Simulation.IsPvp && Stage < 35)
            {
                if (NetworkMatch.Object.HasStateAuthority) NetworkMatch.RequestNextStage();
                resultsScreen.SetStatus(NetworkMatch.Object.HasStateAuthority ? "PREPARING NEXT STAGE" : "WAITING FOR HOST", true);
                return;
            }
            if (!IsOnline && Simulation.Won && Stage < 35) { CloseResults(); LoadStage(Stage + 1); return; }
            LeaveResults();
        }

        async void LeaveResults()
        {
            if (leavingResults) return;
            leavingResults = true;
            if (resultsScreen) resultsScreen.SetStatus("RETURNING TO MENU", true);
            if (IsOnline && Multiplayer.BattleSession.Instance) await Multiplayer.BattleSession.Instance.Leave();
            if (this)
            {
                if (Multiplayer.BattleSession.Instance) Multiplayer.BattleSession.Instance.Lobby.Hide();
                SceneManager.LoadScene(LevelEditor.LevelEditorPlaytest.IsActive ? "LevelEditor" : "MainMenu");
            }
        }
    }
}
