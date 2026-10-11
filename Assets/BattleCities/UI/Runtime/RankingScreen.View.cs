using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class RankingScreen
    {
        void BuildView()
        {
            header=Panel("Title plate",root,art.tankTitlePanel);header.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=4;
            Icon("Ranking icon",header,theme.NavigationIcons[3],new Rect(.10f,.12f,.25f,.76f));
            Label("Title",header,"RANKING",new Rect(.36f,.06f,.57f,.88f),28,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            tabsBar=Panel("Tabs",header,art.tankCostButton);tabsBar.GetComponent<UnityEngine.UI.Image>().color=new Color(.08f,.63f,.68f,1);
            tabs[0]=Tab("GAMING",tabsBar,()=>SelectScope(false),out selectedTabs[0],out tabLabels[0]);
            tabs[1]=Tab("TRADING",tabsBar,()=>SelectScope(true),out selectedTabs[1],out tabLabels[1]);
            Icon("Icon",tabs[0].transform,theme.RankingGamingIcon,new Rect(.055f,.08f,.23f,.84f));
            Icon("Icon",tabs[1].transform,theme.RankingTradingIcon,new Rect(.055f,.08f,.23f,.84f));
            for(int i=0;i<2;i++)Fit(tabLabels[i].rectTransform,new Rect(.30f,.02f,.66f,.96f));
            LockedTabIcon.Apply(tabs[1],art.tankCostButtonLocked);
            tabs[2]=back=Tab("BACK",tabsBar,()=>Close(),out selectedTabs[2],out tabLabels[2]);selectedTabs[2].enabled=false;
            Fit(tabLabels[2].rectTransform,new Rect(.28f,.02f,.68f,.96f));Arrow("Arrow",back.transform,new Rect(.10f,.17f,.14f,.66f),0);
            for(int i=1;i<3;i++){var line=Panel("Divider "+i,tabsBar,null);line.GetComponent<UnityEngine.UI.Image>().color=new Color32(2,27,64,255);}

            history=Panel("Payout history",root,theme.BlueFrame);historyPaper=Panel("Paper",history,theme.Rounded);
            historyHeading=Panel("Heading bar",historyPaper,theme.Rounded);historyHeading.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=5;
            Icon("Chest",historyHeading,theme.PrizeCrates.Length>0?theme.PrizeCrates[0]:theme.TrophyIcon,new Rect(.035f,.10f,.18f,.80f));
            Label("Heading",historyHeading,"PAYOUTS",new Rect(.25f,.02f,.72f,.96f),24,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            historyPeriod=Label("Period",historyPaper,"CURRENT CYCLE",new Rect(.05f,.10f,.90f,.08f),24,ArcadeTextTreatment.PrizeAmount);
            historyEmpty=Panel("Empty history",historyPaper,null);
            ContactShadow("Chest shadow",historyEmpty,new Rect(.29f,.36f,.42f,.045f));
            Icon("Chest",historyEmpty,theme.PrizeCrates.Length>0?theme.PrizeCrates[0]:theme.TrophyIcon,new Rect(.27f,.04f,.46f,.35f));
            Label("Title",historyEmpty,"PREVIOUS PAYOUTS",new Rect(.04f,.45f,.92f,.18f),26,ArcadeTextTreatment.PrizeAmount);
            var historyCopy=Label("Description",historyEmpty,"No payout records\navailable yet.",new Rect(.04f,.64f,.92f,.25f),24,ArcadeTextTreatment.PrizeAmount);
            historyCopy.textWrappingMode=TextWrappingModes.Normal;
            eligibility=Panel("Eligibility",historyPaper,null);
            var lineTop=Panel("Divider",eligibility,null);lineTop.GetComponent<UnityEngine.UI.Image>().color=new Color32(6,29,54,65);Fit(lineTop,new Rect(0,0,1,.015f));
            eligibilityText=Label("Label",eligibility,"",new Rect(.02f,.10f,.96f,.80f),22,ArcadeTextTreatment.PrizeAmount);

            table=Panel("Standings",root,theme.CreamPanel);table.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=2;
            tableHeading=Panel("Heading",table,null);
            tableTitle=Label("Period title",tableHeading,"LIVE PLAYER RANKINGS",new Rect(.02f,.05f,.96f,.45f),26,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            string[] columns={"#","PLAYER","SCORE","MATCHES"};
            var positions=new[]{new Rect(.025f,.51f,.07f,.40f),new Rect(.12f,.51f,.43f,.40f),new Rect(.56f,.51f,.21f,.40f),new Rect(.79f,.51f,.18f,.40f)};
            for(int i=0;i<4;i++){var label=Label(columns[i],tableHeading,columns[i],positions[i],19,ArcadeTextTreatment.PrizeAmount,i==1?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center);label.color=new Color32(63,89,115,255);}
            var tableDivider=Panel("Divider",tableHeading,null);tableDivider.GetComponent<UnityEngine.UI.Image>().color=new Color32(52,82,102,120);Fit(tableDivider,new Rect(.016f,.975f,.968f,.02f));
            tableScroll=BuildScroll("Rows",table);
            empty=Panel("Empty standings",table,null);
            var trophy=Icon("Trophy",empty,theme.TrophyIcon,new Rect(.33f,.12f,.34f,.43f));
            var shadowRect=Panel("Ground shadow",empty,null);Fit(shadowRect,new Rect(.37f,.515f,.26f,.045f));shadowRect.SetSiblingIndex(0);
            var oval=shadowRect.Find("Oval") as RectTransform;
            if(!oval){var go=new GameObject("Oval",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=root.gameObject.layer;oval=(RectTransform)go.transform;oval.SetParent(shadowRect,false);}
            Fit(oval,new Rect(0,0,1,1));var shadow=oval.GetComponent<TankGroundShadow>();
            shadow.color=new Color32(35,25,13,112);shadow.raycastTarget=false;
            emptyTitle=Label("Title",empty,"",new Rect(.04f,.62f,.92f,.14f),30,ArcadeTextTreatment.PrizeAmount);
            emptyDetail=Label("Description",empty,"",new Rect(.07f,.78f,.86f,.12f),24,ArcadeTextTreatment.PrizeAmount);
            emptyDetail.textWrappingMode=TextWrappingModes.Normal;

            footer=Panel("Status",root,art.statusPanel);TvStatusFooterLayout.ApplySkin(footer.GetComponent<UnityEngine.UI.Image>(),art.statusPanel);
            Label("Rank label",footer,"YOUR RANK",new Rect(.025f,.08f,.15f,.36f),20,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            rankValue=Label("Rank value",footer,"—",new Rect(.025f,.43f,.15f,.49f),29,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            var oldRule=footer.Find("Reward rule");if(oldRule)oldRule.gameObject.SetActive(false);
            countdown=Label("Countdown",footer,"TOP 10 EVERY 30 MINUTES • —",new Rect(.18f,.10f,.56f,.80f),25,ArcadeTextTreatment.PrizeAmount);
            picker=Action("Period picker",footer,TogglePicker);TvStatusFooterLayout.PlaceAction(picker.transform as RectTransform);
            periodCaption=Label("Label",picker.transform,"CURRENT CYCLE",new Rect(.05f,.06f,.78f,.88f),26,ArcadeTextTreatment.PrizeAmount);periodCaption.color=Color.white;
            Arrow("Up arrow",picker.transform,new Rect(.86f,.25f,.08f,.50f),-90);
            pickerOverlay=Panel("Period overlay",root,null);Fit(pickerOverlay,new Rect(0,0,1,1));
            var dismiss=Action("Dismiss",pickerOverlay,()=>ClosePicker());Fit(dismiss.transform as RectTransform,new Rect(0,0,1,1));dismiss.image.sprite=null;dismiss.image.color=new Color(0,.025f,.08f,.16f);dismiss.transition=Selectable.Transition.None;dismiss.navigation=new Navigation{mode=Navigation.Mode.None};
            pickerPanel=Panel("Picker panel",pickerOverlay,art.tankCostButton);
            var pickerSkin=pickerPanel.GetComponent<UnityEngine.UI.Image>();pickerSkin.color=tabsBar.GetComponent<UnityEngine.UI.Image>().color;pickerSkin.raycastTarget=true;
            TvTitleHeaderLayout.FitSkin(pickerSkin,46);
            periodScroll=BuildScroll("Options",pickerPanel);Fit(periodScroll.transform as RectTransform,new Rect(0,0,1,1));periodScroll.viewport.offsetMin=new Vector2(8,8);periodScroll.viewport.offsetMax=new Vector2(-28,-8);
            pickerOverlay.gameObject.SetActive(false);
        }

        public void ApplyLayout(MainMenuPlatform platform)
        {
            if(!IsConfigured)return;
            var titleLayout=new TvTitleHeaderLayout(root);var statusLayout=new TvStatusFooterLayout(root);
            titleLayout.PlaceTitle(header,header.Find("Ranking icon") as RectTransform,header.Find("Title") as RectTransform);
            titleLayout.PlaceNavigation(header,tabsBar);
            MainMenuScene.Place(tabsBar,header.rect.width-titleLayout.NavigationWidth*.75f-8,
                (titleLayout.Height-titleLayout.NavigationHeight)*.5f,titleLayout.NavigationWidth*.75f,titleLayout.NavigationHeight);
            TvTitleHeaderLayout.FitSkin(tabsBar.GetComponent<UnityEngine.UI.Image>(),titleLayout.NavigationHeight);
            for(int i=0;i<3;i++)
            {
                MainMenuScene.Place(tabs[i].transform as RectTransform,i*tabsBar.rect.width/3+3,3,tabsBar.rect.width/3-6,titleLayout.NavigationHeight-6);
                TvTitleHeaderLayout.FitSkin(tabs[i].targetGraphic as UnityEngine.UI.Image,titleLayout.NavigationHeight-6);
                TvTitleHeaderLayout.FitSkin(selectedTabs[i],titleLayout.NavigationHeight-6);
                if(!tabs[i].interactable)TvTitleHeaderLayout.FitSkin(tabs[i].GetComponent<UnityEngine.UI.Image>(),titleLayout.NavigationHeight-6);
            }
            for(int i=1;i<3;i++)MainMenuScene.Place(tabsBar.Find("Divider "+i) as RectTransform,i*tabsBar.rect.width/3-1,titleLayout.NavigationHeight*.20f,2,titleLayout.NavigationHeight*.60f);
            statusLayout.Place(footer);TvStatusFooterLayout.PlaceAction(picker.transform as RectTransform);
            TvTitleHeaderLayout.FitSkin(picker.image,((RectTransform)picker.transform).rect.height);
            float side=Mathf.Clamp(root.rect.width*.38f,320,460),top=titleLayout.Height+12,height=root.rect.height-top-statusLayout.Height-16;
            MainMenuScene.Place(history,4,top,side,height);MainMenuScene.Place(table,side+12,top,root.rect.width-side-16,height);
            menu.ApplyDetailSurface(history,historyPaper,ref historyMaterial);
            float w=historyPaper.rect.width,h=historyPaper.rect.height;
            MainMenuScene.Place(historyHeading,3,3,w-6,TvSidebarStyle.HeaderHeight);MainMenuScene.Place(historyPeriod.rectTransform,10,52,w-20,32);
            MainMenuScene.Place(historyEmpty,8,Mathf.Max(96,h*.23f),w-16,Mathf.Min(260,h*.49f));
            MainMenuScene.Place(eligibility,10,h-86,w-20,76);
            bool largerSidebarText=TvSidebarStyle.LargerText(platform);
            TvSidebarStyle.Apply(historyHeading.Find("Heading").GetComponent<TMP_Text>(),TvSidebarStyle.HeadingSize(largerSidebarText),textStyles);
            foreach(var label in new[]{historyPeriod,historyEmpty.Find("Title").GetComponent<TMP_Text>(),historyEmpty.Find("Description").GetComponent<TMP_Text>(),eligibilityText})
                TvSidebarStyle.Apply(label,TvSidebarStyle.BodySize(largerSidebarText),textStyles);
            TvSidebarStyle.PlaceHeaderIcon(historyHeading.Find("Chest").GetComponent<UnityEngine.UI.Image>(),new Rect(0,0,1,1));
            float tableHeaderHeight=Mathf.Clamp(height*.105f,44,62),tableRowsTop=tableHeaderHeight+15;
            MainMenuScene.Place(tableHeading,6,7,table.rect.width-12,tableHeaderHeight);
            MainMenuScene.Place(tableScroll.transform as RectTransform,6,tableRowsTop,table.rect.width-12,height-tableRowsTop-8);
            MainMenuScene.Place(empty,14,tableRowsTop,table.rect.width-40,height-tableRowsTop-10);
            foreach(var button in rowButtons)button.GetComponent<LayoutElement>().preferredHeight=58;
            LayoutPicker();
        }
        void LayoutPicker()
        {
            if(!pickerPanel||!footer)return;
            float width=Mathf.Min(root.rect.width-24,Mathf.Max(300,footer.rect.width*.36f));
            float available=root.rect.height-new TvTitleHeaderLayout(root).Height-new TvStatusFooterLayout(root).Height-34;
            float contentHeight=options.Count*50+(trading?1:2)*30+24;
            float height=Mathf.Min(available,Mathf.Max(130,contentHeight));
            float footerTop=root.rect.height-new TvStatusFooterLayout(root).Height-4;
            MainMenuScene.Place(pickerPanel,root.rect.width-width-8,footerTop-height-6,width,height);
        }
        void BuildRankingRows()
        {
            rowButtons.Clear();
            int count=snapshot?.Rows.Count??0;
            foreach(Transform child in tableScroll.content)child.gameObject.SetActive(false);
            for(int i=0;i<count;i++)
            {
                var row=snapshot.Rows[i];var rect=Panel("Row "+i,tableScroll.content,theme.Rounded);rect.gameObject.SetActive(true);
                rect.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=5;
                rect.GetComponent<UnityEngine.UI.Image>().color=i%2==0?new Color32(245,233,205,255):new Color32(255,247,226,255);
                var button=rect.GetComponent<UnityEngine.UI.Button>();if(!button)button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();button.image.raycastTarget=true;button.onClick.RemoveAllListeners();
                string playerId=row.playerId;if(ProfileLinks.ValidPlayerId(playerId))button.onClick.AddListener(()=>menu.OpenPlayerProfile(playerId));
                var colors=ColorBlock.defaultColorBlock;colors.highlightedColor=colors.selectedColor=new Color(.65f,.85f,1f);colors.pressedColor=new Color(.55f,.8f,1f);button.colors=colors;
                var fit=rect.GetComponent<LayoutElement>();if(!fit)fit=rect.gameObject.AddComponent<LayoutElement>();fit.preferredHeight=58;
                Label("Rank",rect,row.rank.ToString(),new Rect(.025f,.10f,.08f,.80f),28,row.rank<=3?ArcadeTextTreatment.Gold:ArcadeTextTreatment.PrizeAmount);
                var name=Label("Player",rect,row.displayName,new Rect(.12f,.10f,.43f,.80f),27,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);name.overflowMode=TextOverflowModes.Ellipsis;
                Label("Score",rect,row.totalPoints.ToString("N0"),new Rect(.56f,.10f,.21f,.80f),27,ArcadeTextTreatment.PrizeAmount);
                Label("Matches",rect,row.matches.ToString("N0"),new Rect(.79f,.10f,.18f,.80f),27,ArcadeTextTreatment.PrizeAmount);
                var focus=rect.GetComponent<RankingRowFocus>();if(!focus)focus=rect.gameObject.AddComponent<RankingRowFocus>();focus.Configure(tableScroll);
                rowButtons.Add(button);
            }
            LayoutRebuilder.MarkLayoutForRebuild(tableScroll.content);
        }
        void BuildPeriodRows()
        {
            if(!periodScroll)return;
            periodButtons.Clear();foreach(Transform child in periodScroll.content)child.gameObject.SetActive(false);
            bool cycleHeader=false,seasonHeader=false;
            foreach(var period in options)
            {
                if(period.IsCycle&&!cycleHeader){Group("Cycles","30-MINUTE CYCLE");cycleHeader=true;}
                if(period.IsAllTime)Group("All time","ALL-TIME STANDINGS");
                if(!period.IsCycle&&!period.IsAllTime&&!seasonHeader){Group("Seasons","MONTHLY SEASONS");seasonHeader=true;}
                string id=period.Id;
                var button=Tab("Period "+id,periodScroll.content,()=>SelectPeriod(id),out var selection,out var label);
                var normal=button.GetComponent<UnityEngine.UI.Image>();normal.sprite=art.tankCostButton;normal.color=tabsBar.GetComponent<UnityEngine.UI.Image>().color;
                button.gameObject.SetActive(true);button.transform.SetAsLastSibling();label.text=period.Label;selection.enabled=id==selectedPeriod.Id;
                textStyles.ApplyCleanButton(label,selection.enabled);
                var element=button.GetComponent<LayoutElement>();if(!element)element=button.gameObject.AddComponent<LayoutElement>();element.preferredHeight=46;
                var focus=button.GetComponent<RankingRowFocus>();if(!focus)focus=button.gameObject.AddComponent<RankingRowFocus>();focus.Configure(periodScroll);
                TvTitleHeaderLayout.FitSkin(normal,46);TvTitleHeaderLayout.FitSkin(selection,46);TvTitleHeaderLayout.FitSkin(button.targetGraphic as UnityEngine.UI.Image,46);
                periodButtons.Add(button);
            }
            if(seasons.Count==0)Group("Season status",loading?"LOADING SEASONS…":"SEASONS UNAVAILABLE");
            LayoutRebuilder.MarkLayoutForRebuild(periodScroll.content);LayoutPicker();
        }
        void Group(string name,string caption)
        {
            var rect=Panel(name,periodScroll.content,null);rect.gameObject.SetActive(true);rect.SetAsLastSibling();
            var fit=rect.GetComponent<LayoutElement>();if(!fit)fit=rect.gameObject.AddComponent<LayoutElement>();fit.preferredHeight=28;
            Label("Label",rect,caption,new Rect(.03f,0,.94f,1),20,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft).color=Color.white;
        }

        TankRosterScroll BuildScroll(string name,Transform parent)
        {
            var area=Panel(name,parent,null);area.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var viewport=Panel("Viewport",area,theme.Rounded);viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;Fit(viewport,new Rect(0,0,1,1));viewport.offsetMin=new Vector2(4,4);viewport.offsetMax=new Vector2(-24,-4);
            var mask=viewport.GetComponent<Mask>();if(!mask)mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var content=Panel("Content",viewport,null);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>();if(!layout)layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.spacing=4;layout.padding=new RectOffset(3,3,3,3);
            var fit=content.GetComponent<ContentSizeFitter>();if(!fit)fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var track=Panel("Scrollbar",area,theme.DarkPanel);track.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=3;track.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.pivot=new Vector2(1,.5f);track.anchoredPosition=new Vector2(-3,0);track.sizeDelta=new Vector2(16,-8);
            var sliding=Panel("Sliding area",track,null);Fit(sliding,new Rect(0,0,1,1));sliding.offsetMin=Vector2.one*2;sliding.offsetMax=Vector2.one*-2;
            var thumb=Panel("Handle",sliding,theme.GoldPanel);Fit(thumb,new Rect(0,0,1,1));thumb.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=3;thumb.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var bar=track.GetComponent<Scrollbar>();if(!bar)bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<UnityEngine.UI.Image>();bar.direction=Scrollbar.Direction.BottomToTop;bar.navigation=new Navigation{mode=Navigation.Mode.None};
            var scroll=area.GetComponent<TankRosterScroll>();if(!scroll)scroll=area.gameObject.AddComponent<TankRosterScroll>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;scroll.inertia=true;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            return scroll;
        }
        UnityEngine.UI.Button Tab(string name,Transform parent,UnityEngine.Events.UnityAction action,out UnityEngine.UI.Image selected,out TMP_Text text)
        {
            var button=Action(name,parent,action);button.image.sprite=null;button.image.color=Color.clear;
            var focus=Panel("Focus",button.transform,art.tankCostButton);Fit(focus,new Rect(0,0,1,1));focus.SetAsFirstSibling();
            var selection=Panel("Selection",button.transform,art.tankCostButtonSelected);Fit(selection,new Rect(0,0,1,1));selection.SetSiblingIndex(1);selected=selection.GetComponent<UnityEngine.UI.Image>();
            button.targetGraphic=focus.GetComponent<UnityEngine.UI.Image>();button.transition=Selectable.Transition.ColorTint;
            var colors=ColorBlock.defaultColorBlock;colors.normalColor=colors.disabledColor=Color.clear;colors.highlightedColor=colors.selectedColor=colors.pressedColor=Color.white;colors.fadeDuration=.08f;button.colors=colors;
            text=Label("Label",button.transform,name,new Rect(.04f,.02f,.92f,.96f),28,ArcadeTextTreatment.PrizeAmount);textStyles.ApplyCleanButton(text,false);text.transform.SetAsLastSibling();return button;
        }
        UnityEngine.UI.Button Action(string name,Transform parent,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(name,parent,art.tankCostButton);var button=rect.GetComponent<UnityEngine.UI.Button>();if(!button)button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();button.image.raycastTarget=true;button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=art.tankCostButtonSelected,selectedSprite=art.tankCostButtonSelected,pressedSprite=art.tankCostButtonSelected};
            button.onClick.RemoveAllListeners();button.onClick.AddListener(action);return button;
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var rect=parent.Find(name) as RectTransform;if(!rect){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            var image=rect.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        UnityEngine.UI.Image Icon(string name,Transform parent,Sprite sprite,Rect bounds)
        {var rect=Panel(name,parent,sprite);Fit(rect,bounds);var image=rect.GetComponent<UnityEngine.UI.Image>();image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;return image;}
        TMP_Text Label(string name,Transform parent,string caption,Rect bounds,float size,ArcadeTextTreatment treatment,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var child=parent.Find(name);TMP_Text text;
            if(child)text=child.GetComponent<TMP_Text>();else{var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);text=go.GetComponent<TMP_Text>();}
            Fit(text.rectTransform,bounds);text.font=ArcadeTextStyles.HeadingSdf;text.text=caption;text.richText=false;text.fontSize=text.fontSizeMax=size;text.fontSizeMin=12;text.enableAutoSizing=true;text.alignment=align;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;textStyles.Apply(text,treatment);return text;
        }
        void Arrow(string name,Transform parent,Rect bounds,float rotation)
        {
            var rect=parent.Find(name) as RectTransform;if(!rect){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            Fit(rect,bounds);rect.localRotation=Quaternion.Euler(0,0,rotation);var arrow=rect.GetComponent<BackTabArrow>();arrow.color=Color.white;arrow.raycastTarget=false;
        }
        void ContactShadow(string name,Transform parent,Rect bounds)
        {
            var rect=parent.Find(name) as RectTransform;
            if(!rect){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            Fit(rect,bounds);rect.SetAsFirstSibling();var shadow=rect.GetComponent<TankGroundShadow>();shadow.color=new Color32(35,25,13,112);shadow.raycastTarget=false;
        }
        static void Fit(RectTransform rect,Rect bounds){rect.anchorMin=new Vector2(bounds.x,1-bounds.y-bounds.height);rect.anchorMax=new Vector2(bounds.x+bounds.width,1-bounds.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
