using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
        public void Refresh()
        {
            if(!IsOpen||!api||page!=Page.Treasury&&page!=Page.Socials)return;
            CancelRequest();request=StartCoroutine(page==Page.Treasury?LoadTreasury(version):LoadSocials(version));
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
            loading=true;xError=discordError=null;SetStatus("CHECKING SOCIAL TASKS...");Render();
            yield return api.Request("GET","/api/integrations/x/status",null,(code,body,error)=>
            {if(generation!=version)return;xStatus=null;xAuthenticated=false;if(code==401)return;if(code>=200&&code<300&&body?["authenticated"]?.Type==JTokenType.Boolean){xStatus=body;xAuthenticated=Yes(body["authenticated"]);}else xError=error??"X status unavailable";});
            if(generation!=version)yield break;
            yield return api.Request("GET","/api/integrations/discord/verification",null,(code,body,error)=>
            {if(generation!=version)return;discord=null;discordAuthenticated=false;if(code==401)return;if(code>=200&&code<300&&body?["authenticated"]?.Type==JTokenType.Boolean){discord=body;discordAuthenticated=Yes(body["authenticated"]);}else discordError=error??"Discord status unavailable";});
            if(generation!=version)yield break;
            if(Yes(xStatus?["follows"]))xReady=false;
            if(Yes(xStatus?["repostTask"]?["claimed"]))repostReady=null;
            if(Yes(xStatus?["commentTask"]?["claimed"]))commentReady=null;
            loading=false;request=null;SetStatus(xError!=null||discordError!=null?"SOCIAL VERIFICATION UNAVAILABLE • REFRESH TO RETRY":!xAuthenticated&&!discordAuthenticated?"CONNECT YOUR WALLET TO VERIFY SOCIAL TASKS":"SOCIAL TASK STATUS UPDATED");Render();
        }
        void ActFollow()
        {
            if(loading)return;if(xError!=null){Refresh();return;}
            if(!xAuthenticated){api.ConnectWallet();SetStatus("CONNECT YOUR WALLET, THEN REFRESH");return;}
            if(!Yes(xStatus?["connected"])){OpenSocialAuth("x");return;}
            if(Yes(xStatus?["follows"])){SetStatus("X FOLLOW ALREADY VERIFIED");return;}
            if(xReady){Verify("/api/integrations/x/verify-follow","follows");return;}
            xReady=true;Render();OpenLink("https://x.com/BattleCitiesHQ","FOLLOW ON X, THEN RETURN AND SELECT VERIFY FOLLOW");
        }
        public void RefreshTasks()
        {
            if(loading||!IsOpen||page!=Page.Socials)return;
            CancelRequest();request=StartCoroutine(VerifyReadyTasks(version));
        }
        IEnumerator VerifyReadyTasks(int generation)
        {
            var checks=new System.Collections.Generic.List<string>();
            if(xAuthenticated&&Yes(xStatus?["connected"])&&!Yes(xStatus?["follows"])&&xReady)checks.Add("follow");
            if(Yes(xStatus?["follows"]))
            {
                if(repostReady!=null&&repostReady==Text(xStatus?["repostTask"]?["id"])&&!Yes(xStatus?["repostTask"]?["claimed"]))checks.Add("repost");
                if(commentReady!=null&&commentReady==Text(xStatus?["commentTask"]?["id"])&&!Yes(xStatus?["commentTask"]?["claimed"]))checks.Add("comment");
            }
            loading=true;SetStatus(checks.Count>0?"VERIFYING SOCIAL TASKS...":"CHECKING SOCIAL TASKS...");Render();bool failed=false;
            foreach(string kind in checks)
            {
                yield return api.Request("POST","/api/integrations/x/verify-"+kind,null,(code,body,error)=>
                {if(generation!=version)return;string field=kind=="follow"?"follows":kind=="repost"?"reposted":"commented";if(code<200||code>=300||!Yes(body?[field]))failed=true;});
                if(generation!=version)yield break;
            }
            yield return LoadSocials(generation);
            if(generation!=version)yield break;
            if(failed)SetStatus("SOME TASKS ARE NOT VERIFIED • COMPLETE THEM, THEN RETRY");
            else if(checks.Count>0)api.RefreshNow();
        }
        void ActTask(string kind)
        {
            if(loading||!Yes(xStatus?["follows"]))return;
            var task=xStatus?[kind+"Task"] as JObject;if(task==null||Yes(task["claimed"]))return;
            string id=Text(task["id"]),ready=kind=="repost"?repostReady:commentReady;
            if(id==ready){Verify("/api/integrations/x/verify-"+kind,kind=="repost"?"reposted":"commented");return;}
            string postId=Text(task["postId"]);if(string.IsNullOrEmpty(postId)){SetStatus("TASK LINK UNAVAILABLE • REFRESH TO RETRY");return;}
            if(kind=="repost")repostReady=id;else commentReady=id;
            Render();OpenLink((kind=="repost"?"https://x.com/intent/retweet?tweet_id=":"https://x.com/intent/tweet?in_reply_to=")+Uri.EscapeDataString(postId),"COMPLETE THE TASK, THEN RETURN TO VERIFY");
        }
        void ActDiscord()
        {
            if(loading)return;if(discordError!=null){Refresh();return;}
            if(!discordAuthenticated){api.ConnectWallet();SetStatus("CONNECT YOUR WALLET, THEN REFRESH");return;}
            if(Yes(discord?["rewardClaimed"])){SetStatus("DISCORD REWARD ALREADY CLAIMED");return;}
            if(Yes(discord?["verified"])){Verify("/api/integrations/discord/claim-reward","ok");return;}
            OpenSocialAuth("discord");
        }
        void OpenSocialAuth(string provider)
        {
            // OAuth callbacks require a browser session. Never put the native session cookie in a URL.
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenLink(api.BaseUrl.TrimEnd('/')+"/api/integrations/"+provider+"/oauth/start","FINISH IN YOUR BROWSER, THEN RETURN AND REFRESH");
#else
            ShowLinkInstructions(provider);
#endif
        }
        void OpenLink(string url,string message){Application.OpenURL(url);SetStatus(message);}
        void Verify(string path,string resultField)
        {if(loading)return;CancelRequest();request=StartCoroutine(VerifyTask(version,path,resultField));}
        IEnumerator VerifyTask(int generation,string path,string resultField)
        {
            loading=true;SetStatus("VERIFYING WITH THE SERVER...");Render();bool confirmed=false;string failure=null;
            yield return api.Request("POST",path,null,(code,body,error)=>
            {if(generation!=version)return;confirmed=code>=200&&code<300&&Yes(body?[resultField]);failure=error??Text(body?["error"]);});
            if(generation!=version)yield break;
            yield return LoadSocials(generation);
            if(generation!=version)yield break;
            SetStatus(confirmed?"VERIFICATION CONFIRMED • STATUS UPDATED":failure!=null?"VERIFICATION FAILED • TRY AGAIN AFTER COMPLETING THE TASK":"TASK NOT CONFIRMED YET • COMPLETE IT, THEN VERIFY AGAIN");
            if(confirmed)api.RefreshNow();
        }
    }
}
