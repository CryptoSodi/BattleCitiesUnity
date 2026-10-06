using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BattleCities.UI
{
    [CreateAssetMenu(menuName="Battle Cities/UI/Profile web links")]
    public sealed class ProfileLinks : ScriptableObject
    {
        [Tooltip("Existing legacy web-game URL for replay playback. Leave empty until a reachable deployment is configured.")]
        public string replayWebBaseUrl="";
        [Tooltip("Public Unity web-game URL. Profile deep links use the playerProfile query parameter.")]
        public string publicWebBaseUrl="";
        public static bool ValidPlayerId(string id)=>id!=null&&id.Length<=160&&Regex.IsMatch(id,@"^ply-[a-z0-9-]+$",RegexOptions.IgnoreCase);
        public static bool ValidMatchId(string id)=>id!=null&&id.Length<=160&&Regex.IsMatch(id,@"^mtc-[a-z0-9-]+$",RegexOptions.IgnoreCase);
        static bool Base(string source,out Uri uri)=>Uri.TryCreate(source,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.IsLoopback&&uri.Scheme=="http");
        public string ShareUrl(string id)
        {
            if(!ValidPlayerId(id))return null;
            return Base(publicWebBaseUrl,out var origin)?origin.GetLeftPart(UriPartial.Path)+"?playerProfile="+Uri.EscapeDataString(id):null;
        }
        public string ReplayUrl(string playerId,string matchId)=>ValidPlayerId(playerId)&&ValidMatchId(matchId)&&Base(replayWebBaseUrl,out var origin)?new Uri(origin,"/?profileReplayPlayer="+Uri.EscapeDataString(playerId)+"&profileReplayMatch="+Uri.EscapeDataString(matchId)).AbsoluteUri:null;
        public static string IncomingPlayer()=>IncomingPlayer(Application.absoluteURL);
        public static string IncomingPlayer(string source)
        {
            if(!Uri.TryCreate(source,UriKind.Absolute,out var url))return null;
            foreach(var part in url.Query.TrimStart('?').Split('&'))
            {var pair=part.Split(new[]{'='},2);if(pair.Length==2&&pair[0]=="playerProfile"){var id=Uri.UnescapeDataString(pair[1]);return ValidPlayerId(id)?id:null;}}
            return null;
        }
    }
}
