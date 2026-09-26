using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        // The arena art belongs to the whole screen. Existing menu controls retain their
        // authored positions above it, including separate console and mobile layouts.
        private void ApplyArenaBackdrop(MainMenuPlatform target, Vector2 available)
        {
            var canvasRect = transform as RectTransform;
            var backdrop = canvasRect ? canvasRect.Find("World backdrop") as RectTransform : null;
            var image = backdrop ? backdrop.GetComponent<UnityEngine.UI.Image>() : null;
            bool portrait = target == MainMenuPlatform.Android && available.y > available.x;
            Sprite sprite = null;
            if (theme)
            {
                if (target == MainMenuPlatform.Psg1) sprite = theme.PsgBattlefield;
                else if (target == MainMenuPlatform.Android || target == MainMenuPlatform.AndroidLandscape)
                    sprite = portrait ? theme.AndroidPortraitBattlefield : theme.AndroidLandscapeBattlefield;
                sprite = sprite ? sprite : theme.Battlefield;
            }
            if (image && sprite)
            {
                image.sprite = sprite;
                image.type = UnityEngine.UI.Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
                image.color = Color.white;
                var screen = canvasRect.rect.size;
                if (screen.x <= 0 || screen.y <= 0) screen = available;
                float aspect = sprite.rect.width / sprite.rect.height;
                float width = Mathf.Max(screen.x, screen.y * aspect);
                float height = Mathf.Max(screen.y, screen.x / aspect);
                // Portrait art has spare ground at the bottom; keep its castle in view.
                var anchor = portrait ? new Vector2(.5f, 1) : new Vector2(.5f, .5f);
                backdrop.anchorMin = backdrop.anchorMax = backdrop.pivot = anchor;
                backdrop.anchoredPosition = Vector2.zero;
                backdrop.sizeDelta = new Vector2(width, height);
            }

            var shade = canvasRect ? canvasRect.Find("Backdrop shade") : null;
            if (shade && shade.TryGetComponent<UnityEngine.UI.Image>(out var shadeImage))
                shadeImage.enabled = false;

            if (!mainFrame) return;
            if (mainFrame.TryGetComponent<UnityEngine.UI.Image>(out var frameImage))
            {
                frameImage.enabled = false;
                frameImage.raycastTarget = false;
            }
            var tv = mainFrame.Find("TV Frame");
            if (tv)
            {
                // Keep the existing TV surround, but let the arena remain visible
                // through its screen instead of drawing the sprite's solid center.
                if (tv.TryGetComponent<UnityEngine.UI.Image>(out var tvImage))
                {
                    tvImage.type = UnityEngine.UI.Image.Type.Sliced;
                    tvImage.fillCenter = false;
                    tvImage.raycastTarget = false;
                }
                tv.gameObject.SetActive(true);
            }
            var viewport = mainFrame.Find("TV Background Viewport") as RectTransform;
            if (viewport && backdrop && sprite)
            {
                var blurred = viewport.Find("TV Background") as RectTransform;
                var blurredImage = blurred ? blurred.GetComponent<UnityEngine.UI.Image>() : null;
                if (blurredImage)
                {
                    // Draw the same arena art in the masked TV opening. Match the
                    // full-screen image in world space so the scene does not jump
                    // at the TV edge when the platform or safe area changes.
                    var corners = new Vector3[4];
                    backdrop.GetWorldCorners(corners);
                    var bottomLeft = viewport.InverseTransformPoint(corners[0]);
                    var topRight = viewport.InverseTransformPoint(corners[2]);
                    blurred.anchorMin = blurred.anchorMax = blurred.pivot = new Vector2(.5f, .5f);
                    blurred.localRotation = Quaternion.identity;
                    blurred.localScale = Vector3.one;
                    blurred.sizeDelta = new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
                    blurred.anchoredPosition = new Vector2(
                        (bottomLeft.x + topRight.x) * .5f - viewport.rect.center.x,
                        (bottomLeft.y + topRight.y) * .5f - viewport.rect.center.y);
                    blurredImage.sprite = sprite;
                    blurredImage.type = UnityEngine.UI.Image.Type.Simple;
                    blurredImage.preserveAspect = false;
                    blurredImage.color = Color.white;
                    blurredImage.raycastTarget = false;
                    viewport.gameObject.SetActive(true);
                    if (viewport.GetSiblingIndex() != 0) viewport.SetAsFirstSibling();
                }
            }
            if (viewport)
            {
                // A regular UI image makes the white fog visible even when the
                // background blur shader is unavailable on a target platform.
                var fog = viewport.Find("TV White Fog") as RectTransform;
                if (!fog)
                {
                    var fogObject = new GameObject("TV White Fog", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    fog = fogObject.GetComponent<RectTransform>();
                    fog.SetParent(viewport, false);
                }
                fog.anchorMin = Vector2.zero;
                fog.anchorMax = Vector2.one;
                fog.pivot = new Vector2(.5f, .5f);
                fog.anchoredPosition = Vector2.zero;
                fog.sizeDelta = Vector2.zero;
                fog.localRotation = Quaternion.identity;
                fog.localScale = Vector3.one;
                var fogImage = fog.GetComponent<UnityEngine.UI.Image>();
                fogImage.color = new Color(1f, 1f, 1f, .60f);
                fogImage.raycastTarget = false;
                fogImage.maskable = true;
                if (fog.GetSiblingIndex() != viewport.childCount - 1) fog.SetAsLastSibling();
                fog.gameObject.SetActive(true);
            }
            var rewardHeader = rewards ? rewards.Find("Header Bar") : null;
            if (rewardHeader) rewardHeader.gameObject.SetActive(false);

            // The supplied full logo is nearly square; the prior logo was wide.
            // Give the taller artwork enough height while retaining each layout's center.
            if (logo && hero && theme && theme.Logo &&
                theme.Logo.rect.width / theme.Logo.rect.height < 1.25f &&
                target != MainMenuPlatform.AndroidLandscape)
            {
                float side = target == MainMenuPlatform.Psg1 ? 380 : portrait ? 420 : 350;
                float lift = target == MainMenuPlatform.Psg1 ? 10 : portrait ? 35 : 32;
                if (Mathf.Abs(logo.sizeDelta.x - side) > .1f ||
                    logo.pivot != new Vector2(.5f, .5f))
                {
                    Vector3 oldCenter = hero.InverseTransformPoint(logo.TransformPoint(logo.rect.center));
                    logo.anchorMin = logo.anchorMax = logo.pivot = new Vector2(.5f, .5f);
                    logo.localScale = Vector3.one;
                    logo.sizeDelta = new Vector2(side, side);
                    logo.anchoredPosition = new Vector2(0, oldCenter.y - hero.rect.center.y + lift);
                }
            }

            if (target == MainMenuPlatform.AndroidLandscape && hero && rewards && logo && startRect)
            {
                // A wide phone has room for the primary action and tabs, but not the
                // full reward podium. Ranking remains available from the navigation.
                rewards.gameObject.SetActive(false);
                float inset = GetLayout(target).contentInset;
                float width = mainFrame.sizeDelta.x - 2 * inset;
                float height = mainFrame.sizeDelta.y - 2 * inset;
                Place(hero, inset, inset, width, height);
                logo.localScale = Vector3.one;
                startRect.localScale = Vector3.one;
                Place(logo, (width - 340) * .5f, 0, 340, 340);
                Place(startRect, (width - 500) * .5f, 325, 500, 110);
            }

            if (portrait && highScoreLabel)
            {
                // An older portrait profile puts the value below its blue card.
                var value = highScoreLabel.rectTransform;
                value.anchorMin = new Vector2(.265f, .34f);
                value.anchorMax = new Vector2(.925f, .58f);
                value.pivot = new Vector2(.5f, .5f);
                value.offsetMin = value.offsetMax = Vector2.zero;
                value.localScale = Vector3.one;
                highScoreLabel.fontSize = 30;
                highScoreLabel.resizeTextForBestFit = true;
                highScoreLabel.resizeTextMinSize = 18;
                highScoreLabel.resizeTextMaxSize = 30;
            }
        }
    }
}
