using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BattleCities.Editor
{
    public static class PowerupSmokeBuild
    {
        public static void Windows()
        {
            string folder=Environment.GetEnvironmentVariable("BC_POWERUP_SMOKE_OUTPUT");
            if(string.IsNullOrEmpty(folder))throw new InvalidOperationException("BC_POWERUP_SMOKE_OUTPUT is required.");
            Directory.CreateDirectory(folder);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/BattleCities/Scenes/Login.unity","Assets/BattleCities/Scenes/MainMenu.unity","Assets/BattleCities/Scenes/BattleCity.unity"},
                locationPathName=Path.Combine(folder,"BattleCitiesPowerupSmoke.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Powerup smoke build failed: "+result.summary.result);
        }
    }
}
