using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleCities.Editor
{
    // Runtime-created battle materials have no serialized scene reference.
    // Keep a texture-free material in the player even when models move to a bundle.
    public sealed class RuntimeShaderBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string Path = "Assets/BattleCities/Resources/RuntimeShaders/BattleLit.mat";
        const string ShaderName = "Universal Render Pipeline/Lit";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var shader = Shader.Find(ShaderName);
            if (!shader) throw new BuildFailedException("Missing battle shader: " + ShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Path);
            if (!material)
            {
                material = new Material(shader) { name = "BattleLit", enableInstancing = true };
                material.SetFloat("_Smoothness", .2f);
                AssetDatabase.CreateAsset(material, Path);
                AssetDatabase.SaveAssets();
            }
            if (material.shader != shader || !material.enableInstancing)
                throw new BuildFailedException("BattleLit must retain the Lit shader and instancing variants.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.DetailedBuildReport) == 0) return;
            var shaderPath = AssetDatabase.GetAssetPath(Shader.Find(ShaderName));
            var paths = report.packedAssets.SelectMany(pack => pack.contents).Select(asset => asset.sourceAssetPath);
            if (!paths.Contains(Path) || !paths.Contains(shaderPath))
                throw new BuildFailedException("The player is missing the runtime battle material or Lit shader.");
            Debug.Log("BATTLE SHADER CHECK PASSED: Lit material and shader retained in player.");
        }
    }
}