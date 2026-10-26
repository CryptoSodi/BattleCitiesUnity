using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleCities.Editor
{
    /// <summary>Retain the specific runtime glow shader variants in stripped Web/Android players.</summary>
    public sealed class DropperGlowBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-100;
        public void OnPreprocessBuild(BuildReport report)=>Ensure();
        public static void Ensure()
        {
            const string folder="Assets/BattleCities/Resources/RuntimeShaders";
            var variants=new HashSet<string>();int index=0;
            foreach(string model in new[]{"image-built-tank","tracked-upgrade","twin-cannon-ground-up","heavy-fourview","wheeled-scout"})
            foreach(var source in AssetDatabase.LoadAllAssetsAtPath("Assets/BattleCities/Art/Tanks/"+model+"/tank.glb").OfType<Material>())
            {
                if(source.name!="PrimaryPaint"&&source.name!="SecondaryArmor")continue;
                var variant=new Material(source);variant.EnableKeyword(BattleTankAppearance.EmissionKeyword(variant));
                string key=variant.shader.name+":"+string.Join(";",variant.shaderKeywords.OrderBy(k=>k,StringComparer.Ordinal));
                if(!variants.Add(key)){UnityEngine.Object.DestroyImmediate(variant);continue;}
                // Keep variant keywords, not model texture references: native models/textures are delivered OTA.
                foreach(string property in variant.GetTexturePropertyNames())variant.SetTexture(property,null);
                foreach(string property in new[]{"emissiveFactor","_EmissionColor","_EmissiveFactor"})
                    if(variant.HasProperty(property))variant.SetColor(property,BattleTankDropperBlink.GlowColour);
                string name="TankDropperGlow-"+index++,path=folder+"/"+name+".mat";variant.name=name;
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing){EditorUtility.CopySerialized(variant,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(variant);}
                else AssetDatabase.CreateAsset(variant,path);
            }
            if(index==0)throw new BuildFailedException("No tank armour shaders found for dropper glow variants.");
            AssetDatabase.SaveAssets();
            Debug.Log("DROPPER GLOW: "+index+" distinct shader variant anchors retained.");
        }
    }
}
