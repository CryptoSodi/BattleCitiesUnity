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
                importer.spritePixelsPerUnit=path.EndsWith("/shared/panels/navigation-container.png")?700:100;
                importer.spriteBorder=path.EndsWith("/shared/panels/navigation-container.png")
                    ?new Vector4(160,160,160,160):path.EndsWith("/shared/header/high-score-center.png")
                    ?new Vector4(160,0,160,0):Vector4.zero;
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

        public static void ApplyHeader(Transform header,MenuTheme theme,bool commanderOnly=false,int onlyCard=-1)
        {
            theme.SettingsIcon=Art("shared/icons/settings");
            theme.ScoreIcon=Art("shared/icons/score-coin");
            theme.HighScoreIcon=Art("shared/icons/high-score-shield");
            if(!theme.SettingsIcon)throw new InvalidOperationException("Import reference-style-v2 artwork first.");
            var icons=new[]{theme.SettingsIcon,theme.ScoreIcon,theme.HighScoreIcon};
            for(int i=0;i<3;i++)
            {
                if((commanderOnly && i!=0)||(onlyCard>=0 && i!=onlyCard))continue;
                var card=header.GetChild(i);
                var image=card.GetComponent<Image>();
                image.sprite=Art(i==1?"shared/header/score-center":"shared/header/high-score-center");
                image.type=Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;
                FrameStrip(card,"Frame Left",image.sprite,new Rect(0,0,.06f,1),0,.06f);
                FrameStrip(card,"Frame Middle",image.sprite,new Rect(.23f,0,.71f,1),.06f,.88f);
                FrameStrip(card,"Frame Right",image.sprite,new Rect(.94f,0,.06f,1),.94f,.06f);
                card.Find("Frame Left").gameObject.SetActive(false);
                card.Find("Frame Middle").gameObject.SetActive(false);
                card.Find("Frame Right").gameObject.SetActive(false);
                var cardRect=(RectTransform)card;
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,((RectTransform)header).rect.height);
                var cardPosition=cardRect.anchoredPosition;cardPosition.y=0;cardRect.anchoredPosition=cardPosition;
                var socket=card.Find("Icon tile");
                var socketImage=socket.GetComponent<Image>();
                socketImage.sprite=Art("shared/panels/cream-card");
                socketImage.type=Image.Type.Simple;
                socketImage.preserveAspect=true;
                socketImage.color=Color.white;
                socketImage.enabled=i!=1;
                socket.gameObject.SetActive(i!=1);
                MainMenuBuilder.Box((RectTransform)socket,.055f,.17f,.165f,.50f);
                var icon=socket.Find("Icon").GetComponent<Image>();
                icon.sprite=icons[i];icon.preserveAspect=true;
                MainMenuBuilder.Box(icon.rectTransform,.09f,.08f,.82f,.84f);
                var readout=card.Find("Readout");
                readout.GetComponent<Image>().enabled=false;
                MainMenuBuilder.Stretch((RectTransform)readout);
                var title=readout.Find("Label").GetComponent<Text>();
                var value=readout.Find("Value").GetComponent<Text>();
                if(i==0)
                {
                    var nameBackdrop=readout.Find("Name Backdrop");
                    if(nameBackdrop)UnityEngine.Object.DestroyImmediate(nameBackdrop.gameObject);
                    MainMenuBuilder.Box(title.rectTransform,.275f,.165f,.65f,.35f);
                    title.alignment=TextAnchor.MiddleLeft;
                    title.resizeTextMinSize=16;
                    var nameShadow=title.GetComponent<Shadow>();
                    if(!nameShadow)nameShadow=title.gameObject.AddComponent<Shadow>();
                    nameShadow.effectColor=new Color(0,.02f,.06f,.85f);nameShadow.effectDistance=new Vector2(1,-2);
                    MainMenuBuilder.Box(value.rectTransform,.75f,.41f,.15f,.23f);
                    value.color=Color.white;value.fontSize=20;value.resizeTextMaxSize=20;value.resizeTextMinSize=12;
                    var oldBadge=readout.Find("Level Badge");
                    if(oldBadge)oldBadge.gameObject.SetActive(false);
                    var levelBadge=EnsureTexture("Level Pill",readout,Art("shared/header/player-card"),new Rect(.765f,.18f,.158f,.275f));
                    MainMenuBuilder.Box(levelBadge.rectTransform,.735f,.40f,.18f,.24f);
                    levelBadge.transform.SetAsFirstSibling();
                    var track=EnsureImage("Progress",readout,theme.Rounded,new Color(.10f,.64f,.93f));
                    MainMenuBuilder.Box(track.rectTransform,.275f,.44f,.44f,.15f);
                    track.pixelsPerUnitMultiplier=10;
                    var recess=EnsureImage("Recess",track.transform,theme.Rounded,new Color(.015f,.10f,.23f));
                    MainMenuBuilder.Stretch(recess.rectTransform,1.5f);recess.pixelsPerUnitMultiplier=12;
                    recess.transform.SetAsFirstSibling();
                    var fill=EnsureImage("Fill",track.transform,theme.Rounded,new Color(.08f,.86f,1));
                    MainMenuBuilder.Stretch(fill.rectTransform,2);
                    fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;
                    value.transform.SetAsLastSibling();title.transform.SetAsLastSibling();
                }
                else
                {
                    float left=i==1?.08f:.265f,width=i==1?.84f:.66f;
                    MainMenuBuilder.Box(title.rectTransform,left,.16f,width,.26f);
                    MainMenuBuilder.Box(value.rectTransform,left,.42f,width,.24f);
                    title.alignment=value.alignment=TextAnchor.MiddleCenter;
                    value.fontSize=38;value.resizeTextMaxSize=38;value.resizeTextMinSize=12;
                    value.color=i==1?new Color(1,.83f,.14f):Color.white;
                    if(i==1)
                    {
                        var well=readout.Find("Score Well");
                        if(well)well.gameObject.SetActive(false);
                    }
                }
            }
            if(onlyCard<0 || onlyCard==0)Export(header.GetChild(0).gameObject,"PlayerStatusCard");
            if(onlyCard<0 || onlyCard==1)Export(header.GetChild(1).gameObject,"StatusCard");
            if(onlyCard<0 || onlyCard==2)Export(header.GetChild(2).gameObject,"HighScoreStatusCard");
            Standalone("IconSocket",Art("shared/panels/cream-card"),new Vector2(110,110));
            Standalone("LevelBadge",Art("shared/panels/blue-card"),new Vector2(120,30));
            Standalone("SettingsIcon",icons[0],new Vector2(80,80));
            Standalone("ScoreIcon",icons[1],new Vector2(80,80));
            Standalone("HighScoreIcon",icons[2],new Vector2(80,80));
        }
        private static RawImage EnsureTexture(string name,Transform parent,Sprite source,Rect uv)
        {
            var child=parent.Find(name);
            var image=child?child.GetComponent<RawImage>():new GameObject(name,typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent,false);image.texture=source.texture;image.uvRect=uv;
            image.raycastTarget=false;image.color=Color.white;return image;
        }
        private static void FrameStrip(Transform card,string name,Sprite source,Rect uv,float x,float width)
        {
            var child=card.Find(name);
            var image=child?child.GetComponent<RawImage>():new GameObject(name,typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(card,false);image.texture=source.texture;image.uvRect=uv;
            image.raycastTarget=false;image.color=Color.white;
            MainMenuBuilder.Box(image.rectTransform,x,0,width,1);
            image.transform.SetAsFirstSibling();
        }
        private static Image EnsureImage(string name,Transform parent,Sprite sprite,Color color)
        {
            var child=parent.Find(name);
            var image=child?child.GetComponent<Image>():new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.sprite=sprite;image.color=color;
            if(!child && name=="Fill")image.fillAmount=0;
            image.type=Image.Type.Sliced;image.raycastTarget=false;return image;
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
                rect.sizeDelta=new Vector2(name=="StatusCard"?250:327,100);
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
