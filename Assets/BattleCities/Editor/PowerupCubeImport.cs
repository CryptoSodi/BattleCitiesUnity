using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities.Editor
{
    public static class PowerupCubeImport
    {
        public const string ModelPath="Assets/BattleCities/Art/Powerups/Cube/powerup-cube.glb";
        public const string PrefabPath="Assets/BattleCities/Resources/PowerupCube.prefab";
        const string Folder="Assets/BattleCities/Art/Powerups/Cube/";
        public static void Run()
        {
            AssetDatabase.Refresh();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if(!model)throw new InvalidOperationException("Missing authored power-up cube: "+ModelPath);
            var root=new GameObject("PowerupCube");
            try{
                var view=root.AddComponent<BattlePickupCube>();
                var shell=UnityEngine.Object.Instantiate(model,root.transform);shell.name="Gold frame and blue faces";
                foreach(var placeholder in shell.GetComponentsInChildren<Transform>(true))
                    if(placeholder.name=="DecalSurfaces")placeholder.gameObject.SetActive(false);
                var renderers=shell.GetComponentsInChildren<Renderer>(true);
                renderers=Array.FindAll(renderers,r=>r.gameObject.activeInHierarchy);
                if(renderers.Length==0)throw new Exception("Cube model has no renderers");
                Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers){
                    bounds.Encapsulate(renderer.bounds);
                    foreach(var material in renderer.sharedMaterials)if(!material||!material.shader||material.shader.name=="Hidden/InternalErrorShader")throw new Exception("Cube material import failed");
                    foreach(var material in renderer.sharedMaterials)Debug.Log("CUBE MATERIAL "+material.name+" shader="+material.shader.name+" color="+material.color+" queue="+material.renderQueue);
                    renderer.sharedMaterials=Array.ConvertAll(renderer.sharedMaterials,ShellMaterial);
                }
                float scale=BattlePickupSizing.WorldSize/Mathf.Max(bounds.size.x,bounds.size.z);
                shell.transform.localScale*=scale;
                shell.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
                if(Mathf.Abs(bounds.size.y/Mathf.Max(bounds.size.x,bounds.size.z)-1f)>.02f)throw new Exception("Authored model must have cubic proportions");
                view.EmblemMaterial=Transparent("Emblem","Universal Render Pipeline/Unlit");
                view.EmblemMaterial.SetFloat("_Cull",2);
                view.HaloMaterial=Transparent("Halo","Universal Render Pipeline/Particles/Unlit");
                view.CurrencyIllustration=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleCities/UI/Art/shared/icons/batc-rewards-coin.png");
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
                Debug.Log("POWERUP CUBE IMPORTED: "+PrefabPath+"; reusable tile-relative mesh, smaller animated hovering body, floor halo and transparent shop illustrations.");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
            PowerupCubeChecks.Run();
        }
        static Material ShellMaterial(Material source)
        {
            string path=Folder+source.name+"-Glow.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            bool gold=source.name.IndexOf("Gold",StringComparison.OrdinalIgnoreCase)>=0;
            material.SetColor("_BaseColor",source.color);material.SetFloat("_Metallic",gold?.62f:.12f);
            material.SetFloat("_Smoothness",gold?.73f:.68f);material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor",gold?new Color(.42f,.16f,.008f):new Color(.012f,.045f,.16f));
            // Keep Unity's emission variant enabled after material import. This does not bake lighting.
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;material.enableInstancing=true;
            EditorUtility.SetDirty(material);return material;
        }
        static Material Transparent(string name,string shaderName)
        {
            string path=Folder+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find(shaderName));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
            material.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(material);return material;
        }
    }
}
