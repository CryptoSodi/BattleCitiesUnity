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
    /// <summary>Gaming cycles and concurrent monthly seasons inside the shared TV.</summary>
    public sealed partial class RankingScreen : MonoBehaviour
    {
        MainMenuScene menu;MenuTheme theme;PreBattleArt art;MainMenuApiClient api;
        RectTransform root,header,tabsBar,footer,history,historyPaper,historyHeading,table,tableHeading;
        RectTransform empty,historyEmpty,eligibility,pickerOverlay,pickerPanel;
        TMP_Text rankValue,countdown,periodCaption,tableTitle,emptyTitle,emptyDetail,historyPeriod,eligibilityText;
        UnityEngine.UI.Button picker,back;
        readonly UnityEngine.UI.Button[] tabs=new UnityEngine.UI.Button[3];
        readonly UnityEngine.UI.Image[] selectedTabs=new UnityEngine.UI.Image[3];
        readonly TMP_Text[] tabLabels=new TMP_Text[3];
        readonly List<UnityEngine.UI.Button> rowButtons=new List<UnityEngine.UI.Button>();
        readonly List<UnityEngine.UI.Button> periodButtons=new List<UnityEngine.UI.Button>();
        readonly List<RankingPeriod> seasons=new List<RankingPeriod>();
        readonly List<RankingPeriod> options=new List<RankingPeriod>();
        readonly ArcadeTextStyles textStyles=new ArcadeTextStyles();
        TankRosterScroll tableScroll,periodScroll;
        RankingPageData snapshot;Coroutine request;
        RankingPeriod selectedPeriod=new RankingPeriod{Id="cycle",Label="CURRENT CYCLE",IsCycle=true};
        Material historyMaterial;
        bool trading,loading;string error,currentSeasonId;
        int generation;float refreshAt,nextClock;
        [NonSerialized] bool configured;
        public bool IsConfigured=>configured&&root&&menu;
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public bool IsPickerOpen=>pickerOverlay&&pickerOverlay.gameObject.activeSelf;
        public RectTransform Root=>root;
        public TankRosterScroll TableScroll=>tableScroll;
        public IReadOnlyList<RankingPeriod> Periods=>options;
        public string SelectedPeriodId=>selectedPeriod.Id;
        public bool IsTrading=>trading;
        public bool IsLoading=>loading;
        public string LoadError=>error;
        public RankingPageData Data=>snapshot;
        public void Present(RankingPageData value){snapshot=value;loading=false;Render();}

        public void Configure(MainMenuScene owner,MenuTheme skin,MainMenuApiClient client,RectTransform frame)
        {
            configured=false;
            if(api)api.PlayerLoaded-=OnPlayer;
            menu=owner;theme=skin;api=client;art=Resources.Load<PreBattleArt>("PreBattleArt");
            if(api)api.PlayerLoaded+=OnPlayer;
            var existing=frame.Find("Ranking screen");bool wasOpen=existing&&existing.gameObject.activeSelf;
            root=Panel("Ranking screen",frame,null);root.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            BuildView();RebuildOptions();Render();root.gameObject.SetActive(wasOpen);configured=true;
        }
        public void Open(bool refresh=true)
        {
            if(!IsConfigured)return;
            trading=false;selectedPeriod=new RankingPeriod{Id="cycle",Label="CURRENT CYCLE",IsCycle=true};snapshot=null;
            root.gameObject.SetActive(true);root.SetAsLastSibling();ClosePicker(false);
            menu.SetHeroVisible(false);menu.SetTankSelectorBackdrop(true);menu.RefreshLayout();
            refreshAt=float.PositiveInfinity;RebuildOptions();Render();Focus(tabs[0]);if(refresh)RefreshData();
        }
        public void Close(bool restoreFocus=true)
        {
            generation++;if(request!=null)StopCoroutine(request);request=null;loading=false;
            ClosePicker(false);root.gameObject.SetActive(false);
            menu.SetTankSelectorBackdrop(false);menu.RefreshLayout();
            if(restoreFocus)Focus(menu.Tabs[2]);
        }
        public void Back(){if(IsPickerOpen)ClosePicker();else Close();}
        public void SelectScope(bool value)
        {
            if(value||trading==value)return;
            trading=value;ClosePicker(false);
            selectedPeriod=value?(seasons.Find(p=>p.Id==currentSeasonId)??new RankingPeriod{Id="",Label="CURRENT SEASON"})
                :new RankingPeriod{Id="cycle",Label="CURRENT CYCLE",IsCycle=true};
            snapshot=null;RebuildOptions();RefreshData();
        }
        public void SelectPeriod(string id)
        {
            var period=options.Find(p=>p.Id==id);if(period==null)return;
            selectedPeriod=period;snapshot=null;ClosePicker();RefreshData();
        }
        public void RefreshData()
        {
            if(!IsOpen||!api)return;
            generation++;if(request!=null)StopCoroutine(request);
            request=StartCoroutine(LoadData(generation));
        }
        IEnumerator LoadData(int version)
        {
            loading=true;error=null;RebuildOptions();Render();
            JObject seasonResponse=null;string seasonError=null;
            string path="/api/rankings?scope="+(trading?"trading":"gaming");
            if(!selectedPeriod.IsCycle&&!string.IsNullOrEmpty(selectedPeriod.Id))path+="&seasonId="+Uri.EscapeDataString(selectedPeriod.Id);
            yield return api.Request("GET",path,null,(code,body,message)=>
            {if(code>=200&&code<300&&body?["rows"] is JArray)seasonResponse=body;else seasonError=message??"Standings could not be loaded.";});
            if(version!=generation)yield break;
            if(seasonResponse!=null)
            {
                seasons.Clear();seasons.AddRange(RankingPageData.Seasons(seasonResponse));
                currentSeasonId=(string)seasonResponse["currentSeason"]?["id"];
                if(!selectedPeriod.IsCycle)
                {
                    string id=string.IsNullOrEmpty(selectedPeriod.Id)?(string)seasonResponse["seasonId"]:selectedPeriod.Id;
                    selectedPeriod=seasons.Find(p=>p.Id==id)??selectedPeriod;
                }
            }
            RebuildOptions();
            if(selectedPeriod.IsCycle)
            {
                JObject cycleResponse=null;
                yield return api.Request("GET","/api/leaderboard/rewards",null,(code,body,message)=>
                {if(code>=200&&code<300&&body?["rows"] is JArray)cycleResponse=body;else error=message??"The live cycle could not be loaded.";});
                if(version!=generation)yield break;
                snapshot=cycleResponse!=null?RankingPageData.Parse(cycleResponse,selectedPeriod):null;
            }
            else {snapshot=seasonResponse!=null?RankingPageData.Parse(seasonResponse,selectedPeriod):null;error=seasonError;}
            request=null;refreshAt=Time.unscaledTime+30f;Present(snapshot);
        }
        void RebuildOptions()
        {
            options.Clear();
            if(!trading)options.Add(new RankingPeriod{Id="cycle",Label="CURRENT CYCLE",IsCycle=true});
            if(!trading)options.Add(new RankingPeriod{Id="all",Label="ALL TIME"});
            options.AddRange(seasons);BuildPeriodRows();
        }
        void OnPlayer(MainMenuApiClient.PlayerSnapshot player){if(IsOpen)RefreshData();}
        void Update()
        {
            if(!IsConfigured||!IsOpen)return;
            if(Time.unscaledTime>=nextClock){nextClock=Time.unscaledTime+.5f;RefreshFooter();}
            if(!loading&&!IsPickerOpen&&Time.unscaledTime>=refreshAt)RefreshData();
            var sprite=picker.image.overrideSprite?picker.image.overrideSprite:picker.image.sprite;
            textStyles.ApplyCleanButton(periodCaption,sprite==art.tankCostButtonSelected);
            foreach(var button in periodButtons)
            {
                bool selected=button.transform.Find("Selection").GetComponent<UnityEngine.UI.Image>().enabled;
                button.GetComponentInChildren<TMP_Text>().color=selected?ArcadeTextStyles.PrizeAmountFace:Color.white;
            }
        }
        void Render()
        {
            if(!root)return;
            for(int i=0;i<2;i++)
            {bool active=i==(trading?1:0);selectedTabs[i].enabled=active;textStyles.ApplyCleanButton(tabLabels[i],active);}
            periodCaption.text=selectedPeriod.Label;
            tableTitle.text=selectedPeriod.IsCycle?"LIVE PLAYER RANKINGS":(trading?"TRADING":"GAMING")+" • "+selectedPeriod.Label;
            historyPeriod.text=selectedPeriod.Label;
            eligibilityText.text=trading?"TRADING STANDINGS\nMONTHLY SEASONS":selectedPeriod.IsAllTime?"ALL RECORDED MATCHES\nNO SEASON PASS REQUIRED":selectedPeriod.IsCycle?"CYCLE: ALL PLAYERS\nSEASON: PASS HOLDERS":"SEASON PASS SCORES\nALSO EARN IN THE CYCLE";
            BuildRankingRows();
            bool noRows=snapshot==null||snapshot.Rows.Count==0;empty.gameObject.SetActive(noRows);
            emptyTitle.text=loading?"LOADING RANKINGS":error!=null?"RANKINGS UNAVAILABLE":trading?"NO TRADERS RANKED YET":"NO PLAYERS RANKED YET";
            emptyDetail.text=loading?"Fetching the selected standings…":error!=null?"Choose this period again to retry.":trading?"Trading standings will appear here.":selectedPeriod.IsAllTime?"Play a ranked match to join the standings.":selectedPeriod.IsCycle?"Play a ranked match to join this cycle.":"Season pass holders build their score here.";
            RefreshFooter();ConfigureNavigation();
        }
        void RefreshFooter()
        {
            if(!rankValue)return;
            rankValue.text=snapshot==null?"—":snapshot.MyRank is int rank?"#"+rank.ToString(CultureInfo.InvariantCulture):"UNRANKED";
            if(selectedPeriod.IsAllTime){countdown.text="ALL TIME • ALL RECORDED MATCHES";return;}
            string rule=selectedPeriod.IsCycle?"TOP 10 EVERY "+(snapshot?.IntervalMinutes??30)+" MINUTES":"MONTHLY SEASON PAYOUT";
            string status;
            if(snapshot?.EndsAt==null)status=loading?"LOADING…":"TIME UNAVAILABLE";
            else
            {
                var remaining=snapshot.EndsAt.Value-DateTimeOffset.UtcNow;
                if(remaining.TotalSeconds<=0)status=selectedPeriod.IsCycle?"NEXT CYCLE…":"SEASON ENDED";
                else
                {
                    string time=remaining.TotalDays>=1?((int)remaining.TotalDays)+"D "+remaining.ToString(@"hh\:mm\:ss"):
                        ((int)remaining.TotalHours).ToString("00")+":"+remaining.ToString(@"mm\:ss");
                    status=time+(selectedPeriod.IsCycle&&!snapshot.PayoutsEnabled?" • PAYOUTS PAUSED":"");
                }
            }
            countdown.text=rule+" • "+status;
        }
        public void TogglePicker()
        {
            if(IsPickerOpen){ClosePicker();return;}
            pickerOverlay.gameObject.SetActive(true);pickerOverlay.SetAsLastSibling();LayoutPicker();
            ConfigureNavigation();var index=options.FindIndex(p=>p.Id==selectedPeriod.Id);
            Focus(periodButtons.Count>0?periodButtons[Mathf.Clamp(index,0,periodButtons.Count-1)]:picker);
        }
        void ClosePicker(bool restoreFocus=true)
        {if(pickerOverlay)pickerOverlay.gameObject.SetActive(false);ConfigureNavigation();if(restoreFocus)Focus(picker);}
        public void KeepControllerFocus()=>Psg1UiNavigation.KeepFocus(IsPickerOpen?pickerPanel:root,
            IsPickerOpen&&periodButtons.Count>0?periodButtons[0]:tabs[trading?1:0]);
        void ConfigureNavigation()
        {
            if(!back||!picker)return;
            if(IsPickerOpen&&periodButtons.Count>0)
            {
                for(int i=0;i<periodButtons.Count;i++)Link(periodButtons[i],periodButtons[i],periodButtons[i],
                    periodButtons[Math.Max(0,i-1)],periodButtons[Math.Min(periodButtons.Count-1,i+1)]);
                return;
            }
            Link(tabs[0],back,back,picker,rowButtons.Count>0?rowButtons[0]:picker);
            Link(back,tabs[0],tabs[0],picker,rowButtons.Count>0?rowButtons[0]:picker);
            tabs[1].navigation=new Navigation{mode=Navigation.Mode.None};
            for(int i=0;i<rowButtons.Count;i++)Link(rowButtons[i],tabs[trading?1:0],picker,
                i>0?rowButtons[i-1]:tabs[trading?1:0],i+1<rowButtons.Count?rowButtons[i+1]:picker);
            Link(picker,tabs[0],back,rowButtons.Count>0?rowButtons[rowButtons.Count-1]:tabs[trading?1:0],tabs[trading?1:0]);
        }
        static void Link(Selectable target,Selectable left,Selectable right,Selectable up,Selectable down)
        {target.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=left,selectOnRight=right,selectOnUp=up,selectOnDown=down};}
        static void Focus(Selectable control){if(control&&EventSystem.current)EventSystem.current.SetSelectedGameObject(control.gameObject);}
        void OnDestroy()
        {
            if(api)api.PlayerLoaded-=OnPlayer;textStyles.Dispose();
            if(historyMaterial){if(Application.isPlaying)Destroy(historyMaterial);else DestroyImmediate(historyMaterial);}
            if(root){if(Application.isPlaying)Destroy(root.gameObject);else DestroyImmediate(root.gameObject);}
        }
    }
}
