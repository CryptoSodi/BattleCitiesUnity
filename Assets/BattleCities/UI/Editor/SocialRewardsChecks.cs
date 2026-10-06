using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.Editor
{
    /// <summary>Exercises the main site's social contract without real OAuth or reward mutations.</summary>
    public static class SocialRewardsChecks
    {
        public static string Status {get;private set;}="Not started";
        public static void Start()
                {
            if(Application.isPlaying)UnityEngine.Object.FindFirstObjectByType<MainMenuScene>().StartCoroutine(Run(null));
            else new SocialFixturePump().Run(Run);
        }
        static string Caption(OperationsScreen screen,int card)=>screen.Scroll.content.Find("Card "+card+"/Action/Caption").GetComponent<TMP_Text>().text;
        static void Click(OperationsScreen screen,int card)=>screen.Scroll.content.Find("Card "+card).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        static IEnumerator Wait(OperationsScreen screen)
        {
            float deadline=Time.realtimeSinceStartup+10;int quiet=0;
            while(quiet<10&&Time.realtimeSinceStartup<deadline){quiet=screen.IsLoading?0:quiet+1;yield return null;}
            if(quiet<10)throw new Exception("Social fixture request timed out.");
        }
        static IEnumerator Run(SocialFixturePump pump)
        {
            Status="Running";int checks=0;
            void Check(bool ok,string what){if(!ok)throw new Exception("Social rewards: "+what);checks++;}
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.OpenQuarters();
            var screen=menu.GetComponent<OperationsScreen>();var realApi=menu.GetComponent<MainMenuApiClient>();var frame=(RectTransform)screen.Root.parent;
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MenuTheme")[0]));
            var input=UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool inputEnabled=input&&input.enabled;if(input)input.enabled=false;
            var host=new GameObject("Social reward local fixture");host.SetActive(false);
            var api=host.AddComponent<MainMenuApiClient>();api.ConfigureGuestFallback(false);api.ConfigureAutomaticRefresh(false);
            api.Configure("http://127.0.0.1:1/social-fixture/"+Guid.NewGuid().ToString("N"));
            var fixture=new Fixture();api.EditorRequestOverride=fixture.Request;host.SetActive(true);
            var links=new List<string>();screen.EditorOpenUrlOverride=links.Add;if(pump!=null){screen.EditorStartRoutineOverride=pump.Start;api.EditorStartRoutineOverride=pump.Start;}
            try
            {
                api.RefreshNow();for(int n=0;n<100&&api.LastPlayer==null;n++)yield return null;yield return Wait(screen);
                Check(api.LastPlayer!=null,"fixture player session loaded");
                screen.Configure(menu,theme,api,frame);screen.Open(true);yield return Wait(screen);
                Check(Caption(screen,1)=="CONNECT X"&&Caption(screen,4)=="LOCKED","unlinked X state");
                Click(screen,1);yield return Wait(screen);
                Check(screen.IsSocialLinkPending&&screen.IsLinkNoticeOpen&&links.Last().StartsWith("https://x.com/i/oauth2/authorize"),"native X authorization");
                Check(fixture.VerifyCount==0,"linking must not verify or grant X rewards");
                screen.Refresh();yield return Wait(screen);Check(screen.IsSocialLinkPending&&screen.IsLinkNoticeOpen,"pending native connection stays pending");
                fixture.NativeState="completed";screen.Refresh();yield return Wait(screen);
                Check(!screen.IsSocialLinkPending&&!screen.IsLinkNoticeOpen&&Caption(screen,1)=="FOLLOW ON X","native X completion uses server status");
                Check(fixture.VerifyCount==0,"passive return refresh is read-only");
                Click(screen,1);Check(Caption(screen,1)=="VERIFY FOLLOW"&&links.Last()=="https://x.com/BattleCitiesHQ","follow opens X before verification");
                fixture.Identity="B";fixture.Connected=false;api.RefreshNow();yield return Wait(screen);
                Check(Caption(screen,1)=="CONNECT X","another account cannot inherit follow readiness");
                fixture.Identity="A";fixture.Connected=true;api.RefreshNow();yield return Wait(screen);
                Check(Caption(screen,1)=="VERIFY FOLLOW","saved follow readiness survives account/page reload");
                Click(screen,1);yield return Wait(screen);
                Check(Caption(screen,1)=="FOLLOWED"&&screen.Status.Contains("+5 FUEL ADDED"),"follow grant feedback");
                int before=fixture.VerifyCount;Click(screen,1);Check(fixture.VerifyCount==before,"completed follow cannot repeat verification");
                Click(screen,4);Check(Caption(screen,4)=="VERIFY REPOST"&&links.Last().Contains("intent/retweet?tweet_id=123"),"repost intent and verification step");
                fixture.Failure="repost";fixture.FailureCode=503;Click(screen,4);Click(screen,4);yield return Wait(screen);
                Check(Caption(screen,4)=="VERIFY REPOST"&&screen.Status.Contains("FAILED")&&!fixture.RepostClaimed,"failed verification retains retry without completing");
                fixture.Failure=null;Click(screen,4);yield return Wait(screen);
                Check(Caption(screen,4)=="COMPLETED"&&screen.Status.Contains("+7 FUEL ADDED"),"repost uses campaign's supplied reward");
                before=fixture.VerifyCount;Click(screen,4);Check(fixture.VerifyCount==before,"completed repost cannot claim twice");
                Click(screen,5);Check(links.Last().Contains("intent/tweet?in_reply_to=456"),"comment reply intent");
                fixture.Failure="comment";fixture.FailureCode=200;Click(screen,5);yield return Wait(screen);
                Check(Caption(screen,5)=="VERIFY COMMENT"&&screen.Status.Contains("NOT CONFIRMED"),"comment not found remains uncompleted");
                fixture.FailureCode=429;Click(screen,5);yield return Wait(screen);Check(screen.Status.Contains("WAIT")&&!fixture.CommentClaimed,"rate limit remains retryable");
                fixture.FailureCode=409;Click(screen,5);yield return Wait(screen);Check(!fixture.CommentClaimed&&!screen.Status.Contains("FUEL ADDED"),"failed claim cannot report a reward");
                fixture.Failure=null;Click(screen,5);yield return Wait(screen);
                Check(Caption(screen,5)=="COMPLETED"&&screen.Status.Contains("+9 FUEL ADDED"),"comment grant feedback");
                fixture.CommentClaimed=false;fixture.CommentId="comment-new";screen.Refresh();yield return Wait(screen);Click(screen,5);
                fixture.CommentId="comment-newer";before=fixture.VerifyCount;screen.Refresh();yield return Wait(screen);
                Check(Caption(screen,5)=="COMMENT"&&fixture.VerifyCount==before,"task rotation clears readiness and passive refresh never verifies");
                fixture.RepostClaimed=false;fixture.RepostId="repost-new";screen.Refresh();yield return Wait(screen);Click(screen,4);Click(screen,5);
                screen.RefreshTasks();yield return Wait(screen);Check(fixture.RepostClaimed&&fixture.CommentClaimed&&screen.Status.Contains("FUEL REWARDS ADDED"),"explicit Refresh verifies opened eligible missions");
                fixture.NativeState="completed";Click(screen,3);yield return Wait(screen);Check(screen.IsSocialLinkPending&&links.Last().StartsWith("https://discord.com/oauth2/authorize"),"native Discord authorization");
                screen.Refresh();yield return Wait(screen);Check(Caption(screen,3)=="VERIFIED"&&!screen.IsSocialLinkPending,"Discord completion reads automatic reward claim");
                fixture.DiscordClaimed=false;screen.Refresh();yield return Wait(screen);Check(Caption(screen,3)=="CLAIM FUEL","verified Discord membership with unclaimed Fuel");
                Click(screen,3);yield return Wait(screen);Check(Caption(screen,3)=="VERIFIED"&&screen.Status.Contains("+5 FUEL ADDED"),"Discord uses ok/granted response");
                before=fixture.DiscordClaims;Click(screen,3);Check(fixture.DiscordClaims==before,"claimed Discord reward cannot repeat");
                fixture.DiscordClaimed=false;fixture.NoGrant="discord";screen.Refresh();yield return Wait(screen);Click(screen,3);yield return Wait(screen);
                Check(screen.Status.Contains("NO NEW FUEL")&&!screen.Status.Contains("FUEL ADDED"),"a successful response without a grant never reports new Fuel");fixture.NoGrant=null;
                fixture.NoTasks=true;before=fixture.VerifyCount;screen.Refresh();yield return Wait(screen);screen.RefreshTasks();yield return Wait(screen);
                Check(Caption(screen,4)=="LOCKED"&&Caption(screen,5)=="LOCKED"&&fixture.VerifyCount==before,"explicit null tasks stay locked and are never submitted");
                fixture.NoTasks=false;screen.Refresh();yield return Wait(screen);
                foreach(var v in new[]{(MainMenuPlatform.Web,1583,924,"web"),(MainMenuPlatform.AndroidLandscape,844,390,"seeker"),(MainMenuPlatform.Psg1,1240,1080,"psg1")})
                {
                    menu.ApplyLayout(v.Item1,new Vector2(v.Item2,v.Item3));Canvas.ForceUpdateCanvases();
                    for(int i=0;i<6;i++)foreach(var label in screen.Scroll.content.Find("Card "+i).GetComponentsInChildren<TMP_Text>())
                    {label.ForceMeshUpdate();Check(!label.isTextOverflowing,v.Item4+" "+i+"/"+label.name+" text fits");}
                    MainMenuChecks.Capture(v.Item1,v.Item2,v.Item3,"social-mainsite-"+v.Item4);
                }
                fixture.Connected=fixture.Follows=false;fixture.NativeState="failed";screen.Refresh();yield return Wait(screen);Click(screen,1);yield return Wait(screen);screen.Refresh();yield return Wait(screen);
                Check(!screen.IsSocialLinkPending&&!screen.IsLinkNoticeOpen&&screen.Status.Contains("FAILED"),"failed native link is retryable");
                fixture.NativeState="expired";Click(screen,1);yield return Wait(screen);screen.Refresh();yield return Wait(screen);Check(!screen.IsSocialLinkPending&&screen.Status.Contains("EXPIRED"),"expired native link clears flow");
                fixture.InvalidUrl=true;before=links.Count;Click(screen,1);yield return Wait(screen);Check(!screen.IsSocialLinkPending&&links.Count==before&&screen.Status.Contains("UNAVAILABLE"),"invalid authorization URL cannot open");
                fixture.InvalidUrl=false;fixture.NativeState="pending";Click(screen,1);yield return Wait(screen);Check(screen.IsSocialLinkPending,"new native attempt starts");
                fixture.Identity="B";api.RefreshNow();yield return Wait(screen);Check(!screen.IsSocialLinkPending&&!screen.IsLinkNoticeOpen,"account switch clears native credentials and dialog");
                fixture.Identity="A";api.RefreshNow();yield return Wait(screen);before=links.Count;Click(screen,1);screen.Close(false);yield return null;yield return null;yield return null;
                Check(!screen.IsSocialLinkPending&&links.Count==before,"closing cancels stale native start callback");
                Status="PASS: "+checks+" social reward, persistence, native-link and platform checks";Debug.Log(Status);
            }
            finally
            {
                screen.Close(false);screen.EditorOpenUrlOverride=null;screen.EditorStartRoutineOverride=null;api.EditorStartRoutineOverride=null;
                foreach(string identity in new[]{"A","B"})foreach(string key in new[]{"follow","repost","comment"})PlayerPrefs.DeleteKey("battlecities.social."+Uri.EscapeDataString(api.BaseUrl+"|fixture-wallet-"+identity)+"."+key);
                PlayerPrefs.Save();screen.Configure(menu,theme,realApi,frame);menu.OpenSocials();menu.RefreshLayout();if(Application.isPlaying)UnityEngine.Object.Destroy(host);else UnityEngine.Object.DestroyImmediate(host);if(input)input.enabled=inputEnabled;
                if(Status=="Running")Status="FAILED; see the social fixture error";
            }
        }
        sealed class Fixture
        {
            public string Identity="A",NativeState="pending",NativeProvider,Failure,NoGrant,RepostId="repost-1",CommentId="comment-1";
            public bool Connected,Follows,RepostClaimed,CommentClaimed,DiscordVerified,DiscordClaimed,InvalidUrl,NoTasks;
            public int FailureCode,VerifyCount,DiscordClaims;
            public IEnumerator Request(string method,string path,JObject payload,Action<long,JObject,string> reply)
            {
                yield return null;yield return null;
                JObject body;long code=200;
                if(path=="/api/session")body=new JObject{["authenticated"]=true,["provider"]="wallet"};
                else if(path=="/api/player")body=new JObject{["authenticated"]=true,["player"]=new JObject{["id"]="fixture-"+Identity,["walletAddress"]="fixture-wallet-"+Identity,["displayName"]="SOCIAL FIXTURE",["provider"]="wallet"}};
                else if(path=="/api/leaderboard/rewards")body=new JObject{["rows"]=new JArray(),["enabled"]=false};
                else if(path.StartsWith("/api/rankings"))body=new JObject{["rows"]=new JArray(),["seasons"]=new JArray()};
                else if(path.EndsWith("/oauth/native/start"))
                {
                    if(method!="POST"||payload==null)throw new Exception("Native start requires authenticated JSON POST.");
                    NativeProvider=path.Contains("/x/")?"x":"discord";
                    string token=new string(NativeProvider=="x"?'X':'D',43);
                    body=new JObject{["ok"]=true,["flowId"]=token,["expiresAt"]=DateTimeOffset.UtcNow.AddMinutes(10),["authorizationUrl"]=InvalidUrl?"https://invalid.example/authorize":NativeProvider=="x"?"https://x.com/i/oauth2/authorize?state=native."+token:"https://discord.com/oauth2/authorize?state=native."+token};
                }
                else if(path.StartsWith("/api/integrations/oauth/native/status?"))
                {
                    if(NativeState=="completed"){if(NativeProvider=="x")Connected=true;else DiscordVerified=DiscordClaimed=true;}
                    body=new JObject{["item"]=new JObject{["provider"]=NativeProvider,["status"]=NativeState,["expiresAt"]=DateTimeOffset.UtcNow.AddMinutes(10)}};
                }
                else if(path=="/api/integrations/x/status")body=new JObject{["authenticated"]=true,["connected"]=Connected,["follows"]=Follows,["repostTask"]=Follows&&!NoTasks?Task(RepostId,"123",7,RepostClaimed):null,["commentTask"]=Follows&&!NoTasks?Task(CommentId,"456",9,CommentClaimed):null};
                else if(path=="/api/integrations/discord/verification")body=new JObject{["authenticated"]=true,["verified"]=DiscordVerified,["rewardClaimed"]=DiscordClaimed};
                else if(path.StartsWith("/api/integrations/x/verify-"))
                {
                    VerifyCount++;string kind=path.Substring(path.LastIndexOf('-')+1),field=kind=="follow"?"follows":kind=="repost"?"reposted":"commented";
                    if(Failure==kind){code=FailureCode;body=new JObject{["ok"]=code==200,[field]=false,["rewardGranted"]=false};}
                    else{if(kind=="follow")Follows=true;else if(kind=="repost")RepostClaimed=true;else CommentClaimed=true;body=new JObject{["ok"]=true,[field]=true,["rewardGranted"]=true};}
                }
                else if(path=="/api/integrations/discord/claim-reward"){DiscordClaims++;DiscordClaimed=true;body=new JObject{["ok"]=true,["granted"]=NoGrant!="discord"};}
                else{code=404;body=new JObject{["error"]="Unknown fixture request"};}
                reply(code,body,code>=400?"Fixture rejection":null);
            }
            static JObject Task(string id,string post,int fuel,bool claimed)=>new JObject{["id"]=id,["postId"]=post,["rewardFuel"]=fuel,["claimed"]=claimed};
        }
    }
}
