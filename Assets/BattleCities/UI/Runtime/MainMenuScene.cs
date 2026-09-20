using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Screen = UnityEngine.Device.Screen;

namespace BattleCities.UI
{
    public enum MainMenuPlatform { Auto, Web, Psg1, Android }

    /// <summary>Shared live UI with platform composition; art and labels are independent assets.</summary>
    [ExecuteAlways]
    public sealed class MainMenuScene : MonoBehaviour
    {
        [SerializeField] private MainMenuPlatform platform = MainMenuPlatform.Auto;
        [SerializeField] private string gameplayScene = "BattleCity";
        [SerializeField] private string quartersScene, shopScene, socialsScene;
        [SerializeField] private MenuTheme theme;
        [SerializeField] private RectTransform safeArea, content, statusBar, mainFrame, hero, rewards, navigation, leaderboard, howItWorks, controls;
        [SerializeField] private RectTransform logo, startRect, selectHint, modal;
        [SerializeField] private Button startButton, settingsButton, retryButton, closeButton;
        [SerializeField] private Button[] tabs;
        [SerializeField] private Text playerLabel, scoreLabel, highScoreLabel, modalTitle, modalBody, leaderboardMessage, leaderboardDetail;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private InputSystemUIInputModule inputModule;
        private Canvas canvas;
        private Vector2 lastSize;
        private Rect lastSafeArea;
        private MainMenuPlatform lastPlatform = (MainMenuPlatform)(-1);
        private InputActionAsset liveInput;
        private InputAction cancel;
        private readonly List<InputActionReference> inputReferences = new List<InputActionReference>();
        private GameObject previousSelection;
        private bool loading;
        private string[] rows = Array.Empty<string>();
        public MainMenuPlatform Platform { get => platform; set { platform = value; RefreshLayout(); } }
        public Button StartButton => startButton;
        public Button[] Tabs => tabs;
        public bool IsModalOpen => modal && modal.gameObject.activeSelf;
        public RectTransform Content => content;

        public void Configure(MenuTheme skin, RectTransform safe, RectTransform root, RectTransform header, RectTransform frame,
            RectTransform battlefield, RectTransform prizes, RectTransform nav, RectTransform board, RectTransform info, RectTransform hints,
            RectTransform brand, RectTransform play, RectTransform hint, RectTransform dialog, Button start, Button settings, Button retry,
            Button close, Button[] buttons, Text player, Text score, Text highScore, Text title, Text body, Text boardMessage, Text boardDetail,
            InputActionAsset actions, InputSystemUIInputModule module)
        {
            theme=skin;safeArea=safe;content=root;statusBar=header;mainFrame=frame;hero=battlefield;rewards=prizes;navigation=nav;
            leaderboard=board;howItWorks=info;controls=hints;logo=brand;startRect=play;selectHint=hint;modal=dialog;
            startButton=start;settingsButton=settings;retryButton=retry;closeButton=close;tabs=buttons;
            playerLabel=player;scoreLabel=score;highScoreLabel=highScore;modalTitle=title;modalBody=body;
            leaderboardMessage=boardMessage;leaderboardDetail=boardDetail;inputActions=actions;inputModule=module;
            canvas=GetComponent<Canvas>();
        }
        private void Awake() { canvas=GetComponent<Canvas>(); }
        private void OnEnable()
        {
            if (!Application.isPlaying || !inputActions) return;
            liveInput=Instantiate(inputActions);
            SetPlayer(PlayerPrefs.GetString("battlecities.playerName","COMMANDER"), PlayerPrefs.GetInt("battlecities.lastScore",0), PlayerPrefs.GetInt("battlecities.highScore",0));
            RefreshLayout();
        }
        private void Start()
        {
            if (!Application.isPlaying) return;
            RefreshLayout();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
        private void OnDisable()
        {
            if (cancel != null) cancel.performed -= OnCancel;
            cancel=null;
            if (liveInput) { liveInput.Disable(); Destroy(liveInput); liveInput=null; }
            ClearInputReferences();
        }
        private void Update()
        {
            if (!content || !safeArea) return;
            var size=new Vector2(Screen.width,Screen.height);
            var resolved=Resolve(size);
            if(size!=lastSize || Screen.safeArea!=lastSafeArea || resolved!=lastPlatform) RefreshLayout();
        }
        public MainMenuPlatform Resolve(Vector2 size)
        {
            if(platform!=MainMenuPlatform.Auto)return platform;
            if(RuntimePlatformInfo.IsPsg1)return MainMenuPlatform.Psg1;
            if(RuntimePlatformInfo.Current==GameRuntimePlatform.Android || size.x/Mathf.Max(1,size.y)<.8f)return MainMenuPlatform.Android;
            return MainMenuPlatform.Web;
        }
        public void RefreshLayout()
        {
            if(!content || !safeArea)return;
            if(!canvas)canvas=GetComponent<Canvas>();
            lastSize=new Vector2(Screen.width,Screen.height);lastSafeArea=Screen.safeArea;
            var safe=lastSafeArea;
            safeArea.anchorMin=new Vector2(safe.xMin/Mathf.Max(1,Screen.width),safe.yMin/Mathf.Max(1,Screen.height));
            safeArea.anchorMax=new Vector2(safe.xMax/Mathf.Max(1,Screen.width),safe.yMax/Mathf.Max(1,Screen.height));
            safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            ApplyLayout(Resolve(lastSize),safeArea.rect.size);
        }
        /// <summary>Also used by the editor preview and layout checks at exact platform resolutions.</summary>
        public void ApplyLayout(MainMenuPlatform target, Vector2 available)
        {
            bool psg=target==MainMenuPlatform.Psg1;
            bool portrait=target==MainMenuPlatform.Android && available.y>available.x;
            bool compact=psg || target==MainMenuPlatform.Android;
            Vector2 reference=portrait?new Vector2(940,1672):compact?new Vector2(1240,1080):new Vector2(1600,1067);
            float scale=Mathf.Min(available.x/reference.x,available.y/reference.y);
            if(scale<=0)return;
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=new Vector2(.5f,.5f);
            content.anchoredPosition=Vector2.zero;content.localScale=Vector3.one*scale;
            content.sizeDelta=available/scale;
            float w=content.sizeDelta.x,h=content.sizeDelta.y;
            float pad=portrait?18:16, headerHeight=portrait?112:94;
            float headerWidth=compact?w-2*pad:w*.65f;
            Place(statusBar,(w-headerWidth)/2,pad,headerWidth,headerHeight);
            LayoutHeaderCards();
            float bodyY=pad+headerHeight+12;
            if(!compact)
            {
                float boardW=w*.32f, footH=126;
                float bodyH=h-bodyY-footH-32;
                float navH=bodyH;
                float navW=((navH-40-4*8)/5)*NavigationAspect+40;
                Place(navigation,pad,bodyY,navW,navH);
                Place(mainFrame,pad+navW+12,bodyY,w-boardW-navW-4*pad,bodyH);
                Place(leaderboard,w-pad-boardW,bodyY,boardW,h-bodyY-pad);
                float leftColumnWidth=mainFrame.anchoredPosition.x+mainFrame.sizeDelta.x-pad;
                Place(howItWorks,pad,h-pad-footH,leftColumnWidth,footH);
            }
            else
            {
                float navH=((w-2*pad-40-4*6)/5)/NavigationAspect+40, hintsH=psg?48:0, howH=portrait?200:0;
                float navY=h-pad-navH-hintsH;
                Place(navigation,pad,navY,w-2*pad,navH);
                float mainBottom=navY-12;
                if(portrait){Place(howItWorks,pad,navY-howH-12,w-2*pad,howH);mainBottom=navY-howH-24;}
                Place(mainFrame,pad,bodyY,w-2*pad,mainBottom-bodyY);
                Place(controls,pad,h-pad-hintsH,w-2*pad,hintsH);
            }
            leaderboard.gameObject.SetActive(!compact);howItWorks.gameObject.SetActive(!compact||portrait);controls.gameObject.SetActive(psg);
            float fw=mainFrame.sizeDelta.x,fh=mainFrame.sizeDelta.y;
            float rewardHeight=portrait?fh*.30f:psg?fh*.52f:fh*.43f;
            Place(hero,15,15,fw-30,fh-rewardHeight-25);
            Place(rewards,15,fh-rewardHeight-5,fw-30,rewardHeight-10);
            LayoutRewardRow();
            float hw=hero.sizeDelta.x,hh=hero.sizeDelta.y;
            float startW=Mathf.Min(hw*.68f,psg?500:portrait?650:660);
            var startSprite=startRect.GetComponent<UnityEngine.UI.Image>().sprite;
            float startAspect=startSprite?startSprite.rect.width/startSprite.rect.height:1400f/335f;
            float startH=startW/startAspect;
            float hintH=psg?32:0;
            float logoH=Mathf.Min(hw*.64f*2/3,Mathf.Max(40,hh-startH-hintH-30));
            float logoW=logoH*1.5f;
            float blockH=logoH+startH+hintH+16;
            float top=Mathf.Max(8,(hh-blockH)*.45f);
            Place(logo,(hw-logoW)/2,top,logoW,logoH);
            Place(startRect,(hw-startW)/2,top+logoH+8,startW,startH);
            Place(selectHint,(hw-180)/2,top+logoH+startH+12,180,32);
            selectHint.gameObject.SetActive(psg);
            for(int i=0;i<tabs.Length;i++)
            {
                float nw=navigation.sizeDelta.x,nh=navigation.sizeDelta.y;
                if(compact)
                {
                    float buttonW=(nw-40-4*6)/5;
                    Place((RectTransform)tabs[i].transform,20+i*(buttonW+6),20,buttonW,buttonW/NavigationAspect);
                }
                else
                {
                    float buttonH=(nh-40-4*8)/5;
                    Place((RectTransform)tabs[i].transform,20,20+i*(buttonH+8),buttonH*NavigationAspect,buttonH);
                }
            }
            var modalPanel=(RectTransform)modal.Find("Dialog");
            float mw=Mathf.Min(w-70,720),mh=portrait?520:440;
            Place(modalPanel,(w-mw)/2,(h-mh)/2,mw,mh);
            ConfigureNavigation(compact);
            if(Application.isPlaying && (target!=lastPlatform || inputModule.actionsAsset!=liveInput))ConfigureInput(psg);
            lastPlatform=target;
        }
        private const float NavigationAspect=794f/759f;
        public static void Place(RectTransform rt,float x,float y,float width,float height)
        { rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(width,height); }
        private void LayoutHeaderCards()
        {
            float cell=statusBar.sizeDelta.x/3f;
            for(int i=0;i<3;i++)
            {
                var card=(RectTransform)statusBar.GetChild(i);
                var image=card.GetComponent<Image>();
                if(!image||!image.sprite)continue;
                float aspect=image.sprite.rect.width/image.sprite.rect.height;
                float width=Mathf.Min(cell-12,statusBar.sizeDelta.y*aspect);
                float height=width/aspect;
                image.preserveAspect=true;
                card.localScale=Vector3.one;
                Place(card,i*cell+(cell-width)/2,(statusBar.sizeDelta.y-height)/2,width,height);
            }
        }
        private void LayoutRewardRow()
        {
            // Fit the entire group first, then place adjoining cells. Never stretch artwork
            // or let independent per-cell fitting reintroduce large horizontal gutters.
            var garden=rewards.Find("Garden") as RectTransform;
            if(!garden)return;
            float width=rewards.sizeDelta.x, height=rewards.sizeDelta.y*.82f;
            const float aspect=543f/653f;
            float cellWidth=Mathf.Min(width*.88f/4,height*.96f*aspect);
            float cellHeight=cellWidth/aspect;
            float left=(width-cellWidth*4)/2;
            for(int i=0;i<4;i++)
            {
                var item=garden.Find("Reward "+i) as RectTransform;
                if(!item)continue;
                item.localScale=Vector3.one;
                Place(item,left+i*cellWidth,(height-cellHeight)/2,cellWidth,cellHeight);
                var art=item.Find("Artwork") as RectTransform;
                if(!art)continue;
                // The row already computes exact source proportions. Avoid a second,
                // deferred fitter pass fighting the platform layout and moving the baseline.
                var fitter=art.GetComponent<AspectRatioFitter>();
                if(fitter)fitter.enabled=false;
                art.anchorMin=Vector2.zero;art.anchorMax=Vector2.one;
                art.offsetMin=art.offsetMax=Vector2.zero;art.localScale=Vector3.one;
            }
        }
        private static void Link(Button b,Selectable up,Selectable down,Selectable left,Selectable right)
        { b.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,selectOnLeft=left,selectOnRight=right}; }
        private void ConfigureNavigation(bool compact)
        {
            Link(startButton,settingsButton,tabs[0],compact?tabs[0]:tabs[0],compact?tabs[4]:retryButton);
            Link(settingsButton,compact?tabs[0]:null,startButton,tabs[0],startButton);
            for(int i=0;i<tabs.Length;i++)Link(tabs[i],compact?startButton:(i==0?settingsButton:tabs[i-1]),compact?startButton:tabs[(i+1)%tabs.Length],compact?tabs[(i+tabs.Length-1)%tabs.Length]:startButton,compact?tabs[(i+1)%tabs.Length]:startButton);
            Link(retryButton,settingsButton,startButton,startButton,tabs[0]);
            Link(closeButton,closeButton,closeButton,closeButton,closeButton);
        }
        private void ConfigureInput(bool psg)
        {
            if(!liveInput || !inputModule)return;
            if(cancel!=null)cancel.performed-=OnCancel;
            liveInput.Disable();
            inputModule.actionsAsset=liveInput;
            var map=liveInput.FindActionMap(psg?"PSG1":"UI",true);
            ClearInputReferences();
            inputModule.point=Reference(map.FindAction("Point"));
            inputModule.leftClick=Reference(map.FindAction("Click"));
            inputModule.move=Reference(map.FindAction("Navigate"));
            inputModule.submit=Reference(map.FindAction("Submit"));
            inputModule.cancel=null;
            cancel=map.FindAction("Cancel");cancel.performed+=OnCancel;
            inputModule.moveRepeatDelay=.35f;inputModule.moveRepeatRate=.13f;
            map.Enable();
        }
        private InputActionReference Reference(InputAction action)
        { var reference=InputActionReference.Create(action);inputReferences.Add(reference);return reference; }
        private void ClearInputReferences()
        { foreach(var reference in inputReferences)if(reference)Destroy(reference);inputReferences.Clear(); }
        private void OnCancel(InputAction.CallbackContext ctx) { Back(); }
        public void Back()
        {
            if(IsModalOpen){modal.gameObject.SetActive(false);if(EventSystem.current)EventSystem.current.SetSelectedGameObject(previousSelection?previousSelection:startButton.gameObject);}
            else if(EventSystem.current)EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
        public void StartBattle()
        {
            if(loading)return;
            if(!Application.CanStreamedLevelBeLoaded(gameplayScene)){ShowDialog("BATTLE UNAVAILABLE","The gameplay scene is not included in this build.");return;}
            loading=true; startButton.interactable=false;
            SceneManager.LoadSceneAsync(gameplayScene,LoadSceneMode.Single);
        }
        public void PlayTab() { if(EventSystem.current)EventSystem.current.SetSelectedGameObject(startButton.gameObject); }
        public void OpenQuarters() { OpenDestination(quartersScene,"QUARTERS"); }
        public void OpenShop() { OpenDestination(shopScene,"SHOP"); }
        public void OpenSocials() { OpenDestination(socialsScene,"SOCIALS"); }
        private void OpenDestination(string scene,string title)
        {
            if(!string.IsNullOrWhiteSpace(scene)&&Application.CanStreamedLevelBeLoaded(scene)){SceneManager.LoadSceneAsync(scene);return;}
            ShowDialog(title,"This page has not been converted to Unity yet.\n\nYour main menu is ready. Select START to enter a battle.");
        }
        public void OpenSettings() { ShowDialog("CONTROLS","WEB   Arrow keys / WASD to navigate\nEnter to select · Escape to go back\n\nPSG1   D-pad / left stick to navigate\nA to select · B to go back\n\nANDROID   Tap a button to select"); }
        public void OpenRanking() { ShowDialog("REWARDS LEADERBOARD",rows.Length>0?string.Join("\n",rows):"LIVE BOARD UNAVAILABLE\n\nThe Unity leaderboard service is not connected yet."); }
        public void RetryLeaderboard() { ShowDialog("LIVE BOARD UNAVAILABLE","The Unity leaderboard service is not connected yet.\n\nLive rankings will appear here when it is configured."); }
        private void ShowDialog(string title,string message)
        {
            previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            modalTitle.text=title;modalBody.text=message;modal.gameObject.SetActive(true);modal.SetAsLastSibling();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
        public void SetPlayer(string displayName,int score,int highScore)
        { playerLabel.text=string.IsNullOrEmpty(displayName)?"COMMANDER":displayName;scoreLabel.text=Mathf.Max(0,score).ToString("D6");highScoreLabel.text=Mathf.Max(0,highScore).ToString("D6"); }
        public void SetLeaderboard(string[] formattedRows)
        {
            rows=formattedRows??Array.Empty<string>();
            leaderboardMessage.text=rows.Length>0?string.Join("\n",rows):"LIVE SCORES UNAVAILABLE";
            leaderboardDetail.text=rows.Length>0?"":"Leaderboard service not connected.";
        }
    }
}
