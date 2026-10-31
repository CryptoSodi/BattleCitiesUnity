using UnityEngine;

namespace BattleCities.UI
{
    // Layout choice is deliberately separate from wallet, network and device identity.
    public static class ResponsiveWebLayout
    {
        public static MainMenuPlatform ForViewport(float cssWidth,float cssHeight)
        {
            cssWidth=Mathf.Max(1,cssWidth);cssHeight=Mathf.Max(1,cssHeight);
            bool portrait=cssHeight>cssWidth;
            if(portrait)return MainMenuPlatform.Psg1;
            if(cssWidth<1250 || cssHeight<480 || cssWidth/cssHeight<1.2f)
                return MainMenuPlatform.AndroidLandscape;
            return MainMenuPlatform.Web;
        }

        public static bool IsBrowserSurface
        {
            get {
#if UNITY_EDITOR
                if(!Application.isPlaying && ScreenContent.EditorPlatform!=null)return ScreenContent.EditorPlatform=="web";
#endif
                return RuntimePlatformInfo.IsWeb;
            }
        }
    }
}
