using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using BattleCities.Core;
using BattleCities.UI;
using Fusion;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BattleCities.Multiplayer
{
    public sealed partial class BattleNetworkMatch
    {
        [Networked,Capacity(4)] public NetworkArray<NetworkString<_64>> AppliedPowerup => default;
        [Networked,Capacity(256)] public NetworkArray<ulong> UsedPowerups => default;
        [Networked] public int UsedPowerupCount {get;set;}
        private readonly HashSet<string> verifyingPowerups=new HashSet<string>();
        private readonly Queue<(int slot,string type,string receipt,string matchId)> verifiedPowerups=new Queue<(int,string,string,string)>();
        private static ulong ReceiptHash(string receipt)
        {using(var sha=SHA256.Create())return BitConverter.ToUInt64(sha.ComputeHash(Encoding.UTF8.GetBytes(receipt)),0);}
        public string LocalPowerupMatchId => Runner&&Runner.SessionInfo!=null&&LocalSlot>=0
            ? Runner.SessionInfo.Name+":"+Round+":"+LocalSlot : null;
        public bool PowerupAcknowledged(string receipt)=>LocalSlot>=0&&AppliedPowerup[LocalSlot].ToString()==receipt;
        public void SendPowerup(EconomyClient.Consumption receipt)
        {
            if(receipt!=null&&receipt.MatchId==LocalPowerupMatchId)
                RPC_RequestPowerup(receipt.PlayerId,receipt.Item,receipt.RequestId);
        }
        [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
        private void RPC_RequestPowerup(string playerId,string item,string receipt,RpcInfo info=default)
        {
            if(!Object.HasStateAuthority||Simulation==null||!Simulation.MatchStarted||Simulation.Won||Simulation.Lost)return;
            var source=info.Source==PlayerRef.None?Runner.LocalPlayer:info.Source;
            int slot=Enumerable.Range(0,4).Where(i=>Players[i]==source).DefaultIfEmpty(-1).First();
            if(slot<0||string.IsNullOrEmpty(receipt)||AppliedPowerup[slot].ToString()==receipt)return;
            ulong hash=ReceiptHash(receipt);
            if(Enumerable.Range(0,UsedPowerupCount).Any(i=>UsedPowerups[i]==hash)){AppliedPowerup.Set(slot,receipt);return;}
            if(UsedPowerupCount>=UsedPowerups.Length||!verifyingPowerups.Add(receipt))return;
            StartCoroutine(VerifyPowerup(slot,source,playerId,item,receipt,Runner.SessionInfo.Name+":"+Round+":"+slot));
        }
        private IEnumerator VerifyPowerup(int slot,PlayerRef source,string playerId,string item,string receipt,string matchId)
        {
            try
            {
                string url=(BattlePreparation.ApiUrl??MainMenuApiClient.DefaultApiBaseUrl).TrimEnd('/')+"/api/economy/powerups/redeem";
                using(var request=new UnityWebRequest(url,"POST"))
                {
                    request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(new JObject{{"playerId",playerId},{"itemId",item},{"requestId",receipt},{"matchId",matchId}}.ToString()));
                    request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=10;
                    yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success||Players[slot]!=source||matchId!=Runner.SessionInfo.Name+":"+Round+":"+slot)yield break;
                    var result=JObject.Parse(request.downloadHandler.text);string type=(string)result["powerupType"];
                    if((bool?)result["ok"]==true&&Array.IndexOf(ReplayJson.Powerups,type)>=0)verifiedPowerups.Enqueue((slot,type,receipt,matchId));
                }
            }
            finally{verifyingPowerups.Remove(receipt);}
        }
        private void ApplyVerifiedPowerups()
        {
            int ready=verifiedPowerups.Count;
            while(ready-->0)
            {
                var result=verifiedPowerups.Dequeue();
                ulong hash=ReceiptHash(result.receipt);
                if(result.matchId!=Runner.SessionInfo.Name+":"+Round+":"+result.slot||!Simulation.MatchStarted||Simulation.Won||Simulation.Lost||UsedPowerupCount>=UsedPowerups.Length||Enumerable.Range(0,UsedPowerupCount).Any(i=>UsedPowerups[i]==hash))continue;
                if(result.type!="life"&&!Simulation.Tanks.Any(t=>t.Player&&t.Alive&&t.Slot==result.slot))
                {verifiedPowerups.Enqueue(result);continue;}
                Game.EnqueueOnlinePowerup(result.slot,result.type);
                UsedPowerups.Set(UsedPowerupCount++,hash);
                AppliedPowerup.Set(result.slot,result.receipt);
            }
        }
    }
}
