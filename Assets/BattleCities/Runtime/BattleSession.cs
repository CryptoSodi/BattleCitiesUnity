using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BattleCities.Core;
using BattleCities.UI;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Multiplayer
{
    public sealed class BattleSession : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const string AppId="dedb8ea5-35ab-42a2-a8af-9d5461c4d72c";
        public const string ProtocolVersion="battlecities-2";
        public static BattleSession Instance {get;private set;}
        public BattleMode SelectedMode=BattleMode.Coop;
        public bool ModeLockedByLaunchFlag=>BattleLaunchOptions.HasModeFlag;
        public int SelectedMap=1;
        public string Region="asia",RoomCode="";
        public string Status {get;private set;}="Create a room or enter a friend's room code.";
        public bool Busy {get;private set;}
        public bool QuickMatching {get;private set;}
        public bool Online=>Runner&&Runner.IsRunning;
        public bool IsHost=>Online&&Runner.IsServer;
        public NetworkRunner Runner {get;private set;}
        public BattleNetworkMatch Match {get;private set;}
        public BattleLobbyUI Lobby {get;private set;}
        private CancellationTokenSource connection;
        private bool leaving;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Func<BattleNetworkInput> TestInput;
#endif
        public static readonly string[] RegionCodes={"asia","in","eu","us","usw","au","jp","kr","sa"};

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()=>Instance=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if(!Instance)new GameObject("Battle multiplayer").AddComponent<BattleSession>();
        }
        private void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);Application.runInBackground=true;
            SelectedMode=BattleLaunchOptions.Mode;
            Lobby=gameObject.AddComponent<BattleLobbyUI>();
        }
        public void Attach(BattleNetworkMatch match){Match=match;Status="Connected. Waiting for the host to start.";}

        public Task Connect(bool host,string room)=>ConnectInternal(host,room,false);
        public Task QuickMatch()=>ConnectInternal(false,null,true);

        private async Task ConnectInternal(bool host,string room,bool quick)
        {
            if(Busy||Online)return;
            if(BattleLaunchOptions.Error!=null){Status=BattleLaunchOptions.Error;Lobby.Show();return;}
            if(Application.platform==RuntimePlatform.WebGLPlayer)
            {Status="This host-mode build supports native PC and Android. Browser multiplayer requires a separate build.";return;}
            Busy=true;leaving=false;QuickMatching=quick;
            Status=quick?"Finding a "+(SelectedMode==BattleMode.Versus?"PvP":"co-op")+" match...":"Connecting to Photon...";
            try
            {
                string code=null;
                if(!quick)
                {
                    code=(room??"").Trim().ToUpperInvariant();
                    if(host)code=Region.ToUpperInvariant()+"-"+Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant();
                    int dash=code.IndexOf('-');
                    if(dash<1||!RegionCodes.Contains(code.Substring(0,dash).ToLowerInvariant())||code.Length!=dash+7||
                       !code.Substring(dash+1).All(c=>char.IsLetterOrDigit(c)))throw new ArgumentException("Enter the full room code, for example ASIA-A1B2C3.");
                    Region=code.Substring(0,dash).ToLowerInvariant();RoomCode=code;
                }
                else RoomCode="";
                if((host||quick)&&ModeLockedByLaunchFlag)SelectedMode=BattleLaunchOptions.Mode;
                if((host||quick)&&(SelectedMap<1||SelectedMap>35))throw new ArgumentOutOfRangeException(nameof(SelectedMap),"Select a map from 01 to 35.");
                connection=new CancellationTokenSource(TimeSpan.FromSeconds(35));
                // Load before connecting so an incoming network prefab cannot be destroyed by a scene switch.
                if(SceneManager.GetActiveScene().name!="BattleCity")
                {
                    if(!Application.CanStreamedLevelBeLoaded("BattleCity"))throw new InvalidOperationException("BattleCity is missing from build scenes.");
                    var load=SceneManager.LoadSceneAsync("BattleCity");while(!load.isDone)await Task.Yield();
                }
                var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();if(game)game.Paused=true;
                connection.Token.ThrowIfCancellationRequested();
                var go=new GameObject("Fusion runner");go.transform.SetParent(transform);
                Runner=go.AddComponent<NetworkRunner>();Runner.ProvideInput=true;Runner.AddCallbacks(this);
                var settings=PhotonAppSettings.Global.AppSettings.GetCopy();
                settings.AppIdFusion=AppId;settings.AppVersion=ProtocolVersion;settings.FixedRegion=Region;
                var guestId=MainMenuApiClient.CurrentGuestId;
                var result=await Runner.StartGame(new StartGameArgs{GameMode=quick?GameMode.AutoHostOrClient:host?GameMode.Host:GameMode.Client,
                    SessionName=code,SessionNameGenerator=quick?(()=>Region.ToUpperInvariant()+"-"+Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant()):null,
                    PlayerCount=4,IsVisible=quick,IsOpen=true,EnableClientSessionCreation=quick,
                    MatchmakingMode=Photon.Realtime.MatchmakingMode.FillRoom,
                    AuthValues=string.IsNullOrEmpty(guestId)?null:new AuthenticationValues(guestId),
                    CustomPhotonAppSettings=settings,StartGameCancellationToken=connection.Token,
                    SessionProperties=quick?new Dictionary<string,SessionProperty>{{"mode",(int)SelectedMode}}:
                        host?new Dictionary<string,SessionProperty>{{"mode",(int)SelectedMode},{"map",SelectedMap}}:null});
                if(!result.Ok)throw new InvalidOperationException(result.ShutdownReason.ToString());
                if(leaving||!Runner||!Runner.IsRunning)return;
                if(quick)RoomCode=Runner.SessionInfo.Name;
                if(Runner.IsServer)
                {
                    var prefab=Resources.Load<NetworkObject>("Multiplayer/BattleNetworkMatch");
                    if(!prefab)throw new InvalidOperationException("The multiplayer prefab has not been built. Run Battle Cities/Multiplayer/Configure Fusion.");
                    Runner.Spawn(prefab);
                }
                Status=quick?"Waiting for players. Match starts automatically.":"Connected. Share "+RoomCode+" with your friends.";
                Lobby.Show();
            }
            catch(Exception e)
            {
                Status=FriendlyError(e.Message);QuickMatching=false;await ShutdownRunner();Lobby.Show();
            }
            finally{Busy=false;connection?.Dispose();connection=null;}
        }

        public async Task Leave()
        {
            if(leaving)return;leaving=true;connection?.Cancel();Busy=true;
            await ShutdownRunner();
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();if(game)game.EndOnline();
            Status="Disconnected. Create or join another room.";QuickMatching=false;Busy=false;leaving=false;Lobby.Show();
        }
        private async Task ShutdownRunner()
        {
            var runner=Runner;Runner=null;Match=null;
            if(runner){runner.RemoveCallbacks(this);await runner.Shutdown();if(runner)Destroy(runner.gameObject);}
        }
        private static string FriendlyError(string reason)
        {
            if(reason.Contains("GameNotFound"))return "Room not found. Check the full code and ask the host to keep the lobby open.";
            if(reason.Contains("GameIsFull"))return "That room already has four players.";
            if(reason.Contains("GameClosed"))return "That match has started. Ask the host to open a rematch lobby.";
            return "Could not connect: "+reason+". Check your connection and try again.";
        }
        public void OnInput(NetworkRunner runner,NetworkInput input)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(TestInput!=null){input.Set(TestInput());return;}
#endif
            input.Set(Match&&Match.Game?Match.Game.ReadOnlineInput():new BattleNetworkInput{Move=-1,Aim=-1,Secondary=1});
        }
        public void OnInputMissing(NetworkRunner runner,PlayerRef player,NetworkInput input)
        { }
        public void OnPlayerJoined(NetworkRunner runner,PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner,PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner,NetworkRunnerCallbackArgs.ConnectRequest request,byte[] token)
        {if(Match&&Match.Started)request.Refuse();else request.Accept();}
        public void OnConnectFailed(NetworkRunner runner,NetAddress address,NetConnectFailedReason reason){Status=FriendlyError(reason.ToString());}
        public void OnShutdown(NetworkRunner runner,ShutdownReason reason)
        {
            if(leaving||runner!=Runner)return;
            Runner=null;Match=null;Busy=false;QuickMatching=false;Status="Session ended: "+reason+". Create or join a new room.";
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();if(game)game.EndOnline();
            Lobby.Show();if(runner)Destroy(runner.gameObject);
        }
        public void OnDisconnectedFromServer(NetworkRunner runner,NetDisconnectReason reason){Status="Connection lost: "+reason;}
        public void OnHostMigration(NetworkRunner runner,HostMigrationToken token){Status="The host left. Please create a new room.";_ = Leave();}
        public void OnSessionListUpdated(NetworkRunner runner,List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner,Dictionary<string,object> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner,NetworkObject obj,PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner,NetworkObject obj,PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner,PlayerRef player,ReliableKey key,ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner,PlayerRef player,ReliableKey key,float progress) { }
        public void OnUserSimulationMessage(NetworkRunner runner,SimulationMessagePtr message) { }
        private void OnDestroy(){connection?.Cancel();connection?.Dispose();if(Instance==this)Instance=null;}
    }
}
