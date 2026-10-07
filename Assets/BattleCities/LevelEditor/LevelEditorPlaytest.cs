using BattleCities.Core;

namespace BattleCities.LevelEditor
{
    // SessionState survives domain reload without shipping a draft in a build.
    public static class LevelEditorPlaytest
    {
        public const string Key = "BattleCities.LevelEditor.";
        public static bool IsActive
        {
            get
            {
#if UNITY_EDITOR
                return UnityEngine.Application.isPlaying && UnityEditor.SessionState.GetBool(Key + "Testing", false);
#else
                return false;
#endif
            }
        }
        public static bool IsAuthoringOrTesting => IsActive || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LevelEditor";
        public static int PreviewStage
        {
            get
            {
#if UNITY_EDITOR
                return UnityEngine.Mathf.Clamp(UnityEditor.SessionState.GetInt(Key + "Stage", 1), 1, 35);
#else
                return 1;
#endif
            }
        }
        public static MapData Draft
        {
            get
            {
#if UNITY_EDITOR
                return IsActive ? Newtonsoft.Json.JsonConvert.DeserializeObject<MapData>(UnityEditor.SessionState.GetString(Key + "Draft", "{}")) : null;
#else
                return null;
#endif
            }
        }
    }
}
