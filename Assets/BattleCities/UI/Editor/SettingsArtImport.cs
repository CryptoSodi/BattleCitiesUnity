using System;
using System.IO;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class SettingsArtImport
    {
        const string Destination="Assets/BattleCities/UI/Art/settings-v2";
        public static void Import(string source="C:/repos/BattleCity/public/assets/settings-v2")
        {
            string[] names={"mute-speaker","scanline-monitor","account-wallet","phone-controller","logout-button-red"};
            foreach(var name in names)if(!File.Exists(source+"/"+name+".png"))throw new FileNotFoundException("Settings artwork is not ready: "+name);
            Directory.CreateDirectory(Destination);
            var resource="Assets/BattleCities/UI/Resources/SettingsArt.asset";
            var art=AssetDatabase.LoadAssetAtPath<SettingsArt>(resource);
            if(!art){art=ScriptableObject.CreateInstance<SettingsArt>();AssetDatabase.CreateAsset(art,resource);}
            var sprites=names.Select((name,index)=>ImportSprite(source+"/"+name+".png",name,index==4)).ToArray();
            art.mute=sprites[0];art.scanline=sprites[1];art.account=sprites[2];art.phone=sprites[3];art.logout=sprites[4];
            if(File.Exists(source+"/manifest.json")){File.Copy(source+"/manifest.json",Destination+"/source.json",true);AssetDatabase.ImportAsset(Destination+"/source.json");}
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            var screen=menu?menu.GetComponent<SettingsScreen>():null;
            if(menu&&screen)
            {
                bool wasOpen=screen.IsOpen;
                var skin=AssetDatabase.LoadAssetAtPath<MenuTheme>(MainMenuBuilder.Root+"Settings/ArcadeMenuTheme.asset");
                screen.Configure(menu,skin,menu.GetComponent<MainMenuApiClient>(),menu.Content.Find("Main Display") as RectTransform);
                if(wasOpen)screen.Open();menu.RefreshLayout();
            }
            Debug.Log("[SettingsArt] Imported five newly generated Settings assets.");
        }
        static Sprite ImportSprite(string source,string name,bool skin)
        {
            var path=Destination+"/"+name+".png";File.Copy(source,path,true);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=2048;importer.spritePixelsPerUnit=100;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.ASTC_6x6});
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(source));
            try
            {
                var pixels=texture.GetPixels32();int w=texture.width,h=texture.height,l=w,r=-1,b=h,t=-1;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[y*w+x].a>4){l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);}
                int pad=skin?0:Mathf.CeilToInt(Mathf.Max(r-l+1,t-b+1)*.035f);
                Rect rect=Rect.MinMaxRect(Math.Max(0,l-pad),Math.Max(0,b-pad),Math.Min(w,r+pad+1),Math.Min(h,t+pad+1));
                importer.spriteImportMode=SpriteImportMode.Multiple;
                Vector4 border=skin?Vector4.one*128f:Vector4.zero;
#pragma warning disable 618
                importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=rect,pivot=new Vector2(.5f,.5f),alignment=9,border=border}};
#pragma warning restore 618
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
    }
}
