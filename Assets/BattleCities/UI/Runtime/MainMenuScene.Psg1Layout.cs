using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private const float PsgHudBandHeight = 184f;
        private const float PsgLegendBottomInset = 32f;
        private const float PsgLegendWidth = 880f, PsgLegendHeight = 112f;
        private int psgLegendArtRevision=-1;
        private Sprite psgLegendSelect,psgLegendStart;

        private void ApplyPsg1Layout()
        {
            float width = content.rect.width, height = content.rect.height;
            const float pad = 16f, side = 10f, gap = 8f;
            const float verticalEdge = 2f, tvSideInset = 0f, tvTopInset = 0f, tvToMenuGap = 2f, menuLift = 6f;
            // Very tall browser windows need wider touch targets than the
            // console's fixed-width navigation bar would provide.
            bool narrowWeb=ResponsiveWebLayout.IsBrowserSurface && width/Mathf.Max(1f,height)<.7f;
            float navWidth = narrowWeb ? width - pad * 2f : Mathf.Min(width - pad * 2f, 800f);
            float buttonWidth = (navWidth - side * 2f - gap * (tabs.Length - 1)) / tabs.Length;
            float buttonHeight = buttonWidth / NavigationAspect;
            float navHeight = buttonHeight + side * 2f;
            float legendHeight = controllerLegendHidden || ResponsiveWebLayout.IsBrowserSurface ? 0f : Mathf.Max(PsgLegendHeight,GetLayout(MainMenuPlatform.Psg1).controllerLegendHeight);
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
            float outerNaturalWidth = outerWidth;
            outerWidth *= CompactOuterHudWidthRatio;
            hudWidth = scoreWidth + outerWidth * 2f + 24f;
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
                float cardHeight = (i == 1 ? scoreWidth : outerNaturalWidth) * image.sprite.rect.height / image.sprite.rect.width;
                image.preserveAspect = true;
                card.localScale = Vector3.one;
                Place(card, cardLeft, (hudHeight - cardHeight) * .5f, cardWidth, cardHeight);
                FitStatusCardFrame(card, i != 1);
                FitPsg1StatusCard(card, i);
                cardLeft += cardWidth + 12f;
            }
            statusBar.SetSiblingIndex(mainFrame.GetSiblingIndex() + 1);
            bool showHomeHud = !IsModalOpen && !IsTvScreenOpen;
            statusBar.gameObject.SetActive(showHomeHud);
            if(ResponsiveWebLayout.IsBrowserSurface&&howItWorks)howItWorks.gameObject.SetActive(showHomeHud);

            navigation.localScale = Vector3.one;
            Place(navigation, (width - navWidth) * .5f, navTop - menuLift, navWidth, navHeight);
            for (int i = 0; i < tabs.Length; i++)
            {
                var rect = (RectTransform)tabs[i].transform;
                rect.localScale = Vector3.one;
                Place(rect, side + i * (buttonWidth + gap), side, buttonWidth, buttonHeight);
            }
            float legendWidth = Mathf.Min(PsgLegendWidth, width - pad * 2f);
            controls.localScale = Vector3.one;
            Place(controls, (width - legendWidth) * .5f,
                tvTopInset + tvHeight - PsgLegendBottomInset - legendHeight, legendWidth, legendHeight);
            if(Application.isPlaying&&!ResponsiveWebLayout.IsBrowserSurface)ConfigurePsg1MenuLegend();
            controls.SetSiblingIndex(statusBar.GetSiblingIndex() + 1);
            var legendGroup = controls.GetComponent<CanvasGroup>();
            if (!legendGroup) legendGroup = controls.gameObject.AddComponent<CanvasGroup>();
            legendGroup.interactable = false;
            legendGroup.blocksRaycasts = false;
            legendGroup.alpha = Application.isPlaying && controllerLegendHideAt >= 0f
                ? Mathf.Clamp01((controllerLegendHideAt - Time.unscaledTime) / .4f) : 1f;
            controls.gameObject.SetActive(showHomeHud && legendHeight > 0f);
            LayoutMonitorInstructions();
        }

        // Use the same controller artwork as the pre-battle guide, with native text on the button skins.
        private void ConfigurePsg1MenuLegend()
        {
            if(!controls)return;
            var dpad=controls.Find("D-Pad Hint");var a=controls.Find("A Hint");var b=controls.Find("B Hint");
            var label=dpad?dpad.Find("Label")?.GetComponent<UnityEngine.UI.Text>():null;
            if(!dpad||!a||!b||!label)return;
            var art=Resources.Load<Psg1BattleGuideArt>("Psg1BattleGuideArt");
            if(!art||!art.dpad||!art.face||!art.shoulder)return;
            var existing=controls.Find("Extended PSG1 hints") as RectTransform;
            if(existing&&existing.Find("Guide artwork")&&psgLegendArtRevision==ScreenUpdates.Revision&&psgLegendSelect==art.select&&psgLegendStart==art.start)return;
            if(!existing){var holder=new GameObject("Extended PSG1 hints",typeof(RectTransform));holder.layer=controls.gameObject.layer;existing=(RectTransform)holder.transform;existing.SetParent(controls,false);HudBounds(existing,0,0,1,1);}
            foreach(Transform old in existing){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            psgLegendArtRevision=ScreenUpdates.Revision;psgLegendSelect=art.select;psgLegendStart=art.start;
            dpad.gameObject.SetActive(false);a.gameObject.SetActive(false);b.gameObject.SetActive(false);
            var artwork=new GameObject("Guide artwork",typeof(RectTransform));artwork.layer=controls.gameObject.layer;
            var rect=(RectTransform)artwork.transform;rect.SetParent(existing,false);HudBounds(rect,0,0,1,1);
            var dpadArt=ScreenContent.Sprite(BattleGuideScreen.Psg1ScreenId,"dpad",art.dpad);
            var faceArt=ScreenContent.Sprite(BattleGuideScreen.Psg1ScreenId,"face",art.face);
            var shoulderArt=ScreenContent.Sprite(BattleGuideScreen.Psg1ScreenId,"shoulder",art.shoulder);
            LegendButton(label,rect,dpadArt,null,"NAVIGATE",.035f,.53f,.38f,.86f,.20f);
            LegendButton(label,rect,faceArt,"A","SELECT",.39f,.53f,.58f,.86f,.26f);
            LegendButton(label,rect,faceArt,"B","BACK",.59f,.53f,.77f,.86f,.27f);
            LegendButton(label,rect,faceArt,"X","PROFILE",.78f,.53f,.97f,.86f,.24f);
            LegendButton(label,rect,shoulderArt,"L1",null,.035f,.12f,.105f,.45f,1);
            LegendButton(label,rect,shoulderArt,"R1",null,.11f,.12f,.18f,.45f,1);
            LegendText(label,rect,"CHANGE PAGE",.18f,.12f,.39f,.45f);
            var select=ScreenContent.Sprite(BattleGuideScreen.Psg1ScreenId,"select",art.select);
            var start=ScreenContent.Sprite(BattleGuideScreen.Psg1ScreenId,"start",art.start);
            if(select)LegendButton(label,rect,select,null,"SETTINGS",.40f,.12f,.68f,.45f,.43f);
            else LegendText(label,rect,"SELECT  SETTINGS",.40f,.12f,.68f,.45f);
            if(start)LegendButton(label,rect,start,null,"SELECT TANK",.69f,.12f,.97f,.45f,.38f);
            else LegendText(label,rect,"START  SELECT TANK",.69f,.12f,.97f,.45f);
        }
        private static void LegendButton(UnityEngine.UI.Text template,RectTransform parent,Sprite sprite,string key,string caption,float left,float bottom,float right,float top,float iconWidth)
        {
            var obj=new GameObject((key??"Dpad")+" icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));obj.layer=parent.gameObject.layer;
            var rect=(RectTransform)obj.transform;rect.SetParent(parent,false);HudBounds(rect,left,bottom,Mathf.Lerp(left,right,iconWidth),top);
            var image=obj.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.raycastTarget=false;
            if(!string.IsNullOrEmpty(key))LegendText(template,rect,key,.12f,.10f,.88f,.94f,key.Length>1?16:23);
            if(!string.IsNullOrEmpty(caption))LegendText(template,parent,caption,Mathf.Lerp(left,right,iconWidth)+.008f,bottom,right,top);
        }
        private static void LegendText(UnityEngine.UI.Text template,RectTransform parent,string value,float left,float bottom,float right,float top,int size=23)
        {
            var copy=Instantiate(template.gameObject,parent,false);copy.name=value;copy.SetActive(true);
            var text=copy.GetComponent<UnityEngine.UI.Text>();text.text=value;text.alignment=TextAnchor.MiddleCenter;
            text.fontSize=text.resizeTextMaxSize=size;text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Min(15,size);text.raycastTarget=false;
            HudBounds((RectTransform)copy.transform,left,bottom,right,top);
        }

        private static void FitPsg1StatusCard(RectTransform card, int index)
        {
            var readout = card.Find("Readout") as RectTransform;
            if (!readout) return;
            HudBounds(readout, 0f, 0f, 1f, 1f);
            readout.localScale = Vector3.one;
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
            float width = card.rect.width, height = card.rect.height;
            FitCompactStatusIcon(card, index, out float textLeft, out float textWidth);
            if (label)
            {
                Place(label.rectTransform, textLeft, height * .10f, textWidth, height * .39f);
                FitHudText(label, 42, TextAnchor.MiddleLeft);
            }
            if (index == 0)
            {
                FitCompactCommanderRow(card, readout, value, textLeft, false);
            }
            else if (value)
            {
                Place(value.rectTransform, textLeft, height * .43f, textWidth, height * .47f);
                FitHudText(value, 56, TextAnchor.MiddleLeft);
            }
        }

        public void FitPreBattleScreen(RectTransform screen)
        {
            if (!screen) return;
            if ((lastPlatform == MainMenuPlatform.Web || lastPlatform == MainMenuPlatform.Psg1 ||
                 lastPlatform == MainMenuPlatform.AndroidLandscape) &&
                mainFrame.Find("TV Background Viewport") is RectTransform opening)
            {
                // The visible glass is the content boundary on every TV layout.
                // Use its full area with one small rim clearance.
                const float contentClearance = 12f;
                var min = mainFrame.InverseTransformPoint(opening.TransformPoint(
                    new Vector3(opening.rect.xMin, opening.rect.yMin)));
                var max = mainFrame.InverseTransformPoint(opening.TransformPoint(
                    new Vector3(opening.rect.xMax, opening.rect.yMax)));
                screen.localScale = Vector3.one;
                Place(screen, min.x - mainFrame.rect.xMin + contentClearance,
                    mainFrame.rect.yMax - max.y + contentClearance,
                    Mathf.Max(1f, max.x - min.x - contentClearance * 2f),
                    Mathf.Max(1f, max.y - min.y - contentClearance * 2f));
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
