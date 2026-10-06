using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
        // These are the API's fixed membership rewards, not locally credited balances.
        const int MembershipRewardFuel=5;
        string socialProgressKey,socialResultMessage;

        void BindSocialProgress()
        {
            string key=string.IsNullOrEmpty(playerIdentity)?null:
                "battlecities.social."+Uri.EscapeDataString(api.BaseUrl+"|"+playerIdentity)+".";
            if(key==socialProgressKey)return;
            socialProgressKey=key;socialResultMessage=null;ClearNativeLink();
            xReady=key!=null&&PlayerPrefs.GetInt(key+"follow",0)==1;
            repostReady=key==null?null:PlayerPrefs.GetString(key+"repost",null);
            commentReady=key==null?null:PlayerPrefs.GetString(key+"comment",null);
        }
        void SaveSocialProgress()
        {
            if(socialProgressKey==null)return;
            if(xReady)PlayerPrefs.SetInt(socialProgressKey+"follow",1);else PlayerPrefs.DeleteKey(socialProgressKey+"follow");
            SaveReadyTask("repost",repostReady);SaveReadyTask("comment",commentReady);PlayerPrefs.Save();
        }
        void SaveReadyTask(string kind,string id)
        {if(string.IsNullOrEmpty(id))PlayerPrefs.DeleteKey(socialProgressKey+kind);else PlayerPrefs.SetString(socialProgressKey+kind,id);}
        void SyncSocialProgress()
        {
            if(xError!=null||!xAuthenticated)return;
            if(!Yes(xStatus?["connected"]))xReady=false;
            if(Yes(xStatus?["follows"]))xReady=false;
            var repost=xStatus?["repostTask"] as JObject;var comment=xStatus?["commentTask"] as JObject;
            if(repostReady!=Text(repost?["id"])||Yes(repost?["claimed"]))repostReady=null;
            if(commentReady!=Text(comment?["id"])||Yes(comment?["claimed"]))commentReady=null;
            SaveSocialProgress();
        }
        void ClearVerifiedProgress(string kind)
        {
            if(kind=="follow")xReady=false;else if(kind=="repost")repostReady=null;else if(kind=="comment")commentReady=null;
            SaveSocialProgress();
        }
        static bool ValidTask(JToken token)
        {
            if(token==null||token.Type==JTokenType.Null)return true;
            if(!(token is JObject task)||task["id"]?.Type!=JTokenType.String||string.IsNullOrWhiteSpace(Text(task["id"]))||
                task["claimed"]?.Type!=JTokenType.Boolean||task["rewardFuel"]?.Type!=JTokenType.Integer)return false;
            string post=Text(task["postId"]);
            if(string.IsNullOrEmpty(post)||post.Length>20)return false;
            foreach(char c in post)if(c<'0'||c>'9')return false;
            return decimal.TryParse(Text(task["rewardFuel"]),NumberStyles.Integer,CultureInfo.InvariantCulture,out var fuel)&&fuel>0;
        }
        static bool ValidXStatus(JObject body)=>body?["authenticated"]?.Type==JTokenType.Boolean&&
            (!Yes(body["authenticated"])||body["connected"]?.Type==JTokenType.Boolean&&body["follows"]?.Type==JTokenType.Boolean&&
                ValidTask(body["repostTask"])&&ValidTask(body["commentTask"]));
        static bool ValidDiscordStatus(JObject body)=>body?["authenticated"]?.Type==JTokenType.Boolean&&
            (!Yes(body["authenticated"])||body["verified"]?.Type==JTokenType.Boolean&&body["rewardClaimed"]?.Type==JTokenType.Boolean);
        string SocialReward(string kind)=>kind=="follow"||kind=="discord"?MembershipRewardFuel.ToString(CultureInfo.InvariantCulture):Amount((xStatus?[kind+"Task"] as JObject)?["rewardFuel"]);
        static string ResultField(string kind)=>kind=="follow"?"follows":kind=="repost"?"reposted":kind=="comment"?"commented":"ok";
        static string VerifyPath(string kind)=>kind=="discord"?"/api/integrations/discord/claim-reward":"/api/integrations/x/verify-"+kind;
        static bool Confirmed(long code,JObject body,string kind)=>code>=200&&code<300&&Yes(body?["ok"])&&Yes(body?[ResultField(kind)])&&body?[kind=="discord"?"granted":"rewardGranted"]?.Type==JTokenType.Boolean;
        static bool RewardGranted(JObject body,string kind)=>Yes(body?[kind=="discord"?"granted":"rewardGranted"]);
        static string VerificationFailure(string kind,long code,JObject body)
        {
            if(code==401)return "CONNECT YOUR WALLET, THEN RETRY";
            if(code==429)return "TOO MANY CHECKS • WAIT, THEN RETRY";
            string name=kind=="discord"?"DISCORD":"X "+kind.ToUpperInvariant();
            if(code>=200&&code<300&&Yes(body?["ok"]))return name+" NOT CONFIRMED • COMPLETE IT, THEN RETRY";
            return name+" CHECK FAILED • RETRY";
        }
    }
}
