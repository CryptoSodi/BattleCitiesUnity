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
            var seasonIndex=JObject.Parse("{\"seasons\":[{\"id\":\"season-test\",\"name\":\"Season test\",\"endsAt\":\"2026-11-01T00:00:00Z\"},{\"id\":\"season-test\",\"name\":\"Duplicate\"}]}");
            var seasons=RankingPageData.Seasons(seasonIndex);
            if(seasons.Count!=1||seasons[0].IsCycle||seasons[0].EndsAt==null)throw new Exception("Season options are invalid");
            var body=JObject.Parse("{\"rows\":[],\"enabled\":false,\"nextRewardAt\":\"2026-10-06T18:30:00Z\",\"currentPlayer\":{\"rank\":17},\"me\":{\"rank\":3}}");
            var cycle=RankingPageData.Parse(body,new RankingPeriod{Id="cycle",IsCycle=true,Label="CURRENT CYCLE"});
            if(cycle.MyRank!=17||cycle.PayoutsEnabled||cycle.EndsAt==null)throw new Exception("Cycle rank or payout state is invalid");
            var season=RankingPageData.Parse(body,seasons[0]);
            if(season.MyRank!=3||season.Cycle||season.EndsAt!=seasons[0].EndsAt)throw new Exception("Cycle and season data were mixed");
            body["me"]["restricted"]=true;
            if(RankingPageData.Parse(body,seasons[0]).MyRank!=null)throw new Exception("Restricted player has a rank");
            if(RankingPageData.Parse(new JObject(),seasons[0])!=null)throw new Exception("Malformed response became a valid empty board");
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
