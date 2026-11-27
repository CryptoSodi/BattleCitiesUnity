using System;
using System.IO;
using System.Linq;
using BattleCities.UI;
using TMPro;
using UnityEngine;

namespace BattleCities.Editor
{
    // Display-only fixture; it never starts a battle or contacts the API.
    public static class TankCostButtonChecks
    {
        public static void Run()
        {
            RankingPreviewCapture.Run();
            var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuScene>();
            menu.StartBattle();
            var screen=menu.GetComponent<PreBattleScreen>();
            if(!screen||!screen.IsOpen)throw new Exception("Tank selection did not open");
            foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
            {
                ScreenContent.EditorPlatform=platform==MainMenuPlatform.Web?"web":platform==MainMenuPlatform.Psg1?"psg1":"seeker";
                var size=platform==MainMenuPlatform.AndroidLandscape?new Vector2(1688,780):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(1920,1080);
                menu.ApplyLayout(platform,size);Canvas.ForceUpdateCanvases();
                var badges=screen.Root.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="Cost badge"&&r.Find("Cost text")).ToArray();
                if(badges.Length!=4)throw new Exception(platform+": expected four tank cost buttons, found "+badges.Length);
                foreach(var badge in badges)
                {
                    var icon=(RectTransform)badge.Find("Fuel can");var label=badge.Find("Cost text").GetComponent<TMP_Text>();
                    if(!icon||!label||label.alignment!=TextAlignmentOptions.Center||
                       !Mathf.Approximately(icon.anchorMin.x,.05f)||!Mathf.Approximately(icon.anchorMax.x,.22f)||
                       !Mathf.Approximately(label.rectTransform.anchorMin.x,.22f)||!Mathf.Approximately(label.rectTransform.anchorMax.x,.96f))
                        throw new Exception(platform+": tank button does not match Shop alignment");
                    if(badge.rect.width<=0||badge.rect.height<=0)throw new Exception(platform+": tank button has no size");
                }
                Capture((int)size.x,(int)size.y,platform.ToString());
            }
            Debug.Log("TANK COST BUTTON CHECKS PASSED");
        }

        static void Capture(int width,int height,string platform)
        {
            var camera=Camera.main;var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;camera.aspect=(float)width/height;
            try
            {
                Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
                var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Builds/RankingPreviews/tank-buttons-"+platform+".png",image.EncodeToPNG());}
                finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
            }
            finally{camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
