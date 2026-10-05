using System.Collections;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private static readonly Color TankSelectorMonitorColor = new Color32(0, 52, 153, 255);
        private const float AndroidTvRevealSeconds = .3f;
        private Coroutine androidTvReveal;
        private bool androidTvVisible;
        private Material rankingsBackdropMaterial;
        private Material rankingHeadingBackdropMaterial;
        private Material rankingFooterBackdropMaterial;
        private Material instructionsBackdropMaterial;
        private Material inventoryBackdropMaterial;

        private void ReleasePaperBackdropMaterials()
        {
            ClearPanelMaterial(leaderboard ? leaderboard.Find("Scores/TV Background") : null);
            ClearPanelMaterial(leaderboard ? leaderboard.Find("Heading/Glass viewport/Paper/TV Background") : null);
            ClearPanelMaterial(leaderboard ? leaderboard.Find("Footer/Glass viewport/Paper/TV Background") : null);
            ClearPanelMaterial(howItWorks ? howItWorks.Find("Paper/TV Background") : null);
            ClearPanelMaterial(mainFrame ? mainFrame.Find("Shop screen/Inventory/Paper/TV Background") : null);
            ReleasePanelMaterial(rankingsBackdropMaterial);
            ReleasePanelMaterial(rankingHeadingBackdropMaterial);
            ReleasePanelMaterial(rankingFooterBackdropMaterial);
            ReleasePanelMaterial(instructionsBackdropMaterial);
            ReleasePanelMaterial(inventoryBackdropMaterial);
            rankingsBackdropMaterial = instructionsBackdropMaterial = inventoryBackdropMaterial = null;
            rankingHeadingBackdropMaterial = rankingFooterBackdropMaterial = null;
        }

        private static void ClearPanelMaterial(Transform background)
        {
            if (background && background.TryGetComponent<UnityEngine.UI.Image>(out var image))
                image.material = null;
        }

        private static void ReleasePanelMaterial(Material material)
        {
            if (!material) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        private void ApplyPaperBackdrop(RectTransform panel, RectTransform backdrop, Sprite sprite,
            Material televisionMaterial, ref Material panelMaterial, float cornerRadius = 16f)
        {
            if (!panel || !backdrop || !sprite || !theme || !theme.Rounded) return;

            // The former paper is now the clipping boundary. Its contents and
            // the arena image all stay inside the same rounded opening.
            var maskImage = panel.GetComponent<UnityEngine.UI.Image>();
            if (!maskImage) maskImage = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            maskImage.enabled = true;
            maskImage.sprite = theme.Rounded;
            maskImage.type = UnityEngine.UI.Image.Type.Sliced;
            maskImage.pixelsPerUnitMultiplier = 32f / (cornerRadius * maskImage.pixelsPerUnit);
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            var mask = panel.GetComponent<UnityEngine.UI.Mask>();
            if (!mask) mask = panel.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;

            var blurred = EnsurePanelImage(panel, "TV Background");
            var blurredRect = blurred.rectTransform;
            blurredRect.SetAsFirstSibling();
            var corners = new Vector3[4];
            backdrop.GetWorldCorners(corners);
            var bottomLeft = panel.InverseTransformPoint(corners[0]);
            var topRight = panel.InverseTransformPoint(corners[2]);
            blurredRect.anchorMin = blurredRect.anchorMax = blurredRect.pivot = new Vector2(.5f, .5f);
            blurredRect.localRotation = Quaternion.identity;
            blurredRect.localScale = Vector3.one;
            blurredRect.sizeDelta = new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
            blurredRect.anchoredPosition = new Vector2(
                (bottomLeft.x + topRight.x) * .5f - panel.rect.center.x,
                (bottomLeft.y + topRight.y) * .5f - panel.rect.center.y);
            blurred.sprite = sprite;
            blurred.type = UnityEngine.UI.Image.Type.Simple;
            blurred.preserveAspect = false;

            if (televisionMaterial)
            {
                // Each opening needs its own glass bounds; sharing the TV's
                // mutable material would move its edges onto these panels.
                if (!panelMaterial)
                    panelMaterial = new Material(televisionMaterial) { hideFlags = HideFlags.HideAndDontSave };
                panelMaterial.CopyPropertiesFromMaterial(televisionMaterial);
                panel.GetWorldCorners(corners);
                var glassMin = blurredRect.InverseTransformPoint(corners[0]);
                var glassMax = blurredRect.InverseTransformPoint(corners[2]);
                var glassBounds = new Vector4(glassMin.x, glassMin.y,
                    glassMax.x - glassMin.x, glassMax.y - glassMin.y);
                var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                var bounds = blurredRect.rect;
                var glassUv = new Vector4(
                    Mathf.Lerp(uv.x, uv.z, (glassMin.x - bounds.xMin) / bounds.width),
                    Mathf.Lerp(uv.y, uv.w, (glassMin.y - bounds.yMin) / bounds.height),
                    (glassMax.x - glassMin.x) / bounds.width * (uv.z - uv.x),
                    (glassMax.y - glassMin.y) / bounds.height * (uv.w - uv.y));
                panelMaterial.SetVector("_GlassRect", glassBounds);
                panelMaterial.SetVector("_GlassUVRect", glassUv);
                panelMaterial.SetFloat("_CornerRadius", cornerRadius);
                blurred.material = panelMaterial;
                blurred.materialForRendering.SetVector("_GlassRect", glassBounds);
                blurred.materialForRendering.SetVector("_GlassUVRect", glassUv);
                blurred.materialForRendering.SetFloat("_CornerRadius", cornerRadius);
                blurred.SetMaterialDirty();
            }

            var fog = EnsurePanelImage(panel, "TV White Fog");
            var fogRect = fog.rectTransform;
            fogRect.anchorMin = Vector2.zero;
            fogRect.anchorMax = Vector2.one;
            fogRect.offsetMin = fogRect.offsetMax = Vector2.zero;
            fogRect.localScale = Vector3.one;
            fogRect.SetSiblingIndex(1);
            // The TV shader supplies the white fog. Keep a plain white fallback
            // for a missing blur material, without doubling its normal opacity.
            fog.color = new Color(1, 1, 1, televisionMaterial ? 0 : .6f);
        }

        private static UnityEngine.UI.Image EnsurePanelImage(RectTransform panel, string name)
        {
            var child = panel.Find(name);
            if (!child)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(panel, false);
                child = go.transform;
            }
            child.gameObject.layer = panel.gameObject.layer;
            var image = child.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            image.maskable = true;
            return image;
        }

        private void SetAndroidTvVisible(bool visible,bool animate)
        {
            if(!mainFrame)return;
            var tv=mainFrame.Find("TV Frame");
            var viewport=mainFrame.Find("TV Background Viewport");
            bool android=lastPlatform==MainMenuPlatform.Android;
            if(!android)
            {
                if(androidTvReveal!=null){StopCoroutine(androidTvReveal);androidTvReveal=null;}
                var oldGroup=mainFrame.GetComponent<CanvasGroup>();
                if(oldGroup)oldGroup.alpha=1f;
                androidTvVisible=false;
                return;
            }
            var group=mainFrame.GetComponent<CanvasGroup>();
            if(!visible)
            {
                if(androidTvReveal!=null){StopCoroutine(androidTvReveal);androidTvReveal=null;}
                if(group)group.alpha=1f;
                if(tv)tv.gameObject.SetActive(false);
                if(viewport)viewport.gameObject.SetActive(false);
                androidTvVisible=false;
                return;
            }
            if(tv)tv.gameObject.SetActive(true);
            if(viewport)viewport.gameObject.SetActive(true);
            if(androidTvVisible)return;
            androidTvVisible=true;
            if(animate && Application.isPlaying && isActiveAndEnabled)
            {
                if(!group)group=mainFrame.gameObject.AddComponent<CanvasGroup>();
                group.alpha=0f;
                androidTvReveal=StartCoroutine(RevealAndroidTv(group));
            }
            else if(group)group.alpha=1f;
        }

        private IEnumerator RevealAndroidTv(CanvasGroup group)
        {
            float elapsed=0f;
            while(elapsed<AndroidTvRevealSeconds)
            {
                elapsed+=Time.unscaledDeltaTime;
                group.alpha=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(elapsed/AndroidTvRevealSeconds));
                yield return null;
            }
            group.alpha=1f;
            androidTvReveal=null;
        }

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
            if (target == MainMenuPlatform.Psg1) ApplyPsg1Layout();
            else if (target == MainMenuPlatform.AndroidLandscape) ApplyAndroidLandscapeLayout(available);
            else if (statusBar)
            {
                statusBar.gameObject.SetActive(true);
                for (int i = 0; i < statusBar.childCount; i++)
                    FitStatusCardFrame((RectTransform)statusBar.GetChild(i), false);
            }
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
                        // Canvas batching can transform UI vertices into canvas
                        // space. Texture coordinates remain stable when entering
                        // fullscreen, so use them to locate the glass edges.
                        var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                        var bounds = blurred.rect;
                        var glassUv = new Vector4(
                            Mathf.Lerp(uv.x, uv.z, (glassMin.x - bounds.xMin) / bounds.width),
                            Mathf.Lerp(uv.y, uv.w, (glassMin.y - bounds.yMin) / bounds.height),
                            (glassMax.x - glassMin.x) / bounds.width * (uv.z - uv.x),
                            (glassMax.y - glassMin.y) / bounds.height * (uv.w - uv.y));
                        blurredImage.material.SetVector("_GlassRect", glassBounds);
                        blurredImage.material.SetVector("_GlassUVRect", glassUv);
                        // A stencil Mask caches a derived material. Update that
                        // copy too when switching between screen layouts.
                        blurredImage.materialForRendering.SetVector("_GlassRect", glassBounds);
                        blurredImage.materialForRendering.SetVector("_GlassUVRect", glassUv);
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
            var televisionBlur = viewport ? viewport.Find("TV Background")?.GetComponent<UnityEngine.UI.Image>() : null;
            var televisionMaterial = televisionBlur ? televisionBlur.material : null;
            var rewardCoin = leaderboard ? leaderboard.Find("Footer/Reward Coin") as RectTransform : null;
            if (rewardCoin)
            {
                // Authored layouts still contain the old animated chest's
                // transform. The replacement coin uses the footer icon bounds.
                rewardCoin.anchorMin = rewardCoin.anchorMax = new Vector2(0f, .5f);
                rewardCoin.pivot = new Vector2(0f, .5f);
                rewardCoin.anchoredPosition = new Vector2(14f, 0f);
                float coinSize = target == MainMenuPlatform.AndroidLandscape
                    ? Mathf.Min(78f, ((RectTransform)rewardCoin.parent).rect.height * .8f) : 78f;
                rewardCoin.sizeDelta = new Vector2(coinSize, coinSize);
                rewardCoin.localScale = Vector3.one;
            }
            ApplyPaperBackdrop(leaderboard ? leaderboard.Find("Scores") as RectTransform : null,
                backdrop, sprite, televisionMaterial, ref rankingsBackdropMaterial);
            var instructionsPaper = howItWorks ? howItWorks.Find("Paper") as RectTransform : null;
            if (instructionsPaper)
            {
                bool directOnTv = target == MainMenuPlatform.AndroidLandscape || target == MainMenuPlatform.Psg1;
                // Inner edge of navigation-container's white rim, in source
                // pixels. Nine-sliced borders keep this inset fixed as the
                // container widens; percentage padding leaves a growing gap.
                var frame = howItWorks.GetComponent<UnityEngine.UI.Image>();
                if (frame) frame.enabled = !directOnTv;
                var headingPanel = howItWorks.Find("Heading")?.GetComponent<UnityEngine.UI.Image>();
                if (headingPanel) headingPanel.enabled = true;
                var startSeparator = EnsurePanelImage(howItWorks, "Start separator");
                startSeparator.gameObject.SetActive(directOnTv);
                startSeparator.color = new Color(6f / 255f, 29f / 255f, 54f / 255f, .5f);
                var separatorRect = startSeparator.rectTransform;
                separatorRect.anchorMin = new Vector2(.015f, 1f);
                separatorRect.anchorMax = new Vector2(.985f, 1f);
                separatorRect.pivot = new Vector2(.5f, .5f);
                float headingBottom = headingPanel
                    ? howItWorks.InverseTransformPoint(headingPanel.rectTransform.TransformPoint(
                        new Vector3(0f, headingPanel.rectTransform.rect.yMin, 0f))).y
                    : howItWorks.rect.yMax - 38f;
                separatorRect.anchoredPosition = new Vector2(0f, headingBottom - howItWorks.rect.yMax - 5f);
                separatorRect.sizeDelta = new Vector2(0f, 3f);
                separatorRect.localScale = Vector3.one;
                separatorRect.SetAsLastSibling();
                float pixelsPerUnit = frame ? frame.pixelsPerUnit * frame.pixelsPerUnitMultiplier : 7f;
                var inset = new Vector4(114f, 138f, 113f, 126f) / pixelsPerUnit;
                instructionsPaper.anchorMin = Vector2.zero;
                instructionsPaper.anchorMax = Vector2.one;
                instructionsPaper.offsetMin = directOnTv ? Vector2.zero : new Vector2(inset.x, inset.y);
                instructionsPaper.offsetMax = directOnTv ? new Vector2(0f, -42f) : new Vector2(-inset.z, -inset.w);
                var paperMask = instructionsPaper.GetComponent<UnityEngine.UI.Mask>();
                if (paperMask) paperMask.enabled = !directOnTv;
                foreach (var name in new[] { "TV Background", "TV White Fog" })
                {
                    var background = instructionsPaper.Find(name);
                    if (background) background.gameObject.SetActive(!directOnTv);
                }
                if (directOnTv)
                {
                    var paperImage = instructionsPaper.GetComponent<UnityEngine.UI.Image>();
                    if (paperImage) paperImage.enabled = false;
                }
                else
                    ApplyPaperBackdrop(instructionsPaper, backdrop, sprite, televisionMaterial,
                        ref instructionsBackdropMaterial, 46f / pixelsPerUnit);
                LayoutInstructionSteps(instructionsPaper);
                for (int step = 1; step < 3; step++)
                {
                    var divider = EnsurePanelImage(instructionsPaper, "Step Divider " + step);
                    divider.color = new Color(6f / 255f, 29f / 255f, 54f / 255f, .5f);
                    var dividerRect = divider.rectTransform;
                    dividerRect.anchorMin = new Vector2(step / 3f, .10f);
                    dividerRect.anchorMax = new Vector2(step / 3f, .86f);
                    dividerRect.pivot = new Vector2(.5f, .5f);
                    dividerRect.anchoredPosition = Vector2.zero;
                    dividerRect.sizeDelta = new Vector2(3f, 0);
                    dividerRect.localScale = Vector3.one;
                    dividerRect.SetAsLastSibling();
                }
                LayoutPrizeStrip(instructionsPaper, portrait);
            }
            var tankScreen = mainFrame.Find("Pre-battle screens");
            FitPreBattleScreen(tankScreen as RectTransform);
            LayoutShopScreen();
            SetTankSelectorBackdrop(IsTvScreenOpen);
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

                FitHeroBetweenMonitorPanels(target);

                float width = hero.rect.width;
                float height = hero.rect.height;
                bool landscapePhone = target == MainMenuPlatform.AndroidLandscape;
                float logoLimit = landscapePhone ? 300f : portrait ? 420f :
                    target == MainMenuPlatform.Psg1 ? 420f : 350f;
                float logoSide = Mathf.Min(logoLimit * monitorLogoSize, height * .72f);
                float buttonLimit = target == MainMenuPlatform.Psg1 ? startButtonMaxWidth * 1.1f : startButtonMaxWidth;
                float buttonWidth = Mathf.Min(Mathf.Max(100f, buttonLimit), width * .7f);
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

            if (portrait && statusBar)
            {
                FitAndroidStatusCard((RectTransform)statusBar.GetChild(0),true);
                FitAndroidStatusCard((RectTransform)statusBar.GetChild(2),false);
            }
            bool showHome=!IsModalOpen && !IsTvScreenOpen;
            SetHeroVisible(showHome,false);
        }
    }
}
