using System;
using System.Collections;
using System.Collections.Generic;
using BattleCities.Core;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace BattleCities
{
    // One immutable, authenticated devnet session per simulation. Only the match authority calls this.
    public sealed class MagicBlockDrops:MonoBehaviour
    {
        readonly Queue<Action<ReplayEvent>> pending=new Queue<Action<ReplayEvent>>();
        BattleSimulation simulation;MainMenuApiClient api;Coroutine worker;string token;int index;
        long expires;
        public bool Ready {get;private set;}
        public string Status {get;private set;}="Preparing power-up drops...";
#if UNITY_EDITOR
        public Func<string,string,JObject,Action<long,JObject,string>,IEnumerator> EditorRequestOverride;
        public Action<IEnumerator> EditorRoutineOverride;
#endif
        Coroutine Run(IEnumerator routine)
        {
#if UNITY_EDITOR
            if(EditorRoutineOverride!=null){EditorRoutineOverride(routine);return null;}
#endif
            return StartCoroutine(routine);
        }
        IEnumerator Request(string path,JObject body,Action<long,JObject,string> complete)
        {
#if UNITY_EDITOR
            if(EditorRequestOverride!=null){yield return EditorRequestOverride("POST",path,body,complete);yield break;}
#endif
            yield return api.Request("POST",path,body,complete);
        }
        public void Prepare(BattleSimulation current)
        {
            if(worker!=null)StopCoroutine(worker);
            pending.Clear();simulation=current;token=null;index=0;expires=0;Ready=false;enabled=true;
            Status="Preparing power-up drops...";
            worker=Run(PrepareSession(current));
        }
        // Refresh only before combat, including after a long multiplayer lobby wait.
        public void RefreshBeforeStart()
        {
            if(Ready&&enabled&&expires<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+60000)
                Prepare(simulation);
        }
        IEnumerator PrepareSession(BattleSimulation current)
        {
            // Scene activation and first-frame shader work can block WebGL callbacks.
            // Let those frames finish before starting the HTTP timeout.
            yield return null;
            yield return null;
            if(simulation!=current)yield break;
            EnsureTransport();
            long status=0;JObject reply=null;
            for(int attempt=0;attempt<3;attempt++)
            {
                yield return Request("/api/magicblock/session",new JObject{{"width",(int)current.Width},{"height",(int)current.Height}},(c,j,e)=>{status=c;reply=j;});
                if(simulation!=current)yield break;
                if(status!=0&&status<500)break;
                if(attempt<2)yield return new WaitForSecondsRealtime(1);
            }
            var sessionToken=(string)reply?["token"];
            var expiry=(long?)reply?["expires"]??0;
            if(status==201&&!string.IsNullOrEmpty(sessionToken)&&expiry>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+60000)
            {
                token=sessionToken;expires=expiry;Ready=true;Status="Power-up drops ready";
            }
            else
            {
                Status=status==401?"Your session expired. Return to login and sign in again.":"Could not prepare power-up drops. Return to the menu and try again.";
                Debug.LogWarning("MagicBlock pre-match setup HTTP "+status+": "+Status);
            }
            worker=null;
        }
        public void Enqueue(BattleSimulation current,Action<ReplayEvent> complete)
        {
            // Setup belongs to match loading. Never authenticate or create a session on a drop.
            if(!enabled||!Ready||simulation!=current||string.IsNullOrEmpty(token))return;
            if(pending.Count>=64){Status="MagicBlock drop queue full";return;}
            pending.Enqueue(complete);if(worker==null)worker=Run(Process());
        }
        public void StopForRecovery(BattleSimulation current)
        {
            if(worker!=null)StopCoroutine(worker);
            worker=null;pending.Clear();simulation=current;enabled=false;Ready=true;token=null;
            Status="MagicBlock drops paused after host recovery; start a new round.";
        }
        void EnsureTransport()
        {
#if UNITY_EDITOR
            if(EditorRequestOverride!=null)return;
#endif
            if(!api)
            {
                var host=new GameObject("MagicBlock transport");host.SetActive(false);host.transform.SetParent(transform);
                api=host.AddComponent<MainMenuApiClient>();api.ConfigureAutomaticRefresh(false);api.ConfigureGuestFallback(false);
                api.Configure(MainMenuApiClient.TestApiBaseUrl);host.SetActive(true);
            }
        }
        IEnumerator Process()
        {
            var current=simulation;
            while(pending.Count>0&&!simulation.Won&&!simulation.Lost)
            {
                var callback=pending.Dequeue();bool resolved=false;
                // Poll the same onchain PDA. A delayed response never causes a new random roll.
                for(int attempt=0;attempt<60&&!simulation.Won&&!simulation.Lost;attempt++)
                {
                    long status=0;JObject reply=null;
                    yield return Request("/api/magicblock/drop",new JObject{{"token",token},{"index",index}},(c,j,e)=>{status=c;reply=j;});
                    if(simulation!=current||current.Won||current.Lost)yield break;
                    if(status==200&&(string)reply?["status"]=="ready")
                    {
                        string value=(string)reply["value"];
                        if(!MagicDropEvent.Valid(value,simulation.Width,simulation.Height))break;
                        callback(new ReplayEvent{kind="magicpickup",value=value});resolved=true;Status="MagicBlock drop verified";break;
                    }
                    Status="MagicBlock drop pending";
                    if(status>=400&&status<500&&status!=429)break;
                    yield return new WaitForSecondsRealtime(2);
                }
                if(!resolved)
                {Status="MagicBlock drop failed; no random fallback";Debug.LogWarning(Status);pending.Clear();enabled=false;break;}
                index++;
            }
            worker=null;
        }
    }
}
