using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private const float PsgHudBandHeight = 120f;
        private const float PsgLegendBottomInset = 32f;

        private void ApplyPsg1Layout()
        {
            float width = content.rect.width, height = content.rect.height;
            const float pad = 16f, side = 10f, gap = 8f;
            const float verticalEdge = 2f, tvSideInset = 0f, tvTopInset = 0f, tvToMenuGap = 2f, menuLift = 6f;
            float navWidth = Mathf.Min(width - pad * 2f, 800f);
            float buttonWidth = (navWidth - side * 2f - gap * (tabs.Length - 1)) / tabs.Length;
            float buttonHeight = buttonWidth / NavigationAspect;
            float navHeight = buttonHeight + side * 2f;
            float legendHeight = controllerLegendHidden ? 0f : GetLayout(MainMenuPlatform.Psg1).controllerLegendHeight;
            float navTop = height - verticalEdge - navHeight;
            float tvHeight = navTop - tvToMenuGap - tvTopInset;

            mainFrame.localScale = Vector3.one;
            Place(mainFrame, tvSideInset, tvTopInset, width - tvSideInset * 2f, tvHeight);
            var tv = mainFrame.Find("TV Frame") as RectTransform;
            var viewport = mainFrame.Find("TV Background Viewport") as RectTransform;
            if (tv) Place(tv, 0f, 0f, mainFrame.rect.width, tvHeight);
            if (viewport) Place(viewport, 16f, 16f, mainFrame.rect.width - 32f, tvHeight - 32f);

            // Keep the HUD in its shared hierarchy while placing it inside the PSG1 screen.
            float hudWidth = Mathf.Min(mainFrame.rect.width - 64f, 1050f);
            statusBar.localScale = Vector3.one;
            Place(statusBar, (width - hudWidth) * .5f, pad + 28f, hudWidth, 84f);
            LayoutHeaderCards();
            foreach (RectTransform card in statusBar)
            {
                var image = card.GetComponent<UnityEngine.UI.Image>();
                if (!image || !image.sprite) continue;
                float cardHeight = Mathf.Min(statusBar.rect.height,
                    card.rect.width * image.sprite.rect.height / image.sprite.rect.width);
                card.sizeDelta = new Vector2(card.sizeDelta.x, cardHeight);
                card.anchoredPosition = new Vector2(card.anchoredPosition.x,
                    -(statusBar.rect.height - cardHeight) * .5f);
            }
            statusBar.SetSiblingIndex(mainFrame.GetSiblingIndex() + 1);
            var battleScreen = mainFrame.Find("Pre-battle screens");
            bool showHomeHud = !IsModalOpen && !(battleScreen && battleScreen.gameObject.activeSelf);
            statusBar.gameObject.SetActive(showHomeHud);

            navigation.localScale = Vector3.one;
            Place(navigation, (width - navWidth) * .5f, navTop - menuLift, navWidth, navHeight);
            for (int i = 0; i < tabs.Length; i++)
            {
                var rect = (RectTransform)tabs[i].transform;
                rect.localScale = Vector3.one;
                Place(rect, side + i * (buttonWidth + gap), side, buttonWidth, buttonHeight);
            }
            float legendWidth = Mathf.Min(720f, width - pad * 2f);
            controls.localScale = Vector3.one;
            Place(controls, (width - legendWidth) * .5f,
                tvTopInset + tvHeight - PsgLegendBottomInset - legendHeight, legendWidth, legendHeight);
            controls.SetSiblingIndex(statusBar.GetSiblingIndex() + 1);
            var legendGroup = controls.GetComponent<CanvasGroup>();
            if (!legendGroup) legendGroup = controls.gameObject.AddComponent<CanvasGroup>();
            legendGroup.interactable = false;
            legendGroup.blocksRaycasts = false;
            legendGroup.alpha = Application.isPlaying && controllerLegendHideAt >= 0f
                ? Mathf.Clamp01((controllerLegendHideAt - Time.unscaledTime) / .4f) : 1f;
            controls.gameObject.SetActive(showHomeHud && legendHeight > 0f);
        }

        public void FitPreBattleScreen(RectTransform screen)
        {
            if (!screen) return;
            if (lastPlatform == MainMenuPlatform.Psg1)
            {
                Place(screen, 8f, 8f, mainFrame.rect.width - 16f,
                    mainFrame.rect.height - 16f);
            }
            else
            {
                screen.anchorMin = new Vector2(.025f, .035f);
                screen.anchorMax = new Vector2(.975f, .965f);
                screen.pivot = new Vector2(.5f, .5f);
                screen.offsetMin = screen.offsetMax = Vector2.zero;
            }
            GetComponent<PreBattleScreen>()?.ApplyPlatformSpacing(lastPlatform == MainMenuPlatform.Psg1);
        }
    }
}
