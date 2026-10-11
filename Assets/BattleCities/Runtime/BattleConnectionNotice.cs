using BattleCities.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities
{
    // A wallet battle stays at tick zero until its server session is ready.
    public sealed class BattleConnectionNotice : MonoBehaviour
    {
        BattleGame game;
        GameObject surface;
        TMP_Text message,heading,caption;
        RectTransform safeArea,frame,content,header,footer;
        PreBattleArt art;
        bool? captionGold;
        Vector2 lastCanvasSize;
        Rect lastSafe;
        Button back;
        float waitingSince;
        readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        public void Initialize(BattleGame owner){game=owner;waitingSince=Time.unscaledTime;}
        void Update()
        {
            bool visible=game&&!game.ReplayReady&&!game.IsReplaying;
            if(!visible){waitingSince=Time.unscaledTime;if(surface)surface.SetActive(false);return;}
            if(Time.unscaledTime-waitingSince<.5f)return;
            if(!surface)Build();
            if(!surface.activeSelf)
            {
                surface.SetActive(true);
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(back.gameObject);
            }
            message.text=game.ReplayStatus;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        Image Panel(string name,Transform parent,Vector2 position,Vector2 size,Sprite sprite,Color color)
        {var i=Rect(name,parent,position,size).gameObject.AddComponent<Image>();i.sprite=sprite;i.type=Image.Type.Sliced;i.color=color;return i;}
        TMP_Text Text(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize,bool white)
        {
            var t=Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            t.font=ArcadeTextStyles.HeadingSdf;t.text=value;t.richText=false;t.fontSize=fontSize;
            t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=fontSize;t.alignment=TextAlignmentOptions.Center;
            t.textWrappingMode=TextWrappingModes.Normal;t.raycastTarget=false;styles.ApplyCleanButton(t,!white);return t;
        }
        void Build()
        {
            art=Resources.Load<PreBattleArt>("PreBattleArt");
            var theme=Resources.Load<BattleResultsArt>("BattleResultsArt").theme;
            surface=new GameObject("Battle connection",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            surface.transform.SetParent(transform,false);
            var canvas=surface.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scale=surface.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            var shade=Panel("Backdrop",surface.transform,Vector2.zero,Vector2.zero,null,new Color(0,.03f,.08f,.65f));
            Stretch(shade.rectTransform);
            safeArea=Rect("Safe area",surface.transform,Vector2.zero,Vector2.zero);
            frame=Panel("TV frame",safeArea,Vector2.zero,new Vector2(760,320),theme.BlueFrame,Color.white).rectTransform;
            frame.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            content=Panel("Connection status",frame,Vector2.zero,Vector2.zero,theme.CreamPanel,Color.white).rectTransform;
            Stretch(content);content.offsetMin=Vector2.one*10;content.offsetMax=Vector2.one*-10;
            content.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            header=Panel("Title",content,Vector2.zero,Vector2.zero,art.tankTitlePanel,Color.white).rectTransform;
            header.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            heading=Text("Heading",header,"CONNECTING BATTLE",Vector2.zero,Vector2.zero,28,false);
            styles.Apply(heading,ArcadeTextTreatment.Gold);heading.alignment=TextAlignmentOptions.MidlineLeft;
            message=Text("Message",content,"Connecting...",Vector2.zero,Vector2.zero,27,false);
            footer=Panel("Footer",content,Vector2.zero,Vector2.zero,art.statusPanel,Color.white).rectTransform;
            TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),art.statusPanel);
            var button=Panel("Main menu",footer,Vector2.zero,Vector2.zero,art.tankCostButton,Color.white);
            back=button.gameObject.AddComponent<Button>();back.targetGraphic=button;back.transition=Selectable.Transition.SpriteSwap;
            back.spriteState=new SpriteState{highlightedSprite=art.tankCostButtonSelected,selectedSprite=art.tankCostButtonSelected,pressedSprite=art.tankCostButtonSelected,disabledSprite=art.tankCostButtonLocked};
            back.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=back,selectOnDown=back,selectOnLeft=back,selectOnRight=back};
            back.onClick.AddListener(()=>SceneManager.LoadScene("MainMenu"));
            caption=Text("Label",button.transform,"MAIN MENU",Vector2.zero,Vector2.zero,28,true);
            Stretch(caption.rectTransform);caption.rectTransform.offsetMin=new Vector2(6,2);caption.rectTransform.offsetMax=new Vector2(-6,-2);
            Layout();
            surface.SetActive(false);
        }
        static void Stretch(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void LateUpdate()
        {
            if(!surface||!surface.activeSelf)return;
            if(lastCanvasSize!=((RectTransform)surface.transform).rect.size||lastSafe!=Screen.safeArea)Layout();
            bool gold=back.image.overrideSprite==art.tankCostButtonSelected;
            if(captionGold!=gold){captionGold=gold;styles.ApplyCleanButton(caption,gold);}
        }
        void Layout()
        {
            var safe=Screen.safeArea;float sw=Mathf.Max(1,Screen.width),sh=Mathf.Max(1,Screen.height);
            safeArea.anchorMin=new Vector2(safe.xMin/sw,safe.yMin/sh);safeArea.anchorMax=new Vector2(safe.xMax/sw,safe.yMax/sh);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            frame.sizeDelta=new Vector2(Mathf.Min(760,Mathf.Max(1,safeArea.rect.width-24)),Mathf.Min(320,Mathf.Max(1,safeArea.rect.height-24)));
            var top=new TvTitleHeaderLayout(content);top.PlaceTitle(header,null,null);
            MainMenuScene.Place(heading.rectTransform,28,4,header.rect.width-56,top.Height-8);
            var bottom=new TvStatusFooterLayout(content);bottom.Place(footer);TvStatusFooterLayout.PlaceAction(back.image.rectTransform);
            MainMenuScene.Place(message.rectTransform,24,top.Height+18,content.rect.width-48,Mathf.Max(1,content.rect.height-top.Height-bottom.Height-36));
            Canvas.ForceUpdateCanvases();TvTitleHeaderLayout.FitSkin(back.image,back.image.rectTransform.rect.height);
            lastCanvasSize=((RectTransform)surface.transform).rect.size;lastSafe=safe;
        }
        void OnDestroy(){styles.Dispose();}
    }
}
