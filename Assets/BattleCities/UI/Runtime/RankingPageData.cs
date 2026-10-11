using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace BattleCities.UI
{
    public sealed class RankingPeriod
    {
        public string Id, Label, Status;
        public bool IsCycle;
        public bool IsAllTime => Id == "all";
        public DateTimeOffset? EndsAt;
    }

    /// <summary>Read-only presentation data from the existing cycle and season APIs.</summary>
    public sealed class RankingPageData
    {
        public readonly List<MainMenuApiClient.RankingRow> Rows=new List<MainMenuApiClient.RankingRow>();
        public int? MyRank;
        public bool Cycle, PayoutsEnabled;
        public int IntervalMinutes=30;
        public DateTimeOffset? EndsAt;
        public string PeriodId, PeriodLabel;

        public static List<RankingPeriod> Seasons(JObject response)
        {
            var result=new List<RankingPeriod>();
            if(!(response?["seasons"] is JArray seasons))return result;
            var ids=new HashSet<string>();
            foreach(var value in seasons)
            {
                if(!(value is JObject))continue;
                string id=(string)value["id"],name=(string)value["name"];
                if(string.IsNullOrWhiteSpace(id)||!ids.Add(id))continue;
                result.Add(new RankingPeriod{Id=id,Label=string.IsNullOrWhiteSpace(name)?id:name.ToUpperInvariant(),
                    Status=(string)value["status"],EndsAt=Date(value["endsAt"])});
            }
            return result;
        }

        public static RankingPageData Parse(JObject response,RankingPeriod period)
        {
            if(!(response?["rows"] is JArray rows))return null;
            var data=new RankingPageData{Cycle=period.IsCycle,PeriodId=period.Id,PeriodLabel=period.Label,
                EndsAt=period.IsCycle?Date(response["nextRewardAt"]):period.EndsAt,
                PayoutsEnabled=period.IsCycle&&(bool?)response["enabled"]==true,
                IntervalMinutes=Math.Max(1,Integer(response["rewardIntervalMinutes"],30))};
            foreach(var value in rows)
            {
                if(!(value is JObject row))continue;
                data.Rows.Add(new MainMenuApiClient.RankingRow{rank=Integer(row["rank"],data.Rows.Count+1),
                    playerId=(string)row["playerId"],displayName=(string)row["displayName"]??"PLAYER",
                    totalPoints=Integer(row["totalPoints"]),matches=Integer(row["matches"])});
            }
            var me=response[period.IsCycle?"currentPlayer":"me"] as JObject;
            int rank=Integer(me?["rank"]);
            if(rank>0&&(bool?)me?["restricted"]!=true&&(bool?)me?["guest"]!=true)data.MyRank=rank;
            return data;
        }

        public static DateTimeOffset? Date(JToken token)
        {
            if(token==null||token.Type==JTokenType.Null)return null;
            if(token is JValue dateValue)
            {
                if(dateValue.Value is DateTimeOffset offset)return offset;
                if(dateValue.Value is DateTime date)return new DateTimeOffset(date.Kind==DateTimeKind.Unspecified?DateTime.SpecifyKind(date,DateTimeKind.Utc):date);
            }
            return DateTimeOffset.TryParse(token.ToString(),CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,out var value)?value:(DateTimeOffset?)null;
        }
        static int Integer(JToken token,int fallback=0)=>int.TryParse(token?.ToString(),NumberStyles.Integer,
            CultureInfo.InvariantCulture,out var value)?Math.Max(0,value):fallback;
    }
}
