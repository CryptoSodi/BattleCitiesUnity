using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private const float PsgHudBandHeight = 184f;
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
            float hudWidth = mainFrame.rect.width - 80f;
            float scoreWidth = hudWidth * .28f;
            float outerWidth = (hudWidth - scoreWidth - 24f) * .5f;
            var scoreImage = statusBar.GetChild(1).GetComponent<UnityEngine.UI.Image>();
            float hudHeight = scoreImage && scoreImage.sprite
                ? scoreWidth * scoreImage.sprite.rect.height / scoreImage.sprite.rect.width : 130f;
            statusBar.localScale = Vector3.one;
            Place(statusBar, (width - hudWidth) * .5f, pad + 28f, hudWidth, hudHeight);
            float cardLeft = 0f;
            for (int i = 0; i < 3; i++)
            {
                var card = (RectTransform)statusBar.GetChild(i);
                var image = card.GetComponent<UnityEngine.UI.Image>();
                if (!image || !image.sprite) continue;
                float cardWidth = i == 1 ? scoreWidth : outerWidth;
                float cardHeight = cardWidth * image.sprite.rect.height / image.sprite.rect.width;
                image.preserveAspect = true;
                card.localScale = Vector3.one;
                Place(card, cardLeft, (hudHeight - cardHeight) * .5f, cardWidth, cardHeight);
                FitPsg1StatusCard(card, i);
                cardLeft += cardWidth + 12f;
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

        private static void FitPsg1StatusCard(RectTransform card, int index)
        {
            var readout = card.Find("Readout");
            if (!readout) return;
            var label = readout.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
            var value = readout.Find("Value")?.GetComponent<UnityEngine.UI.Text>();
            if (index == 1)
            {
                if (label) { HudBounds(label.rectTransform, .14f, .58f, .86f, .90f); FitHudText(label, 44, TextAnchor.MiddleCenter); }
                if (value) { HudBounds(value.rectTransform, .11f, .08f, .89f, .61f); FitHudText(value, 72, TextAnchor.MiddleCenter); }
                var well = readout.Find("Score Well") as RectTransform;
                if (well) HudBounds(well, .065f, .10f, .935f, .60f);
                return;
            }
            var socket = card.Find("Icon tile") as RectTransform;
            if (socket)
            {
                float size = card.rect.height * .72f;
                Place(socket, card.rect.width * .045f, (card.rect.height - size) * .5f, size, size);
                var icon = socket.Find("Icon") as RectTransform;
                if (icon)
                {
                    icon.localScale = Vector3.one;
                    HudBounds(icon, .07f, .07f, .93f, .93f);
                    var image = icon.GetComponent<UnityEngine.UI.Image>();
                    if (image) { image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true; }
                }
            }
            if (label) { HudBounds(label.rectTransform, .285f, .51f, .93f, .90f); FitHudText(label, 42, index == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter); }
            if (index == 0)
            {
                var progress = readout.Find("Progress") as RectTransform;
                var level = readout.Find("Level Pill") as RectTransform;
                if (progress) HudBounds(progress, .285f, .16f, .72f, .39f);
                if (level) HudBounds(level, .745f, .14f, .93f, .43f);
                if (value) { HudBounds(value.rectTransform, .75f, .16f, .923f, .42f); FitHudText(value, 32, TextAnchor.MiddleCenter); }
            }
            else if (value)
            {
                HudBounds(value.rectTransform, .285f, .10f, .93f, .57f);
                FitHudText(value, 56, TextAnchor.MiddleCenter);
            }
        }

        public void FitPreBattleScreen(RectTransform screen)
        {
            if (!screen) return;
            if (lastPlatform == MainMenuPlatform.Web &&
                mainFrame.Find("TV Background Viewport") is RectTransform opening)
            {
                // The desktop glass already defines the visible opening. Do not
                // add the sprite's full nine-slice border as a second inset.
                const float desktopClearance = 4f;
                var min = mainFrame.InverseTransformPoint(opening.TransformPoint(
                    new Vector3(opening.rect.xMin, opening.rect.yMin)));
                var max = mainFrame.InverseTransformPoint(opening.TransformPoint(
                    new Vector3(opening.rect.xMax, opening.rect.yMax)));
                screen.localScale = Vector3.one;
                Place(screen, min.x - mainFrame.rect.xMin + desktopClearance,
                    mainFrame.rect.yMax - max.y + desktopClearance,
                    Mathf.Max(1f, max.x - min.x - desktopClearance * 2f),
                    Mathf.Max(1f, max.y - min.y - desktopClearance * 2f));
                GetComponent<PreBattleScreen>()?.ApplyPlatformSpacing(lastPlatform);
                return;
            }
            var tv = mainFrame.Find("TV Frame") as RectTransform;
            var frameImage = tv ? tv.GetComponent<UnityEngine.UI.Image>() : null;
            if (frameImage && frameImage.sprite)
            {
                // The sliced surround occupies real layout space. Keep every selector
                // control inside its opening even when a platform scales the TV.
                var border = frameImage.sprite.border /
                    Mathf.Max(.01f, frameImage.pixelsPerUnit * frameImage.pixelsPerUnitMultiplier);
                const float clearance = 4f;
                var min = mainFrame.InverseTransformPoint(tv.TransformPoint(
                    new Vector3(tv.rect.xMin + border.x + clearance, tv.rect.yMin + border.y + clearance)));
                var max = mainFrame.InverseTransformPoint(tv.TransformPoint(
                    new Vector3(tv.rect.xMax - border.z - clearance, tv.rect.yMax - border.w - clearance)));
                screen.localScale = Vector3.one;
                Place(screen, min.x - mainFrame.rect.xMin, mainFrame.rect.yMax - max.y,
                    Mathf.Max(1f, max.x - min.x), Mathf.Max(1f, max.y - min.y));
            }
            else
            {
                screen.anchorMin = new Vector2(.025f, .035f);
                screen.anchorMax = new Vector2(.975f, .965f);
                screen.pivot = new Vector2(.5f, .5f);
                screen.offsetMin = screen.offsetMax = Vector2.zero;
            }
            GetComponent<PreBattleScreen>()?.ApplyPlatformSpacing(lastPlatform);
        }
    }
}
