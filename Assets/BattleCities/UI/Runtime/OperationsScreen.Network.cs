using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
#if UNITY_EDITOR
        // Used only by isolated edit-mode fixtures; production uses Unity coroutines.
        public Action<IEnumerator> EditorStartRoutineOverride;
#endif
        Coroutine StartRequest(IEnumerator routine)
        {
#if UNITY_EDITOR
            if(EditorStartRoutineOverride!=null){EditorStartRoutineOverride(routine);return null;}
#endif
            return StartCoroutine(routine);
        }
        public void Refresh()
        {
            if(!IsOpen||!api||page!=Page.Treasury&&page!=Page.Socials)return;
            if(page==Page.Socials)BindSocialProgress();
            CancelRequest();request=StartRequest(page==Page.Treasury?LoadTreasury(version):IsSocialLinkPending?CheckNativeLink(version):LoadSocials(version));
        }
        void FooterAction()
        {
            if(page==Page.Treasury&&!treasuryAuthenticated&&!loading&&accountError==null){api.ConnectWallet();SetStatus("CONNECT YOUR WALLET TO VIEW YOUR TREASURY");}
            else Refresh();
        }
        IEnumerator LoadTreasury(int generation)
        {
            loading=true;accountError=ledgerError=null;account=null;ledger=null;treasuryAuthenticated=false;
            SetStatus("LOADING BALANCES & TRANSACTIONS...");Render();
            yield return api.Request("GET","/api/economy/account",null,(code,body,error)=>
            {
                if(generation!=version)return;
                if(code==401||code>=200&&code<300&&body?["authenticated"]?.Type==JTokenType.Boolean&&!Yes(body["authenticated"]))return;
                if(code>=200&&code<300&&body?["account"] is JObject data){account=data;treasuryAuthenticated=true;}
                else accountError=error??"Account unavailable";
            });
            if(generation!=version)yield break;
            if(treasuryAuthenticated)yield return api.Request("GET","/api/economy/ledger",null,(code,body,error)=>
            {if(generation!=version)return;if(code>=200&&code<300&&body?["entries"] is JArray entries)ledger=entries;else ledgerError=error??"History unavailable";});
            if(generation!=version)yield break;
            loading=false;request=null;SetStatus(accountError!=null?"TREASURY UNAVAILABLE • REFRESH TO RETRY":!treasuryAuthenticated?"CONNECT YOUR WALLET TO VIEW YOUR TREASURY":ledgerError!=null?"HOLDINGS UPDATED • HISTORY UNAVAILABLE":"TREASURY UPDATED • RECENT 20 TRANSACTIONS");Render();
        }
        IEnumerator LoadSocials(int generation)
        {
            BindSocialProgress();
            loading=true;xError=discordError=null;SetStatus("CHECKING SOCIAL TASKS...");Render();
            yield return api.Request("GET","/api/integrations/x/status",null,(code,body,error)=>
            {if(generation!=version)return;xStatus=null;xAuthenticated=false;if(code==401)return;if(code>=200&&code<300&&ValidXStatus(body)){xStatus=body;xAuthenticated=Yes(body["authenticated"]);}else xError=error??"X status unavailable";});
            if(generation!=version)yield break;
            yield return api.Request("GET","/api/integrations/discord/verification",null,(code,body,error)=>
            {if(generation!=version)return;discord=null;discordAuthenticated=false;if(code==401)return;if(code>=200&&code<300&&ValidDiscordStatus(body)){discord=body;discordAuthenticated=Yes(body["authenticated"]);}else discordError=error??"Discord status unavailable";});
            if(generation!=version)yield break;
            SyncSocialProgress();
            loading=false;request=null;SetStatus(xError!=null||discordError!=null?"SOCIAL VERIFICATION UNAVAILABLE • REFRESH TO RETRY":!xAuthenticated&&!discordAuthenticated?"CONNECT YOUR WALLET TO VERIFY SOCIAL TASKS":socialResultMessage??"SOCIAL TASK STATUS UPDATED");Render();
        }
        void ActFollow()
        {
            if(loading)return;if(xError!=null){Refresh();return;}
            if(!xAuthenticated){api.ConnectWallet();SetStatus("CONNECT YOUR WALLET, THEN REFRESH");return;}
            if(!Yes(xStatus?["connected"])){OpenSocialAuth("x");return;}
            if(Yes(xStatus?["follows"])){SetStatus("X FOLLOW ALREADY VERIFIED");return;}
            if(xReady){Verify("follow");return;}
            xReady=true;SaveSocialProgress();socialResultMessage=null;Render();OpenLink("https://x.com/BattleCitiesHQ","FOLLOW ON X, THEN RETURN AND SELECT VERIFY FOLLOW");
        }
        public void RefreshTasks()
        {
            if(loading||!IsOpen||page!=Page.Socials)return;
            socialResultMessage=null;
            if(IsSocialLinkPending){Refresh();return;}
            CancelRequest();request=StartRequest(VerifyReadyTasks(version));
        }
        IEnumerator VerifyReadyTasks(int generation)
        {
            var checks=new System.Collections.Generic.List<string>();
            if(xAuthenticated&&Yes(xStatus?["connected"])&&!Yes(xStatus?["follows"])&&xReady)checks.Add("follow");
            if(Yes(xStatus?["follows"]))
            {
                var repost=xStatus?["repostTask"] as JObject;var comment=xStatus?["commentTask"] as JObject;
                if(repostReady!=null&&repostReady==Text(repost?["id"])&&!Yes(repost?["claimed"]))checks.Add("repost");
                if(commentReady!=null&&commentReady==Text(comment?["id"])&&!Yes(comment?["claimed"]))checks.Add("comment");
            }
            loading=true;SetStatus(checks.Count>0?"VERIFYING SOCIAL TASKS...":"CHECKING SOCIAL TASKS...");Render();bool failed=false,confirmedAny=false,grantedAny=false;
            foreach(string kind in checks)
            {
                yield return api.Request("POST",VerifyPath(kind),null,(code,body,error)=>
                {
                    if(generation!=version)return;
                    if(!Confirmed(code,body,kind)){failed=true;socialResultMessage=VerificationFailure(kind,code,body);return;}
                    confirmedAny=true;grantedAny|=RewardGranted(body,kind);ClearVerifiedProgress(kind);
                });
                if(generation!=version)yield break;
            }
            if(!failed&&checks.Count>0)socialResultMessage=grantedAny?"SOCIAL TASKS VERIFIED • FUEL REWARDS ADDED":"SOCIAL TASKS VERIFIED • NO NEW FUEL";
            yield return LoadSocials(generation);
            if(generation!=version)yield break;
            if(confirmedAny)api.RefreshNow();
        }
        void ActTask(string kind)
        {
            if(loading)return;if(xError!=null){Refresh();return;}if(!xAuthenticated||!Yes(xStatus?["follows"]))return;
            var task=xStatus?[kind+"Task"] as JObject;if(task==null||Yes(task["claimed"]))return;
            string id=Text(task["id"]),ready=kind=="repost"?repostReady:commentReady;
            if(id==ready){Verify(kind);return;}
            string postId=Text(task["postId"]);if(string.IsNullOrEmpty(postId)){SetStatus("TASK LINK UNAVAILABLE • REFRESH TO RETRY");return;}
            if(kind=="repost")repostReady=id;else commentReady=id;
            SaveSocialProgress();socialResultMessage=null;
            Render();OpenLink((kind=="repost"?"https://x.com/intent/retweet?tweet_id=":"https://x.com/intent/tweet?in_reply_to=")+Uri.EscapeDataString(postId),"COMPLETE THE TASK, THEN RETURN TO VERIFY");
        }
        void ActDiscord()
        {
            if(loading)return;if(discordError!=null){Refresh();return;}
            if(!discordAuthenticated){api.ConnectWallet();SetStatus("CONNECT YOUR WALLET, THEN REFRESH");return;}
            if(Yes(discord?["rewardClaimed"])){SetStatus("DISCORD REWARD ALREADY CLAIMED");return;}
            if(Yes(discord?["verified"])){Verify("discord");return;}
            OpenSocialAuth("discord");
        }
        void OpenSocialAuth(string provider)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenLink(api.BaseUrl.TrimEnd('/')+"/api/integrations/"+provider+"/oauth/start","FINISH IN YOUR BROWSER, THEN RETURN AND REFRESH");
#else
            CancelRequest();request=StartRequest(StartNativeLink(version,provider));
#endif
        }
        void OpenLink(string url,string message)
        {
#if UNITY_EDITOR
            if(EditorOpenUrlOverride!=null)EditorOpenUrlOverride(url);else Application.OpenURL(url);
#else
            Application.OpenURL(url);
#endif
            SetStatus(message);
        }
        void Verify(string kind)
        {if(loading)return;socialResultMessage=null;CancelRequest();request=StartRequest(VerifyTask(version,kind));}
        IEnumerator VerifyTask(int generation,string kind)
        {
            loading=true;SetStatus(kind=="discord"?"CLAIMING DISCORD FUEL...":"VERIFYING X "+kind.ToUpperInvariant()+"...");Render();bool confirmed=false;string fuel=SocialReward(kind);
            yield return api.Request("POST",VerifyPath(kind),null,(code,body,error)=>
            {
                if(generation!=version)return;
                confirmed=Confirmed(code,body,kind);
                socialResultMessage=confirmed?(kind=="discord"?"DISCORD":"X "+kind.ToUpperInvariant())+(RewardGranted(body,kind)?" • +"+fuel+" FUEL ADDED":" VERIFIED • NO NEW FUEL"):VerificationFailure(kind,code,body);
                if(confirmed)ClearVerifiedProgress(kind);
            });
            if(generation!=version)yield break;
            yield return LoadSocials(generation);
            if(generation!=version)yield break;
            if(confirmed)api.RefreshNow();
        }
    }
}
