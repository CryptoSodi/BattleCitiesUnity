using System;
using System.Collections;
using System.Collections.Generic;
using BattleCities.Core;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    // Lives across scene changes so leaving a finished match does not abort its upload.
    public sealed class BattleReplayService : MonoBehaviour
    {
        private static BattleReplayService instance;
        public static BattleReplayService Instance
        {
            get
            {
                if(instance)return instance;
                var host=new GameObject("Replay archive transport");DontDestroyOnLoad(host);
                instance=host.AddComponent<BattleReplayService>();return instance;
            }
        }
        private readonly HashSet<string> uploading=new HashSet<string>();
        private float retryAt;
        public static event Action ResultsChanged;
#if UNITY_EDITOR
        public Func<string,string,JObject,Action<long,JObject,string>,IEnumerator> EditorRequestOverride;
        public Action<IEnumerator> EditorStartRoutineOverride;
#endif
        private void OnEnable(){SceneManager.sceneLoaded+=OnSceneLoaded;retryAt=Time.unscaledTime+2;}
        private void OnDisable(){SceneManager.sceneLoaded-=OnSceneLoaded;}
        private void OnSceneLoaded(Scene scene,LoadSceneMode mode){retryAt=Time.unscaledTime+2;}
        private void OnApplicationFocus(bool focused){if(focused)retryAt=Time.unscaledTime+1;}
        private void OnApplicationPause(bool paused){if(!paused)retryAt=Time.unscaledTime+1;}
        private void Update(){if(Time.unscaledTime>=retryAt){retryAt=Time.unscaledTime+30;RetryPending();}}
        private MainMenuApiClient CreateApi(string url)
        {
            var host=new GameObject("Replay request "+Guid.NewGuid().ToString("N"));host.SetActive(false);host.transform.SetParent(transform);
            var api=host.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);
            try {api.Configure(url);}
            catch {ReleaseApi(api);throw;}
#if UNITY_EDITOR
            api.EditorRequestOverride=EditorRequestOverride;
#endif
            host.SetActive(true);return api;
        }
        private static void ReleaseApi(MainMenuApiClient api)
        {if(api){if(Application.isPlaying)Destroy(api.gameObject);else DestroyImmediate(api.gameObject);}}
        private static bool Transient(long code)=>code==0||code==408||code==429||code>=500;
        private static string Failure(long code,string error,string fallback)
        {return (string.IsNullOrWhiteSpace(error)?fallback:error)+(code>0?" (HTTP "+code+")":"");}
        private IEnumerator PrepareRequest(MainMenuApiClient api,string method,string path,JObject body,ReplayArchive archive,bool walletRequired,Func<bool> current,Action<long,JObject,string> completed)
        {
            int attempt=0;
            while(current())
            {
                long status=0;JObject response=null;string error=null;
                yield return api.Request(method,path,body,(code,json,message)=>{status=code;response=json;error=message;});
                if(!current())yield break;
                attempt=Math.Min(attempt+1,10);
                if(!Transient(status)||!walletRequired&&attempt>=3){completed(status,response,error);yield break;}
                archive.uploadStatus="Reconnecting before battle; "+Failure(status,error,"Score service unavailable")+". You can return to the menu.";
                // A charged wallet match must not start without its server seed.
                yield return new WaitForSecondsRealtime(Math.Min(10,attempt));
            }
        }
        private static IEnumerator AwaitPreparationExit(Func<bool> current)
        {while(current())yield return new WaitForSecondsRealtime(1);}
        private static string Text(JToken value)=>value?.Type==JTokenType.String?value.Value<string>():null;
        private static bool Authenticated(JObject player)=>player?["authenticated"]?.Type==JTokenType.Boolean&&player["authenticated"].Value<bool>();
        private static bool AcceptSession(ReplayRecorder recorder,ReplayArchive archive,JObject response,out string failure)
        {
            failure="The server returned an invalid ranked session";
            try
            {
                if(response?["ok"]?.Type!=JTokenType.Boolean||response["ok"].Value<bool>()!=true||!(response["session"] is JObject session))return false;
                if(session["playerId"]!=null&&Text(session["playerId"])!=archive.ownerId)
                {failure="Server session belongs to a different account";return false;}
                var r=recorder.Data;
                if(Text(session["simulationVersion"])!=r.simulationVersion||Text(session["levelHash"])!=r.levelHash||
                    Text(session["configHash"])!=r.configHash||Text(session["mode"])!=r.mode||(int?)session["levelNumber"]!=r.levelNumber||
                    (int?)session["tickRate"]!=60||!uint.TryParse((string)session["seed"],out uint seed)||seed==0)
                {failure="Server session does not match this battle";return false;}
                var id=Text(session["id"]);if(string.IsNullOrEmpty(id)||!id.StartsWith("urs-",StringComparison.Ordinal))return false;
                recorder.Reseed(seed);archive.sessionId=id;archive.uploadStatus="Recording for review";return true;
            }
            catch(Exception){return false;}
        }
        public IEnumerator Prepare(ReplayRecorder recorder,ReplayArchive archive,Func<bool> current,Action done)
        {
            MainMenuApiClient api=null;bool walletRequired=archive.ownerProvider=="wallet";
            string expectedOwner=archive.ownerId;
            try
            {
                archive.uploadStatus=walletRequired?"Connecting score recording; battle is paused":"Recording locally; no ranked session was started";
                if(!CurrentNetwork(archive.apiUrl))
                {
                    if(walletRequired){archive.uploadStatus="Score service is unavailable. Return to the menu before starting.";yield return AwaitPreparationExit(current);}
                    yield break;
                }
                api=CreateApi(archive.apiUrl);JObject player=null;long status=0;string failure=null;
                yield return PrepareRequest(api,"GET","/api/player",null,archive,walletRequired,current,(code,json,error)=>{status=code;failure=error;if(code==200&&error==null)player=json;});
                if(!current())yield break;
                if(!Authenticated(player))
                {
                    archive.uploadStatus=walletRequired?"Wallet session unavailable. Return to the menu and sign in again.":
                        status==200||status==401?"Recording locally; sign in before starting to save online scores":"Recording locally; "+Failure(status,failure,"Player session unavailable");
                    if(walletRequired)yield return AwaitPreparationExit(current);yield break;
                }
                var identity=player["player"] as JObject;
                string owner=Text(identity?["id"]),provider=Text(identity?["provider"]);
                if(string.IsNullOrEmpty(owner)||!string.IsNullOrEmpty(expectedOwner)&&owner!=expectedOwner||walletRequired&&provider!="wallet")
                {
                    archive.uploadStatus="Wallet account changed or unavailable. Return to the menu and reconnect the recording account.";
                    yield return AwaitPreparationExit(current);yield break;
                }
                archive.ownerId=owner;archive.ownerProvider=provider;walletRequired=provider=="wallet";
                var r=recorder.Data;
                var body=new JObject { ["simulationVersion"]=r.simulationVersion,["buildVersion"]=r.buildVersion,["levelNumber"]=r.levelNumber,
                    ["levelHash"]=r.levelHash,["configHash"]=r.configHash,["tickRate"]=60,["mode"]=r.mode };
                JObject response=null;
                yield return PrepareRequest(api,"POST","/api/replay-sessions",body,archive,walletRequired,current,(code,json,error)=>{status=code;failure=error;if(code>=200&&code<300&&error==null)response=json;});
                if(!current())yield break;
                if(response?["session"] is JObject)
                {
                    // Browser/native cookies can change while the session request is in flight.
                    JObject sessionPlayer=null;
                    yield return PrepareRequest(api,"GET","/api/player",null,archive,walletRequired,current,(code,json,error)=>
                    {if(code==200&&error==null)sessionPlayer=json;});
                    if(!current())yield break;
                    var currentIdentity=sessionPlayer?["player"] as JObject;
                    if(!Authenticated(sessionPlayer)||Text(currentIdentity?["id"])!=archive.ownerId||
                        walletRequired&&Text(currentIdentity?["provider"])!="wallet")
                    {
                        archive.uploadStatus="Wallet account changed or expired. Return to the menu and reconnect the recording account.";
                        yield return AwaitPreparationExit(current);yield break;
                    }
                }
                if(AcceptSession(recorder,archive,response,out var invalidSession))yield break;
                archive.uploadStatus=(walletRequired?"Battle paused; ":"Recording locally; ")+Failure(status,failure,invalidSession)+
                    (walletRequired?". Return to the menu to reconnect.":"");
                if(walletRequired)yield return AwaitPreparationExit(current);
            }
            finally {ReleaseApi(api);done?.Invoke();}
        }
        public void Upload(ReplayArchive archive)
        {
            if(archive==null||!archive.HasPendingUpload||archive.uploadStopped||archive.retryAfterUtcTicks>DateTime.UtcNow.Ticks||uploading.Count>=2||!CurrentNetwork(archive.apiUrl)||!uploading.Add(archive.replay.id))return;
#if UNITY_EDITOR
            if(EditorStartRoutineOverride!=null){EditorStartRoutineOverride(UploadRoutine(archive));return;}
#endif
            StartCoroutine(UploadRoutine(archive));
        }
        private static bool CurrentNetwork(string url)
        {
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))return false;
#if !UNITY_EDITOR
            return string.Equals(uri.GetLeftPart(UriPartial.Authority),MainMenuApiClient.DefaultApiBaseUrl,StringComparison.OrdinalIgnoreCase)&&
                uri.AbsolutePath=="/"&&string.IsNullOrEmpty(uri.Query)&&string.IsNullOrEmpty(uri.Fragment)&&string.IsNullOrEmpty(uri.UserInfo);
#else
            return true;
#endif
        }
        private static void Persist(ReplayArchive archive)
        {try{BattleReplayStore.Save(archive);}catch(Exception e){Debug.LogWarning("Replay archive: "+e.Message);}}
        private static void ScheduleRetry(ReplayArchive archive)
        {
            archive.uploadAttempts=Math.Min(archive.uploadAttempts+1,8);
            archive.retryAfterUtcTicks=DateTime.UtcNow.AddSeconds(Math.Min(300,15*Math.Pow(2,archive.uploadAttempts-1))).Ticks;
        }
        private static void UploadFailure(ReplayArchive archive,long status,string error,string operation)
        {
            archive.uploadStopped=status==400||status==410||status==422||
                (status==409&&!string.Equals(error,"Replay duration exceeds issued session time",StringComparison.Ordinal));
            archive.uploadStatus=operation+": "+Failure(status,error,"The server did not accept the request")+
                (archive.uploadStopped?"; local evidence retained":"; saved locally, retry scheduled");
        }
        private IEnumerator UploadRoutine(ReplayArchive archive)
        {
            MainMenuApiClient api=null;
            try
            {
                ScheduleRetry(archive);Persist(archive);
                api=CreateApi(archive.apiUrl);JObject player=null;long status=0;string failure=null;
                yield return api.Request("GET","/api/player",null,(code,json,error)=>{status=code;failure=error;if(code==200&&error==null)player=json;});
                if(player?["authenticated"]?.Value<bool>()!=true)
                {archive.uploadStatus=status==200||status==401?"Saved locally; sign in to the recording account to upload":"Saved locally; "+Failure(status,failure,"Player session unavailable")+"; retry scheduled";yield break;}
                if((string)player["player"]?["id"]!=archive.ownerId)
                {archive.uploadStatus="Saved locally; sign in to the recording account to upload";yield break;}
                if(string.IsNullOrEmpty(archive.replayId))
                {
                    var body=new JObject { ["sessionId"]=archive.sessionId,["replay"]=JObject.Parse(ReplayJson.Write(archive.replay)) };
                    JObject response=null;
                    yield return api.Request("POST","/api/replays/unity",body,(code,json,error)=>{status=code;failure=error;if(string.IsNullOrEmpty(error))response=json;});
                    var item=response?["item"];
                    if(status>=200&&status<300&&response?["ok"]?.Value<bool>()==true&&item!=null&&!string.IsNullOrEmpty((string)item["id"]))
                    {
                        archive.replayId=(string)item["id"];archive.verificationStatus=(string)item["verificationStatus"]??"pending";
                        archive.uploadStatus="Replay uploaded; "+archive.verificationStatus;
                        // Preserve the accepted replay ID before starting the independent score request.
                        Persist(archive);
                    }
                    else {UploadFailure(archive,status,failure,"Replay upload failed");yield break;}
                }
                if(string.IsNullOrEmpty(archive.matchId)&&archive.NeedsMatchSubmission)
                {
                    var r=archive.replay;var body=new JObject { ["mode"]="single",["levelNumber"]=r.levelNumber,["score"]=r.claimedResult.score,["won"]=r.claimedResult.won,["replayId"]=archive.replayId };
                    JObject response=null;
                    yield return api.Request("POST","/api/matches/submit",body,(code,json,error)=>{status=code;failure=error;if(code>=200&&code<300&&string.IsNullOrEmpty(error))response=json;});
                    if(response?["ok"]?.Value<bool>()==true&&!string.IsNullOrEmpty((string)response["result"]?["id"]))
                    {
                        archive.matchId=(string)response["result"]["id"];
                        archive.uploadStatus="Score submitted; "+((string)response["result"]["validationStatus"]??"awaiting review");
                        Persist(archive);ResultsChanged?.Invoke();
                    }
                    else UploadFailure(archive,status,failure,"Score submission failed");
                }
                if(!archive.HasPendingUpload){archive.uploadAttempts=0;archive.retryAfterUtcTicks=0;}
            }
            finally
            {
                ReleaseApi(api);uploading.Remove(archive.replay.id);Persist(archive);
            }
        }
        public void RetryPending()
        {foreach(var archive in BattleReplayStore.List())Upload(archive);}
        public void DownloadRecent(string url,Action<string> completed)=>StartCoroutine(DownloadRoutine(url,completed));
        private IEnumerator DownloadRoutine(string url,Action<string> completed)
        {
            var api=CreateApi(url);int saved=0;string message="Recordings unavailable; try again";
            try
            {
                JObject player=null,list=null;
                yield return api.Request("GET","/api/player",null,(code,json,error)=>{if(code==200&&string.IsNullOrEmpty(error))player=json;});
                string owner=(string)player?["player"]?["id"];
                if(player?["authenticated"]?.Value<bool>()!=true||string.IsNullOrEmpty(owner)){message="Sign in to sync recordings";yield break;}
                yield return api.Request("GET","/api/replays/unity?limit=20",null,(code,json,error)=>{if(code==200&&string.IsNullOrEmpty(error))list=json;});
                if(list?["ok"]?.Value<bool>()!=true||!(list["items"] is JArray items))yield break;
                foreach(var summary in items)
                {
                    string id=(string)summary["id"];if(string.IsNullOrEmpty(id)||!id.StartsWith("urp-",StringComparison.Ordinal))continue;
                    JObject response=null;
                    yield return api.Request("GET","/api/replays/unity?id="+Uri.EscapeDataString(id),null,(code,json,error)=>{if(code==200&&string.IsNullOrEmpty(error))response=json;});
                    var item=response?["item"];if(item?["replay"]==null||(string)item["playerId"]!=owner)continue;
                    try
                    {
                        var replay=ReplayJson.Read(item["replay"].ToString());
                        var archive=BattleReplayStore.Load(replay.id)??new ReplayArchive {replay=replay};
                        archive.apiUrl=url;archive.ownerId=owner;archive.replayId=id;archive.sessionId=(string)item["sessionId"];
                        archive.verificationStatus=(string)item["verificationStatus"]??"pending";archive.uploadStatus="Uploaded; "+archive.verificationStatus;
                        BattleReplayStore.Save(archive);saved++;
                    }
                    catch(Exception e){Debug.LogWarning("Replay download rejected: "+e.Message);}
                }
                message=saved==0?"No online recordings found":saved+" online recordings synced";
            }
            finally {ReleaseApi(api);completed?.Invoke(message);}
        }
    }
}
