using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Audio, display, account and browser phone pairing inside the shared TV.</summary>
    public sealed partial class SettingsScreen : MonoBehaviour
    {
        MainMenuScene menu;MainMenuApiClient api;MenuTheme theme;PreBattleArt art;SettingsArt illustrations;
        RectTransform root,header,footer,body,backBar;Image titleIcon;TMP_Text title,version,accountName,phoneDescription,phoneStatus,pairCaption;
        readonly RectTransform[] rows=new RectTransform[4];
        readonly Image[] icons=new Image[4];
        readonly CardSelectionHighlight[] rowFocus=new CardSelectionHighlight[4];
        readonly Button[] toggles=new Button[4];
        readonly Button[] rowButtons=new Button[2];
        Button back,logout,pair;RawImage qr;RectTransform qrFrame;TMP_Text qrLabel;
        readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        PhoneControllerHost phone;[System.NonSerialized] bool configured;string logoutError;float nextPhoneRefresh;
        public bool IsConfigured=>configured&&root&&menu&&rows[0]&&toggles[0];
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public RectTransform Root=>root;
        public Button[] ToggleButtons=>toggles;
        public Button[] RowButtons=>rowButtons;
        public Button BackButton=>back;
        public Button LogoutButton=>logout;
        public void Configure(MainMenuScene owner,MenuTheme skin,MainMenuApiClient client,RectTransform frame)
        {
            if(api)api.PlayerLoaded-=OnPlayer;
            menu=owner;theme=skin;api=client;art=Resources.Load<PreBattleArt>("PreBattleArt");illustrations=Resources.Load<SettingsArt>("SettingsArt");
            var old=frame.Find("Settings screen");bool open=old&&old.gameObject.activeSelf;
            root=Panel("Settings screen",frame,null);root.GetComponent<Image>().raycastTarget=true;
            header=Panel("Title plate",root,art.tankTitlePanel);header.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            titleIcon=Icon("Icon",header,theme.SettingsIcon,new Rect(0,0,1,1));
            title=Label("Title",header,"SETTINGS",new Rect(0,0,1,1),28,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            backBar=Panel("Navigation",header,art.tankCostButton);backBar.GetComponent<Image>().color=new Color(.08f,.63f,.68f);
            back=Control("Back",backBar,"BACK",()=>menu.CloseSettings());
            var backBackground=back.GetComponent<Image>();backBackground.sprite=null;backBackground.color=Color.clear;
            var backFocus=back.transform.Find("Focus").GetComponent<Image>();backFocus.enabled=true;
            back.GetComponent<SettingsControlVisual>().enabled=false;back.targetGraphic=backFocus;back.transition=Selectable.Transition.ColorTint;
            var backColors=ColorBlock.defaultColorBlock;backColors.normalColor=backColors.disabledColor=Color.clear;
            backColors.highlightedColor=backColors.selectedColor=backColors.pressedColor=Color.white;backColors.fadeDuration=.08f;back.colors=backColors;
            var backText=back.transform.Find("Caption").GetComponent<TMP_Text>();Fit(backText.rectTransform,new Rect(.29f,.025f,.68f,.95f));styles.ApplyCleanButton(backText,false);
            var arrow=back.transform.Find("Back arrow") as RectTransform;
            if(!arrow){var go=new GameObject("Back arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));go.layer=root.gameObject.layer;arrow=(RectTransform)go.transform;arrow.SetParent(back.transform,false);}
            Fit(arrow,new Rect(.10f,.17f,.14f,.66f));arrow.GetComponent<BackTabArrow>().raycastTarget=false;
            body=Panel("Rows",root,null);
            string[] names={"MUTE","SCANLINE","ACCOUNT","PHONE CONTROLLER"};
            string[] descriptions={"Game audio","Retro screen effect","Loading account...","Scan with your phone to use it as a controller."};
            Sprite[] pictures={illustrations?illustrations.mute:null,illustrations?illustrations.scanline:null,illustrations?illustrations.account:null,illustrations?illustrations.phone:null};
            for(int i=0;i<4;i++)
            {
                var row=rows[i]=Panel("Row "+i,body,art.tankCardAvailable);row.GetComponent<Image>().pixelsPerUnitMultiplier=4;
                rowFocus[i]=CardSelectionHighlight.Ensure(row.GetComponent<Image>());
                Shadow(row,i==3?new Rect(.035f,.75f,.26f,.04f):new Rect(.052f,.77f,.14f,.055f));
                icons[i]=Icon("Icon",row,pictures[i],i==3?new Rect(.025f,.15f,.28f,.62f):new Rect(.035f,.12f,.17f,.72f));
                var divider=Panel("Divider",row,null);divider.GetComponent<Image>().color=new Color32(20,125,199,180);Fit(divider,i==3?new Rect(.33f,.14f,.0015f,.72f):new Rect(.235f,.17f,.0015f,.66f));
                Label("Name",row,names[i],i==3?new Rect(.36f,.12f,.39f,.20f):new Rect(.27f,.14f,.41f,.39f),i==3?34:38,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
                var description=Label("Description",row,descriptions[i],i==3?new Rect(.36f,.34f,.37f,.22f):new Rect(.27f,.54f,.39f,.27f),24,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);description.color=new Color32(25,76,126,255);
                if(i==2)accountName=description;if(i==3){phoneDescription=description;description.textWrappingMode=TextWrappingModes.Normal;}
                if(i<2)
                {
                    int rowIndex=i;
                    var rowButton=row.GetComponent<Button>();if(!rowButton)rowButton=row.gameObject.AddComponent<Button>();rowButtons[i]=rowButton;
                    rowButton.targetGraphic=row.GetComponent<Image>();rowButton.image.raycastTarget=true;rowButton.transition=Selectable.Transition.None;rowButton.onClick.RemoveAllListeners();rowButton.onClick.AddListener(()=>TogglePreference(rowIndex));
                    var focus=row.GetComponent<SettingsRowFocus>();if(!focus)focus=row.gameObject.AddComponent<SettingsRowFocus>();focus.Configure(rowFocus[i]);
                    var group=Panel("Toggle",row,art.tankCostButton);group.GetComponent<Image>().color=new Color(.08f,.63f,.68f);Fit(group,new Rect(.72f,.22f,.25f,.56f));
                    for(int j=0;j<2;j++){bool value=j==1;var b=toggles[i*2+j]=Control(value?"On":"Off",group,value?"ON":"OFF",()=>SetPreference(rowIndex,value),true);b.enabled=false;b.image.raycastTarget=false;b.navigation=new Navigation{mode=Navigation.Mode.None};Fit((RectTransform)b.transform,new Rect(j*.5f+.015f,.06f,.47f,.88f));}
                }
            }
            logout=Control("Logout",rows[2],"LOGOUT",Logout);Fit((RectTransform)logout.transform,new Rect(.72f,.22f,.25f,.56f));
            if(illustrations&&illustrations.logout)logout.image.sprite=illustrations.logout;
            phoneStatus=Label("Connection",rows[3],"NOT CONNECTED",new Rect(.36f,.59f,.36f,.12f),22,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
            pair=Control("Pair",rows[3],"PAIR PHONE",Pair);Fit((RectTransform)pair.transform,new Rect(.36f,.72f,.32f,.22f));pairCaption=pair.transform.Find("Caption").GetComponent<TMP_Text>();pairCaption.fontSizeMax=24;
            qrFrame=Panel("Pairing",rows[3],art.statusPanel);qrFrame.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            var q=qrFrame.Find("QR");if(!q){var go=new GameObject("QR",typeof(RectTransform),typeof(RawImage));go.layer=root.gameObject.layer;go.transform.SetParent(qrFrame,false);q=go.transform;}
            qr=q.GetComponent<RawImage>();qr.raycastTarget=false;qr.color=Color.white;
            qrLabel=Label("Label",qrFrame,"PAIRING QR",new Rect(.07f,.83f,.86f,.12f),18,ArcadeTextTreatment.PrizeAmount);
            footer=Panel("Status",root,art.statusPanel);TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),art.statusPanel);
            version=Label("Version",footer,"VERSION "+Application.version,new Rect(.03f,.13f,.94f,.74f),24,ArcadeTextTreatment.PrizeAmount);
            ConfigureNotifications();if(api)api.PlayerLoaded+=OnPlayer;
            configured=true;root.gameObject.SetActive(open);GamePreferences.Apply();Refresh();
        }
        public void Open()
        {
            root.gameObject.SetActive(true);root.SetAsLastSibling();menu.SetHeroVisible(false);menu.SetTankSelectorBackdrop(true);menu.RefreshLayout();
            phone=PhoneControllerHost.Ensure();if(phone.Supported)phone.Begin();
            Refresh();if(EventSystem.current)EventSystem.current.SetSelectedGameObject(rowButtons[0].gameObject);
        }
        public void Close(){root.gameObject.SetActive(false);}
        public void KeepControllerFocus()=>Psg1UiNavigation.KeepFocus(root,rowButtons[0]);
        void TogglePreference(int row){if(row==0)SetPreference(0,!GamePreferences.Muted);else if(row==1)SetPreference(1,!GamePreferences.Scanlines);}
        public void SetPreference(int row,bool value){if(row==0)GamePreferences.SetMuted(value);else if(row==1)GamePreferences.SetScanlines(value);Refresh();}
        void OnPlayer(MainMenuApiClient.PlayerSnapshot player){Refresh();}
        void Refresh()
        {
            if(!IsConfigured)return;
            for(int i=0;i<4;i++)toggles[i].GetComponent<SettingsControlVisual>().SetActive((i<2?GamePreferences.Muted:GamePreferences.Scanlines)==(i%2==1));
            string displayName=api?.LastPlayer?.displayName;
            accountName.text=!string.IsNullOrEmpty(logoutError)?logoutError:!string.IsNullOrWhiteSpace(displayName)?displayName:api&&api.IsLocalGuest?"LOCAL GUEST":api&&api.IsWalletAuthenticated?"WALLET CONNECTED":"NOT CONNECTED";
            bool loggedIn=api&&(api.IsWalletAuthenticated||api.IsLocalGuest);
            logout.interactable=api&&!api.IsSigningOut;
            logout.transform.Find("Caption").GetComponent<TMP_Text>().text=api&&api.IsSigningOut?"LOGGING OUT...":loggedIn?"LOGOUT":"CONNECT WALLET";
            var logoutSkin=loggedIn&&illustrations&&illustrations.logout?illustrations.logout:art.tankCostButton;
            logout.image.sprite=logoutSkin;logout.image.color=loggedIn&&illustrations&&illustrations.logout?new Color(.82f,.82f,.82f):new Color(.5f,.68f,.86f);
            logout.transform.Find("Focus").GetComponent<Image>().sprite=logoutSkin;
            RefreshPhone();RefreshNotifications();
            Psg1UiNavigation.Rows(new Selectable[]{back},new Selectable[]{rowButtons[0]},new Selectable[]{rowButtons[1]},new Selectable[]{logout},new Selectable[]{pair},new Selectable[]{notifications});
        }
        void RefreshPhone()
        {
            bool supported=phone&&phone.Supported;
            phoneDescription.text=supported?"Scan with your phone to use it as a controller.":"Use touch or your device controller. To pair a phone, open the web game.";
            phoneStatus.text=supported?phone.Status:"TOUCH / GAMEPAD READY";
            pair.gameObject.SetActive(supported);pairCaption.text=phone&&phone.Started?"NEW PAIRING":"RETRY PAIRING";pair.interactable=supported;
            pair.image.sprite=art.tankCostButton;pair.image.color=new Color(.5f,.68f,.86f);
            pair.GetComponent<SettingsControlVisual>().Refresh();
            qr.enabled=supported&&phone.QrTexture;qr.texture=qr.enabled?phone.QrTexture:null;
            qrLabel.text=qr.enabled?phone.RoomCode:supported?"WAITING FOR QR":"WEB PAIRING";
            qrFrame.gameObject.SetActive(supported);
            Fit(phoneDescription.rectTransform,supported?new Rect(.36f,.34f,.37f,.22f):new Rect(.36f,.35f,.57f,.23f));
            Fit(phoneStatus.rectTransform,supported?new Rect(.36f,.59f,.36f,.12f):new Rect(.36f,.68f,.55f,.12f));
        }
        void Pair(){if(phone){phone.Begin(true);RefreshPhone();RefreshNotifications();}}
        void Logout()
        {
            if(!api||api.IsSigningOut)return;
            if(!api.IsWalletAuthenticated&&!api.IsLocalGuest){api.ConnectWallet();return;}
            if(!Application.CanStreamedLevelBeLoaded("Login")){logoutError="Login screen is unavailable in this build.";Refresh();return;}
            logoutError=null;api.SignOut((ok,error)=>
            {
                if(!this)return;
                if(!ok){logoutError=error;Refresh();return;}
                if(phone)phone.Stop();SceneManager.LoadSceneAsync("Login");
            });Refresh();
        }
        void Update()
        {
            if(!IsConfigured||!IsOpen)return;
            if(Time.unscaledTime>=nextPhoneRefresh){nextPhoneRefresh=Time.unscaledTime+.1f;RefreshPhone();RefreshNotifications();}
            rowFocus[0].SetState(false,rowButtons[0].GetComponent<SettingsRowFocus>().Focused);
            rowFocus[1].SetState(false,rowButtons[1].GetComponent<SettingsRowFocus>().Focused);
            rowFocus[2].SetState(false,logout.GetComponent<SettingsControlVisual>().Focused);
            rowFocus[3].SetState(false,pair.interactable&&pair.GetComponent<SettingsControlVisual>().Focused);
        }
        public void ApplyLayout(MainMenuPlatform platform)
        {
            if(!IsConfigured)return;
            var head=new TvTitleHeaderLayout(root);var foot=new TvStatusFooterLayout(root);
            head.PlaceTitle(header,titleIcon.rectTransform,title.rectTransform,true,iconHeightFraction:.64f);head.PlaceNavigation(header,backBar,true);
            MainMenuScene.Place(back.transform as RectTransform,3,3,backBar.rect.width-6,head.NavigationHeight-6);
            foot.Place(footer);LayoutNotifications();float top=head.Height+12,height=root.rect.height-top-foot.Height-16;
            MainMenuScene.Place(body,4,top,root.rect.width-8,height);
            float gap=8,rowH=(height-gap*3)/4.6f;
            for(int i=0;i<4;i++)MainMenuScene.Place(rows[i],0,i*(rowH+gap),body.rect.width,i==3?rowH*1.6f:rowH);
            float side=Mathf.Min(rows[3].rect.height*.86f,rows[3].rect.width*.20f);
            MainMenuScene.Place(qrFrame,rows[3].rect.width-side-16,(rows[3].rect.height-side)*.5f,side,side);
            float qrSide=side*.74f;MainMenuScene.Place(qr.rectTransform,(side-qrSide)*.5f,side*.07f,qrSide,qrSide);
            foreach(var image in root.GetComponentsInChildren<Image>(true))
                if(image.sprite==art.tankCostButton||image.sprite==art.tankCostButtonSelected||image.sprite==(illustrations?illustrations.logout:null))
                    if(image.sprite)TvTitleHeaderLayout.FitSkin(image,Mathf.Max(20,image.rectTransform.rect.height));
            Refresh();
        }
        Button Control(string name,Transform parent,string caption,UnityEngine.Events.UnityAction action,bool toggle=false)
        {
            var rect=Panel(name,parent,art.tankCostButton);var b=rect.GetComponent<Button>();if(!b)b=rect.gameObject.AddComponent<Button>();
            b.targetGraphic=rect.GetComponent<Image>();b.image.raycastTarget=true;b.transition=Selectable.Transition.None;b.onClick.RemoveAllListeners();b.onClick.AddListener(action);
            b.image.color=new Color(.5f,.68f,.86f);
            var focus=Panel("Focus",rect,art.tankCostButton);Fit(focus,new Rect(0,0,1,1));
            var active=Panel("Active",rect,art.tankCostButtonSelected);Fit(active,new Rect(0,0,1,1));active.gameObject.SetActive(toggle);
            var label=Label("Caption",rect,caption,new Rect(.035f,.05f,.93f,.90f),28,ArcadeTextTreatment.PrizeAmount);
            var visual=rect.GetComponent<SettingsControlVisual>();if(!visual)visual=rect.gameObject.AddComponent<SettingsControlVisual>();visual.Configure(focus.GetComponent<Image>(),toggle?active.GetComponent<Image>():null,label,styles);
            return b;
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var rect=parent.Find(name) as RectTransform;
            if(!rect){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=parent.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            var image=rect.GetComponent<Image>();image.sprite=sprite;image.overrideSprite=null;image.type=Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        Image Icon(string name,Transform parent,Sprite sprite,Rect bounds){var r=Panel(name,parent,sprite);Fit(r,bounds);var i=r.GetComponent<Image>();i.type=Image.Type.Simple;i.preserveAspect=true;return i;}
        TMP_Text Label(string name,Transform parent,string value,Rect bounds,float size,ArcadeTextTreatment treatment,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var old=parent.Find(name);TMP_Text t;
            if(old)t=old.GetComponent<TMP_Text>();else{var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=root.gameObject.layer;go.transform.SetParent(parent,false);t=go.GetComponent<TMP_Text>();}
            Fit(t.rectTransform,bounds);t.font=ArcadeTextStyles.HeadingSdf;t.text=value;t.richText=false;t.fontSize=t.fontSizeMax=size;t.fontSizeMin=12;t.enableAutoSizing=true;t.alignment=align;t.textWrappingMode=TextWrappingModes.NoWrap;t.raycastTarget=false;styles.Apply(t,treatment);return t;
        }
        void Shadow(Transform parent,Rect bounds)
        {
            var rect=parent.Find("Ground shadow") as RectTransform;if(!rect){var go=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));go.layer=root.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(parent,false);}
            Fit(rect,bounds);var s=rect.GetComponent<TankGroundShadow>();s.color=new Color32(35,25,13,112);s.raycastTarget=false;
        }
        static void Fit(RectTransform r,Rect b){r.pivot=new Vector2(.5f,.5f);r.anchorMin=new Vector2(b.x,1-b.y-b.height);r.anchorMax=new Vector2(b.x+b.width,1-b.y);r.offsetMin=r.offsetMax=Vector2.zero;}
        void OnDestroy(){if(api)api.PlayerLoaded-=OnPlayer;styles.Dispose();}
    }
}
