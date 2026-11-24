using System;
using System.Collections;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Optional referral entry and a shareable account link.</summary>
    public sealed class ReferralScreen : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void BattleCitiesCopyReferral(string target,string text);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void BattleCitiesPasteReferral(string target,string requestId);
#endif
        MainMenuApiClient api; MenuTheme theme; PreBattleArt art;
        RectTransform overlay, panel; TMP_Text title, detail, message, primaryText, secondaryText;
        TMP_InputField input; Button paste,primary,secondary; GameObject previousFocus;
        readonly ArcadeTextStyles styles = new ArcadeTextStyles();
        string requestedPlayer, checkedPlayer, loadedPlayer,pasteRequestId,pasteSnapshot; JObject profile;
        Action profileChanged;
        bool firstPrompt, share, busy, promptPending;
        public bool IsConfigured => overlay && api;
        public bool IsOpen => overlay && overlay.gameObject.activeSelf;

        public void Configure(MenuTheme skin, MainMenuApiClient client, RectTransform parent, Action onProfileChanged)
        {
            theme=skin;api=client;profileChanged=onProfileChanged;art=RemoteGameArt.Load<PreBattleArt>("PreBattleArt");
            overlay=Panel("Referral dialog",parent,null); Fit(overlay,new Rect(0,0,1,1));
            var shade=overlay.GetComponent<Image>();shade.color=new Color(0,.025f,.08f,.76f);shade.raycastTarget=true;
            panel=Panel("Card",overlay,art.tankCardAvailable);panel.GetComponent<Image>().pixelsPerUnitMultiplier=3;
            Fit(panel,new Rect(.14f,.11f,.72f,.78f));
            title=Label("Title",panel,"REFERRAL",new Rect(.06f,.045f,.88f,.14f),38,ArcadeTextTreatment.Gold);
            detail=Label("Description",panel,"",new Rect(.08f,.21f,.84f,.23f),25,ArcadeTextTreatment.Navy);detail.textWrappingMode=TextWrappingModes.Normal;
            var inputRect=Panel("Referral link or code",panel,art.statusPanel);Fit(inputRect,new Rect(.12f,.4875f,.54f,.115f));inputRect.GetComponent<Image>().raycastTarget=true;
            inputRect.gameObject.AddComponent<ReferralInputFrame>();
            inputRect.gameObject.AddComponent<RectMask2D>();
            var text=Label("Text",inputRect,"",new Rect(.04f,.05f,.92f,.90f),24,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);
            var placeholder=Label("Placeholder",inputRect,"REFERRAL CODE",new Rect(.04f,.05f,.92f,.90f),22,ArcadeTextTreatment.Navy,TextAlignmentOptions.MidlineLeft);placeholder.color=new Color32(7,43,94,130);
            input=inputRect.gameObject.AddComponent<TMP_InputField>();input.textViewport=inputRect;input.textComponent=text;input.placeholder=placeholder;input.characterLimit=2048;
            input.lineType=TMP_InputField.LineType.SingleLine;input.contentType=TMP_InputField.ContentType.Standard;
            paste=MakeButton("Paste",panel,new Rect(.68f,.4875f,.20f,.115f),Paste);paste.transform.Find("Caption").GetComponent<TMP_Text>().text="PASTE";
            message=Label("Status",panel,"",new Rect(.08f,.63f,.84f,.12f),21,ArcadeTextTreatment.Navy);message.textWrappingMode=TextWrappingModes.Normal;
            primary=MakeButton("Primary",panel,new Rect(.14f,.80f,.34f,.105f),Primary);primaryText=primary.transform.Find("Caption").GetComponent<TMP_Text>();
            secondary=MakeButton("Secondary",panel,new Rect(.52f,.80f,.34f,.105f),Close);secondaryText=secondary.transform.Find("Caption").GetComponent<TMP_Text>();
            Psg1UiNavigation.Rows(new Selectable[]{input,paste},new Selectable[]{primary,secondary});
            overlay.gameObject.SetActive(false);
        }
        public void CheckPlayer(MainMenuApiClient.PlayerSnapshot player)
        {
            if(!IsConfigured||player==null||player.provider!="wallet"||string.IsNullOrEmpty(player.id)||requestedPlayer==player.id||checkedPlayer==player.id)return;
            requestedPlayer=player.id;StartCoroutine(GetProfile(player.id,true));
        }
        internal bool CanEnterFromSettings(string playerId) => loadedPlayer==playerId && (bool?)profile?["referred"]==false;
        public void OpenEntry() { Open(false,false); }
        public void OpenShare() { Open(true,false); }
        void Open(bool sharing,bool initial)
        {
            if(!IsConfigured)return;
            previousFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            share=sharing;firstPrompt=initial;busy=false;pasteRequestId=null;
            overlay.gameObject.SetActive(true);overlay.SetAsLastSibling();
            title.text=sharing?"REFER & EARN":"HAVE A REFERRAL?";
            detail.text=sharing?"Share your link. Earn 1% of your friend's confirmed shop purchases, in the currency they spend.":"Enter a friend's referral link or code to permanently raise your 30-minute Fuel refill cap from 5 to 6.";
            input.gameObject.SetActive(true);input.readOnly=sharing;input.text="";
            Fit(input.transform as RectTransform,sharing?new Rect(.12f,.4875f,.76f,.115f):new Rect(.12f,.4875f,.54f,.115f));paste.gameObject.SetActive(!sharing);
            primaryText.text=sharing?"COPY LINK":"APPLY";secondaryText.text=initial?"SKIP":"CLOSE";
            message.text="";SetBusy(false);
            string player=api.LastPlayer?.id;
            if(string.IsNullOrEmpty(player)||api.LastPlayer.provider!="wallet") { message.text="SIGN IN WITH YOUR WALLET TO USE REFERRALS.";primary.interactable=false; }
            else if(initial&&loadedPlayer==player&&profile!=null) UpdateProfile();
            else {SetBusy(true);StartCoroutine(GetProfile(player,false));}
            RefreshNavigation();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(sharing?secondary.gameObject:input.gameObject);
        }
        IEnumerator GetProfile(string player,bool automatic)
        {
            long status=0;JObject body=null;string error=null;
            yield return api.Request("GET","/api/referrals",null,(s,b,e)=>{status=s;body=b;error=e;});
            if(requestedPlayer==player)requestedPlayer=null;
            if(!this||!api||api.LastPlayer?.id!=player)yield break;
            var result=body?["referral"] as JObject;
            if(status!=200||result==null)
            {
                if(!automatic&&IsOpen){SetBusy(false);message.text=error??"REFERRALS ARE UNAVAILABLE. PLEASE TRY AGAIN.";primary.interactable=false;RefreshNavigation();}
                yield break;
            }
            CacheProfile(player,result);
            if(automatic&&(bool?)profile["prompt"]==true)
            {
                if(!IsOpen)Open(false,true);else promptPending=true;
            }
            if(IsOpen&&!firstPrompt){SetBusy(false);UpdateProfile();}
        }
        void CacheProfile(string player, JObject result)
        {
            // Applying a referral is permanent. An older request may return after the confirmation.
            if(loadedPlayer==player && (bool?)profile?["referred"]==true)
            {
                result["referred"]=true;
                result["prompt"]=false;
            }
            loadedPlayer=checkedPlayer=player;
            profile=result;
            profileChanged?.Invoke();
        }
        void UpdateProfile()
        {
            if(profile==null)return;
            if(share)
            {
                input.text=(string)profile["link"]??"";
                var amounts=new System.Collections.Generic.List<string>();
                if(profile["rewards"] is JArray rewards)foreach(var reward in rewards)
                    amounts.Add(((string)reward["amount"]??"0")+" "+((string)reward["currency"]??"").ToUpperInvariant());
                message.text=(amounts.Count>0?"EARNED: "+string.Join("   ",amounts):"NO REWARDS YET")+"  •  CLAIMS COMING LATER";
                primary.interactable=!string.IsNullOrEmpty(input.text);
            }
            else if((bool?)profile["referred"]==true)
            {
                detail.text="A REFERRAL IS ALREADY ACTIVE. YOUR FUEL REFILLS UP TO 6 EVERY 30 MINUTES.";
                input.gameObject.SetActive(false);paste.gameObject.SetActive(false);primary.interactable=false;message.text="REFERRAL APPLIED";
            }
            RefreshNavigation();
        }
        void Primary()
        {
            if(busy)return;
            if(share)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                BattleCitiesCopyReferral(gameObject.name,input.text);
#else
                GUIUtility.systemCopyBuffer=input.text;message.text="LINK COPIED";
#endif
                return;
            }
            string value=input.text.Trim();if(string.IsNullOrEmpty(value)){message.text="ENTER A LINK OR CODE FIRST.";return;}
            var player=api.LastPlayer?.id;if(string.IsNullOrEmpty(player)){message.text="SIGN IN FIRST.";return;}
            pasteRequestId=null;
            SetBusy(true);StartCoroutine(Post(player,"apply",value));
        }
        void Paste()
        {
            if(busy||share||!input||!input.IsActive()||!input.interactable)return;
#if UNITY_WEBGL && !UNITY_EDITOR
            pasteRequestId=System.Guid.NewGuid().ToString("N");pasteSnapshot=input.text;
            BattleCitiesPasteReferral(gameObject.name,pasteRequestId);
#else
            try { ApplyPastedText(GUIUtility.systemCopyBuffer); }
            catch(System.Exception) { message.text="PASTE UNAVAILABLE. ENTER THE CODE MANUALLY."; }
#endif
        }
        void ApplyPastedText(string value)
        {
            if(string.IsNullOrWhiteSpace(value)){message.text="CLIPBOARD IS EMPTY.";return;}
            if(value.Length>input.characterLimit){message.text="CLIPBOARD TEXT IS TOO LONG.";return;}
            if(!TryReadReferralCode(value,out var code))
            {
                message.text="COPY A BATTLE CITIES REFERRAL LINK OR CODE.";
                return;
            }
            input.text=code;message.text="REFERRAL CODE PASTED.";
        }
        static bool TryReadReferralCode(string value,out string code)
        {
            code=value.Trim();
            if(Uri.TryCreate(code,UriKind.Absolute,out var link))
            {
                if(link.Scheme!=Uri.UriSchemeHttps||!link.IsDefaultPort||link.UserInfo.Length!=0)return false;
                switch(link.Host)
                {
                    case "battlecities.com": case "www.battlecities.com": case "play.battlecities.com": case "test.battlecities.com": break;
                    default: return false;
                }
                code="";
                foreach(var parameter in link.Query.TrimStart('?').Split('&'))
                {
                    var parts=parameter.Split(new[]{'='},2);
                    if(Uri.UnescapeDataString(parts[0])!="ref")continue;
                    if(parts.Length==2)code=Uri.UnescapeDataString(parts[1]);
                    break;
                }
            }
            code=code.ToUpperInvariant();
            return Regex.IsMatch(code,@"\A[A-F0-9]{12}\z");
        }
        [UnityEngine.Scripting.Preserve]
        public void OnReferralPasteResult(string json)
        {
            JObject result;try{result=JObject.Parse(json);}catch{ return; }
            if(!IsOpen||share||busy||!input.IsActive()||!input.interactable||string.IsNullOrEmpty(pasteRequestId)||
                (string)result["id"]!=pasteRequestId)return;
            pasteRequestId=null;
            if(input.text!=pasteSnapshot)return;
            if((bool?)result["ok"]!=true){message.text="PASTE UNAVAILABLE. ENTER THE CODE MANUALLY.";return;}
            ApplyPastedText((string)result["text"]);
        }
        IEnumerator Post(string player,string action,string code=null)
        {
            long status=0;JObject body=null;string error=null;
            var data=new JObject{["action"]=action,["expectedPlayerId"]=player};if(code!=null)data["code"]=code;
            yield return api.Request("POST","/api/referrals",data,(s,b,e)=>{status=s;body=b;error=e;});
            if(!this||!api||api.LastPlayer?.id!=player)yield break;
            if(status==200&&body?["referral"] is JObject result)
            {
                CacheProfile(player,result);
                if(action=="apply"&&IsOpen){SetBusy(false);firstPrompt=false;UpdateProfile();}
            }
            else if(action=="apply"&&IsOpen){SetBusy(false);message.text=(string)body?["statusText"]??error??"COULD NOT APPLY REFERRAL.";}
        }
        public void Close()
        {
            if(!IsOpen||busy)return;
            if(firstPrompt&&!share&&!string.IsNullOrEmpty(api.LastPlayer?.id))StartCoroutine(Post(api.LastPlayer.id,"dismiss"));
            bool showPrompt=promptPending&&!firstPrompt&&(bool?)profile?["prompt"]==true;
            promptPending=false;
            firstPrompt=false;pasteRequestId=null;overlay.gameObject.SetActive(false);input.gameObject.SetActive(true);
            var selectable=previousFocus?previousFocus.GetComponent<Selectable>():null;
            if(EventSystem.current&&Psg1UiNavigation.Available(selectable))EventSystem.current.SetSelectedGameObject(previousFocus);
            else if(previousFocus && TryGetComponent<SettingsScreen>(out var settings) && settings.IsOpen && previousFocus.transform.IsChildOf(settings.Root))
                Psg1UiNavigation.KeepFocus(settings.Root,settings.LogoutButton);
            if(showPrompt)Open(false,true);
        }
        public void KeepControllerFocus()=>Psg1UiNavigation.KeepFocus(overlay,secondary);
        public void OnReferralCopyResult(string result){if(IsOpen&&share)message.text=result=="copied"?"LINK COPIED":"COPY UNAVAILABLE - SELECT THE LINK TO COPY IT.";}
        void SetBusy(bool value){busy=value;primary.interactable=!value;secondary.interactable=!value;input.interactable=!value;paste.interactable=!value;if(value)message.text="LOADING...";RefreshNavigation();}
        void RefreshNavigation()
        {
            if(!IsOpen)return;
            primary.GetComponent<ReferralActionVisual>().Refresh();
            secondary.GetComponent<ReferralActionVisual>().Refresh();
            paste.GetComponent<ReferralActionVisual>().Refresh();
            Psg1UiNavigation.Rows(new Selectable[]{input,paste},new Selectable[]{primary,secondary});
        }
        RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;var image=go.GetComponent<Image>();image.sprite=sprite;image.type=sprite?Image.Type.Sliced:Image.Type.Simple;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        TMP_Text Label(string name,Transform parent,string value,Rect bounds,float size,ArcadeTextTreatment treatment,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var text=go.GetComponent<TMP_Text>();Fit(text.rectTransform,bounds);text.font=ArcadeTextStyles.HeadingSdf;text.text=value;text.richText=false;text.fontSize=text.fontSizeMax=size;text.fontSizeMin=12;text.enableAutoSizing=true;text.alignment=alignment;text.raycastTarget=false;styles.Apply(text,treatment);return text;
        }
        Button MakeButton(string name,Transform parent,Rect bounds,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(name,parent,art.tankCostButton);Fit(rect,bounds);var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.onClick.AddListener(action);
            var caption=Label("Caption",rect,"",new Rect(.04f,.04f,.92f,.92f),34,ArcadeTextTreatment.PrizeAmount);
            rect.gameObject.AddComponent<ReferralActionVisual>().Configure(art.tankCostButton,art.tankCostButtonSelected,art.tankCostButtonLocked,caption,styles);return button;
        }
        static void Fit(RectTransform rect,Rect b){rect.pivot=new Vector2(.5f,.5f);rect.anchorMin=new Vector2(b.x,1-b.y-b.height);rect.anchorMax=new Vector2(b.x+b.width,1-b.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void OnDestroy(){styles.Dispose();}
    }

    [ExecuteAlways]
    public sealed class ReferralInputFrame : MonoBehaviour
    {
        void OnEnable()=>FitSkin();
        void OnRectTransformDimensionsChange()=>FitSkin();
        void FitSkin()
        {
            var image=GetComponent<Image>();
            if(image&&image.sprite&&image.sprite.border.x>0&&image.rectTransform.rect.height>0)
                TvTitleHeaderLayout.FitSkin(image,image.rectTransform.rect.height,.18f);
        }
    }

    public sealed class ReferralActionVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        Sprite normal,selected,disabled;TMP_Text caption;ArcadeTextStyles styles;Button button;bool focused,hovered;
        public void Configure(Sprite normalSprite,Sprite selectedSprite,Sprite disabledSprite,TMP_Text text,ArcadeTextStyles textStyles)
        {normal=normalSprite;selected=selectedSprite;disabled=disabledSprite;caption=text;styles=textStyles;button=GetComponent<Button>();Refresh();}
        public void Refresh()
        {
            if(!button)return;
            bool active=button.interactable&&(focused||hovered);
            button.image.sprite=!button.interactable&&disabled?disabled:active?selected:normal;
            button.image.color=Color.white;
            FitSkin();
            styles.ApplyCleanButton(caption,active);
        }
        void FitSkin()
        {
            if(!button||!button.image||!button.image.sprite)return;
            float height=button.image.rectTransform.rect.height;
            if(height>0)TvTitleHeaderLayout.FitSkin(button.image,height);
        }
        void OnRectTransformDimensionsChange(){FitSkin();}
        public void OnSelect(BaseEventData e){focused=true;Refresh();}
        public void OnDeselect(BaseEventData e){focused=false;Refresh();}
        public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData e){hovered=false;Refresh();}
        void OnDisable(){focused=hovered=false;}
    }
}
