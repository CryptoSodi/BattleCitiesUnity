using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    [InitializeOnLoad]
    public static class LoginSmokeCheck
    {
        private const string Key = "BattleCities.LoginSmoke";
        static LoginSmokeCheck() { EditorApplication.update += Tick; }
        [MenuItem("Battle Cities/Login/Test guest flow")]
        public static void Run()
        {
            if (SceneManager.GetActiveScene().name != "Login" || EditorApplication.isPlaying)
            { Debug.LogError("Open Login outside Play Mode before testing."); return; }
            SessionState.SetString(Key+"Mode", PlayerPrefs.GetString("battlecities.loginMode", ""));
            SessionState.SetString(Key+"Name", PlayerPrefs.GetString("battlecities.playerName", ""));
            SessionState.SetString(Key+"GuestId", PlayerPrefs.GetString("battlecities.guestId", ""));
            SessionState.SetString(Key+"GuestName", PlayerPrefs.GetString("battlecities.guestName", ""));
            SessionState.SetBool(Key+"HadGuestId", PlayerPrefs.HasKey("battlecities.guestId"));
            SessionState.SetBool(Key+"HadGuestName", PlayerPrefs.HasKey("battlecities.guestName"));
            SessionState.SetInt(Key, 1);
            SessionState.SetFloat(Key+"Deadline", (float)EditorApplication.timeSinceStartup + 45);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key+"Deadline", 0))
            { var a = Object.FindFirstObjectByType<MainMenuApiClient>(); Finish(false, "stage=" + stage + " scene=" + SceneManager.GetActiveScene().name + " playing=" + EditorApplication.isPlaying + " api=" + (a ? a.enabled + "/" + a.LastStatusTitle : "missing")); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (stage == 1)
            {
                var flow = Object.FindFirstObjectByType<LoginScene>();
                if (!flow) return;
                var api = Object.FindFirstObjectByType<MainMenuApiClient>();
                if (!api || !api.isActiveAndEnabled) return;
                SessionState.SetInt(Key, 2);
                Application.runInBackground = true;
                Debug.Log("LOGIN_SMOKE: invoking guest");
                flow.ContinueAsGuest();
                Debug.Log("LOGIN_SMOKE: local=" + api.IsLocalGuest + " status=" + api.LastStatusTitle);
            }
            else if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                var api = Object.FindFirstObjectByType<MainMenuApiClient>();
                if (api && api.IsLocalGuest) Finish(!api.IsWalletAuthenticated, "Guest reached MainMenu as local guest, not wallet-authenticated.");
            }
        }
        private static void Finish(bool pass, string detail)
        {
            SessionState.SetInt(Key, 0);
            PlayerPrefs.SetString("battlecities.loginMode", SessionState.GetString(Key+"Mode", ""));
            PlayerPrefs.SetString("battlecities.playerName", SessionState.GetString(Key+"Name", ""));
            if(SessionState.GetBool(Key+"HadGuestId",false))PlayerPrefs.SetString("battlecities.guestId",SessionState.GetString(Key+"GuestId",""));
            else PlayerPrefs.DeleteKey("battlecities.guestId");
            if(SessionState.GetBool(Key+"HadGuestName",false))PlayerPrefs.SetString("battlecities.guestName",SessionState.GetString(Key+"GuestName",""));
            else PlayerPrefs.DeleteKey("battlecities.guestName");
            PlayerPrefs.Save();
            if (pass) Debug.Log("LOGIN_SMOKE_PASS: " + detail); else Debug.LogError("LOGIN_SMOKE_FAIL: " + detail);
            EditorApplication.isPlaying = false;
        }
    }
}
