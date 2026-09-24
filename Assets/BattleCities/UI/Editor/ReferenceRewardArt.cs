using System;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace BattleCities.Editor
{
    public static class ReferenceRewardArt
    {
        [MenuItem("Battle Cities/UI/Apply Supplied Reward Crates")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=MainMenuBuilder.ScenePath||scene.isDirty)
                throw new InvalidOperationException("Open and save MainMenu in edit mode first.");
            ReferenceHeaderArt.Import();
            var view=UnityEngine.Object.FindAnyObjectByType<MainMenuScene>();
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"Apply supplied reward crates");
            ApplyTo(view.Content.Find("Main Display/Rewards"));
            view.RefreshLayout();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        public static void ApplyTo(Transform rewards)
        {
            string[] names={"silver","gold","bronze","cyan"};
            for(int i=0;i<4;i++)
            {
                var item=rewards.Find("Garden/Reward "+i);
                MainMenuBuilder.Box((RectTransform)item,.10f+i*.20f,.015f,.202f,.97f);
                var art=item.Find("Artwork");
                if(!art)
                {
                    var go=new GameObject("Artwork",typeof(RectTransform),typeof(AspectRatioFitter));
                    art=go.transform;art.SetParent(item,false);
                    var fitter=go.GetComponent<AspectRatioFitter>();
                    fitter.aspectRatio=543f/724;fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                    item.Find("Chest").SetParent(art,false);
                    item.Find("Podium").SetParent(art,false);
                }
                var chest=art.Find("Chest").GetComponent<Image>();
                chest.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/main-menu/rewards/reward-"+names[i]+".png");
                if(!chest.sprite)throw new InvalidOperationException("Reward artwork missing: "+names[i]);
                var aspect=art.GetComponent<AspectRatioFitter>();
                if(!aspect)aspect=art.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectRatio=543f/653f;
                aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                chest.preserveAspect=true;chest.type=Image.Type.Simple;MainMenuBuilder.Stretch(chest.rectTransform);
                var podium=art.Find("Podium");
                podium.GetComponent<Image>().enabled=false;
                MainMenuBuilder.Box((RectTransform)podium,.12f,.635f,.76f,.27f);
                var rank=podium.Find("Rank").GetComponent<Text>();
                var amount=podium.Find("Reward").GetComponent<Text>();
                MainMenuBuilder.Box(rank.rectTransform,.02f,.04f,.96f,.40f);
                MainMenuBuilder.Box(amount.rectTransform,.02f,.48f,.96f,.40f);
                rank.fontSize=38;rank.resizeTextMinSize=18;rank.resizeTextMaxSize=38;
                amount.fontSize=30;amount.resizeTextMinSize=16;amount.resizeTextMaxSize=30;
                rank.fontStyle=FontStyle.Bold;amount.fontStyle=FontStyle.Bold;
                rank.alignment=TextAnchor.MiddleCenter;amount.alignment=TextAnchor.MiddleCenter;
                rank.alignByGeometry=true;amount.alignByGeometry=true;
                ConfigureShadow(rank);ConfigureShadow(amount);
                amount.color=i==1?new Color32(255,224,62,255):Color.white;
                var copy=UnityEngine.Object.Instantiate(item.gameObject);
                copy.name="RewardPodium-"+names[i];
                PrefabUtility.SaveAsPrefabAsset(copy,MainMenuBuilder.Root+"Prefabs/Shared/"+copy.name+".prefab");
                if(i==1)PrefabUtility.SaveAsPrefabAsset(copy,MainMenuBuilder.Root+"Prefabs/Shared/RewardPodium.prefab");
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
        private static void ConfigureShadow(Text label)
        {
            var shadow=label.GetComponent<Shadow>();
            if(!shadow)return;
            shadow.effectColor=new Color(0,0,.02f,.85f);
            shadow.effectDistance=new Vector2(2,-2);
        }
    }
}
