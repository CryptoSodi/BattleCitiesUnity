using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class WebFullscreenPreview
    {
        const string ArtFolder="Assets/BattleCities/UI/Art/shared/fullscreen";
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        public static void Run()
        {
            ImportArtwork();
            RankingPreviewCapture.Run();
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            typeof(MainMenuScene).GetMethod("ResetStartupView",Private).Invoke(menu,null);
            menu.ApplyLayout(MainMenuPlatform.Web,new Vector2(1920,1080));Canvas.ForceUpdateCanvases();
            var button=menu.GetComponentInChildren<WebFullscreenButton>();
            if(!button)throw new Exception("Web fullscreen button missing");
            var rect=(RectTransform)button.transform;
            if(rect.rect.width!=48||rect.rect.height!=48)throw new Exception("Fullscreen target must be compact 48x48");
            if(button.GetComponent<Image>().type!=Image.Type.Simple||!button.GetComponent<Image>().preserveAspect)throw new Exception("Fullscreen artwork must use aspect fit");
            if(button.GetComponentsInChildren<TMPro.TMP_Text>().Length!=0)throw new Exception("Fullscreen must be icon only");
            var camera=Camera.main;var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            try{
                var board=menu.Content.Find("Leaderboard") as RectTransform;
                var a=new Vector3[4];var b=new Vector3[4];rect.GetWorldCorners(a);board.GetWorldCorners(b);
                if(Mathf.Abs(a[2].x-b[2].x)>.1f||a[0].y<=b[2].y)throw new Exception("Fullscreen must align above rankings' right edge");
                Capture(camera,target,"fullscreen-home.png",new Rect(0,0,1920,1080));
                Capture(camera,target,"fullscreen-detail.png",new Rect(1320,780,480,290));
                button.OnPointerEnter(new PointerEventData(EventSystem.current));
                Capture(camera,target,"fullscreen-focused-detail.png",new Rect(1320,780,480,290));
                button.OnPointerExit(new PointerEventData(EventSystem.current));
                foreach(var platform in new[]{MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1}){
                    menu.ApplyLayout(platform,new Vector2(1920,1080));
                    if(button.gameObject.activeSelf)throw new Exception("Fullscreen visible on native profile");
                }
                menu.ApplyLayout(MainMenuPlatform.Web,new Vector2(1920,1080));
                if(menu.GetComponentsInChildren<WebFullscreenButton>(true).Length!=1||!button.gameObject.activeSelf)throw new Exception("Fullscreen duplicate or lost on profile change");
                Debug.Log("WEB FULLSCREEN: compact size, aspect fit, icon-only, alignment, native exclusions and profile restoration verified. Previews: Builds/RankingPreviews/fullscreen-*.png");
            }finally{camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void ImportArtwork()
        {
            AssetDatabase.Refresh();
            if(!Directory.Exists(ArtFolder))throw new Exception("Waiting for fullscreen art");
            foreach(var file in Directory.GetFiles(ArtFolder,"*.png")){
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var path="Assets/BattleCities/Resources/WebFullscreenArt.asset";
            var art=AssetDatabase.LoadAssetAtPath<WebFullscreenArt>(path);
            if(!art){art=ScriptableObject.CreateInstance<WebFullscreenArt>();AssetDatabase.CreateAsset(art,path);}
            Sprite Find(bool collapse,bool gold){
                var paths=Directory.GetFiles(ArtFolder,"*.png").Where(p=>{
                    string name=Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    return (collapse?(name.Contains("exit")||name.Contains("collapse")):(name.Contains("enter")||name.Contains("expand")))&&name.Contains(gold?"gold":"blue");
                }).ToArray();
                if(paths.Length!=1)throw new Exception("Expected one fullscreen sprite for "+collapse+" "+gold);
                return AssetDatabase.LoadAssetAtPath<Sprite>(paths[0].Replace('\\','/'));
            }
            art.expandBlue=Find(false,false);art.expandGold=Find(false,true);art.collapseBlue=Find(true,false);art.collapseGold=Find(true,true);
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        }
        static void Capture(Camera camera,RenderTexture target,string name,Rect area)
        {
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var picture=new Texture2D((int)area.width,(int)area.height,TextureFormat.RGB24,false);
            try{picture.ReadPixels(area,0,0);picture.Apply();File.WriteAllBytes("Builds/RankingPreviews/"+name,picture.EncodeToPNG());}
            finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(picture);}
        }
    }
}
