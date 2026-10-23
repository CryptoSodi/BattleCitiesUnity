using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Platform controls guide before saving/debiting a launch.</summary>
    public sealed partial class BattleGuideScreen : MonoBehaviour
    {
        public const string ScreenId = "battle-guide", PreferenceKey = "battlecities.web.controls.dismissed.v1";
        public static bool IsPsg1Platform => ScreenContent.Platform=="psg1";
        public static string ActivePreferenceKey => IsPsg1Platform?Psg1PreferenceKey:PreferenceKey;
        public static bool ShouldShow => IsSupportedPlatform && PlayerPrefs.GetInt(ActivePreferenceKey, 0) != 1;
        public static bool IsSupportedPlatform {
            get {
#if UNITY_EDITOR
                return (ScreenContent.Platform == "web" && !RuntimePlatformInfo.IsMobileBrowser) || IsPsg1Platform;
#elif UNITY_WEBGL
                return !RuntimePlatformInfo.IsMobileBrowser;
#else
                return RuntimePlatformInfo.IsPsg1;
#endif
            }
        }
        public static readonly Dictionary<string,string> Copy = new Dictionary<string,string> {
            {"title","BATTLE STATION"},{"continue","CONTINUE"},{"dont-show","DON'T SHOW AGAIN"},
            {"move-title","MOVE TANK"},{"move-body","Move up, left, down, and right."},
            {"aim-title","AIM TURRET"},{"aim-body","Aim independently while moving."},
            {"fire-title","FIRE"},{"fire-body","Tap and release Space to fire a normal shot."},
            {"charge-title","POWER SHOT"},{"charge-body","Hold Space for 1 second, then release to fire."},
            {"turret-title","HOW YOUR TURRET WORKS"},{"step-1-title","1  MOVE TO AIM"},
            {"step-1-body","Without turret input, the turret follows movement."},
            {"step-2-title","2  AIM AND RELEASE"},{"step-2-body","Aim in another direction and release Space to shoot."},
            {"step-3-title","3  SHOT FIRST, THEN TURN"},{"step-3-body","The shot keeps its release direction. The turret follows movement again after firing."},
            {"special-title","DEPLOY SPECIAL"},{"special-body","Press E or right-click to deploy."},
            {"switch-title","SWITCH SPECIAL"},{"switch-body","Only available when your tank has multiple special abilities."},
            {"powers-title","USE POWERUP"},{"powers-body","Activate the corresponding equipped powerup slot."}
        };
        public static readonly string[] ArtKeys = { "keycap", "tap", "charge", "normal-shell", "power-shell" };
        readonly ArcadeTextStyles styles = new ArcadeTextStyles();
        readonly List<RectTransform> cards = new List<RectTransform>();
        readonly List<BattleGuideTankPreview> tanks = new List<BattleGuideTankPreview>();
        MenuTheme theme; PreBattleArt skin; BattleGuideArt art;
        RectTransform bounds, header, footer, viewport, content, turretHeading;
        Image headerIcon;
        RectTransform swapHint;
        TMP_Text title; Button proceed; Toggle hideAgain; Action confirmed;
        Vector2 lastSize; bool built, needsLayout; int tier;
        Sprite slicedKeycap;
        bool psg1Guide;
        public bool IsOpen => gameObject.activeSelf;
        string Text(string key) => ScreenContent.Text(psg1Guide?Psg1ScreenId:ScreenId,key,psg1Guide?Psg1Copy[key]:Copy[key]);
        public void Configure(MenuTheme theme, PreBattleArt skin)
        {
            psg1Guide=IsPsg1Platform;
            this.theme=theme;this.skin=skin;art=Resources.Load<BattleGuideArt>("BattleGuideArt");
            if(psg1Guide)psgArt=Resources.Load<Psg1BattleGuideArt>("Psg1BattleGuideArt");
            bounds=(RectTransform)transform;
            var background=gameObject.AddComponent<Image>();background.sprite=theme.CreamPanel;
            background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=3;background.color=theme.Cream;
            background.raycastTarget=true;
            header=Panel("Title",bounds,skin?skin.tankTitlePanel:theme.BlueFrame);
            header.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            title=Label("Title",header,Text("title"),44,true);
            title.alignment=TextAlignmentOptions.MidlineLeft;
            headerIcon=Panel("Controller icon",header,ScreenContent.Sprite(ScreenId,"controller-icon",theme.RankingGamingIcon)).GetComponent<Image>();
            headerIcon.type=Image.Type.Simple;headerIcon.preserveAspect=true;
            if(psg1Guide)headerIcon.sprite=PsgArt("device",psgArt?psgArt.device:null)??headerIcon.sprite;
            footer=Panel("Footer",bounds,skin?skin.statusPanel:theme.CreamPanel);
            TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),skin?skin.statusPanel:theme.CreamPanel);
            proceed=ActionButton(footer);
            hideAgain=Checkbox(footer);
            var scrollObject=new GameObject("Instructions",typeof(RectTransform),typeof(ScrollRect));
            scrollObject.transform.SetParent(bounds,false);
            viewport=Panel("Viewport",scrollObject.transform,null);Fill(viewport);
            var mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            viewport.GetComponent<Image>().color=Color.white;viewport.GetComponent<Image>().raycastTarget=true;
            content=Panel("Content",viewport,null);content.GetComponent<Image>().enabled=false;
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            var scroll=scrollObject.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            if(psg1Guide)BuildPsg1Cards();else BuildCards();built=true;gameObject.SetActive(false);
        }
        public void Open(int selectedTier,Action continueAction)
        {
            if(!IsSupportedPlatform)return;
            tier=Mathf.Clamp(selectedTier,0,3);confirmed=continueAction;
            gameObject.SetActive(true);transform.SetAsLastSibling();
            hideAgain.SetIsOnWithoutNotify(PlayerPrefs.GetInt(psg1Guide?Psg1PreferenceKey:PreferenceKey,0)==1);
            for(int i=0;i<tanks.Count;i++)tanks[i].Configure(tier,i==1);
            foreach(var preview in tanks)preview.RenderPreview();
            Layout();content.anchoredPosition=Vector2.zero;
            if(psg1Guide)ConfigurePsg1Navigation();
            if(psg1Guide&&EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(proceed.gameObject);
        }
        public void Accept()
        {
            if(!IsOpen)return;
            var action=confirmed;confirmed=null;gameObject.SetActive(false);action?.Invoke();
        }
        public void Cancel(){confirmed=null;gameObject.SetActive(false);}
        void OnDisable(){confirmed=null;}
        void LateUpdate(){if(built&&(needsLayout||lastSize!=bounds.rect.size))Layout();if(psg1Guide&&IsOpen){KeepControllerFocus();UpdatePsg1Scrolling();}}
        void OnRectTransformDimensionsChange(){needsLayout=true;}
        void Layout()
        {
            if(!built)return;needsLayout=false;lastSize=bounds.rect.size;
            float headerHeight=Mathf.Clamp(bounds.rect.height*.095f,72,88),footerHeight=Mathf.Clamp(bounds.rect.height*.09f,72,84);
            Place(header,4,4,bounds.rect.width-8,headerHeight);
            float iconHeight=headerHeight*.64f;
            float iconWidth=headerIcon.sprite?Mathf.Min(headerHeight*.95f,iconHeight*headerIcon.sprite.rect.width/headerIcon.sprite.rect.height):iconHeight;
            Place(headerIcon.rectTransform,24,(headerHeight-iconHeight)*.5f,iconWidth,iconHeight);
            MainMenuScene.Place(title.rectTransform,24+iconWidth+12,8,header.rect.width-48-iconWidth-12,headerHeight-16);
            Place(footer,4,bounds.rect.height-footerHeight-4,bounds.rect.width-8,footerHeight);TvStatusFooterLayout.PlaceAction((RectTransform)proceed.transform);
            Place((RectTransform)hideAgain.transform,18,8,Mathf.Max(120,footer.rect.width*.72f-24),footerHeight-16);
            var checkBox=(RectTransform)hideAgain.transform.Find("Box");float checkSize=Mathf.Min(40,footerHeight-24);
            Place(checkBox,0,(footerHeight-16-checkSize)*.5f,checkSize,checkSize);
            var checkLabel=hideAgain.transform.Find("Caption") as RectTransform;
            Place(checkLabel,checkSize+14,0,((RectTransform)hideAgain.transform).rect.width-checkSize-14,footerHeight-16);
            if(skin&&skin.tankCostButton)TvTitleHeaderLayout.FitSkin(proceed.image,footerHeight*.78f);
            float y=headerHeight+14,height=Mathf.Max(1,bounds.rect.height-y-footerHeight-14);
            Place((RectTransform)viewport.parent,10,y,Mathf.Max(1,bounds.rect.width-20),height);
            if(psg1Guide){LayoutPsg1(width:viewport.rect.width,height:height);return;}
            float width=viewport.rect.width,gap=8;int columns=width>=1000?4:width>=440?2:1;
            float cardWidth=(width-gap*(columns-1))/columns;
            float topCard=Mathf.Clamp(height*.30f,210,260),stepCard=Mathf.Clamp(height*.30f,210,280),bottomCard=Mathf.Clamp(height*.28f,200,260);
            for(int i=0;i<4;i++)Place(cards[i],i%columns*(cardWidth+gap),i/columns*(topCard+gap),cardWidth,topCard);
            LayoutKeyGrid(cards[0],new[]{"W","A","S","D"});
            LayoutKeyGrid(cards[1],new[]{"↑","←","↓","→"});
            y=Mathf.Ceil(4f/columns)*(topCard+gap);
            Place(turretHeading,6,y,width-12,36);y+=40;
            int steps=width>=1100?3:1;float stepWidth=(width-gap*(steps-1))/steps;
            for(int i=0;i<3;i++)Place(cards[4+i],i%steps*(stepWidth+gap),y+i/steps*(stepCard+gap),stepWidth,stepCard);
            y+=Mathf.Ceil(3f/steps)*(stepCard+gap);
            int bottomColumns=width>=650?2:1;float bottomWidth=(width-gap*(bottomColumns-1))/bottomColumns;
            Place(cards[7],0,y,bottomWidth,bottomCard);
            Place(cards[8],bottomColumns==2?bottomWidth+gap:0,bottomColumns==2?y:y+bottomCard+gap,bottomWidth,bottomCard);
            LayoutSpecial(cards[7]);LayoutPowerups(cards[8]);
            y+=bottomColumns==2?bottomCard+gap:2*(bottomCard+gap);
            content.sizeDelta=new Vector2(0,y);content.anchoredPosition=new Vector2(0,Mathf.Clamp(content.anchoredPosition.y,0,Mathf.Max(0,y-height)));
            foreach(var key in content.GetComponentsInChildren<Image>())
                if(key.type==Image.Type.Sliced&&key.name.StartsWith("Key ")&&key.sprite&&key.sprite.border.x>0)
                    TvTitleHeaderLayout.FitSkin(key,key.rectTransform.rect.height,.18f);
        }
        void BuildCards()
        {
            var move=Card("Move", "move-title");KeyGrid(move,new[]{"W","A","S","D"});Body(move,"move-body",.76f);
            var aim=Card("Aim", "aim-title");KeyGrid(aim,new[]{"↑","←","↓","→"});Body(aim,"aim-body",.76f);
            ShotCard("Fire","fire-title","fire-body","tap",art?art.tap:null,"normal-shell",art?art.normalShell:null);
            ShotCard("Power shot","charge-title","charge-body","charge",art?art.charge:null,"power-shell",art?art.powerShell:null);
            turretHeading=Label("Turret heading",content,Text("turret-title"),32).rectTransform;
            for(int i=0;i<3;i++)
            {
                var card=Card("Step "+(i+1),"step-"+(i+1)+"-title");
                var modelBounds=(RectTransform)new GameObject("Tank bounds",typeof(RectTransform)).transform;modelBounds.SetParent(card,false);Area(modelBounds,new Rect(.10f,.19f,.8f,.44f));
                var shadowObject=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));shadowObject.transform.SetParent(modelBounds,false);
                Area((RectTransform)shadowObject.transform,new Rect(.23f,.66f,.54f,.22f));var shadow=shadowObject.GetComponent<TankGroundShadow>();shadow.color=new Color32(35,25,13,112);shadow.raycastTarget=false;
                var picture=(RectTransform)new GameObject("Tank",typeof(RectTransform)).transform;picture.SetParent(modelBounds,false);Fill(picture);
                var raw=picture.gameObject.AddComponent<RawImage>();raw.raycastTarget=false;
                raw.uvRect=new Rect(0,0,1,1);var fitter=picture.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=1.6f;
                tanks.Add(picture.gameObject.AddComponent<BattleGuideTankPreview>());
                var arrow=Label("Movement arrow",card,"→",36);arrow.color=new Color32(0,185,237,255);Area(arrow.rectTransform,new Rect(.75f,.39f,.19f,.22f));
                Body(card,"step-"+(i+1)+"-body",.64f);
            }
            var special=Card("Special","special-title");Key(special,"E",new Rect(.04f,.23f,.29f,.24f));
            var description=Label("Description",special,Text("special-body"),26);Area(description.rectTransform,new Rect(.37f,.21f,.59f,.28f));
            description.alignment=TextAlignmentOptions.MidlineLeft;
            swapHint=Label("Switch special",special,Text("switch-title"),28).rectTransform;Area(swapHint,new Rect(.04f,.52f,.92f,.15f));
            Key(special,"Q",new Rect(.045f,.70f,.12f,.25f));
            var switchDescription=Label("Switch availability",special,Text("switch-body"),24);Area(switchDescription.rectTransform,new Rect(.20f,.68f,.76f,.29f));
            switchDescription.alignment=TextAlignmentOptions.MidlineLeft;
            var powers=Card("Powerups","powers-title");
            var shop=RemoteGameArt.Load<ShopArt>("ShopArt");string[] examplePowers={"shield","freeze","speed","upgrade"};
            for(int i=0;i<4;i++)
            {
                Key(powers,(i+1).ToString(),new Rect(.035f+i*.24f,.21f,.20f,.28f));
                Sprite icon=null;
                for(int j=0;j<ShopCatalog.Products.Length;j++)if(ShopCatalog.Products[j].InventoryId==examplePowers[i]&&shop&&j<shop.products.Length)icon=shop.products[j];
                Illustration(powers,"Power example "+i,icon,new Rect(.045f+i*.24f,.49f,.18f,.25f));
            }
            Body(powers,"powers-body",.70f);
        }
        void ShotCard(string name,string heading,string body,string handKey,Sprite hand,string shellKey,Sprite shell)
        {
            var card=Card(name,heading);Key(card,"SPACE",new Rect(.18f,.2f,.64f,.25f));
            Illustration(card,handKey,hand,new Rect(.12f,.47f,.25f,.25f));
            Illustration(card,shellKey,shell,new Rect(.44f,.46f,.45f,.23f));Body(card,body,.76f);
        }
        RectTransform Card(string name,string heading)
        {
            var card=Panel(name,content,theme.CreamPanel);card.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            var caption=Label("Heading",card,Text(heading),30);caption.alignment=TextAlignmentOptions.MidlineLeft;
            Area(caption.rectTransform,new Rect(.04f,.035f,.92f,.14f));cards.Add(card);return card;
        }
        void KeyGrid(RectTransform card,string[] labels)
        {
            Key(card,labels[0],new Rect(.39f,.22f,.22f,.25f));
            for(int i=1;i<4;i++)Key(card,labels[i],new Rect(.15f+(i-1)*.24f,.49f,.22f,.25f));
        }
        static void LayoutKeyGrid(RectTransform card,string[] keys)
        {
            float size=Mathf.Min(60,card.rect.height*.25f,(card.rect.width-44)/3f),gap=6;
            float left=(card.rect.width-size*3-gap*2)*.5f,top=card.rect.height*.22f;
            Place((RectTransform)card.Find("Key "+keys[0]),left+size+gap,top,size,size);
            for(int i=1;i<4;i++)Place((RectTransform)card.Find("Key "+keys[i]),left+(i-1)*(size+gap),top+size+gap,size,size);
        }
        static void LayoutSpecial(RectTransform card)
        {
            float left=card.rect.width*.04f,size=Mathf.Min(56,card.rect.height*.24f),textLeft=left+size+18;
            Place((RectTransform)card.Find("Key E"),left,card.rect.height*.23f,size,size);
            Place((RectTransform)card.Find("Key Q"),left,card.rect.height*.70f,size,size);
            Place((RectTransform)card.Find("Description"),textLeft,card.rect.height*.23f,card.rect.width-textLeft-left,size);
            Place((RectTransform)card.Find("Switch availability"),textLeft,card.rect.height*.70f,card.rect.width-textLeft-left,size);
        }
        static void LayoutPowerups(RectTransform card)
        {
            float left=card.rect.width*.04f,gap=12,insideGap=8;
            float size=Mathf.Clamp((card.rect.width-left*2-gap*3)/8f-insideGap*.5f,30,74);
            float groupWidth=size*2+insideGap,top=card.rect.height*.26f;
            for(int i=0;i<4;i++)
            {
                float x=left+i*(groupWidth+gap);
                Place((RectTransform)card.Find("Key "+(i+1)),x,top,size,size);
                var number=card.Find("Key "+(i+1)+"/Caption").GetComponent<TMP_Text>();
                number.fontSize=number.fontSizeMax=44;
                Place((RectTransform)card.Find("Power example "+i),x+size+insideGap,top,size,size);
            }
        }
        void Key(RectTransform parent,string caption,Rect area)
        {
            var sprite=ScreenContent.Sprite(ScreenId,"keycap",art&&art.keycap?art.keycap:theme.CreamPanel);
            if(sprite&&sprite.border==Vector4.zero&&caption.Length>1)
            {
                if(!slicedKeycap)slicedKeycap=Sprite.Create(sprite.texture,sprite.rect,new Vector2(.5f,.5f),sprite.pixelsPerUnit,0,SpriteMeshType.FullRect,
                    new Vector4(sprite.rect.width*.1875f,sprite.rect.height*.1875f,sprite.rect.width*.1875f,sprite.rect.height*.1875f));
                sprite=slicedKeycap;
            }
            if(psg1Guide)sprite=PsgArt(caption=="L1"||caption=="R1"?"shoulder":"face",psgArt?(caption=="L1"||caption=="R1"?psgArt.shoulder:psgArt.face):sprite);
            var rect=Panel("Key "+caption,parent,sprite);Area(rect,area);
            var image=rect.GetComponent<Image>();image.type=caption.Length==1?Image.Type.Simple:Image.Type.Sliced;image.preserveAspect=caption.Length==1;
            if(psg1Guide){image.type=Image.Type.Simple;image.preserveAspect=true;}
            var label=Label("Caption",rect,caption,36);Fill(label.rectTransform,4);label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.anchoredPosition+=Vector2.up*3;
            if(psg1Guide){styles.ApplyCleanButton(label,false);label.fontSize=label.fontSizeMax=32;}
        }
        void Body(RectTransform parent,string key,float top)
        {
            var label=Label("Description",parent,Text(key),26);Area(label.rectTransform,new Rect(.04f,top,.92f,.98f-top));
            label.alignment=TextAlignmentOptions.MidlineLeft;label.textWrappingMode=TextWrappingModes.Normal;
        }
        void Illustration(RectTransform parent,string key,Sprite fallback,Rect area)
        {
            var image=Panel(key,parent,ScreenContent.Sprite(psg1Guide?Psg1ScreenId:ScreenId,key,fallback));Area(image,area);
            var graphic=image.GetComponent<Image>();graphic.type=Image.Type.Simple;graphic.preserveAspect=true;graphic.enabled=graphic.sprite;
        }
        Button ActionButton(RectTransform parent)
        {
            var rect=Panel("Continue",parent,skin?skin.tankCostButton:theme.BlueFrame);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.transition=Selectable.Transition.SpriteSwap;
            button.image.raycastTarget=true;
            if(skin)button.spriteState=new SpriteState{highlightedSprite=skin.tankCostButtonSelected,selectedSprite=skin.tankCostButtonSelected,pressedSprite=skin.tankCostButtonSelected};
            var caption=Label("Caption",rect,Text("continue"),30);Fill(caption.rectTransform,4);caption.alignment=TextAlignmentOptions.Center;
            caption.rectTransform.anchoredPosition+=Vector2.up*3;
            styles.ApplyCleanButton(caption,false);rect.gameObject.AddComponent<BattleGuideButtonCaption>().Configure(caption);
            button.onClick.AddListener(Accept);return button;
        }
        Toggle Checkbox(RectTransform parent)
        {
            var rect=Panel("Don't show again",parent,null);rect.GetComponent<Image>().color=Color.clear;rect.GetComponent<Image>().raycastTarget=true;
            var toggle=rect.gameObject.AddComponent<Toggle>();toggle.targetGraphic=rect.GetComponent<Image>();
            var box=Panel("Box",rect,art&&art.keycap?art.keycap:theme.CreamPanel);Place(box,0,3,28,28);box.GetComponent<Image>().type=Image.Type.Simple;box.GetComponent<Image>().preserveAspect=true;
            var mark=Label("Check",box,"X",22);Fill(mark.rectTransform,2);mark.alignment=TextAlignmentOptions.Center;mark.color=theme.Navy;
            toggle.graphic=mark;
            var label=Label("Caption",rect,Text("dont-show"),28);Area(label.rectTransform,new Rect(.12f,0,.88f,1));
            label.alignment=TextAlignmentOptions.MidlineLeft;label.textWrappingMode=TextWrappingModes.NoWrap;
            toggle.onValueChanged.AddListener(value=>{PlayerPrefs.SetInt(psg1Guide?Psg1PreferenceKey:PreferenceKey,value?1:0);PlayerPrefs.Save();});return toggle;
        }
        TMP_Text Label(string name,Transform parent,string caption,float size,bool gold=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var label=go.GetComponent<TextMeshProUGUI>();label.font=ArcadeTextStyles.HeadingSdf;label.text=caption;label.richText=false;
            label.fontSize=label.fontSizeMax=size;label.fontSizeMin=22;label.enableAutoSizing=true;label.raycastTarget=false;
            label.textWrappingMode=TextWrappingModes.Normal;styles.Apply(label,gold?ArcadeTextTreatment.Gold:ArcadeTextTreatment.PrizeAmount);return label;
        }
        static RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.raycastTarget=false;
            if(!sprite)image.color=Color.clear;return (RectTransform)go.transform;
        }
        static void Fill(RectTransform rect,float inset=0){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset;}
        static void Area(RectTransform rect,Rect area){rect.anchorMin=new Vector2(area.x,1-area.y-area.height);rect.anchorMax=new Vector2(area.xMax,1-area.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static void Place(RectTransform rect,float x,float y,float width,float height)=>MainMenuScene.Place(rect,x,y,width,height);
        void OnDestroy(){styles.Dispose();if(slicedKeycap){if(Application.isPlaying)Destroy(slicedKeycap);else DestroyImmediate(slicedKeycap);}}
    }
    public sealed class BattleGuideButtonCaption : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        TMP_Text caption; readonly ArcadeTextStyles styles=new ArcadeTextStyles();bool hovered,selected;
        public void Configure(TMP_Text text){caption=text;Refresh();}
        void Refresh(){if(caption)styles.ApplyCleanButton(caption,hovered||selected);}
        public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();}public void OnPointerExit(PointerEventData e){hovered=false;Refresh();}
        public void OnSelect(BaseEventData e){selected=true;Refresh();}public void OnDeselect(BaseEventData e){selected=false;Refresh();}
        void OnDestroy()=>styles.Dispose();
    }
}
