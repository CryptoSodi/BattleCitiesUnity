using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class RankingCardsChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            ImportArt();RankingPageChecks.CheckParsing();
            int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception("RANKING CARDS: "+message);checks++;}
            var period=new RankingPeriod{Id="cycle",Label="CURRENT CYCLE",IsCycle=true};
            var response=JObject.Parse("{\"rows\":[{\"rank\":1,\"displayName\":\"FIRST\",\"totalPoints\":1200,\"matches\":4}],\"currentPlayer\":{\"rank\":24,\"totalPoints\":8400}}");
            var parsed=RankingPageData.Parse(response,period);
            Check(parsed.MyRank==24&&parsed.CurrentPlayer.rank==24&&parsed.CurrentPlayer.totalPoints==8400,"current rank and score come from API, independent of top row count");
            Check(!parsed.CurrentPlayer.matchesKnown,"missing personal match count remains unknown");
            response["currentPlayer"]["restricted"]=true;
            Check(RankingPageData.Parse(response,period).CurrentPlayer==null,"restricted current player has no pinned card");
            response["currentPlayer"]["restricted"]=false;response["currentPlayer"]["guest"]=true;
            Check(RankingPageData.Parse(response,period).CurrentPlayer==null,"guest has no fabricated current rank");
            response["currentPlayer"]["guest"]=false;response["currentPlayer"]["rank"]=0;
            Check(RankingPageData.Parse(response,period).CurrentPlayer==null,"unranked current player is not assigned a position");
            var season=JObject.Parse("{\"rows\":[],\"me\":{\"rank\":7,\"totalPoints\":3000,\"matches\":2},\"currentPlayer\":{\"rank\":24}}");
            Check(RankingPageData.Parse(season,new RankingPeriod{Id="s1",Label="SEASON"}).CurrentPlayer.rank==7,"season card uses me, not cycle rank");
            RankingPreviewCapture.Run();
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            var events=UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if(!events)events=new GameObject("Ranking preview events",typeof(EventSystem)).GetComponent<EventSystem>();
            var home=menu.Content.Find("Leaderboard/Scores/Ranking cards");
            var cards=home.GetComponentsInChildren<RankingRowCard>(true);
            Check(cards.Count(c=>!c.IsCurrentPlayer&&c.gameObject.activeSelf)==10,"home shows ten top cards");
            var yours=cards.Single(c=>c.IsCurrentPlayer);
            Check(yours.Data.rank==24&&yours.transform.Find("Rank").GetComponent<TMP_Text>().text=="24","pinned home rank is actual 24, never row 11");
            Check(yours.transform.Find("Matches").GetComponent<TMP_Text>().text=="—","unknown matches display a dash");
            var art=Resources.Load<RankingCardArt>("RankingCardArt");
            foreach(var card in cards.Where(c=>!c.IsCurrentPlayer&&c.Data.rank<=3)){
                Check(card.GetComponent<Image>().sprite==art.Row(card.Data.rank,card.Data.rank-1,false),"podium row uses its corresponding skin");
                Check(card.transform.Find("Rank badge").GetComponent<Image>().sprite==art.Medal(card.Data.rank),"podium uses blank laurel plus native rank text");
            }
            var page=menu.GetComponent<RankingScreen>();var board=page.Data;
            Check(page.Root.Find("Standings/Your rank card").GetComponent<RankingRowCard>().Data.rank==24,"full page pins the same API current player outside scrolling rows");
            var scroll=page.TableScroll;Canvas.ForceUpdateCanvases();
            var rowButtons=scroll.content.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeSelf).ToArray();
            Check(rowButtons.Length==10,"full ranking rows remain selectable");
            var first=rowButtons[0].GetComponent<RankingRowCard>();
            Check(!first.transform.Find("Player").GetComponent<TMP_Text>().richText,"player names render as plain text");
            var last=(RectTransform)rowButtons.Last().transform;events.SetSelectedGameObject(last.gameObject);scroll.Reveal(last);Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,last);
            Check(bounds.min.y>=scroll.viewport.rect.yMin-1&&bounds.max.y<=scroll.viewport.rect.yMax+1,"controller focus reveals complete final card");
            Check(last.GetComponent<Outline>().enabled,"selection shows cyan card focus");
            scroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();scroll.OnScroll(new PointerEventData(events){scrollDelta=new Vector2(0,-3)});Canvas.ForceUpdateCanvases();
            Check(scroll.verticalNormalizedPosition<.99f,"mouse-wheel still scrolls cards");
            page.Present(new RankingPageData{Cycle=true,PeriodId="cycle",PeriodLabel="CURRENT CYCLE"});
            Check(page.Root.Find("Standings/Empty standings").gameObject.activeSelf&&!page.Root.Find("Standings/Your rank card").gameObject.activeSelf,"valid empty response clears rows and personal card");
            page.Present(board);scroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
            Capture(menu,MainMenuPlatform.AndroidLandscape,1688,780,"ranking-page-seeker.png");
            Capture(menu,MainMenuPlatform.Psg1,1240,1080,"ranking-page-psg1.png");
            Debug.Log("RANKING CARD CHECKS PASSED: "+checks+". Sample previews: Builds/RankingPreviews");
        }
        static void Capture(MainMenuScene menu,MainMenuPlatform platform,int width,int height,string name)
        {
            var camera=Camera.main;var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;camera.aspect=(float)width/height;
            try{
                menu.ApplyLayout(platform,new Vector2(width,height));Canvas.ForceUpdateCanvases();camera.Render();camera.Render();
                var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Builds/RankingPreviews/"+name,image.EncodeToPNG());}
                finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
            }finally{camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void ImportArt()
        {
            AssetDatabase.Refresh();const string folder="Assets/BattleCities/UI/Art/shared/ranking-cards/";
            var art=AssetDatabase.LoadAssetAtPath<RankingCardArt>("Assets/BattleCities/Resources/RankingCardArt.asset");
            if(!art){art=ScriptableObject.CreateInstance<RankingCardArt>();AssetDatabase.CreateAsset(art,"Assets/BattleCities/Resources/RankingCardArt.asset");}
            Sprite Load(string name){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(folder+name+".png");if(!sprite)throw new Exception("Missing ranking card artwork: "+name);return sprite;}
            art.goldRow=Load("row-gold");art.silverRow=Load("row-silver");art.bronzeRow=Load("row-bronze");art.creamRow=Load("row-cream");art.blueRow=Load("row-ice-blue");art.yourRow=Load("row-you");
            art.goldLaurel=Load("laurel-gold");art.silverLaurel=Load("laurel-silver");art.bronzeLaurel=Load("laurel-bronze");
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        }
    }
}
