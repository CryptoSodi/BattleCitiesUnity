using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private const float InstructionsPageSeconds = 8f;
        private static readonly string[] PrizeRanks = { "1ST", "2ND", "3RD", "4TH–10TH" };
        private static readonly string[] PrizeAmounts = { "100 SKR", "50 SKR", "25 SKR", "15 SKR EACH" };
        private RectTransform prizeStrip;
        private Vector2 instructionsHeadingSize;
        private float instructionsPageElapsed;
        private bool showingPrizes;

        private void LayoutInstructionSteps(RectTransform paper)
        {
            for (int i = 0; i < 3; i++)
            {
                var step = paper.Find("Step " + (i + 1)) as RectTransform;
                if (!step) continue;
                PrizeBox(step, .015f + i / 3f, .06f, (i + 1) / 3f - .015f, .86f);
                var icon = step.Find("Icon") as RectTransform;
                if (i == 2 && icon && theme && theme.PrizeCrates != null &&
                    theme.PrizeCrates.Length > 0 && theme.PrizeCrates[0])
                {
                    var image = icon.GetComponent<Image>();
                    if (image)
                    {
                        image.sprite = theme.PrizeCrates[0];
                        image.type = Image.Type.Simple;
                        image.preserveAspect = true;
                    }
                }
                var badge = step.Find("Step Badge") as RectTransform;
                var title = step.Find("Title")?.GetComponent<Text>();
                var body = step.Find("Body")?.GetComponent<Text>();
                if (!icon || !badge || !title || !body) continue;

                FitHudText(title, 25, TextAnchor.LowerLeft);
                FitHudText(body, 22, TextAnchor.UpperLeft);
                ArcadeTextStyles.ApplyGold(title, theme ? theme.HeadingFont : null);
                title.resizeTextMinSize = body.resizeTextMinSize = 12;
                float width = step.rect.width;
                float height = step.rect.height;
                float badgeSize = Mathf.Min(height * .6f, width * .18f);
                float iconSize = Mathf.Min(height * .74f, width * .24f);
                float gap = Mathf.Clamp(width * .025f, 5f, 10f);
                float textWidth = Mathf.Min(Mathf.Max(title.preferredWidth, body.preferredWidth) + 2f,
                    width - badgeSize - iconSize - gap * 2.5f);
                float groupWidth = badgeSize + iconSize + textWidth + gap * 2f;
                float left = (width - groupWidth) * .5f;

                // The number leads each group, outside the icon and text pair.
                InformationBox(badge, left, 0f, badgeSize, badgeSize);
                left += badgeSize + gap * 1.4f;
                InformationBox(icon, left, 0f, iconSize, iconSize);
                left += iconSize + gap * .6f;
                const float textGap = 2f;
                float titleHeight = Mathf.Min(31f, height * .37f);
                float bodyHeight = Mathf.Min(31f, height * .37f);
                InformationBox(title.rectTransform, left, (bodyHeight + textGap) * .5f,
                    textWidth, titleHeight);
                InformationBox(body.rectTransform, left, -(titleHeight + textGap) * .5f,
                    textWidth, bodyHeight);
            }
        }

        private void LayoutPrizeStrip(RectTransform paper, bool portrait)
        {
            if (!prizeStrip) prizeStrip = PrizeRect(paper, "Prize Distribution");
            PrizeBox(prizeStrip, .012f, .06f, .988f, .82f);
            for (int i = 0; i < PrizeRanks.Length; i++)
            {
                var item = PrizeRect(prizeStrip, "Prize " + (i + 1));
                PrizeBox(item, i / 4f, 0f, (i + 1) / 4f, 1f);
                var icon = EnsurePanelImage(item, "Crate");
                icon.sprite = theme && theme.PrizeCrates != null && theme.PrizeCrates.Length > i
                    ? theme.PrizeCrates[i] : null;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                var previousBadge = item.Find("Step Badge");
                if (previousBadge) previousBadge.gameObject.SetActive(false);
                var rank = PrizeText(item, "Rank", PrizeRanks[i], portrait ? 24 : 25);
                var amount = PrizeText(item, "Amount", PrizeAmounts[i], 30);
                ArcadeTextStyles.ApplyGold(rank, theme ? theme.HeadingFont : null);
                if (portrait)
                {
                    PrizeBox(icon.rectTransform, .14f, .49f, .86f, 1f);
                    PrizeBox(rank.rectTransform, .02f, .27f, .98f, .49f);
                    PrizeBox(amount.rectTransform, .02f, 0f, .98f, .27f);
                }
                else
                {
                    rank.alignment = amount.alignment = TextAnchor.MiddleLeft;
                    float width = item.rect.width;
                    float height = item.rect.height;
                    float iconSize = Mathf.Min(height * .9f, width * .28f);
                    float gap = Mathf.Clamp(width * .025f, 5f, 10f);
                    float textWidth = Mathf.Min(Mathf.Max(rank.preferredWidth, amount.preferredWidth) + 2f,
                        width * .9f - iconSize - gap);
                    float left = (width - iconSize - gap - textWidth) * .5f;
                    InformationBox(icon.rectTransform, left, 0f, iconSize, iconSize);
                    left += iconSize + gap;
                    float lineHeight = Mathf.Min(34f, height * .42f);
                    InformationBox(rank.rectTransform, left, (lineHeight + 2f) * .5f,
                        textWidth, lineHeight);
                    InformationBox(amount.rectTransform, left, -(lineHeight + 2f) * .5f,
                        textWidth, lineHeight);
                }
            }
            for (int i = 1; i < 4; i++)
            {
                var divider = EnsurePanelImage(prizeStrip, "Prize Divider " + i);
                divider.color = new Color(6f / 255f, 29f / 255f, 54f / 255f, .3f);
                var rect = divider.rectTransform;
                rect.anchorMin = new Vector2(i / 4f, 0f);
                rect.anchorMax = new Vector2(i / 4f, 1f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1.5f, 0f);
            }
            SetInstructionsPage(showingPrizes);
        }

        private static RectTransform PrizeRect(Transform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect) return rect;
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private Text PrizeText(Transform parent, string name, string value, int size)
        {
            var rect = PrizeRect(parent, name);
            var label = rect.GetComponent<Text>();
            if (!label) label = rect.gameObject.AddComponent<Text>();
            label.text = value;
            label.font = theme ? theme.HeadingFont : null;
            label.fontSize = size;
            label.color = theme ? theme.Navy : new Color32(6, 29, 54, 255);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            return label;
        }

        private static void PrizeBox(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void InformationBox(RectTransform rect, float left, float centerY, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, centerY);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private void SetInstructionsPage(bool prizes)
        {
            showingPrizes = prizes;
            if (!howItWorks || !prizeStrip) return;
            var paper = prizeStrip.parent;
            for (int i = 1; i <= 3; i++)
            {
                var step = paper.Find("Step " + i);
                if (step) step.gameObject.SetActive(!prizes);
                var divider = paper.Find("Step Divider " + i);
                if (divider) divider.gameObject.SetActive(!prizes);
            }
            prizeStrip.gameObject.SetActive(prizes);
            var heading = howItWorks.Find("Heading") as RectTransform;
            var title = heading ? heading.Find("Title")?.GetComponent<Text>() : null;
            if (!title) return;
            if (instructionsHeadingSize == Vector2.zero) instructionsHeadingSize = heading.sizeDelta;
            title.text = prizes ? "PRIZE DISTRIBUTION !" : "HOW IT WORKS ?";
            heading.sizeDelta = new Vector2(
                Mathf.Max(instructionsHeadingSize.x, title.preferredWidth + 24f), heading.sizeDelta.y);
        }

        private void UpdateInstructionsPage(float elapsed)
        {
            if (!howItWorks || !howItWorks.gameObject.activeInHierarchy)
            {
                instructionsPageElapsed = 0f;
                if (showingPrizes) SetInstructionsPage(false);
                return;
            }
            if (!prizeStrip) return;
            instructionsPageElapsed += elapsed;
            if (instructionsPageElapsed < InstructionsPageSeconds) return;
            instructionsPageElapsed %= InstructionsPageSeconds;
            SetInstructionsPage(!showingPrizes);
        }
    }
}
