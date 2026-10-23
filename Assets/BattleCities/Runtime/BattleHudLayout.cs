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
        public const float TouchPickupSize=72;
        public float PickupSizePixels => (RuntimePlatformInfo.UsesTouchGameplay?TouchPickupSize:48)*Scale;
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
        public static BattleHudLayout ForViewport(float width,float height)
        {
            float minimumScale=1;
#if UNITY_WEBGL && !UNITY_EDITOR
            minimumScale=Mathf.Max(1,BattleCitiesHudPixelRatio());
#endif
            return new BattleHudLayout(width,height,minimumScale);
        }
        public static float PickupWorldSize(Camera camera,Vector3 position)
        {
            float pixels=ForViewport(camera.pixelWidth,camera.pixelHeight).PickupSizePixels;
            float viewHeight=camera.orthographic?2*camera.orthographicSize:
                2*Mathf.Max(camera.nearClipPlane,Vector3.Dot(position-camera.transform.position,camera.transform.forward))*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            return pixels*viewHeight/Mathf.Max(1,camera.pixelHeight);
        }
    }
}
