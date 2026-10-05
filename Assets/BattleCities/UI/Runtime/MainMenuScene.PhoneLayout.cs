using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private const float CompactOuterHudWidthRatio = .84f;

        private void ApplyAndroidLandscapeLayout(Vector2 available)
        {
            // Fill the safe area on both wide phones and 16:9 Android screens.
            float scale = available.y / GetLayout(MainMenuPlatform.AndroidLandscape).referenceResolution.y;
            if (scale <= 0f) return;
            content.localScale = Vector3.one * scale;
            content.sizeDelta = available / scale;
            float width = content.rect.width, height = content.rect.height;
            const float pad = 12f, gap = 12f, navInset = 12f, buttonGap = 6f;
            float bodyHeight = height - pad * 2f;
            float buttonHeight = (bodyHeight - navInset * 2f - buttonGap * (tabs.Length - 1)) / tabs.Length;
            float buttonWidth = buttonHeight * NavigationAspect;
            float navWidth = buttonWidth + navInset * 2f;
            float boardWidth = Mathf.Clamp(width * .255f, 360f, 520f);
            float tvLeft = pad + navWidth + gap;
            float tvWidth = width - pad * 2f - navWidth - boardWidth - gap * 2f;

            navigation.localScale = leaderboard.localScale = mainFrame.localScale = Vector3.one;
            Place(navigation, pad, pad, navWidth, bodyHeight);
            for (int i = 0; i < tabs.Length; i++)
            {
                var button = (RectTransform)tabs[i].transform;
                button.localScale = Vector3.one;
                Place(button, navInset, navInset + i * (buttonHeight + buttonGap), buttonWidth, buttonHeight);
            }
            Place(mainFrame, tvLeft, pad, tvWidth, bodyHeight);
            Place(leaderboard, width - pad - boardWidth, pad, boardWidth, bodyHeight);
            leaderboard.gameObject.SetActive(true);
            controls.gameObject.SetActive(false);
            var tv = mainFrame.Find("TV Frame") as RectTransform;
            var viewport = mainFrame.Find("TV Background Viewport") as RectTransform;
            if (tv) Place(tv, 0f, 0f, tvWidth, bodyHeight);
            if (viewport) Place(viewport, 16f, 16f, tvWidth - 32f, bodyHeight - 32f);

            var opening = MonitorOpeningInContent();
            const float hudGap = 16f;
            float hudWidth = Mathf.Min(opening.width * .82f, 860f);
            float scoreWidth = hudWidth * .24f;
            float outerWidth = (hudWidth - scoreWidth - hudGap * 2f) * .5f;
            float outerNaturalWidth = outerWidth;
            outerWidth *= CompactOuterHudWidthRatio;
            hudWidth = scoreWidth + outerWidth * 2f + hudGap * 2f;
            var scoreArt = statusBar.GetChild(1).GetComponent<UnityEngine.UI.Image>();
            float hudHeight = scoreArt && scoreArt.sprite
                ? scoreWidth * scoreArt.sprite.rect.height / scoreArt.sprite.rect.width : 100f;
            statusBar.localScale = Vector3.one;
            Place(statusBar, opening.x + (opening.width - hudWidth) * .5f, opening.y + 12f, hudWidth, hudHeight);
            float left = 0f;
            for (int i = 0; i < 3; i++)
            {
                var card = (RectTransform)statusBar.GetChild(i);
                var image = card.GetComponent<UnityEngine.UI.Image>();
                float cardWidth = i == 1 ? scoreWidth : outerWidth;
                float cardHeight = image && image.sprite
                    ? (i == 1 ? scoreWidth : outerNaturalWidth) * image.sprite.rect.height / image.sprite.rect.width : hudHeight;
                card.localScale = Vector3.one;
                if (image) image.preserveAspect = true;
                Place(card, left, (hudHeight - cardHeight) * .5f, cardWidth, cardHeight);
                FitStatusCardFrame(card, i != 1);
                FitLandscapeStatusCard(card, i);
                left += cardWidth + hudGap;
            }
            statusBar.SetSiblingIndex(mainFrame.GetSiblingIndex() + 1);
            LayoutMonitorInstructions();
            foreach (var name in new[] { "Heading", "Round status", "Columns", "Scores", "Footer" })
            {
                var rect = leaderboard.Find(name) as RectTransform;
                if (!rect) continue;
                switch (name)
                {
                    case "Heading": HudBounds(rect, .04f, .87f, .96f, .98f); break;
                    case "Round status": HudBounds(rect, .05f, .77f, .95f, .855f); break;
                    case "Columns": HudBounds(rect, .05f, .695f, .95f, .755f); break;
                    case "Scores": HudBounds(rect, .05f, .145f, .95f, .69f); break;
                    case "Footer": HudBounds(rect, .04f, .03f, .96f, .13f); break;
                }
            }
            ConfigureNavigation(false);
        }

        private static void FitLandscapeStatusCard(RectTransform card, int index)
        {
            var readout = card.Find("Readout") as RectTransform;
            if (!readout) return;
            HudBounds(readout, 0f, 0f, 1f, 1f);
            readout.localScale = Vector3.one;
            float width = card.rect.width, height = card.rect.height;
            var label = readout.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
            var value = readout.Find("Value")?.GetComponent<UnityEngine.UI.Text>();
            if (index == 1)
            {
                if (label)
                {
                    Place(label.rectTransform, width * .12f, height * .13f, width * .76f, height * .29f);
                    FitLandscapeHudText(label, 28, TextAnchor.MiddleCenter);
                }
                var well = readout.Find("Score Well") as RectTransform;
                if (well) Place(well, width * .075f, height * .43f, width * .85f, height * .43f);
                if (value)
                {
                    Place(value.rectTransform, width * .10f, height * .43f, width * .80f, height * .43f);
                    FitLandscapeHudText(value, 48, TextAnchor.MiddleCenter);
                }
                return;
            }

            FitCompactStatusIcon(card, index, out float textLeft, out float textWidth);
            if (label)
            {
                Place(label.rectTransform, textLeft, height * .13f, textWidth, height * .33f);
                FitLandscapeHudText(label, 28, TextAnchor.MiddleLeft);
            }
            if (index == 0)
            {
                FitCompactCommanderRow(card, readout, value, textLeft, true);
            }
            else if (value)
            {
                Place(value.rectTransform, textLeft, height * .48f, textWidth, height * .36f);
                FitLandscapeHudText(value, 36, TextAnchor.MiddleLeft);
            }
        }

        private static float StatusFrameAspect(RectTransform card)
        {
            var image = card.GetComponent<UnityEngine.UI.Image>();
            return image && image.sprite ? image.sprite.rect.width / image.sprite.rect.height : 4f;
        }

        private static void FitCompactStatusIcon(RectTransform card, int index, out float textLeft, out float textWidth)
        {
            float height = card.rect.height;
            float naturalWidth = height * StatusFrameAspect(card);
            var socket = card.Find("Icon tile") as RectTransform;
            if (socket)
            {
                // Match the Web socket geometry using the original frame aspect,
                // so shortening the frame does not move the icon into its rivets.
                socket.localScale = Vector3.one;
                Place(socket, naturalWidth * .07f, height * .10f, naturalWidth * .18f, height * .78f);
                var icon = socket.Find("Icon") as RectTransform;
                if (icon)
                {
                    icon.pivot = new Vector2(.5f, .5f);
                    HudBounds(icon, index == 0 ? .025f : .06f, .025f,
                        index == 0 ? .975f : .94f, .975f);
                    icon.localScale = Vector3.one * (index == 0 ? .77902f : .74368f);
                    var image = icon.GetComponent<UnityEngine.UI.Image>();
                    if (image) { image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true; }
                }
            }
            textLeft = naturalWidth * (index == 0 ? .265f : .275f);
            textWidth = card.rect.width - textLeft - naturalWidth * (index == 0 ? .085f : .095f);
        }

        private static void FitCompactCommanderRow(RectTransform card, RectTransform readout,
            UnityEngine.UI.Text value, float textLeft, bool landscapePhone)
        {
            float height = card.rect.height, naturalWidth = height * StatusFrameAspect(card);
            float levelWidth = naturalWidth * .175f;
            float levelLeft = card.rect.width - naturalWidth * .088f - levelWidth;
            var progress = readout.Find("Progress") as RectTransform;
            var level = readout.Find("Level Pill") as RectTransform;
            if (progress) Place(progress, textLeft, height * .575f,
                levelLeft - textLeft - naturalWidth * .012f, height * .205f);
            if (level) Place(level, levelLeft, height * .555f, levelWidth, height * .235f);
            if (value)
            {
                // Web aligns the level text and plate on the same vertical center.
                Place(value.rectTransform, levelLeft + levelWidth * .04f, height * .55f,
                    levelWidth * (.16f / .175f), height * .245f);
                if (landscapePhone) FitLandscapeHudText(value, 18, TextAnchor.MiddleCenter);
                else FitHudText(value, 32, TextAnchor.MiddleCenter);
            }
        }

        private static void FitStatusCardFrame(RectTransform card, bool compact)
        {
            var image = card.GetComponent<UnityEngine.UI.Image>();
            if (!image || !image.sprite) return;
            image.type = compact ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = !compact;
            // Scale the horizontal slice caps to the existing frame height.
            image.pixelsPerUnitMultiplier = compact
                ? image.sprite.rect.height / Mathf.Max(1f, card.rect.height * image.pixelsPerUnit) : 1f;
        }

        private static void FitLandscapeHudText(UnityEngine.UI.Text text, int maximum, TextAnchor alignment)
        {
            FitHudText(text, maximum, alignment);
            text.resizeTextMinSize = Mathf.Clamp(Mathf.FloorToInt(text.rectTransform.rect.height * .5f), 8, 16);
        }

        private Rect MonitorOpeningInContent()
        {
            var viewport = mainFrame.Find("TV Background Viewport") as RectTransform;
            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            var bottomLeft = content.InverseTransformPoint(corners[0]);
            var topRight = content.InverseTransformPoint(corners[2]);
            return new Rect(bottomLeft.x - content.rect.xMin, content.rect.yMax - topRight.y,
                topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        }

        private void LayoutMonitorInstructions()
        {
            if (!howItWorks) return;
            var opening = MonitorOpeningInContent();
            float height = Mathf.Clamp(opening.height * .22f, 164f, 184f);
            howItWorks.localScale = Vector3.one;
            Place(howItWorks, opening.x + 12f, opening.yMax - height - 12f, opening.width - 24f, height);
            howItWorks.SetSiblingIndex(statusBar.GetSiblingIndex() + 1);
            var heading = howItWorks.Find("Heading") as RectTransform;
            if (heading)
            {
                heading.localScale = Vector3.one;
                heading.sizeDelta = new Vector2(heading.sizeDelta.x, 38f);
                var title = heading.Find("Title")?.GetComponent<UnityEngine.UI.Text>();
                if (title) FitHudText(title, 32, TextAnchor.MiddleCenter);
            }
        }

        private void FitHeroBetweenMonitorPanels(MainMenuPlatform target)
        {
            if (target != MainMenuPlatform.Psg1 && target != MainMenuPlatform.AndroidLandscape) return;
            float top = target == MainMenuPlatform.Psg1 ? PsgHudBandHeight - 16f : statusBar.rect.height + 32f;
            float bottom = target == MainMenuPlatform.AndroidLandscape || controllerLegendHidden
                ? howItWorks.rect.height + 24f : GetLayout(MainMenuPlatform.Psg1).controllerLegendHeight + PsgLegendBottomInset;
            hero.anchoredPosition -= new Vector2(0f, top);
            hero.sizeDelta -= new Vector2(0f, top + bottom);
        }
    }
}
