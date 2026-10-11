using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BattleCities.Editor
{
    /// <summary>Set the native launch orientation before Android creates its splash window.</summary>
    public sealed class AndroidStartupOrientation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Configure();
        }

        public static void Configure()
        {
            bool psg1 = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(';').Any(symbol => symbol.Trim() == "BATTLE_CITIES_PSG1");
            // PSG1's natural 1240 x 1080 display uses Android's Portrait orientation.
            // Phone builds must start in the same landscape orientation as the runtime.
            PlayerSettings.defaultInterfaceOrientation = psg1 ? UIOrientation.Portrait : UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = psg1;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = !psg1;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }
    }
}
