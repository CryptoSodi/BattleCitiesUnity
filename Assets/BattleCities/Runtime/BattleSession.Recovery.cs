using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace BattleCities.Multiplayer
{
    public sealed partial class BattleSession
    {
        private readonly byte[] connectionToken=Guid.NewGuid().ToByteArray();
        private bool recovering;
        public bool Recovering=>recovering;
        public string LocalTokenKey=>TokenKey(connectionToken);
        public static string TokenKey(byte[] token)
        {
            if(token==null||token.Length==0)return "";
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(token)).Replace("-","");
        }
        private NetworkRunner NewRecoveryRunner()
        {
            var go=new GameObject("Fusion recovery runner");go.transform.SetParent(transform);
            var runner=go.AddComponent<NetworkRunner>();runner.ProvideInput=true;runner.AddCallbacks(this);Runner=runner;return runner;
        }
        private async Task Migrate(NetworkRunner previous,HostMigrationToken token)
        {
            if(recovering||leaving)return;
            recovering=true;Busy=true;Status="Restoring the match...";
            try
            {
                var game=Match?Match.Game:null;if(game){game.Paused=true;game.StopRecordingForRecovery();}
                previous.RemoveCallbacks(this);await previous.Shutdown(shutdownReason:ShutdownReason.HostMigration);
                if(previous)Destroy(previous.gameObject);Match=null;
                if(leaving)return;
                var runner=NewRecoveryRunner();
                var settings=PhotonAppSettings.Global.AppSettings.GetCopy();settings.AppIdFusion=AppId;settings.AppVersion=PhotonVersion;settings.FixedRegion=Region;
                var result=await runner.StartGame(new StartGameArgs {GameMode=token.GameMode,HostMigrationToken=token,
                    HostMigrationResume=ResumeObjects,ConnectionToken=connectionToken,CustomPhotonAppSettings=settings});
                if(!result.Ok)throw new InvalidOperationException(result.ShutdownReason.ToString());
                Status="Match restored.";
            }
            catch(Exception e){Status="Unable to restore match: "+e.Message;await ShutdownRunner();Lobby.Show();}
            finally{recovering=false;Busy=false;}
        }
        private void ResumeObjects(NetworkRunner runner)
        {
            foreach(var previous in runner.GetResumeSnapshotNetworkObjects())
                runner.Spawn(previous,onBeforeSpawned:(r,obj)=>obj.CopyStateFrom(previous));
        }
        private async Task Reconnect()
        {
            if(recovering||leaving)return;
            recovering=true;Busy=true;Status="Connection lost. Rejoining your match...";
            string room=RoomCode;
            try
            {
                if(Match&&Match.Game)Match.Game.Paused=true;
                await Task.Yield();
                await ShutdownRunner();
                for(int attempt=0;attempt<3&&!leaving;attempt++)
                {
                    var runner=NewRecoveryRunner();
                    var settings=PhotonAppSettings.Global.AppSettings.GetCopy();settings.AppIdFusion=AppId;settings.AppVersion=PhotonVersion;settings.FixedRegion=Region;
                    var result=await runner.StartGame(new StartGameArgs {GameMode=GameMode.Client,SessionName=room,
                        ConnectionToken=connectionToken,CustomPhotonAppSettings=settings,EnableClientSessionCreation=false});
                    if(result.Ok){Status="Reconnected.";return;}
                    await ShutdownRunner();await Task.Delay(1000);
                }
                Status="Could not rejoin the match. Please start another game.";Lobby.Show();
            }
            catch(Exception e){Status="Could not rejoin: "+e.Message;await ShutdownRunner();Lobby.Show();}
            finally{recovering=false;Busy=false;}
        }
    }
}
