using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private static readonly Color TankSelectorMonitorColor = new Color32(0, 52, 153, 255);

        public void SetTankSelectorBackdrop(bool active)
        {
            var viewport = mainFrame ? mainFrame.Find("TV Background Viewport") : null;
            var fog = viewport ? viewport.Find("TV White Fog") : null;
            if (fog && fog.TryGetComponent<UnityEngine.UI.Image>(out var image))
                image.color = active ? TankSelectorMonitorColor : new Color(1f, 1f, 1f, 0f);
        }

        [SerializeField, Min(100f), Tooltip("Start button width used by the responsive layout. Adjust this instead of the button RectTransform scale.")]
        private float startButtonMaxWidth = 330f;
        [SerializeField, Range(.25f, 1.5f), Tooltip("Logo size multiplier used by the responsive monitor layout.")]
        private float monitorLogoSize = .9f;
        [SerializeField, Tooltip("Moves the logo and Start group upward inside the monitor, in layout units.")]
        private float monitorHeroLift = 20f;
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
                    // Keep refraction attached to the TV opening, not the larger
                    // background image, across all platform layouts.
                    if (blurredImage.material && blurredImage.material.HasProperty("_GlassRect"))
                    {
                        viewport.GetWorldCorners(corners);
                        var glassMin = blurred.InverseTransformPoint(corners[0]);
                        var glassMax = blurred.InverseTransformPoint(corners[2]);
                        var glassBounds = new Vector4(glassMin.x, glassMin.y,
                            glassMax.x - glassMin.x, glassMax.y - glassMin.y);
                        blurredImage.material.SetVector("_GlassRect", glassBounds);
                        // A stencil Mask caches a derived material. Update that
                        // copy too when switching between screen layouts.
                        blurredImage.materialForRendering.SetVector("_GlassRect", glassBounds);
                        blurredImage.SetMaterialDirty();
                    }
                    viewport.gameObject.SetActive(true);
                    if (viewport.GetSiblingIndex() != 0) viewport.SetAsFirstSibling();
                }
            }
            if (viewport)
            {
                // A light blue frost remains as a fallback if the blur shader is unavailable.
                var fog = viewport.Find("TV White Fog") as RectTransform;
                if (!fog)
                {
                    var fogObject = new GameObject("TV White Fog", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    fog = fogObject.GetComponent<RectTransform>();
                    fog.SetParent(viewport, false);
                }
                fog.gameObject.layer = viewport.gameObject.layer;
                fog.anchorMin = Vector2.zero;
                fog.anchorMax = Vector2.one;
                fog.pivot = new Vector2(.5f, .5f);
                fog.anchoredPosition = Vector2.zero;
                fog.sizeDelta = Vector2.zero;
                fog.localRotation = Quaternion.identity;
                fog.localScale = Vector3.one;
                var fogImage = fog.GetComponent<UnityEngine.UI.Image>();
                fogImage.raycastTarget = false;
                fogImage.maskable = true;
                if (fog.GetSiblingIndex() != viewport.childCount - 1) fog.SetAsLastSibling();
                fog.gameObject.SetActive(true);
            }
            var tankScreen = mainFrame.Find("Pre-battle screens");
            SetTankSelectorBackdrop(tankScreen && tankScreen.gameObject.activeSelf);
            // The hero fills the TV opening now that the old reward area is gone.
            // Center the logo and primary action as one group on each layout.
            if (viewport && hero && logo && startRect)
            {
                hero.anchorMin = viewport.anchorMin;
                hero.anchorMax = viewport.anchorMax;
                hero.pivot = viewport.pivot;
                hero.anchoredPosition = viewport.anchoredPosition;
                hero.sizeDelta = viewport.sizeDelta;
                hero.localScale = Vector3.one;

                float width = hero.rect.width;
                float height = hero.rect.height;
                bool landscapePhone = target == MainMenuPlatform.AndroidLandscape;
                float logoLimit = landscapePhone ? 300f : portrait ? 420f :
                    target == MainMenuPlatform.Psg1 ? 380f : 350f;
                float logoSide = Mathf.Min(logoLimit * monitorLogoSize, height * .72f);
                float buttonWidth = Mathf.Min(Mathf.Max(100f, startButtonMaxWidth), width * .7f);
                var startImage = startRect.GetComponent<UnityEngine.UI.Image>();
                float buttonAspect = startImage && startImage.sprite ?
                    startImage.sprite.rect.width / startImage.sprite.rect.height : 1400f / 335f;
                float buttonHeight = buttonWidth / buttonAspect;
                float gap = landscapePhone ? 12f : Mathf.Clamp(height * .045f, 24f, 40f);
                float groupHeight = logoSide + gap + buttonHeight;
                if (groupHeight > height - 20f)
                {
                    float fit = (height - 20f) / groupHeight;
                    logoSide *= fit;
                    buttonWidth *= fit;
                    buttonHeight *= fit;
                    gap *= fit;
                    groupHeight = logoSide + gap + buttonHeight;
                }
                float top = Mathf.Clamp((height - groupHeight) * .5f - monitorHeroLift,
                    10f, Mathf.Max(10f, height - groupHeight - 10f));
                logo.localScale = Vector3.one;
                startRect.localScale = Vector3.one;
                Place(logo, (width - logoSide) * .5f, top, logoSide, logoSide);
                Place(startRect, (width - buttonWidth) * .5f,
                    top + logoSide + gap, buttonWidth, buttonHeight);
                // Scale around the monitor center, not the left edge.
                // Preserve the authored top edge and vertical placement.
                logo.pivot = new Vector2(.5f, logo.pivot.y);
                logo.anchoredPosition += new Vector2(logoSide * .5f, 0);
                startRect.pivot = new Vector2(.5f, startRect.pivot.y);
                startRect.anchoredPosition += new Vector2(buttonWidth * .5f, 0);
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
