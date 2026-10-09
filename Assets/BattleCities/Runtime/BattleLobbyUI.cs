using System;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace BattleCities.Multiplayer
{
    public sealed partial class BattleLobbyUI : MonoBehaviour
    {
        private Canvas canvas;
        private BattleGame replayGame;
        private GameObject panel;
        private UnityEngine.UI.Text heading,status,roomLabel,roster,modeLabel,regionLabel,mapLabel,quickControls;
        private UnityEngine.UI.Button open,create,join,start,rematch,leave,close,mode,region,previousMap,nextMap;
        private UnityEngine.UI.InputField code;
        private Font font;
        private bool terminalShown;
        private bool lastStarted;
        private Psg1UiInput controllerInput;
        private GameObject previousSelection;
        private UI.MainMenuScene mainMenuView;
        private int openedFrame;
        public bool Visible=>panel&&panel.activeSelf;
        public UnityEngine.UI.Button OpenButton=>open;
        private BattleSession Session=>BattleSession.Instance;
        private static readonly Color Cream=new Color(1,.94f,.8f),Gold=new Color(.92f,.64f,.12f),Dark=new Color(.065f,.085f,.095f,.98f);

        private void Start()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas=new GameObject("Multiplayer UI",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,900);scaler.matchWidthOrHeight=.5f;
            open=Button(canvas.transform,"ONLINE",new Vector2(92,-74),new Vector2(158,38),Show);
            var openRect=(RectTransform)open.transform;openRect.anchorMin=openRect.anchorMax=new Vector2(0,1);
            panel=new GameObject("Room panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=new Vector2(.12f,.08f);rect.anchorMax=new Vector2(.88f,.92f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color=Dark;
            heading=Label(panel.transform,"BATTLE TOGETHER",32,TextAnchor.MiddleCenter);Place(heading.rectTransform,.05f,.95f,.84f,.96f);
            mode=Button(panel.transform,"CO-OP",Vector2.zero,Vector2.zero,()=>Session.SelectedMode=BattleModeRules.NextOnline(Session.SelectedMode));
            Place((RectTransform)mode.transform,.06f,.47f,.75f,.83f);modeLabel=mode.GetComponentInChildren<UnityEngine.UI.Text>();
            region=Button(panel.transform,"REGION",Vector2.zero,Vector2.zero,()=>Session.Region=BattleSession.RegionCodes[(Array.IndexOf(BattleSession.RegionCodes,Session.Region)+1)%BattleSession.RegionCodes.Length]);
            Place((RectTransform)region.transform,.53f,.94f,.75f,.83f);regionLabel=region.GetComponentInChildren<UnityEngine.UI.Text>();
            mapLabel=Label(panel.transform,"MAP 01",22,TextAnchor.MiddleCenter);Place(mapLabel.rectTransform,.22f,.78f,.66f,.73f);
            previousMap=Button(panel.transform,"<",Vector2.zero,Vector2.zero,()=>Session.SelectedMap=Session.SelectedMap<=1?35:Session.SelectedMap-1);Place((RectTransform)previousMap.transform,.06f,.2f,.66f,.73f);
            nextMap=Button(panel.transform,">",Vector2.zero,Vector2.zero,()=>Session.SelectedMap=Session.SelectedMap>=35?1:Session.SelectedMap+1);Place((RectTransform)nextMap.transform,.8f,.94f,.66f,.73f);
            var inputGo=new GameObject("Room code",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.InputField));inputGo.transform.SetParent(panel.transform,false);
            Place((RectTransform)inputGo.transform,.06f,.94f,.56f,.64f);inputGo.GetComponent<UnityEngine.UI.Image>().color=new Color(.16f,.19f,.19f);
            code=inputGo.GetComponent<UnityEngine.UI.InputField>();code.characterLimit=16;code.targetGraphic=inputGo.GetComponent<UnityEngine.UI.Image>();
            code.textComponent=Label(inputGo.transform,"",24,TextAnchor.MiddleCenter);Stretch(code.textComponent.rectTransform);
            var placeholder=Label(inputGo.transform,"FRIEND'S ROOM CODE",20,TextAnchor.MiddleCenter);placeholder.color=new Color(.65f,.7f,.69f);Stretch(placeholder.rectTransform);code.placeholder=placeholder;
            create=Button(panel.transform,"CREATE ROOM",Vector2.zero,Vector2.zero,()=>{_ = Session.Connect(true,"");});Place((RectTransform)create.transform,.06f,.47f,.46f,.54f);
            join=Button(panel.transform,"JOIN ROOM",Vector2.zero,Vector2.zero,()=>{_ = Session.Connect(false,code.text);});Place((RectTransform)join.transform,.53f,.94f,.46f,.54f);
            roomLabel=Label(panel.transform,"",24,TextAnchor.MiddleCenter);Place(roomLabel.rectTransform,.06f,.94f,.38f,.45f);
            roster=Label(panel.transform,"",18,TextAnchor.MiddleCenter);Place(roster.rectTransform,.06f,.94f,.29f,.38f);
            status=Label(panel.transform,"",19,TextAnchor.MiddleCenter);Place(status.rectTransform,.06f,.94f,.17f,.29f);
            quickControls=Label(panel.transform,"DRIVE  WASD     AIM  ARROW KEYS\nFIRE / CHARGE  SPACE\nCHANGE WEAPON  Q     DEPLOY  E / RIGHT CLICK",19,TextAnchor.MiddleCenter);
            Place(quickControls.rectTransform,.08f,.92f,.25f,.45f);quickControls.gameObject.SetActive(false);
            start=Button(panel.transform,"START MATCH",Vector2.zero,Vector2.zero,()=>{Session.Match.RequestStart();Hide();});Place((RectTransform)start.transform,.06f,.47f,.075f,.155f);
            rematch=Button(panel.transform,"REMATCH LOBBY",Vector2.zero,Vector2.zero,()=>{Session.Match.RequestRematch();terminalShown=false;});Place((RectTransform)rematch.transform,.06f,.47f,.075f,.155f);
            leave=Button(panel.transform,"LEAVE ROOM",Vector2.zero,Vector2.zero,()=>{if(Session.CanCancelConnection)Session.CancelConnection();else _ = Session.Leave();});Place((RectTransform)leave.transform,.53f,.94f,.075f,.155f);
            close=Button(panel.transform,"BACK TO GAME",Vector2.zero,Vector2.zero,Hide);Place((RectTransform)close.transform,.3f,.7f,.015f,.065f);
            BuildRoomCodeKeypad();
            panel.SetActive(false);
        }
        public void Show()
        {
            if(!panel)return;
            if(!Visible)previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            EnsureEventSystem();panel.SetActive(true);
            ConfigureController();openedFrame=Time.frameCount;
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();if(game)game.Paused=true;
            codeKeypad.SetActive(false);
            EventSystem.current.SetSelectedGameObject(Session.QuickMatching?leave.gameObject:close.gameObject);
        }
        public void Hide()
        {
            if(!panel)return;panel.SetActive(false);codeKeypad.SetActive(false);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(previousSelection&&previousSelection.activeInHierarchy?previousSelection:null);
            ReleaseController();
            var game=UnityEngine.Object.FindFirstObjectByType<BattleGame>();if(game)game.Paused=false;
        }
        private void Update()
        {
            if(!canvas||!Session)return;
            if(!replayGame&&SceneManager.GetActiveScene().name=="BattleCity")replayGame=UnityEngine.Object.FindAnyObjectByType<BattleGame>();
            if(replayGame&&(replayGame.IsReplaying||replayGame.HasMatchResult||replayGame.ResultsVisible))
            {open.gameObject.SetActive(false);if(Visible){panel.SetActive(false);ReleaseController();}return;}
            bool available=SceneManager.GetActiveScene().name!="Login";
            bool psgMenu=RuntimePlatformInfo.IsPsg1&&SceneManager.GetActiveScene().name=="MainMenu";
            if(psgMenu&&!mainMenuView)mainMenuView=UnityEngine.Object.FindFirstObjectByType<UI.MainMenuScene>();
            var homeHud=psgMenu&&mainMenuView?mainMenuView.Content.Find("Status Bar"):null;
            open.gameObject.SetActive(available&&!Visible&&(!homeHud||homeHud.gameObject.activeInHierarchy));
            ((RectTransform)open.transform).anchoredPosition=psgMenu
                ?new Vector2(128,-206):new Vector2(92,-74);
            var match=Session.Match;
            bool advancing=match&&match.Mode==BattleMode.Coop&&match.Won&&match.Map<35;
            bool ended=match&&(match.Won||match.Lost)&&!advancing;
            if(advancing&&Visible)Hide();
            bool started=match&&match.Started;
            if(started&&!lastStarted&&!ended)Hide();
            lastStarted=started;
            if(ended&&!terminalShown){terminalShown=true;Show();}
            if(match&&!match.Started)terminalShown=false;
            if(!Visible)return;
            EnsureEventSystem();
            ConfigureController();
            if(controllerInput!=null && Time.frameCount>openedFrame && controllerInput.CancelPressed)
            {
                if(codeKeypad.activeSelf){CloseRoomCodeKeypad();return;}
                if(Session.CanCancelConnection){Session.CancelConnection();return;}
                if(Session.QuickMatching && !Session.Busy){_ = Session.Leave();return;}
                if(!Session.Busy){Hide();return;}
            }
            bool connected=Session.Online,editable=!connected&&!Session.Busy;
            bool quick=Session.QuickMatching;
            mode.gameObject.SetActive(!quick);region.gameObject.SetActive(!quick);
            mapLabel.gameObject.SetActive(!quick);previousMap.gameObject.SetActive(!quick);nextMap.gameObject.SetActive(!quick);
            code.gameObject.SetActive(!quick&&!RuntimePlatformInfo.IsPsg1);
            codeButton.gameObject.SetActive(!quick&&RuntimePlatformInfo.IsPsg1);
            create.gameObject.SetActive(!quick);join.gameObject.SetActive(!quick);
            roomLabel.gameObject.SetActive(!quick);quickControls.gameObject.SetActive(quick&&!ended);
            close.gameObject.SetActive(!quick);
            Place(roster.rectTransform,.06f,.94f,quick?.63f:.29f,quick?.74f:.38f);
            Place(status.rectTransform,.06f,.94f,quick?.47f:.17f,quick?.62f:.29f);
            Place((RectTransform)leave.transform,quick&&!ended?.3f:.53f,quick&&!ended?.7f:.94f,.075f,.155f);
            mode.interactable=editable&&!Session.ModeLockedByLaunchFlag;
            region.interactable=previousMap.interactable=nextMap.interactable=create.interactable=editable;
            code.interactable=editable;join.interactable=editable&&!string.IsNullOrWhiteSpace(code.text);
            codeButton.interactable=editable;
            codeButton.GetComponentInChildren<UnityEngine.UI.Text>().text=string.IsNullOrEmpty(code.text)?"ENTER ROOM CODE":code.text;
            if(!editable&&codeKeypad.activeSelf)CloseRoomCodeKeypad();
            modeLabel.text=BattleModeRules.Label(match?match.Mode:Session.SelectedMode);
            regionLabel.text="REGION: "+Session.Region.ToUpperInvariant();mapLabel.text="MAP "+(match?match.Map:Session.SelectedMap).ToString("00");
            roomLabel.text=connected?(Session.QuickMatching?"AUTOMATIC MATCH":"ROOM  "+Session.RoomCode):BattleModeRules.PlayerLimit(Session.SelectedMode)+" PLAYERS";
            roster.text=match?string.Join("   ",Enumerable.Range(0,4).Where(i=>match.Players[i]!=Fusion.PlayerRef.None).Select(i=>"P"+(i+1)+(i==match.LocalSlot?" (YOU)":"")+"  ♥ "+match.Participants[i].Lives)):"Co-op: defend together. Brawl: destroy the enemy base. CTF: capture three flags.";
            heading.text=ended&&BattleModeRules.IsTeamMode(match.Mode)?(match.Winner>=0?"TEAM "+(BattleSimulation.TeamForSlot(match.Winner)+1)+" WINS":"DRAW"):ended?(match.Mode==BattleMode.Versus?(match.Winner>=0?"PLAYER "+(match.Winner+1)+" WINS":"DRAW"):(match.Won?"STAGE CLEAR":"BASE LOST / TEAM ELIMINATED")):quick?"GET READY TO BATTLE":"BATTLE TOGETHER";
            status.text=Session.Status+(connected&&match&&!match.Started?"\n"+(Session.QuickMatching?(match.RequiredPlayers==4?"The match starts when four players join.":"The match starts when two players join."):Session.IsHost?"Start when everyone has joined.":"Waiting for the host to start."):"");
            if(quick&&!ended)
            {
                roster.text=match?match.PlayerCount+" / "+match.RequiredPlayers+" PLAYERS CONNECTED":"SEARCHING FOR A MATCH";
                status.text=match&&match.PlayerCount>=match.RequiredPlayers?"Players found. Match starting soon...":connected?(match&&match.RequiredPlayers==4?"Waiting for four players. Defend your eagle and destroy the opposing base.":"Waiting for another player to join..."):"Finding a match...";
            }
            else if(quick&&ended)status.text="Match complete.";
            start.gameObject.SetActive(connected&&!ended&&!Session.QuickMatching);start.interactable=Session.IsHost&&match&&!match.Started&&match.PlayerCount>=match.RequiredPlayers;
            rematch.gameObject.SetActive(connected&&ended);rematch.interactable=Session.IsHost;
            leave.gameObject.SetActive(connected||Session.Busy);leave.interactable=!Session.Busy||Session.CanCancelConnection;
            leave.GetComponentInChildren<UnityEngine.UI.Text>().text=Session.CanCancelConnection?"CANCEL CONNECTION":"LEAVE ROOM";
            close.interactable=!Session.Busy;
            close.GetComponentInChildren<UnityEngine.UI.Text>().text=SceneManager.GetActiveScene().name=="MainMenu"?"BACK TO MENU":"BACK TO GAME";
            if(RuntimePlatformInfo.IsPsg1)
            {
                quickControls.text=BattleGamepadBindings.Help;
                ConfigureLobbyNavigation();
            }
        }
        private void ConfigureController()
        {
            if(!RuntimePlatformInfo.IsPsg1||!EventSystem.current)return;
            if(controllerInput!=null&&controllerInput.MatchesCurrent)return;
            ReleaseController();controllerInput=new Psg1UiInput();
        }
        private void ReleaseController()
        {
            controllerInput?.Dispose();controllerInput=null;
        }
        private void OnDestroy()=>ReleaseController();
        private void EnsureEventSystem()
        {
            if(EventSystem.current)return;
            var go=new GameObject("Multiplayer EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform,false);
        }
        private UnityEngine.UI.Text Label(Transform parent,string text,int size,TextAnchor alignment)
        {
            var go=new GameObject(text.Length>0?text:"Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(parent,false);
            var label=go.GetComponent<UnityEngine.UI.Text>();label.font=font;label.fontSize=size;label.text=text;label.color=Cream;label.alignment=alignment;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        private UnityEngine.UI.Button Button(Transform parent,string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=Gold;
            var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var colors=button.colors;colors.highlightedColor=new Color(1,1,.7f);colors.selectedColor=new Color(1,1,.7f);colors.disabledColor=new Color(.4f,.4f,.4f,.6f);button.colors=colors;
            var label=Label(go.transform,title,20,TextAnchor.MiddleCenter);label.color=new Color(.05f,.07f,.08f);Stretch(label.rectTransform);return button;
        }
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(8,0);rect.offsetMax=new Vector2(-8,0);}
        private static void Place(RectTransform rect,float left,float right,float bottom,float top)
        {rect.anchorMin=new Vector2(left,bottom);rect.anchorMax=new Vector2(right,top);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
