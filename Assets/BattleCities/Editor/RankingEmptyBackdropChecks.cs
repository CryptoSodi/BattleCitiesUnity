using System;
using System.IO;
using System.Reflection;
using BattleCities.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    // Exercises delayed ranking responses without building a player or contacting the API.
    public static class RankingEmptyBackdropChecks
    {
        public static void Run()
        {
            RankingPreviewCapture.Run();
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            menu.GetComponent<RankingScreen>().Close(false);
            var load=typeof(MainMenuScene).GetMethod("OnApiRoundLoaded",BindingFlags.Instance|BindingFlags.NonPublic);
            var row=new MainMenuApiClient.RankingRow{rank=1,displayName="TEST",totalPoints=100,matches=1};
            var populated=new MainMenuApiClient.RoundSnapshot{rows=new[]{row},intervalMinutes=30};
            var empty=new MainMenuApiClient.RoundSnapshot{rows=Array.Empty<MainMenuApiClient.RankingRow>(),intervalMinutes=30};
            var board=menu.Content.Find("Leaderboard");
            var columns=board.Find("Columns");
            var scores=(RectTransform)board.Find("Scores");
            foreach(var platform in new[]{MainMenuPlatform.Web,MainMenuPlatform.AndroidLandscape})
            {
                var size=platform==MainMenuPlatform.Web?new Vector2(1920,1080):new Vector2(1688,780);
                ScreenContent.EditorPlatform=platform==MainMenuPlatform.Web?"web":"seeker";
                board.gameObject.SetActive(true);
                menu.ApplyLayout(platform,size);
                load.Invoke(menu,new object[]{populated});
                Canvas.ForceUpdateCanvases();
                CheckHeadingGap(board,platform.ToString());
                float populatedTop=scores.anchorMax.y;
                load.Invoke(menu,new object[]{empty});
                Canvas.ForceUpdateCanvases();
                if(columns.gameObject.activeSelf||scores.anchorMax.y<=populatedTop)throw new Exception(platform+": empty panel did not expand");
                CheckBounds(board,scores,platform.ToString());
                menu.ApplyLayout(platform,size);Canvas.ForceUpdateCanvases();
                CheckBounds(board,scores,platform+" after layout refresh");
                if(platform==MainMenuPlatform.AndroidLandscape)Capture(menu,(int)size.x,(int)size.y);
                load.Invoke(menu,new object[]{populated});
                Canvas.ForceUpdateCanvases();
                var headerCorners=new Vector3[4];var scoreCorners=new Vector3[4];
                ((RectTransform)columns).GetWorldCorners(headerCorners);scores.GetWorldCorners(scoreCorners);
                float overlap=board.InverseTransformPoint(scoreCorners[1]).y-board.InverseTransformPoint(headerCorners[0]).y;
                if(!columns.gameObject.activeSelf||overlap>1f)throw new Exception(platform+": populated heading and scores overlap by "+overlap);
                CheckHeadingGap(board,platform+" after restoring rows");
            }
            Debug.Log("RANKING EMPTY BACKDROP CHECKS PASSED");
        }

        static void CheckHeadingGap(Transform board,string label)
        {
            var header=Array.Find(board.Find("Columns").GetComponentsInChildren<Text>(),text=>text.text=="PLAYER");
            var first=(RectTransform)board.Find("Scores/Ranking cards/Rank card 1");
            if(!header||!first)throw new Exception(label+": missing heading or first card");
            var headingCorners=new Vector3[4];var cardCorners=new Vector3[4];
            header.rectTransform.GetWorldCorners(headingCorners);first.GetWorldCorners(cardCorners);
            float gap=board.InverseTransformPoint(headingCorners[0]).y-board.InverseTransformPoint(cardCorners[1]).y;
            if(gap<0f||gap>22f)throw new Exception(label+": heading/card gap is "+gap);
        }

        static void CheckBounds(Transform board,RectTransform scores,string label)
        {
            var glass=scores.Find("TV Background") as RectTransform;
            if(!glass)throw new Exception(label+": missing glass background");
            var image=glass.GetComponent<Image>();
            if(!image.material.HasProperty("_GlassRect"))throw new Exception(label+": glass shader is missing");
            var corners=new Vector3[4];scores.GetWorldCorners(corners);
            var min=glass.InverseTransformPoint(corners[0]);var max=glass.InverseTransformPoint(corners[2]);
            var expected=new Vector4(min.x,min.y,max.x-min.x,max.y-min.y);
            var actual=image.materialForRendering.GetVector("_GlassRect");
            if((expected-actual).sqrMagnitude>.1f)throw new Exception(label+": glass does not cover panel: "+actual+" expected "+expected);
            var surface=(RectTransform)board.Find("Standings surface");
            var surfaceCorners=new Vector3[4];surface.GetWorldCorners(surfaceCorners);
            for(int i=0;i<4;i++)if((surfaceCorners[i]-corners[i]).sqrMagnitude>.1f)
                throw new Exception(label+": backing surface extends outside empty panel");
        }

        static void Capture(MainMenuScene menu,int width,int height)
        {
            var camera=Camera.main;var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;camera.aspect=(float)width/height;
            try
            {
                Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
                var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Builds/RankingPreviews/home-empty-android.png",image.EncodeToPNG());}
                finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
            }
            finally{camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
