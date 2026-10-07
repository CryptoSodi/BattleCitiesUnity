using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class PlayerProfileScreen : MonoBehaviour
    {
        public enum ViewState { Loading,Ready,Guest,SignIn,Unavailable,NotFound }
        [SerializeField] string targetId;
        [SerializeField] bool ownProfile=true;
        [SerializeField] int requestedPage=1;
        MainMenuScene menu;MainMenuApiClient api;MenuTheme theme;PreBattleArt art;PlayerProfileArt pictures;ProfileLinks links;
        RectTransform root,header,nav,footer,hero,heroPaper,metrics,battleLog,battleHeading,empty;
        TMP_Text title,nameLabel,identityLabel,recordCount,pageLabel,emptyTitle,emptyDescription;
        UnityEngine.UI.Image titleIcon,avatar;
        UnityEngine.UI.Button back,share,previous,next,retry;
        readonly List<UnityEngine.UI.Button> battles=new List<UnityEngine.UI.Button>();
        readonly TMP_Text[] values=new TMP_Text[4],statNames=new TMP_Text[4];
        readonly RectTransform[] stats=new RectTransform[4];
        readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        Material commanderMaterial;
        Coroutine statusFeedback;
        TankRosterScroll scroll;PlayerProfileData data;ViewState state;Coroutine request;int generation;string playerIdentity;
        [NonSerialized] bool configured;
        public bool IsConfigured=>configured&&root&&menu&&stats[0];
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public bool IsLoading=>state==ViewState.Loading;
        public bool IsOwnProfile=>ownProfile;
        public RectTransform Root=>root;
        public TankRosterScroll Scroll=>scroll;
        public PlayerProfileData Data=>data;
        public ViewState State=>state;
        public string PlayerId=>targetId;
        public IReadOnlyList<UnityEngine.UI.Button> BattleButtons=>battles;
        public UnityEngine.UI.Button ShareButton=>share;
        public void Configure(MainMenuScene owner,MenuTheme skin,MainMenuApiClient client,RectTransform frame)
        {
            CloseReplayViewer();ClearStatus();Cancel();configured=false;if(api)api.PlayerLoaded-=OnPlayer;
            menu=owner;theme=skin;api=client;art=Resources.Load<PreBattleArt>("PreBattleArt");pictures=Resources.Load<PlayerProfileArt>("PlayerProfileArt");links=Resources.Load<ProfileLinks>("ProfileLinks");
            var old=frame.Find("Player profile screen");bool open=old&&old.gameObject.activeSelf;
            root=Panel("Player profile screen",frame,null);root.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            BuildView();configured=true;root.gameObject.SetActive(open);
            if(api)api.PlayerLoaded+=OnPlayer;Render();
        }
        public void Open(string playerId=null)
        {
            CloseReplayViewer();ClearStatus();ownProfile=string.IsNullOrEmpty(playerId);targetId=playerId;requestedPage=1;data=null;
            root.gameObject.SetActive(true);root.SetAsLastSibling();menu.SetHeroVisible(false);menu.SetTankSelectorBackdrop(true);menu.RefreshLayout();
            Refresh();Focus(back);
        }
        public void Resume(){if(IsOpen&&!ReplayViewerOpen)Refresh();}
        public void Close(){CloseReplayViewer();ClearStatus();Cancel();root.gameObject.SetActive(false);}
        public void Refresh()
        {
            if(!IsOpen||!api)return;
            CloseReplayViewer();Cancel();request=StartCoroutine(Load(generation));
        }
        IEnumerator Load(int version)
        {
            if(ownProfile&&api.IsLocalGuest){data=null;state=ViewState.Guest;Render();yield break;}
            state=ViewState.Loading;Render();
            if(ownProfile)
            {
                targetId=api.LastPlayer?.id;
                if(!ProfileLinks.ValidPlayerId(targetId))
                {
                    JObject current=null;long statusCode=0;string failure=null;
                    yield return api.Request("GET","/api/player",null,(code,json,error)=>{statusCode=code;current=json;failure=error;});
                    if(version!=generation)yield break;
                    if(statusCode<200||statusCode>=300||!string.IsNullOrEmpty(failure)||current?["authenticated"]?.Type!=JTokenType.Boolean){Fail(ViewState.Unavailable);yield break;}
                    if(!(bool)current["authenticated"]){Fail(ViewState.SignIn);yield break;}
                    targetId=(string)current["player"]?["id"];
                }
            }
            if(!ProfileLinks.ValidPlayerId(targetId)){Fail(ViewState.NotFound);yield break;}
            JObject response=null;long codeReceived=0;string requestError=null;
            string path="/api/players/"+Uri.EscapeDataString(targetId)+"/profile?page="+Mathf.Max(1,requestedPage);
            yield return api.Request("GET",path,null,(code,json,error)=>{response=json;codeReceived=code;requestError=error;});
            if(version!=generation)yield break;
            if(codeReceived==404){Fail(ViewState.NotFound);yield break;}
            if(codeReceived<200||codeReceived>=300||!string.IsNullOrEmpty(requestError)){Fail(ViewState.Unavailable);yield break;}
            try{data=PlayerProfileData.Parse(response,targetId);requestedPage=data.Page;state=ViewState.Ready;}
            catch(Exception exception) when(exception is FormatException||exception is OverflowException||exception is InvalidCastException||exception is ArgumentException||exception is InvalidOperationException){Fail(ViewState.Unavailable);yield break;}
            Render();ResetScroll();
        }
        void Fail(ViewState failure){data=null;state=failure;Render();}
        void Cancel(){generation++;if(request!=null)StopCoroutine(request);request=null;}
        public void SelectPage(int page)
        {
            if(IsLoading||data==null||page<1||page>data.TotalPages)return;
            requestedPage=page;Refresh();Focus(back);
        }
        void OnPlayer(MainMenuApiClient.PlayerSnapshot player)
        {
            string identity=player?.id;bool changed=identity!=playerIdentity;playerIdentity=identity;
            if(IsOpen&&ownProfile&&(changed||state==ViewState.Guest||state==ViewState.SignIn)&&!IsLoading)Refresh();
        }
        void Share()
        {
            string url=data!=null&&links?links.ShareUrl(data.Id):null;
            if(string.IsNullOrEmpty(url)){SetStatus("PROFILE SHARING IS UNAVAILABLE");return;}
#if UNITY_WEBGL && !UNITY_EDITOR
            ProfileShareBridge.Share(gameObject.name,data.Name,url);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var intent=new AndroidJavaObject("android.content.Intent","android.intent.action.SEND"))
                using(var intentClass=new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setType","text/plain");intent.Call<AndroidJavaObject>("putExtra","android.intent.extra.TEXT",url);
                    using(var chooser=intentClass.CallStatic<AndroidJavaObject>("createChooser",intent,"Share Battle Cities profile"))activity.Call("startActivity",chooser);
                }
                SetStatus("PROFILE SHARE OPENED");
            }
            catch(Exception){SetStatus("PROFILE SHARE UNAVAILABLE");}
#else
            GUIUtility.systemCopyBuffer=url;SetStatus("PROFILE LINK COPIED");
#endif
        }
        public void OnProfileShareResult(string result)
        {if(!IsOpen)return;SetStatus(result=="copied"?"PROFILE LINK COPIED":result=="shared"?"PROFILE SHARED":result=="cancelled"?"SHARE CANCELLED":"PROFILE SHARE UNAVAILABLE");}
        #if UNITY_EDITOR
        // Exercise public WATCH responses without replacing the editor's current scene.
        public Action<Core.BattleReplay> EditorReplayLaunchOverride;
#endif
        public void Watch(int index)
        {
            if(!IsOpen||ReplayViewerOpen||state!=ViewState.Ready||data==null||index<0||index>=data.Battles.Count)return;
            var battle=data.Battles[index];if(!battle.Replay){SetStatus("NO REPLAY WAS SAVED FOR THIS BATTLE");return;}
            Cancel();SetStatus("LOADING REPLAY...");request=StartCoroutine(WatchRecording(generation,data.Id,battle.Id));
        }
        IEnumerator WatchRecording(int version,string playerId,string matchId)
        {
            JObject response=null;long status=0;
            string path="/api/players/"+Uri.EscapeDataString(playerId)+"/profile/matches/"+Uri.EscapeDataString(matchId)+"/replay";
            yield return api.Request("GET",path,null,(code,json,error)=>{status=code;if(string.IsNullOrEmpty(error))response=json;});
            if(version!=generation||!IsOpen)yield break;
            if(status<200||status>=300||response==null){SetStatus(status==404?"REPLAY IS NO LONGER AVAILABLE":"REPLAY COULD NOT BE LOADED; TRY AGAIN");yield break;}
            var item=response["item"] as JObject;
            var raw=(item?["replay"] as JObject)??((item?["replays"] as JArray)?.First as JObject);
            if(raw==null){SetStatus("REPLAY COULD NOT BE LOADED; TRY AGAIN");yield break;}
            string format=raw["format"]?.Type==JTokenType.String?(string)raw["format"]:null;
            if(format==Core.BattleReplay.Format)
            {
                Core.BattleReplay recording=null;
                try{recording=Core.ReplayJson.Read(raw.ToString());}
                catch(Exception){SetStatus("REPLAY IS DAMAGED OR NEEDS A DIFFERENT GAME VERSION");}
                if(recording!=null)
                {
#if UNITY_EDITOR
                    if(EditorReplayLaunchOverride!=null){EditorReplayLaunchOverride(recording);yield break;}
#endif
                    try{ShowReplay(recording);}catch(Exception){SetStatus("REPLAY COULD NOT BE OPENED; TRY AGAIN");}
                }
                yield break;
            }
            if(format!=null&&format.StartsWith("battlecities-unity-",StringComparison.Ordinal))
            {SetStatus("REPLAY NEEDS A DIFFERENT GAME VERSION");yield break;}
            SetStatus("THIS RECORDING NEEDS A DIFFERENT GAME VERSION");
        }
        string IdentityText()=>state==ViewState.Ready&&data!=null?data.Provider.ToUpperInvariant()+" PLAYER  •  JOINED "+data.Joined:state==ViewState.Guest?"GUEST PLAYER  •  LOCAL ACCOUNT":"PROFILE RECORD UNAVAILABLE";
        void SetStatus(string message)
        {
            if(!identityLabel)return;ClearStatus();identityLabel.text=message;
            statusFeedback=StartCoroutine(RestoreIdentity());
        }
        IEnumerator RestoreIdentity(){yield return new WaitForSecondsRealtime(4f);statusFeedback=null;if(IsOpen&&identityLabel)identityLabel.text=IdentityText();}
        void ClearStatus(){if(statusFeedback!=null)StopCoroutine(statusFeedback);statusFeedback=null;}
        void ResetScroll(){Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.verticalNormalizedPosition=1;}
        static void Focus(Selectable control){if(control&&EventSystem.current)EventSystem.current.SetSelectedGameObject(control.gameObject);}
        public void KeepControllerFocus(){if(ReplayViewerOpen)Psg1UiNavigation.KeepFocus(replayView,replayPause);else Psg1UiNavigation.KeepFocus(root,back);}
        void OnDisable(){CloseReplayViewer();ClearStatus();Cancel();}
        void OnDestroy(){CloseReplayViewer();ClearStatus();Cancel();if(api)api.PlayerLoaded-=OnPlayer;styles.Dispose();if(commanderMaterial){if(Application.isPlaying)Destroy(commanderMaterial);else DestroyImmediate(commanderMaterial);}}
    }
    static class ProfileShareBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void BattleCitiesProfileShare(string target,string name,string url);
#endif
        public static void Share(string target,string name,string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesProfileShare(target,name,url);
#endif
        }
    }
}
