using System;
using System.Linq;
using Fusion;
using UnityEngine;

namespace BattleCities.Multiplayer
{
    public sealed partial class BattleNetworkMatch
    {
        const int CheckpointCapacity=65536;
        
        [Networked] public int CheckpointLength {get;set;}
        [Networked,Capacity(4)] public NetworkArray<NetworkString<_64>> PlayerTokens => default;
        private readonly float[] disconnectedAt={-1,-1,-1,-1};
        private readonly bool[] inputInitialized=new bool[4];
        private float recoveryReadyAt, nextSnapshotAt;
        private bool snapshotPending;
        public bool CanReconnect(string token)=>!string.IsNullOrEmpty(token)&&Enumerable.Range(0,4).Any(i=>PlayerTokens[i].ToString()==token);

        private void UpdateMembership()
        {
            var active=Runner.ActivePlayers.ToArray();
            for(int slot=0;slot<4;slot++)
            {
                if(Players[slot]==PlayerRef.None||active.Contains(Players[slot]))continue;
                Simulation.DropCarriedFlag(slot);Players.Set(slot,PlayerRef.None);disconnectedAt[slot]=Time.realtimeSinceStartup;inputInitialized[slot]=false;
            }
            foreach(var player in active)
            {
                if(Enumerable.Range(0,4).Any(i=>Players[i]==player))continue;
                var token=BattleSession.TokenKey(Runner.GetPlayerConnectionToken(player));
                // Fusion may not expose a token for the local host.
                if(string.IsNullOrEmpty(token)&&player==Runner.LocalPlayer)token=BattleSession.Instance.LocalTokenKey;
                int slot=Enumerable.Range(0,4).Where(i=>PlayerTokens[i].ToString()==token).DefaultIfEmpty(-1).First();
                if(slot<0&&!Simulation.MatchStarted)slot=Enumerable.Range(0,RequiredPlayers).Where(i=>Players[i]==PlayerRef.None&&PlayerTokens[i].ToString().Length==0).DefaultIfEmpty(-1).First();
                if(slot<0||string.IsNullOrEmpty(token)){Runner.Disconnect(player);continue;}
                PlayerTokens.Set(slot,token);Players.Set(slot,player);disconnectedAt[slot]=-1;
                inputInitialized[slot]=false;chargeSeconds[slot]=0;
            }
            for(int slot=0;slot<4;slot++)
            {
                if(disconnectedAt[slot]<0||Time.realtimeSinceStartup-disconnectedAt[slot]<30)continue;
                Simulation.SetParticipant(slot,false);PlayerTokens.Set(slot,default);disconnectedAt[slot]=-1;
            }
        }

        private async void UpdateRecoverySnapshot()
        {
            if(BattleDedicatedServer.Requested||snapshotPending||Time.realtimeSinceStartup<nextSnapshotAt)return;
            snapshotPending=true;nextSnapshotAt=Time.realtimeSinceStartup+3;
            try
            {
                var data=Simulation.SaveNetworkCheckpoint();
                if(data.Length>CheckpointCapacity)throw new InvalidOperationException("Match exceeds host migration checkpoint capacity.");
                WriteCheckpoint(data);CheckpointLength=data.Length;
                bool saved=await Runner.PushHostMigrationSnapshot();
                if(!saved)Debug.LogWarning("Host migration snapshot was not accepted; retrying in three seconds.");
            }
            catch(Exception e){Debug.LogWarning("Host migration snapshot failed: "+e.Message);}
            finally{snapshotPending=false;}
        }

        private void RestoreMigration()
        {
            if(CheckpointLength<=0||CheckpointLength>CheckpointCapacity)throw new InvalidOperationException("No valid recovery checkpoint.");
            Game.StopRecordingForRecovery();
            Simulation.RestoreNetworkCheckpoint(ReadCheckpoint());
            for(int i=0;i<4;i++){Players.Set(i,PlayerRef.None);disconnectedAt[i]=Time.realtimeSinceStartup;inputInitialized[i]=false;}
            recoveryReadyAt=Time.realtimeSinceStartup+5;
            EventSequence=0;Publish();
            Runner.SessionInfo.IsOpen=true;Runner.SessionInfo.IsVisible=!Simulation.MatchStarted;
        }
    }
}
