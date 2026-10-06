using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class PreBattleScreen
    {
        RectTransform loadoutWorkspace,loadoutInventory,loadoutPaper,inventoryHeader,inventoryHint;
        TMP_Text loadoutTankName,loadoutInstruction,inventoryHintText;
        RawImage loadoutTank;
        Image deploymentPlatform;
        TankGroundShadow tankPreviewShadow,platformPreviewShadow;
        ShopArt loadoutShopArt;
        LoadoutArt loadoutArt;
        TankRosterScroll loadoutInventoryScroll;
        readonly Button[] loadoutRows=new Button[8];
        readonly Image[] equippedArt=new Image[4],emptyArt=new Image[4],slotActionSkin=new Image[4];
        readonly TMP_Text[] slotActions=new TMP_Text[4],equippedNames=new TMP_Text[4];
        readonly TankGroundShadow[] equipmentShadows=new TankGroundShadow[4];
        Button clearSlot;
        Material loadoutGlass;
        int activeSlot;
        public bool IsLoadout=>IsOpen&&inLoadout;
        public bool IsBusy=>busy;
        public bool IsInventoryLoading=>accountLoading;
        public string LoadoutStatus=>status?status.text:"";
        public int SelectedTankTier=>selected;
        public int ActiveSlot=>activeSlot;
        public RectTransform Root=>root;
        public TankRosterScroll InventoryScroll=>loadoutInventoryScroll;
        public string EquippedPower(int slot)=>slot>=0&&slot<4?(string)loadout[Slots[slot]]:null;

        RectTransform LoadoutPanel(string name,Transform parent,Sprite sprite)
        {
            var existing=parent.Find(name) as RectTransform;
            var rect=existing?existing:Panel(name,parent,sprite,new Rect(0,0,1,1));
            var image=rect.GetComponent<Image>();if(!image)image=rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.type=Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;
            return rect;
        }
        TMP_Text LoadoutLabel(string name,Transform parent,string caption,Rect area,float size,bool white=false)
        {
            var existing=parent.Find(name);var text=existing?existing.GetComponent<TMP_Text>():Label(name,parent,caption,area,size,theme.Navy);
            Fit(text.rectTransform,area);text.text=caption;text.font=font;text.fontSize=text.fontSizeMax=size;text.fontSizeMin=12;
            text.enableAutoSizing=true;text.textWrappingMode=TextWrappingModes.NoWrap;
            textStyles.Apply(text,ArcadeTextTreatment.PrizeAmount);if(white)text.color=Color.white;return text;
        }
        Image LoadoutIcon(string name,Transform parent,Sprite sprite,Rect area)
        {
            var rect=LoadoutPanel(name,parent,sprite);Fit(rect,area);var image=rect.GetComponent<Image>();image.type=Image.Type.Simple;image.preserveAspect=true;return image;
        }
        Button LoadoutAction(string name,Transform parent,string caption,UnityEngine.Events.UnityAction action)
        {
            var existing=parent.Find(name);var button=existing?existing.GetComponent<Button>():Button(name,parent,caption,new Rect(0,0,1,1),action);
            BindClick(button,action);button.image.sprite=art.tankCostButton;button.image.type=Image.Type.Sliced;button.image.color=Color.white;
            button.transition=Selectable.Transition.SpriteSwap;button.spriteState=new SpriteState{highlightedSprite=art.tankCostButtonSelected,selectedSprite=art.tankCostButtonSelected,pressedSprite=art.tankCostButtonSelected,disabledSprite=art.tankCostButtonLocked};
            button.targetGraphic=button.image;button.colors=ColorBlock.defaultColorBlock;
            var label=button.GetComponentInChildren<TMP_Text>(true);label.text=caption;label.fontSize=label.fontSizeMax=24;label.fontSizeMin=12;
            Fit(label.rectTransform,new Rect(.05f,.04f,.9f,.92f));textStyles.ApplyCleanButton(label,false);return button;
        }
        TankGroundShadow LoadoutShadow(Transform parent,string name,Rect area)
        {
            var old=parent.Find(name);var go=old?old.gameObject:new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));
            go.layer=parent.gameObject.layer;if(!old)go.transform.SetParent(parent,false);Fit((RectTransform)go.transform,area);
            var shadow=go.GetComponent<TankGroundShadow>();shadow.color=new Color32(35,25,13,112);shadow.raycastTarget=false;return shadow;
        }
        Sprite PowerArtwork(string item)
        {
            if(!loadoutShopArt||string.IsNullOrEmpty(item))return null;
            for(int i=0;i<ShopCatalog.Products.Length;i++)if(ShopCatalog.Products[i].InventoryId==item&&i<loadoutShopArt.products.Length)return loadoutShopArt.products[i];
            return null;
        }
        static string PowerName(string item)
        {if(string.IsNullOrEmpty(item))return "EMPTY";foreach(var product in ShopCatalog.Products)if(product.InventoryId==item)return product.Title;return "EMPTY";}
        void EnsureLoadoutView()
        {
            loadoutShopArt=Resources.Load<ShopArt>("ShopArt");loadoutArt=Resources.Load<LoadoutArt>("LoadoutArt");
            var legacy=loadoutPage.Find("Wallet and inventory");if(legacy)legacy.gameObject.SetActive(false);
            loadoutWorkspace=LoadoutPanel("Workspace",loadoutPage,theme.CreamPanel);loadoutWorkspace.GetComponent<Image>().pixelsPerUnitMultiplier=2;
            loadoutTankName=LoadoutLabel("Tank name",loadoutWorkspace,Names[selected]+" LOADOUT",new Rect(.02f,.01f,.96f,.08f),30);
            loadoutInstruction=LoadoutLabel("Instruction",loadoutWorkspace,"EQUIP UP TO FOUR POWERS",new Rect(.02f,.09f,.96f,.055f),21);loadoutInstruction.color=new Color32(58,93,119,255);
            var preview=LoadoutPanel("Tank preview",loadoutWorkspace,null);
            platformPreviewShadow=LoadoutShadow(preview,"Platform shadow",new Rect(.04f,.70f,.92f,.16f));
            tankPreviewShadow=LoadoutShadow(preview,"Ground shadow",new Rect(.10f,.59f,.8f,.18f));
            deploymentPlatform=LoadoutIcon("Deployment platform",preview,loadoutArt?loadoutArt.deploymentPlatform:null,new Rect(.015f,.46f,.97f,.44f));
            platformPreviewShadow.transform.SetSiblingIndex(0);deploymentPlatform.transform.SetSiblingIndex(1);tankPreviewShadow.transform.SetSiblingIndex(2);
            var model=preview.Find("Selected tank bounds/Selected tank");loadoutTank=model?model.GetComponent<RawImage>():Raw("Selected tank",preview,new Rect(.02f,.04f,.96f,.70f));
            for(int i=0;i<4;i++)
            {
                int index=i;var button=slotButtons[i];button.transform.SetParent(loadoutWorkspace,false);BindClick(button,()=>ChooseLoadoutSlot(index));
                button.image.type=Image.Type.Sliced;button.image.pixelsPerUnitMultiplier=4;button.image.raycastTarget=true;
                button.transition=Selectable.Transition.ColorTint;var colors=ColorBlock.defaultColorBlock;colors.highlightedColor=colors.selectedColor=Color.white;colors.disabledColor=Color.white;button.colors=colors;
                var heading=LoadoutPanel("Slot plate",button.transform,art.tankTitlePanel);heading.GetComponent<Image>().pixelsPerUnitMultiplier=4;heading.gameObject.SetActive(false);
                var titleText=(button.transform.Find("Slot heading")??heading.Find("Slot heading")).GetComponent<TMP_Text>();titleText.transform.SetParent(heading,false);Fit(titleText.rectTransform,new Rect(.06f,.04f,.88f,.92f));textStyles.ApplyCleanButton(titleText,false);
                equipmentShadows[i]=LoadoutShadow(button.transform,"Power shadow",new Rect(.24f,.53f,.52f,.08f));equipmentShadows[i].transform.SetSiblingIndex(1);
                emptyArt[i]=LoadoutIcon("Empty equipment socket",button.transform,loadoutArt?loadoutArt.emptySocket:null,new Rect(.22f,.22f,.56f,.34f));
                equippedArt[i]=LoadoutIcon("Power artwork",button.transform,null,new Rect(.18f,.22f,.64f,.34f));
                Fit(slotIcons[i].transform.parent as RectTransform,new Rect(.18f,.22f,.64f,.34f));
                slotEmpty[i].text="";slotEmpty[i].gameObject.SetActive(false);
                slotNames[i].textWrappingMode=TextWrappingModes.NoWrap;slotNames[i].fontSizeMin=12;textStyles.Apply(slotNames[i],ArcadeTextTreatment.PrizeAmount);
                equippedNames[i]=LoadoutLabel("Power name",button.transform,"",new Rect(.04f,.655f,.92f,.115f),22);
                var action=LoadoutPanel("Change plate",button.transform,art.tankCostButton);slotActionSkin[i]=action.GetComponent<Image>();
                slotActions[i]=(button.transform.Find("Change")??action.Find("Change")).GetComponent<TMP_Text>();slotActions[i].transform.SetParent(action,false);slotActions[i].text="CHANGE";
                Fit(slotActions[i].rectTransform,new Rect(.04f,.04f,.92f,.92f));textStyles.ApplyCleanButton(slotActions[i],false);
                CardSelectionHighlight.Ensure(button.image);
            }
            loadoutInventory=LoadoutPanel("Inventory",loadoutPage,theme.BlueFrame);loadoutPaper=LoadoutPanel("Paper",loadoutInventory,theme.Rounded);
            inventoryHeader=LoadoutPanel("Heading bar",loadoutPaper,theme.Rounded);inventoryHeader.GetComponent<Image>().pixelsPerUnitMultiplier=5;
            LoadoutIcon("Crate",inventoryHeader,loadoutShopArt?loadoutShopArt.supplyCrate:null,new Rect(.025f,.1f,.18f,.8f));
            LoadoutLabel("Heading",inventoryHeader,"INVENTORY",new Rect(.25f,.02f,.72f,.96f),24);
            inventoryHint=LoadoutPanel("Slot hint",loadoutPaper,null);inventoryHintText=LoadoutLabel("Label",inventoryHint,"SLOT 01 • CHOOSE A POWER",new Rect(.02f,0,.96f,1),18);
            var scrollRoot=LoadoutPanel("Scroll",loadoutPaper,null);loadoutInventoryScroll=scrollRoot.GetComponent<TankRosterScroll>();if(!loadoutInventoryScroll)loadoutInventoryScroll=scrollRoot.gameObject.AddComponent<TankRosterScroll>();
            var viewport=LoadoutPanel("Viewport",scrollRoot,null);Fit(viewport,new Rect(0,0,1,1));viewport.GetComponent<Image>().raycastTarget=true;viewport.GetComponent<Image>().color=Color.white;
            var mask=viewport.GetComponent<Mask>();if(!mask)mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var content=LoadoutPanel("Content",viewport,null);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.offsetMin=content.offsetMax=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>();if(!layout)layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=3;layout.padding=new RectOffset(2,2,2,2);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fitter=content.GetComponent<ContentSizeFitter>();if(!fitter)fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            loadoutInventoryScroll.content=content;loadoutInventoryScroll.viewport=viewport;loadoutInventoryScroll.horizontal=false;loadoutInventoryScroll.vertical=true;loadoutInventoryScroll.scrollSensitivity=28;loadoutInventoryScroll.movementType=ScrollRect.MovementType.Clamped;
            viewport.offsetMax=new Vector2(-14,0);
            var rail=LoadoutPanel("Scrollbar",scrollRoot,theme.DarkPanel);rail.anchorMin=new Vector2(1,0);rail.anchorMax=Vector2.one;rail.pivot=new Vector2(1,.5f);rail.anchoredPosition=new Vector2(-1,0);rail.sizeDelta=new Vector2(10,-4);rail.GetComponent<Image>().pixelsPerUnitMultiplier=4;rail.GetComponent<Image>().raycastTarget=true;
            var sliding=LoadoutPanel("Sliding area",rail,null);Fit(sliding,new Rect(.12f,.01f,.76f,.98f));var thumb=LoadoutPanel("Thumb",sliding,theme.GoldPanel);Fit(thumb,new Rect(0,0,1,1));thumb.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            var bar=rail.GetComponent<Scrollbar>();if(!bar)bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;bar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            loadoutInventoryScroll.verticalScrollbar=bar;loadoutInventoryScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            for(int i=0;i<8;i++)
            {
                int index=i;string item=Items[i];var row=LoadoutPanel(item,content,theme.Rounded);row.SetSiblingIndex(i);row.GetComponent<Image>().pixelsPerUnitMultiplier=5;
                var element=row.GetComponent<LayoutElement>();if(!element)element=row.gameObject.AddComponent<LayoutElement>();element.preferredHeight=48;
                var button=row.GetComponent<Button>();if(!button)button=row.gameObject.AddComponent<Button>();loadoutRows[i]=button;button.targetGraphic=row.GetComponent<Image>();button.image.raycastTarget=true;
                BindClick(button,()=>EquipLoadoutPower(item));var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.white;colors.highlightedColor=colors.selectedColor=new Color(.68f,.88f,1);colors.disabledColor=Color.white;button.colors=colors;
                var plate=LoadoutPanel("Icon tile",row,theme.CreamPanel);plate.GetComponent<Image>().pixelsPerUnitMultiplier=3;
                LoadoutShadow(plate,"Ground shadow",new Rect(.12f,.77f,.76f,.14f));LoadoutIcon("Artwork",plate,PowerArtwork(item),new Rect(.02f,.015f,.96f,.96f));
                var label=LoadoutLabel("Name",row,PowerName(item),new Rect(.29f,.05f,.49f,.9f),20);label.alignment=TextAlignmentOptions.MidlineLeft;
                if(item=="base-defence"||item=="extra-life"||item=="zoom-out"){label.text=PowerName(item).Replace(' ','\n');label.textWrappingMode=TextWrappingModes.Normal;}
                var badge=LoadoutPanel("Count badge",row,theme.DarkPanel);badge.GetComponent<Image>().pixelsPerUnitMultiplier=3;
                inventoryLabels[index]=LoadoutLabel("Count",badge,"—",new Rect(.04f,.04f,.92f,.92f),28,true);
                var focus=row.GetComponent<RankingRowFocus>();if(!focus)focus=row.gameObject.AddComponent<RankingRowFocus>();focus.Configure(loadoutInventoryScroll);
            }
            var balance=(RectTransform)fuel.transform.parent;
            var previousClear=loadoutPaper.Find("Clear slot");if(previousClear)previousClear.SetParent(balance,false);
            clearSlot=LoadoutAction("Clear slot",balance,"CLEAR SLOT",ClearLoadoutSlot);clearSlot.gameObject.SetActive(inLoadout);
            var previousRefresh=loadoutPaper.Find("Refresh");if(previousRefresh)previousRefresh.gameObject.SetActive(false);
        }
        void LayoutLoadoutFooter()
        {
            var balance=(RectTransform)fuel.transform.parent;new TvStatusFooterLayout(root).Place(balance);
            foreach(var name in new[]{"Fuel can","Fuel","Fuel amount","Deployment heading","Deployment cost"})balance.Find(name).gameObject.SetActive(false);
            if(status.transform.parent!=balance)status.transform.SetParent(balance,false);Fit(status.rectTransform,new Rect(.024f,.08f,.535f,.84f));
            if(proceed.transform.parent!=balance)proceed.transform.SetParent(balance,false);TvStatusFooterLayout.PlaceAction((RectTransform)proceed.transform);
            if(clearSlot.transform.parent!=balance)clearSlot.transform.SetParent(balance,false);
            Fit((RectTransform)clearSlot.transform,new Rect(.58f,.11f,.175f,.78f));TvTitleHeaderLayout.FitSkin(clearSlot.image,((RectTransform)clearSlot.transform).rect.height);
            var clearCaption=clearSlot.GetComponentInChildren<TMP_Text>(true);clearCaption.fontSize=clearCaption.fontSizeMax=psg1Spacing?34:30;clearCaption.fontSizeMin=13;
            status.fontSize=status.fontSizeMax=psg1Spacing?28:22;status.fontSizeMin=13;status.enableAutoSizing=true;status.textWrappingMode=TextWrappingModes.NoWrap;status.gameObject.SetActive(true);
            textStyles.Apply(status,ArcadeTextTreatment.PrizeAmount);
        }
        void ApplyLoadoutLayout()
        {
            if(!loadoutWorkspace)return;
            ApplyLoadoutBounds(loadoutPage);LayoutLoadoutFooter();float width=loadoutPage.rect.width,height=loadoutPage.rect.height,gap=8;
            float inventoryWidth=Mathf.Clamp(width*.28f,225,330);float workspaceWidth=width-inventoryWidth-gap;
            MainMenuScene.Place(loadoutInventory,0,0,inventoryWidth,height);MainMenuScene.Place(loadoutWorkspace,inventoryWidth+gap,0,workspaceWidth,height);
            GetComponent<MainMenuScene>()?.ApplyDetailSurface(loadoutInventory,loadoutPaper,ref loadoutGlass);
            float headerHeight=TvSidebarStyle.HeaderHeight;
            MainMenuScene.Place(inventoryHeader,3,3,loadoutPaper.rect.width-6,headerHeight);
            var heading=inventoryHeader.Find("Heading").GetComponent<TMP_Text>();TvSidebarStyle.Apply(heading,TvSidebarStyle.HeadingSize(psg1Spacing||androidSpacing),textStyles);
            TvSidebarStyle.PlaceHeaderIcon(inventoryHeader.Find("Crate").GetComponent<Image>(),TvSidebarStyle.InventoryCrateBounds);
            MainMenuScene.Place(inventoryHint,4,headerHeight+5,loadoutPaper.rect.width-8,27);inventoryHintText.fontSize=inventoryHintText.fontSizeMax=psg1Spacing?22:18;
            float paperHeight=loadoutPaper.rect.height;
            MainMenuScene.Place((RectTransform)loadoutInventoryScroll.transform,4,headerHeight+34,loadoutPaper.rect.width-8,Mathf.Max(45,paperHeight-headerHeight-41));
            float rowHeight=Mathf.Max(psg1Spacing||androidSpacing?54:47,(loadoutInventoryScroll.viewport.rect.height-25)/8f);
            foreach(var row in loadoutRows)
            {
                var rect=(RectTransform)row.transform;rect.GetComponent<LayoutElement>().preferredHeight=rowHeight;
                float rowWidth=loadoutPaper.rect.width-26,iconSize=Mathf.Min(rowHeight-6,rowWidth*.25f),countWidth=Mathf.Min(rowWidth*.18f,rowHeight*.67f);
                MainMenuScene.Place(rect.Find("Icon tile") as RectTransform,2,(rowHeight-iconSize)*.5f,iconSize,iconSize);
                MainMenuScene.Place(rect.Find("Name") as RectTransform,iconSize+8,3,rowWidth-iconSize-countWidth-16,rowHeight-6);
                MainMenuScene.Place(rect.Find("Count badge") as RectTransform,rowWidth-countWidth-2,(rowHeight-countWidth)*.5f,countWidth,countWidth);
                TvSidebarStyle.Apply(rect.Find("Name").GetComponent<TMP_Text>(),TvSidebarStyle.BodySize(psg1Spacing||androidSpacing),textStyles);
                var count=rect.Find("Count badge/Count").GetComponent<TMP_Text>();TvSidebarStyle.Apply(count,TvSidebarStyle.CountSize(psg1Spacing||androidSpacing),textStyles);count.color=Color.white;
            }
            float columnWidth=Mathf.Clamp(workspaceWidth*.27f,110,210),top=Mathf.Clamp(height*.15f,52,78),rowHeightAvailable=(height-top-20)*.5f;
            float slotWidth=columnWidth*.84f,slotHeight=rowHeightAvailable*.84f;
            for(int i=0;i<4;i++)
            {
                var rect=(RectTransform)slotButtons[i].transform;MainMenuScene.Place(rect,(i%2==0?8:workspaceWidth-columnWidth-8)+(columnWidth-slotWidth)*.5f,top+(i/2)*(rowHeightAvailable+5)+(rowHeightAvailable-slotHeight)*.5f,slotWidth,slotHeight);
                Fit(rect.Find("Slot plate") as RectTransform,new Rect(.04f,.035f,.92f,.16f));
                var text=rect.Find("Slot plate/Slot heading").GetComponent<TMP_Text>();text.fontSize=text.fontSizeMax=psg1Spacing?26:22;
                Fit(equippedArt[i].rectTransform,new Rect(.06f,.18f,.88f,.47f));Fit(emptyArt[i].rectTransform,new Rect(.06f,.175f,.88f,.60f));
                Fit(slotIcons[i].transform.parent as RectTransform,new Rect(.06f,.18f,.88f,.47f));Fit(equipmentShadows[i].rectTransform,new Rect(.24f,.585f,.52f,.065f));
                Fit(slotNames[i].rectTransform,new Rect(.085f,.045f,.83f,.125f));slotNames[i].fontSize=slotNames[i].fontSizeMax=psg1Spacing?28:24;
                Fit(equippedNames[i].rectTransform,new Rect(.04f,.655f,.92f,.115f));equippedNames[i].fontSize=equippedNames[i].fontSizeMax=psg1Spacing?26:22;
                Fit(slotActionSkin[i].rectTransform,new Rect(.20f,.79f,.60f,.15f));TvTitleHeaderLayout.FitSkin(slotActionSkin[i],slotHeight*.15f);
                slotActions[i].fontSize=slotActions[i].fontSizeMax=psg1Spacing?25:22;
            }
            float middleLeft=columnWidth+17,middleWidth=workspaceWidth-middleLeft*2;
            var preview=loadoutWorkspace.Find("Tank preview") as RectTransform;MainMenuScene.Place(preview,middleLeft,top+5,middleWidth,Mathf.Max(90,height-top-20));
            float platformWidth=workspaceWidth*.58f,platformHeight=platformWidth*2f/3f,restingY=preview.rect.height*.57f;
            float tankAspect=loadoutTank.texture?(float)loadoutTank.texture.width/loadoutTank.texture.height:1.33f;
            float tankWidth=Mathf.Min(platformWidth*.65f,middleWidth*.90f,preview.rect.height*.66f*tankAspect),tankHeight=tankWidth/tankAspect;
            MainMenuScene.Place(deploymentPlatform.rectTransform,(middleWidth-platformWidth)*.5f,restingY-platformHeight*.52f,platformWidth,platformHeight);
            MainMenuScene.Place(loadoutTank.transform.parent as RectTransform,(middleWidth-tankWidth)*.5f,restingY-tankHeight,tankWidth,tankHeight);
            MainMenuScene.Place(platformPreviewShadow.rectTransform,(middleWidth-platformWidth*.94f)*.5f,restingY+platformHeight*.26f-platformWidth*.075f,platformWidth*.94f,platformWidth*.15f);
            MainMenuScene.Place(tankPreviewShadow.rectTransform,(middleWidth-tankWidth*.94f)*.5f,restingY-tankWidth*.075f,tankWidth*.94f,tankWidth*.18f);
            proceed.image.sprite=art.tankCostButton;proceed.image.overrideSprite=null;proceed.image.type=Image.Type.Sliced;proceed.image.color=Color.white;proceed.transition=Selectable.Transition.SpriteSwap;
            proceed.spriteState=new SpriteState{highlightedSprite=art.tankCostButtonSelected,selectedSprite=art.tankCostButtonSelected,pressedSprite=art.tankCostButtonSelected,disabledSprite=art.tankCostButtonLocked};TvTitleHeaderLayout.FitSkin(proceed.image,((RectTransform)proceed.transform).rect.height);
            continueLabel.text="START BATTLE";continueLabel.fontSize=continueLabel.fontSizeMax=psg1Spacing?34:30;continueLabel.fontSizeMin=13;continueCaptionStyled=false;
            RefreshLoadoutView();
        }
        void RefreshLoadoutView()
        {
            if(!loadoutWorkspace)return;
            loadoutTankName.text=Names[selected]+" LOADOUT";loadoutTank.texture=art.tanks[selected];loadoutTank.color=loadoutTank.texture?Color.white:Color.clear;
            var aspect=loadoutTank.GetComponent<AspectRatioFitter>();aspect.aspectRatio=loadoutTank.texture?(float)loadoutTank.texture.width/loadoutTank.texture.height:1;
            deploymentPlatform.sprite=loadoutArt?loadoutArt.deploymentPlatform:null;deploymentPlatform.enabled=deploymentPlatform.sprite;
            platformPreviewShadow.gameObject.SetActive(deploymentPlatform.enabled);tankPreviewShadow.gameObject.SetActive(loadoutTank.texture);
            inventoryHintText.text=accountLoading?"LOADING INVENTORY...":inventoryUnavailable?"INVENTORY UNAVAILABLE":account==null?"INVENTORY NOT CONNECTED":"SLOT 0"+(activeSlot+1)+" • CHOOSE A POWER";
            for(int i=0;i<4;i++)
            {
                string item=EquippedPower(i);bool filled=item!=null,active=i==activeSlot;
                slotButtons[i].image.sprite=active?art.tankCardSelected:art.tankCardAvailable;slotButtons[i].image.color=Color.white;CardSelectionHighlight.Ensure(slotButtons[i].image).SetActivated(active);
                equippedArt[i].sprite=PowerArtwork(item);equippedArt[i].color=equippedArt[i].sprite?Color.white:Color.clear;emptyArt[i].enabled=!filled&&emptyArt[i].sprite;
                SetIcon(slotIcons[i],item);slotIcons[i].gameObject.SetActive(filled&&!equippedArt[i].sprite);slotEmpty[i].gameObject.SetActive(false);
                slotNames[i].text="SLOT "+(i+1);equippedNames[i].text=filled?PowerName(item):"";equippedNames[i].gameObject.SetActive(filled);textStyles.Apply(equippedNames[i],ArcadeTextTreatment.PrizeAmount);equipmentShadows[i].gameObject.SetActive(filled);slotActionSkin[i].sprite=active?art.tankCostButtonSelected:art.tankCostButton;
                textStyles.ApplyCleanButton(slotActions[i],active);textStyles.Apply(slotNames[i],ArcadeTextTreatment.PrizeAmount);
            }
            for(int i=0;i<8;i++)
            {
                string item=Items[i];int count=OwnedPowerCount(item);inventoryLabels[i].text=account!=null&&!inventoryUnavailable?count.ToString("N0"):"—";
                loadoutRows[i].interactable=!busy&&!accountLoading;loadoutRows[i].image.color=EquippedPower(activeSlot)==item?new Color32(255,223,119,255):Color.white;
                var label=loadoutRows[i].transform.Find("Name").GetComponent<TMP_Text>();label.color=count>0&&!inventoryUnavailable?new Color32(6,29,54,255):new Color32(70,91,109,255);
            }
            clearSlot.interactable=!busy&&EquippedPower(activeSlot)!=null;
            continueLabel.gameObject.SetActive(true);textStyles.ApplyCleanButton(continueLabel,false);RefreshLoadoutActionText();
        }
        void RefreshLoadoutActionText()
        {
            foreach(var button in new[]{clearSlot})if(button){var skin=button.image.overrideSprite?button.image.overrideSprite:button.image.sprite;textStyles.ApplyCleanButton(button.GetComponentInChildren<TMP_Text>(true),skin==art.tankCostButtonSelected);}
        }
    }
}
