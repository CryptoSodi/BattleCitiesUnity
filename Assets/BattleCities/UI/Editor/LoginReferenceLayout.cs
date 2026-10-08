using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class LoginReferenceLayout
    {
        public const string PreviewDirectory="C:/Users/tassa/OneDrive/Documents/ChatGPT/BattleCitiesUnity/LoginReferencePreviews";
        [MenuItem("Battle Cities/Login/Apply approved reference layouts")]
        public static void Apply()
        {
            if(Application.isPlaying||SceneManager.GetActiveScene().name!="Login")throw new InvalidOperationException("Open Login outside Play Mode.");
            var layout=UnityEngine.Object.FindFirstObjectByType<LoginLayout>();var canvas=layout.GetComponent<Canvas>();
            var standard=(RectTransform)canvas.transform.Find("Login Layout (edit child positions freely)");
            var psg=(RectTransform)canvas.transform.Find("PSG1 Login Layout");
            var beforeTexts=Texts(canvas);var beforeButtons=canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var beforeSprites=beforeButtons.Select(b=>b.image.sprite).ToArray();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject,"Login reference layout");
            layout.Preview(MainMenuPlatform.Android);
            var portrait=standard.Cast<Transform>().Select(t=>LoginElementPlacement.Capture((RectTransform)t)).ToList();
            var column=Line(standard,"Column Divider");var footer=Line(standard,"Footer Separator");var storeDivider=Line(standard,"Store Divider");
            foreach(var line in new[]{column,footer,storeDivider})
                if(portrait.All(p=>p.target!=line)){var hidden=LoginElementPlacement.Capture(line);hidden.visible=false;portrait.Add(hidden);}
            var wide=portrait.ToArray();
            Place(wide,"Main Menu Blue Container",0,0,1440,830);
            Place(wide,"Battle Cities Logo",-408,104,530,530);
            Place(wide,"Heading",285,296,735,82,58,TextAnchor.MiddleLeft);
            Place(wide,"Description",285,216,735,78,34,TextAnchor.MiddleLeft);
            Art(wide,"Connect Phantom",285,88,735);
            Art(wide,"Continue as Guest",285,-98,735);
            Place(wide,"Or",285,-10,70,32,24);
            var rules=wide.Select((p,i)=>new{p,i}).Where(p=>p.p.target.name=="Divider").ToArray();
            Box(ref wide[rules[0].i],75,-10,300,2);Box(ref wide[rules[1].i],495,-10,300,2);
            Place(wide,"Golden Eagle",-408,-199,112,58);
            Box(ref wide[rules[2].i],-594,-199,210,2);Box(ref wide[rules[3].i],-222,-199,210,2);
            Place(wide,"Choose Entry",-408,-241,565,34,28);
            Place(wide,"Login Status",285,-218,735,60,26);
            Place(wide,"Built on Solana",-500,-325,260,77);
            Place(wide,"Powered by MagicBlock",-172,-325,260,77);
            Place(wide,"Partner Divider",-336,-325,2,58);
            Art(wide,"Solana dApp Store",405,-325,400);
            Place(wide,"Column Divider",-105,57,2,600);
            Place(wide,"Footer Separator",0,-265,1344,2);
            Place(wide,"Store Divider",94,-325,2,64);
            layout.ConfigureLandscape(portrait.ToArray(),wide);
            // Adjust existing PSG1 objects only. Button artwork and all strings remain unchanged.
            Put(psg,"Main Menu Blue Container",0,0,1184,1044);
            Put(psg,"Battle Cities Logo",-330,304,425,425);
            PutText(psg,"Heading",225,354,630,174,94,TextAnchor.MiddleLeft);
            PutText(psg,"Description",225,226,630,86,38,TextAnchor.MiddleLeft);
            PutArt(psg,"Connect Phantom",0,70,1060);
            PutArt(psg,"Continue as Guest",0,-142,1060);
            psg.Find("Solana dApp Store").gameObject.SetActive(false);
            PutText(psg,"Login Status",0,-294,1040,72,30);
            Put(psg,"Footer Divider",0,-348,1090,2);
            Put(psg,"Built on Solana",-210,-391,330,98);
            Put(psg,"Powered by MagicBlock",210,-391,330,98);
            Put(psg,"Partner Divider",0,-391,2,64);
            Put(psg,"PSG1 Controller Legend",0,-477,1130,68);
            var legend=psg.Find("PSG1 Controller Legend");Put(legend,"Legend Interior",0,0,1114,56);
            string[] labels={"NAVIGATE","SELECT","BACK"};float[] columns={-366,0,366};
            for(int i=0;i<labels.Length;i++){Put(legend,labels[i]+" Icon",columns[i]-95,0,52,52);PutText(legend,labels[i],columns[i]+30,0,180,56,30,TextAnchor.MiddleLeft);}
            layout.Preview(MainMenuPlatform.Auto);Canvas.ForceUpdateCanvases();
            if(!Texts(canvas).SequenceEqual(beforeTexts)||beforeButtons.Length!=canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length||beforeButtons.Where((b,i)=>b.image.sprite!=beforeSprites[i]).Any())
                throw new InvalidOperationException("Reference layout must preserve every existing text and button asset.");
            EditorUtility.SetDirty(layout);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Login updated using the original six buttons, original sprites and exact existing text. Portrait placements preserved.");
        }
        static string[] Texts(Canvas canvas)=>canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t=>t.text).ToArray();
        static RectTransform Line(Transform root,string name)
        {
            var found=root.Find(name) as RectTransform;if(found)return found;
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(root,false);
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color32(37,98,128,190);image.raycastTarget=false;return (RectTransform)go.transform;
        }
        static void Box(ref LoginElementPlacement p,float x,float y,float w,float h){p.position=new Vector2(x,y);p.size=new Vector2(w,h);p.visible=true;}
        static void Place(LoginElementPlacement[] list,string name,float x,float y,float w,float h,int font=0,TextAnchor align=TextAnchor.MiddleCenter)
        {
            int i=Array.FindIndex(list,p=>p.target.name==name);if(i<0)throw new InvalidOperationException("Missing "+name);
            Box(ref list[i],x,y,w,h);if(font>0){list[i].fontSize=list[i].maxFontSize=font;list[i].minFontSize=Mathf.RoundToInt(font*.78f);list[i].alignment=align;list[i].bestFit=true;}
        }
        static void Art(LoginElementPlacement[] list,string name,float x,float y,float width)
        {var sprite=list.First(p=>p.target.name==name).target.GetComponent<UnityEngine.UI.Image>().sprite;Place(list,name,x,y,width,width*sprite.rect.height/sprite.rect.width);}
        static void Put(Transform root,string name,float x,float y,float w,float h)
        {var r=(RectTransform)root.Find(name);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        static void PutArt(Transform root,string name,float x,float y,float width)
        {var sprite=root.Find(name).GetComponent<UnityEngine.UI.Image>().sprite;Put(root,name,x,y,width,width*sprite.rect.height/sprite.rect.width);}
        static void PutText(Transform root,string name,float x,float y,float w,float h,int font,TextAnchor align=TextAnchor.MiddleCenter)
        {Put(root,name,x,y,w,h);var text=root.Find(name).GetComponent<UnityEngine.UI.Text>();text.alignment=align;text.fontSize=text.resizeTextMaxSize=font;text.resizeTextMinSize=Mathf.RoundToInt(font*.78f);text.resizeTextForBestFit=true;}

        public static void Capture(MainMenuPlatform platform,int width,int height,string name)
        {
            var layout=UnityEngine.Object.FindFirstObjectByType<LoginLayout>();var canvas=layout.GetComponent<Canvas>();var preview=layout.PreviewPlatform;
            var cameraRoot=new GameObject("Login capture camera",typeof(Camera)){hideFlags=HideFlags.HideAndDontSave};var camera=cameraRoot.GetComponent<Camera>();
            camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(width,height,24);var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var oldActive=RenderTexture.active;Texture2D image=null;
            try
            {
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();layout.Preview(platform);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                Directory.CreateDirectory(PreviewDirectory);File.WriteAllBytes(PreviewDirectory+"/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=oldActive;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;camera.targetTexture=null;
                UnityEngine.Object.DestroyImmediate(cameraRoot);UnityEngine.Object.DestroyImmediate(target);if(image)UnityEngine.Object.DestroyImmediate(image);
                Canvas.ForceUpdateCanvases();layout.Preview(preview);
            }
        }
    }
}