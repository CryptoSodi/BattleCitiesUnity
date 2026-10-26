using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ExtendedPowerupUiChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity",OpenSceneMode.Single);
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();typeof(MainMenuScene).GetMethod("ResetStartupView",Private).Invoke(menu,null);
            menu.OpenShop();var shop=menu.GetComponent<ShopScreen>();int checks=0;
            void Check(bool ok,string name){checks++;if(!ok)throw new Exception("POWERUP UI: "+name);}
            Check(((Array)typeof(ShopScreen).GetField("cards",Private).GetValue(shop)).Length==17,"all catalog cards constructed");
            var rows=(RectTransform[])typeof(ShopScreen).GetField("inventoryTiles",Private).GetValue(shop);Check(rows.Length==12,"all inventory rows constructed");
            string folder=Path.GetFullPath("Builds/Automation/PowerupUi");Directory.CreateDirectory(folder);
            foreach(var view in new[]{(MainMenuPlatform.Web,1600,1067,"web"),(MainMenuPlatform.Psg1,1240,1080,"psg1"),(MainMenuPlatform.AndroidLandscape,844,390,"android")})
            {
                menu.ApplyLayout(view.Item1,new Vector2(view.Item2,view.Item3));Canvas.ForceUpdateCanvases();
                shop.RefreshPresentation();Canvas.ForceUpdateCanvases();
                foreach(var row in rows){Check(row.rect.height>0&&row.rect.width>0,"inventory row placed "+row.name);row.Find("Name").GetComponent<TMP_Text>().ForceMeshUpdate(true);}
                var canvas=menu.GetComponent<Canvas>();var camera=Camera.main;var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;
                var target=new RenderTexture(view.Item2,view.Item3,24);target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                menu.ApplyLayout(view.Item1,new Vector2(view.Item2,view.Item3));shop.RefreshPresentation();Canvas.ForceUpdateCanvases();camera.Render();camera.Render();var active=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(view.Item2,view.Item3,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,view.Item2,view.Item3),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"shop-"+view.Item4+".png"),image.EncodeToPNG());
                var productScroll=(ScrollRect)typeof(ShopScreen).GetField("scroll",Private).GetValue(shop);productScroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
                image.ReadPixels(new Rect(0,0,view.Item2,view.Item3),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"new-products-"+view.Item4+".png"),image.EncodeToPNG());productScroll.verticalNormalizedPosition=1;
                foreach(string id in new[]{"max-healer","health-healer","rapid-shooter","rapid-shooter-long"})Check(productScroll.content.Find(id).Find("Artwork").GetComponent<Image>().sprite,"new product artwork visible "+id);
                UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=active;camera.targetTexture=null;canvas.renderMode=mode;canvas.worldCamera=previousCamera;target.Release();UnityEngine.Object.DestroyImmediate(target);
            }
            menu.StartBattle();var loadout=menu.GetComponent<PreBattleScreen>();
            Check(loadout.InventoryScroll.content.childCount==12,"all loadout rows constructed");
            typeof(PreBattleScreen).GetMethod("LoadoutNavigation",Private).Invoke(loadout,null);
            var buttons=(Button[])typeof(PreBattleScreen).GetField("loadoutRows",Private).GetValue(loadout);
            for(int i=0;i<buttons.Length-1;i++)Check(buttons[i].navigation.selectOnDown==buttons[i+1],"controller can reach row "+(i+1));
            foreach(string id in new[]{"max-healer","health-healer","rapid-shooter","rapid-shooter-long"})Check(loadout.InventoryScroll.content.Find(id),"new loadout item exists "+id);
            var manual=JsonUtility.FromJson<FieldManualEntries>(RemoteGameArt.Load<OperationsArt>("OperationsArt").manual.text);
            Check(manual.entries.Count(e=>e.category=="powerups")==12,"all powers documented in Field Manual");
            Debug.Log("EXTENDED POWERUP UI PASS: "+checks+" checks. Preview fixtures: "+folder);
        }
    }
}
