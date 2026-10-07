using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>TV-contained supply shop using the approved tank-selection UI assets.</summary>
    public sealed partial class ShopScreen : MonoBehaviour
    {
        const float ShopCardAspect=1f;
        MainMenuScene menu;MenuTheme theme;PreBattleArt art;ShopArt shopArt;MainMenuApiClient api;
        RectTransform root,heading,currencyBar,inventory,inventoryPaper,inventoryHeading,catalog,filterRow,filterBar,footer,swapPage,notice,noticeCard;
        RectTransform[] inventoryTiles=new RectTransform[8];
        TMP_Text[] inventoryCounts=new TMP_Text[8];
        readonly Image[] inventoryArt=new Image[8];
        readonly RawImage[] inventoryFallbacks=new RawImage[8];
        readonly TankGroundShadow[] inventoryShadows=new TankGroundShadow[8];
        TMP_Text itemCount,connection,skrBalance,solBalance,fuelBalance,noticeTitle,noticeBody,swapFrom,swapTo,swapBalance;
        TMP_InputField swapAmount;
        Button back,connect,swapDirection,swapAction,noticeClose;
        readonly Button[] currencyTabs=new Button[3],filters=new Button[5];
        readonly Button[] hudTabs=new Button[4];
        readonly Image[] hudSelections=new Image[4];
        readonly TMP_Text[] currencyLabels=new TMP_Text[3];
        readonly Image[] filterSelections=new Image[5];
        readonly TMP_Text[] filterLabels=new TMP_Text[5];
        readonly List<ButtonCaption> buttonCaptions=new List<ButtonCaption>();
        readonly List<Image> tokenIcons=new List<Image>();
        readonly Card[] cards=new Card[13];
        readonly List<int> visible=new List<int>();
        readonly ArcadeTextStyles textStyles=new ArcadeTextStyles();
        TankRosterScroll scroll;GridLayoutGroup grid;
        ShopCurrency currency;ShopCategory category;int selected,columns=4,focusedProduct=-1,hoveredProduct=-1;bool swapFromSol=true;
        JObject account;Coroutine accountRequest;
        [NonSerialized] bool configured;
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public bool IsConfigured=>configured&&root&&back&&menu;
        public RectTransform Root=>root;
        public ShopCurrency Currency=>currency;
        public ShopCategory Category=>category;
        public int SelectedProduct=>selected;
        public int VisibleProductCount=>visible.Count;
        public TankRosterScroll CatalogScroll=>scroll;
        sealed class Card {public RectTransform Rect;public Image Frame,Art,Coin;public RawImage Fallback;public TankGroundShadow Shadow;public CardSelectionHighlight Highlight;public Button Price;public TMP_Text Title,Reward,PriceText;}
        sealed class ButtonCaption {public Button Button;public TMP_Text Text;public bool? Navy;}

        public void Configure(MainMenuScene owner,MenuTheme skin,MainMenuApiClient client,RectTransform frame)
        {
            // Unity restores object references on script reload, but runtime arrays need rebinding.
            configured=false;
            if(api)api.PlayerLoaded-=OnPlayerLoaded;
            menu=owner;theme=skin;api=client;art=Resources.Load<PreBattleArt>("PreBattleArt");shopArt=Resources.Load<ShopArt>("ShopArt");
            if(api)api.PlayerLoaded+=OnPlayerLoaded;
            var existing=frame.Find("Shop screen");bool open=existing&&existing.gameObject.activeSelf;
            root=Panel("Shop screen",frame,null);root.GetComponent<Image>().raycastTarget=true;
            buttonCaptions.Clear();BuildView();RefreshArt();RefreshAccount();SetCurrency(currency==ShopCurrency.Swap?ShopCurrency.Skr:currency,false);
            root.gameObject.SetActive(open);
            configured=true;
        }
        void BuildView()
        {
            heading=Panel("Title plate",root,art.tankTitlePanel);heading.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            Icon("Shop icon",heading,theme.NavigationIcons[2],new Rect(.10f,.12f,.25f,.76f));
            Label("Title",heading,"SHOP",new Rect(.36f,.06f,.57f,.88f),28,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            BuildCurrencies();
            BuildInventory();
            catalog=Panel("Catalog",root,theme.CreamPanel);catalog.GetComponent<Image>().pixelsPerUnitMultiplier=2;
            var oldFilters=root.Find("Filters");if(oldFilters&&!catalog.Find("Filters"))oldFilters.SetParent(catalog,false);
            filterRow=Panel("Filters",catalog,art.tankTitlePanel);filterRow.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            BuildFilters();
            itemCount=Label("Item count",filterRow,"13 ITEMS",new Rect(.025f,.06f,.20f,.88f),TvTitleHeaderLayout.TextSize,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            BuildScroll();
            for(int i=0;i<cards.Length;i++)BuildCard(i);
            footer=Panel("Balances",root,art.statusPanel);TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),art.statusPanel);
            connect=MakeButton("Connection",footer,"CONNECT WALLET",()=>{if(api)api.ConnectWallet();});connection=connect.GetComponentInChildren<TMP_Text>();
            skrBalance=Balance("SKR",0,shopArt?shopArt.skr:null);solBalance=Balance("SOL",1,shopArt?shopArt.solana:null);fuelBalance=Balance("FUEL",2,art.fuelCan);
            BuildSwap();BuildNotice();BuildCheckout();
        }
        void BuildInventory()
        {
            inventory=Panel("Inventory",root,theme.BlueFrame);
            inventoryPaper=Panel("Paper",inventory,theme.Rounded);
            inventoryHeading=Panel("Heading bar",inventoryPaper,theme.Rounded);inventoryHeading.GetComponent<Image>().pixelsPerUnitMultiplier=5;
            var oldHeading=inventory.Find("Heading");if(oldHeading)oldHeading.SetParent(inventoryHeading,false);
            Icon("Crate",inventoryHeading,shopArt?shopArt.supplyCrate:null,new Rect(.025f,.10f,.18f,.80f));
            Label("Heading",inventoryHeading,"INVENTORY",new Rect(.23f,.02f,.74f,.96f),24,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            for(int i=0;i<8;i++)
            {
                string id=ShopCatalog.InventoryIds[i];var old=inventory.Find(id);if(old)old.SetParent(inventoryPaper,false);
                var row=inventoryTiles[i]=Panel(id,inventoryPaper,null);
                var oldIcon=row.Find("Icon bounds");if(oldIcon)oldIcon.gameObject.SetActive(false);
                var plate=Panel("Icon tile",row,theme.CreamPanel);plate.GetComponent<Image>().pixelsPerUnitMultiplier=3;
                var shadow=plate.Find("Ground shadow") as RectTransform;
                if(!shadow){var go=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=row.gameObject.layer;shadow=(RectTransform)go.transform;shadow.SetParent(plate,false);}
                inventoryShadows[i]=shadow.GetComponent<TankGroundShadow>();inventoryShadows[i].color=new Color32(35,25,13,112);inventoryShadows[i].raycastTarget=false;
                inventoryArt[i]=Icon("Artwork",plate,null,new Rect(.02f,.025f,.96f,.95f));
                inventoryFallbacks[i]=Raw("Atlas fallback",plate,new Rect(.02f,.025f,.96f,.95f));SetInventoryIcon(inventoryFallbacks[i],id);
                int product=InventoryProduct(id);string name=product>=0?ShopCatalog.Products[product].Title:id.ToUpperInvariant();
                if(id=="base-defence")name="BASE\nDEFENCE";else if(id=="extra-life")name="EXTRA\nLIFE";
                Label("Name",row,name,new Rect(.29f,.08f,.50f,.84f),20,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
                var count=Panel("Count badge",row,theme.DarkPanel);count.GetComponent<Image>().pixelsPerUnitMultiplier=3;
                var oldCount=row.Find("Count");if(oldCount)oldCount.SetParent(count,false);
                inventoryCounts[i]=Label("Count",count,"0",new Rect(.08f,.04f,.84f,.92f),28,ArcadeTextTreatment.PrizeAmount);inventoryCounts[i].color=Color.white;
                var divider=Panel("Divider",row,null);divider.GetComponent<Image>().color=new Color32(6,29,54,85);divider.gameObject.SetActive(i<7);
            }
        }
        static int InventoryProduct(string id)
        {
            for(int i=0;i<ShopCatalog.Products.Length;i++)if(ShopCatalog.Products[i].InventoryId==id)return i;
            return -1;
        }
        void LayoutInventory(bool largerText)
        {
            menu.ApplyShopInventorySurface(inventory,inventoryPaper);
            float width=inventoryPaper.rect.width,height=inventoryPaper.rect.height;
            float header=TvSidebarStyle.HeaderHeight,rowTop=header+7f,rowHeight=(height-rowTop-4f)/8f;
            MainMenuScene.Place(inventoryHeading,3,3,width-6,header);
            var headingText=inventoryHeading.Find("Heading").GetComponent<TMP_Text>();
            TvSidebarStyle.Apply(headingText,TvSidebarStyle.HeadingSize(largerText),textStyles);
            TvSidebarStyle.PlaceHeaderIcon(inventoryHeading.Find("Crate").GetComponent<Image>(),TvSidebarStyle.InventoryCrateBounds);
            MainMenuScene.Place(headingText.rectTransform,inventoryHeading.rect.width*.25f,header*.02f,inventoryHeading.rect.width*.72f,header*.96f);
            for(int i=0;i<8;i++)
            {
                var row=inventoryTiles[i];float rowWidth=width-10f,iconSize=Mathf.Min(rowHeight-8f,rowWidth*.27f),countSize=Mathf.Min(rowHeight*.57f,rowWidth*.18f);
                MainMenuScene.Place(row,5,rowTop+i*rowHeight,rowWidth,rowHeight);
                MainMenuScene.Place(row.Find("Icon tile") as RectTransform,0,(rowHeight-iconSize)*.5f,iconSize,iconSize);
                MainMenuScene.Place(row.Find("Count badge") as RectTransform,rowWidth-countSize,(rowHeight-countSize)*.5f,countSize,countSize);
                float nameLeft=iconSize+7f;MainMenuScene.Place(row.Find("Name") as RectTransform,nameLeft,3,rowWidth-countSize-nameLeft-5f,rowHeight-6f);
                var nameText=row.Find("Name").GetComponent<TMP_Text>();
                TvSidebarStyle.Apply(nameText,TvSidebarStyle.BodySize(largerText),textStyles);
                TvSidebarStyle.Apply(inventoryCounts[i],TvSidebarStyle.CountSize(largerText),textStyles);inventoryCounts[i].color=Color.white;
                MainMenuScene.Place(row.Find("Divider") as RectTransform,0,rowHeight-1f,rowWidth,1f);
            }
        }
        void BuildCurrencies()
        {
            var oldBar=root.Find("Currencies");if(oldBar)oldBar.SetParent(heading,false);
            currencyBar=Panel("Currencies",heading,art.tankCostButton);currencyBar.GetComponent<Image>().color=new Color(.08f,.63f,.68f,1);
            string[] names={"SKR","SOLANA","SWAP"};
            for(int i=0;i<3;i++)
            {
                int n=i;hudTabs[i]=currencyTabs[i]=JoinedTab(names[i],currencyBar,()=>SetCurrency((ShopCurrency)n,true),out hudSelections[i],out currencyLabels[i],new Rect(.29f,.025f,.68f,.95f));
                Icon("Icon",currencyTabs[i].transform,shopArt?(i==0?shopArt.skr:i==1?shopArt.solana:shopArt.swap):null,new Rect(.075f,.12f,.19f,.76f));
            }
            LockedTabIcon.Apply(currencyTabs[2],art.tankCostButtonLocked);
            var oldBack=root.Find("Back");if(oldBack)oldBack.SetParent(currencyBar,false);
            hudTabs[3]=back=JoinedTab("Back",currencyBar,Close,out hudSelections[3],out var backLabel,new Rect(.29f,.025f,.68f,.95f));backLabel.text="BACK";hudSelections[3].enabled=false;
            var arrow=back.transform.Find("Arrow") as RectTransform;
            if(!arrow){var go=new GameObject("Arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=back.gameObject.layer;arrow=(RectTransform)go.transform;arrow.SetParent(back.transform,false);}
            Fit(arrow,new Rect(.10f,.17f,.14f,.66f));var arrowGraphic=arrow.GetComponent<BackTabArrow>();arrowGraphic.color=Color.white;arrowGraphic.raycastTarget=false;
            JoinedDividers(currencyBar,4);
        }
        void BuildFilters()
        {
            filterBar=Panel("Category bar",filterRow,art.tankCostButton);
            filterBar.GetComponent<Image>().color=new Color(.08f,.63f,.68f,1);
            string[] categories={"ALL","FUEL","POWER","PACKS","SEASON PASS"};
            for(int i=0;i<filters.Length;i++)
            {
                // Reparent the existing controls when refreshing an already-open Shop.
                var old=filterRow.Find(categories[i]);if(old)old.SetParent(filterBar,false);
                int n=i;filters[i]=JoinedTab(categories[i],filterBar,()=>SetCategory((ShopCategory)n,true),out filterSelections[i],out filterLabels[i],new Rect(.035f,.025f,.93f,.95f));
            }
            JoinedDividers(filterBar,filters.Length);
        }
        Button JoinedTab(string name,Transform parent,UnityEngine.Events.UnityAction action,out Image selection,out TMP_Text label,Rect textArea)
        {
            var button=MakeButton(name,parent,"",action);button.image.sprite=null;button.image.overrideSprite=null;button.image.color=Color.clear;
            var selected=Panel("Selection",button.transform,art.tankCostButtonSelected);Fit(selected,new Rect(0,0,1,1));selected.SetAsFirstSibling();selection=selected.GetComponent<Image>();
            var focus=Panel("Focus",button.transform,art.tankCostButton);Fit(focus,new Rect(0,0,1,1));focus.SetAsFirstSibling();selected.SetSiblingIndex(1);
            button.targetGraphic=focus.GetComponent<Image>();button.transition=Selectable.Transition.ColorTint;
            var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.clear;colors.highlightedColor=colors.selectedColor=colors.pressedColor=Color.white;colors.disabledColor=Color.clear;colors.fadeDuration=.08f;button.colors=colors;
            label=Label("Label",button.transform,name,textArea,28,ArcadeTextTreatment.PrizeAmount);textStyles.ApplyCleanButton(label,false);label.transform.SetAsLastSibling();return button;
        }
        void JoinedDividers(RectTransform bar,int count)
        {
            for(int i=1;i<count;i++)
            {
                var divider=Panel("Divider "+i,bar,null);divider.GetComponent<Image>().color=new Color32(2,27,64,255);
                var light=Panel("Highlight",divider,null);light.GetComponent<Image>().color=new Color32(47,183,242,210);Fit(light,new Rect(.67f,0,.33f,1));
            }
        }
        void LayoutJoinedBar(RectTransform bar,Button[] buttons,Image[] selections,float height,float cornerFraction=.36f)
        {
            float width=bar.rect.width;FitFilterSkin(bar.GetComponent<Image>(),height,cornerFraction);
            float[] weights=buttons==filters?new[]{.65f,.8f,1f,.85f,1.65f}:null;
            float unit=width/(weights!=null?4.95f:buttons.Length),cursor=0;
            for(int i=0;i<buttons.Length;i++)
            {
                float cell=unit*(weights!=null?weights[i]:1);
                MainMenuScene.Place((RectTransform)buttons[i].transform,cursor+3,3,cell-6,height-6);
                FitFilterSkin(buttons[i].targetGraphic as Image,height-6,cornerFraction);FitFilterSkin(selections[i],height-6,cornerFraction);
                if(!buttons[i].interactable)FitFilterSkin(buttons[i].GetComponent<Image>(),height-6,cornerFraction);
                if(i>0)MainMenuScene.Place(bar.Find("Divider "+i) as RectTransform,cursor-1.5f,height*.20f,3,height*.60f);
                cursor+=cell;
            }
            if(buttons==filters)
            {
                float size=28;
                foreach(var label in filterLabels)
                {
                    label.enableAutoSizing=false;label.fontSize=28;
                    var preferred=label.GetPreferredValues(label.text);
                    size=Mathf.Min(size,28*Mathf.Min(label.rectTransform.rect.width/Mathf.Max(1,preferred.x),label.rectTransform.rect.height/Mathf.Max(1,preferred.y)));
                }
                foreach(var label in filterLabels)label.fontSize=Mathf.Clamp(size,12,28);
            }
        }
        TMP_Text Balance(string name,int index,Sprite icon)
        {
            var cell=Panel(name+" balance",footer,null);Fit(cell,new Rect(index*.25f,0,.25f,1));
            var line=Panel("Divider",cell,null);line.GetComponent<Image>().color=new Color32(7,43,94,55);Fit(line,new Rect(.997f,.18f,.003f,.64f));
            Icon("Icon",cell,icon,new Rect(.04f,.12f,.18f,.76f));
            Label("Label",cell,name,new Rect(.25f,.12f,.31f,.76f),24,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            return Label("Value",cell,"—",new Rect(.57f,.08f,.39f,.84f),30,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
        }
        void BuildScroll()
        {
            var area=Panel("Products",catalog,null);area.GetComponent<Image>().raycastTarget=true;
            var viewport=Panel("Viewport",area,theme.CreamPanel);viewport.GetComponent<Image>().pixelsPerUnitMultiplier=2;viewport.GetComponent<Image>().raycastTarget=true;
            Fit(viewport,new Rect(0,0,1,1));viewport.offsetMin=new Vector2(4,4);viewport.offsetMax=new Vector2(-26,-4);
            var mask=viewport.GetComponent<Mask>();if(!mask)mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var content=Panel("Content",viewport,null);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            grid=content.GetComponent<GridLayoutGroup>();if(!grid)grid=content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;grid.padding=new RectOffset(4,4,4,4);grid.spacing=new Vector2(6,8);grid.childAlignment=TextAnchor.UpperLeft;
            var fit=content.GetComponent<ContentSizeFitter>();if(!fit)fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var track=Panel("Scrollbar",area,theme.DarkPanel);track.GetComponent<Image>().pixelsPerUnitMultiplier=3;track.GetComponent<Image>().color=new Color32(110,198,255,255);track.GetComponent<Image>().raycastTarget=true;
            track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.pivot=new Vector2(1,.5f);track.anchoredPosition=new Vector2(-4,0);track.sizeDelta=new Vector2(18,-8);
            var sliding=Panel("Sliding Area",track,null);Fit(sliding,new Rect(0,0,1,1));sliding.offsetMin=new Vector2(2,2);sliding.offsetMax=new Vector2(-2,-2);
            var thumb=Panel("Handle",sliding,theme.GoldPanel);Fit(thumb,new Rect(0,0,1,1));thumb.GetComponent<Image>().pixelsPerUnitMultiplier=3;thumb.GetComponent<Image>().color=new Color32(255,224,76,255);thumb.GetComponent<Image>().raycastTarget=true;
            var bar=track.GetComponent<Scrollbar>();if(!bar)bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;bar.navigation=new Navigation{mode=Navigation.Mode.None};
            scroll=area.GetComponent<TankRosterScroll>();if(!scroll)scroll=area.gameObject.AddComponent<TankRosterScroll>();scroll.CardAspectRatio=ShopCardAspect;scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;scroll.inertia=true;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
        }
        void BuildCard(int index)
        {
            var product=ShopCatalog.Products[index];var rect=Panel(product.Id,scroll.content,art.tankCardAvailable);rect.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            var card=new Card{Rect=rect,Frame=rect.GetComponent<Image>()};cards[index]=card;
            card.Highlight=CardSelectionHighlight.Ensure(card.Frame);
            var focus=rect.GetComponent<ShopCardFocus>();if(!focus)focus=rect.gameObject.AddComponent<ShopCardFocus>();focus.Configure(this,index);card.Frame.raycastTarget=true;
            card.Title=Label("Title",rect,product.Title,new Rect(.13f,.055f,.74f,.167f),34,ArcadeTextTreatment.Navy);
            var shadow=rect.Find("Ground shadow") as RectTransform;
            if(!shadow){var go=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=rect.gameObject.layer;shadow=(RectTransform)go.transform;shadow.SetParent(rect,false);}
            Fit(shadow,new Rect(.18f,.535f,.64f,.055f));card.Shadow=shadow.GetComponent<TankGroundShadow>();card.Shadow.color=new Color32(35,25,13,112);card.Shadow.raycastTarget=false;
            card.Art=Icon("Artwork",rect,null,new Rect(.12f,.17f,.76f,.485f));card.Fallback=Raw("Inventory artwork",rect,new Rect(.23f,.18f,.54f,.465f));
            var reward=DetailRow("Reward strip",rect,new Rect(.06f,.66f,.88f,.125f),new Color32(238,220,184,130));
            var existingReward=rect.Find("Reward");if(existingReward)existingReward.SetParent(reward,false);
            card.Reward=Label("Reward",reward,product.Reward,new Rect(.035f,0,.93f,1),30,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);
            card.Reward.fontSizeMin=9;card.Reward.margin=new Vector4(1,0,1,0);
            var oldOwned=rect.Find("Owned strip");if(oldOwned)oldOwned.gameObject.SetActive(false);
            card.Price=MakeButton("Price",rect,"",()=>InspectProduct(index));card.PriceText=Label("Label",card.Price.transform,"",new Rect(.22f,.05f,.74f,.90f),26,ArcadeTextTreatment.PrizeAmount);textStyles.ApplyCleanButton(card.PriceText,false);
            // The shared card highlight keeps focus distinct from gold activation.
            card.Price.transition=Selectable.Transition.ColorTint;card.Price.spriteState=default;
            var colors=card.Price.colors;colors.normalColor=Color.white;colors.highlightedColor=colors.selectedColor=new Color(1f,.98f,.86f);colors.pressedColor=new Color(.92f,.92f,.92f);colors.disabledColor=Color.white;card.Price.colors=colors;
            buttonCaptions.Add(new ButtonCaption{Button=card.Price,Text=card.PriceText});
            card.Coin=Icon("Currency",card.Price.transform,shopArt?shopArt.skr:null,new Rect(.05f,.13f,.17f,.74f));
            var priceFocus=card.Price.GetComponent<ShopCardFocus>();if(!priceFocus)priceFocus=card.Price.gameObject.AddComponent<ShopCardFocus>();priceFocus.Configure(this,index,false);
            float height=.72f*ShopCardAspect*(art.tankCostButton.rect.height/art.tankCostButton.rect.width)*1.25f;
            Fit((RectTransform)card.Price.transform,new Rect(.14f,.88f-height*.5f,.72f,height));
        }
        RectTransform DetailRow(string name,Transform parent,Rect area,Color color)
        {
            var row=Panel(name,parent,null);Fit(row,area);var background=row.GetComponent<Image>();
            background.type=Image.Type.Simple;background.color=color;background.raycastTarget=false;return row;
        }
        void BuildSwap()
        {
            swapPage=Panel("Swap",root,theme.CreamPanel);swapPage.GetComponent<Image>().pixelsPerUnitMultiplier=2;
            Label("Title",swapPage,"SWAP",new Rect(.08f,.045f,.84f,.12f),38,ArcadeTextTreatment.Gold);
            var pay=Panel("You pay",swapPage,art.statusPanel);Fit(pay,new Rect(.08f,.22f,.84f,.22f));pay.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            Label("Heading",pay,"YOU PAY",new Rect(.05f,.05f,.90f,.25f),21,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);
            swapFrom=Label("Currency",pay,"SOL",new Rect(.06f,.39f,.25f,.47f),32,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);
            var inputArea=Panel("Amount",pay,null);Fit(inputArea,new Rect(.38f,.33f,.56f,.56f));inputArea.GetComponent<Image>().raycastTarget=true;
            var inputText=Label("Text",inputArea,"",new Rect(.03f,0,.94f,1),32,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineRight);
            var placeholder=Label("Placeholder",inputArea,"0.00",new Rect(.03f,0,.94f,1),32,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineRight);placeholder.color=new Color32(7,43,94,135);
            swapAmount=inputArea.GetComponent<TMP_InputField>();if(!swapAmount)swapAmount=inputArea.gameObject.AddComponent<TMP_InputField>();swapAmount.textViewport=inputArea;swapAmount.textComponent=inputText;swapAmount.placeholder=placeholder;swapAmount.contentType=TMP_InputField.ContentType.DecimalNumber;swapAmount.characterLimit=14;
            swapDirection=MakeButton("Change direction",swapPage,"SWITCH",()=>{swapFromSol=!swapFromSol;RefreshSwap();});Fit((RectTransform)swapDirection.transform,new Rect(.37f,.455f,.26f,.08f));
            var receive=Panel("You receive",swapPage,art.statusPanel);Fit(receive,new Rect(.08f,.55f,.84f,.22f));receive.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            Label("Heading",receive,"YOU RECEIVE",new Rect(.05f,.05f,.90f,.25f),21,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);
            swapTo=Label("Currency",receive,"SKR",new Rect(.06f,.39f,.25f,.47f),32,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);
            Label("Amount",receive,"—",new Rect(.38f,.33f,.56f,.56f),32,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineRight);
            swapBalance=Label("Balance",swapPage,"",new Rect(.08f,.80f,.84f,.06f),20,ArcadeTextTreatment.Navy);
            swapAction=MakeButton("Swap action",swapPage,"SWAP",()=>ShowNotice("SWAP","Swaps are not available yet."));Fit((RectTransform)swapAction.transform,new Rect(.25f,.885f,.5f,.09f));
        }
        void BuildNotice()
        {
            notice=Panel("Notice",root,null);Fit(notice,new Rect(0,0,1,1));notice.GetComponent<Image>().color=new Color(0,.035f,.09f,.70f);notice.GetComponent<Image>().raycastTarget=true;
            noticeCard=Panel("Panel",notice,art.tankCardAvailable);Fit(noticeCard,new Rect(.18f,.24f,.64f,.50f));noticeCard.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            noticeTitle=Label("Title",noticeCard,"",new Rect(.07f,.07f,.86f,.16f),34,ArcadeTextTreatment.Gold);
            noticeBody=Label("Body",noticeCard,"",new Rect(.08f,.27f,.84f,.40f),27,ArcadeTextTreatment.Navy);noticeBody.textWrappingMode=TextWrappingModes.Normal;
            noticeClose=MakeButton("Close",noticeCard,"CLOSE",DismissNotice);Fit((RectTransform)noticeClose.transform,new Rect(.25f,.77f,.5f,.15f));notice.gameObject.SetActive(false);
        }
        public void Open(bool refreshAccount=true)
        {
            if(!IsConfigured)return;
            var tankScreen=menu.Content.Find("Main Display/Pre-battle screens");
            if(tankScreen)tankScreen.gameObject.SetActive(false);
            root.gameObject.SetActive(true);menu.SetHeroVisible(false);menu.SetTankSelectorBackdrop(true);menu.RefreshLayout();
            if(refreshAccount&&Application.isPlaying&&api){if(accountRequest!=null)StopCoroutine(accountRequest);accountRequest=StartCoroutine(LoadAccount());}
            Focus(currency==ShopCurrency.Swap?swapDirection:PreferredProductButton());
            if(currency!=ShopCurrency.Swap&&visible.Count>0)scroll.Reveal(PreferredProductButton().transform.parent as RectTransform);
        }
        public void Close(){Close(true);}
        public void Close(bool restoreFocus)
        {
            if(!root)return;CancelCheckout();StopAllCoroutines();accountRequest=null;notice.gameObject.SetActive(false);root.gameObject.SetActive(false);
            if(menu){menu.SetTankSelectorBackdrop(false);menu.RefreshLayout();if(restoreFocus&&menu.Tabs.Length>1)Focus(menu.Tabs[1]);}
        }
        public void Back(){if(notice&&notice.gameObject.activeSelf)DismissNotice();else Close();}
        public void ApplyLayout(MainMenuPlatform platform)
        {
            if(!IsConfigured)return;float w=root.rect.width,h=root.rect.height;
            bool compact=platform==MainMenuPlatform.Android||platform==MainMenuPlatform.AndroidLandscape||platform==MainMenuPlatform.Psg1;
            columns=compact?3:4;
            if(w<620f)columns=2;
            var headerLayout=new TvTitleHeaderLayout(root);
            var footerLayout=new TvStatusFooterLayout(root);
            float header=headerLayout.Height,footerHeight=footerLayout.Height;
            float sidebar=Mathf.Clamp(w*.25f,208f,300f);
            headerLayout.PlaceTitle(heading,heading.Find("Shop icon") as RectTransform,heading.Find("Title") as RectTransform,iconHeightFraction:.64f);
            headerLayout.PlaceNavigation(heading,currencyBar);
            LayoutJoinedBar(currencyBar,hudTabs,hudSelections,headerLayout.NavigationHeight,.22f);
            float top=header+12;
            float bodyHeight=Mathf.Max(120,h-top-footerHeight-16);
            MainMenuScene.Place(inventory,4,top,sidebar,bodyHeight);MainMenuScene.Place(catalog,sidebar+12,top,w-sidebar-16,bodyHeight);MainMenuScene.Place(swapPage,sidebar+12,top,w-sidebar-16,bodyHeight);
            footerLayout.Place(footer);TvStatusFooterLayout.PlaceAction((RectTransform)connect.transform);
            LayoutInventory(compact);
            MainMenuScene.Place(filterRow,8,7,catalog.rect.width-16,header);
            float countWidth=Mathf.Clamp(filterRow.rect.width*.13f,64f,94f);
            MainMenuScene.Place(itemCount.rectTransform,24,header*.06f,countWidth,header*.88f);
            MainMenuScene.Place(filterBar,countWidth+24f,(header-headerLayout.NavigationHeight)*.5f,
                filterRow.rect.width-countWidth-32f,headerLayout.NavigationHeight);
            LayoutJoinedBar(filterBar,filters,filterSelections,headerLayout.NavigationHeight,.22f);
            MainMenuScene.Place((RectTransform)scroll.transform,4,header+14,catalog.rect.width-8,bodyHeight-header-18);
            grid.constraintCount=columns;LayoutRebuilder.ForceRebuildLayoutImmediate(catalog);Canvas.ForceUpdateCanvases();
            LayoutCardShadows();
            foreach(var button in buttonCaptions)FitButtonSkin(button.Button);
            ConfigureNavigation();RefreshPresentation();
        }
        public void SetCurrency(ShopCurrency next,bool focus)
        {
            if(next==ShopCurrency.Swap)return;
            currency=next;bool swap=currency==ShopCurrency.Swap;catalog.gameObject.SetActive(!swap);filterRow.gameObject.SetActive(!swap);swapPage.gameObject.SetActive(swap);
            for(int i=0;i<3;i++){bool active=i==(int)currency;hudSelections[i].enabled=active;textStyles.ApplyCleanButton(currencyLabels[i],active);}
            SetCategory(category,false);RefreshSwap();if(focus)Focus(currencyTabs[(int)currency]);
        }
        public void SetCategory(ShopCategory next,bool focus)
        {
            category=next;visible.Clear();for(int i=0;i<cards.Length;i++){bool show=category==ShopCategory.All||ShopCatalog.Products[i].Category==category;cards[i].Rect.gameObject.SetActive(show);if(show)visible.Add(i);}
            for(int i=0;i<filters.Length;i++)
            {
                bool active=i==(int)category;filterSelections[i].enabled=active;
                textStyles.ApplyCleanButton(filterLabels[i],active);
            }
            itemCount.text=visible.Count+(visible.Count==1?" ITEM":" ITEMS");UpdateCards();LayoutRebuilder.MarkLayoutForRebuild(scroll.content);scroll.StopMovement();scroll.verticalNormalizedPosition=1;ConfigureNavigation();if(focus)Focus(filters[(int)category]);
        }
        public void SelectProduct(int index,bool reveal)
        {if(index<0||index>=cards.Length||!visible.Contains(index))return;selected=index;UpdateCards();Focus(cards[index].Price);if(reveal)scroll.Reveal(cards[index].Rect);}
        internal void FocusProduct(int index,bool reveal)
        {if(!visible.Contains(index))return;focusedProduct=index;RefreshCardHighlights();if(reveal)scroll.Reveal(cards[index].Rect);}
        internal void ClearProductFocus(int index)
        {if(focusedProduct==index)focusedProduct=-1;RefreshCardHighlights();}
        internal void HoverProduct(int index,bool hovered)
        {if(hovered)hoveredProduct=index;else if(hoveredProduct==index)hoveredProduct=-1;RefreshCardHighlights();}
        void RefreshCardHighlights()
        {for(int i=0;i<cards.Length;i++)if(cards[i]!=null)cards[i].Highlight.SetState(i==selected,i==focusedProduct||i==hoveredProduct);}
        Button PreferredProductButton()=>visible.Count>0?cards[visible.Contains(selected)?selected:visible[0]].Price:back;
        void UpdateCards()
        {
            for(int i=0;i<cards.Length;i++)
            {
                var c=cards[i];var p=ShopCatalog.Products[i];bool active=i==selected;c.Frame.sprite=active?art.tankCardSelected:art.tankCardAvailable;SetButtonGold(c.Price,active);
                c.PriceText.text=LivePrice(p.Id);
                SetSprite(c.Coin,shopArt?(currency==ShopCurrency.Solana?shopArt.solana:shopArt.skr):null);
                int owned=p.InventoryId!=null?Count(account?["inventory"]?[p.InventoryId]):p.Category==ShopCategory.Fuel?Count(account?["fuelBalance"]):0;
                c.Reward.text=p.Category==ShopCategory.SeasonPass?SeasonCaption():p.Reward+"  •  OWNED "+(account!=null?owned.ToString(CultureInfo.InvariantCulture):"—");
            }
            RefreshCardHighlights();
            RefreshPresentation();
        }
        void InspectProduct(int index)
        {if(checkoutBusy)return;SelectProduct(index,true);InspectCheckout(index);}
        void ShowNotice(string title,string body){noticeTitle.text=title;noticeBody.text=body;notice.gameObject.SetActive(true);notice.SetAsLastSibling();Focus(noticeClose);}
        void DismissNotice(){CancelCheckout();notice.gameObject.SetActive(false);Focus(currency==ShopCurrency.Swap?swapAction:cards[selected].Price);}
        public void RefreshArt()
        {
            shopArt=Resources.Load<ShopArt>("ShopArt");SetSprite(heading.Find("Shop icon").GetComponent<Image>(),theme.NavigationIcons[2]);
            SetSprite(inventoryHeading.Find("Crate").GetComponent<Image>(),shopArt?shopArt.supplyCrate:null);
            for(int i=0;i<8;i++)
            {
                int product=InventoryProduct(ShopCatalog.InventoryIds[i]);var sprite=shopArt&&product>=0&&product<shopArt.products.Length?shopArt.products[product]:null;
                SetSprite(inventoryArt[i],sprite);inventoryArt[i].gameObject.SetActive(sprite);inventoryFallbacks[i].transform.parent.gameObject.SetActive(!sprite);inventoryShadows[i].gameObject.SetActive(sprite);
            }
            if(shopArt){SetSprite(currencyTabs[0].transform.Find("Icon").GetComponent<Image>(),shopArt.skr);SetSprite(currencyTabs[1].transform.Find("Icon").GetComponent<Image>(),shopArt.solana);SetSprite(currencyTabs[2].transform.Find("Icon").GetComponent<Image>(),shopArt.swap);SetSprite(footer.Find("SKR balance/Icon").GetComponent<Image>(),shopArt.skr);SetSprite(footer.Find("SOL balance/Icon").GetComponent<Image>(),shopArt.solana);}
            for(int i=0;i<cards.Length;i++)
            {
                var c=cards[i];var p=ShopCatalog.Products[i];var sprite=shopArt&&shopArt.products!=null&&i<shopArt.products.Length?shopArt.products[i]:null;
                if(!sprite&&p.Category==ShopCategory.Fuel)sprite=art.fuelCan;
                if(!sprite&&p.Category==ShopCategory.SeasonPass)sprite=Resources.Load<PlayerProfileArt>("PlayerProfileArt")?.insignia;
                SetSprite(c.Art,sprite);c.Art.gameObject.SetActive(sprite);c.Fallback.gameObject.SetActive(!sprite);
                SetSprite(c.Coin,shopArt?(currency==ShopCurrency.Solana?shopArt.solana:shopArt.skr):null);
                if(!sprite)SetInventoryIcon(c.Fallback,p.InventoryId??"shield");c.Shadow.gameObject.SetActive(sprite);
            }
            tokenIcons.Clear();
            if(shopArt)foreach(var icon in root.GetComponentsInChildren<Image>(true))
                if(icon.sprite&&(icon.sprite==shopArt.skr||icon.sprite==shopArt.solana))tokenIcons.Add(icon);
            FitTokenIcons();
        }
        public void RefreshPresentation()
        {
            FitTokenIcons();
            foreach(var entry in buttonCaptions)
            {
                var sprite=entry.Button.image.overrideSprite?entry.Button.image.overrideSprite:entry.Button.image.sprite;bool navy=sprite==art.tankCostButtonSelected;
                if(entry.Navy==navy)continue;entry.Navy=navy;
                textStyles.ApplyCleanButton(entry.Text,navy);
            }
        }
        void FitTokenIcons()
        {
            // preserveAspect fits the local rectangle; also cancel unequal ancestor scaling.
            foreach(var icon in tokenIcons)
            {
                if(!icon)continue;
                var rect=icon.rectTransform;var parentScale=rect.parent.lossyScale;
                float x=Mathf.Abs(parentScale.x),y=Mathf.Abs(parentScale.y);
                if(x<.0001f||y<.0001f)continue;
                float fit=Mathf.Min(x,y);var scale=new Vector3(fit/x,fit/y,1);
                if((rect.localScale-scale).sqrMagnitude>.000001f)rect.localScale=scale;
            }
        }
        void LayoutCardShadows()
        {
            for(int i=0;i<cards.Length;i++)
            {
                var card=cards[i];if(!card.Art.sprite)continue;
                var drawn=card.Art.rectTransform.rect;float aspect=card.Art.sprite.rect.width/card.Art.sprite.rect.height;
                if(drawn.width>drawn.height*aspect){float width=drawn.height*aspect;drawn.x+=(drawn.width-width)*.5f;drawn.width=width;}
                else{float height=drawn.width/aspect;drawn.y+=(drawn.height-height)*.5f;drawn.height=height;}
                var visible=shopArt&&shopArt.productVisibleBounds!=null&&i<shopArt.productVisibleBounds.Length?shopArt.productVisibleBounds[i]:new Rect(0,0,1,1);
                if(visible.width<=0||visible.height<=0)visible=new Rect(0,0,1,1);
                float widthVisible=drawn.width*visible.width,heightVisible=drawn.height*visible.height;
                var basePoint=new Vector3(drawn.x+drawn.width*visible.center.x,drawn.yMax-drawn.height*visible.yMax+heightVisible*.006f,0);
                var shadow=card.Shadow.rectTransform;shadow.anchorMin=shadow.anchorMax=new Vector2(.5f,.5f);shadow.pivot=new Vector2(.5f,.5f);
                shadow.sizeDelta=new Vector2(widthVisible*1.1f,heightVisible*.10f);
                shadow.localPosition=card.Rect.InverseTransformPoint(card.Art.rectTransform.TransformPoint(basePoint));
            }
            for(int i=0;i<8;i++)
            {
                var image=inventoryArt[i];if(!image.sprite)continue;
                var drawn=image.rectTransform.rect;float aspect=image.sprite.rect.width/image.sprite.rect.height;
                if(drawn.width>drawn.height*aspect){float width=drawn.height*aspect;drawn.x+=(drawn.width-width)*.5f;drawn.width=width;}
                else{float height=drawn.width/aspect;drawn.y+=(drawn.height-height)*.5f;drawn.height=height;}
                int product=InventoryProduct(ShopCatalog.InventoryIds[i]);var visible=shopArt&&product>=0&&product<shopArt.productVisibleBounds.Length?shopArt.productVisibleBounds[product]:new Rect(0,0,1,1);
                if(visible.width<=0||visible.height<=0)visible=new Rect(0,0,1,1);
                var shadow=inventoryShadows[i].rectTransform;var owner=(RectTransform)shadow.parent;
                shadow.anchorMin=shadow.anchorMax=shadow.pivot=new Vector2(.5f,.5f);shadow.sizeDelta=new Vector2(drawn.width*visible.width*1.1f,drawn.height*visible.height*.10f);
                shadow.localPosition=owner.InverseTransformPoint(image.rectTransform.TransformPoint(new Vector3(drawn.x+drawn.width*visible.center.x,drawn.yMax-drawn.height*visible.yMax,0)));
            }
        }
        void LateUpdate(){if(IsOpen)RefreshPresentation();}
        void RefreshSwap(){swapFrom.text=swapFromSol?"SOL":"SKR";swapTo.text=swapFromSol?"SKR":"SOL";swapBalance.text="AVAILABLE  "+(swapFromSol?solBalance.text+" SOL":skrBalance.text+" SKR");}
        void OnPlayerLoaded(MainMenuApiClient.PlayerSnapshot player){if(loadedOwner!=Owner){CancelCheckout();if(notice)notice.gameObject.SetActive(false);}if(isActiveAndEnabled&&IsOpen&&api&&Application.isPlaying){if(accountRequest!=null)StopCoroutine(accountRequest);accountRequest=StartCoroutine(LoadAccount());}}
        IEnumerator LoadAccount()
        {
            yield return LoadLiveAccount();accountRequest=null;
        }
        void RefreshAccount()
        {
            for(int i=0;i<8;i++)inventoryCounts[i].text=account!=null?Count(account?["inventory"]?[ShopCatalog.InventoryIds[i]]).ToString(CultureInfo.InvariantCulture):"—";
            connection.text=api&&api.IsWalletAuthenticated?"REFRESH":"CONNECT WALLET";
            // The older tokenBalance field represents BATC; it must never be presented as SKR.
            skrBalance.text=Amount(walletBalances?["skrBalance"],"0.#########");solBalance.text=Amount(walletBalances?["solBalance"],"0.#########");fuelBalance.text=account!=null?Count(account?["fuelBalance"]).ToString("N0",CultureInfo.InvariantCulture):"—";UpdateCards();RefreshSwap();
        }
        static int Count(JToken token){return int.TryParse(token?.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)?Math.Max(0,n):0;}
        static string Amount(JToken token,string format){return decimal.TryParse(token?.ToString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?Math.Max(0,n).ToString(format,CultureInfo.InvariantCulture):"—";}
        public void KeepControllerFocus(){Psg1UiNavigation.KeepFocus(root,notice.gameObject.activeSelf?noticeClose:currency==ShopCurrency.Swap?swapDirection:PreferredProductButton());}
        void ConfigureNavigation()
        {
            if(!back)return;
            Link(back,currencyTabs[1],currencyTabs[0],connect,filters[3]);
            for(int i=0;i<2;i++)Link(currencyTabs[i],i==0?back:currencyTabs[0],i==1?back:currencyTabs[1],connect,filters[i]);
            currencyTabs[2].navigation=new Navigation{mode=Navigation.Mode.None};
            for(int i=0;i<filters.Length;i++)Link(filters[i],filters[(i+filters.Length-1)%filters.Length],filters[(i+1)%filters.Length],i==filters.Length-1?back:currencyTabs[Math.Min(i,1)],visible.Count>0?cards[visible[Math.Min(i,Math.Min(columns,visible.Count)-1)]].Price:back);
            for(int at=0;at<visible.Count;at++){int row=at/columns*columns,last=Math.Min(row+columns,visible.Count)-1;var button=cards[visible[at]].Price;Link(button,cards[visible[at>row?at-1:last]].Price,cards[visible[at<last?at+1:row]].Price,at>=columns?cards[visible[at-columns]].Price:filters[Math.Min(at,3)],row+columns<visible.Count?cards[visible[Math.Min(at+columns,visible.Count-1)]].Price:connect);}
            Link(connect,back,back,currency==ShopCurrency.Swap?swapAction:visible.Count>0?cards[visible[visible.Count-1]].Price:back,back);
            Link(swapDirection,currencyTabs[2],swapAmount,swapAmount,swapAction);Link(swapAction,swapDirection,connect,swapDirection,connect);Link(swapAmount,swapDirection,swapDirection,currencyTabs[2],swapDirection);LinkNoticeButtons();
        }
        static void Link(Selectable target,Selectable left,Selectable right,Selectable up,Selectable down){target.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=left,selectOnRight=right,selectOnUp=up,selectOnDown=down};}
        static void Focus(Selectable control){if(control&&EventSystem.current)EventSystem.current.SetSelectedGameObject(control.gameObject);}
        void SetButtonGold(Button button,bool gold){button.image.sprite=gold?art.tankCostButtonSelected:art.tankCostButton;button.image.overrideSprite=null;FitButtonSkin(button);}
        static void FitFilterSkin(Image image,float height,float cornerFraction){TvTitleHeaderLayout.FitSkin(image,height,cornerFraction);}
        void FitButtonSkin(Button button){float width=((RectTransform)button.transform).rect.width;if(width>0)button.image.pixelsPerUnitMultiplier=art.tankCostButton.rect.width/(width*Mathf.Max(.01f,button.image.pixelsPerUnit));}
        Button MakeButton(string name,Transform parent,string caption,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(name,parent,art.tankCostButton);var button=rect.GetComponent<Button>();if(!button)button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.image.raycastTarget=true;button.transition=Selectable.Transition.SpriteSwap;button.spriteState=new SpriteState{highlightedSprite=art.tankCostButtonSelected,pressedSprite=art.tankCostButtonSelected,selectedSprite=art.tankCostButtonSelected,disabledSprite=art.tankCostButtonLocked};button.onClick.RemoveAllListeners();button.onClick.AddListener(action);
            if(caption.Length>0){var text=Label("Label",rect,caption,new Rect(.06f,.06f,.88f,.88f),28,ArcadeTextTreatment.PrizeAmount);textStyles.ApplyCleanButton(text,false);buttonCaptions.Add(new ButtonCaption{Button=button,Text=text});}return button;
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var child=parent.Find(name);var rect=child as RectTransform;if(!rect){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}var image=rect.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        static void SetSprite(Image image,Sprite sprite){image.sprite=sprite;image.color=sprite?Color.white:Color.clear;}
        Image Icon(string name,Transform parent,Sprite sprite,Rect area){var rect=Panel(name,parent,sprite);Fit(rect,area);var image=rect.GetComponent<Image>();image.type=Image.Type.Simple;image.preserveAspect=true;SetSprite(image,sprite);return image;}
        RawImage Raw(string name,Transform parent,Rect area)
        {
            var holder=Panel(name+" bounds",parent,null);Fit(holder,area);var child=holder.Find(name);RawImage image;
            if(child)image=child.GetComponent<RawImage>();else{var go=new GameObject(name,typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));go.layer=parent.gameObject.layer;go.transform.SetParent(holder,false);image=go.GetComponent<RawImage>();}
            Fit(image.rectTransform,new Rect(0,0,1,1));var aspect=image.GetComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1;image.raycastTarget=false;return image;
        }
        void SetInventoryIcon(RawImage image,string id){image.texture=art.powerups;if(BattleHud.TryPowerupUv(ShopCatalog.PowerupType(id),out var uv)){image.uvRect=uv;image.color=Color.white;}else image.color=Color.clear;}
        TMP_Text Label(string name,Transform parent,string caption,Rect area,float size,ArcadeTextTreatment style,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var child=parent.Find(name);TMP_Text text;if(child)text=child.GetComponent<TMP_Text>();else{var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);text=go.GetComponent<TextMeshProUGUI>();}
            Fit(text.rectTransform,area);text.font=ArcadeTextStyles.HeadingSdf;text.text=caption;text.fontSize=text.fontSizeMax=size;text.fontSizeMin=12;text.enableAutoSizing=true;text.alignment=alignment;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;textStyles.Apply(text,style);return text;
        }
        static void Fit(RectTransform rect,Rect a){rect.anchorMin=new Vector2(a.x,1-a.y-a.height);rect.anchorMax=new Vector2(a.x+a.width,1-a.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void OnDestroy(){CancelCheckout();if(api)api.PlayerLoaded-=OnPlayerLoaded;textStyles.Dispose();if(root){if(Application.isPlaying)Destroy(root.gameObject);else DestroyImmediate(root.gameObject);}}
    }
}
