using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class PlayerProfileScreen
    {
        static readonly string[] BattleColumns={"RESULT","MODE","STAGE","SCORE","POINTS"};
        static readonly float[] BattleColumnLeft={.018f,.28f,.425f,.51f,.675f};
        static readonly float[] BattleColumnWidth={.25f,.13f,.07f,.16f,.13f};
        void BuildView()
        {
            header=Panel("Title plate",root,art.tankTitlePanel);header.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            titleIcon=Icon("Icon",header,pictures?pictures.insignia:null,new Rect(0,0,1,1));
            title=Label("Title",header,"PLAYER PROFILE",new Rect(0,0,1,1),28,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            nav=Panel("Navigation",header,art.tankCostButton);nav.GetComponent<Image>().color=new Color(.08f,.63f,.68f);
            back=Control("Back",nav,"BACK",()=>menu.ClosePlayerProfile());
            var backBackground=back.GetComponent<Image>();backBackground.sprite=null;backBackground.color=Color.clear;
            var backFocus=back.transform.Find("Focus").GetComponent<Image>();backFocus.enabled=true;
            back.GetComponent<SettingsControlVisual>().enabled=false;back.targetGraphic=backFocus;back.transition=Selectable.Transition.ColorTint;
            var backColors=ColorBlock.defaultColorBlock;backColors.normalColor=backColors.disabledColor=Color.clear;
            backColors.highlightedColor=backColors.selectedColor=backColors.pressedColor=Color.white;backColors.fadeDuration=.08f;back.colors=backColors;
            var backCaption=back.transform.Find("Caption").GetComponent<TMP_Text>();Fit(backCaption.rectTransform,new Rect(.29f,.025f,.68f,.95f));styles.ApplyCleanButton(backCaption,false);
            var arrow=back.transform.Find("Arrow") as RectTransform;
            if(!arrow){var go=new GameObject("Arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=root.gameObject.layer;arrow=(RectTransform)go.transform;arrow.SetParent(back.transform,false);}
            Fit(arrow,new Rect(.10f,.17f,.14f,.66f));arrow.GetComponent<BackTabArrow>().raycastTarget=false;
            hero=Panel("Commander",root,theme.BlueFrame);
            heroPaper=Panel("Paper",hero,theme.Rounded);
            foreach(var childName in new[]{"Portrait","Eyebrow","Name","Identity","Player ID","Share"})
            {var old=hero.Find(childName);if(old)old.SetParent(heroPaper,false);}
            foreach(var childName in new[]{"Eyebrow","Player ID","Details","Identity divider"})
            {var old=heroPaper.Find(childName);if(old)old.gameObject.SetActive(false);}
            avatar=Icon("Portrait",heroPaper,pictures?pictures.commander:null,new Rect(0,0,1,1));
            nameLabel=Label("Name",heroPaper,"—",new Rect(0,0,1,1),40,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);nameLabel.overflowMode=TextOverflowModes.Ellipsis;
            identityLabel=Label("Identity",heroPaper,"—",new Rect(0,0,1,1),22,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);identityLabel.overflowMode=TextOverflowModes.Ellipsis;
            share=Control("Share",heroPaper,"SHARE PROFILE",Share);
            Icon("Icon",share.transform,pictures?pictures.share:null,new Rect(.06f,.22f,.17f,.56f));Fit(share.transform.Find("Caption") as RectTransform,new Rect(.25f,.08f,.72f,.84f));
            footer=Panel("Status",root,art.statusPanel);TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),art.statusPanel);
            var oldStatus=footer.Find("Label");if(oldStatus)oldStatus.gameObject.SetActive(false);
            var oldMetrics=root.Find("Statistics");if(oldMetrics)oldMetrics.SetParent(footer,false);
            metrics=Panel("Statistics",footer,null);
            string[] captions={"SEASON RANK","POINTS","MATCHES","BEST SCORE"};
            Sprite[] icons={pictures?pictures.insignia:null,theme.ScoreIcon,pictures?pictures.matches:null,theme.TrophyIcon};
            for(int i=0;i<4;i++)
            {
                var r=stats[i]=Panel("Stat "+i,metrics,null);
                Shadow(r,new Rect(.035f,.86f,.23f,.035f));
                Icon("Icon",r,icons[i],new Rect(.025f,.05f,.25f,.90f));
                statNames[i]=Label("Name",r,captions[i],new Rect(.31f,.06f,.40f,.88f),24,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
                values[i]=Label("Value",r,"—",new Rect(.73f,.06f,.24f,.88f),38,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
                var oldDetail=r.Find("Detail");if(oldDetail)oldDetail.gameObject.SetActive(false);
                if(i<3){var divider=Panel("Divider",r,null);divider.GetComponent<Image>().color=new Color32(7,91,139,150);Fit(divider,new Rect(.992f,.12f,.004f,.76f));}
            }
            battleLog=Panel("Battle log",root,theme.CreamPanel);battleLog.GetComponent<Image>().pixelsPerUnitMultiplier=2;
            battleHeading=Panel("Heading",battleLog,null);
            Label("Title",battleHeading,"RECENT BATTLES",new Rect(.018f,.02f,.38f,.43f),26,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            recordCount=Label("Records",battleHeading,"",new Rect(.42f,.04f,.22f,.39f),19,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);recordCount.color=new Color32(63,89,115,255);
            localReplays=Control("Local replays",battleHeading,"REPLAYS",()=>{if(ownProfile)ReplayBrowser.Open(null);});Fit((RectTransform)localReplays.transform,new Rect(.42f,.02f,.22f,.43f));
            for(int i=0;i<BattleColumns.Length;i++)
            {
                var column=Label(BattleColumns[i],battleHeading,BattleColumns[i],new Rect(BattleColumnLeft[i],.55f,BattleColumnWidth[i],.35f),20,ArcadeTextTreatment.PrizeAmount,i==0?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center);
                column.color=new Color32(63,89,115,255);
            }
            var logDivider=Panel("Divider",battleHeading,null);logDivider.GetComponent<Image>().color=new Color32(52,82,102,120);Fit(logDivider,new Rect(.016f,.975f,.968f,.02f));
            previous=Control("Previous",battleHeading,"PREV",()=>SelectPage(requestedPage-1));Fit((RectTransform)previous.transform,new Rect(.675f,.02f,.09f,.43f));
            pageLabel=Label("Page",battleHeading,"1 / 1",new Rect(.77f,.03f,.11f,.41f),22,ArcadeTextTreatment.PrizeAmount);
            next=Control("Next",battleHeading,"NEXT",()=>SelectPage(requestedPage+1));Fit((RectTransform)next.transform,new Rect(.89f,.02f,.09f,.43f));
            scroll=BuildScroll(battleLog);
            empty=Panel("Empty",battleLog,null);
            Shadow(empty,new Rect(.456f,.46f,.088f,.035f));
            Icon("Icon",empty,theme.TrophyIcon,new Rect(.43f,.10f,.14f,.38f));
            emptyTitle=Label("Title",empty,"",new Rect(.04f,.53f,.92f,.19f),28,ArcadeTextTreatment.PrizeAmount);
            emptyDescription=Label("Description",empty,"",new Rect(.07f,.76f,.86f,.16f),23,ArcadeTextTreatment.PrizeAmount);emptyDescription.textWrappingMode=TextWrappingModes.Normal;
            var oldAction=footer.Find("Action");if(oldAction){oldAction.SetParent(empty,false);oldAction.name="Retry";}
            retry=Control("Retry",empty,"RETRY",Refresh);Fit((RectTransform)retry.transform,new Rect(.38f,.84f,.24f,.14f));
        }
        void Render()
        {
            if(!IsConfigured)return;
            bool ready=state==ViewState.Ready&&data!=null,guest=state==ViewState.Guest;
            nameLabel.text=ready?data.Name:guest?(api.LastPlayer?.displayName??"LOCAL GUEST"):state==ViewState.Loading?"LOADING...":"COMMANDER";
            ClearStatus();identityLabel.text=IdentityText();
            share.interactable=ready&&links&&!string.IsNullOrEmpty(links.ShareUrl(data.Id));
            for(int i=0;i<4;i++)values[i].text="—";
            if(ready){values[0].text=data.SeasonRank.HasValue?PlayerProfileData.Format(data.SeasonRank.Value):"—";values[1].text=PlayerProfileData.Format(data.Points);values[2].text=PlayerProfileData.Format(data.Matches);values[3].text=PlayerProfileData.Format(data.BestScore);}
            statNames[0].text=ready?data.Season.ToUpperInvariant()+" RANK":"SEASON RANK";
            recordCount.text=ready?PlayerProfileData.Format(data.TotalRecords)+" RECORDS":"— RECORDS";pageLabel.text=ready?data.Page+" / "+data.TotalPages:"—";
            localReplays.gameObject.SetActive(ownProfile);recordCount.gameObject.SetActive(!ownProfile);
            previous.interactable=ready&&data.Page>1;next.interactable=ready&&data.Page<data.TotalPages;
            retry.gameObject.SetActive(state==ViewState.Unavailable);retry.interactable=state==ViewState.Unavailable;
            foreach(var control in new[]{share,previous,next,retry})
            {control.image.sprite=control.interactable?art.tankCostButton:art.tankCostButtonLocked;control.image.color=control.interactable?new Color(.5f,.68f,.86f):Color.white;control.GetComponent<SettingsControlVisual>().Refresh();}
            string heading,detail;
            switch(state)
            {
                case ViewState.Ready:heading="NO BATTLES RECORDED";detail="Completed battles will appear here.";break;
                case ViewState.Guest:heading="LOCAL GUEST PROFILE";detail="Connect a wallet to load your online battles and rankings.";break;
                case ViewState.SignIn:heading="CONNECT TO VIEW YOUR PROFILE";detail="Your online combat record needs a connected account.";break;
                case ViewState.NotFound:heading="PLAYER NOT FOUND";detail="This profile link does not match an available player.";break;
                case ViewState.Loading:heading="LOADING COMBAT RECORD";detail="Fetching player statistics and recent battles...";break;
                default:heading="PROFILE UNAVAILABLE";detail="Could not load the combat record. Please retry.";break;
            }
            emptyTitle.text=heading;emptyDescription.text=detail;
            Fit(emptyDescription.rectTransform,new Rect(.07f,.74f,.86f,state==ViewState.Unavailable?.10f:.16f));
            BuildBattles(ready);empty.gameObject.SetActive(!ready||data.Battles.Count==0);
            ApplyLayout(menu.Platform);Navigation();
        }
        void BuildBattles(bool ready)
        {
            battles.Clear();foreach(Transform child in scroll.content)child.gameObject.SetActive(false);
            if(!ready)return;
            for(int i=0;i<data.Battles.Count;i++)
            {
                int index=i;var row=data.Battles[i];var r=Panel("Battle "+i,scroll.content,theme.Rounded);r.gameObject.SetActive(true);r.GetComponent<Image>().pixelsPerUnitMultiplier=5;
                var stripe=i%2==0?new Color32(245,234,207,255):new Color32(255,248,230,255);r.GetComponent<Image>().color=stripe;
                var b=r.GetComponent<Button>();if(!b)b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.image.raycastTarget=true;b.transition=Selectable.Transition.None;b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>Watch(index));
                Icon("Outcome",r,pictures?(row.Won?pictures.victory:pictures.defeat):null,new Rect(.018f,.12f,.08f,.76f));
                var outcome=Label("Result",r,row.Won?"VICTORY":"DEFEAT",new Rect(.112f,.12f,.16f,.40f),28,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);outcome.color=row.Won?new Color32(25,112,40,255):new Color32(190,24,24,255);
                var date=Label("Date",r,row.Date,new Rect(.112f,.56f,.16f,.30f),19,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);date.color=new Color32(63,89,115,255);
                string[] numbers={row.Mode,PlayerProfileData.Format(row.Stage),PlayerProfileData.Format(row.Score),PlayerProfileData.Format(row.Points)};
                for(int j=1;j<BattleColumns.Length;j++)
                {
                    var oldCaption=r.Find(BattleColumns[j]);if(oldCaption)oldCaption.gameObject.SetActive(false);
                    Label(BattleColumns[j]+" value",r,numbers[j-1],new Rect(BattleColumnLeft[j],.10f,BattleColumnWidth[j],.80f),32,ArcadeTextTreatment.PrizeAmount);
                }
                var rowDivider=Panel("Divider",r,null);rowDivider.GetComponent<Image>().color=new Color32(31,123,171,60);Fit(rowDivider,new Rect(.008f,.985f,.984f,.015f));
                bool available=row.Replay;
                var watch=Panel("Watch",r,available?art.tankCostButton:art.tankCostButtonLocked);Fit(watch,new Rect(.835f,.17f,.15f,.66f));
                var caption=Label("Caption",watch,available?"WATCH":"NO REPLAY",available?new Rect(.06f,.10f,.65f,.80f):new Rect(.06f,.10f,.88f,.80f),26,ArcadeTextTreatment.PrizeAmount);styles.ApplyCleanButton(caption,false);
                var play=watch.Find("Play arrow") as RectTransform;
                if(!play){var go=new GameObject("Play arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=root.gameObject.layer;play=(RectTransform)go.transform;play.SetParent(watch,false);}
                Fit(play,new Rect(.75f,.27f,.12f,.46f));play.localRotation=Quaternion.Euler(0,0,180);play.GetComponent<BackTabArrow>().raycastTarget=false;play.gameObject.SetActive(available);
                var highlight=CardSelectionHighlight.Ensure(b.image,stripe);var focus=r.GetComponent<ProfileRowFocus>();if(!focus)focus=r.gameObject.AddComponent<ProfileRowFocus>();focus.Configure(scroll,highlight);battles.Add(b);
            }
            LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
        }
        void Navigation()
        {
            var rows=new List<Selectable[]>{new Selectable[]{back,share},new Selectable[]{localReplays,previous,next}};
            foreach(var b in battles)rows.Add(new Selectable[]{b});if(retry.gameObject.activeSelf)rows.Add(new Selectable[]{retry});Psg1UiNavigation.Rows(rows.ToArray());
        }
        public void ApplyLayout(MainMenuPlatform platform)
        {
            if(!IsConfigured)return;
            var head=new TvTitleHeaderLayout(root);var foot=new TvStatusFooterLayout(root);
            head.PlaceTitle(header,titleIcon.rectTransform,title.rectTransform,true);head.PlaceNavigation(header,nav,true);foot.Place(footer);
            MainMenuScene.Place((RectTransform)back.transform,3,3,nav.rect.width-6,head.NavigationHeight-6);
            float top=head.Height+12,available=root.rect.height-top-foot.Height-16,width=root.rect.width-8;
            float heroH=Mathf.Clamp(available*.15f,76f,94f);
            MainMenuScene.Place(hero,4,top,width,heroH);MainMenuScene.Place(metrics,6,3,width-12,foot.Height-6);
            menu.ApplyDetailSurface(hero,heroPaper,ref commanderMaterial,3f);LayoutCommander();
            for(int i=0;i<4;i++)
            {
                MainMenuScene.Place(stats[i],i*metrics.rect.width/4,0,metrics.rect.width/4,metrics.rect.height);
                LayoutStatistic(i);
            }
            float logTop=top+heroH+7,logHeight=root.rect.height-foot.Height-12-logTop;
            float logHeaderHeight=Mathf.Clamp(logHeight*.13f,64,78);
            MainMenuScene.Place(battleLog,4,logTop,width,logHeight);MainMenuScene.Place(battleHeading,11,7,width-42,logHeaderHeight);
            MainMenuScene.Place((RectTransform)scroll.transform,5,logHeaderHeight+15,width-10,logHeight-logHeaderHeight-21);
            MainMenuScene.Place(empty,10,logHeaderHeight+17,width-36,logHeight-logHeaderHeight-28);
            scroll.CardAspectRatio=Mathf.Max(1,(width-42)/Mathf.Clamp(available*.12f,68,82));
            foreach(var image in root.GetComponentsInChildren<Image>(true))if(image.sprite==art.tankCostButton||image.sprite==art.tankCostButtonLocked)TvTitleHeaderLayout.FitSkin(image,Mathf.Max(20,image.rectTransform.rect.height));
        }
        void LayoutCommander()
        {
            float width=heroPaper.rect.width,compactHeight=heroPaper.rect.height;
            float portraitSize=compactHeight-10f,left=portraitSize+21f,shareWidth=Mathf.Clamp(width*.28f,210f,300f),shareLeft=width-shareWidth-8f,textWidth=shareLeft-left-12f;
            MainMenuScene.Place(avatar.rectTransform,7,5,portraitSize,portraitSize);
            nameLabel.fontSize=nameLabel.fontSizeMax=40;
            MainMenuScene.Place(nameLabel.rectTransform,left,1,textWidth,compactHeight*.54f);
            float identityTop=compactHeight*.54f,identityHeight=compactHeight*.39f;
            identityLabel.fontSize=identityLabel.fontSizeMax=Mathf.Clamp(compactHeight*.28f,18f,22f);
            MainMenuScene.Place(identityLabel.rectTransform,left,identityTop,textWidth,identityHeight);
            float shareHeight=Mathf.Min(compactHeight*.72f,52f);
            MainMenuScene.Place(share.transform as RectTransform,shareLeft,(compactHeight-shareHeight)*.5f,shareWidth,shareHeight);
        }
        void LayoutStatistic(int index)
        {
            var row=stats[index];float width=row.rect.width,height=row.rect.height;
            var icon=row.Find("Icon").GetComponent<Image>();float aspect=icon.sprite?icon.sprite.rect.width/icon.sprite.rect.height:1f;
            float iconHeight=Mathf.Min(height*.90f,width*.30f/Mathf.Max(1f,aspect)),iconWidth=iconHeight*aspect;
            float left=6f+iconWidth+7f,gap=7f,textWidth=width-left-9f-gap;
            MainMenuScene.Place(icon.rectTransform,6,(height-iconHeight)*.5f,iconWidth,iconHeight);
            MainMenuScene.Place(row.Find("Ground shadow") as RectTransform,6+iconWidth*.10f,(height+iconHeight)*.5f-2,iconWidth*.80f,2f);
            var name=statNames[index];var value=values[index];
            float nameSize=Mathf.Clamp(height*.44f,20,24),valueSize=Mathf.Clamp(height*.68f,30,38);
            name.fontSize=name.fontSizeMax=nameSize;value.fontSize=value.fontSizeMax=valueSize;
            float nameWidth=name.GetPreferredValues(name.text).x,valueWidth=value.GetPreferredValues(value.text).x;
            float scale=Mathf.Min(1,textWidth/Mathf.Max(1,nameWidth+valueWidth));
            name.fontSize=name.fontSizeMax=Mathf.Max(12,nameSize*scale);value.fontSize=value.fontSizeMax=Mathf.Max(12,valueSize*scale);
            nameWidth=Mathf.Min(nameWidth*scale,textWidth*.78f);
            MainMenuScene.Place(name.rectTransform,left,0,nameWidth,height);
            MainMenuScene.Place(value.rectTransform,left+nameWidth+gap,0,textWidth-nameWidth,height);
            var divider=row.Find("Divider") as RectTransform;
            if(divider)MainMenuScene.Place(divider,width-1f,height*.12f,1f,height*.76f);
        }
        TankRosterScroll BuildScroll(Transform parent)
        {
            var area=Panel("Rows",parent,null);area.GetComponent<Image>().raycastTarget=true;
            var viewport=Panel("Viewport",area,null);viewport.GetComponent<Image>().raycastTarget=true;Fit(viewport,new Rect(0,0,1,1));viewport.offsetMin=new Vector2(3,3);viewport.offsetMax=new Vector2(-23,-3);
            if(!viewport.GetComponent<RectMask2D>())viewport.gameObject.AddComponent<RectMask2D>();
            var content=Panel("Content",viewport,null);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=content.sizeDelta=Vector2.zero;
            var grid=content.GetComponent<GridLayoutGroup>();if(!grid)grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=1;grid.spacing=new Vector2(0,4);grid.padding=new RectOffset(3,3,3,3);
            var fit=content.GetComponent<ContentSizeFitter>();if(!fit)fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var track=Panel("Scrollbar",area,theme.DarkPanel);track.GetComponent<Image>().pixelsPerUnitMultiplier=3;track.GetComponent<Image>().raycastTarget=true;track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.pivot=new Vector2(1,.5f);track.anchoredPosition=new Vector2(-3,0);track.sizeDelta=new Vector2(16,-8);
            var sliding=Panel("Sliding",track,null);Fit(sliding,new Rect(0,0,1,1));sliding.offsetMin=Vector2.one*2;sliding.offsetMax=Vector2.one*-2;
            var thumb=Panel("Handle",sliding,theme.GoldPanel);Fit(thumb,new Rect(0,0,1,1));thumb.GetComponent<Image>().pixelsPerUnitMultiplier=3;thumb.GetComponent<Image>().raycastTarget=true;
            var bar=track.GetComponent<Scrollbar>();if(!bar)bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=thumb;bar.targetGraphic=thumb.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;bar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var s=area.GetComponent<TankRosterScroll>();if(!s)s=area.gameObject.AddComponent<TankRosterScroll>();s.viewport=viewport;s.content=content;s.horizontal=false;s.vertical=true;s.movementType=ScrollRect.MovementType.Clamped;s.scrollSensitivity=35;s.inertia=true;s.verticalScrollbar=bar;s.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;return s;
        }
        Button Control(string name,Transform parent,string caption,UnityEngine.Events.UnityAction action)
        {
            var r=Panel(name,parent,art.tankCostButton);var b=r.GetComponent<Button>();if(!b)b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.image.raycastTarget=true;b.transition=Selectable.Transition.None;b.onClick.RemoveAllListeners();b.onClick.AddListener(action);b.image.color=new Color(.5f,.68f,.86f);
            var f=Panel("Focus",r,art.tankCostButton);Fit(f,new Rect(0,0,1,1));var label=Label("Caption",r,caption,new Rect(.04f,.05f,.92f,.90f),28,ArcadeTextTreatment.PrizeAmount);
            var visual=r.GetComponent<SettingsControlVisual>();if(!visual)visual=r.gameObject.AddComponent<SettingsControlVisual>();visual.Configure(f.GetComponent<Image>(),null,label,styles);return b;
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var r=parent.Find(name) as RectTransform;if(!r){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=parent.gameObject.layer;r=(RectTransform)go.transform;r.SetParent(parent,false);}
            var image=r.GetComponent<Image>();image.sprite=sprite;image.overrideSprite=null;image.type=Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return r;
        }
        Image Icon(string name,Transform parent,Sprite sprite,Rect bounds){var r=Panel(name,parent,sprite);Fit(r,bounds);var image=r.GetComponent<Image>();image.type=Image.Type.Simple;image.preserveAspect=true;return image;}
        void Shadow(Transform parent,Rect bounds)
        {
            var r=parent.Find("Ground shadow") as RectTransform;if(!r){var go=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=root.gameObject.layer;r=(RectTransform)go.transform;r.SetParent(parent,false);}
            r.SetAsFirstSibling();Fit(r,bounds);var shadow=r.GetComponent<TankGroundShadow>();shadow.raycastTarget=false;shadow.color=new Color32(35,25,13,112);
        }
        TMP_Text Label(string name,Transform parent,string value,Rect bounds,float size,ArcadeTextTreatment treatment,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var old=parent.Find(name);TMP_Text t;if(old)t=old.GetComponent<TMP_Text>();else{var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=root.gameObject.layer;go.transform.SetParent(parent,false);t=go.GetComponent<TMP_Text>();}
            Fit(t.rectTransform,bounds);t.font=ArcadeTextStyles.HeadingSdf;t.text=value;t.richText=false;t.fontSize=t.fontSizeMax=size;t.fontSizeMin=12;t.enableAutoSizing=true;t.alignment=alignment;t.textWrappingMode=TextWrappingModes.NoWrap;t.raycastTarget=false;styles.Apply(t,treatment);return t;
        }
        static void Fit(RectTransform r,Rect b){r.pivot=new Vector2(.5f,.5f);r.anchorMin=new Vector2(b.x,1-b.y-b.height);r.anchorMax=new Vector2(b.x+b.width,1-b.y);r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
