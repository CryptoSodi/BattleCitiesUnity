using UnityEngine;

namespace BattleCities
{
    /// <summary>One readable HUD coordinate system for native and browser render sizes.</summary>
    public readonly struct BattleHudLayout
    {
        public readonly float Scale, Width, Height;
        public bool Narrow => Width<920;
        public float TopHeight => Narrow?84:48;
        public float TopHeightPixels => TopHeight*Scale;
        public float PickupSizePixels => 96*Scale;
        public float DetailScale => Mathf.Clamp(Width/1240f,.78f,1.15f);
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
                    float minimumScale=1;
#if UNITY_WEBGL && !UNITY_EDITOR
                    minimumScale=Mathf.Max(1,BattleCitiesHudPixelRatio());
#endif
                    current=new BattleHudLayout(Screen.width,Screen.height,minimumScale);frame=Time.frameCount;
                }
                return current;
            }
        }
        public static float PickupWorldSize(Camera camera,Vector3 position)
        {
            float pixels=new BattleHudLayout(camera.pixelWidth,camera.pixelHeight).PickupSizePixels;
            float viewHeight=camera.orthographic?2*camera.orthographicSize:
                2*Mathf.Max(camera.nearClipPlane,Vector3.Dot(position-camera.transform.position,camera.transform.forward))*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            return pixels*viewHeight/Mathf.Max(1,camera.pixelHeight);
        }
    }
}
