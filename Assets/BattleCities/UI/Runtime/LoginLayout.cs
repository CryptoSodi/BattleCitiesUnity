using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    [Serializable]
    public struct LoginElementPlacement
    {
        public RectTransform target;
        public Vector2 position, size;
        public bool visible;
        public TextAnchor alignment;
        public int fontSize, minFontSize, maxFontSize;
        public bool bestFit;

        public static LoginElementPlacement Capture(RectTransform rect)
        {
            var text = rect.GetComponent<UnityEngine.UI.Text>();
            return new LoginElementPlacement
            {
                target = rect, position = rect.anchoredPosition, size = rect.sizeDelta,
                visible = rect.gameObject.activeSelf,
                alignment = text ? text.alignment : TextAnchor.MiddleCenter,
                fontSize = text ? text.fontSize : 0, bestFit = text && text.resizeTextForBestFit,
                minFontSize = text ? text.resizeTextMinSize : 0,
                maxFontSize = text ? text.resizeTextMaxSize : 0
            };
        }

        public void Apply()
        {
            if (!target) return;
            target.anchoredPosition = position;
            target.sizeDelta = size;
            target.gameObject.SetActive(visible);
            var text = target.GetComponent<UnityEngine.UI.Text>();
            if (!text) return;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = bestFit;
            text.resizeTextMinSize = minFontSize;
            text.resizeTextMaxSize = maxFontSize;
        }
    }

    /// <summary>Uses the same existing controls for web, portrait and landscape, with the authored PSG1 root.</summary>
    [ExecuteAlways]
    public sealed class LoginLayout : MonoBehaviour
    {
        [SerializeField] RectTransform designRoot;
        [SerializeField] Vector2 designSize = new Vector2(760, 1260);
        [SerializeField, Range(.8f, 1f)] float screenCoverage = .96f;
        [Header("Platform preview (Auto detects device and orientation)")]
        [SerializeField] MainMenuPlatform platform = MainMenuPlatform.Auto;
        [Header("Same controls, saved portrait and landscape positions")]
        [SerializeField] Vector2 landscapeDesignSize = new Vector2(1480, 840);
        [SerializeField, Range(.8f, 1f)] float landscapeScreenCoverage = .94f;
        [SerializeField] LoginElementPlacement[] portraitElements = Array.Empty<LoginElementPlacement>();
        [SerializeField] LoginElementPlacement[] landscapeElements = Array.Empty<LoginElementPlacement>();
        [SerializeField] bool landscapeUsesBattlefield;
        [Header("Web battlefield composition")]
        [SerializeField] Vector2 webDesignSize = new Vector2(1600, 900);
        [SerializeField, Range(.8f, 1f)] float webScreenCoverage = .98f;
        [SerializeField] LoginElementPlacement[] webElements = Array.Empty<LoginElementPlacement>();
        [SerializeField] RectTransform webBackdrop;
        [SerializeField] RectTransform originalBackdrop, originalShade;
        [SerializeField] UnityEngine.UI.Text webHeading;
        [SerializeField] Color headingRestingColor;
        [SerializeField] FontStyle headingRestingStyle;
        [Header("PSG1 only: child RectTransforms remain the layout source of truth")]
        [SerializeField] RectTransform psg1Root;
        [SerializeField] RectTransform psg1Backdrop;
        [SerializeField] Vector2 psg1DesignSize = new Vector2(1240, 1080);
        [SerializeField, Range(.8f, 1f)] float psg1ScreenCoverage = .98f;

        [SerializeField] LoginElementPlacement[] mobilePortraitElements = Array.Empty<LoginElementPlacement>();
        bool? lastGuestAllowed;
        int lastMode = -1;
        bool fitting;
        Vector2 lastSize;
        public MainMenuPlatform PreviewPlatform => platform;
        public bool AllowsGuestLogin => platform == MainMenuPlatform.Web ||
            (platform == MainMenuPlatform.Auto && !RuntimePlatformInfo.IsAndroid && !UsesPsg1);
        public void ConfigureMobilePortrait(LoginElementPlacement[] placements)
        { mobilePortraitElements = placements; lastMode = -1; Fit(); }
        public bool UsesPsg1 => platform == MainMenuPlatform.Psg1 ||
            (platform == MainMenuPlatform.Auto && RuntimePlatformInfo.IsPsg1);
        public bool UsesLandscape => !UsesPsg1 && landscapeElements != null && landscapeElements.Length > 0 &&
            (platform == MainMenuPlatform.Web || platform == MainMenuPlatform.AndroidLandscape ||
             (platform == MainMenuPlatform.Auto && WideViewport));
        public bool UsesWeb => !UsesPsg1 && webElements != null && webElements.Length > 0 &&
            (platform == MainMenuPlatform.Web ||
             (platform == MainMenuPlatform.Auto && !RuntimePlatformInfo.IsAndroid && WideViewport));
        bool WideViewport => ((RectTransform)transform).rect.width > ((RectTransform)transform).rect.height;
        int Mode => UsesPsg1 && psg1Root ? 2 : UsesWeb ? 3 : UsesLandscape ? 1 : 0;

        public void ConfigurePsg1(RectTransform root) { psg1Root = root; Fit(); }
        public void ConfigurePsg1Battlefield(RectTransform backdrop)
        { psg1Backdrop = backdrop; lastMode = -1; Fit(); }
        public void ConfigureLandscape(LoginElementPlacement[] portrait, LoginElementPlacement[] landscape)
        { portraitElements = portrait; landscapeElements = landscape; landscapeUsesBattlefield = false; lastMode = -1; Fit(); }
        public void ConfigureSeekerLandscape(LoginElementPlacement[] placements)
        {
            landscapeElements = placements;
            landscapeDesignSize = new Vector2(1800, 810);
            landscapeScreenCoverage = .98f;
            landscapeUsesBattlefield = true;
            portraitElements = WithHiddenDecorations(portraitElements, placements);
            webElements = WithHiddenDecorations(webElements, placements);
            lastMode = -1;
            Fit();
        }
        public void ConfigureWeb(LoginElementPlacement[] placements, RectTransform backdrop,
            RectTransform previousBackdrop, RectTransform previousShade, UnityEngine.UI.Text heading)
        {
            // Capture the original heading treatment before enabling the web-only gold treatment.
            if (!webHeading) { headingRestingColor = heading.color; headingRestingStyle = heading.fontStyle; }
            webHeading = heading;
            webElements = placements;
            webBackdrop = backdrop;
            originalBackdrop = previousBackdrop;
            originalShade = previousShade;
            portraitElements = WithHiddenDecorations(portraitElements, placements);
            landscapeElements = WithHiddenDecorations(landscapeElements, placements);
            lastMode = -1;
            Fit();
        }

        static LoginElementPlacement[] WithHiddenDecorations(LoginElementPlacement[] existing, LoginElementPlacement[] added)
        {
            var list = new List<LoginElementPlacement>(existing ?? Array.Empty<LoginElementPlacement>());
            foreach (var placement in added)
            {
                if (list.Exists(p => p.target == placement.target)) continue;
                var hidden = LoginElementPlacement.Capture(placement.target);
                hidden.visible = false;
                list.Add(hidden);
            }
            return list.ToArray();
        }

        public void Preview(MainMenuPlatform value) { platform = value; Fit(); }
        public void Configure(RectTransform root) { designRoot = root; lastMode = -1; Fit(); }
        void Update()
        {
            if (Mode != lastMode || AllowsGuestLogin != lastGuestAllowed || ((RectTransform)transform).rect.size != lastSize) Fit();
        }
        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void OnValidate() => Fit();

        void RefreshStandardNavigation()
        {
            var actions = designRoot.GetComponentsInChildren<UnityEngine.UI.Button>(false);
            for (int i = 0; i < actions.Length; i++)
            {
                var navigation = actions[i].navigation;
                navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                navigation.selectOnUp = actions[(i + actions.Length - 1) % actions.Length];
                navigation.selectOnDown = actions[(i + 1) % actions.Length];
                actions[i].navigation = navigation;
            }
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (Application.isPlaying && events && events.currentSelectedGameObject &&
                !events.currentSelectedGameObject.activeInHierarchy && actions.Length > 0)
                events.SetSelectedGameObject(actions[0].gameObject);
        }
        void Fit()
        {
            if (fitting || !designRoot || !(transform is RectTransform viewport)) return;
            var size = viewport.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            fitting = true;
            try
            {
                int mode = Mode;
                bool battlefield = mode == 3 || (mode == 1 && landscapeUsesBattlefield);
                bool psgBattlefield = mode == 2 && psg1Backdrop;
                designRoot.gameObject.SetActive(mode != 2);
                if (psg1Root) psg1Root.gameObject.SetActive(mode == 2);
                if (webBackdrop) webBackdrop.gameObject.SetActive(battlefield);
                if (psg1Backdrop) psg1Backdrop.gameObject.SetActive(psgBattlefield);
                if (originalBackdrop) originalBackdrop.gameObject.SetActive(!battlefield && !psgBattlefield);
                if (originalShade) originalShade.gameObject.SetActive(!battlefield && !psgBattlefield);
                bool changed = mode != lastMode || AllowsGuestLogin != lastGuestAllowed;
                if (changed)
                {
                    if (mode != 2)
                    {
                        var placements = mode == 3 ? webElements : mode == 1 ? landscapeElements : portraitElements;
                        foreach (var element in placements ?? Array.Empty<LoginElementPlacement>()) element.Apply();
                        if (mode == 0 && !AllowsGuestLogin)
                            foreach (var element in mobilePortraitElements ?? Array.Empty<LoginElementPlacement>()) element.Apply();
                    }
                    if (webHeading)
                    {
                        var gold = webHeading.GetComponent<ArcadeGoldText>();
                        if (gold) gold.enabled = battlefield;
                        webHeading.color = battlefield ? Color.white : headingRestingColor;
                        webHeading.fontStyle = battlefield ? FontStyle.Bold : headingRestingStyle;
                    }
                }
                var guest = designRoot.Find("Continue as Guest");
                if (guest) guest.gameObject.SetActive(AllowsGuestLogin);
                var psgGuest = psg1Root ? psg1Root.Find("Continue as Guest") : null;
                if (psgGuest) psgGuest.gameObject.SetActive(false);
                if (changed && mode != 2) RefreshStandardNavigation();
                var root = mode == 2 ? psg1Root : designRoot;
                var design = mode == 2 ? psg1DesignSize : mode == 3 ? webDesignSize : mode == 1 ? landscapeDesignSize : designSize;
                float coverage = mode == 2 ? psg1ScreenCoverage : mode == 3 ? webScreenCoverage : mode == 1 ? landscapeScreenCoverage : screenCoverage;
                root.sizeDelta = design;
                root.localScale = Vector3.one * Mathf.Min(size.x / Mathf.Max(1, design.x), size.y / Mathf.Max(1, design.y)) * coverage;
                lastGuestAllowed = AllowsGuestLogin;
                lastMode = mode;
                lastSize = size;
            }
            finally { fitting = false; }
        }
    }
}
