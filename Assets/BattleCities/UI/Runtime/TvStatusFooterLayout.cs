using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>The Shop's canonical white footer dimensions and container skin.</summary>
    internal readonly struct TvStatusFooterLayout
    {
        public const float EdgeInset=4f,SkinScale=3f;
        public static Rect ActionArea=>new Rect(.765f,.11f,.22f,.78f);
        public readonly float Height;
        readonly float width,rootHeight;

        public TvStatusFooterLayout(RectTransform root)
        {
            width=root.rect.width;rootHeight=root.rect.height;
            Height=Mathf.Clamp(rootHeight*.078f,50f,64f);
        }
        public void Place(RectTransform bar)
        {
            MainMenuScene.Place(bar,EdgeInset,rootHeight-Height-EdgeInset,width-EdgeInset*2f,Height);
        }
        public static void PlaceAction(RectTransform action)
        {
            var area=ActionArea;
            action.pivot=new Vector2(.5f,.5f);
            action.anchorMin=new Vector2(area.x,1f-area.y-area.height);
            action.anchorMax=new Vector2(area.x+area.width,1f-area.y);
            action.offsetMin=action.offsetMax=Vector2.zero;
        }
        public static void ApplySkin(Image image,Sprite skin)
        {
            if(!image)return;
            image.sprite=skin;image.type=Image.Type.Sliced;image.preserveAspect=false;
            image.pixelsPerUnitMultiplier=SkinScale;image.color=Color.white;image.raycastTarget=false;
        }
    }
}
