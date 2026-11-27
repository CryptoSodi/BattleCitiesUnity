using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    // Preview sample data only; does not contact the API or save the scene.
    public static class FuelRefillPreviewChecks
    {
        public static void Run()
        {
            RankingPreviewCapture.Run();
            var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuScene>();
            menu.GetComponent<RankingScreen>().Close(false);
            var board=menu.Content.Find("Leaderboard");
            foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape,MainMenuPlatform.Psg1})
            {
                ScreenContent.EditorPlatform=platform==MainMenuPlatform.Web?"web":platform==MainMenuPlatform.Psg1?"psg1":"seeker";
                if(platform==MainMenuPlatform.Psg1)typeof(MainMenuScene).GetField("controllerLegendHidden",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,true);
                board.gameObject.SetActive(true);
                var size=platform==MainMenuPlatform.AndroidLandscape?new Vector2(1688,780):platform==MainMenuPlatform.Psg1?new Vector2(1240,1080):new Vector2(1920,1080);
                menu.ApplyLayout(platform,size);Canvas.ForceUpdateCanvases();
                var info=(RectTransform)typeof(MainMenuScene).GetField("howItWorks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
                var indicator=menu.Content.Find("Fuel refill indicator") as RectTransform;
                if(!indicator||indicator.rect.width<=0||indicator.rect.height<=0)throw new Exception(platform+": missing Fuel indicator");
                var text=indicator.Find("Label").GetComponent<Text>();text.text="+1 FUEL IN 04:36";
                typeof(MainMenuScene).GetMethod("SetFuelProgress",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{.23f});
                if(indicator.gameObject.activeInHierarchy)Capture((int)size.x,(int)size.y,platform.ToString());
            }
            CheckServerCountdown(menu);
            Debug.Log("FUEL REFILL PREVIEW CHECKS PASSED");
        }

        static void CheckServerCountdown(MainMenuScene menu)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var api=menu.GetComponent<MainMenuApiClient>()??menu.gameObject.AddComponent<MainMenuApiClient>();
            typeof(MainMenuScene).GetField("apiClient",flags).SetValue(menu,api);
            typeof(MainMenuApiClient).GetProperty("IsAuthenticated").GetSetMethod(true).Invoke(api,new object[]{true});
            typeof(MainMenuApiClient).GetProperty("LastPlayer").GetSetMethod(true).Invoke(api,new object[]{new MainMenuApiClient.PlayerSnapshot{id="ply-sample"}});
            typeof(MainMenuScene).GetField("homeFuelOwner",flags).SetValue(menu,"ply-sample");
            api.EditorRequestOverride=(method,path,payload,completed)=>Reply(method,path,completed);
            Drain((IEnumerator)typeof(MainMenuScene).GetMethod("LoadFuelRefill",flags).Invoke(menu,new object[]{"ply-sample"}));
            typeof(MainMenuScene).GetMethod("UpdateFuelRefill",flags).Invoke(menu,null);
            var label=menu.Content.Find("Fuel refill indicator/Label").GetComponent<Text>();
            if(!label.text.StartsWith("+1 FUEL IN 04:"))throw new Exception("Server countdown was not displayed: "+label.text);
            var fill=(RectTransform)menu.Content.Find("Fuel refill indicator/Fuel progress/Fill");
            if(Mathf.Abs(fill.anchorMax.x-(1f-276f/360f))>.01f)throw new Exception("Fuel progress does not match the server interval");
        }

        static IEnumerator Reply(string method,string path,Action<long,JObject,string> completed)
        {
            if(method!="GET"||path!="/api/economy/account")throw new Exception("Unexpected fuel request");
            completed(200,new JObject{["account"]=new JObject{["playerId"]="ply-sample",["fuelBalance"]=3,
                ["fuelRefill"]=new JObject{["cap"]=5,["intervalSeconds"]=360,["serverTime"]="2030-01-01T00:00:00Z",["nextAt"]="2030-01-01T00:04:36Z"}}},null);
            yield break;
        }

        static void Drain(IEnumerator routine)
        {
            while(routine.MoveNext())if(routine.Current is IEnumerator child)Drain(child);
        }

        static void Capture(int width,int height,string platform)
        {
            var camera=Camera.main;var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;camera.aspect=(float)width/height;
            try
            {
                Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
                var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Builds/RankingPreviews/fuel-"+platform+".png",image.EncodeToPNG());}
                finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
            }
            finally{camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
