using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleCities.UI.Editor
{
    public static class PreBattleSetup
    {
        [MenuItem("Battle Cities/UI/Refresh Tank Selection Art")]
        public static void BuildArt()
        {
            const string folder="Assets/BattleCities/UI/Art/pre-battle";
            Directory.CreateDirectory(folder);
            var names=new[]{"image-built-tank","tracked-upgrade","twin-cannon-ground-up","heavy-fourview"};
            var textures=new Texture2D[4];
            var cardNames=new[]{"vanguard","striker","twin-fang","siegebreaker"};
            bool hasCardArt=true;
            for(int i=0;i<cardNames.Length;i++)
            {
                textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/tank-card-"+cardNames[i]+".png");
                if(!textures[i])hasCardArt=false;
            }
            if(!hasCardArt)
            for(int i=0;i<4;i++)
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BattleCities/Art/Tanks/"+names[i]+"/tank.glb");
                if(!model)throw new InvalidOperationException("Missing tank: "+names[i]);
                var preview=new PreviewRenderUtility();
                try
                {
                    var instance=(GameObject)UnityEngine.Object.Instantiate(model);
                    preview.AddSingleGO(instance);
                    var renderers=instance.GetComponentsInChildren<Renderer>();
                    var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                    camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.z)*1.7f;
                    camera.nearClipPlane=.01f;camera.farClipPlane=1000;
                    camera.transform.position=bounds.center+new Vector3(-1.1f,1.3f,-1.7f)*bounds.size.magnitude;
                    camera.transform.LookAt(bounds.center);
                    preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);
                    preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.65f,.65f,.65f);
                    // EndStaticPreview returns RGB and discards the transparent background.
                    preview.BeginPreview(new Rect(0,0,512,512),GUIStyle.none);preview.Render(true);
                    var rendered=(RenderTexture)preview.EndPreview();
                    var previous=RenderTexture.active;
                    var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);
                    try {RenderTexture.active=rendered;texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();}
                    finally {RenderTexture.active=previous;}
                    string path=folder+"/tank-"+i+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                    textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                finally {preview.Cleanup();}
            }
            const string assetPath="Assets/BattleCities/Resources/PreBattleArt.asset";
            var art=AssetDatabase.LoadAssetAtPath<PreBattleArt>(assetPath);
            if(!art){art=ScriptableObject.CreateInstance<PreBattleArt>();AssetDatabase.CreateAsset(art,assetPath);}
            art.tanks=textures;art.powerups=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BattleCities/Art/Powerups/atlas.png");
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        }
    }
}
