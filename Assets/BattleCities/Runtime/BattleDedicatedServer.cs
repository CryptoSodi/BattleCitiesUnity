using System;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities.Multiplayer
{
    // A dedicated process owns one room. An external supervisor replaces it at
    // round completion; no client is ever promoted into this trusted role.
    public sealed class BattleDedicatedServer : MonoBehaviour
    {
        public static bool Requested=>Environment.GetCommandLineArgs().Contains("--dedicated");
        public static bool DedicatedOnly=>Environment.GetCommandLineArgs().Contains("--dedicated-only");
        bool launched,started,connected;float emptySince=-1,created;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if(!Requested)return;
            var go=new GameObject("Dedicated match supervisor");DontDestroyOnLoad(go);go.AddComponent<BattleDedicatedServer>();
        }
        async void Update()
        {
            var session=BattleSession.Instance;if(!session)return;
            if(!launched)
            {
                launched=true;created=Time.realtimeSinceStartup;
                if(BattleLaunchOptions.Error!=null||BattleLaunchOptions.Mode==BattleMode.Offline)
                {Debug.LogError("Dedicated server requires --mode coop, 2v2, ctf or ctf1v1");Application.Quit(2);return;}
                string region=Environment.GetEnvironmentVariable("BATTLECITIES_REGION");
                if(!string.IsNullOrEmpty(region))session.Region=region;
                await session.QuickMatch();
                if(!session.Online){Debug.LogError(session.Status);Application.Quit(3);return;}
                connected=true;
                session.Lobby.Hide();Debug.Log("DEDICATED_READY room="+session.RoomCode+" mode="+session.SelectedMode);
            }
            // A supervisor replaces a disconnected process; it must never become a client.
            if(connected&&!session.Online&&!session.Busy){Application.Quit(3);return;}
            if(Time.realtimeSinceStartup-created>3600){Application.Quit(0);return;}
            if(!session.Online||!session.Match)return;
            var match=session.Match;
            if(match.Started)started=true;
            if(match.PlayerCount>0)emptySince=-1;else if(emptySince<0)emptySince=Time.realtimeSinceStartup;
            if((started&&emptySince>=0&&Time.realtimeSinceStartup-emptySince>45)||match.Won||match.Lost)
            {Debug.Log("DEDICATED_COMPLETE");Application.Quit(0);}
        }
    }
}
