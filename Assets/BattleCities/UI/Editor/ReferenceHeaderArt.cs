using System;
using System.IO;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ReferenceHeaderArt
    {
        private const string ArtRoot="Assets/BattleCities/UI/Art/reference-style-v2/";
        private const string Prefabs="Assets/BattleCities/UI/Prefabs/Shared/";
        private static Sprite Art(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot+path+".png");

        [MenuItem("Battle Cities/UI/Apply Reference Header Art")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=MainMenuBuilder.ScenePath||scene.isDirty)throw new InvalidOperationException("Open and save MainMenu before applying artwork.");
            Import();
            var view=UnityEngine.Object.FindAnyObjectByType<MainMenuScene>();
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(MainMenuBuilder.Root+"Settings/ArcadeMenuTheme.asset");
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Apply reference header artwork");
            ApplyHeader(view.Content.Find("Status Bar"),theme);
            EditorUtility.SetDirty(theme);
            view.RefreshLayout();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenu] Applied reference header artwork and exported reusable header/icon prefabs.");
        }

        public static void Import()
        {
            AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles(ArtRoot,"*.png",SearchOption.AllDirectories))
            {
                var path=file.Replace('\\','/');
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=100;
                importer.spriteBorder=path.EndsWith("/shared/panels/navigation-container.png")
                    ?new Vector4(78,78,78,78):Vector4.zero;
                importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false; importer.isReadable=false;
                importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                var android=importer.GetPlatformTextureSettings("Android");
                android.overridden=true;android.maxTextureSize=2048;android.format=TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
            }
        }

        public static void ApplyHeader(Transform header,MenuTheme theme)
        {
            theme.SettingsIcon=Art("shared/icons/settings");
            theme.ScoreIcon=Art("shared/icons/score-coin");
            theme.HighScoreIcon=Art("shared/icons/high-score-shield");
            if(!theme.SettingsIcon)throw new InvalidOperationException("Import reference-style-v2 artwork first.");
            var icons=new[]{theme.SettingsIcon,theme.ScoreIcon,theme.HighScoreIcon};
            for(int i=0;i<3;i++)
            {
                var card=header.GetChild(i);
                var image=card.GetComponent<Image>();
                image.sprite=Art(i==0?"shared/header/player-card":"shared/header/score-card");
                image.type=Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;
                var socket=card.Find("Icon tile");
                socket.GetComponent<Image>().enabled=false; // The new frame already includes its cream socket.
                MainMenuBuilder.Box((RectTransform)socket,i==0?.066f:.066f,.14f,.169f,.71f);
                var icon=socket.Find("Icon").GetComponent<Image>();
                icon.sprite=icons[i];icon.preserveAspect=true;
                MainMenuBuilder.Box(icon.rectTransform,.075f,.04f,.85f,.92f);
                var readout=card.Find("Readout");
                readout.GetComponent<Image>().enabled=false;
                MainMenuBuilder.Stretch((RectTransform)readout);
                var title=readout.Find("Label").GetComponent<Text>();
                var value=readout.Find("Value").GetComponent<Text>();
                if(i==0)
                {
                    MainMenuBuilder.Box(title.rectTransform,.27f,.16f,.64f,.38f);
                    MainMenuBuilder.Box(value.rectTransform,.772f,.585f,.142f,.235f);
                    value.color=Color.white;value.fontSize=20;value.resizeTextMaxSize=20;value.resizeTextMinSize=12;
                }
                else
                {
                    MainMenuBuilder.Box(title.rectTransform,.26f,.12f,.67f,.31f);
                    MainMenuBuilder.Box(value.rectTransform,.26f,.42f,.67f,.41f);
                    value.fontSize=38;value.resizeTextMaxSize=38;value.resizeTextMinSize=12;
                }
            }
            Export(header.GetChild(0).gameObject,"PlayerStatusCard");
            Export(header.GetChild(1).gameObject,"StatusCard");
            Export(header.GetChild(2).gameObject,"HighScoreStatusCard");
            Standalone("IconSocket",Art("shared/panels/cream-card"),new Vector2(110,110));
            Standalone("LevelBadge",Art("shared/panels/blue-card"),new Vector2(120,30));
            Standalone("SettingsIcon",icons[0],new Vector2(80,80));
            Standalone("ScoreIcon",icons[1],new Vector2(80,80));
            Standalone("HighScoreIcon",icons[2],new Vector2(80,80));
        }
        private static void Export(GameObject source,string name)
        {
            var copy=UnityEngine.Object.Instantiate(source);copy.name=name;
            var cardImage=copy.GetComponent<Image>();
            if(cardImage&&cardImage.sprite)
            {
                var rect=(RectTransform)copy.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
                rect.anchoredPosition=Vector2.zero;
                rect.sizeDelta=new Vector2(380,380*cardImage.sprite.rect.height/cardImage.sprite.rect.width);
            }
            foreach(var b in copy.GetComponentsInChildren<Button>(true))
            {b.onClick=new Button.ButtonClickedEvent();b.navigation=new Navigation{mode=Navigation.Mode.Automatic};}
            PrefabUtility.SaveAsPrefabAsset(copy,Prefabs+name+".prefab");
            UnityEngine.Object.DestroyImmediate(copy);
        }
        private static void Standalone(string name,Sprite sprite,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            var image=go.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            image.rectTransform.sizeDelta=size;
            PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab");UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
