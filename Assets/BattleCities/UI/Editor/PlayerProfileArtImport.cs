using System;
using System.IO;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class PlayerProfileArtImport
    {
        const string Destination="Assets/BattleCities/UI/Art/player-profile-v2";
        public static void Import(string source="C:/repos/BattleCity/public/assets/player-profile-v2")
        {
            string[] names={"profile-insignia","commander-avatar","matches-swords","defeat-skull","victory-medal","share-arrow"};
            foreach(var name in names)if(!File.Exists(source+"/"+name+".png"))throw new FileNotFoundException("Profile artwork is not ready: "+name);
            Directory.CreateDirectory(Destination);
            var resource="Assets/BattleCities/UI/Resources/PlayerProfileArt.asset";
            var art=AssetDatabase.LoadAssetAtPath<PlayerProfileArt>(resource);
            if(!art){art=ScriptableObject.CreateInstance<PlayerProfileArt>();AssetDatabase.CreateAsset(art,resource);}
            var sprites=names.Select(name=>ImportSprite(source+"/"+name+".png",name)).ToArray();
            art.insignia=sprites[0];art.commander=sprites[1];art.matches=sprites[2];art.defeat=sprites[3];art.victory=sprites[4];art.share=sprites[5];
            if(File.Exists(source+"/manifest.json")){File.Copy(source+"/manifest.json",Destination+"/source.json",true);AssetDatabase.ImportAsset(Destination+"/source.json");}
            EditorUtility.SetDirty(art);
            var links=AssetDatabase.LoadAssetAtPath<ProfileLinks>("Assets/BattleCities/UI/Resources/ProfileLinks.asset");
            if(!links){links=ScriptableObject.CreateInstance<ProfileLinks>();AssetDatabase.CreateAsset(links,"Assets/BattleCities/UI/Resources/ProfileLinks.asset");}
            links.publicWebBaseUrl=links.replayWebBaseUrl="https://battlecities.com/";EditorUtility.SetDirty(links);AssetDatabase.SaveAssets();
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();var screen=menu?menu.GetComponent<PlayerProfileScreen>():null;
            if(menu&&screen){screen.Configure(menu,AssetDatabase.LoadAssetAtPath<MenuTheme>(MainMenuBuilder.Root+"Settings/ArcadeMenuTheme.asset"),menu.GetComponent<MainMenuApiClient>(),menu.Content.Find("Main Display") as RectTransform);menu.RefreshLayout();screen.Resume();}
            Debug.Log("[PlayerProfileArt] Imported six new assets; public and replay links use battlecities.com.");
        }
        static Sprite ImportSprite(string source,string name)
        {
            string path=Destination+"/"+name+".png";File.Copy(source,path,true);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=100;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.ASTC_6x6});
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(source));
            try
            {
                var pixels=texture.GetPixels32();int w=texture.width,h=texture.height,l=w,r=-1,b=h,t=-1;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[y*w+x].a>4){l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);}
                int pad=Mathf.CeilToInt(Mathf.Max(r-l+1,t-b+1)*.035f);
                Rect rect=Rect.MinMaxRect(Math.Max(0,l-pad),Math.Max(0,b-pad),Math.Min(w,r+pad+1),Math.Min(h,t+pad+1));
                importer.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable 618
                importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=rect,pivot=new Vector2(.5f,.5f),alignment=9}};
#pragma warning restore 618
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
    }
}
