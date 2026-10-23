using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        WebFullscreenButton fullscreenButton;
        void LayoutWebFullscreen()
        {
            bool visible=lastPlatform==MainMenuPlatform.Web&&WebFullscreenButton.Supported;
#if !UNITY_EDITOR
            visible&=RuntimePlatformInfo.IsWeb;
#endif
            if(!visible){
                if(fullscreenButton)fullscreenButton.gameObject.SetActive(false);
                if(settingsButton&&fullscreenButton&&settingsButton.navigation.selectOnRight==fullscreenButton){var links=settingsButton.navigation;links.selectOnRight=startButton;settingsButton.navigation=links;}
                return;
            }
            if(!content||!leaderboard)return;
            if(!fullscreenButton){
                var art=RemoteGameArt.Load<PreBattleArt>("PreBattleArt");if(!art)return;
                var existing=content.Find("Web fullscreen");
                var go=existing?existing.gameObject:new GameObject("Web fullscreen",typeof(RectTransform),typeof(Image),typeof(WebFullscreenButton));go.layer=content.gameObject.layer;go.transform.SetParent(content,false);
                fullscreenButton=go.GetComponent<WebFullscreenButton>();fullscreenButton.Initialize(art);
            }
            fullscreenButton.gameObject.SetActive(true);
            var corners=new Vector3[4];leaderboard.GetWorldCorners(corners);
            var corner=content.InverseTransformPoint(corners[2]);
            float right=corner.x-content.rect.xMin,top=content.rect.yMax-corner.y;
            var canvas=content.GetComponentInParent<Canvas>();
            float size=WebFullscreenButton.TargetSize(RuntimePlatformInfo.IsMobileBrowser,BattleHudLayout.BrowserPixelRatio,canvas?canvas.scaleFactor:1);
            Place((RectTransform)fullscreenButton.transform,right-size,Mathf.Max(8,(top-size)*.5f),size,size);
            fullscreenButton.FitSkin();
            fullscreenButton.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=settingsButton,selectOnDown=startButton};
            if(settingsButton){var links=settingsButton.navigation;links.selectOnRight=fullscreenButton;settingsButton.navigation=links;}
        }
    }
}
