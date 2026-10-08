#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using BattleCities.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Tests
{
    public static class ReplayRecoveryChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static IEnumerator Response(Action action){action();yield break;}
        static void Drain(IEnumerator routine,Action<object> onYield=null)
        {
            try {while(routine.MoveNext()){if(routine.Current is IEnumerator nested)Drain(nested,onYield);else onYield?.Invoke(routine.Current);}}
            finally {(routine as IDisposable)?.Dispose();}
        }
        static JObject Player(string id="recovery-owner")=>new JObject { ["authenticated"]=true,
            ["player"]=new JObject { ["id"]=id,["provider"]="wallet" } };
        static ReplayArchive Archive(BattleReplay template)=>new ReplayArchive { replay=ReplayJson.Copy(template),
            apiUrl="https://replay-fixture.invalid",ownerId="recovery-owner",ownerProvider="wallet",sessionId="urs-"+Guid.NewGuid().ToString("N") };

        // Every request is intercepted; this check never signs in or sends real scores.
        [MenuItem("Battle Cities/Checks/Replay delivery recovery")]
        public static void Run()
        {
            string previousDirectory=BattleReplayStore.EditorDirectoryOverride;
            string directory=Path.Combine(Path.GetTempPath(),"BattleCities-ReplayRecovery-"+Guid.NewGuid().ToString("N"));
            var host=new GameObject("Isolated replay delivery fixture");host.SetActive(false);
            var service=host.AddComponent<BattleReplayService>();service.EditorStartRoutineOverride=routine=>Drain(routine);
            int changed=0;Action onChanged=()=>changed++;BattleReplayService.ResultsChanged+=onChanged;
            try
            {
                BattleReplayStore.EditorDirectoryOverride=directory;
                var template=ReplayChecks.Fixture();
                // A structurally valid completed envelope for transport checks only.
                // Simulation determinism is covered separately by ReplayChecks.
                template.completion="completed";template.claimedResult.lost=true;template.debugUsed=false;

                int playerRequests=0,sessionRequests=0;bool prepared=false;
                var simulation=new BattleSimulation(ReplayJson.Copy(template.map),template.levelNumber);
                var recorder=new ReplayRecorder(simulation,template.map,"recovery-check");
                var preparing=new ReplayArchive { replay=recorder.Data,apiUrl="https://replay-fixture.invalid",ownerId="recovery-owner",ownerProvider="wallet" };
                service.EditorRequestOverride=(method,path,body,done)=>Response(()=>
                {
                    Check(simulation.Tick==0,"Gameplay advanced before the server seed arrived");
                    if(path=="/api/player"){playerRequests++;done(200,Player(),null);return;}
                    Check(path=="/api/replay-sessions","Unexpected preparation request");sessionRequests++;
                    if(sessionRequests<=4){done(503,null,"Session service temporarily unavailable");return;}
                    var session=(JObject)body.DeepClone();session["id"]="urs-prepared-fixture";session["seed"]="742";
                    done(201,new JObject { ["ok"]=true,["session"]=session },null);
                });
                Drain(service.Prepare(recorder,preparing,()=>true,()=>prepared=true));
                Check(prepared&&sessionRequests==5&&playerRequests==2,"Wallet preparation gave up or skipped its final identity check");
                Check(preparing.sessionId=="urs-prepared-fixture"&&recorder.Data.seed==742&&simulation.Tick==0,"Prepared session seed was not installed before tick one");
                Check(preparing.ownerProvider=="wallet","Recording identity provider was not retained");
                recorder.Finish("aborted");

                bool active=true,cancelled=false;int switchedRequests=0;
                simulation=new BattleSimulation(ReplayJson.Copy(template.map),template.levelNumber);
                recorder=new ReplayRecorder(simulation,template.map,"recovery-check");
                var switched=new ReplayArchive { replay=recorder.Data,apiUrl="https://replay-fixture.invalid",ownerId="recovery-owner",ownerProvider="wallet" };
                service.EditorRequestOverride=(method,path,body,done)=>Response(()=>
                {switchedRequests++;Check(path=="/api/player","Wallet mismatch created a server session");done(200,Player("other-owner"),null);});
                Drain(service.Prepare(recorder,switched,()=>active,()=>cancelled=true),_=>
                {
                    Check(!cancelled&&simulation.Tick==0&&switched.sessionId==null&&switched.ownerId=="recovery-owner","Wallet mismatch silently resumed or rebound the charged match");
                    Check(switched.uploadStatus.Contains("Return to the menu"),"Wallet mismatch has no recovery action");active=false;
                });
                Check(cancelled&&switchedRequests==1,"Cancelling paused preparation did not finish cleanly");recorder.Finish("aborted");

                int replayRequests=0,matchRequests=0;
                var archive=Archive(template);BattleReplayStore.Save(archive);
                service.EditorRequestOverride=(method,path,body,done)=>Response(()=>
                {
                    if(path=="/api/player"){done(200,Player(),null);return;}
                    if(path=="/api/replays/unity")
                    {replayRequests++;done(201,new JObject { ["ok"]=true,["item"]=new JObject { ["id"]="replay-fixture",["verificationStatus"]="pending" } },null);return;}
                    Check(path=="/api/matches/submit","Retry attempted to create a new session for an existing recording");matchRequests++;
                    Check(BattleReplayStore.Load(archive.replay.id).replayId=="replay-fixture","Accepted replay ID was not durable before score submission");
                    if(matchRequests==1){done(503,null,"Score service temporarily unavailable");return;}
                    done(200,new JObject { ["ok"]=true,["result"]=new JObject { ["id"]="match-fixture",["validationStatus"]="pending" } },null);
                });
                service.Upload(archive);
                var saved=BattleReplayStore.Load(archive.replay.id);
                Check(saved.replayId=="replay-fixture"&&saved.matchId==null&&saved.HasPendingUpload,"Failed score submission was lost");
                Check(saved.retryAfterUtcTicks>DateTime.UtcNow.Ticks&&!saved.uploadStopped&&saved.uploadStatus.Contains("Score service temporarily unavailable"),"Recoverable score failure was not retained with its cause and backoff");
                service.RetryPending();Check(matchRequests==1,"Retry ignored the persisted backoff");
                saved.retryAfterUtcTicks=0;BattleReplayStore.Save(saved);service.RetryPending();
                saved=BattleReplayStore.Load(archive.replay.id);
                Check(saved.matchId=="match-fixture"&&!saved.HasPendingUpload&&replayRequests==1&&matchRequests==2&&changed==1,"Recovery did not reuse the uploaded replay exactly once");
                Check(saved.retryAfterUtcTicks==0&&saved.uploadAttempts==0,"Successful delivery did not clear retry state");

                int deniedRequests=0;
                var other=Archive(template);other.replay.id=Guid.NewGuid().ToString("N");
                service.EditorRequestOverride=(method,path,body,done)=>Response(()=>
                {deniedRequests++;Check(path=="/api/player","Recording was uploaded using another player's session");done(200,Player("other-owner"),null);});
                service.Upload(other);Check(deniedRequests==1&&other.replayId==null&&other.uploadStatus.Contains("recording account"),"Wrong-account recording was not safely retained");

                int expiredRequests=0;
                var expired=Archive(template);expired.replay.id=Guid.NewGuid().ToString("N");
                service.EditorRequestOverride=(method,path,body,done)=>Response(()=>
                {
                    if(path=="/api/player"){done(200,Player(),null);return;}
                    Check(path=="/api/replays/unity","Expired recording was assigned a new session");expiredRequests++;done(410,null,"Replay session expired");
                });
                service.Upload(expired);expired.retryAfterUtcTicks=0;service.Upload(expired);
                Check(expiredRequests==1&&expired.uploadStopped&&expired.HasPendingUpload&&expired.uploadStatus.Contains("HTTP 410"),"Expired evidence was not retained without resubmission");

                var local=Archive(template);local.sessionId=null;local.replay.id=Guid.NewGuid().ToString("N");
                service.EditorRequestOverride=(method,path,body,done)=>throw new Exception("Offline recording contacted the server");
                service.Upload(local);Check(local.replayId==null,"Offline capture received a retroactive server session");

                var pending=Archive(template);pending.replay.id=Guid.NewGuid().ToString("N");pending.replayId="uploaded-score-pending";
                string pendingPath=BattleReplayStore.Save(pending);File.SetLastWriteTimeUtc(pendingPath,new DateTime(2000,1,1));
                for(int i=0;i<22;i++)
                {
                    var complete=Archive(template);complete.replay.id=Guid.NewGuid().ToString("N");complete.replayId="uploaded-"+i;complete.matchId="submitted-"+i;
                    BattleReplayStore.Save(complete);
                }
                Check(BattleReplayStore.Load(pending.replay.id)?.HasPendingUpload==true,"Pruning deleted a replay with an outstanding score submission");
                Check(BattleReplayStore.Load(expired.replay.id)?.uploadStopped==true,"Pruning deleted rejected local evidence");
                Debug.Log("Replay recovery checks passed: pre-start transient recovery, durable score retry, backoff, account isolation, expired/offline capture protection and pending-score retention.");
            }
            finally
            {
                BattleReplayService.ResultsChanged-=onChanged;
                UnityEngine.Object.DestroyImmediate(host);BattleReplayStore.EditorDirectoryOverride=previousDirectory;
                // This unique directory was created above for this fixture alone.
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
        }
    }
}
#endif
