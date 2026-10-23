using UnityEngine;

namespace BattleCities
{
    /// <summary>One readable HUD coordinate system for native and browser render sizes.</summary>
    public readonly struct BattleHudLayout
    {
        public readonly float Scale, Width, Height;
        public bool Narrow => Width<920;
        public float TopHeight => 48;
        public float TopHeightPixels => TopHeight*Scale;
        public float DetailScale => Mathf.Clamp(Width/1240f,.78f,1.15f);
        public float OverlayScale => DetailScale*.7f;
        public const float AndroidPowerupSlotWidth=52,AndroidPowerupGap=4,AndroidPowerupIconSize=40;
        public float AndroidPowerupWidth => AndroidPowerupSlotWidth*4+AndroidPowerupGap*3+12;
        public BattleHudLayout(float width,float height,float minimumScale=1)
        {
            width=Mathf.Max(1,width);height=Mathf.Max(1,height);
            Scale=Mathf.Max(Mathf.Max(1,minimumScale),height>width?Mathf.Min(width/720f,height/1280f):Mathf.Min(width/1280f,height/720f));
            Width=width/Scale;Height=height/Scale;
        }
        static int frame=-1;
        static BattleHudLayout current;
#if UNITY_EDITOR
        public static float PreviewPixelRatio=1;
        public static bool PreviewMobileBrowser;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern float BattleCitiesHudPixelRatio();
#endif
        public static BattleHudLayout Current
        {
            get
            {
                if(frame!=Time.frameCount)
                {
                    current=ForViewport(Screen.width,Screen.height);frame=Time.frameCount;
                }
                return current;
            }
        }
        public static float BrowserPixelRatio
        {
            get {
#if UNITY_EDITOR
                return Mathf.Max(1,PreviewPixelRatio);
#elif UNITY_WEBGL
                return Mathf.Max(1,BattleCitiesHudPixelRatio());
#else
                return 1;
#endif
            }
        }
        public static BattleHudLayout ForViewport(float width,float height)
        {
            bool nativeMobileScale=RuntimePlatformInfo.UsesTouchGameplay;
#if UNITY_EDITOR
            nativeMobileScale|=PreviewMobileBrowser;
#endif
            // Mobile Web mirrors the APK's render-viewport scale. Applying DPR as
            // a minimum here doubles/triples its header and squeezes the roster.
            return new BattleHudLayout(width,height,nativeMobileScale?1:BrowserPixelRatio);
        }
    }
}
