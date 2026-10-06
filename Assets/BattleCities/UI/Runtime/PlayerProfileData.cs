using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace BattleCities.UI
{
    public sealed class PlayerProfileData
    {
        public string Id,Name,Provider,Joined,Season;
        public long? SeasonRank;
        public long Points,Matches,BestScore,TotalRecords;
        public int Page,PageSize;
        public readonly List<PlayerProfileMatch> Battles=new List<PlayerProfileMatch>();
        public int TotalPages=>PageSize>0?(int)Math.Min(10000,Math.Max(1,TotalRecords/PageSize+(TotalRecords%PageSize>0?1:0))):1;
        public static PlayerProfileData Parse(JObject body,string expectedId)
        {
            var item=body?["item"] as JObject;if(item==null)throw new FormatException("Missing profile");
            string id=(string)item["id"],name=(string)item["displayName"],provider=(string)item["provider"];
            if(id!=expectedId||string.IsNullOrWhiteSpace(name)||!(provider=="wallet"||provider=="google"||provider=="guest"))throw new FormatException("Invalid profile identity");
            var all=item["stats"]?["allTime"] as JObject;var season=item["stats"]?["currentSeason"] as JObject;
            var page=item["recentMatchesPage"] as JObject;var rows=item["recentMatches"] as JArray;
            if(all==null||season==null||page==null||rows==null||rows.Count>100)throw new FormatException("Incomplete combat record");
            int currentPage=checked((int)Number(page["page"])),pageSize=checked((int)Number(page["pageSize"]));
            if(currentPage<1||pageSize<1||pageSize>100)throw new FormatException("Invalid pagination");
            var value=new PlayerProfileData{Id=id,Name=name,Provider=provider,Joined=Date(item["joinedAt"]),Season=(string)season["name"]??"CURRENT SEASON",Points=Number(all["totalPoints"]),Matches=Number(all["matches"]),BestScore=Number(item["highscores"]?["primary"]),TotalRecords=Number(page["total"]),Page=currentPage,PageSize=pageSize};
            var rank=season["rank"];if(rank==null)throw new FormatException("Missing rank");
            value.SeasonRank=rank.Type==JTokenType.Null?(long?)null:Number(rank);
            foreach(var token in rows)
            {
                var row=token as JObject;string matchId=(string)row?["id"],mode=(string)row?["mode"];
                if(!ProfileLinks.ValidMatchId(matchId)||(mode!="single"&&mode!="multi")||row?["won"]?.Type!=JTokenType.Boolean||row?["replayAvailable"]?.Type!=JTokenType.Boolean)throw new FormatException("Invalid battle row");
                value.Battles.Add(new PlayerProfileMatch{Id=matchId,Mode=mode=="single"?"SINGLE":"MULTI",Stage=Number(row["levelNumber"]),Score=Number(row["score"]),Points=Number(row["gamePoints"]),Won=(bool)row["won"],Replay=(bool)row["replayAvailable"],Date=Date(row["createdAt"])});
            }
            return value;
        }
        static long Number(JToken token)
        {if(token==null||token.Type!=JTokenType.Integer||!long.TryParse(token.ToString(),out long n)||n<0)throw new FormatException("Invalid statistic");return n;}
        static string Date(JToken token)
        {
            if(token?.Type==JTokenType.Date)return token.Value<DateTime>().ToUniversalTime().ToString("dd MMM yyyy",CultureInfo.InvariantCulture).ToUpperInvariant();
            if(DateTimeOffset.TryParse((string)token,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date))return date.UtcDateTime.ToString("dd MMM yyyy",CultureInfo.InvariantCulture).ToUpperInvariant();
            return "—";
        }
        public static string Format(long value)=>value.ToString("N0",CultureInfo.InvariantCulture);
    }
    public sealed class PlayerProfileMatch
    {public string Id,Mode,Date;public long Stage,Score,Points;public bool Won,Replay;}
}
