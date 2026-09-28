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
            foreach(var rect in content.GetComponentsInChildren<RectTransform>(true))
            {
                // Modal visibility and API-driven text contents belong to runtime behavior.
                if(rect==content || (modal && (rect==modal || rect.IsChildOf(modal))))continue;
                if(battleScreen && (rect==battleScreen || rect.IsChildOf(battleScreen)))continue;
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
            else if(switching)
            {
                // Pre-battle controls own their layout and visibility; old scene captures may include them.
                var battleScreen=mainFrame?mainFrame.Find("Pre-battle screens"):null;
                foreach(var element in profile.elements)
                {
                    if(battleScreen && element.rect && (element.rect==battleScreen || element.rect.IsChildOf(battleScreen)))continue;
                    element.Restore();
                }
            }
            authoredPlatform=target;
            Vector2 design=profile.designSize;
            if(design.x<=0 || design.y<=0)design=GetLayout(target).referenceResolution;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,.5f);
            content.anchoredPosition=Vector2.zero;
            content.sizeDelta=design;
            content.localScale=Vector3.one*Mathf.Min(available.x/design.x,available.y/design.y);

            var viewport=mainFrame?mainFrame.Find("TV Background Viewport"):null;
            var background=viewport?viewport.Find("TV Background"):null;
            var image=background?background.GetComponent<UnityEngine.UI.Image>():null;
            if(image && theme)image.sprite=target==MainMenuPlatform.Psg1 && theme.PsgBattlefield?theme.PsgBattlefield:theme.Battlefield;

            if(Application.isPlaying && target==MainMenuPlatform.Psg1 && controls && navigation)
            {
                controls.gameObject.SetActive(!controllerLegendHidden);
            }
            ConfigureNavigation(target!=MainMenuPlatform.Web,target==MainMenuPlatform.Psg1);
            if(Application.isPlaying && inputModule && (target!=lastPlatform || inputModule.actionsAsset!=liveInput))
                ConfigureInput(target==MainMenuPlatform.Psg1);
            // A platform profile may restore the home artwork while the tank selector is open.
            var openBattleScreen=mainFrame?mainFrame.Find("Pre-battle screens"):null;
            if(openBattleScreen && openBattleScreen.gameObject.activeInHierarchy)
                SetHeroVisible(false);
            lastPlatform=target;
        }
    }
}
