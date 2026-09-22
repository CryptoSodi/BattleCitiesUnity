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


    [Serializable]
    public sealed class RewardItemLayoutSettings
    {
        [Tooltip("Additional pixel offset for this reward. Positive Y moves it upward.")]
        public Vector2 positionOffset;
        [Tooltip("Uniform size multiplier. 1 keeps the automatically fitted size and aspect ratio.")]
        [Range(.25f, 2f)] public float sizeScale = 1;
    }

    [Serializable]
    public sealed class MainMenuLayoutSettings
    {
        [Header("Canvas and Header")]
        public Vector2 referenceResolution = new Vector2(1600, 1067);
        [Min(0)] public float outerPadding = 16;
        [Min(1)] public float headerHeight = 94;
        [Range(.2f, 1f)] public float headerWidthRatio = .65f;
        [Min(0)] public float headerToBodyGap = 12;

        [Header("Desktop Columns")]
        [Range(.15f, .6f)] public float leaderboardWidthRatio = .32f;
        [Min(0)] public float howItWorksHeight = 126;
        [Min(0)] public float desktopBottomSpacing = 32;
        [Min(0)] public float desktopColumnGap = 12;

        [Header("Compact Navigation")]
        [Range(.2f, 1f)] public float navigationWidthRatio = 1;
        [Min(0)] public float navigationSidePadding = 20;
        [Min(0)] public float navigationButtonGap = 8;
        [Min(0)] public float navigationVerticalPadding = 20;
        [Min(0)] public float controllerLegendHeight;
        [Min(0)] public float controllerLegendAutoHideSeconds;
        [Min(0)] public float navigationToLegendGap;
        [Min(0)] public float mainToNavigationGap = 12;
        [Min(0)] public float controllerLegendSideInset = 16;
        [Min(0)] public float portraitHowItWorksHeight = 200;
        [Min(0)] public float portraitHowToNavigationGap = 12;
        [Min(0)] public float mainToPortraitHowGap = 12;

        [Header("TV and Rewards")]
        [Min(0)] public float tvFrameInset = 12;
        [Min(0)] public float contentInset = 28;
        [Range(.1f, .8f)] public float rewardsHeightRatio = .43f;
        public bool rewardBannerAtBottom;
        [Tooltip("Additional pixel offset for the reward chest row. Positive Y moves it upward.")]
        public Vector2 rewardItemsOffset;
        [Tooltip("Additional pixel offset for the TOP 10 banner. Positive Y moves it upward.")]
        public Vector2 rewardBannerOffset;

        [Header("Individual Rewards")]
        public RewardItemLayoutSettings secondPlaceReward = new RewardItemLayoutSettings();
        public RewardItemLayoutSettings firstPlaceReward = new RewardItemLayoutSettings();
        public RewardItemLayoutSettings thirdPlaceReward = new RewardItemLayoutSettings();
        public RewardItemLayoutSettings fourthToTenthReward = new RewardItemLayoutSettings();

        public RewardItemLayoutSettings GetRewardItem(int index)
        {
            if(index==0)return secondPlaceReward ?? (secondPlaceReward=new RewardItemLayoutSettings());
            if(index==1)return firstPlaceReward ?? (firstPlaceReward=new RewardItemLayoutSettings());
            if(index==2)return thirdPlaceReward ?? (thirdPlaceReward=new RewardItemLayoutSettings());
            return fourthToTenthReward ?? (fourthToTenthReward=new RewardItemLayoutSettings());
        }

        [Header("Hero, Logo and Start")]
        [Range(.1f, 1f)] public float startWidthRatio = .58f;
        [Min(1)] public float startMaxWidth = 560;
        [Tooltip("Additional pixel offset for the Start button. Positive Y moves it downward.")]
        public Vector2 startButtonOffset;
        [Min(0)] public float selectHintHeight;
        [Range(.05f, .8f)] public float logoHeightFromWidthRatio = .3066667f;
        [Range(.05f, .9f)] public float logoMaxHeroHeightRatio = .4f;
        [Min(0)] public float heroBlockSpacing = 16;
        [Range(0, 1)] public float heroTopFactor = .45f;
        [Range(0, .2f)] public float heroDownRatio = .03f;
        [Min(0)] public float heroDownMin = 8;
        [Min(0)] public float heroDownMax = 16;
        [Range(0, .2f)] public float logoStartGapRatio = .054f;
        [Min(0)] public float logoStartGapMin = 14;
        [Min(0)] public float logoStartGapMax = 24;

        [Header("Menu Labels")]
        [Range(0, 1)] public float menuLabelBottom = .09f;
        [Range(0, 1)] public float menuLabelTop = .27f;
        [Min(1)] public int menuLabelMinSize = 19;
        [Min(1)] public int menuLabelMaxSize = 29;

        public static MainMenuLayoutSettings Web()
        {
            return new MainMenuLayoutSettings();
        }

        public static MainMenuLayoutSettings Psg1()
        {
            return new MainMenuLayoutSettings
            {
                referenceResolution = new Vector2(1240, 1080),
                headerWidthRatio = 1208f / 1240f,
                headerToBodyGap = 4,
                navigationWidthRatio = .60f,
                navigationSidePadding = 16,
                navigationButtonGap = 12,
                navigationVerticalPadding = 10,
                controllerLegendHeight = 48,
                controllerLegendAutoHideSeconds = 30,
                navigationToLegendGap = 4,
                mainToNavigationGap = 20,
                controllerLegendSideInset = 34,
                portraitHowItWorksHeight = 0,
                rewardsHeightRatio = .52f,
                rewardBannerAtBottom = true,
                startMaxWidth = 440,
                startButtonOffset = new Vector2(0,12),
                selectHintHeight = 0,
                menuLabelBottom = .12f,
                menuLabelTop = .30f,
                menuLabelMinSize = 16,
                menuLabelMaxSize = 22
            };
        }

        public static MainMenuLayoutSettings Android()
        {
            return new MainMenuLayoutSettings
            {
                referenceResolution = new Vector2(940, 1672),
                outerPadding = 18,
                headerHeight = 112,
                headerWidthRatio = 904f / 940f,
                headerToBodyGap = 12,
                navigationWidthRatio = 1,
                navigationSidePadding = 20,
                navigationButtonGap = 6,
                navigationVerticalPadding = 20,
                controllerLegendHeight = 0,
                navigationToLegendGap = 0,
                mainToNavigationGap = 12,
                controllerLegendSideInset = 18,
                portraitHowItWorksHeight = 200,
                portraitHowToNavigationGap = 12,
                mainToPortraitHowGap = 12,
                rewardsHeightRatio = .30f,
                startMaxWidth = 560,
                selectHintHeight = 0
            };
        }
    }

    /// <summary>Shared live UI with platform composition; art and labels are independent assets.</summary>
    [ExecuteAlways]
    public sealed class MainMenuScene : MonoBehaviour
    {
        [SerializeField] private MainMenuPlatform platform = MainMenuPlatform.Auto;
        [SerializeField] private string gameplayScene = "BattleCity";
        [SerializeField] private string quartersScene, shopScene, socialsScene;
        [SerializeField] private MenuTheme theme;

        [Header("Platform Layouts — edit these instead of RectTransforms")]
        [SerializeField] private MainMenuLayoutSettings webLayout = MainMenuLayoutSettings.Web();
        [SerializeField] private MainMenuLayoutSettings psg1Layout = MainMenuLayoutSettings.Psg1();
        [SerializeField] private MainMenuLayoutSettings androidLayout = MainMenuLayoutSettings.Android();
        [SerializeField] private RectTransform safeArea, content, statusBar, mainFrame, hero, rewards, navigation, leaderboard, howItWorks, controls;
        [SerializeField] private RectTransform logo, startRect, selectHint, modal;
        [SerializeField] private Button startButton, settingsButton, retryButton, closeButton;
        [SerializeField] private Button[] tabs;
        [SerializeField] private Text playerLabel, scoreLabel, highScoreLabel, modalTitle, modalBody, leaderboardMessage, leaderboardDetail;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private MainMenuApiClient apiClient;
        private Canvas canvas;
        private Vector2 lastSize;
        private Rect lastSafeArea;
        private MainMenuPlatform lastPlatform = (MainMenuPlatform)(-1);
        private InputActionAsset liveInput;
        private InputAction cancel;
        private readonly List<InputActionReference> inputReferences = new List<InputActionReference>();
        private GameObject previousSelection;
        private Button walletLoginButton;
        private bool loading;
        private float controllerLegendHideAt = -1;
        private bool controllerLegendHidden;
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
        private void Awake()
        {
            canvas=GetComponent<Canvas>();
            if(Application.isPlaying)EnsureApiClient();
        }
        public MainMenuLayoutSettings WebLayout => webLayout;
        public MainMenuLayoutSettings Psg1Layout => psg1Layout;
        public MainMenuLayoutSettings AndroidLayout => androidLayout;

        private MainMenuLayoutSettings GetLayout(MainMenuPlatform target)
        {
            if(target==MainMenuPlatform.Psg1)return psg1Layout ?? (psg1Layout=MainMenuLayoutSettings.Psg1());
            if(target==MainMenuPlatform.Android)return androidLayout ?? (androidLayout=MainMenuLayoutSettings.Android());
            return webLayout ?? (webLayout=MainMenuLayoutSettings.Web());
        }

        private void OnValidate()
        {
            lastSize=new Vector2(-1,-1);
            lastPlatform=(MainMenuPlatform)(-1);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall-=RefreshLayoutAfterValidation;
            UnityEditor.EditorApplication.delayCall+=RefreshLayoutAfterValidation;
#endif
        }

#if UNITY_EDITOR
        private void RefreshLayoutAfterValidation()
        {
            UnityEditor.EditorApplication.delayCall-=RefreshLayoutAfterValidation;
            if(this && !Application.isPlaying && isActiveAndEnabled && content && safeArea)RefreshLayout();
        }
#endif

        private void ResetControllerLegendTimer()
        {
            controllerLegendHidden=false;
            float delay=GetLayout(MainMenuPlatform.Psg1).controllerLegendAutoHideSeconds;
            controllerLegendHideAt=delay>0?Time.unscaledTime+delay:-1;
        }

        public void SetControllerLegendVisible(bool visible)
        {
            controllerLegendHidden=!visible;
            if(visible)ResetControllerLegendTimer();
            RefreshLayout();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            EnsureApiClient();
            BindApiClient();
            ResetControllerLegendTimer();
            if(!inputActions){RefreshLayout();return;}
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
            UnbindApiClient();
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
            if(Application.isPlaying && resolved==MainMenuPlatform.Psg1 && !controllerLegendHidden && controllerLegendHideAt>=0 && Time.unscaledTime>=controllerLegendHideAt)
            {
                controllerLegendHidden=true;
                RefreshLayout();
            }
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
            var layout=GetLayout(target);
            bool hideControllerLegend=psg && controllerLegendHidden;
            Vector2 reference=layout.referenceResolution;
            float scale=Mathf.Min(available.x/reference.x,available.y/reference.y);
            if(scale<=0)return;
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=new Vector2(.5f,.5f);
            content.anchoredPosition=Vector2.zero;content.localScale=Vector3.one*scale;
            content.sizeDelta=available/scale;
            float w=content.sizeDelta.x,h=content.sizeDelta.y;
            float pad=layout.outerPadding,headerHeight=layout.headerHeight;
            float headerWidth=w*layout.headerWidthRatio;
            Place(statusBar,(w-headerWidth)/2,pad,headerWidth,headerHeight);
            LayoutHeaderCards();
            float bodyY=pad+headerHeight+layout.headerToBodyGap;
            float compactGap=layout.navigationButtonGap,compactSide=layout.navigationSidePadding,compactVertical=layout.navigationVerticalPadding;
            if(!compact)
            {
                float boardW=w*layout.leaderboardWidthRatio,footH=layout.howItWorksHeight;
                float bodyH=h-bodyY-footH-layout.desktopBottomSpacing;
                float navH=bodyH;
                float navW=((navH-2*compactVertical-4*compactGap)/5)*NavigationAspect+2*compactSide;
                Place(navigation,pad,bodyY,navW,navH);
                Place(mainFrame,pad+navW+layout.desktopColumnGap,bodyY,w-boardW-navW-4*pad,bodyH);
                Place(leaderboard,w-pad-boardW,bodyY,boardW,h-bodyY-pad);
                float leftColumnWidth=mainFrame.anchoredPosition.x+mainFrame.sizeDelta.x-pad;
                Place(howItWorks,pad,h-pad-footH,leftColumnWidth,footH);
            }
            else
            {
                float availableNavW=w-2*pad;
                float navW=availableNavW*layout.navigationWidthRatio;
                float navButtonW=(navW-compactSide*2-compactGap*4)/5;
                float navH=navButtonW/NavigationAspect+compactVertical*2,hintsH=hideControllerLegend?0:layout.controllerLegendHeight,howH=portrait?layout.portraitHowItWorksHeight:0;
                float navControlsGap=hideControllerLegend?0:layout.navigationToLegendGap;
                float navY=h-pad-navH-hintsH-navControlsGap;
                Place(navigation,(w-navW)*.5f,navY,navW,navH);
                float mainBottom=navY-layout.mainToNavigationGap;
                if(portrait){Place(howItWorks,pad,navY-howH-layout.portraitHowToNavigationGap,w-2*pad,howH);mainBottom=navY-howH-layout.portraitHowToNavigationGap-layout.mainToPortraitHowGap;}
                Place(mainFrame,pad,bodyY,w-2*pad,mainBottom-bodyY);
                float controlsInset=layout.controllerLegendSideInset;
                Place(controls,controlsInset,h-pad-hintsH,w-2*controlsInset,hintsH);
            }
            leaderboard.gameObject.SetActive(!compact);howItWorks.gameObject.SetActive(!compact||portrait);controls.gameObject.SetActive(psg&&!hideControllerLegend);
            float fw=mainFrame.sizeDelta.x,fh=mainFrame.sizeDelta.y;
            float tvInset=layout.tvFrameInset,contentInset=layout.contentInset;
            float innerW=fw-contentInset*2,innerH=fh-contentInset*2;
            var television=(RectTransform)mainFrame.Find("TV Frame");
            if(television!=null)Place(television,tvInset,tvInset,fw-tvInset*2,fh-tvInset*2);
            var televisionViewport=(RectTransform)mainFrame.Find("TV Background Viewport");
            if(televisionViewport!=null)Place(televisionViewport,contentInset,contentInset,innerW,innerH);
            var televisionBackground=televisionViewport!=null?televisionViewport.Find("TV Background") as RectTransform:null;
            if(televisionBackground!=null)
            {
                var backgroundImage=televisionBackground.GetComponent<UnityEngine.UI.Image>();
                var platformBackground=psg&&theme&&theme.PsgBattlefield?theme.PsgBattlefield:theme?theme.Battlefield:null;
                if(backgroundImage&&platformBackground)backgroundImage.sprite=platformBackground;
                float backgroundAspect=backgroundImage.sprite?backgroundImage.sprite.rect.width/backgroundImage.sprite.rect.height:1;
                float viewportAspect=innerW/innerH;
                televisionBackground.anchorMin=televisionBackground.anchorMax=televisionBackground.pivot=new Vector2(.5f,.5f);
                televisionBackground.anchoredPosition=Vector2.zero;
                televisionBackground.sizeDelta=backgroundAspect>viewportAspect
                    ?new Vector2(innerH*backgroundAspect,innerH)
                    :new Vector2(innerW,innerW/backgroundAspect);
            }
            float rewardHeight=layout.rewardsHeightRatio*innerH;
            Place(hero,contentInset,contentInset,innerW,innerH-rewardHeight);
            Place(rewards,contentInset,contentInset+innerH-rewardHeight,innerW,rewardHeight);
            LayoutRewardRow(layout);
            float hw=hero.sizeDelta.x,hh=hero.sizeDelta.y;
            float startW=Mathf.Min(hw*layout.startWidthRatio,layout.startMaxWidth);
            var startSprite=startRect.GetComponent<UnityEngine.UI.Image>().sprite;
            float startAspect=startSprite?startSprite.rect.width/startSprite.rect.height:1400f/335f;
            float startH=startW/startAspect;
            float hintH=layout.selectHintHeight;
            float logoH=Mathf.Min(hw*layout.logoHeightFromWidthRatio,Mathf.Max(40,hh*layout.logoMaxHeroHeightRatio));
            float logoW=logoH*1.5f;
            float blockH=logoH+startH+hintH+layout.heroBlockSpacing;
            float top=Mathf.Max(8,(hh-blockH)*layout.heroTopFactor);
            float heroDown=Mathf.Clamp(hh*layout.heroDownRatio,layout.heroDownMin,layout.heroDownMax);
            float heroGap=Mathf.Clamp(hh*layout.logoStartGapRatio,layout.logoStartGapMin,layout.logoStartGapMax);
            float logoVisualH=logoH*Mathf.Abs(logo.localScale.y);
            float startVisualH=startH*Mathf.Abs(startRect.localScale.y);
            top+=heroDown;
            Place(logo,(hw-logoW*Mathf.Abs(logo.localScale.x))*.5f,top,logoW,logoH);
            Place(startRect,(hw-startW*Mathf.Abs(startRect.localScale.x))*.5f+layout.startButtonOffset.x,top+logoVisualH+heroGap+layout.startButtonOffset.y,startW,startH);
            Place(selectHint,(hw-180)/2+layout.startButtonOffset.x,top+logoVisualH+heroGap+startVisualH+8+layout.startButtonOffset.y,180,32);
            selectHint.gameObject.SetActive(psg&&layout.selectHintHeight>0);
            for(int i=0;i<tabs.Length;i++)
            {
                float nw=navigation.sizeDelta.x,nh=navigation.sizeDelta.y;
                if(compact)
                {
                    float buttonW=(nw-compactSide*2-compactGap*4)/5;
                    Place((RectTransform)tabs[i].transform,compactSide+i*(buttonW+compactGap),compactVertical,buttonW,buttonW/NavigationAspect);
                }
                else
                {
                    float buttonH=(nh-40-4*8)/5;
                    Place((RectTransform)tabs[i].transform,20,20+i*(buttonH+8),buttonH*NavigationAspect,buttonH);
                }
                var tabLabel=tabs[i].GetComponentInChildren<UnityEngine.UI.Text>(true);
                if(tabLabel!=null)
                {
                    var labelRect=tabLabel.rectTransform;
                    labelRect.anchorMin=new Vector2(.06f,layout.menuLabelBottom);
                    labelRect.anchorMax=new Vector2(.94f,layout.menuLabelTop);
                    labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
                    tabLabel.resizeTextMinSize=layout.menuLabelMinSize;
                    tabLabel.resizeTextMaxSize=layout.menuLabelMaxSize;
                    tabLabel.transform.SetAsLastSibling();
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
        private void LayoutRewardRow(MainMenuLayoutSettings layout)
        {
            // Fit the entire group first, then place adjoining cells. Never stretch artwork
            // or let independent per-cell fitting reintroduce large horizontal gutters.
            var garden=rewards.Find("Garden") as RectTransform;
            if(!garden)return;
            float width=rewards.sizeDelta.x, height=rewards.sizeDelta.y*.82f;
            float verticalNudge=Mathf.Clamp(rewards.sizeDelta.y*.025f,6f,12f);
            var banner=rewards.Find("Header Bar") as RectTransform;
            bool bannerAtBottom=layout.rewardBannerAtBottom;
            if(banner)
            {
                banner.anchorMin=new Vector2(0,bannerAtBottom?0:.84f);
                banner.anchorMax=new Vector2(1,bannerAtBottom?.16f:1);
                banner.pivot=new Vector2(.5f,.5f);
                banner.sizeDelta=new Vector2(-width*.06812f,0);
                banner.anchoredPosition=new Vector2(width*.00208f,bannerAtBottom?0:-verticalNudge)+layout.rewardBannerOffset;
            }
            garden.anchorMin=new Vector2(0,bannerAtBottom?.16f:0);
            garden.anchorMax=new Vector2(1,bannerAtBottom?1:.84f);
            garden.pivot=new Vector2(.5f,.5f);
            garden.sizeDelta=Vector2.zero;
            garden.anchoredPosition=new Vector2(0,bannerAtBottom?verticalNudge:-verticalNudge)+layout.rewardItemsOffset;
            garden.localScale=Vector3.one*.92f;
            const float aspect=543f/653f;
            float cellWidth=Mathf.Min(width*.88f/4,height*.96f*aspect);
            float cellHeight=cellWidth/aspect;
            float left=(width-cellWidth*4)/2;
            for(int i=0;i<4;i++)
            {
                var item=garden.Find("Reward "+i) as RectTransform;
                if(!item)continue;
                item.localScale=Vector3.one;
                var itemLayout=layout.GetRewardItem(i);
                float itemScale=Mathf.Clamp(itemLayout.sizeScale,.25f,2f);
                float itemWidth=cellWidth*itemScale,itemHeight=cellHeight*itemScale;
                float itemX=left+i*cellWidth+(cellWidth-itemWidth)/2+itemLayout.positionOffset.x;
                float itemY=(height-cellHeight)/2+(cellHeight-itemHeight)/2-itemLayout.positionOffset.y;
                Place(item,itemX,itemY,itemWidth,itemHeight);
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
        public void RetryLeaderboard()
        {
            if(apiClient)apiClient.RefreshNow();
            ShowDialog("REFRESHING LIVE BOARD","Loading the current 30-minute reward round from the Battle Cities API.");
        }
        public void ConnectWallet()
        {
            EnsureApiClient();
            if(apiClient)apiClient.ConnectWallet();
        }
        public void ContinueAsGuest()
        {
            EnsureApiClient();
            if(apiClient)apiClient.ContinueAsGuest();
        }
        private void ShowDialog(string title,string message)
        {
            previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            modalTitle.text=title;modalBody.text=message;modal.gameObject.SetActive(true);modal.SetAsLastSibling();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }
        private void EnsureApiClient()
        {
            if(apiClient)return;
            apiClient=GetComponent<MainMenuApiClient>();
            if(!apiClient)apiClient=gameObject.AddComponent<MainMenuApiClient>();
        }

        private void BindApiClient()
        {
            if(!apiClient)return;
            apiClient.PlayerLoaded-=OnApiPlayerLoaded;
            apiClient.LeaderboardLoaded-=OnApiLeaderboardLoaded;
            apiClient.StatusChanged-=OnApiStatusChanged;
            apiClient.PlayerLoaded+=OnApiPlayerLoaded;
            apiClient.LeaderboardLoaded+=OnApiLeaderboardLoaded;
            apiClient.StatusChanged+=OnApiStatusChanged;
            if(playerLabel)
            {
                var walletTarget=playerLabel.transform.parent.gameObject;
                walletLoginButton=walletTarget.GetComponent<Button>();
                if(!walletLoginButton)walletLoginButton=walletTarget.AddComponent<Button>();
                walletLoginButton.transition=Selectable.Transition.None;
                walletLoginButton.onClick.RemoveListener(ConnectWallet);
                walletLoginButton.onClick.AddListener(ConnectWallet);
            }
            apiClient.RefreshNow();
        }

        private void UnbindApiClient()
        {
            if(!apiClient)return;
            apiClient.PlayerLoaded-=OnApiPlayerLoaded;
            apiClient.LeaderboardLoaded-=OnApiLeaderboardLoaded;
            apiClient.StatusChanged-=OnApiStatusChanged;
            if(walletLoginButton)walletLoginButton.onClick.RemoveListener(ConnectWallet);
        }

        private void OnApiPlayerLoaded(MainMenuApiClient.PlayerSnapshot player)
        {
            if(player==null)return;
            var lastScore=PlayerPrefs.GetInt("battlecities.lastScore",0);
            var highScore=Mathf.Max(PlayerPrefs.GetInt("battlecities.highScore",0),player.highscorePrimary);
            PlayerPrefs.SetString("battlecities.playerName",player.displayName??"COMMANDER");
            PlayerPrefs.SetInt("battlecities.highScore",highScore);
            PlayerPrefs.Save();
            SetPlayer(player.displayName,lastScore,highScore);
        }

        private void OnApiLeaderboardLoaded(string[] formattedRows)
        {
            SetLeaderboard(formattedRows);
        }

        private void OnApiStatusChanged(string title,string detail)
        {
            if(leaderboardDetail)leaderboardDetail.text=string.IsNullOrWhiteSpace(detail)?title:detail;
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
