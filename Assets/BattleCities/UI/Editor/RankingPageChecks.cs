using System;
using Newtonsoft.Json.Linq;
using BattleCities.UI;
using UnityEngine;

namespace BattleCities.Editor
{
    /// <summary>Focused checks for the cycle/season response boundary and ranking UI.</summary>
    public static class RankingPageChecks
    {
        public static string ApiProbeStatus {get;private set;}="Not started";
        public static void ProbeApi()
        {
            ApiProbeStatus="Pending";
            var api=UnityEngine.Object.FindFirstObjectByType<MainMenuApiClient>();
            api.StartCoroutine(api.Request("GET","/api/rankings?scope=gaming",null,(status,body,error)=>
            {ApiProbeStatus="HTTP "+status+", seasons="+(body?["seasons"] as JArray)?.Count+", rows="+(body?["rows"] as JArray)?.Count+", error="+error;}));
        }
        public static void CheckParsing()
        {
            CheckHomeCycleBinding();
            var seasonIndex=JObject.Parse("{\"seasons\":[{\"id\":\"season-test\",\"name\":\"Season test\",\"endsAt\":\"2026-11-01T00:00:00Z\"},{\"id\":\"season-test\",\"name\":\"Duplicate\"}]}");
            var seasons=RankingPageData.Seasons(seasonIndex);
            if(seasons.Count!=1||seasons[0].IsCycle||seasons[0].EndsAt==null)throw new Exception("Season options are invalid");
            var body=JObject.Parse("{\"rows\":[],\"enabled\":false,\"nextRewardAt\":\"2026-10-06T18:30:00Z\",\"currentPlayer\":{\"rank\":17},\"me\":{\"rank\":3}}");
            var cycle=RankingPageData.Parse(body,new RankingPeriod{Id="cycle",IsCycle=true,Label="CURRENT CYCLE"});
            if(cycle.MyRank!=17||cycle.PayoutsEnabled||cycle.EndsAt==null)throw new Exception("Cycle rank or payout state is invalid");
            var season=RankingPageData.Parse(body,seasons[0]);
            if(season.MyRank!=3||season.Cycle||season.EndsAt!=seasons[0].EndsAt)throw new Exception("Cycle and season data were mixed");
            var allTime=RankingPageData.Parse(body,new RankingPeriod{Id="all",Label="ALL TIME"});
            if(allTime.MyRank!=3||allTime.Cycle||allTime.EndsAt!=null||allTime.PayoutsEnabled)
                throw new Exception("All-time standings inherited cycle or season payout state");
            body["me"]["restricted"]=true;
            if(RankingPageData.Parse(body,seasons[0]).MyRank!=null)throw new Exception("Restricted player has a rank");
            if(RankingPageData.Parse(new JObject(),seasons[0])!=null)throw new Exception("Malformed response became a valid empty board");
        }
        static void CheckHomeCycleBinding()
        {
            var fixture=new GameObject("Cycle ranking regression");
            fixture.SetActive(false);
            try
            {
                var menu=fixture.AddComponent<MainMenuScene>();
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var title=new GameObject("Status",typeof(RectTransform),typeof(UnityEngine.UI.Text));
                title.transform.SetParent(fixture.transform);
                typeof(MainMenuScene).GetField("rankingAvailability",flags).SetValue(menu,title.GetComponent<UnityEngine.UI.Text>());
                var season=typeof(MainMenuScene).GetMethod("OnApiRankingsLoaded",flags);
                var cycle=typeof(MainMenuScene).GetMethod("OnApiRoundLoaded",flags);
                var populated=new MainMenuApiClient.RoundSnapshot{payoutsEnabled=false,rows=new[]{
                    new MainMenuApiClient.RankingRow{rank=1,displayName="TEST WALLET",totalPoints=1040,matches=3}}};
                var rows=typeof(MainMenuScene).GetField("rows",flags);
                season.Invoke(menu,new object[]{new MainMenuApiClient.RankingsSnapshot{rows=Array.Empty<MainMenuApiClient.RankingRow>()}});
                cycle.Invoke(menu,new object[]{populated});
                season.Invoke(menu,new object[]{new MainMenuApiClient.RankingsSnapshot{rows=Array.Empty<MainMenuApiClient.RankingRow>()}});
                if(((string[])rows.GetValue(menu)).Length!=1||!title.GetComponent<UnityEngine.UI.Text>().text.Contains("CURRENT CYCLE"))
                    throw new Exception("Empty season overwrote populated cycle with payouts paused");
                cycle.Invoke(menu,new object[]{new MainMenuApiClient.RoundSnapshot{rows=Array.Empty<MainMenuApiClient.RankingRow>()}});
                if(((string[])rows.GetValue(menu)).Length!=0)throw new Exception("Previous cycle rows persisted into empty cycle");
                cycle.Invoke(menu,new object[]{null});
                if(title.GetComponent<UnityEngine.UI.Text>().text!="RANKINGS UNAVAILABLE")throw new Exception("Failed cycle appeared as empty success");
            }
            finally{UnityEngine.Object.DestroyImmediate(fixture);}
        }
        public static RankingPageData SampleBoard()
        {
            var data=new RankingPageData{Cycle=true,PeriodId="cycle",PeriodLabel="CURRENT CYCLE",MyRank=17,
                EndsAt=DateTimeOffset.UtcNow.AddMinutes(20),PayoutsEnabled=false};
            for(int i=1;i<=20;i++)data.Rows.Add(new MainMenuApiClient.RankingRow{rank=i,displayName=i==1?"COMMANDER WITH A LONG NAME":"PLAYER "+i,totalPoints=12345678-i*1000,matches=1234-i});
            return data;
        }
    }
}
