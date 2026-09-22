using System;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class LoginPsg1Builder
    {
        [MenuItem("Battle Cities/Login/Add PSG1 reference layout")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Login") throw new InvalidOperationException("Open Login outside Play Mode.");
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var layout = canvas.GetComponent<LoginLayout>();
            if (canvas.transform.Find("PSG1 Login Layout")) throw new InvalidOperationException("PSG1 layout already exists. Edit its RectTransforms directly.");
            var original = canvas.transform.Find("Login Layout (edit child positions freely)");
            var root = (RectTransform)UnityEngine.Object.Instantiate(original, canvas.transform, false);
            root.name = "PSG1 Login Layout"; root.localScale = Vector3.one; root.sizeDelta = new Vector2(1240,1080);
            Place(root,"Main Menu Blue Container",0,10,760,950);
            Place(root,"Battle Cities Logo",0,370,490,327);
            Place(root,"Heading",0,190,700,68); root.Find("Heading").GetComponent<Text>().fontSize=54;
            Place(root,"Description",0,129,685,46);
            var desc=root.Find("Description").GetComponent<Text>(); desc.text="Sign in to save your progress and join the leaderboard."; desc.fontSize=28;
            PlaceArt(root,"Connect Phantom",0,37,620);
            PlaceArt(root,"Continue as Guest",0,-90,620);
            PlaceArt(root,"Solana dApp Store",0,-222,550);
            Place(root,"Built on Solana",-155,-337,280,83);
            Place(root,"Powered by MagicBlock",155,-337,280,83);
            Place(root,"Partner Divider",0,-337,2,52);
            foreach(Transform child in root)
                if(child.name=="Or"||child.name=="Divider"||child.name=="Golden Eagle"||child.name=="Choose Entry") child.gameObject.SetActive(false);
            Place(root,"Login Status",0,-391,654,58);
            var status=root.Find("Login Status").GetComponent<Text>(); status.fontSize=20;
            status.resizeTextForBestFit=true; status.resizeTextMinSize=14; status.resizeTextMaxSize=20;
            status.text="Guest progress is local. Connect a wallet for verified play.";
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            var line=Image("Footer Divider",root,null); Box(line.rectTransform,0,-378,620,2); line.color=new Color32(111,134,147,255);
            var bar=Image("PSG1 Controller Legend",root,theme.BlueFrame); Box(bar.rectTransform,0,-504,1212,60); bar.type=UnityEngine.UI.Image.Type.Sliced;
            var inner=Image("Legend Interior",bar.transform,theme.DarkPanel); Box(inner.rectTransform,0,0,1200,50); inner.type=UnityEngine.UI.Image.Type.Sliced;
            Hint(bar.transform,theme,-428,"dpad","NAVIGATE"); Hint(bar.transform,theme,0,"button-a","SELECT"); Hint(bar.transform,theme,398,"button-b","BACK");
            var phantom=root.Find("Connect Phantom").GetComponent<Button>();
            var guest=root.Find("Continue as Guest").GetComponent<Button>();
            var store=root.Find("Solana dApp Store").GetComponent<Button>();
            var buttons=new[]{phantom,guest,store};
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[(i+2)%3],selectOnDown=buttons[(i+1)%3]};
                buttons[i].gameObject.AddComponent<LoginPsg1Back>().Configure(phantom);
            }
            UnityEngine.Object.FindFirstObjectByType<LoginScene>().ConfigurePsg1(layout,phantom,guest,store,status);
            layout.ConfigurePsg1(root); layout.Preview(MainMenuPlatform.Auto);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject=canvas.gameObject;
        }
        private static void Place(Transform root,string name,float x,float y,float w,float h) => Box((RectTransform)root.Find(name),x,y,w,h);
        private static void PlaceArt(Transform root,string name,float x,float y,float width)
        { var image=root.Find(name).GetComponent<UnityEngine.UI.Image>(); Box(image.rectTransform,x,y,width,width*image.sprite.rect.height/image.sprite.rect.width); }
        private static void Box(RectTransform r,float x,float y,float w,float h)
        { r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h); }
        private static UnityEngine.UI.Image Image(string name,Transform parent,Sprite sprite)
        { var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var img=go.GetComponent<UnityEngine.UI.Image>();img.sprite=sprite;img.raycastTarget=false;return img; }
        private static void Hint(Transform parent,MenuTheme theme,float x,string icon,string title)
        {
            var art=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleCities/UI/Art/reference-style-v2/shared/icons/controls/"+icon+".png");
            var img=Image(title+" Icon",parent,art); Box(img.rectTransform,x-73,0,44,44);img.preserveAspect=true;
            var go=new GameObject(title,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Box((RectTransform)go.transform,x+42,0,160,50);
            var text=go.GetComponent<Text>();text.font=theme.HeadingFont;text.text=title;text.fontSize=30;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;
        }
    }
}
