using System;
using System.IO;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BattleCities.Editor
{
    public static class ExtendedPowerupArtImport
    {
        const string Folder="Assets/BattleCities/UI/Art/shop/";
        static Texture2D Png(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));return t;}
        static Sprite Import(string name)
        {
            string path=Folder+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        public static void Run()
        {
            var atlas=Png("Assets/BattleCities/Art/Powerups/atlas.png");
            var art=AssetDatabase.LoadAssetAtPath<ExtendedPowerupArt>("Assets/BattleCities/Resources/ExtendedPowerupArt.asset");
            if(!art){art=ScriptableObject.CreateInstance<ExtendedPowerupArt>();AssetDatabase.CreateAsset(art,"Assets/BattleCities/Resources/ExtendedPowerupArt.asset");}
            art.maxHealer=Import("max-healer");art.healthHealer=Import("health-healer");art.rapidShooterLong=Import("rapid-shooter-long");
            art.maxHealerFrame=Frame("max-healer",atlas);art.healthHealerFrame=Frame("health-healer",atlas);art.rapidShooterLongFrame=Frame("rapid-shooter-long",atlas);
            // The short shooter is the existing three-shell atlas tile, not a second item/art variant.
            var shortIcon=new Texture2D(303,283,TextureFormat.RGBA32,false);
            shortIcon.SetPixels(atlas.GetPixels(978,971-621,303,283));shortIcon.Apply();
            File.WriteAllBytes(Folder+"rapid-shooter.png",shortIcon.EncodeToPNG());Object.DestroyImmediate(shortIcon);art.rapidShooter=Import("rapid-shooter");
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();Object.DestroyImmediate(atlas);
            Debug.Log("EXTENDED POWERUP ART READY: supplied healer/ammo art, existing short-shooter tile, framed HUD/drop textures; legacy atlas unchanged.");
        }
        public static void ValidateUi(){Run();ExtendedPowerupUiChecks.Run();}
        static Texture2D Frame(string name,Texture2D atlas)
        {
            var icon=Png(Folder+name+".png");const int w=295,h=289;var result=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var pixels=atlas.GetPixels(24,971-323,w,h);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                // Keep the delivered gold frame; clear only its inner blue well.
                float dx=Mathf.Max(Mathf.Abs(x-(w-1)*.5f)-(w*.5f-49),0),dy=Mathf.Max(Mathf.Abs(y-(h-1)*.5f)-(h*.5f-49),0);
                if(dx*dx+dy*dy<=23*23)pixels[y*w+x]=Color.Lerp(new Color32(0,37,92,255),new Color32(0,99,191,255),(float)y/h);
            }
            float scale=Mathf.Min(222f/icon.width,222f/icon.height);float iw=icon.width*scale,ih=icon.height*scale;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float u=(x-(w-iw)*.5f)/iw,v=(y-(h-ih)*.5f)/ih;if(u<0||u>1||v<0||v>1)continue;
                var fg=icon.GetPixelBilinear(u,v);var bg=pixels[y*w+x];pixels[y*w+x]=new Color(fg.r*fg.a+bg.r*(1-fg.a),fg.g*fg.a+bg.g*(1-fg.a),fg.b*fg.a+bg.b*(1-fg.a),fg.a+bg.a*(1-fg.a));
            }
            result.SetPixels(pixels);result.Apply();string output=Folder+name+"-framed.png";File.WriteAllBytes(output,result.EncodeToPNG());
            Object.DestroyImmediate(icon);Object.DestroyImmediate(result);Import(name+"-framed");return AssetDatabase.LoadAssetAtPath<Texture2D>(output);
        }
    }
}
