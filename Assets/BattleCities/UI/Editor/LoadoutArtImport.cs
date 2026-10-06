using UnityEditor;
using UnityEngine;
using BattleCities.UI;

namespace BattleCities.Editor
{
    public static class LoadoutArtImport
    {
        public static void Import()
        {
            const string folder="Assets/BattleCities/UI/Art/loadout/";
            Sprite Load(string file)
            {
                string path=folder+file;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;
                importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var art=AssetDatabase.LoadAssetAtPath<LoadoutArt>("Assets/BattleCities/Resources/LoadoutArt.asset");
            if(!art){art=ScriptableObject.CreateInstance<LoadoutArt>();AssetDatabase.CreateAsset(art,"Assets/BattleCities/Resources/LoadoutArt.asset");}
            art.deploymentPlatform=Load("deployment-platform.png");art.emptySocket=Load("empty-powerup-socket.png");EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        }
    }
}
