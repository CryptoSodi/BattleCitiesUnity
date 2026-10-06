using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
        string nativeFlowId,nativeProvider,linkBrowserUrl;
        public bool IsSocialLinkPending=>!string.IsNullOrEmpty(nativeFlowId);
#if UNITY_EDITOR
        public Action<string> EditorOpenUrlOverride;
#endif
        void ClearNativeLink(){nativeFlowId=nativeProvider=null;linkBrowserUrl=null;HideLinkInstructions(false);}
        static bool ValidFlowId(string value)
        {
            if(value==null||value.Length!=43)return false;
            foreach(char c in value)if(!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_'||c=='-'))return false;
            return true;
        }
        static bool ValidAuthorizationUrl(string value,string provider)
        {
            if(!Uri.TryCreate(value,UriKind.Absolute,out var url)||url.Scheme!="https"||url.UserInfo.Length!=0)return false;
            return provider=="x"?url.Host=="x.com"&&url.AbsolutePath=="/i/oauth2/authorize":
                provider=="discord"&&url.Host=="discord.com"&&url.AbsolutePath=="/oauth2/authorize";
        }
        static bool FutureExpiry(JToken token)
        {
            if(token?.Type==JTokenType.Date)return token.Value<DateTimeOffset>()>DateTimeOffset.UtcNow;
            return DateTimeOffset.TryParse(Text(token),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var value)&&value>DateTimeOffset.UtcNow;
        }
        IEnumerator StartNativeLink(int generation,string provider)
        {
            loading=true;socialResultMessage=null;SetStatus("PREPARING "+provider.ToUpperInvariant()+" CONNECTION...");Render();
            bool started=false,fallback=false;
            yield return api.Request("POST","/api/integrations/"+provider+"/oauth/native/start",new JObject(),(code,body,error)=>
            {
                if(generation!=version)return;
                fallback=code==404||code==405||code==501;
                string flow=Text(body?["flowId"]),url=Text(body?["authorizationUrl"]);
                started=code>=200&&code<300&&Yes(body?["ok"])&&ValidFlowId(flow)&&ValidAuthorizationUrl(url,provider)&&FutureExpiry(body?["expiresAt"]);
                if(started){nativeFlowId=flow;nativeProvider=provider;linkBrowserUrl=url;}
                else socialResultMessage=code==401?"CONNECT YOUR WALLET, THEN RETRY":code==429?"TOO MANY CONNECTION ATTEMPTS • WAIT, THEN RETRY":"SOCIAL CONNECTION UNAVAILABLE • RETRY";
            });
            if(generation!=version)yield break;
            loading=false;request=null;Render();
            if(started)
            {
                ShowLinkInstructions(provider,true);
                OpenLink(linkBrowserUrl,"APPROVE "+provider.ToUpperInvariant()+" IN THE BROWSER, THEN RETURN AND CHECK STATUS");
            }
            else if(fallback){ClearNativeLink();ShowLinkInstructions(provider);SetStatus("LINK WITH THE SAME WALLET IN YOUR BROWSER");}
            else SetStatus(socialResultMessage);
        }
        IEnumerator CheckNativeLink(int generation)
        {
            string provider=nativeProvider,flow=nativeFlowId;loading=true;SetStatus("CHECKING "+provider.ToUpperInvariant()+" CONNECTION...");Render();
            bool completed=false,terminal=false;
            yield return api.Request("GET","/api/integrations/oauth/native/status?flowId="+Uri.EscapeDataString(flow),null,(code,body,error)=>
            {
                if(generation!=version)return;
                var item=body?["item"] as JObject;string state=Text(item?["status"]);
                if(code==401){terminal=true;socialResultMessage="CONNECT YOUR WALLET, THEN RETRY";}
                else if(code==404){terminal=true;socialResultMessage="CONNECTION EXPIRED • CONNECT AGAIN";}
                else if(code>=200&&code<300&&Text(item?["provider"])==provider)
                {
                    completed=state=="completed";terminal=completed||state=="failed"||state=="expired";
                    socialResultMessage=completed?provider.ToUpperInvariant()+" CONNECTED • STATUS UPDATED":
                        state=="failed"?"CONNECTION FAILED OR CANCELLED • TRY AGAIN":state=="expired"?"CONNECTION EXPIRED • CONNECT AGAIN":
                        state=="pending"||state=="exchanging"?"FINISH IN YOUR BROWSER, THEN CHECK STATUS":"CONNECTION STATUS UNAVAILABLE • RETRY";
                }
                else socialResultMessage="CONNECTION STATUS UNAVAILABLE • RETRY";
            });
            if(generation!=version)yield break;
            if(terminal){ClearNativeLink();HideLinkInstructions(false);}
            yield return LoadSocials(generation);
            if(generation!=version)yield break;
            if(completed)api.RefreshNow();
        }
    }
}
