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
    /// <summary>Quarters, its available subpages, and community tasks inside the shared TV.</summary>
    public sealed partial class OperationsScreen : MonoBehaviour
    {
        public enum Page { Quarters, Treasury, Manual, ManualDetail, Socials }
        [SerializeField] Page page;
        [SerializeField] Page detailOrigin=Page.Manual;
        [SerializeField] int category;
        [SerializeField] bool history;
        [SerializeField] string entryId,activeCard;
        MainMenuScene menu;MenuTheme theme;PreBattleArt art;ShopArt shopArt;OperationsArt illustrations;MainMenuApiClient api;
        RectTransform root,header,tabBar,footer,body,intro,summary,detail,empty;
        Image titleIcon,emptyIcon,detailIcon;
        TMP_Text title,introTitle,introDescription,status,emptyTitle,emptyDescription,detailTitle,detailRole,detailLore,detailEffect,detailSource,footerCaption;
        Button footerAction;
        TankRosterScroll scroll;GridLayoutGroup grid;
        readonly List<Button> tabs=new List<Button>();
        readonly List<Image> tabSelections=new List<Image>();
        readonly List<TMP_Text> tabCaptions=new List<TMP_Text>();
        readonly List<Card> cards=new List<Card>();
        readonly List<Button> ledgerRows=new List<Button>();
        readonly List<Item> items=new List<Item>();
        readonly List<Stat> stats=new List<Stat>();
        readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        FieldManualEntry[] manual=Array.Empty<FieldManualEntry>();
        Coroutine request;int version,columns=3,focused=-1,hovered=-1;
        bool configured,loading;string statusMessage="SELECT A SECTION TO CONTINUE",playerIdentity;
        MainMenuPlatform platform;
        static readonly string[] Categories={"tanks","weapons","powerups","enemies"};
        static readonly string[] CategoryLabels={"TANKS","WEAPONS","POWERUPS","ENEMIES"};
        sealed class Item { public string Key,Title,Detail,Action;public Sprite Icon;public bool Locked;public UnityEngine.Events.UnityAction Click; }
        sealed class Card { public RectTransform Rect;public Button Button;public Image Frame,Icon,Action;public TMP_Text Title,Description,Caption;public CardSelectionHighlight Highlight; }
        sealed class Stat { public RectTransform Rect;public TMP_Text Value; }
        public bool IsConfigured=>configured&&root&&menu;
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public bool IsSocials=>page==Page.Socials;
        public Page CurrentPage=>page;
        public RectTransform Root=>root;
        public TankRosterScroll Scroll=>scroll;
        public int ItemCount=>items.Count;
        public bool IsLoading=>loading;
        public string Status=>statusMessage;
        public void Configure(MainMenuScene owner,MenuTheme skin,MainMenuApiClient client,RectTransform frame)
        {
            CancelRequest();configured=false;if(api)api.PlayerLoaded-=OnPlayer;
            menu=owner;theme=skin;api=client;art=Resources.Load<PreBattleArt>("PreBattleArt");shopArt=Resources.Load<ShopArt>("ShopArt");illustrations=Resources.Load<OperationsArt>("OperationsArt");
            if(illustrations&&illustrations.manual)manual=JsonUtility.FromJson<FieldManualEntries>(illustrations.manual.text).entries;
            if(api)api.PlayerLoaded+=OnPlayer;
            var old=frame.Find("Operations screen");bool open=old&&old.gameObject.activeSelf;
            root=Panel("Operations screen",frame,null);root.GetComponent<Image>().raycastTarget=true;
            BuildView();configured=true;Render();root.gameObject.SetActive(open);
        }
        public void Open(bool socials=false)
        {
            if(!IsConfigured)return;
            HideLinkInstructions(false);
            page=socials?Page.Socials:Page.Quarters;history=false;category=0;activeCard=null;
            statusMessage=socials?"CHECKING SOCIAL TASKS...":"SELECT A SECTION TO CONTINUE";
            root.gameObject.SetActive(true);root.SetAsLastSibling();menu.SetHeroVisible(false);menu.SetTankSelectorBackdrop(true);
            menu.RefreshLayout();Render();ResetScroll();FocusFirst();if(socials)Refresh();
        }
        public void Resume(){if(IsOpen){Render();if(page==Page.Socials||page==Page.Treasury)Refresh();}}
        public void Close(bool restoreFocus=true)
        {
            HideLinkInstructions(false);
            CancelRequest();bool socials=IsSocials;root.gameObject.SetActive(false);menu.SetTankSelectorBackdrop(false);menu.RefreshLayout();
            if(restoreFocus)Focus(menu.Tabs[socials?4:3]);
        }
        public void Back()
        {
            if(IsLinkNoticeOpen){HideLinkInstructions();return;}
            if(page==Page.ManualDetail){Navigate(detailOrigin);if(detailOrigin==Page.Treasury)Refresh();return;}
            if(page==Page.Treasury||page==Page.Manual){Navigate(Page.Quarters);return;}
            Close();
        }
        public void OpenTreasury(){history=false;Navigate(Page.Treasury);Refresh();}
        public void OpenManual(){category=0;Navigate(Page.Manual);}
        public void SelectManualCategory(int index){category=Mathf.Clamp(index,0,3);activeCard=null;Navigate(Page.Manual);}
        public void SelectTreasuryHistory(bool value){history=value;Render();ResetScroll();FocusFirst();}
        public void OpenManualEntry(string slug)
        {
            var entry=Array.Find(manual,e=>e.slug==slug);
            if(entry==null){SetStatus("NO FIELD MANUAL ENTRY AVAILABLE");return;}
            detailOrigin=page==Page.Treasury?Page.Treasury:Page.Manual;
            category=Mathf.Max(0,Array.IndexOf(Categories,entry.category));entryId=slug;Navigate(Page.ManualDetail);
        }
        void Navigate(Page target)
        {
            if(target==Page.Quarters)activeCard=page==Page.Treasury?"quarter-0":"quarter-1";
            else if(target==Page.Manual&&page==Page.ManualDetail)activeCard=entryId;
            CancelRequest();page=target;focused=hovered=-1;
            statusMessage=page==Page.Quarters?"SELECT A SECTION TO CONTINUE":page==Page.Manual?"SELECT AN ENTRY TO VIEW FIELD INTELLIGENCE":page==Page.ManualDetail?"FIELD MANUAL • B / BACK TO RETURN":"BALANCES, ITEMS & TRANSACTION HISTORY";
            Render();menu.RefreshLayout();ResetScroll();FocusFirst();
        }
        void CancelRequest(){version++;if(request!=null)StopCoroutine(request);request=null;loading=false;}
        void ResetScroll(){Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.verticalNormalizedPosition=1;}
        void FocusFirst(){int selected=items.FindIndex(item=>item.Key==activeCard);Focus(cards.Count>0&&items.Count>0?cards[Mathf.Max(0,selected)].Button:tabs.Count>0?tabs[0]:footerAction);}
        static void Focus(Selectable button){if(button&&EventSystem.current)EventSystem.current.SetSelectedGameObject(button.gameObject);}
        public void KeepControllerFocus()=>Psg1UiNavigation.KeepFocus(IsLinkNoticeOpen?linkNotice:root,IsLinkNoticeOpen?linkOpen:items.Count>0?cards[0].Button:tabs.Count>0?tabs[0]:footerAction);
        public void FocusCard(int index,bool value,bool pointer)
        {
            if(index<0||index>=items.Count)return;
            if(pointer){if(value)hovered=index;else if(hovered==index)hovered=-1;}
            else{if(value)focused=index;else if(focused==index)focused=-1;}
            if(value)scroll.Reveal(cards[index].Rect);RefreshCards();
        }
        void Activate(int index)
        {
            if(index<0||index>=items.Count)return;
            var item=items[index];
            if(loading&&page==Page.Socials&&item.Key!="website"&&item.Key!="instagram")return;
            if(item.Locked){SetStatus(item.Title+" • NOT AVAILABLE YET");return;}
            activeCard=item.Key;RefreshCards();item.Click?.Invoke();
        }
        void SetStatus(string text){statusMessage=text;if(status)status.text=text;}
        void OnPlayer(MainMenuApiClient.PlayerSnapshot player)
        {
            string identity=player.walletAddress??player.id;
            // Regular refreshes for the same player must preserve tasks awaiting verification.
            bool changed=identity!=playerIdentity;
            if(changed)
            {playerIdentity=identity;account=null;xStatus=null;discord=null;ledger=null;treasuryAuthenticated=xAuthenticated=discordAuthenticated=false;xReady=false;repostReady=commentReady=null;}
            if(IsOpen&&(page==Page.Treasury||page==Page.Socials)&&(changed||!loading))Refresh();
        }
        void OnApplicationFocus(bool value){if(value&&IsOpen&&page==Page.Socials&&!loading)Refresh();}
        void Update()
        {
            if(IsLinkNoticeOpen){StyleLinkAction(linkOpen,linkOpenCaption);StyleLinkAction(linkDone,linkDoneCaption);}
            if(!IsOpen||!footerAction||!footerAction.gameObject.activeSelf)return;
            var sprite=footerAction.image.overrideSprite?footerAction.image.overrideSprite:footerAction.image.sprite;
            styles.ApplyCleanButton(footerCaption,sprite==art.tankCostButtonSelected);
        }
        void OnDestroy()
        {
            if(api)api.PlayerLoaded-=OnPlayer;styles.Dispose();
            if(root){if(Application.isPlaying)Destroy(root.gameObject);else DestroyImmediate(root.gameObject);}
        }
    }
}
