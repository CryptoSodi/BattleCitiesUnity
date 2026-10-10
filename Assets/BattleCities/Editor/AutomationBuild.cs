using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    /// <summary>Shared configuration for the live Editor and command-line release builds.</summary>
    public static class AutomationBuild
    {
        [Serializable]
        public sealed class Request
        {
            public string version, target, outputPath, reportPath;
            public int androidVersionCode;
            public bool buildAppBundle, testNetwork;
        }

        [Serializable]
        sealed class Receipt
        {
            public string version, platform, outputPath, result;
            public int totalErrors, totalWarnings;
            public long totalSizeBytes;
            public double buildTimeMs;
        }

        public static Request ReadRequest(string path)
        {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
            if (request == null || !Regex.IsMatch(request.version ?? "", @"^\d+\.\d+\.\d+$"))
                throw new BuildFailedException("The build request needs a three-part version.");
            if (request.target != "WebGL" && request.target != "Android")
                throw new BuildFailedException("Only WebGL and Android builds are supported.");
            ValidateOutput(request.outputPath);
            ValidateOutput(request.reportPath);
            if (request.target == "Android")
            {
                string extension = request.buildAppBundle ? ".aab" : ".apk";
                if (!string.Equals(Path.GetExtension(request.outputPath), extension, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Android output must have the " + extension + " extension for the selected format.");
            }
            return request;
        }

        static void ValidateOutput(string path)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.IsNullOrWhiteSpace(path) || !Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Automation output must stay inside this project's Builds directory.");
        }

        public static void ConfigureFromFile(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new BuildFailedException("Stop Play Mode and wait for compilation or the current build to finish.");
            var request = ReadRequest(path);
            var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), request.target);
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException("Install " + request.target + " build support for this project's Unity version in Unity Hub.");
            PlayerSettings.bundleVersion = request.version;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            if (request.target == "Android")
            {
                if (request.androidVersionCode < 1)
                    throw new BuildFailedException("Android version code must be positive.");
                PlayerSettings.Android.bundleVersionCode = request.androidVersionCode;
                EditorUserBuildSettings.buildAppBundle = request.buildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                // Keep the project's existing signing identity. Never write passwords into a request file.
                string storePassword = Environment.GetEnvironmentVariable("BATTLECITIES_ANDROID_KEYSTORE_PASSWORD");
                string aliasPassword = Environment.GetEnvironmentVariable("BATTLECITIES_ANDROID_KEY_ALIAS_PASSWORD");
                if (!string.IsNullOrEmpty(storePassword)) PlayerSettings.Android.keystorePass = storePassword;
                if (!string.IsNullOrEmpty(aliasPassword)) PlayerSettings.Android.keyaliasPass = aliasPassword;
                if (PlayerSettings.Android.useCustomKeystore &&
                    (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass)))
                    throw new BuildFailedException("Enter the existing keystore passwords in Unity Publishing Settings, or set the BATTLECITIES_ANDROID_*_PASSWORD environment variables before a closed-editor build.");
            }
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isDirty && string.IsNullOrEmpty(scene.path))
                    throw new BuildFailedException("Save the untitled scene before running a build.");
            }
            if (!EditorSceneManager.SaveOpenScenes()) throw new BuildFailedException("Could not save the open scenes.");
            if (request.target == "WebGL")
            {
                PhotonMultiplayerSetup.Configure();
                MultiplayerChunkSetup.Configure();
                var fusionConfig=Fusion.NetworkProjectConfig.Global;
                fusionConfig.AllowClientServerModesInWebGL=true;
                Fusion.Editor.NetworkProjectConfigUtilities.SaveGlobalConfig(fusionConfig);
                const string testDefine = "BATTLECITIES_DEVNET";
                var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL)
                    .Split(';').Where(value => !string.IsNullOrWhiteSpace(value) && value != testDefine).ToList();
                // Network selection is runtime hostname-based; remove the obsolete build override.
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, string.Join(";", defines));
            }
            else if (request.testNetwork) throw new BuildFailedException("The test deployment currently supports WebGL only.");
            AssetDatabase.SaveAssets();
        }

        // unity build --execute-method BattleCities.Editor.AutomationBuild.Build
        public static void Build()
        {
            string path = Environment.GetEnvironmentVariable("BATTLECITIES_BUILD_REQUEST");
            if (string.IsNullOrWhiteSpace(path)) throw new BuildFailedException("BATTLECITIES_BUILD_REQUEST is missing.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled scenes in Build Settings.");
            // A fresh batch Editor starts with an untitled scene. Open a configured
            // scene before the shared save step, which cannot show a save dialog here.
            if (Application.isBatchMode) EditorSceneManager.OpenScene(scenes[0], OpenSceneMode.Single);
            ConfigureFromFile(path);
            var request = ReadRequest(path);
            Directory.CreateDirectory(Path.GetDirectoryName(request.outputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = request.outputPath,
                target = (BuildTarget)Enum.Parse(typeof(BuildTarget), request.target),
                options = BuildOptions.DetailedBuildReport
            });
            var summary = report.summary;
            var receipt = new Receipt
            {
                version = request.version, platform = request.target, outputPath = request.outputPath,
                result = summary.result.ToString(), totalErrors = (int)summary.totalErrors,
                totalWarnings = (int)summary.totalWarnings, totalSizeBytes = (long)summary.totalSize,
                buildTimeMs = summary.totalTime.TotalMilliseconds
            };
            File.WriteAllText(request.reportPath, JsonUtility.ToJson(receipt, true));
            File.WriteAllLines(Path.ChangeExtension(request.reportPath, ".messages.txt"),
                report.steps.SelectMany(step => step.messages).Select(message => message.type + ": " + message.content));
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                throw new BuildFailedException("Build failed. See " + request.reportPath);
            Debug.Log("BATTLE CITIES BUILD SUCCEEDED: " + request.outputPath);
        }
    }
}
