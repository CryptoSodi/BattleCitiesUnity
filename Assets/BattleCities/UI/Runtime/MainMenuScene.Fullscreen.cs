using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        WebFullscreenButton fullscreenButton;
        Button originalSettingsButton,webSettingsButton,webAvatarButton;
        Image playerIcon;
        Sprite originalPlayerIcon;
        bool originalSocketRaycast, socketRaycastCaptured;
        bool webPortraitApplied;
        Vector2 portraitAnchorMin,portraitAnchorMax,portraitOffsetMin,portraitOffsetMax;
        Vector3 portraitScale;

        void ConfigureWebHeaderActions(bool web)
        {
            if(!originalSettingsButton)originalSettingsButton=settingsButton;
            var socket=statusBar?statusBar.Find("Player/Icon tile"):null;
            if(!socket||!originalSettingsButton)return;
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
                webSettingsButton.onClick.AddListener(OpenSettings);
                var icon=new GameObject("Settings icon",typeof(RectTransform),typeof(Image));icon.transform.SetParent(go.transform,false);
                var r=(RectTransform)icon.transform;r.anchorMin=new Vector2(.08f,.08f);r.anchorMax=new Vector2(.92f,.92f);r.offsetMin=r.offsetMax=Vector2.zero;
                var art=icon.GetComponent<Image>();art.sprite=theme.SettingsIcon;art.preserveAspect=true;art.raycastTarget=false;
            }
            if(web&&!webAvatarButton){
                webAvatarButton=socket.GetComponent<Button>();if(!webAvatarButton)webAvatarButton=socket.gameObject.AddComponent<Button>();
                webAvatarButton.hideFlags=HideFlags.DontSaveInEditor|HideFlags.DontSaveInBuild;
                webAvatarButton.transition=Selectable.Transition.None;webAvatarButton.targetGraphic=socket.GetComponent<Image>();
                webAvatarButton.onClick.RemoveListener(OpenOwnProfile);webAvatarButton.onClick.AddListener(OpenOwnProfile);
            }
            if(webAvatarButton)webAvatarButton.enabled=web;
            originalSettingsButton.enabled=!web;
            if(webSettingsButton)webSettingsButton.gameObject.SetActive(web);
            settingsButton=web&&webSettingsButton?webSettingsButton:originalSettingsButton;
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
            bool web=lastPlatform==MainMenuPlatform.Web;
            ConfigureWebHeaderActions(web);
            bool visible=web&&WebFullscreenButton.Supported;
#if !UNITY_EDITOR
            visible&=RuntimePlatformInfo.IsWeb;
#endif
            if(!visible){
                if(fullscreenButton)fullscreenButton.gameObject.SetActive(false);
                if(settingsButton&&fullscreenButton&&settingsButton.navigation.selectOnRight==fullscreenButton){var links=settingsButton.navigation;links.selectOnRight=startButton;settingsButton.navigation=links;}
                if(web&&webSettingsButton){var r=(RectTransform)webSettingsButton.transform;Place(r,content.rect.width-56,8,48,48);}
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
            float right=corner.x-content.rect.xMin,top=content.rect.yMax-corner.y;
            var canvas=content.GetComponentInParent<Canvas>();
            float size=WebFullscreenButton.TargetSize(RuntimePlatformInfo.IsMobileBrowser,BattleHudLayout.BrowserPixelRatio,canvas?canvas.scaleFactor:1);
            Place((RectTransform)fullscreenButton.transform,right-size,Mathf.Max(8,(top-size)*.5f),size,size);
            if(webSettingsButton)Place((RectTransform)webSettingsButton.transform,right-size*2-8,Mathf.Max(8,(top-size)*.5f),size,size);
            fullscreenButton.FitSkin();
            fullscreenButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=settingsButton,selectOnDown=startButton};
            if(settingsButton){var links=settingsButton.navigation;links.selectOnRight=fullscreenButton;settingsButton.navigation=links;}
        }
    }
}
