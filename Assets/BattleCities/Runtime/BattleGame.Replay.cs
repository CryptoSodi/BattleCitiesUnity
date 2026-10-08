using System;
using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private ReplayRecorder replayRecorder;
        private ReplayPlayer replayPlayer;
        private ReplayArchive replayArchive;
        private MapData replayMap;
        private readonly Queue<ReplayEvent> replayPending=new Queue<ReplayEvent>();
        private bool replayPreparing,loadingReplay,replaySaved;
        private float replaySpeed=1;
        private string battleFuelRequestId;
        public bool IsReplaying=>replayPlayer!=null||loadingReplay;
        public bool ReplayReady=>!replayPreparing;
        private string replayStatus="";
        public string ReplayStatus {get=>(replayPreparing||replaySaved&&replayArchive!=null&&!string.IsNullOrEmpty(replayArchive.sessionId))?(replayArchive?.uploadStatus??replayStatus):replayStatus;private set=>replayStatus=value;}
        public ReplayPlayer ReplayPlayback=>replayPlayer;
        public float ReplaySpeed {get=>replaySpeed;set=>replaySpeed=Mathf.Clamp(value,.25f,4);}
        private void BeginRecording(MapData map)
        {
            replayMap=ReplayJson.Copy(map);replayPending.Clear();replaySaved=false;
            if(tvReplay||loadingReplay||IsOnline||LevelEditor.LevelEditorPlaytest.IsActive)return;
            StartReplayRecorder();
        }
        private void StartReplayRecorder()
        {
            replaySaved=false;
            battleFuelRequestId=BattlePreparation.Ready?BattlePreparation.FuelRequestId:null;
            replayRecorder=new ReplayRecorder(Simulation,replayMap,Application.version);
            replayArchive=new ReplayArchive {replay=replayRecorder.Data,apiUrl=BattlePreparation.Ready?BattlePreparation.ApiUrl:null,
                ownerId=BattlePreparation.Ready?BattlePreparation.OwnerId:null,ownerProvider=BattlePreparation.Ready?BattlePreparation.OwnerProvider:null};
            ReplayStatus="Recording";
            if(!string.IsNullOrEmpty(replayArchive.apiUrl))
            {
                replayPreparing=true;
                var notice=GetComponent<BattleConnectionNotice>();if(!notice)notice=gameObject.AddComponent<BattleConnectionNotice>();notice.Initialize(this);
                var recorder=replayRecorder;
                BattleReplayService.Instance.StartCoroutine(BattleReplayService.Instance.Prepare(recorder,replayArchive,
                    ()=>this&&replayRecorder==recorder&&!recorder.Finished,
                    ()=>{if(this&&replayRecorder==recorder){replayPreparing=false;ReplayStatus=replayArchive.uploadStatus;}}));
            }
        }
        public void StartOnlineRecording()
        { if(NetworkMatch&&NetworkMatch.Object.HasStateAuthority)StartReplayRecorder(); }
        private void QueueReplayEvent(ReplayEvent e)
        {if(!IsReplaying&&Simulation!=null&&!Simulation.Won&&!Simulation.Lost)replayPending.Enqueue(e);}
        private void ApplyPendingReplayEvents()
        {
            while(replayPending.Count>0)
            {
                var e=replayPending.Dequeue();replayRecorder?.RecordEvent(e);
                // The claim token is intentionally outside the replay file; playback cannot redeem it.
                if(e.kind=="pickup"&&pendingReplayClaims.TryGetValue(e,out var claim)){Simulation.SpawnPickup(e.value,claim);pendingReplayClaims.Remove(e);}
                else ReplayPlayer.ApplyEvent(Simulation,e);
            }
        }
        private readonly Dictionary<ReplayEvent,string> pendingReplayClaims=new Dictionary<ReplayEvent,string>();
        private void StepRecorded(Command command,IReadOnlyDictionary<int,Command> online=null)
        {
            if(Simulation.Won||Simulation.Lost||!ReplayReady)return;
            if(Simulation.IsMultiplayer&&!Simulation.MatchStarted)return;
            if(!string.IsNullOrEmpty(battleFuelRequestId))
            {BattleFuelReceipt.MarkStarted(replayArchive?.apiUrl,replayArchive?.ownerId,battleFuelRequestId);battleFuelRequestId=null;}
            ApplyPendingReplayEvents();
            replayRecorder?.BeforeStep(command,online);
            if(online!=null)Simulation.StepMultiplayer(online);else Simulation.Step(command);
            replayRecorder?.AfterStep();
            if(replayRecorder!=null&&replayRecorder.Finished)SaveRecording();
        }
        public void StepOnlineRecorded(IReadOnlyDictionary<int,Command> commands)=>StepRecorded(default,commands);
        private void FinishRecording()
        {
            replayPreparing=false;replayPending.Clear();pendingReplayClaims.Clear();
            if(replayRecorder==null)return;
            replayRecorder.Finish("aborted");SaveRecording();replayRecorder=null;
        }
        private void SaveRecording()
        {
            if(replaySaved||replayRecorder==null||replayRecorder.Data.durationTicks==0)return;
            replaySaved=true;
            try
            {
                BattleScoreCache.Record(replayArchive);
                BattleReplayStore.Save(replayArchive);ReplayStatus=replayRecorder.Data.completion=="truncated"?"Recording limit reached; partial replay saved":"Replay saved";
                if(!string.IsNullOrEmpty(replayArchive.sessionId))BattleReplayService.Instance.Upload(replayArchive);
            }
            catch(Exception e){ReplayStatus="Replay could not be saved: "+e.Message;Debug.LogWarning(ReplayStatus);}
        }
        public void PlayReplay(BattleReplay data)
        {
            if(IsOnline)throw new InvalidOperationException("Leave the online match before watching a replay.");
            ReplayJson.Validate(data);FinishRecording();replayPlayer=null;loadingReplay=true;
            try
            {
                replayLoadingData=data;LoadStageMap(data.levelNumber,ReplayJson.Copy(data.map));
                replayPlayer=new ReplayPlayer(data,Simulation);ReplayStatus=replayPlayer.Error??"Replay";
                paused=false;showDebug=false;replaySpeed=1;accumulator=0;ResetPrimaryFire();
                if(touchControls)touchControls.enabled=false;
            }
            finally {loadingReplay=false;replayLoadingData=null;}
        }
        private BattleReplay replayLoadingData;
        public void RestartReplay(){if(replayPlayer!=null)PlayReplay(replayPlayer.Data);}
        private void TickReplay(float dt)
        {
            if(paused||replayPlayer==null||replayPlayer.Complete||replayPlayer.Error!=null)return;
            accumulator+=dt*replaySpeed;
            int budget=32;
            while(accumulator>=BattleSimulation.StepSeconds&&budget-->0&&!replayPlayer.Complete&&replayPlayer.Error==null)
            {replayPlayer.Step();accumulator-=BattleSimulation.StepSeconds;}
            ReplayStatus=replayPlayer.Error??(replayPlayer.Complete?"Replay complete — state matched":"Replay");
        }
    }
}
