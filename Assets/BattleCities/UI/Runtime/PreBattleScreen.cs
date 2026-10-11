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
    /// <summary>TV-contained tank selection and server-confirmed deployment/loadout flow.</summary>
    public sealed partial class PreBattleScreen : MonoBehaviour
    {
        const float ContinueWidth=220f,Psg1ContinueWidth=280f,ContinueRosterGap=2f,FooterColumnGap=10f;
        const float ContinueHeightScale=1.2f;
        const float PsgContentInset=4f,PsgHeaderGap=8f;
        const float DefaultStatLabelSize=15f,Psg1StatLabelSize=30f,AndroidLandscapeStatLabelSize=28f;
        static readonly string[] Names={"VANGUARD","STRIKER","TWIN FANG","SIEGEBREAKER"};
        // Roster ratings: Health, Rounds, Speed, Power. Classified II-IV are placeholders.
        static readonly int[,] TankRatings=
        {
            {1,1,1,1}, // Vanguard
            {2,1,1,1}, // Striker
            {1,2,1,1}, // Twin Fang
            {5,1,1,2}, // Siegebreaker
            {1,1,3,1}, // Classified I
            {3,1,2,2}, // Classified II
            {2,3,2,1}, // Classified III
            {4,2,1,3}  // Classified IV
        };
        static readonly string[] Items={"shield","base-defence","freeze","speed","upgrade","zoom-out","wipeout","extra-life"};
        static readonly string[] ItemNames={"SHIELD","BASE DEFENCE","FREEZE","SPEED","STAR","ZOOM OUT","WIPEOUT","EXTRA LIFE"};
        static readonly string[] Slots={"active-one","active-two","active-three","active-four"};
        MenuTheme theme; MainMenuApiClient api; Action launch; Button home;
        RectTransform root,tankPage,loadoutPage;
        TMP_FontAsset font;
        TMP_Text title,status,fuel,fuelAmount,deploymentHeading,deploymentCost,continueLabel,wallet;
        readonly List<Button> selectable=new List<Button>();
        readonly ArcadeTextStyles textStyles=new ArcadeTextStyles();
        readonly Button[] tankButtons=new Button[4],slotButtons=new Button[4];
        readonly Button[] classifiedButtons=new Button[4];
        TankRosterScroll rosterScroll;
        readonly TMP_Text[] slotNames=new TMP_Text[4],slotEmpty=new TMP_Text[4],inventoryLabels=new TMP_Text[8];
        readonly RawImage[] slotIcons=new RawImage[4];
        Button back,proceed;
        PreBattleArt art;
        JObject account,pendingFuel,loadout=new JObject();
        int selected,paidTier=-1;
        int rosterColumns=4;
        bool busy,inLoadout,ownsFont,psg1Spacing,androidSpacing,androidLandscapeSpacing;
        bool continueCaptionStyled,continueCaptionNavy;
        bool FillTvOpening=>psg1Spacing||androidLandscapeSpacing;
        float StatLabelSize=>psg1Spacing?Psg1StatLabelSize:androidLandscapeSpacing?AndroidLandscapeStatLabelSize:DefaultStatLabelSize;
        float StatLabelMinSize=>psg1Spacing?29f:androidLandscapeSpacing?15f:10f;
        float ActiveFooterHeight=>new TvStatusFooterLayout(root).Height;
        float ActiveContinueWidth=>psg1Spacing?Psg1ContinueWidth:androidSpacing?260f:ContinueWidth;
        float FooterBottomAnchor=>0f;
        float ContinueBottomInset=>TvStatusFooterLayout.EdgeInset;
        float FooterLeftAnchor=>TvStatusFooterLayout.EdgeInset/Mathf.Max(1f,root.rect.width);
        float FooterRightAnchor=>1f-FooterLeftAnchor;
        float ActiveHeaderHeight=>new TvTitleHeaderLayout(root).Height;
        Rect CostBadgeArea
        {
            get
            {
                const float width=.80f;
                var sprite=art?art.tankCostButton:null;
                float heightPerWidth=.72f*(sprite?sprite.rect.height/sprite.rect.width:1f/5.44f)*1.25f;
                float height=heightPerWidth*TankRosterScroll.CardAspect;
                // Seat the button between the lower corner rivets, just inside the rim.
                float bottomInset=.023f*TankRosterScroll.CardAspect;
                return new Rect((1f-width)*.5f,1f-bottomInset-height,width,height);
            }
        }
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public bool IsConfigured=>root&&tankButtons[0]&&launch!=null;
        public void Configure(RectTransform frame,MenuTheme skin,MainMenuApiClient client,Action onLaunch,Button returnTo)
        {
            if(api)api.PlayerLoaded-=OnLoadoutPlayer;theme=skin;api=client;launch=onLaunch;home=returnTo;if(api)api.PlayerLoaded+=OnLoadoutPlayer;BindLoadoutIdentity();
            art=Resources.Load<PreBattleArt>("PreBattleArt");
            root=frame.Find("Pre-battle screens") as RectTransform;
            if(root)
            {
                BindExistingView();
                EnsureLoadoutView();ApplyScreenSurface();
                ShowPage(false);
                return;
            }
            font=ArcadeTextStyles.HeadingSdf;
            ownsFont=!font;
            if(!font)font=TMP_FontAsset.CreateFontAsset(theme.HeadingFont);
            // Keep the original cream panel's sliced silhouette as the rounded screen mask.
            root=Panel("Pre-battle screens",frame,theme.CreamPanel,new Rect(.025f,.035f,.95f,.93f));
            root.GetComponent<Image>().raycastTarget=true;
            root.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            var titlePanel=Panel("Title plate",root,art&&art.tankTitlePanel?art.tankTitlePanel:theme.BlueFrame,new Rect(.02176f,.0176f,.7203f,.0798f));
            titlePanel.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            var mascotSprite=theme.NavigationIcons!=null&&theme.NavigationIcons.Length>0?theme.NavigationIcons[0]:null;
            var mascot=Panel("Mascot",titlePanel,mascotSprite,new Rect(.30f,.08f,.10f,.84f));
            var mascotImage=mascot.GetComponent<Image>();mascotImage.type=Image.Type.Simple;mascotImage.preserveAspect=true;
            title=Label("Title",titlePanel,"SELECT TANK",new Rect(.42f,0,.52f,1),34,theme.Gold);
            title.alignment=TextAlignmentOptions.MidlineLeft;
            back=Button("Back",root,"< BACK",new Rect(.7548f,.0176f,.2254f,.0798f),Back);
            var balance=Panel("Fuel summary",root,art&&art.statusPanel?art.statusPanel:theme.DarkPanel,new Rect(.015f,.103f,.97f,.089f));
            fuel=Label("Fuel",balance,"FUEL AVAILABLE",new Rect(.105f,.10f,.27f,.80f),25,theme.Navy);
            ApplyStatusPanel();
            tankPage=Panel("Tank roster",root,null,new Rect(.015f,.207f,.97f,.633f));
            for(int i=0;i<4;i++)
            {
                int index=i;
                var b=Button(Names[i],tankPage,"",new Rect(i*.25f+.006f,0,.238f,1),()=>SelectTank(index));
                tankButtons[i]=b;
                Label("Name",b.transform,Names[i],new Rect(.025f,.04f,.95f,.10f),26,theme.Navy);
                var picture=Raw("Tank model",b.transform,new Rect(.10f,.115f,.80f,.38f));
                picture.texture=art&&art.tanks!=null&&i<art.tanks.Length?art.tanks[i]:null;
                picture.color=picture.texture?Color.white:Color.clear;
                Label("Specifications",b.transform,"",new Rect(.06f,.58f,.88f,.19f),18,theme.Navy);
            }
            ApplyRosterPanel();
            loadoutPage=Panel("Loadout",root,null,new Rect(.015f,.207f,.97f,.633f));
            var inventory=Panel("Wallet and inventory",loadoutPage,theme.DarkPanel,new Rect(0,0,.25f,1));
            wallet=Label("Wallet",inventory,"WALLET\nCONNECT TO LOAD INVENTORY",new Rect(.04f,.02f,.92f,.25f),23,Color.white);
            for(int i=0;i<Items.Length;i++)
            {
                float x=(i%2)*.5f,y=.30f+(i/2)*.17f;
                var image=Raw(Items[i],inventory,new Rect(x+.10f,y,.30f,.10f));SetIcon(image,Items[i]);
                inventoryLabels[i]=Label("Count",inventory,"0",new Rect(x+.02f,y+.10f,.46f,.055f),19,theme.Gold);
            }
            for(int i=0;i<4;i++)
            {
                int index=i;
                var b=Button("Slot "+(i+1),loadoutPage,"",new Rect(.27f+(i%2)*.365f,(i/2)*.51f,.35f,.49f),()=>ChooseLoadoutSlot(index));
                slotButtons[i]=b;
                Label("Slot heading",b.transform,"SLOT 0"+(i+1),new Rect(.05f,.03f,.90f,.12f),24,theme.Navy);
                slotIcons[i]=Raw("Equipped item",b.transform,new Rect(.30f,.19f,.40f,.43f));
                slotEmpty[i]=Label("Empty slot",b.transform,"",new Rect(.30f,.19f,.40f,.43f),72,new Color(.25f,.38f,.48f));
                slotNames[i]=Label("Item name",b.transform,"EMPTY",new Rect(.03f,.65f,.94f,.13f),25,theme.Navy);
                Label("Change",b.transform,"CHANGE",new Rect(.08f,.81f,.84f,.14f),24,new Color(.03f,.32f,.65f));
            }
            status=Label("Status",root,"",new Rect(.035f,.88f,.58f,.105f),19,new Color32(245,235,207,255));
            status.alignment=TextAlignmentOptions.MidlineLeft;
            proceed=Button("Continue",root,"CONTINUE",new Rect(.68f,.88f,.29f,.105f),Continue);
            continueLabel=proceed.GetComponentInChildren<TMP_Text>();
            EnsureLoadoutView();ApplyContinueArt();
            ApplyTitleHeader();
            root.gameObject.SetActive(false);
        }
        // Scene previews and editor reloads retain GameObjects, but not runtime UnityEvent listeners.
        void BindExistingView()
        {
            var heading=root.Find("Heading");
            if(heading)
            {
                heading.Find("Title plate").SetParent(root,false);
                heading.Find("Back").SetParent(root,false);
                if(Application.isPlaying)Destroy(heading.gameObject);else DestroyImmediate(heading.gameObject);
            }
            title=root.Find("Title plate/Title").GetComponent<TMP_Text>();font=ArcadeTextStyles.HeadingSdf?ArcadeTextStyles.HeadingSdf:title.font;
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))label.font=font;
            status=(root.Find("Status")??root.Find("Fuel summary/Status")).GetComponent<TMP_Text>();
            fuel=root.Find("Fuel summary/Fuel").GetComponent<TMP_Text>();
            var backControl=root.Find("Title plate/Back container/Back")??root.Find("Back");
            back=backControl.GetComponent<Button>();
            var continueControl=root.Find("Fuel summary/Continue")??root.Find("Continue")??root.Find("Loadout/Workspace/Continue");
            proceed=continueControl.GetComponent<Button>();
            continueLabel=proceed.GetComponentInChildren<TMP_Text>(true);
            tankPage=(RectTransform)root.Find("Tank roster");
            var rosterCards=tankPage.Find("Viewport/Content");
            if(!rosterCards)rosterCards=tankPage;
            loadoutPage=(RectTransform)root.Find("Loadout");
            var inventory=loadoutPage.Find("Wallet and inventory");
            wallet=inventory.Find("Wallet").GetComponent<TMP_Text>();
            int count=0;
            foreach(var label in inventory.GetComponentsInChildren<TMP_Text>(true))
                if(label.name=="Count")inventoryLabels[count++]=label;
            BindClick(back,Back);BindClick(proceed,Continue);
            for(int i=0;i<4;i++)
            {
                int index=i;
                tankButtons[i]=rosterCards.Find(Names[i]).GetComponent<Button>();
                slotButtons[i]=(loadoutPage.Find("Workspace/Slot "+(i+1))??loadoutPage.Find("Slot "+(i+1))).GetComponent<Button>();
                var slot=slotButtons[i].transform;
                slotNames[i]=slot.Find("Item name").GetComponent<TMP_Text>();
                slotEmpty[i]=slot.Find("Empty slot").GetComponent<TMP_Text>();
                slotIcons[i]=slot.Find("Equipped item bounds/Equipped item").GetComponent<RawImage>();
                BindClick(tankButtons[i],()=>SelectTank(index));
                BindClick(slotButtons[i],()=>ChooseLoadoutSlot(index));
            }
        }
        static void BindClick(Button button,UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        void ApplyScreenSurface()
        {
            Fit(root,new Rect(.025f,.035f,.95f,.93f));
            var surface=root.GetComponent<Image>();
            surface.sprite=theme.CreamPanel;surface.type=Image.Type.Sliced;surface.color=Color.white;
            surface.raycastTarget=true;
            var rectangularMask=root.GetComponent<RectMask2D>();
            if(rectangularMask)
            {
                rectangularMask.enabled=false;
                if(Application.isPlaying)Destroy(rectangularMask);else DestroyImmediate(rectangularMask);
            }
            var mask=root.GetComponent<UnityEngine.UI.Mask>();
            if(!mask)mask=root.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic=false;
            var backdrop=root.Find("Blue backdrop") as RectTransform;
            if(backdrop)
            {
                if(Application.isPlaying)Destroy(backdrop.gameObject);
                else DestroyImmediate(backdrop.gameObject);
            }
            ApplyTitleHeader();
            Fit(loadoutPage,new Rect(.015f,.207f,.97f,.633f));
            status.color=new Color32(245,235,207,255);
            ApplyRosterPanel();
            ApplyStatusPanel();
            GetComponent<MainMenuScene>()?.FitPreBattleScreen(root);
        }
        public void ApplyPlatformSpacing(MainMenuPlatform platform)
        {
            bool psg1=platform==MainMenuPlatform.Psg1;
            androidSpacing=platform==MainMenuPlatform.Android||platform==MainMenuPlatform.AndroidLandscape;
            androidLandscapeSpacing=platform==MainMenuPlatform.AndroidLandscape;
            rosterColumns=psg1||platform==MainMenuPlatform.Android||platform==MainMenuPlatform.AndroidLandscape?3:4;
            if(!root)
            {
                var frame=GetComponent<MainMenuScene>()?.Content?.Find("Main Display");
                root=frame?frame.Find("Pre-battle screens") as RectTransform:null;
            }
            if(!root)return;
            psg1Spacing=psg1;
            var roster=root.Find("Tank roster") as RectTransform;
            var balance=root.Find("Fuel summary") as RectTransform;
            ApplyTitleHeader();
            if(roster)
            {
                ApplyRosterBounds(roster);
                var content=roster.Find("Viewport/Content") as RectTransform;
                var grid=content?content.GetComponent<GridLayoutGroup>():null;
                if(grid)
                {
                    grid.constraintCount=rosterColumns;
                    grid.padding=new RectOffset(4,4,psg1?2:4,4);
                    grid.spacing=new Vector2(4,psg1?6:8);
                    LayoutRebuilder.MarkLayoutForRebuild(roster);
                }
            }
            var loadoutRect=root.Find("Loadout") as RectTransform;
            if(loadoutRect)ApplyLoadoutBounds(loadoutRect);
            var statArea=CardStatArea;
            var badgeArea=CostBadgeArea;
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if(rect.name=="Specifications")Fit(rect,statArea);
                else if(rect.name=="Cost badge"){Fit(rect,badgeArea);FitCostBadgeContents(rect);}
                else if(rect.name=="Name"&&rect.parent&&
                    (Array.IndexOf(Names,rect.parent.name)>=0||rect.parent.name.StartsWith("CLASSIFIED ")))
                    Fit(rect,CardTitleArea(psg1,rect.parent.name.StartsWith("CLASSIFIED ")));
            }
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.name!="Heading"||!label.transform.parent||
                    !label.transform.parent.name.StartsWith("Specification row "))continue;
                Fit((RectTransform)label.transform,
                    psg1?new Rect(.03f,0,.50f,1):new Rect(.04f,0,.43f,1));
                var rating=label.transform.parent.Find("Rating") as RectTransform;
                if(rating)Fit(rating,
                    psg1?new Rect(.53f,.13f,.45f,.74f):new Rect(.49f,.13f,.48f,.74f));
                float size=StatLabelSize;
                label.fontSize=label.fontSizeMax=size;
                label.fontSizeMin=StatLabelMinSize;
            }
            foreach(var meter in root.GetComponentsInChildren<TankStatMeter>(true))
                meter.UseBlockStyle=true;
            if(balance&&roster&&roster.gameObject.activeSelf)
            {
                if(psg1)FitPsg1SummaryLabels(balance);
                SetSummaryFont(balance,"Fuel",psg1?36:24);
                SetSummaryFont(balance,"Fuel amount",psg1?52:36);
                SetSummaryFont(balance,"Deployment heading",psg1?27:20);
                SetSummaryFont(balance,"Deployment cost",psg1?32:26);
            }
            if(fuelAmount&&proceed)ApplyContinueArt();
            ApplyTypography();
            if(balance)FitFuelBalanceGroup(balance);
            if(back&&proceed&&tankButtons[0]&&classifiedButtons[3])Navigation();
        }
        void ApplyTitleHeader()
        {
            if(!root||!title||!back)return;
            var plate=root.Find("Title plate") as RectTransform;
            var plateImage=plate.GetComponent<Image>();
            plateImage.sprite=art&&art.tankTitlePanel?art.tankTitlePanel:theme.BlueFrame;
            plateImage.type=Image.Type.Sliced;plateImage.pixelsPerUnitMultiplier=4;
            var layout=new TvTitleHeaderLayout(root);
            layout.PlaceTitle(plate,plate.Find("Mascot") as RectTransform,title.rectTransform,true);
            title.fontSize=title.fontSizeMax=TvTitleHeaderLayout.TextSize;
            title.fontSizeMin=12;title.enableAutoSizing=true;
            title.textWrappingMode=TextWrappingModes.NoWrap;title.alignment=TextAlignmentOptions.MidlineLeft;

            var backPlate=plate.Find("Back container") as RectTransform;
            if(!backPlate)backPlate=Panel("Back container",plate,null,new Rect(0,0,1,1));
            var skin=backPlate.GetComponent<Image>();skin.sprite=art.tankCostButton;
            skin.type=Image.Type.Sliced;skin.color=new Color(.08f,.63f,.68f,1);
            layout.PlaceNavigation(plate,backPlate,true);TvTitleHeaderLayout.FitSkin(skin,layout.NavigationHeight);
            if(back.transform.parent!=backPlate)back.transform.SetParent(backPlate,false);
            MainMenuScene.Place((RectTransform)back.transform,3,3,backPlate.rect.width-6,layout.NavigationHeight-6);
            var hit=back.GetComponent<Image>();hit.sprite=hit.overrideSprite=null;hit.color=Color.clear;hit.raycastTarget=true;
            var label=back.GetComponentInChildren<TMP_Text>(true);label.gameObject.SetActive(true);label.text="BACK";
            Fit(label.rectTransform,new Rect(.29f,.025f,.68f,.95f));
            label.fontSize=label.fontSizeMax=TvTitleHeaderLayout.TextSize;label.fontSizeMin=12;label.enableAutoSizing=true;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.alignment=TextAlignmentOptions.Center;
            textStyles.ApplyCleanButton(label,false);
            var arrow=back.transform.Find("Arrow") as RectTransform;
            if(!arrow){var go=new GameObject("Arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=back.gameObject.layer;arrow=(RectTransform)go.transform;arrow.SetParent(back.transform,false);}
            Fit(arrow,new Rect(.10f,.17f,.14f,.66f));var arrowGraphic=arrow.GetComponent<BackTabArrow>();arrowGraphic.color=Color.white;arrowGraphic.raycastTarget=false;
            var focus=back.transform.Find("Focus") as RectTransform;
            if(!focus)focus=Panel("Focus",back.transform,null,new Rect(.18f,.90f,.64f,.035f));
            Fit(focus,new Rect(0,0,1,1));focus.SetAsFirstSibling();var focusImage=focus.GetComponent<Image>();focusImage.sprite=art.tankCostButton;focusImage.type=Image.Type.Sliced;TvTitleHeaderLayout.FitSkin(focusImage,layout.NavigationHeight-6);focusImage.color=Color.white;
            back.targetGraphic=focusImage;back.transition=Selectable.Transition.ColorTint;back.spriteState=default;
            var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.clear;colors.highlightedColor=Color.white;colors.selectedColor=Color.white;colors.pressedColor=Color.white;colors.disabledColor=Color.clear;colors.fadeDuration=.08f;back.colors=colors;
        }
        static void SetSummaryFont(Transform parent,string name,float size)
        {
            var label=parent.Find(name)?.GetComponent<TMP_Text>();
            if(label)label.fontSize=label.fontSizeMax=size;
        }
        static Rect CardTitleArea(bool psg1,bool classified)
        {
            return CardContentArea(new Rect(.025f,psg1?.02f:.04f,.95f,classified?.12f:.10f));
        }
        // Keep the established artwork and title sizes tied to width as the card gets shorter.
        static Rect CardContentArea(Rect area)
        {
            float scale=TankRosterScroll.CardAspect/.8f;
            return new Rect(area.x,area.y*scale,area.width,area.height*scale);
        }
        Rect CardStatArea=>CardContentArea(psg1Spacing
            ?new Rect(.035f,.465f,.93f,.304f):new Rect(.06f,.505f,.88f,.265f));
        static void FitPsg1SummaryLabels(RectTransform balance)
        {
            Fit((RectTransform)balance.Find("Fuel"),new Rect(.10f,.08f,.245f,.84f));
            Fit((RectTransform)balance.Find("Fuel amount"),new Rect(.365f,.08f,.07f,.84f));
            Fit((RectTransform)balance.Find("Deployment heading"),new Rect(.439f,.08f,.215f,.84f));
            Fit((RectTransform)balance.Find("Deployment cost"),new Rect(.667f,.08f,.32f,.84f));
            balance.Find("Deployment cost").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        }
        void ApplyStatusPanel()
        {
            if(inLoadout){LayoutLoadoutFooter();return;}
            var balance=(RectTransform)fuel.transform.parent;
            foreach(var name in new[]{"Fuel can","Fuel","Fuel amount","Deployment heading","Deployment cost"}){var child=balance.Find(name);if(child)child.gameObject.SetActive(true);}
            if(status&&status.transform.parent==balance)status.transform.SetParent(root,false);
            Fit(balance,new Rect(.015f,.103f,.97f,.089f));
            var image=balance.GetComponent<Image>();
            TvStatusFooterLayout.ApplySkin(image,art&&art.statusPanel?art.statusPanel:theme.DarkPanel);
            var oldRefresh=balance.Find("Refresh");
            if(oldRefresh)
            {
                if(Application.isPlaying)Destroy(oldRefresh.gameObject);
                else DestroyImmediate(oldRefresh.gameObject);
            }
            var icon=balance.Find("Fuel can") as RectTransform;
            if(!icon)icon=Panel("Fuel can",balance,null,new Rect(.018f,0,.09f,1));
            Fit(icon,new Rect(.018f,0,.09f,1));
            var iconImage=icon.GetComponent<Image>();
            iconImage.sprite=art?art.fuelCan:null;
            iconImage.type=Image.Type.Simple;
            iconImage.preserveAspect=true;
            iconImage.color=iconImage.sprite?Color.white:Color.clear;
            iconImage.raycastTarget=false;
            Fit((RectTransform)fuel.transform,new Rect(.105f,.10f,.27f,.80f));
            fuel.text="FUEL AVAILABLE";
            fuel.color=theme.Navy;
            fuel.alignment=TextAlignmentOptions.MidlineLeft;
            fuel.fontSize=25;
            fuel.fontSizeMax=25;
            fuelAmount=SummaryLabel(balance,"Fuel amount",new Rect(.265f,.08f,.06f,.84f),"0",38,theme.Gold,TextAlignmentOptions.Center);
            deploymentHeading=SummaryLabel(balance,"Deployment heading",new Rect(.64f,.08f,.34f,.39f),"DEPLOYMENT COST",21,theme.Navy,TextAlignmentOptions.MidlineRight);
            deploymentCost=SummaryLabel(balance,"Deployment cost",new Rect(.56f,.45f,.42f,.48f),"VANGUARD / 1 FUEL",28,new Color32(192,108,4,255),TextAlignmentOptions.MidlineRight);
            fuelAmount.outlineColor=new Color32(75,45,7,255);
            fuelAmount.outlineWidth=.12f;
            deploymentCost.outlineColor=new Color32(76,46,8,255);
            deploymentCost.outlineWidth=.08f;
            if(inLoadout){ApplyLoadoutLayout();return;}
            ApplySummaryLayout();
        }
        void ApplySummaryLayout()
        {
            if(inLoadout){LayoutLoadoutFooter();return;}
            var balance=(RectTransform)fuel.transform.parent;
            foreach(var name in new[]{"Fuel can","Fuel","Fuel amount","Deployment heading","Deployment cost"}){var child=balance.Find(name);if(child)child.gameObject.SetActive(true);}
            if(status&&status.transform.parent==balance)status.transform.SetParent(root,false);
            if(inLoadout&&proceed&&proceed.transform.parent!=root)proceed.transform.SetParent(root,false);
            if(inLoadout&&FillTvOpening)
                MainMenuScene.Place(balance,PsgContentInset,PsgContentInset+ActiveHeaderHeight+PsgHeaderGap,
                    root.rect.width-PsgContentInset*2f,ActiveFooterHeight);
            else if(inLoadout)Fit(balance,new Rect(.015f,.103f,.97f,.089f));
            else new TvStatusFooterLayout(root).Place(balance);
            Fit((RectTransform)balance.Find("Fuel can"),new Rect(.018f,.08f,.085f,.84f));
            if(psg1Spacing&&!inLoadout)FitPsg1SummaryLabels(balance);
            else if(androidSpacing&&!inLoadout)
            {
                Fit((RectTransform)fuel.transform,new Rect(.105f,.12f,.30f,.76f));
                Fit((RectTransform)fuelAmount.transform,new Rect(.42f,.12f,.085f,.76f));
                Fit((RectTransform)deploymentHeading.transform,new Rect(.54f,.13f,.425f,.32f));
                Fit((RectTransform)deploymentCost.transform,new Rect(.52f,.46f,.445f,.40f));
            }
            else
            {
                Fit((RectTransform)fuel.transform,new Rect(.105f,.10f,.225f,.80f));
                Fit((RectTransform)fuelAmount.transform,new Rect(.34f,.08f,.12f,.84f));
                Fit((RectTransform)deploymentHeading.transform,
                    inLoadout?new Rect(.53f,.07f,.445f,.39f):new Rect(.51f,.12f,.445f,.39f));
                Fit((RectTransform)deploymentCost.transform,
                    inLoadout?new Rect(.485f,.45f,.49f,.48f):new Rect(.465f,.45f,.49f,.48f));
            }
            if(!inLoadout)
            {
                // Reserve the rightmost slot inside the shared white footer for Continue.
                float contentFraction=TvStatusFooterLayout.ActionArea.x-FooterColumnGap/Mathf.Max(1f,balance.rect.width);
                foreach(var name in new[]{"Fuel can","Fuel","Fuel amount","Deployment heading","Deployment cost"})
                {
                    var rect=balance.Find(name) as RectTransform;
                    rect.anchorMin=new Vector2(rect.anchorMin.x*contentFraction,rect.anchorMin.y);
                    rect.anchorMax=new Vector2(rect.anchorMax.x*contentFraction,rect.anchorMax.y);
                }
            }
            fuelAmount.alignment=TextAlignmentOptions.MidlineLeft;
            deploymentCost.alignment=psg1Spacing&&!inLoadout?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.MidlineRight;
            fuel.fontSize=fuel.fontSizeMax=inLoadout?25:psg1Spacing?36:androidSpacing?26:24;
            fuelAmount.fontSize=fuelAmount.fontSizeMax=inLoadout?38:psg1Spacing?52:androidSpacing?42:36;
            deploymentHeading.fontSize=deploymentHeading.fontSizeMax=inLoadout?21:psg1Spacing?27:androidSpacing?22:20;
            deploymentCost.fontSize=deploymentCost.fontSizeMax=inLoadout?28:psg1Spacing?32:androidSpacing?28:26;
            ApplyTypography();
            FitFuelBalanceGroup(balance);
        }
        void ApplyTypography()
        {
            if(!root)return;
            StyleText(root.Find("Title plate/Title")?.GetComponent<TMP_Text>(),true);
            var backCaption=back?back.GetComponentInChildren<TMP_Text>(true):null;
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if(loadoutPage&&label.transform.IsChildOf(loadoutPage))continue;
                if(label==backCaption){textStyles.ApplyCleanButton(label,false);continue;}
                if(clearSlot&&label.transform.IsChildOf(clearSlot.transform)){var skin=clearSlot.image.overrideSprite?clearSlot.image.overrideSprite:clearSlot.image.sprite;textStyles.ApplyCleanButton(label,skin==art.tankCostButtonSelected);continue;}
                if(label==continueLabel||(label.transform.parent&&label.transform.parent.name=="Cost badge"&&
                    (label.name=="Cost text"||label.name=="Locked text")))continue;
                if(label.name=="Count")StyleText(label,true);
                else if(ArcadeTextStyles.IsWhite(label.color)&&!label.enableVertexGradient)
                    textStyles.Apply(label,ArcadeTextTreatment.WhiteButton);
            }
            var summary=root.Find("Fuel summary");
            if(!summary)return;
            StyleText(summary.Find("Fuel")?.GetComponent<TMP_Text>(),false);
            StyleText(summary.Find("Fuel amount")?.GetComponent<TMP_Text>(),true);
            StyleText(summary.Find("Deployment heading")?.GetComponent<TMP_Text>(),false);
            StyleText(summary.Find("Deployment cost")?.GetComponent<TMP_Text>(),true);
        }
        void StyleText(TMP_Text label,bool gold)
        {
            textStyles.Apply(label,gold?ArcadeTextTreatment.Gold:ArcadeTextTreatment.Navy);
        }
        static void FitFuelBalanceGroup(RectTransform balance)
        {
            var label=balance.Find("Fuel")?.GetComponent<TMP_Text>();
            var amount=balance.Find("Fuel amount")?.GetComponent<TMP_Text>();
            if(!label||!amount)return;
            float textWidth=Mathf.Min(label.rectTransform.rect.width,label.GetPreferredValues(label.text).x);
            float localRight=label.rectTransform.rect.xMin+textWidth;
            if(label.isActiveAndEnabled)
            {
                label.ForceMeshUpdate();
                if(label.textBounds.size.x>0f&&label.textBounds.size.x<label.rectTransform.rect.width*2f)
                    localRight=label.textBounds.max.x;
            }
            float textRight=balance.InverseTransformPoint(label.rectTransform.TransformPoint(
                new Vector3(localRight,0f,0f))).x;
            float gap=Mathf.Clamp(balance.rect.height*.16f,6f,12f);
            var rect=amount.rectTransform;
            float width=rect.rect.width;
            float left=(textRight-balance.rect.xMin+gap)/Mathf.Max(1f,balance.rect.width);
            var min=rect.anchorMin;var max=rect.anchorMax;
            min.x=max.x=left;
            rect.anchorMin=min;rect.anchorMax=max;
            rect.sizeDelta=new Vector2(width,rect.sizeDelta.y);
            rect.anchoredPosition=new Vector2(width*rect.pivot.x,rect.anchoredPosition.y);
            amount.alignment=TextAlignmentOptions.MidlineLeft;
        }
        void ApplyRosterBounds(RectTransform roster)
        {
            if(!androidSpacing||FillTvOpening)
            {
                roster.anchorMin=Vector2.zero;roster.anchorMax=Vector2.one;
                roster.offsetMin=new Vector2(PsgContentInset,ContinueBottomInset+ActiveFooterHeight+6f);
                roster.offsetMax=new Vector2(-PsgContentInset,-(PsgContentInset+ActiveHeaderHeight+PsgHeaderGap));
                return;
            }
            float top=psg1Spacing ? .096f : .103f;
            Fit(roster,new Rect(.015f,top,.97f,1f-top-FooterBottomAnchor));
            if(androidSpacing)
            {
                Fit(roster,new Rect(.015f,0,.97f,1f-FooterBottomAnchor));
                roster.offsetMax=new Vector2(0,-(ActiveHeaderHeight+root.rect.width*.024f));
            }
            roster.offsetMin=new Vector2(0,ContinueBottomInset+ActiveFooterHeight+ContinueRosterGap);
        }
        void ApplyLoadoutBounds(RectTransform page)
        {
            page.anchorMin=Vector2.zero;page.anchorMax=Vector2.one;
            page.offsetMin=new Vector2(4,4+ActiveFooterHeight+8);
            page.offsetMax=new Vector2(-4,-(4+ActiveHeaderHeight+8));
        }
        TMP_Text SummaryLabel(Transform parent,string name,Rect area,string value,float size,Color color,TextAlignmentOptions alignment)
        {
            var child=parent.Find(name);
            var label=child?child.GetComponent<TMP_Text>():Label(name,parent,value,area,size,color);
            Fit((RectTransform)label.transform,area);
            label.text=value;
            label.color=color;
            label.alignment=alignment;
            label.fontSize=size;
            label.fontSizeMax=size;
            return label;
        }
        void ApplyRosterPanel()
        {
            // Fill the space from the title down to the shared fuel/Continue footer.
            var rosterAspect=tankPage.GetComponent<AspectRatioFitter>();
            if(rosterAspect)
            {
                rosterAspect.aspectMode=AspectRatioFitter.AspectMode.None;
                rosterAspect.enabled=false;
            }
            ApplyRosterBounds(tankPage);
            var panel=tankPage.GetComponent<Image>();
            panel.sprite=theme.CreamPanel;
            panel.type=Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier=2f;
            panel.color=Color.white;
            panel.raycastTarget=true;
            var content=ConfigureRosterScroll();
            for(int i=0;i<tankButtons.Length;i++)
            {
                var card=(RectTransform)tankButtons[i].transform;
                if(card.parent!=content)card.SetParent(content,false);
                card.SetSiblingIndex(i);
                var cardAspect=card.GetComponent<AspectRatioFitter>();
                if(cardAspect)cardAspect.enabled=false;
                var name=tankButtons[i].transform.Find("Name") as RectTransform;
                if(name)Fit(name,CardTitleArea(psg1Spacing,false));
                ApplyTankPicture(tankButtons[i],i);
                ApplyCostBadge(tankButtons[i],i);
                ConfigureRosterFocus(tankButtons[i]);
            }
            for(int i=0;i<classifiedButtons.Length;i++)ApplyClassifiedCard(content,i);
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }
        RectTransform ConfigureRosterScroll()
        {
            var viewport=tankPage.Find("Viewport") as RectTransform;
            if(!viewport)viewport=Panel("Viewport",tankPage,theme.CreamPanel,new Rect(0,0,1,1));
            Fit(viewport,new Rect(0,0,1,1));
            viewport.offsetMin=new Vector2(6,6);
            viewport.offsetMax=new Vector2(-28,-6);
            var viewportImage=viewport.GetComponent<Image>();
            viewportImage.sprite=theme.CreamPanel;
            viewportImage.pixelsPerUnitMultiplier=2f;
            viewportImage.color=Color.white;
            viewportImage.raycastTarget=true;
            var mask=viewport.GetComponent<Mask>();
            if(!mask)mask=viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic=false;

            var content=viewport.Find("Content") as RectTransform;
            if(!content)
            {
                var go=new GameObject("Content",typeof(RectTransform));go.layer=viewport.gameObject.layer;
                content=(RectTransform)go.transform;content.SetParent(viewport,false);
                content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);
                content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            }
            var grid=content.GetComponent<GridLayoutGroup>();
            if(!grid)grid=content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=rosterColumns;
            grid.startAxis=GridLayoutGroup.Axis.Horizontal;grid.startCorner=GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment=TextAnchor.UpperLeft;grid.padding=new RectOffset(4,4,4,4);grid.spacing=new Vector2(4,8);
            var fitter=content.GetComponent<ContentSizeFitter>();
            if(!fitter)fitter=content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;

            var track=tankPage.Find("Scrollbar") as RectTransform;
            if(!track)track=Panel("Scrollbar",tankPage,theme.DarkPanel,new Rect(0,0,1,1));
            track.anchorMin=new Vector2(1,0);track.anchorMax=new Vector2(1,1);track.pivot=new Vector2(1,.5f);
            track.anchoredPosition=new Vector2(-5,0);track.sizeDelta=new Vector2(18,-12);
            var trackImage=track.GetComponent<Image>();trackImage.sprite=theme.DarkPanel;
            trackImage.pixelsPerUnitMultiplier=3f;trackImage.color=new Color32(110,198,255,255);trackImage.raycastTarget=true;
            var sliding=track.Find("Sliding Area") as RectTransform;
            if(!sliding)sliding=Panel("Sliding Area",track,null,new Rect(0,0,1,1));
            Fit(sliding,new Rect(0,0,1,1));sliding.offsetMin=new Vector2(2,2);sliding.offsetMax=new Vector2(-2,-2);
            var thumb=sliding.Find("Handle") as RectTransform;
            if(!thumb)thumb=Panel("Handle",sliding,theme.GoldPanel,new Rect(0,0,1,1));
            var thumbImage=thumb.GetComponent<Image>();thumbImage.sprite=theme.GoldPanel;
            thumbImage.pixelsPerUnitMultiplier=3f;thumbImage.color=new Color32(255,224,76,255);thumbImage.raycastTarget=true;
            var scrollbar=track.GetComponent<Scrollbar>();
            if(!scrollbar)scrollbar=track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect=thumb;scrollbar.targetGraphic=thumbImage;scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scrollbar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            rosterScroll=tankPage.GetComponent<TankRosterScroll>();
            if(!rosterScroll)rosterScroll=tankPage.gameObject.AddComponent<TankRosterScroll>();
            rosterScroll.CardAspectRatio=TankRosterScroll.CardAspect;
            rosterScroll.viewport=viewport;rosterScroll.content=content;
            rosterScroll.horizontal=false;rosterScroll.vertical=true;rosterScroll.movementType=ScrollRect.MovementType.Clamped;
            rosterScroll.scrollSensitivity=35f;rosterScroll.inertia=true;
            rosterScroll.verticalScrollbar=scrollbar;rosterScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }
        void ConfigureRosterFocus(Button card)
        {
            var focus=card.GetComponent<TankRosterCardFocus>();
            if(!focus)focus=card.gameObject.AddComponent<TankRosterCardFocus>();
            focus.Configure(rosterScroll);
        }
        void ApplyClassifiedCard(RectTransform content,int index)
        {
            string[] numerals={"I","II","III","IV"};
            string name="CLASSIFIED "+numerals[index];
            var existing=content.Find(name);
            var card=existing?existing.GetComponent<Button>():Button(name,content,"",new Rect(0,0,1,1),()=>{});
            classifiedButtons[index]=card;
            card.transform.SetSiblingIndex(4+index);
            BindClick(card,()=>{status.text=name+" — LOCKED";});
            card.image.sprite=art&&art.tankCardUnavailable?art.tankCardUnavailable:theme.SilverFrame;
            card.image.type=Image.Type.Sliced;card.image.pixelsPerUnitMultiplier=4f;
            card.image.color=new Color32(160,188,208,255);
            var colors=card.colors;colors.normalColor=Color.white;
            colors.highlightedColor=colors.selectedColor=Color.white;
            colors.pressedColor=new Color(.85f,.91f,1f);card.colors=colors;
            SummaryLabel(card.transform,"Name",CardTitleArea(psg1Spacing,true),name,26,theme.Navy,TextAlignmentOptions.Center);
            var covered=card.transform.Find("Classified tank") as RectTransform;
            if(!covered)covered=Panel("Classified tank",card.transform,null,CardContentArea(new Rect(.08f,.17f,.84f,.32f)));
            Fit(covered,CardContentArea(new Rect(.08f,.17f,.84f,.32f)));
            var coveredImage=covered.GetComponent<Image>();coveredImage.sprite=art?art.classifiedTank:null;
            coveredImage.type=Image.Type.Simple;coveredImage.preserveAspect=true;
            coveredImage.color=Color.white;coveredImage.raycastTarget=false;
            ApplySpecifications(card,4+index);
            var oldLocked=card.transform.Find("Locked");
            if(oldLocked)oldLocked.gameObject.SetActive(false);
            var badge=card.transform.Find("Cost badge") as RectTransform;
            var badgeArea=CostBadgeArea;
            if(!badge)badge=Panel("Cost badge",card.transform,null,badgeArea);
            Fit(badge,badgeArea);
            var badgeImage=badge.GetComponent<Image>();
            bool hasLockedArt=art&&art.tankCostButtonLocked;
            badgeImage.sprite=hasLockedArt?art.tankCostButtonLocked:theme.SilverFrame;
            badgeImage.type=Image.Type.Sliced;
            badgeImage.preserveAspect=false;
            badgeImage.color=Color.white;
            badgeImage.raycastTarget=false;
            var caption=SummaryLabel(badge,"Locked text",new Rect(.05f,.04f,.90f,.92f),
                "LOCKED",27,Color.white,TextAlignmentOptions.Center);
            caption.fontStyle=FontStyles.Normal;
            caption.raycastTarget=false;
            FitCostBadgeContents(badge);
            ConfigureRosterFocus(card);
            CardSelectionHighlight.Ensure(card.image,new Color32(160,188,208,255)).SetActivated(false);
        }
        void ApplyTankPicture(Button card,int index)
        {
            var bounds=card.transform.Find("Tank model bounds");
            var picture=(bounds?bounds.Find("Tank model"):card.transform.Find("Tank model")) as RectTransform;
            if(!picture)return;
            var image=picture.GetComponent<RawImage>();
            var texture=art&&art.tanks!=null&&index<art.tanks.Length?art.tanks[index]:null;
            image.texture=texture;
            image.color=texture?Color.white:Color.clear;
            image.raycastTarget=false;
            if(!texture)return;
            var aspect=picture.GetComponent<AspectRatioFitter>();
            if(!aspect)aspect=picture.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectRatio=(float)texture.width/texture.height;
            if(bounds)
            {
                Fit((RectTransform)bounds,CardContentArea(new Rect(.14f,.144f,.72f,.342f)));
                // Let the illustration tuck behind the title without covering its lettering.
                bounds.SetAsFirstSibling();
                aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                ApplyTankGroundShadow(card,bounds);
                return;
            }
            // Keep the supplied transparent illustrations at their original proportions.
            picture.SetAsFirstSibling();
            float center=1f-.315f*TankRosterScroll.CardAspect/.8f;
            picture.anchorMin=new Vector2(.14f,center);
            picture.anchorMax=new Vector2(.86f,center);
            picture.pivot=new Vector2(.5f,.5f);
            picture.anchoredPosition=Vector2.zero;
            picture.sizeDelta=new Vector2(0,1);
            aspect.aspectMode=AspectRatioFitter.AspectMode.WidthControlsHeight;
            ApplyTankGroundShadow(card,picture);
        }
        static void ApplyTankGroundShadow(Button card,Transform artwork)
        {
            var shadow=card.transform.Find("Tank ground shadow") as RectTransform;
            if(!shadow)
            {
                var go=new GameObject("Tank ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));
                go.layer=card.gameObject.layer;
                shadow=go.GetComponent<RectTransform>();
                shadow.SetParent(card.transform,false);
            }
            if(!shadow.GetComponent<CanvasRenderer>())shadow.gameObject.AddComponent<CanvasRenderer>();
            Fit(shadow,CardContentArea(new Rect(.17f,.455f,.66f,.052f)));
            var graphic=shadow.GetComponent<TankGroundShadow>();
            graphic.color=new Color32(35,25,13,112);
            graphic.raycastTarget=false;
            artwork.SetAsFirstSibling();
            shadow.SetAsFirstSibling();
        }
        void ApplyCostBadge(Button card,int index)
        {
            var role=card.transform.Find("Role") as RectTransform;
            if(role)
            {
                role.GetComponent<TMP_Text>().text="";
                role.gameObject.SetActive(false);
            }
            ApplySpecifications(card,index);
            var oldTier=card.transform.Find("Tank tier");
            if(oldTier)
            {
                if(Application.isPlaying)Destroy(oldTier.gameObject);
                else DestroyImmediate(oldTier.gameObject);
            }
            var badge=card.transform.Find("Cost badge") as RectTransform;
            var badgeArea=CostBadgeArea;
            if(!badge)badge=Panel("Cost badge",card.transform,null,badgeArea);
            Fit(badge,badgeArea);
            var badgeImage=badge.GetComponent<Image>();
            bool hasActiveArt=art&&art.tankCostButton;
            badgeImage.sprite=hasActiveArt?art.tankCostButton:theme.BlueFrame;
            badgeImage.type=Image.Type.Sliced;
            badgeImage.preserveAspect=false;
            badgeImage.color=Color.white;
            badgeImage.raycastTarget=false;
            var can=badge.Find("Fuel can") as RectTransform;
            if(!can)can=Panel("Fuel can",badge,null,new Rect(.13f,.14f,.28f,.72f));
            Fit(can,new Rect(.13f,.20f,.28f,.60f));
            var canImage=can.GetComponent<Image>();
            canImage.sprite=art?(art.buttonFuelCan?art.buttonFuelCan:art.fuelCan):null;
            canImage.type=Image.Type.Simple;
            canImage.preserveAspect=true;
            canImage.color=canImage.sprite?Color.white:Color.clear;
            canImage.raycastTarget=false;
            var child=badge.Find("Cost text");
            var caption=child?child.GetComponent<TMP_Text>():Label("Cost text",badge,"",new Rect(.44f,.045f,.52f,.91f),27,Color.white);
            Fit((RectTransform)caption.transform,new Rect(.44f,.045f,.52f,.91f));
            caption.text=(index+1)+" FUEL";
            caption.color=Color.white;
            caption.fontStyle=FontStyles.Normal;
            caption.alignment=TextAlignmentOptions.MidlineLeft;
            caption.fontSize=27;
            caption.fontSizeMax=27;
            caption.raycastTarget=false;
            ApplyCostBadgeSelection(card,index);
            FitCostBadgeContents(badge);
        }
        void FitCostBadgeContents(RectTransform badge)
        {
            FitCostBadgeSkin(badge);
            var can=badge.Find("Fuel can") as RectTransform;
            var cost=badge.Find("Cost text")?.GetComponent<TMP_Text>();
            if(cost)
            {
                cost.fontSize=cost.fontSizeMax=androidSpacing?25:27;
                textStyles.ApplyCleanButton(cost,cost.color.r<.5f);
                float width=Mathf.Max(1f,badge.rect.width),height=Mathf.Max(1f,badge.rect.height);
                var icon=can?can.GetComponent<Image>():null;
                float iconWidth=can?height*.84f*(icon&&icon.sprite?icon.sprite.rect.width/icon.sprite.rect.height:.8f):0f;
                float gap=can?height*.18f:0f;
                float textWidth=Mathf.Min(cost.GetPreferredValues(cost.text).x+4f,Mathf.Max(1f,width*.82f-iconWidth-gap));
                float left=(width-iconWidth-gap-textWidth)*.5f;
                if(can)Fit(can,new Rect(left/width,.08f,iconWidth/width,.84f));
                Fit(cost.rectTransform,new Rect((left+iconWidth+gap)/width,.04f,textWidth/width,.92f));
            }
            var locked=badge.Find("Locked text")?.GetComponent<TMP_Text>();
            if(locked)
            {
                Fit(locked.rectTransform,androidSpacing?new Rect(.08f,.10f,.84f,.80f):new Rect(.05f,.04f,.90f,.92f));
                locked.fontSize=locked.fontSizeMax=androidSpacing?25:27;
                textStyles.ApplyCleanButton(locked,false);
            }
        }
        static void FitCostBadgeSkin(RectTransform badge)
        {
            var image=badge.GetComponent<Image>();
            if(!image||!image.sprite)return;
            // Only the empty skin resizes; keep its corners at their original width-based scale.
            image.type=Image.Type.Sliced;
            image.preserveAspect=false;
            image.pixelsPerUnitMultiplier=image.sprite.rect.width/
                (Mathf.Max(1f,badge.rect.width)*Mathf.Max(.01f,image.pixelsPerUnit));
        }
        void ApplyCostBadgeSelection(Button card,int index)
        {
            var badge=card.transform.Find("Cost badge");
            if(!badge)return;
            bool gold=index==selected&&art&&art.tankCostButtonSelected;
            var badgeImage=badge.GetComponent<Image>();
            bool supplied=art&&(gold?art.tankCostButtonSelected:art.tankCostButton);
            badgeImage.sprite=supplied?(gold?art.tankCostButtonSelected:art.tankCostButton):theme.BlueFrame;
            FitCostBadgeSkin((RectTransform)badge);
            badgeImage.enabled=true;
            var goldBackground=badge.Find("Selected background") as RectTransform;
            if(goldBackground)goldBackground.gameObject.SetActive(false);
            var caption=badge.Find("Cost text");
            if(caption)textStyles.ApplyCleanButton(caption.GetComponent<TMP_Text>(),gold);
        }
        void ApplySpecifications(Button card,int index)
        {
            var specifications=card.transform.Find("Specifications") as RectTransform;
            var statArea=CardStatArea;
            if(!specifications)specifications=Panel("Specifications",card.transform,null,statArea);
            Fit(specifications,statArea);
            var legacy=specifications.GetComponent<TMP_Text>();
            if(legacy)legacy.text="";
            string[] headings={"HEALTH","ROUNDS","SPEED","POWER"};
            for(int row=0;row<headings.Length;row++)
            {
                var stripe=specifications.Find("Specification row "+row) as RectTransform;
                var area=new Rect(0,(float)row/headings.Length,1,1f/headings.Length);
                if(!stripe)stripe=Panel("Specification row "+row,specifications,null,area);
                Fit(stripe,area);
                var background=stripe.GetComponent<Image>();
                background.type=Image.Type.Simple;
                background.color=row%2==1?new Color32(255,250,238,70):new Color32(238,220,184,130);
                background.raycastTarget=false;
                var heading=stripe.Find("Heading");
                var labelArea=psg1Spacing?new Rect(.03f,0,.50f,1):new Rect(.04f,0,.43f,1);
                var left=heading?heading.GetComponent<TMP_Text>():Label("Heading",stripe,"",labelArea,DefaultStatLabelSize,theme.Navy);
                Fit((RectTransform)left.transform,labelArea);
                left.text=headings[row];
                left.color=theme.Navy;
                left.fontStyle=FontStyles.Bold;
                left.alignment=TextAlignmentOptions.MidlineLeft;
                float statLabelSize=StatLabelSize;
                left.fontSize=left.fontSizeMax=statLabelSize;
                left.fontSizeMin=StatLabelMinSize;
                var value=stripe.Find("Value");
                if(value)
                {
                    value.GetComponent<TMP_Text>().text="";
                    value.gameObject.SetActive(false);
                }
                var meterRect=stripe.Find("Rating") as RectTransform;
                if(!meterRect)
                {
                    var go=new GameObject("Rating",typeof(RectTransform),typeof(TankStatMeter));
                    go.layer=stripe.gameObject.layer;go.transform.SetParent(stripe,false);
                    meterRect=(RectTransform)go.transform;
                }
                Fit(meterRect,psg1Spacing?new Rect(.53f,.13f,.45f,.74f):new Rect(.49f,.13f,.48f,.74f));
                var meter=meterRect.GetComponent<TankStatMeter>();
                meter.raycastTarget=false;
                meter.color=Color.white;
                meter.UseBlockStyle=true;
                meter.FilledSegments=TankRatings[index,row];
            }
        }
        public void Open()
        {
            launchStage=1;
            CancelLoadoutRequests();
            GetComponent<MainMenuScene>()?.FitPreBattleScreen(root);
            ApplyRosterPanel();
            GetComponent<MainMenuScene>()?.SetHeroVisible(false);
            root.gameObject.SetActive(true);root.SetAsLastSibling();
            GetComponent<MainMenuScene>()?.SetTankSelectorBackdrop(true);
            ShowPage(false);SelectTank(selected);
            status.text="";
            Focus(tankButtons[selected]);
            rosterScroll.Reveal((RectTransform)tankButtons[selected].transform);
            launchRequested=false;if(api&&(Application.isPlaying||EditorFixtureActive))RefreshLoadoutAccount();
        }
        public void Back()
        {
            if(busy)return;
            if(inLoadout){launchRequested=false;ShowPage(false);status.text="";Focus(proceed);return;}
            root.gameObject.SetActive(false);
            GetComponent<MainMenuScene>()?.SetHeroVisible(true);
            GetComponent<MainMenuScene>()?.SetTankSelectorBackdrop(false);
            Focus(home);
        }
        void ShowPage(bool loadoutView)
        {
            inLoadout=loadoutView;tankPage.gameObject.SetActive(!inLoadout);loadoutPage.gameObject.SetActive(inLoadout);
            ApplyLoadoutBounds(loadoutPage);
            title.text=inLoadout?"BATTLE LOADOUT":"SELECT TANK";
            EnsureLoadoutView();ApplyContinueArt();
            UpdateView();Navigation();
        }
        void ApplyContinueArt()
        {
            if(inLoadout){ApplyLoadoutLayout();return;}
            ApplySummaryLayout();
            bool useSuppliedSprites=!inLoadout&&art&&art.tankCostButton&&art.tankCostButtonSelected;
            continueLabel.text=inLoadout?"START BATTLE":"CONTINUE";
            continueLabel.gameObject.SetActive(true);
            Fit(continueLabel.rectTransform,new Rect(.08f,.06f,.84f,.88f));
            continueLabel.alignment=TextAlignmentOptions.Center;
            continueLabel.textWrappingMode=TextWrappingModes.NoWrap;
            continueLabel.enableAutoSizing=true;
            continueLabel.fontSizeMin=16;
            continueLabel.fontSize=continueLabel.fontSizeMax=psg1Spacing?40:36;
            if(!useSuppliedSprites)proceed.image.overrideSprite=null;
            proceed.image.color=Color.white;
            var buttonRect=(RectTransform)proceed.transform;
            var statusRect=(RectTransform)status.transform;
            var buttonAspect=proceed.GetComponent<AspectRatioFitter>();
            status.gameObject.SetActive(inLoadout);
            if(useSuppliedSprites)
            {
                proceed.image.sprite=art.tankCostButton;
                proceed.image.type=Image.Type.Sliced;
                proceed.image.preserveAspect=false;
                proceed.image.canvasRenderer.SetColor(Color.white);
                // Add vertical padding while keeping the established footer width.
                float buttonWidth=ActiveContinueWidth;
                float buttonHeight=Mathf.Min(ActiveFooterHeight,buttonWidth*ContinueHeightScale*(art.continueInactive
                    ?art.continueInactive.rect.height/art.continueInactive.rect.width:1f/4.78f));
                proceed.image.pixelsPerUnitMultiplier=art.tankCostButton.rect.width/
                    (buttonWidth*Mathf.Max(.01f,proceed.image.pixelsPerUnit));
                buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(FooterRightAnchor,FooterBottomAnchor);
                buttonRect.pivot=new Vector2(1f,0);
                buttonRect.anchoredPosition=new Vector2(0,ContinueBottomInset+(ActiveFooterHeight-buttonHeight)*.5f);
                if(buttonAspect)buttonAspect.enabled=false;
                buttonRect.sizeDelta=new Vector2(buttonWidth,buttonHeight);
                proceed.spriteState=new SpriteState
                {
                    highlightedSprite=art.tankCostButtonSelected,
                    pressedSprite=art.tankCostButtonSelected,
                    selectedSprite=art.tankCostButtonSelected,
                    disabledSprite=art.tankCostButtonLocked?art.tankCostButtonLocked:art.tankCostButton
                };
                proceed.transition=Selectable.Transition.SpriteSwap;
            }
            else
            {
                if(buttonAspect)buttonAspect.enabled=false;
                if(FillTvOpening)
                {
                    float footerTop=root.rect.height-ContinueBottomInset-ActiveFooterHeight;
                    MainMenuScene.Place(buttonRect,root.rect.width-PsgContentInset-ActiveContinueWidth,
                        footerTop,ActiveContinueWidth,ActiveFooterHeight);
                    MainMenuScene.Place(statusRect,PsgContentInset,footerTop,
                        root.rect.width-PsgContentInset*2f-ActiveContinueWidth-FooterColumnGap,ActiveFooterHeight);
                }
                else
                {
                    Fit(buttonRect,new Rect(.68f,.88f,.29f,.105f));
                    Fit(statusRect,new Rect(.035f,.88f,.58f,.105f));
                }
                status.fontSize=status.fontSizeMax=19;
                proceed.transition=Selectable.Transition.ColorTint;
                proceed.image.sprite=theme.GoldPanel;
                proceed.image.type=Image.Type.Sliced;
                proceed.image.preserveAspect=false;
            }
            if(!inLoadout)
            {
                if(inLoadout){LayoutLoadoutFooter();return;}
            var balance=(RectTransform)fuel.transform.parent;
            foreach(var name in new[]{"Fuel can","Fuel","Fuel amount","Deployment heading","Deployment cost"}){var child=balance.Find(name);if(child)child.gameObject.SetActive(true);}
            if(status&&status.transform.parent==balance)status.transform.SetParent(root,false);
                if(buttonRect.parent!=balance)buttonRect.SetParent(balance,false);
                TvStatusFooterLayout.PlaceAction(buttonRect);
                if(useSuppliedSprites)proceed.image.pixelsPerUnitMultiplier=art.tankCostButton.rect.width/
                    (buttonRect.rect.width*Mathf.Max(.01f,proceed.image.pixelsPerUnit));
            }
            continueCaptionStyled=false;
            RefreshContinueCaption();
        }
        void LateUpdate(){if(!IsOpen&&(busy||accountLoading))CancelLoadoutRequests();RefreshContinueCaption();if(inLoadout)RefreshLoadoutActionText();}
        void RefreshContinueCaption()
        {
            if(!proceed||!continueLabel||!continueLabel.isActiveAndEnabled)return;
            var image=proceed.image;
            var sprite=image.overrideSprite?image.overrideSprite:image.sprite;
            bool navy=sprite==theme.GoldPanel||
                (art&&art.tankCostButtonSelected&&sprite==art.tankCostButtonSelected);
            if(continueCaptionStyled&&navy==continueCaptionNavy)return;
            textStyles.ApplyCleanButton(continueLabel,navy);
            continueCaptionNavy=navy;
            continueCaptionStyled=true;
        }
        void SelectTank(int index)
        {
            if(busy||pendingFuel!=null||paidTier>=0)return;
            selected=index;UpdateView();Focus(tankButtons[index]);
        }
        void UpdateView()
        {
            fuelAmount.text=Number(account?["fuelBalance"]);
            deploymentCost.text=Names[selected]+" / "+(selected+1)+" FUEL";
            for(int i=0;i<4;i++)
            {
                bool available=!busy&&paidTier<0&&pendingFuel==null;
                var card=tankButtons[i];
                card.interactable=available;
                bool supplied=art&&art.tankCardSelected&&art.tankCardAvailable&&art.tankCardUnavailable;
                card.image.sprite=supplied?(available?(i==selected?art.tankCardSelected:art.tankCardAvailable):art.tankCardUnavailable):
                    (i==selected?theme.GoldPanel:theme.CreamPanel);
                card.image.type=Image.Type.Sliced;
                card.image.pixelsPerUnitMultiplier=supplied?4f:1f;
                card.image.color=Color.white;
                ApplyCostBadgeSelection(card,i);
                if(supplied)
                {
                    var colors=card.colors;
                    colors.normalColor=Color.white;
                    colors.highlightedColor=colors.selectedColor=Color.white;
                    colors.pressedColor=new Color(.92f,.92f,.92f);
                    colors.disabledColor=Color.white;
                    card.colors=colors;
                }
                CardSelectionHighlight.Ensure(card.image).SetActivated(available&&i==selected);
                string item=(string)loadout[Slots[i]];
                slotNames[i].text="SLOT "+(i+1);SetIcon(slotIcons[i],item);
                slotEmpty[i].gameObject.SetActive(false);
                slotButtons[i].interactable=!busy;
            }
            wallet.text="WALLET\n"+(account==null?"NOT CONNECTED":"CONNECTED")+"\nBATC  "+Number(account?["tokenBalance"])+"\nSOL  "+Number(account?["solBalance"]);
            for(int i=0;i<8;i++)inventoryLabels[i].text=Number(account?["inventory"]?[Items[i]]);
            back.interactable=!busy;
            foreach(var card in classifiedButtons)if(card)card.interactable=!busy&&paidTier<0&&pendingFuel==null;
            proceed.interactable=!busy&&!launchRequested;RefreshLoadoutView();
            Navigation();
        }
        static string Number(JToken value)=>value==null?"0":value.ToString();
        bool EditorFixtureActive
        {
            get {
#if UNITY_EDITOR
                return EditorStartRoutineOverride!=null;
#else
                return false;
#endif
            }
        }
        IEnumerator LoadAccount()
        {
            int generation=loadoutGeneration;
            yield return api.Request("GET","/api/economy/account",null,(code,body,error)=>
            {
                if(generation!=loadoutGeneration||!IsOpen)return;
                accountLoading=false;inventoryRoutine=null;
                if(code>=200&&code<300&&body?["authenticated"]?.Type==JTokenType.Boolean&&YesLoadout(body["authenticated"])&&body["account"] is JObject data&&ValidLoadoutAccount(data)&&AccountMatchesPlayer(data))
                {
                    account=data;inventoryUnavailable=false;RestoreFuelReceipt();
                    var stored=OwnedDraft(data["loadout"] as JObject);loadout=loadoutDirty?OwnedDraft(loadout):stored;
                    status.text=inLoadout?"SELECT A SLOT, THEN CHOOSE AN OWNED POWER":"";
                }
                else if(code==401||code>=200&&code<300&&body?["authenticated"]?.Type==JTokenType.Boolean&&!YesLoadout(body["authenticated"]))
                {account=null;loadout=new JObject();loadoutDirty=false;inventoryUnavailable=false;status.text=inLoadout?"NO CONNECTED INVENTORY • EMPTY LOADOUT IS READY":"";}
                else {inventoryUnavailable=true;status.text=inLoadout?"INVENTORY UNAVAILABLE • REOPEN LOADOUT TO RETRY":"";}
                UpdateView();
            });
        }
        static bool YesLoadout(JToken token)=>token?.Type==JTokenType.Boolean&&token.Value<bool>();
        void Continue()
        {
            if(busy||launchRequested)return;
            if(!inLoadout)
            {
                ShowPage(true);if(inventoryUnavailable&&!accountLoading)RefreshLoadoutAccount();status.text=accountLoading?"LOADING INVENTORY...":inventoryUnavailable?"INVENTORY UNAVAILABLE • REOPEN LOADOUT TO RETRY":account==null?"EMPTY LOADOUT IS READY • START WHEN YOU ARE READY":"SELECT A SLOT, THEN CHOOSE AN OWNED POWER";
                Focus(slotButtons[activeSlot]);return;
            }
            if(UsesLocalGuestLoadout){LaunchLoadoutBattle();return;}
            if(accountLoading){status.text="WAITING FOR INVENTORY • PLEASE TRY START AGAIN";return;}
            if(inventoryUnavailable&&loadout.Count>0){status.text="INVENTORY UNAVAILABLE • REOPEN LOADOUT BEFORE STARTING";return;}
            if(account==null||!loadoutDirty){LaunchLoadoutBattle();return;}
            StartLoadoutRoutine(SaveLoadoutAndLaunch());
        }
        IEnumerator SaveLoadoutAndLaunch()
        {
            int generation=loadoutGeneration;var desired=(JObject)loadout.DeepClone();
            busy=true;status.text="SAVING LOADOUT...";UpdateView();bool saved=false;
            yield return api.Request("PUT","/api/economy/account",new JObject{{"expectedPlayerId",(string)account["playerId"]},{"account",new JObject{{"loadout",desired}}}},(code,body,error)=>
            {
                if(generation!=loadoutGeneration||!IsLoadout)return;
                if(code>=200&&code<300&&YesLoadout(body?["authenticated"])&&body["account"] is JObject data&&ValidLoadoutAccount(data)&&AccountMatchesPlayer(data)&&JToken.DeepEquals(desired,data["loadout"]))
                {account=data;saved=true;loadoutDirty=false;}
            });
            if(generation!=loadoutGeneration||!IsLoadout)yield break;
            busy=false;if(saved)LaunchLoadoutBattle();else {status.text="LOADOUT NOT SAVED • RETRY START BATTLE";UpdateView();}
        }
        void Navigation()
        {
            if(inLoadout){LoadoutNavigation();return;}
            if(!inLoadout)
            {
                bool available=!busy&&paidTier<0&&pendingFuel==null;
                back.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnDown=available?tankButtons[selected]:proceed,selectOnUp=proceed,
                    selectOnLeft=proceed,selectOnRight=available?tankButtons[selected]:proceed};
                int count=tankButtons.Length+classifiedButtons.Length;
                int lastRow=(count-1)/rosterColumns*rosterColumns;
                proceed.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp=available?RosterCard(Mathf.Min(lastRow+selected%rosterColumns,count-1)):back,selectOnDown=back,
                    selectOnLeft=available?RosterCard(count-1):back,selectOnRight=back};
                for(int i=0;i<count;i++)
                {
                    int rowStart=i/rosterColumns*rosterColumns;
                    int rowEnd=Mathf.Min(rowStart+rosterColumns,count)-1;
                    RosterCard(i).navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,
                        selectOnUp=i>=rosterColumns?RosterCard(i-rosterColumns):back,
                        selectOnDown=rowStart+rosterColumns<count?RosterCard(Mathf.Min(i+rosterColumns,count-1)):proceed,
                        selectOnLeft=RosterCard(i>rowStart?i-1:rowEnd),selectOnRight=RosterCard(i<rowEnd?i+1:rowStart)};
                }
                return;
            }
            selectable.Clear();selectable.Add(back);
            var cards=inLoadout?slotButtons:tankButtons;
            bool editable=inLoadout||(paidTier<0&&pendingFuel==null);
            if(editable)selectable.AddRange(cards);selectable.Add(proceed);
            for(int i=0;i<selectable.Count;i++)
            {
                var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit};
                nav.selectOnUp=selectable[(i+selectable.Count-1)%selectable.Count];nav.selectOnDown=selectable[(i+1)%selectable.Count];
                nav.selectOnLeft=nav.selectOnUp;nav.selectOnRight=nav.selectOnDown;selectable[i].navigation=nav;
            }
            for(int i=0;editable&&i<4;i++)
            {
                var n=cards[i].navigation;n.selectOnUp=inLoadout&&i>=2?cards[i-2]:back;n.selectOnDown=inLoadout&&i<2?cards[i+2]:proceed;
                n.selectOnLeft=cards[(i+3)%4];n.selectOnRight=cards[(i+1)%4];cards[i].navigation=n;
            }
        }
        Button RosterCard(int index)=>index<tankButtons.Length?tankButtons[index]:classifiedButtons[index-tankButtons.Length];
        public void KeepControllerFocus()
        {
            Psg1UiNavigation.KeepFocus(root, inLoadout ? slotButtons[0] : tankButtons[selected]);
        }
        static void Focus(Button button){if(EventSystem.current&&button)EventSystem.current.SetSelectedGameObject(button.gameObject);}
        RectTransform Panel(string name,Transform parent,Sprite sprite,Rect area)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;Fit(rect,area);var image=go.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;
            image.type=UnityEngine.UI.Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        TMP_Text Label(string name,Transform parent,string text,Rect area,float size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);Fit((RectTransform)go.transform,area);
            var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.color=color;label.fontSize=size;label.enableAutoSizing=true;
            label.fontSizeMin=10;label.fontSizeMax=size;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            if(ArcadeTextStyles.IsWhite(color))textStyles.Apply(label,ArcadeTextTreatment.WhiteButton);
            return label;
        }
        Button Button(string name,Transform parent,string caption,Rect area,UnityEngine.Events.UnityAction click)
        {
            var rect=Panel(name,parent,theme.CreamPanel,area);var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
            button.image.raycastTarget=true;var colors=button.colors;colors.highlightedColor=new Color(.80f,.94f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(1,.65f,.1f);button.colors=colors;
            button.onClick.AddListener(click);if(!string.IsNullOrEmpty(caption))Label("Label",rect,caption,new Rect(.03f,.04f,.94f,.92f),30,theme.Navy);return button;
        }
        RawImage Raw(string name,Transform parent,Rect area)
        {
            var holder=new GameObject(name+" bounds",typeof(RectTransform));holder.layer=parent.gameObject.layer;holder.transform.SetParent(parent,false);Fit((RectTransform)holder.transform,area);
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.layer=parent.gameObject.layer;go.transform.SetParent(holder.transform,false);Fit((RectTransform)go.transform,new Rect(0,0,1,1));
            var aspect=go.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1;
            var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;
        }
        void SetIcon(RawImage image,string item)
        {
            string type=item=="base-defence"?"defence":item=="zoom-out"?"zoomout":item=="extra-life"?"life":item;
            image.texture=art?art.powerups:null;
            if(image.texture&&BattleHud.TryPowerupUv(type,out var uv)){image.uvRect=uv;image.color=Color.white;}
            else image.color=Color.clear;
        }
        static void Fit(RectTransform rect,Rect a)
        {rect.anchorMin=new Vector2(a.x,1-a.y-a.height);rect.anchorMax=new Vector2(a.x+a.width,1-a.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void OnDestroy()
        {
            if(api)api.PlayerLoaded-=OnLoadoutPlayer;if(loadoutGlass)Destroy(loadoutGlass);textStyles.Dispose();
            if(root)Destroy(root.gameObject);
            if(ownsFont&&font)Destroy(font);
        }
    }
}
