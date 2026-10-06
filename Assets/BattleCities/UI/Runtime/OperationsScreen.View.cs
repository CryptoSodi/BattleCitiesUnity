using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
        void BuildView()
        {
            header=Panel("Title plate",root,art.tankTitlePanel);header.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            titleIcon=Icon("Icon",header,null,new Rect(0,0,1,1));
            title=Label("Title",header,"QUARTERS",new Rect(0,0,1,1),28,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            tabBar=Panel("Navigation",header,art.tankCostButton);tabBar.GetComponent<Image>().color=new Color(.08f,.63f,.68f,1);
            body=Panel("Body",root,theme.CreamPanel);body.GetComponent<Image>().pixelsPerUnitMultiplier=2;
            intro=Panel("Intro",body,null);
            introTitle=Label("Title",intro,"COMMAND CENTER",new Rect(.02f,.05f,.96f,.45f),26,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            introDescription=Label("Description",intro,"ASSETS, OPERATIONS AND BATTLE INTELLIGENCE",new Rect(.02f,.51f,.96f,.40f),19,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);introDescription.color=new Color32(63,89,115,255);
            var line=Panel("Divider",intro,null);line.GetComponent<Image>().color=new Color32(52,82,102,120);Fit(line,new Rect(.016f,.975f,.968f,.02f));
            summary=Panel("Account summary",body,null);
            scroll=BuildScroll(body);
            empty=Panel("Message",body,null);
            Shadow(empty,new Rect(.32f,.53f,.36f,.065f));
            emptyIcon=Icon("Icon",empty,illustrations?illustrations.quarters[0]:null,new Rect(.29f,.07f,.42f,.48f));
            emptyTitle=Label("Title",empty,"",new Rect(.05f,.62f,.90f,.13f),32,ArcadeTextTreatment.PrizeAmount);
            emptyDescription=Label("Description",empty,"",new Rect(.10f,.76f,.80f,.16f),24,ArcadeTextTreatment.PrizeAmount);emptyDescription.textWrappingMode=TextWrappingModes.Normal;
            detail=Panel("Manual entry",body,null);
            var figure=Panel("Figure",detail,art.tankCardAvailable);figure.GetComponent<Image>().pixelsPerUnitMultiplier=2;Fit(figure,new Rect(.02f,.06f,.35f,.88f));
            Shadow(figure,new Rect(.16f,.64f,.68f,.08f));
            detailIcon=Icon("Artwork",figure,null,new Rect(.07f,.11f,.86f,.57f));
            detailTitle=Label("Entry title",figure,"",new Rect(.05f,.74f,.90f,.16f),30,ArcadeTextTreatment.PrizeAmount);
            detailRole=Label("Role",detail,"",new Rect(.41f,.08f,.55f,.12f),30,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            detailLore=Label("Description",detail,"",new Rect(.41f,.23f,.55f,.24f),27,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.TopLeft);detailLore.textWrappingMode=TextWrappingModes.Normal;
            var effect=Panel("Effect panel",detail,theme.Rounded);effect.GetComponent<Image>().pixelsPerUnitMultiplier=5;effect.GetComponent<Image>().color=new Color32(238,223,175,255);Fit(effect,new Rect(.40f,.52f,.57f,.27f));
            Label("Label",effect,"IN BATTLE",new Rect(.04f,.05f,.92f,.22f),20,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            detailEffect=Label("Effect",effect,"",new Rect(.04f,.30f,.92f,.64f),29,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);detailEffect.textWrappingMode=TextWrappingModes.Normal;
            detailSource=Label("Source",detail,"",new Rect(.41f,.84f,.55f,.10f),23,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            footer=Panel("Status",root,art.statusPanel);TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),art.statusPanel);
            status=Label("Status text",footer,statusMessage,new Rect(.025f,.12f,.70f,.76f),26,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            footerAction=Button("Action",footer,FooterAction);footerCaption=Label("Label",footerAction.transform,"REFRESH",new Rect(.05f,.04f,.90f,.92f),28,ArcadeTextTreatment.PrizeAmount);footerCaption.color=Color.white;
            BuildLinkInstructions();
        }

        public void ApplyLayout(MainMenuPlatform value)
        {
            if(!IsConfigured)return;platform=value;
            var head=new TvTitleHeaderLayout(root);var foot=new TvStatusFooterLayout(root);
            head.PlaceTitle(header,titleIcon.rectTransform,title.rectTransform);
            int tabCount=Mathf.Max(1,tabs.Count);
            float navWidth=Mathf.Min(header.rect.width*.69f,head.NavigationWidth*tabCount/4f);
            MainMenuScene.Place(tabBar,header.rect.width-navWidth-8,(head.Height-head.NavigationHeight)*.5f,navWidth,head.NavigationHeight);
            // Long page names use the remaining title area; all pages retain the shared bar height.
            float titleLeft=title.rectTransform.anchoredPosition.x;
            MainMenuScene.Place(title.rectTransform,titleLeft,head.Height*.06f,Mathf.Max(50,header.rect.width-navWidth-24-titleLeft),head.Height*.88f);
            TvTitleHeaderLayout.FitSkin(tabBar.GetComponent<Image>(),head.NavigationHeight);
            for(int i=0;i<tabs.Count;i++)
            {
                float width=navWidth/tabCount;MainMenuScene.Place(tabs[i].transform as RectTransform,i*width+3,3,width-6,head.NavigationHeight-6);
                TvTitleHeaderLayout.FitSkin(tabs[i].targetGraphic as Image,head.NavigationHeight-6);TvTitleHeaderLayout.FitSkin(tabSelections[i],head.NavigationHeight-6);
                var divider=tabBar.Find("Divider "+i);if(divider)MainMenuScene.Place(divider as RectTransform,i*width-1,head.NavigationHeight*.2f,2,head.NavigationHeight*.6f);
            }
            foot.Place(footer);TvStatusFooterLayout.PlaceAction(footerAction.transform as RectTransform);
            TvTitleHeaderLayout.FitSkin(footerAction.image,((RectTransform)footerAction.transform).rect.height);
            Fit(status.rectTransform,new Rect(.025f,.12f,footerAction.gameObject.activeSelf?.70f:.95f,.76f));
            float top=head.Height+12,height=root.rect.height-top-foot.Height-16;
            MainMenuScene.Place(body,4,top,root.rect.width-8,height);
            float introHeight=Mathf.Clamp(height*.105f,44,62),summaryHeight=page==Page.Treasury?Mathf.Clamp(height*.17f,66,88):0;
            MainMenuScene.Place(intro,6,7,body.rect.width-12,introHeight);
            MainMenuScene.Place(summary,12,introHeight+13,body.rect.width-24,summaryHeight);
            float scrollTop=introHeight+15+(summary.gameObject.activeSelf?summaryHeight+8:0);
            MainMenuScene.Place(scroll.transform as RectTransform,7,scrollTop,body.rect.width-14,height-scrollTop-9);
            MainMenuScene.Place(empty,18,scrollTop,body.rect.width-36,height-scrollTop-13);
            MainMenuScene.Place(detail,10,introHeight+13,body.rect.width-20,height-introHeight-23);
            columns=body.rect.width<510?2:3;
            grid.constraintCount=columns;
            float cardWidth=(scroll.viewport.rect.width-grid.padding.horizontal-grid.spacing.x*(columns-1))/columns;
            float rowHeight=Mathf.Max(120,(scroll.viewport.rect.height-grid.padding.vertical-grid.spacing.y)/2f);
            scroll.CardAspectRatio=Mathf.Max(1.13f,cardWidth/rowHeight);
            if(page==Page.Treasury)scroll.CardAspectRatio=1.28f;
            if(page==Page.Treasury&&history){grid.constraintCount=1;scroll.CardAspectRatio=Mathf.Max(1,(scroll.viewport.rect.width-grid.padding.horizontal)/58f);}
            for(int i=0;i<stats.Count;i++)MainMenuScene.Place(stats[i].Rect,i*summary.rect.width/4+3,0,summary.rect.width/4-6,summaryHeight);
            scroll.SetLayoutHorizontal();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);scroll.SetLayoutVertical();scroll.Rebuild(CanvasUpdate.PostLayout);
            LayoutLinkInstructions();
            ConfigureNavigation();
        }
        void BuildTabs()
        {
            tabs.Clear();tabSelections.Clear();tabCaptions.Clear();foreach(Transform child in tabBar)child.gameObject.SetActive(false);
            if(page==Page.Socials)AddTab("REFRESH",false,RefreshTasks).interactable=!loading;
            if(page==Page.Treasury){AddTab("HOLDINGS",!history,()=>SelectTreasuryHistory(false));AddTab("HISTORY",history,()=>SelectTreasuryHistory(true));}
            if(page==Page.Manual||page==Page.ManualDetail)
                for(int i=0;i<4;i++){int index=i;AddTab(CategoryLabels[i],category==i,()=>SelectManualCategory(index));}
            var back=AddTab("BACK",false,Back);Fit(tabCaptions[tabCaptions.Count-1].rectTransform,new Rect(.31f,.02f,.65f,.96f));
            var arrow=back.transform.Find("Back arrow") as RectTransform;
            if(!arrow){var go=new GameObject("Back arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=root.gameObject.layer;arrow=(RectTransform)go.transform;arrow.SetParent(back.transform,false);}
            arrow.gameObject.SetActive(true);Fit(arrow,new Rect(.10f,.24f,.16f,.52f));arrow.GetComponent<BackTabArrow>().raycastTarget=false;
        }
        Button AddTab(string caption,bool active,UnityEngine.Events.UnityAction click)
        {
            int i=tabs.Count;var button=Button("Tab "+i,tabBar,click);button.gameObject.SetActive(true);button.image.sprite=null;button.image.color=Color.clear;
            var oldArrow=button.transform.Find("Back arrow");if(oldArrow)oldArrow.gameObject.SetActive(false);
            var focus=Panel("Focus",button.transform,art.tankCostButton);Fit(focus,new Rect(0,0,1,1));focus.SetAsFirstSibling();
            var selection=Panel("Selection",button.transform,art.tankCostButtonSelected);Fit(selection,new Rect(0,0,1,1));selection.SetSiblingIndex(1);selection.GetComponent<Image>().enabled=active;
            button.image.overrideSprite=null;focus.GetComponent<Image>().overrideSprite=null;button.spriteState=default;
            button.targetGraphic=focus.GetComponent<Image>();button.transition=Selectable.Transition.ColorTint;
            var colors=ColorBlock.defaultColorBlock;colors.normalColor=colors.disabledColor=Color.clear;colors.selectedColor=colors.highlightedColor=colors.pressedColor=Color.white;colors.fadeDuration=.08f;button.colors=colors;
            var label=Label("Label",button.transform,caption,new Rect(.04f,.02f,.92f,.96f),28,ArcadeTextTreatment.PrizeAmount);styles.ApplyCleanButton(label,active);label.transform.SetAsLastSibling();
            if(i>0){var divider=Panel("Divider "+i,tabBar,null);divider.gameObject.SetActive(true);divider.GetComponent<Image>().color=new Color32(0,44,72,240);}
            tabs.Add(button);tabSelections.Add(selection.GetComponent<Image>());tabCaptions.Add(label);return button;
        }
        void BuildCards()
        {
            foreach(Transform child in scroll.content)child.gameObject.SetActive(false);
            ledgerRows.Clear();
            for(int i=0;i<items.Count;i++)
            {
                int index=i;var item=items[i];Card card;
                if(i>=cards.Count)
                {
                    var rect=Panel("Card "+i,scroll.content,art.tankCardAvailable);var button=rect.GetComponent<Button>();if(!button)button=rect.gameObject.AddComponent<Button>();
                    button.targetGraphic=rect.GetComponent<Image>();button.transition=Selectable.Transition.None;button.image.raycastTarget=true;
                    var ground=Shadow(rect,new Rect(.25f,.625f,.50f,.055f));
                    card=new Card{Rect=rect,Button=button,Frame=button.image};
                    card.Title=Label("Title",rect,"",new Rect(.12f,.045f,.76f,.17f),34,ArcadeTextTreatment.PrizeAmount);
                    card.Icon=Icon("Artwork",rect,null,new Rect(.12f,.22f,.76f,.43f));
                    card.Description=Label("Detail",rect,"",new Rect(.055f,.66f,.89f,.12f),24,ArcadeTextTreatment.PrizeAmount);
                    var action=Panel("Action",rect,art.tankCostButton);Fit(action,new Rect(.07f,.79f,.86f,.165f));card.Action=action.GetComponent<Image>();
                    card.Caption=Label("Caption",action,"",new Rect(.04f,.05f,.92f,.90f),30,ArcadeTextTreatment.PrizeAmount);
                    card.Highlight=CardSelectionHighlight.Ensure(card.Frame);
                    var handler=rect.GetComponent<OperationsCardFocus>();if(!handler)handler=rect.gameObject.AddComponent<OperationsCardFocus>();handler.Configure(this,index);
                    cards.Add(card);
                }
                else card=cards[i];
                card.Rect.gameObject.SetActive(true);card.Rect.SetAsLastSibling();card.Icon.sprite=item.Icon;card.Icon.enabled=item.Icon;card.Icon.color=Color.white;
                card.Title.text=item.Title;card.Description.text=item.Detail;card.Caption.text=item.Action;
                card.Button.onClick.RemoveAllListeners();card.Button.onClick.AddListener(()=>Activate(index));
                card.Button.interactable=true;
            }
            RefreshCards();
        }
        void RefreshCards()
        {
            for(int i=0;i<items.Count&&i<cards.Count;i++)
            {
                var card=cards[i];var item=items[i];bool active=(item.Completed||item.Key==activeCard)&&!item.Locked;
                card.Frame.sprite=item.Locked?art.tankCardUnavailable:active?art.tankCardSelected:art.tankCardAvailable;
                card.Frame.type=Image.Type.Sliced;card.Frame.pixelsPerUnitMultiplier=4;
                card.Action.sprite=item.Locked?art.tankCostButtonLocked:active?art.tankCostButtonSelected:art.tankCostButton;
                TvTitleHeaderLayout.FitSkin(card.Action,Mathf.Max(32,card.Action.rectTransform.rect.height));
                styles.ApplyCleanButton(card.Caption,active);card.Highlight=CardSelectionHighlight.Ensure(card.Frame,item.Locked?new Color(.65f,.80f,.87f):Color.white);card.Highlight.SetState(active,focused==i||hovered==i);
            }
        }
        void ConfigureNavigation()
        {
            if(tabs.Count==0)return;
            var controls=new System.Collections.Generic.List<Button>();
            if(page==Page.Treasury&&history)controls.AddRange(ledgerRows);else for(int i=0;i<items.Count&&i<cards.Count;i++)controls.Add(cards[i].Button);
            Button action=footerAction.gameObject.activeSelf?footerAction:tabs[tabs.Count-1];
            for(int i=0;i<tabs.Count;i++)Link(tabs[i],tabs[(i+tabs.Count-1)%tabs.Count],tabs[(i+1)%tabs.Count],action,controls.Count>0?controls[Math.Min(i,controls.Count-1)]:action);
            int rowWidth=page==Page.Treasury&&history?1:columns;
            for(int i=0;i<controls.Count;i++)Link(controls[i],i%rowWidth>0?controls[i-1]:tabs[0],i%rowWidth<rowWidth-1&&i+1<controls.Count?controls[i+1]:tabs[tabs.Count-1],
                i>=rowWidth?controls[i-rowWidth]:tabs[Math.Min(i,tabs.Count-1)],i+rowWidth<controls.Count?controls[i+rowWidth]:i/rowWidth<(controls.Count-1)/rowWidth?controls[controls.Count-1]:action);
            if(footerAction.gameObject.activeSelf)Link(footerAction,tabs[0],tabs[tabs.Count-1],controls.Count>0?controls[controls.Count-1]:tabs[0],tabs[0]);
        }
        static void Link(Selectable b,Selectable left,Selectable right,Selectable up,Selectable down){b.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=left,selectOnRight=right,selectOnUp=up,selectOnDown=down};}
        TankRosterScroll BuildScroll(Transform parent)
        {
            var area=Panel("Scroll",parent,null);area.GetComponent<Image>().raycastTarget=true;
            var viewport=Panel("Viewport",area,theme.Rounded);Fit(viewport,new Rect(0,0,1,1));viewport.offsetMin=new Vector2(3,3);viewport.offsetMax=new Vector2(-24,-3);viewport.GetComponent<Image>().raycastTarget=true;
            var mask=viewport.GetComponent<Mask>();if(!mask)mask=viewport.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var content=Panel("Content",viewport,null);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            grid=content.GetComponent<GridLayoutGroup>();if(!grid)grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.spacing=Vector2.one*10;grid.padding=new RectOffset(5,5,5,5);
            var fit=content.GetComponent<ContentSizeFitter>();if(!fit)fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var track=Panel("Scrollbar",area,theme.DarkPanel);track.GetComponent<Image>().pixelsPerUnitMultiplier=3;track.GetComponent<Image>().raycastTarget=true;track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.pivot=new Vector2(1,.5f);track.anchoredPosition=new Vector2(-2,0);track.sizeDelta=new Vector2(16,-6);
            var slide=Panel("Sliding area",track,null);Fit(slide,new Rect(0,0,1,1));slide.offsetMin=Vector2.one*2;slide.offsetMax=Vector2.one*-2;
            var handle=Panel("Handle",slide,theme.GoldPanel);Fit(handle,new Rect(0,0,1,1));handle.GetComponent<Image>().pixelsPerUnitMultiplier=3;handle.GetComponent<Image>().raycastTarget=true;
            var bar=track.GetComponent<Scrollbar>();if(!bar)bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;bar.navigation=new Navigation{mode=Navigation.Mode.None};
            var view=area.GetComponent<TankRosterScroll>();if(!view)view=area.gameObject.AddComponent<TankRosterScroll>();view.content=content;view.viewport=viewport;view.horizontal=false;view.vertical=true;view.scrollSensitivity=35;view.inertia=true;view.movementType=ScrollRect.MovementType.Clamped;view.verticalScrollbar=bar;view.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;return view;
        }
        Button Button(string name,Transform parent,UnityEngine.Events.UnityAction click)
        {
            var rect=Panel(name,parent,art.tankCostButton);var b=rect.GetComponent<Button>();if(!b)b=rect.gameObject.AddComponent<Button>();
            b.targetGraphic=rect.GetComponent<Image>();b.image.raycastTarget=true;b.transition=Selectable.Transition.SpriteSwap;
            b.spriteState=new SpriteState{highlightedSprite=art.tankCostButtonSelected,selectedSprite=art.tankCostButtonSelected,pressedSprite=art.tankCostButtonSelected};b.onClick.RemoveAllListeners();b.onClick.AddListener(click);return b;
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var rect=parent.Find(name) as RectTransform;
            if(!rect){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            var image=rect.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        Image Icon(string name,Transform parent,Sprite sprite,Rect bounds){var rect=Panel(name,parent,sprite);Fit(rect,bounds);var image=rect.GetComponent<Image>();image.type=Image.Type.Simple;image.preserveAspect=true;return image;}
        TMP_Text Label(string name,Transform parent,string caption,Rect bounds,float size,ArcadeTextTreatment treatment,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var old=parent.Find(name);TMP_Text text;
            if(old)text=old.GetComponent<TMP_Text>();else{var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);text=go.GetComponent<TMP_Text>();}
            Fit(text.rectTransform,bounds);text.font=ArcadeTextStyles.HeadingSdf;text.text=caption;text.richText=false;text.fontSize=text.fontSizeMax=size;text.fontSizeMin=12;text.enableAutoSizing=true;text.alignment=align;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;styles.Apply(text,treatment);return text;
        }
        RectTransform Shadow(Transform parent,Rect bounds)
        {
            var rect=parent.Find("Ground shadow") as RectTransform;if(!rect){var go=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            Fit(rect,bounds);var shadow=rect.GetComponent<TankGroundShadow>();shadow.color=new Color32(35,25,13,100);shadow.raycastTarget=false;return rect;
        }
        static void Fit(RectTransform rect,Rect bounds){rect.anchorMin=new Vector2(bounds.x,1-bounds.y-bounds.height);rect.anchorMax=new Vector2(bounds.x+bounds.width,1-bounds.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
