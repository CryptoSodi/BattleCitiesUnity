using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        [Header("Scene Editing (like Login)")]
        [Tooltip("Edit child RectTransforms and Text components directly. Only the outer content is fitted to the screen. Each platform remembers its own arrangement.")]
        [SerializeField] private bool editLayoutInScene = true;
        [SerializeField, HideInInspector] private MainMenuPlatform authoredPlatform;
        [SerializeField, HideInInspector] private List<AuthoredMenuLayout> authoredLayouts = new List<AuthoredMenuLayout>();
        private bool generatingLayout;

        [Serializable]
        private sealed class AuthoredMenuLayout
        {
            public MainMenuPlatform platform;
            public Vector2 designSize;
            public List<AuthoredElement> elements = new List<AuthoredElement>();
        }

        [Serializable]
        private sealed class AuthoredElement
        {
            public RectTransform rect;
            public Vector2 min, max, pivot, position, size;
            public Vector3 scale;
            public Quaternion rotation;
            public bool active;
            public UnityEngine.UI.Text text;
            public int fontSize, minFont, maxFont;
            public bool bestFit;
            public TextAnchor alignment;
            public FontStyle style;

            public AuthoredElement(RectTransform value)
            {
                rect=value; min=value.anchorMin; max=value.anchorMax; pivot=value.pivot;
                position=value.anchoredPosition; size=value.sizeDelta;
                scale=value.localScale; rotation=value.localRotation; active=value.gameObject.activeSelf;
                text=value.GetComponent<UnityEngine.UI.Text>();
                if(text)
                {
                    fontSize=text.fontSize; minFont=text.resizeTextMinSize; maxFont=text.resizeTextMaxSize;
                    bestFit=text.resizeTextForBestFit; alignment=text.alignment; style=text.fontStyle;
                }
            }

            public void Restore()
            {
                if(!rect)return;
                rect.anchorMin=min; rect.anchorMax=max; rect.pivot=pivot;
                rect.anchoredPosition=position; rect.sizeDelta=size;
                rect.localScale=scale; rect.localRotation=rotation;
                rect.gameObject.SetActive(active);
                if(text)
                {
                    text.fontSize=fontSize; text.resizeTextMinSize=minFont; text.resizeTextMaxSize=maxFont;
                    text.resizeTextForBestFit=bestFit; text.alignment=alignment; text.fontStyle=style;
                }
            }
        }

        // Invoked before scene saves and platform changes. Never capture transient Play Mode UI.
        public void SaveAuthoredLayout()
        {
            if(Application.isPlaying || !editLayoutInScene || !content)return;
            if(authoredPlatform==MainMenuPlatform.Auto)authoredPlatform=Resolve(new Vector2(Screen.width,Screen.height));
            CaptureAuthoredLayout(authoredPlatform);
        }

        private AuthoredMenuLayout CaptureAuthoredLayout(MainMenuPlatform target)
        {
            var profile=authoredLayouts.Find(p=>p.platform==target);
            if(profile==null)
            {
                profile=new AuthoredMenuLayout { platform=target };
                authoredLayouts.Add(profile);
            }
            profile.designSize=content.rect.size;
            profile.elements.Clear();
            var battleScreen=mainFrame?mainFrame.Find("Pre-battle screens"):null;
            var shopScreen=mainFrame?mainFrame.Find("Shop screen"):null;
            foreach(var rect in content.GetComponentsInChildren<RectTransform>(true))
            {
                // Modal visibility and API-driven text contents belong to runtime behavior.
                if(rect==content || (modal && (rect==modal || rect.IsChildOf(modal))))continue;
                if(battleScreen && (rect==battleScreen || rect.IsChildOf(battleScreen)))continue;
                if(shopScreen && (rect==shopScreen || rect.IsChildOf(shopScreen)))continue;
                profile.elements.Add(new AuthoredElement(rect));
            }
            return profile;
        }

        private void ApplyAuthoredLayout(MainMenuPlatform target, Vector2 available)
        {
            if(available.x<=0 || available.y<=0)return;
            if(authoredPlatform==MainMenuPlatform.Auto)
            {
                authoredPlatform=target;
                CaptureAuthoredLayout(target);
            }
            bool switching=target!=authoredPlatform;
            if(switching && !Application.isPlaying)CaptureAuthoredLayout(authoredPlatform);
            var profile=authoredLayouts.Find(p=>p.platform==target);
            if(profile==null)
            {
                // Generate an initial arrangement once; subsequent visits restore the authored one.
                generatingLayout=true;
                try { ApplyLayout(target,GetLayout(target).referenceResolution); }
                finally { generatingLayout=false; }
                profile=CaptureAuthoredLayout(target);
            }
            else if(switching || target==MainMenuPlatform.Android)
            {
                // Pre-battle controls own their layout and visibility; old scene captures may include them.
                var battleScreen=mainFrame?mainFrame.Find("Pre-battle screens"):null;
                var shopScreen=mainFrame?mainFrame.Find("Shop screen"):null;
                foreach(var element in profile.elements)
                {
                    if(battleScreen && element.rect && (element.rect==battleScreen || element.rect.IsChildOf(battleScreen)))continue;
                    if(shopScreen && element.rect && (element.rect==shopScreen || element.rect.IsChildOf(shopScreen)))continue;
                    element.Restore();
                }
            }
            authoredPlatform=target;
            Vector2 design=profile.designSize;
            if(design.x<=0 || design.y<=0)design=GetLayout(target).referenceResolution;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,.5f);
            content.anchoredPosition=Vector2.zero;
            float scale=Mathf.Min(available.x/design.x,available.y/design.y);
            float extraPortraitHeight=target==MainMenuPlatform.Android && available.y>available.x
                ?Mathf.Max(0f,available.y/scale-design.y):0f;
            content.sizeDelta=new Vector2(design.x,design.y+extraPortraitHeight);
            content.localScale=Vector3.one*scale;
            if(target==MainMenuPlatform.Android)
                FitAndroidPortraitEdges(extraPortraitHeight);

            var viewport=mainFrame?mainFrame.Find("TV Background Viewport"):null;
            var background=viewport?viewport.Find("TV Background"):null;
            var image=background?background.GetComponent<UnityEngine.UI.Image>():null;
            if(image && theme)image.sprite=target==MainMenuPlatform.Psg1 && theme.PsgBattlefield?theme.PsgBattlefield:theme.Battlefield;

            if(Application.isPlaying && target==MainMenuPlatform.Psg1 && controls && navigation)
            {
                controls.gameObject.SetActive(!controllerLegendHidden);
            }
            ConfigureNavigation(target!=MainMenuPlatform.Web && target!=MainMenuPlatform.AndroidLandscape);
            if(Application.isPlaying && inputModule && (target!=lastPlatform || inputModule.actionsAsset!=liveInput))
                ConfigureInput(target==MainMenuPlatform.Psg1);
            // A platform profile may restore the home artwork while the tank selector is open.
            bool showHome= !IsModalOpen && !IsTvScreenOpen;
            lastPlatform=target;
            SetHeroVisible(showHome,false);
        }

        private void FitAndroidPortraitEdges(float extraHeight)
        {
            if(!statusBar || !navigation || !mainFrame)return;

            // Keep the HUD near the safe area's top edge and give its readouts
            // the width that was previously left between the three cards.
            const float outerCardHeight=108f;
            const float scoreCardHeight=124f;
            Place(statusBar,8f,24f,content.sizeDelta.x-16f,scoreCardHeight);
            float hudWidth=statusBar.sizeDelta.x;
            float gap=4f;
            float playerWidth=hudWidth*.35f;
            float scoreWidth=hudWidth*.30f;
            var commander=(RectTransform)statusBar.GetChild(0);
            var score=(RectTransform)statusBar.GetChild(1);
            var highScore=(RectTransform)statusBar.GetChild(2);
            float outerInset=(scoreCardHeight-outerCardHeight)*.5f;
            Place(commander,0f,outerInset,playerWidth,outerCardHeight);
            Place(score,playerWidth+gap,0f,scoreWidth,scoreCardHeight);
            Place(highScore,playerWidth+scoreWidth+gap*2f,outerInset,
                hudWidth-playerWidth-scoreWidth-gap*2f,outerCardHeight);
            // The reference artwork otherwise letterboxes inside these wider cards.
            commander.GetComponent<UnityEngine.UI.Image>().preserveAspect=false;
            score.GetComponent<UnityEngine.UI.Image>().preserveAspect=false;
            highScore.GetComponent<UnityEngine.UI.Image>().preserveAspect=false;
            if(extraHeight>0f)
            {
                navigation.anchoredPosition+=new Vector2(0f,-extraHeight);
                if(howItWorks)howItWorks.anchoredPosition+=new Vector2(0f,-extraHeight);
            }
            if(!howItWorks)return;
            // End the TV above the instruction panel, including on taller phones.
            float mainTop=-mainFrame.anchoredPosition.y;
            float legendTop=-howItWorks.anchoredPosition.y;
            float frameHeight=Mathf.Max(0f,(legendTop-12f-mainTop)/Mathf.Max(.01f,mainFrame.localScale.y));
            mainFrame.sizeDelta=new Vector2(mainFrame.sizeDelta.x,frameHeight);
            foreach(var name in new[]{"TV Frame","TV Background Viewport"})
            {
                var rect=mainFrame.Find(name) as RectTransform;
                if(rect)
                {
                    float inset=-rect.anchoredPosition.y;
                    rect.sizeDelta=new Vector2(rect.sizeDelta.x,Mathf.Max(0f,frameHeight-2f*inset));
                }
            }
        }

        private static void FitAndroidStatusCard(RectTransform card,bool commander)
        {
            // Fit the contents to the compact card, keeping clear of its frame and rivets.
            float width=card.rect.width,height=card.rect.height;
            var socket=card.Find("Icon tile") as RectTransform;
            if(socket)
            {
                float size=height*.66f;
                Place(socket,width*.06f,(height-size)*.5f,size,size);
                var icon=socket.Find("Icon") as RectTransform;
                if(icon)
                {
                    HudBounds(icon,.07f,.07f,.93f,.93f);
                    var image=icon.GetComponent<UnityEngine.UI.Image>();
                    if(image){image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;}
                }
            }
            var readout=card.Find("Readout");
            if(!readout)return;
            var label=readout.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
            var value=readout.Find("Value")?.GetComponent<UnityEngine.UI.Text>();
            if(label)
            {
                HudBounds(label.rectTransform,.31f,.52f,.92f,.88f);
                FitHudText(label,36,commander?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter);
            }
            if(commander)
            {
                var progress=readout.Find("Progress") as RectTransform;
                var level=readout.Find("Level Pill") as RectTransform;
                if(progress)HudBounds(progress,.31f,.21f,.71f,.42f);
                if(level)HudBounds(level,.735f,.17f,.925f,.46f);
                if(value)
                {
                    HudBounds(value.rectTransform,.748f,.19f,.912f,.44f);
                    FitHudText(value,28,TextAnchor.MiddleCenter);
                }
            }
            else if(value)
            {
                HudBounds(value.rectTransform,.31f,.14f,.92f,.54f);
                FitHudText(value,44,TextAnchor.MiddleCenter);
            }
        }

        private static void HudBounds(RectTransform rect,float left,float bottom,float right,float top)
        {
            rect.anchorMin=new Vector2(left,bottom);rect.anchorMax=new Vector2(right,top);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }

        private static void FitHudText(UnityEngine.UI.Text text,int maximum,TextAnchor alignment)
        {
            text.fontSize=text.resizeTextMaxSize=maximum;
            text.resizeTextMinSize=16;text.resizeTextForBestFit=true;
            text.alignment=alignment;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.verticalOverflow=VerticalWrapMode.Truncate;
        }
    }
}
