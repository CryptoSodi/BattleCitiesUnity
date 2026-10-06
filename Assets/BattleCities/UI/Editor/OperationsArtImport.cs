using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class OperationsArtImport
    {
        const string Destination="Assets/BattleCities/UI/Art/operations";
        const string Resource="Assets/BattleCities/UI/Resources/OperationsArt.asset";
        // Newly generated illustrations requested by the user; import only, never rebuild the scene.
        public static void Import(string legacyRoot="C:/repos/BattleCity")
        {
            Directory.CreateDirectory(Destination);
            string[] sections={"treasury-safe","field-manual","campaigns-medal","staking-lock","trading-arrows","boosts-chevrons","airdrop-crate"};
            string[] social={"website","x-follow","instagram","discord","x-repost","x-comment"};
            string[] sources=sections.Select(n=>legacyRoot+"/public/assets/headquarters-v2/"+n+".png")
                .Concat(social.Select(n=>legacyRoot+"/public/assets/socials-v2/"+n+".png"))
                .Concat(new[]{"enemy-scout","enemy-rapid","enemy-armored","enemy-heavy","cannon-module"}.Select(n=>legacyRoot+"/public/assets/headquarters/manual/"+n+".png")).ToArray();
            var missing=sources.Where(p=>!File.Exists(p)).ToArray();
            if(missing.Length>0)throw new FileNotFoundException("New operations artwork is not ready: "+string.Join(", ",missing));
            var art=AssetDatabase.LoadAssetAtPath<OperationsArt>(Resource);
            if(!art){art=ScriptableObject.CreateInstance<OperationsArt>();AssetDatabase.CreateAsset(art,Resource);}
            art.quarters=sections.Select(n=>ImportSprite(legacyRoot+"/public/assets/headquarters-v2/"+n+".png",n)).ToArray();
            art.socials=social.Select(n=>ImportSprite(legacyRoot+"/public/assets/socials-v2/"+n+".png","social-"+n)).ToArray();
            var tankArt=Resources.Load<PreBattleArt>("PreBattleArt");
            art.playerTanks=new Sprite[4];
            for(int i=0;i<4;i++)if(tankArt&&tankArt.tanks.Length>i&&tankArt.tanks[i])
                art.playerTanks[i]=ImportSprite(AssetDatabase.GetAssetPath(tankArt.tanks[i]),"manual-player-"+i);
            string[] enemies={"scout","rapid","armored","heavy"};
            art.enemies=new Sprite[4];
            for(int i=0;i<4;i++)
            {
                var path=legacyRoot+"/public/assets/headquarters/manual/enemy-"+enemies[i]+".png";
                art.enemies[i]=ImportSprite(path,"manual-"+enemies[i]);
            }
            var cannonPath=legacyRoot+"/public/assets/headquarters/manual/cannon-module.png";
            art.cannon=ImportSprite(cannonPath,"manual-cannon");
            var source=File.ReadAllText(legacyRoot+"/src/wiki/WikiData.ts");
            var entries=new System.Collections.Generic.List<FieldManualEntry>();
            foreach(Match category in Regex.Matches(source,@"(?<category>tanks|weapons|powerups|enemies):\s*\[(?<rows>[\s\S]*?)\]"))
                foreach(Match row in Regex.Matches(category.Groups["rows"].Value,@"\{(?<body>[\s\S]*?)\}"))
                {
                    string Read(string key){var m=Regex.Match(row.Groups["body"].Value,key+@":\s*'([^']*)'");return m.Success?m.Groups[1].Value:"";}
                    entries.Add(new FieldManualEntry{category=category.Groups["category"].Value,slug=Read("slug"),name=Read("name"),role=Read("role"),lore=Read("lore"),effect=Read("effect"),source=Read("source")});
                }
            var jsonPath=Destination+"/field-manual.json";
            File.WriteAllText(jsonPath,JsonUtility.ToJson(new FieldManualEntries{entries=entries.ToArray()},true));
            AssetDatabase.ImportAsset(jsonPath);art.manual=AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            File.WriteAllText(Destination+"/sources.json","{\n  \"source\": \"BattleCity/public/assets/headquarters-v2, socials-v2, headquarters/manual\",\n  \"artChat\": \"01a094c4-7348-79f3-9b5b-13a6013430db\",\n  \"manualContent\": \"BattleCity/src/wiki/WikiData.ts\",\n  \"purpose\": \"Quarters, Treasury, Field Manual and Socials UI\",\n  \"importer\": \"OperationsArtImport; whole transparent sprites, preserved proportions\"\n}\n");
            foreach(var folder in new[]{"headquarters-v2","socials-v2","headquarters/manual"})
            {
                string manifest=legacyRoot+"/public/assets/"+folder+"/manifest.json";
                if(File.Exists(manifest)){string target=Destination+"/"+folder.Replace('/','-')+".source.json";File.Copy(manifest,target,true);AssetDatabase.ImportAsset(target);}
            }
            AssetDatabase.ImportAsset(Destination+"/sources.json");
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssetIfDirty(art);
            Debug.Log("Operations artwork imported: 7 sections, 6 social cards, "+entries.Count+" manual entries.");
        }
        static Sprite ImportSprite(string source,string name)
        {
            if(!File.Exists(source)){Debug.LogWarning("Operations artwork missing: "+source);return null;}
            string path=Destination+"/"+name+".png";File.Copy(source,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6});
            importer.spriteImportMode=SpriteImportMode.Multiple;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            // Crop only transparent sprite margins in import metadata, leaving every source pixel intact.
            // This makes each newly generated object read at the same size in cards and title slots.
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(source));
            Rect visible;
            try
            {
                var pixels=texture.GetPixels32();int width=texture.width,height=texture.height,left=width,right=-1,bottom=height,top=-1;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>4){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
                int pad=Mathf.CeilToInt(Mathf.Max(right-left+1,top-bottom+1)*.035f);
                visible=right<left?new Rect(0,0,width,height):Rect.MinMaxRect(Math.Max(0,left-pad),Math.Max(0,bottom-pad),Math.Min(width,right+pad+1),Math.Min(height,top+pad+1));
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
#pragma warning disable 618
            importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=visible,pivot=new Vector2(.5f,.5f),alignment=9}};
#pragma warning restore 618
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }
    }
}
