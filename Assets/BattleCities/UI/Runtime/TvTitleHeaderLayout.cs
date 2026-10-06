using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Shared title, icon and navigation sizing inside the TV.</summary>
    internal readonly struct TvTitleHeaderLayout
    {
        public const float TextSize=28f;
        public readonly float Height,IconGroupWidth,NavigationHeight,NavigationWidth;
        readonly float width;

        public TvTitleHeaderLayout(RectTransform root)
        {
            width=root.rect.width;
            Height=Mathf.Clamp(root.rect.height*.083f,46f,64f);
            IconGroupWidth=Mathf.Clamp(width*.19f,112f,210f);
            NavigationHeight=Mathf.Min(Height-12f,49f);
            NavigationWidth=Mathf.Min(width-IconGroupWidth-32f,NavigationHeight*12f);
        }
        public void PlaceTitle(RectTransform plate,RectTransform icon,RectTransform title,bool wideTitle=false)
        {
            MainMenuScene.Place(plate,4,4,width-8,Height);
            if(icon)MainMenuScene.Place(icon,IconGroupWidth*.10f,Height*.12f,IconGroupWidth*.25f,Height*.76f);
            float left=IconGroupWidth*.36f;
            float textWidth=wideTitle?plate.rect.width-NavigationWidth*.25f-20f-left:IconGroupWidth*.57f;
            if(title)MainMenuScene.Place(title,left,Height*.06f,textWidth,Height*.88f);
        }
        public void PlaceNavigation(RectTransform plate,RectTransform bar,bool backOnly=false)
        {
            float barWidth=NavigationWidth*(backOnly?.25f:1f);
            MainMenuScene.Place(bar,plate.rect.width-barWidth-8,(Height-NavigationHeight)*.5f,barWidth,NavigationHeight);
        }
        public static void FitSkin(Image image,float height,float cornerFraction=.22f)
        {
            image.pixelsPerUnitMultiplier=image.sprite.border.x/
                (Mathf.Max(1f,height*cornerFraction)*Mathf.Max(.01f,image.pixelsPerUnit));
        }
    }
}
