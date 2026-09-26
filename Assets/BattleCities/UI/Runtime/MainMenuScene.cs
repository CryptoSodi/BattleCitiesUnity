using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Screen = UnityEngine.Device.Screen;

namespace BattleCities.UI
{
    public enum MainMenuPlatform { Auto, Web, Psg1, Android, AndroidLandscape }


    [Serializable]
    public sealed class RewardItemLayoutSettings
    {
        [Tooltip("Additional pixel offset for this reward. Positive Y moves it upward.")]
        public Vector2 positionOffset;
        [Tooltip("Uniform size multiplier. 1 keeps the automatically fitted size and aspect ratio.")]
        [Range(.25f, 2f)] public float sizeScale = 1;

        [Header("Podium Text")]
        [Tooltip("Moves both text lines inside this podium. Positive Y moves them upward.")]
        public Vector2 podiumTextOffset;
        [Tooltip("Additional offset for the placement label (1ST, 2ND, 3RD, or 4TH-10TH). Positive Y moves it upward.")]
        public Vector2 rankTextOffset;
        [Tooltip("Maximum placement-label font size. Best Fit can reduce it when necessary to prevent clipping.")]
        [Min(1)] public int rankFontSize = 38;
        [Tooltip("Additional offset for the reward amount. Positive Y moves it upward.")]
        public Vector2 amountTextOffset;
        [Tooltip("Maximum reward-amount font size. Best Fit can reduce it when necessary to prevent clipping.")]
        [Min(1)] public int amountFontSize = 30;
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
        [Min(0)] public float navigationSidePadding = 10;
        [Min(0)] public float navigationButtonGap = 4;
        [Min(0)] public float navigationVerticalPadding = 10;
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

        [Header("Reward Text")]
        [Tooltip("Top edge of the shared reward text panel, normalized against an unscaled reward cell.")]
        [Range(0, 1)] public float rewardTextPanelTop = .635f;
        [Range(.05f, .5f)] public float rewardTextPanelHeight = .27f;
        [Range(0, .5f)] public float rewardRankTop = .04f;
        [Range(.1f, .7f)] public float rewardRankHeight = .40f;
        [Range(0, .8f)] public float rewardAmountTop = .48f;
        [Range(.1f, .7f)] public float rewardAmountHeight = .40f;
        [Min(1)] public int rewardRankMinSize = 18;
        [Min(1)] public int rewardRankMaxSize = 38;
        [Min(1)] public int rewardAmountMinSize = 16;
        [Min(1)] public int rewardAmountMaxSize = 30;

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
                navigationSidePadding = 10,
                navigationButtonGap = 4,
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
                navigationSidePadding = 10,
                navigationButtonGap = 4,
                navigationVerticalPadding = 10,
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

        public static MainMenuLayoutSettings AndroidLandscape()
        {
            return new MainMenuLayoutSettings
            {
                referenceResolution = new Vector2(1871, 841),
                outerPadding = 12,
                headerHeight = 80,
                headerToBodyGap = 6,
                navigationWidthRatio = .64f,
                navigationSidePadding = 10,
                navigationButtonGap = 4,
                navigationVerticalPadding = 10,
                mainToNavigationGap = 8,
                portraitHowItWorksHeight = 0,
                rewardsHeightRatio = .30f,
                startMaxWidth = 400,
                selectHintHeight = 0
            };
        }
    }

    /// <summary>Shared live UI with platform composition; art and labels are independent assets.</summary>
    [ExecuteAlways]
    public sealed partial class MainMenuScene : MonoBehaviour
    {
        [SerializeField] private MainMenuPlatform platform = MainMenuPlatform.Auto;
        [SerializeField] private string gameplayScene = "BattleCity";
        [SerializeField] private string quartersScene, shopScene, socialsScene;
        [SerializeField] private MenuTheme theme;

        [Header("Automatic Layout Defaults (used to initialize new platform layouts)")]
        [SerializeField] private MainMenuLayoutSettings webLayout = MainMenuLayoutSettings.Web();
        [SerializeField] private MainMenuLayoutSettings psg1Layout = MainMenuLayoutSettings.Psg1();
        [SerializeField] private MainMenuLayoutSettings androidLayout = MainMenuLayoutSettings.Android();
        [SerializeField] private MainMenuLayoutSettings androidLandscapeLayout = MainMenuLayoutSettings.AndroidLandscape();
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
        private readonly List<Text[]> rankingCells = new List<Text[]>();
        private Text roundLabel, roundHeading, rankingAvailability, rankingTitle, matchesColumn, rankingFooter, rankingFooterSubtitle;
        private RectTransform scoresPanel;
        private GameObject emptyTrophy;
        private MainMenuApiClient.RoundSnapshot roundSnapshot;
        private DateTimeOffset roundEndsAt;
        private bool hasRoundEnd;
        private float nextRoundUpdate;
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
        public MainMenuLayoutSettings AndroidLandscapeLayout => androidLandscapeLayout;

        private MainMenuLayoutSettings GetLayout(MainMenuPlatform target)
        {
            if(target==MainMenuPlatform.Psg1)return psg1Layout ?? (psg1Layout=MainMenuLayoutSettings.Psg1());
            if(target==MainMenuPlatform.Android)return androidLayout ?? (androidLayout=MainMenuLayoutSettings.Android());
            if(target==MainMenuPlatform.AndroidLandscape)return androidLandscapeLayout ?? (androidLandscapeLayout=MainMenuLayoutSettings.AndroidLandscape());
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
            if (Application.isPlaying && Time.unscaledTime >= nextRoundUpdate)
            {
                nextRoundUpdate = Time.unscaledTime + 1;
                UpdateRoundCountdown();
            }
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
            if(platform!=MainMenuPlatform.Auto)
                return platform==MainMenuPlatform.Android && size.x>size.y ? MainMenuPlatform.AndroidLandscape : platform;
            if(RuntimePlatformInfo.IsPsg1)return MainMenuPlatform.Psg1;
            if(RuntimePlatformInfo.Current==GameRuntimePlatform.Android)
                return size.x>size.y ? MainMenuPlatform.AndroidLandscape : MainMenuPlatform.Android;
            if(size.x/Mathf.Max(1,size.y)<.8f)return MainMenuPlatform.Android;
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
            if(target==MainMenuPlatform.Android && available.x>available.y)
                target=MainMenuPlatform.AndroidLandscape;
            if(editLayoutInScene && !generatingLayout)
            {
                ApplyAuthoredLayout(target,available);
                ApplyArenaBackdrop(target,available);
                return;
            }
            bool psg=target==MainMenuPlatform.Psg1;
            bool portrait=target==MainMenuPlatform.Android && available.y>available.x;
            bool compact=psg || target==MainMenuPlatform.Android || target==MainMenuPlatform.AndroidLandscape;
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
            else if(psg)
            {
                float hintsH=hideControllerLegend?0:layout.controllerLegendHeight;
                float hintsGap=hideControllerLegend?0:layout.navigationToLegendGap;
                float bodyH=h-bodyY-pad-hintsH-hintsGap;
                float buttonH=(bodyH-2*compactVertical-4*compactGap)/tabs.Length;
                float navW=buttonH*NavigationAspect+2*compactSide;
                Place(navigation,pad,bodyY,navW,bodyH);
                Place(mainFrame,pad+navW+layout.mainToNavigationGap,bodyY,w-2*pad-navW-layout.mainToNavigationGap,bodyH);
                float controlsInset=layout.controllerLegendSideInset;
                Place(controls,controlsInset,h-pad-hintsH,w-2*controlsInset,hintsH);
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
                if(psg || !compact)
                {
                    float buttonH=(nh-2*compactVertical-4*compactGap)/tabs.Length;
                    Place((RectTransform)tabs[i].transform,compactSide,compactVertical+i*(buttonH+compactGap),nw-2*compactSide,buttonH);
                }
                else if(compact)
                {
                    float buttonW=(nw-compactSide*2-compactGap*4)/5;
                    Place((RectTransform)tabs[i].transform,compactSide+i*(buttonW+compactGap),compactVertical,buttonW,buttonW/NavigationAspect);
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
            ConfigureNavigation(compact,psg);
            if(Application.isPlaying && (target!=lastPlatform || inputModule.actionsAsset!=liveInput))ConfigureInput(psg);
            lastPlatform=target;
            ApplyArenaBackdrop(target,available);
        }
        // Navigation plates fill their slots; their reference composition is slightly wider than tall.
        private const float NavigationAspect=9f/8f;
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
                float width=(cell-12)*(i==1?.8f:.86f);
                if(i==1)width=Mathf.Min(width,statusBar.sizeDelta.y*image.sprite.rect.width/image.sprite.rect.height);
                float height=statusBar.sizeDelta.y;
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
                banner.anchorMin=new Vector2(0,bannerAtBottom?-.016f:.824f);
                banner.anchorMax=new Vector2(1,bannerAtBottom?.176f:1.016f);
                banner.pivot=new Vector2(.5f,.5f);
                banner.sizeDelta=new Vector2(-width*.06812f,0);
                banner.anchoredPosition=new Vector2(width*.00208f,bannerAtBottom?0:-verticalNudge)+layout.rewardBannerOffset;
                ConfigureRewardHeaderIcons(banner);
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

                var podium=art.Find("Podium") as RectTransform;
                if(!podium)continue;
                float panelTop=layout.rewardTextPanelTop>.01f?layout.rewardTextPanelTop:.635f;
                float panelHeight=layout.rewardTextPanelHeight>.01f?layout.rewardTextPanelHeight:.27f;
                podium.anchorMin=new Vector2(.12f,1-panelTop-panelHeight);
                podium.anchorMax=new Vector2(.88f,1-panelTop);
                podium.pivot=new Vector2(.5f,.5f);
                podium.offsetMin=podium.offsetMax=itemLayout.podiumTextOffset;

                int rankMax=itemLayout.rankFontSize>0?itemLayout.rankFontSize:Mathf.Max(1,layout.rewardRankMaxSize);
                int rankMin=Mathf.Clamp(layout.rewardRankMinSize,1,rankMax);
                int amountMax=itemLayout.amountFontSize>0?itemLayout.amountFontSize:Mathf.Max(1,layout.rewardAmountMaxSize);
                int amountMin=Mathf.Clamp(layout.rewardAmountMinSize,1,amountMax);
                ConfigureRewardText(podium.Find("Rank")?.GetComponent<Text>(),layout.rewardRankTop,layout.rewardRankHeight,itemLayout.rankTextOffset,rankMin,rankMax);
                ConfigureRewardText(podium.Find("Reward")?.GetComponent<Text>(),layout.rewardAmountTop,layout.rewardAmountHeight,itemLayout.amountTextOffset,amountMin,amountMax);
            }
        }
        private void ConfigureRewardHeaderIcons(RectTransform banner)
        {
            if(!banner||!theme)return;
            var trophyRect=banner.Find("Trophy") as RectTransform;
            var trophyImage=trophyRect?trophyRect.GetComponent<UnityEngine.UI.Image>():null;
            if(trophyImage&&theme.TrophyIcon)
            {
                trophyImage.sprite=theme.TrophyIcon;
                trophyImage.preserveAspect=true;
            }

            var timerRect=banner.Find("Timer") as RectTransform;
            if(!theme.TimerIcon)
            {
                if(timerRect)timerRect.gameObject.SetActive(false);
                return;
            }
            if(!timerRect)
            {
                var timerObject=new GameObject("Timer",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));
                timerRect=timerObject.GetComponent<RectTransform>();
                timerRect.SetParent(banner,false);
            }
            timerRect.gameObject.SetActive(true);
            timerRect.anchorMin=new Vector2(.925f,.08f);
            timerRect.anchorMax=new Vector2(.98f,.92f);
            timerRect.pivot=new Vector2(.5f,.5f);
            timerRect.offsetMin=timerRect.offsetMax=Vector2.zero;
            timerRect.SetAsLastSibling();
            var timerImage=timerRect.GetComponent<UnityEngine.UI.Image>();
            timerImage.sprite=theme.TimerIcon;
            timerImage.preserveAspect=true;
            timerImage.raycastTarget=false;

            var title=banner.Find("Title") as RectTransform;
            if(title)
            {
                title.anchorMin=new Vector2(.105f,.02f);
                title.anchorMax=new Vector2(.905f,.98f);
                title.offsetMin=title.offsetMax=Vector2.zero;
            }
        }
        private static void ConfigureRewardText(Text label,float top,float height,Vector2 offset,int minSize,int maxSize)
        {
            if(!label)return;
            var rect=label.rectTransform;
            top=Mathf.Clamp01(top);height=Mathf.Clamp(height,.1f,1-top);
            rect.anchorMin=new Vector2(.02f,1-top-height);
            rect.anchorMax=new Vector2(.98f,1-top);
            rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=rect.offsetMax=offset;
            label.fontSize=maxSize;label.resizeTextForBestFit=true;
            label.resizeTextMinSize=minSize;label.resizeTextMaxSize=maxSize;
            label.alignment=TextAnchor.MiddleCenter;label.alignByGeometry=true;label.fontStyle=FontStyle.Bold;
            var shadow=label.GetComponent<Shadow>();
            if(shadow){shadow.effectColor=new Color(0,0,.02f,.85f);shadow.effectDistance=new Vector2(2,-2);}
        }
        private static void Link(Button b,Selectable up,Selectable down,Selectable left,Selectable right)
        { b.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,selectOnLeft=left,selectOnRight=right}; }
        private void ConfigureNavigation(bool compact,bool leftSidebar=false)
        {
            if(leftSidebar)
            {
                Link(startButton,settingsButton,tabs[0],tabs[0],startButton);
                Link(settingsButton,tabs[0],startButton,tabs[0],startButton);
                for(int i=0;i<tabs.Length;i++)
                    Link(tabs[i],tabs[(i+tabs.Length-1)%tabs.Length],tabs[(i+1)%tabs.Length],tabs[i],startButton);
                Link(closeButton,closeButton,closeButton,closeButton,closeButton);
                return;
            }
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
            else if(preBattle && preBattle.IsOpen)preBattle.Back();
            else if(EventSystem.current)EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
        private PreBattleScreen preBattle;
        public void StartBattle()
        {
            if(loading)return;
            EnsureApiClient();
            if(!preBattle)
            {
                preBattle=gameObject.AddComponent<PreBattleScreen>();
                preBattle.Configure(mainFrame,theme,apiClient,LaunchPreparedBattle,startButton);
            }
            preBattle.Open();
        }
        private async void LaunchPreparedBattle()
        {
            if(loading)return;
            if(BattleCities.Multiplayer.BattleLaunchOptions.Error!=null){ShowDialog("INVALID LAUNCH MODE",BattleCities.Multiplayer.BattleLaunchOptions.Error);return;}
            if(!Application.CanStreamedLevelBeLoaded(gameplayScene)){ShowDialog("BATTLE UNAVAILABLE","The gameplay scene is not included in this build.");return;}
            if(BattleCities.Multiplayer.BattleLaunchOptions.Mode==BattleCities.Core.BattleMode.Offline)
            {
                loading=true;startButton.interactable=false;
                await SceneManager.LoadSceneAsync(gameplayScene);
                return;
            }
            var session=BattleCities.Multiplayer.BattleSession.Instance;
            if(!session){ShowDialog("BATTLE UNAVAILABLE","The multiplayer session has not initialized.");return;}
            session.SelectedMode=BattleCities.Multiplayer.BattleLaunchOptions.Mode;
            loading=true; startButton.interactable=false;
            session.Lobby.Show();
            await session.QuickMatch();
            if(this){loading=false;startButton.interactable=true;}
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
        public void OpenRanking() { ShowDialog("PLAYER RANKINGS",rows.Length>0?string.Join("\n",rows):"No season rankings are available right now. Retry in a moment."); }
        public void RetryLeaderboard()
        {
            if(apiClient)apiClient.RefreshNow();
            ShowDialog("REFRESHING LIVE DATA","Loading the current round and season rankings from the Battle Cities API.");
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
            ResolveLeaderboardUi();
            apiClient.PlayerLoaded-=OnApiPlayerLoaded;
            apiClient.RankingsLoaded-=OnApiRankingsLoaded;
            apiClient.RoundLoaded-=OnApiRoundLoaded;
            apiClient.StatusChanged-=OnApiStatusChanged;
            apiClient.PlayerLoaded+=OnApiPlayerLoaded;
            apiClient.RankingsLoaded+=OnApiRankingsLoaded;
            apiClient.RoundLoaded+=OnApiRoundLoaded;
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
            if (apiClient.LastRankings != null) OnApiRankingsLoaded(apiClient.LastRankings);
            if (apiClient.LastRound != null) OnApiRoundLoaded(apiClient.LastRound);
            apiClient.RefreshNow();
        }

        private void UnbindApiClient()
        {
            if(!apiClient)return;
            apiClient.PlayerLoaded-=OnApiPlayerLoaded;
            apiClient.RankingsLoaded-=OnApiRankingsLoaded;
            apiClient.RoundLoaded-=OnApiRoundLoaded;
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
            var commander=statusBar?statusBar.Find("Player/Readout"):null;
            if(commander)
            {
                var level=commander.Find("Value");
                if(level)level.GetComponent<Text>().text="LVL "+Mathf.Max(1,player.level);
                var progress=commander.Find("Progress/Fill");
                if(progress)progress.GetComponent<Image>().fillAmount=Mathf.Clamp01((float)player.levelPoints/Mathf.Max(1,player.levelPointsRequired));
            }
        }

        private void ResolveLeaderboardUi()
        {
            if (!leaderboard) return;
            roundLabel = leaderboard.Find("Round status/Label")?.GetComponent<Text>();
            roundHeading = leaderboard.Find("Round status/Heading")?.GetComponent<Text>();
            rankingAvailability = leaderboard.Find("Heading/Availability")?.GetComponent<Text>();
            rankingTitle = leaderboard.Find("Heading/Title")?.GetComponent<Text>();
            matchesColumn = leaderboard.Find("Columns/Reward")?.GetComponent<Text>();
            rankingFooter = leaderboard.Find("Footer/Label")?.GetComponent<Text>();
            rankingFooterSubtitle = leaderboard.Find("Footer/Subtitle")?.GetComponent<Text>();
            scoresPanel = leaderboard.Find("Scores") as RectTransform;
            emptyTrophy = leaderboard.Find("Scores/Empty state trophy")?.gameObject;
            if (rankingTitle) rankingTitle.text = "PLAYER RANKINGS";
            if (matchesColumn) matchesColumn.text = "MATCHES";
            if (rankingFooter) rankingFooter.text = "SEASON STANDINGS";
            if (rankingFooterSubtitle)
            {
                rankingFooterSubtitle.resizeTextForBestFit = true;
                rankingFooterSubtitle.resizeTextMinSize = 15;
                rankingFooterSubtitle.resizeTextMaxSize = 21;
            }
            if (rankingAvailability) rankingAvailability.text = "LOADING RANKINGS";
            if (leaderboardMessage) leaderboardMessage.text = "LOADING RANKINGS";
            if (leaderboardDetail) leaderboardDetail.text = "Fetching live player standings.";
        }

        private void OnApiRankingsLoaded(MainMenuApiClient.RankingsSnapshot snapshot)
        {
            var ranked = snapshot?.rows;
            var hasRows = ranked != null && ranked.Length > 0;
            if (rankingAvailability) rankingAvailability.text = snapshot == null ? "RANKINGS UNAVAILABLE" :
                "LIVE  •  " + (string.IsNullOrWhiteSpace(snapshot.seasonName) ? "CURRENT SEASON" : snapshot.seasonName.ToUpperInvariant());
            if (emptyTrophy) emptyTrophy.SetActive(!hasRows);
            if (leaderboardMessage)
            {
                leaderboardMessage.gameObject.SetActive(!hasRows);
                leaderboardMessage.text = snapshot == null ? "RANKINGS UNAVAILABLE" : "NO PLAYERS RANKED YET";
            }
            if (leaderboardDetail)
            {
                leaderboardDetail.gameObject.SetActive(!hasRows);
                leaderboardDetail.text = snapshot == null ? "Retry to load live player standings." : "Play a ranked match to join the standings.";
            }
            rows = hasRows ? new string[ranked.Length] : Array.Empty<string>();
            if (hasRows && scoresPanel) EnsureRankingCells(ranked.Length);
            for (var index = 0; index < rankingCells.Count; index++)
            {
                var visible = hasRows && index < ranked.Length;
                foreach (var cell in rankingCells[index]) cell.gameObject.SetActive(visible);
                if (!visible) continue;
                var row = ranked[index];
                rankingCells[index][0].text = row.rank.ToString();
                rankingCells[index][1].text = string.IsNullOrWhiteSpace(row.displayName) ? "UNKNOWN" : row.displayName;
                rankingCells[index][2].text = row.totalPoints.ToString("N0", CultureInfo.InvariantCulture);
                rankingCells[index][3].text = row.matches.ToString("N0", CultureInfo.InvariantCulture);
                rows[index] = string.Format(CultureInfo.InvariantCulture, "{0}. {1}  •  {2:N0} points  •  {3:N0} matches",
                    row.rank, rankingCells[index][1].text, row.totalPoints, row.matches);
            }
        }

        private void EnsureRankingCells(int count)
        {
            while (rankingCells.Count < count)
            {
                var index = rankingCells.Count;
                var cells = new Text[4];
                var names = new[] { "Rank", "Player", "Score", "Matches" };
                var left = new[] { .025f, .15f, .53f, .77f };
                var widths = new[] { .10f, .36f, .22f, .21f };
                for (var column = 0; column < cells.Length; column++)
                {
                    var child = new GameObject("Live " + (index + 1) + " " + names[column], typeof(RectTransform), typeof(Text));
                    child.transform.SetParent(scoresPanel, false);
                    var rect = (RectTransform)child.transform;
                    var top = .025f + index * .069f;
                    rect.anchorMin = new Vector2(left[column], 1 - top - .064f);
                    rect.anchorMax = new Vector2(left[column] + widths[column], 1 - top);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    var cell = child.GetComponent<Text>();
                    cell.font = theme && theme.BodyFont ? theme.BodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    cell.fontSize = 20;
                    cell.resizeTextForBestFit = true;
                    cell.resizeTextMinSize = 13;
                    cell.resizeTextMaxSize = 20;
                    cell.color = new Color(.04f, .14f, .20f);
                    cell.alignment = column == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                    cell.horizontalOverflow = HorizontalWrapMode.Wrap;
                    cell.verticalOverflow = VerticalWrapMode.Truncate;
                    cell.supportRichText = false;
                    cell.raycastTarget = false;
                    cells[column] = cell;
                }
                rankingCells.Add(cells);
            }
        }

        private void OnApiRoundLoaded(MainMenuApiClient.RoundSnapshot snapshot)
        {
            roundSnapshot = snapshot;
            hasRoundEnd = snapshot != null && DateTimeOffset.TryParse(snapshot.endsAt,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out roundEndsAt);
            if (roundHeading) roundHeading.text = "TOP 10 EVERY 30 MINUTES";
            if (rankingFooterSubtitle)
            {
                var count = snapshot?.rows?.Length ?? 0;
                rankingFooterSubtitle.text = snapshot == null ? "Round status unavailable" :
                    string.Format(CultureInfo.InvariantCulture, "{0} SCORES  •  PAYOUTS {1}",
                        count, snapshot.payoutsEnabled ? "ACTIVE" : "PAUSED");
            }
            UpdateRoundCountdown();
        }

        private void UpdateRoundCountdown()
        {
            if (!roundLabel) return;
            if (!hasRoundEnd)
            {
                roundLabel.text = roundSnapshot == null ? "ROUND UNAVAILABLE" : "ROUND TIME UNAVAILABLE";
                return;
            }
            var remaining = roundEndsAt - DateTimeOffset.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            roundLabel.text = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds);
        }

        private void OnApiStatusChanged(string title,string detail)
        {
            // Authentication status is unrelated to the public rankings and round panels.
        }

        public void SetPlayer(string displayName,int score,int highScore)
        { playerLabel.text=string.IsNullOrEmpty(displayName)?"COMMANDER":displayName;scoreLabel.text=Mathf.Max(0,score).ToString("D6");highScoreLabel.text=Mathf.Max(0,highScore).ToString("D6"); }
        public void SetLeaderboard(string[] formattedRows)
        {
            rows=formattedRows??Array.Empty<string>();
            if (leaderboardMessage) leaderboardMessage.text=rows.Length>0?string.Join("\n",rows):"RANKINGS UNAVAILABLE";
            if (leaderboardDetail) leaderboardDetail.text=rows.Length>0?"":"Retry to load live player standings.";
        }
    }
}
