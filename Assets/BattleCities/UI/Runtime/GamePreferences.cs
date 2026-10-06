using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Device-local audio and display preferences, shared by menus and battle scenes.</summary>
    public static class GamePreferences
    {
        public const string MuteKey="battlecities.settings.mute",ScanlineKey="battlecities.settings.scanlines";
        static Canvas overlay;
        public static bool Muted=>PlayerPrefs.GetInt(MuteKey,0)!=0;
        public static bool Scanlines=>PlayerPrefs.GetInt(ScanlineKey,0)!=0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Apply()
        {
            AudioListener.volume=Muted?0:1;
            if(!overlay)
            {
                var existing=GameObject.Find("Battle Cities scanlines");
                if(existing)overlay=existing.GetComponent<Canvas>();
            }
            if(Scanlines&&!overlay)
            {
                var go=new GameObject("Battle Cities scanlines",typeof(RectTransform),typeof(Canvas));
                overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=1000;
                Object.DontDestroyOnLoad(go);
                var lines=new GameObject("Scanlines",typeof(RectTransform),typeof(CanvasRenderer),typeof(ScanlineGraphic));
                lines.transform.SetParent(go.transform,false);
                var rect=(RectTransform)lines.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                var graphic=lines.GetComponent<ScanlineGraphic>();graphic.raycastTarget=false;graphic.color=new Color(0,0,0,.075f);
            }
            if(overlay)overlay.enabled=Scanlines;
        }
        public static void SetMuted(bool value){PlayerPrefs.SetInt(MuteKey,value?1:0);PlayerPrefs.Save();Apply();}
        public static void SetScanlines(bool value){PlayerPrefs.SetInt(ScanlineKey,value?1:0);PlayerPrefs.Save();Apply();}
    }
}
