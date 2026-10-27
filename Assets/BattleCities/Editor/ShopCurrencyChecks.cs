using System;
using System.IO;
using System.Reflection;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ShopCurrencyChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static bool seasonPassPreview;
        public static void CaptureSeasonPass(){seasonPassPreview=true;Run();}
        public static void Run()
        {
            const string path="Assets/BattleCities/UI/Art/shop/play-token.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var art=AssetDatabase.LoadAssetAtPath<ShopArt>("Assets/BattleCities/Resources/ShopArt.asset");
            art.play=AssetDatabase.LoadAssetAtPath<Sprite>(path);EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
            Check(ShopScreen.BalanceAmount(new JValue("0.0123123123"))=="0.01","two-decimal SOL display");
            Check(ShopScreen.BalanceAmount(new JValue("1.235"))=="1.24","half-up rounding");
            Check(ShopScreen.BalanceAmount(new JValue("1e-9"))=="0.00","tiny balances stay readable");
            Check(ShopScreen.BalanceAmount(null)=="—","unknown balance stays unknown");
            Check(ShopScreen.BalanceAmount(new JValue("58.123123"),0)=="58","whole token balance");
            Check(ShopScreen.DisplayPrice("58.123123",ShopCurrency.Skr)=="58","whole SKR price");
            Check(ShopScreen.DisplayPrice("58.5",ShopCurrency.Play)=="59","whole PLAY price rounds half up");
            Check(ShopScreen.DisplayPrice("0.1",ShopCurrency.Play)=="<1","small token price is not shown as free");
            Check(ShopScreen.DisplayPrice("0.025",ShopCurrency.Solana)=="0.025","SOL price precision preserved");
            Check(ShopLiveCatalog.FormatAtomic("58123123",6)=="58.123123","wallet quote amount stays exact");
            Directory.CreateDirectory("Builds/ShopCurrencyPreviews");
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity",OpenSceneMode.Single);
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            typeof(MainMenuScene).GetMethod("ResetStartupView",Private).Invoke(menu,null);
            var theme=(MenuTheme)typeof(MainMenuScene).GetField("theme",Private).GetValue(menu);
            var shop=menu.GetComponent<ShopScreen>()??menu.gameObject.AddComponent<ShopScreen>();
            shop.Configure(menu,theme,null,menu.Content.Find("Main Display") as RectTransform);
            shop.Root.gameObject.SetActive(true);
            typeof(ShopScreen).GetField("walletBalances",Private).SetValue(shop,new JObject{{"skrBalance","1234.56789"},{"playBalance","9876.54321"},{"solBalance","0.0123123123"}});
            typeof(ShopScreen).GetField("account",Private).SetValue(shop,new JObject{{"fuelBalance",5}});
            var rows=new JArray();foreach(var product in ShopCatalog.Products)if(product.Id!="season-pass")rows.Add(new JObject{{"id",product.Id},{"skrPrice","58.123123"},{"playPrice","58.123123"},{"solPrice","0.025"}});
            var live=ShopLiveCatalog.Parse(new JObject{{"items",rows},{"currency",new JObject{{"skr",new JObject{{"decimals",6}}},{"play",new JObject{{"decimals",6},{"mint","PLAYs3GSSadH2q2JLS7djp7yzeT75NK78XgrE5YLrfq"}}}}},{"seasonPass",new JObject{{"season",new JObject{{"id","editor-fixture"},{"name","PREVIEW"}}},{"scoringPolicy","all_matches_in_season"}}}});
            typeof(ShopScreen).GetField("liveCatalog",Private).SetValue(shop,live);
            CheckUnavailableCards(menu,shop,rows);
            if(seasonPassPreview)
            {
                var catalog=JObject.Parse(File.ReadAllText("Builds/Automation/season-pass-catalog.json"));
                typeof(ShopScreen).GetField("liveCatalog",Private).SetValue(shop,ShopLiveCatalog.Parse(catalog["data"] as JObject??catalog));
                typeof(ShopScreen).GetField("walletBalances",Private).SetValue(shop,null);
                typeof(ShopScreen).GetField("account",Private).SetValue(shop,null);
                shop.SetCategory(ShopCategory.SeasonPass,false);
                Capture(menu,shop,MainMenuPlatform.Web,1920,1080,"season-pass-web.png");
                Capture(menu,shop,MainMenuPlatform.Psg1,1240,1080,"season-pass-psg1.png");
                shop.SetCategory(ShopCategory.All,false);
                Capture(menu,shop,MainMenuPlatform.Psg1,1240,1080,"shop-all-bottom-psg1.png",0);
                Capture(menu,shop,MainMenuPlatform.AndroidLandscape,2316,1080,"shop-all-middle-android.png",.5f);
                Capture(menu,shop,MainMenuPlatform.AndroidLandscape,2316,1080,"shop-all-bottom-android.png",0);
                Debug.Log("SEASON PASS PREVIEW CAPTURED: real public catalog, unknown wallet balances, no API mutations or payments.");
                return;
            }
            Capture(menu,shop,MainMenuPlatform.Web,1920,1080,"shop-web.png");
            Capture(menu,shop,MainMenuPlatform.Psg1,1240,1080,"shop-psg1.png");
            Capture(menu,shop,MainMenuPlatform.AndroidLandscape,1920,900,"shop-android.png");
            Debug.Log("SHOP CURRENCY CHECKS PASSED: whole token prices/balances, two-decimal SOL balances, adjacent tickers, exact wallet amounts, SKR/PLAY platform selection. UI fixture data only; no API or payment calls.");
        }
        static void Capture(MainMenuScene menu,ShopScreen shop,MainMenuPlatform platform,int width,int height,string name,float? scrollPosition=null)
        {
            menu.ApplyLayout(platform,new Vector2(width,height));shop.ApplyLayout(platform);
            typeof(ShopScreen).GetMethod("RefreshAccount",Private).Invoke(shop,null);shop.SetCurrency(ShopCurrency.Skr,false);shop.RefreshPresentation();
            var ticker=shop.Root.Find("Balances/SKR balance/Label").GetComponent<TMPro.TMP_Text>();
            Check(ticker.text==(platform==MainMenuPlatform.Psg1?"PLAY":"SKR"),"correct platform token");
            Check(shop.Currency==(platform==MainMenuPlatform.Psg1?ShopCurrency.Play:ShopCurrency.Skr),"correct checkout currency");
            var value=shop.Root.Find("Balances/SKR balance/Value") as RectTransform;
            var amount=value.GetComponent<TMPro.TMP_Text>();
            Check(ticker.font==amount.font&&ticker.fontStyle==amount.fontStyle&&ticker.fontWeight==amount.fontWeight&&Mathf.Approximately(ticker.fontSize,amount.fontSize),"ticker matches amount typography");
            Check(value.anchorMin.x<ticker.rectTransform.anchorMin.x,"amount precedes ticker");
            Check(Mathf.Abs(ticker.rectTransform.rect.width)>0,"ticker has readable width");
            float gap=(ticker.rectTransform.anchorMin.x-value.anchorMax.x)*((RectTransform)value.parent).rect.width;
            Check(gap>0&&gap<=6.1f,"ticker immediately follows amount");
            if(!seasonPassPreview)Check(livePrice(shop)=="58 "+ticker.text,"catalog price is a whole token amount");
            var camera=Camera.main;if(!camera)camera=new GameObject("Shop preview camera",typeof(Camera)).GetComponent<Camera>();
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            var canvas=menu.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();
            if(scrollPosition.HasValue){LayoutRebuilder.ForceRebuildLayoutImmediate(shop.CatalogScroll.content);Canvas.ForceUpdateCanvases();shop.CatalogScroll.StopMovement();shop.CatalogScroll.verticalNormalizedPosition=scrollPosition.Value;Canvas.ForceUpdateCanvases();}
            camera.Render();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var picture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{picture.ReadPixels(new Rect(0,0,width,height),0,0);picture.Apply();File.WriteAllBytes("Builds/ShopCurrencyPreviews/"+name,picture.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(picture);UnityEngine.Object.DestroyImmediate(target);}
        }
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);}
        static void CheckUnavailableCards(MainMenuScene menu,ShopScreen shop,JArray rows)
        {
            menu.ApplyLayout(MainMenuPlatform.Web,new Vector2(1920,1080));shop.ApplyLayout(MainMenuPlatform.Web);
            shop.SetCategory(ShopCategory.All,false);shop.SetCurrency(ShopCurrency.Skr,false);
            var content=shop.CatalogScroll.content;
            var pass=content.Find("season-pass");var passButton=pass.Find("Price").GetComponent<Button>();
            Check(!passButton.interactable&&!pass.GetComponent<CanvasGroup>().interactable,"unavailable pass is disabled");
            Check(pass.GetComponent<Image>().material.shader.name=="BattleCities/UI/ShopUnavailable","unavailable artwork uses grayscale");
            Check(pass.GetSiblingIndex()==content.childCount-1,"unavailable pass sorts behind all available items");
            int passIndex=Array.FindIndex(ShopCatalog.Products,p=>p.Id=="season-pass"),before=shop.SelectedProduct;
            shop.SelectProduct(passIndex,true);Check(shop.SelectedProduct==before,"unavailable card cannot be selected");
            typeof(ShopScreen).GetMethod("InspectProduct",Private).Invoke(shop,new object[]{passIndex});
            Check(!((RectTransform)typeof(ShopScreen).GetField("notice",Private).GetValue(shop)).gameObject.activeSelf,"unavailable card cannot open checkout");
            foreach(var button in shop.Root.GetComponentsInChildren<Button>(true))
            {
                var n=button.navigation;
                Check(n.selectOnLeft!=passButton&&n.selectOnRight!=passButton&&n.selectOnUp!=passButton&&n.selectOnDown!=passButton,"controller navigation skips disabled pass");
            }
            JObject shield=null;foreach(JObject row in rows)if((string)row["id"]=="shield")shield=row;
            shield["skrPrice"]=null;typeof(ShopScreen).GetMethod("RefreshAccount",Private).Invoke(shop,null);
            var shieldButton=content.Find("shield/Price").GetComponent<Button>();
            Check(!shieldButton.interactable,"currency-specific unavailable item is disabled");
            shield["skrPrice"]="58.123123";typeof(ShopScreen).GetMethod("RefreshAccount",Private).Invoke(shop,null);
            Check(shieldButton.interactable,"item re-enables when its price returns");
            shop.SetCategory(ShopCategory.SeasonPass,false);
            Check(shop.SelectedProduct==-1,"all-unavailable category has no selected card");
            var fallback=(Button)typeof(ShopScreen).GetMethod("PreferredProductButton",Private).Invoke(shop,null);
            Check(fallback.interactable&&fallback!=passButton,"all-unavailable category retains an enabled navigation escape");
            shop.SetCategory(ShopCategory.All,false);
        }
        static string livePrice(ShopScreen shop)=>(string)typeof(ShopScreen).GetMethod("LivePrice",Private).Invoke(shop,new object[]{"shield"});
    }
}
