using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        WebFullscreenButton fullscreenButton;
        Button originalSettingsButton,webSettingsButton,webAvatarButton;
        bool webHeaderEnabled;
        Button ActiveSettingsButton => webHeaderEnabled && webSettingsButton ? webSettingsButton : settingsButton;
        Image playerIcon;
        Sprite originalPlayerIcon;
        bool originalSocketRaycast, socketRaycastCaptured;
        bool webPortraitApplied;
        Vector2 portraitAnchorMin,portraitAnchorMax,portraitOffsetMin,portraitOffsetMax;
        Vector3 portraitScale;

        void ConfigureWebHeaderActions(bool web)
        {
            var sceneSettings=statusBar?statusBar.Find("Player")?.GetComponent<Button>():null;
            if(sceneSettings){originalSettingsButton=sceneSettings;if(settingsButton!=sceneSettings)settingsButton=sceneSettings;}
            else if(!originalSettingsButton)originalSettingsButton=settingsButton;
            var socket=statusBar?statusBar.Find("Player/Icon tile"):null;
            if(!socket||!originalSettingsButton)return;
            webHeaderEnabled=web;
            var socketImage=socket.GetComponent<Image>();
            if(socketImage&&!socketRaycastCaptured){originalSocketRaycast=socketImage.raycastTarget;socketRaycastCaptured=true;}
            if(socketImage)socketImage.raycastTarget=web||originalSocketRaycast;
            if(!playerIcon){playerIcon=socket.Find("Icon")?.GetComponent<Image>();if(playerIcon)originalPlayerIcon=playerIcon.sprite;}
            if(web&&!webSettingsButton){
                var go=new GameObject("Web settings",typeof(RectTransform),typeof(Image),typeof(Button));
                go.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
                go.transform.SetParent(content,false);go.layer=content.gameObject.layer;
                var image=go.GetComponent<Image>();var source=socket.GetComponent<Image>();
                if(source){image.sprite=source.sprite;image.type=Image.Type.Simple;image.preserveAspect=true;}
                webSettingsButton=go.GetComponent<Button>();webSettingsButton.targetGraphic=image;
                var icon=new GameObject("Settings icon",typeof(RectTransform),typeof(Image));icon.transform.SetParent(go.transform,false);
                var r=(RectTransform)icon.transform;r.anchorMin=new Vector2(.08f,.08f);r.anchorMax=new Vector2(.92f,.92f);r.offsetMin=r.offsetMax=Vector2.zero;
                var art=icon.GetComponent<Image>();art.sprite=theme.SettingsIcon;art.preserveAspect=true;art.raycastTarget=false;
            }
            if(web&&webSettingsButton){webSettingsButton.onClick.RemoveListener(OpenSettings);webSettingsButton.onClick.AddListener(OpenSettings);}
            if(web&&!webAvatarButton){
                webAvatarButton=socket.GetComponent<Button>();if(!webAvatarButton)webAvatarButton=socket.gameObject.AddComponent<Button>();
                webAvatarButton.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
                webAvatarButton.transition=Selectable.Transition.None;webAvatarButton.targetGraphic=socket.GetComponent<Image>();
            }
            if(web&&webAvatarButton){webAvatarButton.onClick.RemoveListener(OpenOwnProfile);webAvatarButton.onClick.AddListener(OpenOwnProfile);}
            if(webAvatarButton)webAvatarButton.enabled=web;
            originalSettingsButton.enabled=!web;
            if(webSettingsButton)webSettingsButton.gameObject.SetActive(web);
            var profile=web?RemoteGameArt.Load<PlayerProfileArt>("PlayerProfileArt"):null;
            if(playerIcon){
                playerIcon.sprite=profile&&profile.commander?profile.commander:originalPlayerIcon;playerIcon.preserveAspect=true;
                var rect=playerIcon.rectTransform;
                if(web){
                    if(!webPortraitApplied){portraitAnchorMin=rect.anchorMin;portraitAnchorMax=rect.anchorMax;portraitOffsetMin=rect.offsetMin;portraitOffsetMax=rect.offsetMax;portraitScale=rect.localScale;}
                    rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one*1.08f;webPortraitApplied=true;
                }else if(webPortraitApplied){
                    rect.anchorMin=portraitAnchorMin;rect.anchorMax=portraitAnchorMax;rect.offsetMin=portraitOffsetMin;rect.offsetMax=portraitOffsetMax;rect.localScale=portraitScale;webPortraitApplied=false;
                }
            }
        }
        void LayoutWebFullscreen()
        {
            bool web=lastPlatform==MainMenuPlatform.Web||ResponsiveWebLayout.IsBrowserSurface;
            bool psg1=lastPlatform==MainMenuPlatform.Psg1&&!ResponsiveWebLayout.IsBrowserSurface;
            ConfigureWebHeaderActions(web||psg1);
            bool visible=web&&WebFullscreenButton.Supported;
#if !UNITY_EDITOR
            visible&=RuntimePlatformInfo.IsWeb;
#endif
            if(!visible){
                if(fullscreenButton)fullscreenButton.gameObject.SetActive(false);
                // A previously authored scene can contain this web-only object
                // before fullscreenButton has been initialized in this session.
                var savedFullscreen=content?content.Find("Web fullscreen"):null;
                if(savedFullscreen)savedFullscreen.gameObject.SetActive(false);
                var activeSettings=ActiveSettingsButton;
                if(activeSettings&&fullscreenButton&&activeSettings.navigation.selectOnRight==fullscreenButton){var links=activeSettings.navigation;links.selectOnRight=startButton;activeSettings.navigation=links;}
                if(psg1&&webSettingsButton&&statusBar&&navigation){
                    var navCorners=new Vector3[4];navigation.GetWorldCorners(navCorners);
                    var navBottom=content.InverseTransformPoint(navCorners[0]);
                    var navTop=content.InverseTransformPoint(navCorners[2]);
                    float navRight=navTop.x-content.rect.xMin;
                    float gapWidth=Mathf.Max(1f,content.rect.width-navRight);
                    float settingsSize=Mathf.Min(Mathf.Clamp(statusBar.rect.height*.70f,56f,92f),Mathf.Max(1f,gapWidth-16f));
                    float settingsX=navRight+(gapWidth-settingsSize)*.5f;
                    float settingsY=content.rect.yMax-navTop.y+(navTop.y-navBottom.y-settingsSize)*.5f;
                    Place((RectTransform)webSettingsButton.transform,settingsX,settingsY,settingsSize,settingsSize);
                    webSettingsButton.gameObject.SetActive(!IsModalOpen&&!IsTvScreenOpen);
                }else if(web&&webSettingsButton){var r=(RectTransform)webSettingsButton.transform;Place(r,content.rect.width-56,8,48,48);}
                return;
            }
            if(!content||!leaderboard)return;
            if(!fullscreenButton){
                var art=RemoteGameArt.Load<PreBattleArt>("PreBattleArt");if(!art)return;
                var existing=content.Find("Web fullscreen");
                var go=existing?existing.gameObject:new GameObject("Web fullscreen",typeof(RectTransform),typeof(Image),typeof(WebFullscreenButton));go.layer=content.gameObject.layer;go.transform.SetParent(content,false);
                fullscreenButton=go.GetComponent<WebFullscreenButton>();fullscreenButton.Initialize(art);
            }
            var socket=statusBar.Find("Player/Icon tile");
            fullscreenButton.SetFrame(socket?socket.GetComponent<Image>()?.sprite:null);
            fullscreenButton.gameObject.SetActive(true);
            var corners=new Vector3[4];leaderboard.GetWorldCorners(corners);
            var corner=content.InverseTransformPoint(corners[2]);
            float right=corner.x-content.rect.xMin;
            if(lastPlatform!=MainMenuPlatform.Web){mainFrame.GetWorldCorners(corners);right=content.InverseTransformPoint(corners[2]).x-content.rect.xMin-8;}
            float top=lastPlatform==MainMenuPlatform.Web?content.rect.yMax-corner.y:0;
            var canvas=content.GetComponentInParent<Canvas>();
            float size=WebFullscreenButton.TargetSize(RuntimePlatformInfo.IsMobileBrowser,BattleHudLayout.BrowserPixelRatio,canvas?canvas.scaleFactor:1);
#if UNITY_EDITOR
            bool mobile=RuntimePlatformInfo.IsMobileBrowser||BattleHudLayout.PreviewMobileBrowser;
#else
            bool mobile=RuntimePlatformInfo.IsMobileBrowser;
#endif
            if(lastPlatform!=MainMenuPlatform.Web){
                float targetPixels=(mobile?64:48)*BattleHudLayout.BrowserPixelRatio;
                size=targetPixels/Mathf.Max(.001f,(canvas?canvas.scaleFactor:1)*Mathf.Abs(content.localScale.x));
            }
            float y=Mathf.Max(8,(top-size)*.5f);
            if(lastPlatform!=MainMenuPlatform.Web){
                var hudCorners=new Vector3[4];statusBar.GetWorldCorners(hudCorners);
                var bottom=content.InverseTransformPoint(hudCorners[0]);y=Mathf.Max(8,content.rect.yMax-bottom.y+8);
                if(lastPlatform==MainMenuPlatform.Psg1&&ResponsiveWebLayout.IsBrowserSurface)
                    right=MonitorOpeningInContent().xMax-16;
                if(lastPlatform==MainMenuPlatform.AndroidLandscape){
                    // Keep the actions inside the TV's opening and on the same
                    // header line as the score whenever there is room.
                    var opening=MonitorOpeningInContent();right=opening.xMax-4;
                    var hudTopRight=content.InverseTransformPoint(hudCorners[2]);
                    float hudRight=hudTopRight.x-content.rect.xMin;
                    float hudTop=content.rect.yMax-hudTopRight.y;
                    float actionsLeft=right-size*2-8;
                    float shift=Mathf.Max(0,hudRight+8-actionsLeft);
                    float hudLeft=content.InverseTransformPoint(hudCorners[0]).x-content.rect.xMin;
                    if(shift<=hudLeft-opening.x-4){
                        if(shift>0)Place(statusBar,hudLeft-shift,hudTop,statusBar.rect.width,statusBar.rect.height);
                        y=Mathf.Max(opening.y+4,hudTop);
                    }
                }
            }
            bool showActions=lastPlatform==MainMenuPlatform.Web||(!IsModalOpen&&!IsTvScreenOpen);
            fullscreenButton.gameObject.SetActive(showActions);if(webSettingsButton)webSettingsButton.gameObject.SetActive(showActions);
            Place((RectTransform)fullscreenButton.transform,right-size,y,size,size);
            if(webSettingsButton)Place((RectTransform)webSettingsButton.transform,right-size*2-8,y,size,size);
            fullscreenButton.FitSkin();
            var settings=ActiveSettingsButton;
            fullscreenButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=settings,selectOnDown=startButton};
            if(settings){var links=settings.navigation;links.selectOnRight=fullscreenButton;settings.navigation=links;}
        }
    }
}
