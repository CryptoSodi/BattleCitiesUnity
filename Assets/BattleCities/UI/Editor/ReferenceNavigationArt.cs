using System;
using BattleCities.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace BattleCities.Editor
{
    public static class ReferenceNavigationArt
    {
        public static void ApplyTo(Transform navigation)
        {
            var inactive=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/buttons/inactive.png");
            var active=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/buttons/active.png");
            var focus=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/buttons/focus-blue.png");
            var containerPath=MainMenuBuilder.Root+"Art/reference-style-v2/shared/panels/navigation-container.png";
            ConfigureContainerImporter(containerPath);
            var container=AssetDatabase.LoadAssetAtPath<Sprite>(containerPath);
            if(!inactive||!active||!focus||!container)throw new InvalidOperationException("Import navigation artwork first.");
            var containerImage=navigation.GetComponent<Image>();
            containerImage.sprite=container;
            containerImage.type=Image.Type.Sliced;
            containerImage.preserveAspect=false;
            containerImage.pixelsPerUnitMultiplier=2;
            containerImage.color=Color.white;
            for(int i=0;i<5;i++)
            {
                var tile=navigation.GetChild(i);
                var image=tile.GetComponent<Image>();
                image.type=Image.Type.Simple;image.preserveAspect=false;image.color=Color.white;
                var visual=tile.GetComponent<MenuButtonVisual>();
                visual.ConfigureSkins(image,inactive,active,i==0);
                visual.ConfigureNavigation(focus,i==0);
                MainMenuBuilder.Box((RectTransform)tile.Find("Icon"),.12f,.08f,.76f,.62f);
                MainMenuBuilder.Box((RectTransform)tile.Find("Label"),.06f,.73f,.88f,.18f);
                var copy=UnityEngine.Object.Instantiate(tile.gameObject);
                copy.name="Navigation"+tile.name;
                var rt=(RectTransform)copy.transform;
                rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);
                rt.sizeDelta=new Vector2(200,200*8f/9f);rt.localScale=Vector3.one;
                var button=copy.GetComponent<Button>();button.onClick=new Button.ButtonClickedEvent();
                button.navigation=new Navigation{mode=Navigation.Mode.Automatic};
                PrefabUtility.SaveAsPrefabAsset(copy,MainMenuBuilder.Root+"Prefabs/Shared/"+copy.name+".prefab");
                if(i==1)PrefabUtility.SaveAsPrefabAsset(copy,MainMenuBuilder.Root+"Prefabs/Shared/NavigationTile.prefab");
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static void ConfigureContainerImporter(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)return;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.spritePixelsPerUnit=700;settings.spriteBorder=new Vector4(160,160,160,160);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
