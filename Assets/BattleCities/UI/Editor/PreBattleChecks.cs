using UnityEditor;

namespace BattleCities.UI.Editor
{
    public static class PreBattleChecks
    {
        public static string Result=>BattleCities.Editor.LoadoutChecks.Status;
        [MenuItem("Battle Cities/UI/Test Loadout Flow")]
        public static void Run()=>BattleCities.Editor.LoadoutChecks.Run();
    }
}