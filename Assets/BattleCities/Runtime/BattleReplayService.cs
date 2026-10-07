using System;
using System.Collections;
using System.Collections.Generic;
using BattleCities.Core;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
        private MainMenuApiClient CreateApi(string url)
        {
            var host=new GameObject("Replay request "+Guid.NewGuid().ToString("N"));host.SetActive(false);host.transform.SetParent(transform);
            var api=host.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);api.Configure(url);host.SetActive(true);return api;
        }
        public IEnumerator Prepare(ReplayRecorder recorder,ReplayArchive archive,Func<bool> current,Action done)
        {
            MainMenuApiClient api=null;
            try
            {
                if(string.IsNullOrEmpty(archive.apiUrl))yield break;
                api=CreateApi(archive.apiUrl);JObject player=null;
                yield return api.Request("GET","/api/player",null,(code,json,error)=>{if(code==200&&error==null)player=json;});
                if(!current()||player?["authenticated"]?.Value<bool>()!=true)yield break;
                archive.ownerId=(string)player["player"]?["id"];
                if(string.IsNullOrEmpty(archive.ownerId))yield break;
                var r=recorder.Data;
                var body=new JObject { ["simulationVersion"]=r.simulationVersion,["buildVersion"]=r.buildVersion,["levelNumber"]=r.levelNumber,
                    ["levelHash"]=r.levelHash,["configHash"]=r.configHash,["tickRate"]=60,["mode"]=r.mode };
                JObject response=null;
                yield return api.Request("POST","/api/replay-sessions",body,(code,json,error)=>{if(code>=200&&code<300&&error==null)response=json;});
                if(!current())yield break;
                var session=response?["session"];
                if(response?["ok"]?.Value<bool>()!=true||session==null)yield break;
                if((string)session["simulationVersion"]!=r.simulationVersion||(string)session["levelHash"]!=r.levelHash||
                   (string)session["configHash"]!=r.configHash||(string)session["mode"]!=r.mode||(int?)session["levelNumber"]!=r.levelNumber||
                   (int?)session["tickRate"]!=60||!uint.TryParse((string)session["seed"],out uint seed)||seed==0)yield break;
                var id=(string)session["id"];if(string.IsNullOrEmpty(id)||!id.StartsWith("urs-",StringComparison.Ordinal))yield break;
                recorder.Reseed(seed);archive.sessionId=id;archive.uploadStatus="Recording for review";
            }
            finally {if(api)Destroy(api.gameObject);done();}
        }
        public void Upload(ReplayArchive archive)
        {
            if(archive==null||string.IsNullOrEmpty(archive.sessionId)||string.IsNullOrEmpty(archive.ownerId)||(!string.IsNullOrEmpty(archive.replayId)&&(!NeedsMatch(archive)||!string.IsNullOrEmpty(archive.matchId)))||!uploading.Add(archive.replay.id))return;
            StartCoroutine(UploadRoutine(archive));
        }
        private IEnumerator UploadRoutine(ReplayArchive archive)
        {
            MainMenuApiClient api=null;
            try
            {
                api=CreateApi(archive.apiUrl);JObject player=null;
                yield return api.Request("GET","/api/player",null,(code,json,error)=>{if(code==200&&error==null)player=json;});
                if(player?["authenticated"]?.Value<bool>()!=true||(string)player["player"]?["id"]!=archive.ownerId)
                {archive.uploadStatus="Saved locally; sign in to the recording account to upload";yield break;}
                if(string.IsNullOrEmpty(archive.replayId))
                {
                    var body=new JObject { ["sessionId"]=archive.sessionId,["replay"]=JObject.Parse(ReplayJson.Write(archive.replay)) };
                    long status=0;JObject response=null;
                    yield return api.Request("POST","/api/replays/unity",body,(code,json,error)=>{status=code;if(string.IsNullOrEmpty(error))response=json;});
                    var item=response?["item"];
                    if(status>=200&&status<300&&response?["ok"]?.Value<bool>()==true&&item!=null&&!string.IsNullOrEmpty((string)item["id"]))
                    {archive.replayId=(string)item["id"];archive.verificationStatus=(string)item["verificationStatus"]??"pending";archive.uploadStatus="Uploaded; "+archive.verificationStatus;}
                    else archive.uploadStatus=status==409?"Upload conflict; local evidence retained":status==410?"Session expired; local evidence retained":"Upload pending; retry available";
                }
                if(!string.IsNullOrEmpty(archive.replayId)&&string.IsNullOrEmpty(archive.matchId)&&NeedsMatch(archive))
                {
                    var r=archive.replay;var body=new JObject { ["mode"]="single",["levelNumber"]=r.levelNumber,["score"]=r.claimedResult.score,["won"]=r.claimedResult.won,["replayId"]=archive.replayId };
                    JObject response=null;
                    yield return api.Request("POST","/api/matches/submit",body,(code,json,error)=>{if(code>=200&&code<300&&string.IsNullOrEmpty(error))response=json;});
                    if(response?["ok"]?.Value<bool>()==true&&!string.IsNullOrEmpty((string)response["result"]?["id"]))
                    {archive.matchId=(string)response["result"]["id"];archive.uploadStatus="Uploaded; match awaiting review";}
                    else archive.uploadStatus="Replay uploaded; match link pending retry";
                }
            }
            finally
            {
                if(api)Destroy(api.gameObject);uploading.Remove(archive.replay.id);
                try{BattleReplayStore.Save(archive);}catch(Exception e){Debug.LogWarning("Replay archive: "+e.Message);}
            }
        }
        public void RetryPending()
        {foreach(var archive in BattleReplayStore.List())Upload(archive);}
        private static bool NeedsMatch(ReplayArchive a)=>a.replay.mode=="single"&&a.replay.completion=="completed"&&!a.replay.debugUsed;
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
            finally {Destroy(api.gameObject);completed?.Invoke(message);}
        }
    }
}
