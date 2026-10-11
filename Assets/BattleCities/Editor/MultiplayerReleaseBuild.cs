using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BattleCities
{
    public static class MultiplayerReleaseBuild
    {
        public static void Windows()
        {
            PhotonMultiplayerSetup.Configure();
            MultiplayerChunkSetup.Configure();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            string output=Environment.GetEnvironmentVariable("BATTLECITIES_WINDOWS_OUTPUT")??"Builds/Multiplayer/BattleCities.exe";
            PhotonMultiplayerSetup.BuildWindowsSmoke(Path.GetFullPath(output));
            string result=File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)),"build-result.txt"));
            if(!result.StartsWith("Succeeded"))throw new Exception("Windows multiplayer build failed: "+result);
        }
        public static void LinuxServer()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneLinux64))
                throw new InvalidOperationException("Install Linux Dedicated Server Build Support for this Unity version before building.");
            string folder=Environment.GetEnvironmentVariable("BATTLECITIES_SERVER_OUTPUT")??"Builds/DedicatedLinux";
            Directory.CreateDirectory(folder);
            PhotonMultiplayerSetup.Configure();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/BattleCities/Scenes/Login.unity","Assets/BattleCities/Scenes/MainMenu.unity","Assets/BattleCities/Scenes/BattleCity.unity"},
                target=BuildTarget.StandaloneLinux64,subtarget=(int)StandaloneBuildSubtarget.Server,
                locationPathName=Path.Combine(folder,"BattleCitiesServer.x86_64"),options=BuildOptions.None});
            File.WriteAllText(Path.Combine(folder,"build-result.txt"),report.summary.result+" errors="+report.summary.totalErrors);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Dedicated server build failed.");
        }
    }
}
