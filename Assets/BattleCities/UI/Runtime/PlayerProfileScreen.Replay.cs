using System;
using System.Collections.Generic;
using BattleCities.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class PlayerProfileScreen
    {
        RectTransform replayView,replayHeader,replayNav,replayFooter,replayViewport;
        TMP_Text replayTitle,replayInfo;
        Button replayBack,replayPause,replayRestart,replaySpeed;
        RawImage replayImage;
        RenderTexture replayTexture;
        BattleGame replayGame;
        GameObject replayReturnFocus;
        float replayReturnScroll;
        readonly List<GameObject> replayHidden=new List<GameObject>();
        public bool ReplayViewerOpen=>replayView&&replayView.gameObject.activeSelf;
        public BattleGame ReplayGame=>replayGame;
        public RectTransform ReplayView=>replayView;

        // Per-match WATCH is the only player-facing entry to this viewer.
        public void ShowReplay(BattleReplay recording)
        {
            if(!IsOpen)throw new InvalidOperationException("Open the player's profile first.");
            ReplayJson.Validate(recording);CloseReplayViewer();ClearStatus();identityLabel.text=IdentityText();
            replayReturnFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(scroll){scroll.StopMovement();replayReturnScroll=scroll.verticalNormalizedPosition;}
            if(!replayView)BuildReplayViewer();
            foreach(Transform child in root)
                if(child!=replayView&&child.gameObject.activeSelf){replayHidden.Add(child.gameObject);child.gameObject.SetActive(false);}
            replayView.gameObject.SetActive(true);replayView.SetAsLastSibling();LayoutReplayViewer();
            try{replayGame=BattleGame.CreateTvReplay(recording,replayTexture);}
            catch{CloseReplayViewer();throw;}
            UpdateReplayStatus();Focus(replayPause);
        }
        public void CloseReplayViewer()
        {
            if(replayGame){replayGame.ReleaseTvReplay();replayGame=null;}
            if(replayImage)replayImage.texture=null;
            if(replayTexture){replayTexture.Release();Destroy(replayTexture);replayTexture=null;}
            if(replayView)replayView.gameObject.SetActive(false);
            foreach(var child in replayHidden)if(child)child.SetActive(true);
            bool restore=replayHidden.Count>0;replayHidden.Clear();
            if(restore)Canvas.ForceUpdateCanvases();
            if(restore&&EventSystem.current)
                EventSystem.current.SetSelectedGameObject(replayReturnFocus&&replayReturnFocus.activeInHierarchy?replayReturnFocus:back.gameObject);
            if(restore&&scroll){scroll.StopMovement();scroll.verticalNormalizedPosition=replayReturnScroll;}
            replayReturnFocus=null;
        }
        void BuildReplayViewer()
        {
            replayView=Panel("Match replay",root,theme.CreamPanel);Fit(replayView,new Rect(0,0,1,1));replayView.GetComponent<Image>().raycastTarget=true;
            replayHeader=Panel("Title plate",replayView,art.tankTitlePanel);replayHeader.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            var icon=Icon("Icon",replayHeader,pictures?pictures.matches:null,new Rect(0,0,1,1));
            replayTitle=Label("Title",replayHeader,"MATCH REPLAY",new Rect(0,0,1,1),28,ArcadeTextTreatment.Gold,TextAlignmentOptions.MidlineLeft);
            replayNav=Panel("Navigation",replayHeader,art.tankCostButton);replayNav.GetComponent<Image>().color=new Color(.08f,.63f,.68f);
            replayBack=Control("Back",replayNav,"BACK",CloseReplayViewer);
            // Match the profile's shared joined Back tab, including its triangular arrow.
            replayBack.image.sprite=null;replayBack.image.color=Color.clear;
            var focus=replayBack.transform.Find("Focus").GetComponent<Image>();focus.enabled=true;
            replayBack.GetComponent<SettingsControlVisual>().enabled=false;replayBack.targetGraphic=focus;replayBack.transition=Selectable.Transition.ColorTint;
            var colors=ColorBlock.defaultColorBlock;colors.normalColor=colors.disabledColor=Color.clear;colors.highlightedColor=colors.selectedColor=colors.pressedColor=Color.white;colors.fadeDuration=.08f;replayBack.colors=colors;
            var caption=replayBack.transform.Find("Caption").GetComponent<TMP_Text>();Fit(caption.rectTransform,new Rect(.29f,.025f,.68f,.95f));styles.ApplyCleanButton(caption,false);
            var arrow=new GameObject("Arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));arrow.layer=root.gameObject.layer;arrow.transform.SetParent(replayBack.transform,false);Fit((RectTransform)arrow.transform,new Rect(.10f,.17f,.14f,.66f));arrow.GetComponent<BackTabArrow>().raycastTarget=false;
            replayViewport=Panel("Viewport",replayView,theme.DarkPanel);replayViewport.GetComponent<Image>().color=new Color32(8,27,41,255);
            var image=new GameObject("Battle picture",typeof(RectTransform),typeof(RawImage));image.layer=root.gameObject.layer;image.transform.SetParent(replayViewport,false);replayImage=image.GetComponent<RawImage>();replayImage.raycastTarget=false;Fit(replayImage.rectTransform,new Rect(0,0,1,1));replayImage.rectTransform.offsetMin=Vector2.one*3;replayImage.rectTransform.offsetMax=Vector2.one*-3;
            replayFooter=Panel("Status",replayView,art.statusPanel);TvStatusFooterLayout.ApplySkin(replayFooter.GetComponent<Image>(),art.statusPanel);
            replayInfo=Label("Playback status",replayFooter,"",new Rect(.018f,.05f,.46f,.90f),23,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);replayInfo.overflowMode=TextOverflowModes.Ellipsis;
            replayPause=Control("Pause",replayFooter,"PAUSE",()=>{if(replayGame)replayGame.Paused=!replayGame.Paused;});Fit((RectTransform)replayPause.transform,new Rect(.50f,.11f,.15f,.78f));
            replaySpeed=Control("Speed",replayFooter,"1X",()=>{if(replayGame)replayGame.ReplaySpeed=replayGame.ReplaySpeed>=4?.5f:replayGame.ReplaySpeed*2;});Fit((RectTransform)replaySpeed.transform,new Rect(.665f,.11f,.085f,.78f));
            replayRestart=Control("Restart",replayFooter,"RESTART",()=>{if(replayGame)replayGame.RestartReplay();});TvStatusFooterLayout.PlaceAction((RectTransform)replayRestart.transform);
            Psg1UiNavigation.Rows(new Selectable[]{replayBack},new Selectable[]{replayPause,replaySpeed,replayRestart});
        }
        void LayoutReplayViewer()
        {
            var head=new TvTitleHeaderLayout(root);var foot=new TvStatusFooterLayout(root);
            head.PlaceTitle(replayHeader,replayHeader.Find("Icon") as RectTransform,replayTitle.rectTransform,true);head.PlaceNavigation(replayHeader,replayNav,true);
            MainMenuScene.Place((RectTransform)replayBack.transform,3,3,replayNav.rect.width-6,head.NavigationHeight-6);foot.Place(replayFooter);
            MainMenuScene.Place(replayViewport,4,head.Height+12,root.rect.width-8,Mathf.Max(1,root.rect.height-head.Height-foot.Height-24));
            float aspect=Mathf.Max(1,replayViewport.rect.width-6)/Mathf.Max(1,replayViewport.rect.height-6);
            int width=1280,height=Mathf.Clamp(Mathf.RoundToInt(width/aspect),256,1280);
            if(!replayTexture){replayTexture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Profile match TV",antiAliasing=1};replayTexture.Create();}
            else if(replayTexture.width!=width||replayTexture.height!=height){replayTexture.Release();replayTexture.width=width;replayTexture.height=height;replayTexture.Create();}
            replayImage.texture=replayTexture;
            if(replayGame&&replayGame.TvReplayCamera)replayGame.TvReplayCamera.aspect=(float)width/height;
        }
        void Update()
        {
            if(ReplayViewerOpen&&!IsOpen)CloseReplayViewer();
            if(ReplayViewerOpen)UpdateReplayStatus();
        }
        void UpdateReplayStatus()
        {
            if(!replayGame||replayGame.ReplayPlayback==null)return;
            var player=replayGame.ReplayPlayback;
            replayInfo.text=player.Error!=null?"REPLAY COULD NOT BE REPRODUCED":player.Complete?"REPLAY COMPLETE":(replayGame.Paused?"PAUSED  •  ":"")+TimeLabel(player.Simulation.Tick)+" / "+TimeLabel(player.Data.durationTicks);
            bool canPause=player.Error==null&&!player.Complete;
            if(replayPause.interactable!=canPause)
            {
                replayPause.interactable=canPause;
                replayPause.image.sprite=canPause?art.tankCostButton:art.tankCostButtonLocked;
                replayPause.image.color=canPause?new Color(.5f,.68f,.86f):Color.white;
                replayPause.GetComponent<SettingsControlVisual>().Refresh();
                Psg1UiNavigation.Rows(new Selectable[]{replayBack},new Selectable[]{replayPause,replaySpeed,replayRestart});
            }
            replayPause.transform.Find("Caption").GetComponent<TMP_Text>().text=replayGame.Paused?"RESUME":"PAUSE";
            replaySpeed.transform.Find("Caption").GetComponent<TMP_Text>().text=replayGame.ReplaySpeed.ToString("0.#")+"X";
        }
        static string TimeLabel(int ticks){int seconds=Mathf.FloorToInt(ticks*BattleSimulation.StepSeconds);return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
    }
}