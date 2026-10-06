using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuApiClient
    {
        public PlayerSnapshot LastPlayer {get;private set;}
        public bool IsSigningOut {get;private set;}
        void PublishPlayer(PlayerSnapshot value){LastPlayer=value;PlayerLoaded?.Invoke(value);}
        public void SignOut(Action<bool,string> completed)
        {
            if(IsSigningOut||!isActiveAndEnabled)return;
            CancelWalletLogin(false);
            if(menuDataRoutine!=null){StopCoroutine(menuDataRoutine);menuDataRoutine=null;}
            if(refreshRoutine!=null){StopCoroutine(refreshRoutine);refreshRoutine=null;}
            requestInFlight=true;IsSigningOut=true;
            StartCoroutine(SignOutRoutine(completed));
        }
        IEnumerator SignOutRoutine(Action<bool,string> completed)
        {
            bool ok=IsLocalGuest;
            if(!ok)
                yield return Request("DELETE","/api/session",null,(code,body,error)=>ok=code>=200&&code<300&&string.IsNullOrEmpty(error)&&body?["authenticated"]?.Type==JTokenType.Boolean&&(bool)body["authenticated"]==false);
            IsSigningOut=false;requestInFlight=false;
            if(!ok)
            {
                if(automaticRefresh)refreshRoutine=StartCoroutine(RefreshLoop());
                completed?.Invoke(false,"Could not log out. Check your connection and retry.");yield break;
            }
            sessionCookie=null;sessionCookieOrigin=null;CurrentGuestId=null;currentGuestName=null;
            IsAuthenticated=false;IsWalletAuthenticated=false;IsLocalGuest=false;LastPlayer=null;LastRankings=null;LastRound=null;
            foreach(var key in new[]{"battlecities.guestId","battlecities.guestName","battlecities.playerName"})PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString("battlecities.loginMode","signedout");PlayerPrefs.Save();
            completed?.Invoke(true,null);
        }
    }
}
