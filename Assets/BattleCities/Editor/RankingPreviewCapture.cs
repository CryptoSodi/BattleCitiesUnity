using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    // Isolated display fixture. Never connects to the API or saves the opened scene.
    public static class RankingPreviewCapture
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Output="Builds/RankingPreviews";
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity",OpenSceneMode.Single);
            ScreenContent.EditorPlatform="web";
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            if(!menu)throw new Exception("Main menu missing");
            typeof(MainMenuScene).GetMethod("ResetStartupView",Private).Invoke(menu,null);
            var camera=Camera.main;
            if(!camera)camera=new GameObject("Ranking preview camera",typeof(Camera)).GetComponent<Camera>();
            var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            var canvas=menu.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            menu.ApplyLayout(MainMenuPlatform.Web,new Vector2(1920,1080));Canvas.ForceUpdateCanvases();
            typeof(MainMenuScene).GetMethod("ResolveLeaderboardUi",Private).Invoke(menu,null);
            string[] names={"IRONWOLF","STEELFOX","TANKACE","COMMANDER","GHOSTRIDER","BLAZE","NIGHTHAWK","VIPER","SCOUT","WARMACHINE"};
            int[] points={25700,24350,22100,19800,17650,15900,14200,12750,11300,9850};
            int[] matches={12,11,10,9,9,8,8,7,6,6};
            var rows=names.Select((name,i)=>new MainMenuApiClient.RankingRow{rank=i+1,playerId="sample-player-"+(i+1),displayName=name,totalPoints=points[i],matches=matches[i]}).ToArray();
            var end=DateTimeOffset.UtcNow.AddMinutes(18).AddSeconds(24);
            var current=new MainMenuApiClient.RankingRow{rank=24,playerId="sample-current-player",displayName="COMMANDER",totalPoints=8400,matchesKnown=false};
            typeof(MainMenuScene).GetMethod("OnApiRoundLoaded",Private).Invoke(menu,new object[]{new MainMenuApiClient.RoundSnapshot{
                rows=rows,currentPlayer=current,intervalMinutes=30,payoutsEnabled=true,startsAt=end.AddMinutes(-30).ToString("o"),endsAt=end.ToString("o")}});
            menu.SetPlayer("COMMANDER",19800,25700);
            Canvas.ForceUpdateCanvases();
            Capture(camera,target,"home-with-sample-rankings.png",null);
            Capture(camera,target,"player-rankings-populated.png",menu.Content.Find("Leaderboard") as RectTransform);
            var theme=(MenuTheme)typeof(MainMenuScene).GetField("theme",Private).GetValue(menu);
            var ranking=menu.GetComponent<RankingScreen>()??menu.gameObject.AddComponent<RankingScreen>();
            ranking.Configure(menu,theme,null,menu.Content.Find("Main Display") as RectTransform);
            ranking.Open(false);
            var board=new RankingPageData{Cycle=true,PeriodId="cycle",PeriodLabel="CURRENT CYCLE",MyRank=24,CurrentPlayer=current,EndsAt=end,PayoutsEnabled=true};
            board.Rows.AddRange(rows);ranking.Present(board);
            menu.ApplyLayout(MainMenuPlatform.Web,new Vector2(1920,1080));Canvas.ForceUpdateCanvases();
            Capture(camera,target,"ranking-page-populated.png",null);
            Capture(camera,target,"ranking-page-detail.png",ranking.Root);
            camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("RANKING PREVIEWS READY: "+Path.GetFullPath(Output)+" (fictional sample players; no API/account changes)");
        }
        static void Capture(Camera camera,RenderTexture target,string name,RectTransform region)
        {
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
            var area=new Rect(0,0,target.width,target.height);
            if(region){
                var corners=new Vector3[4];region.GetWorldCorners(corners);
                var low=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var high=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                float x=Mathf.Clamp(Mathf.Floor(low.x)-6,0,target.width-1),y=Mathf.Clamp(Mathf.Floor(low.y)-6,0,target.height-1);
                area=new Rect(x,y,Mathf.Min(target.width-x,Mathf.Ceil(high.x)+6-x),Mathf.Min(target.height-y,Mathf.Ceil(high.y)+6-y));
            }
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D((int)area.width,(int)area.height,TextureFormat.RGB24,false);
            try{image.ReadPixels(area,0,0);image.Apply();File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
