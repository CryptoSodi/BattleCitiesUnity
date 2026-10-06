using TMPro;
using UnityEngine;

namespace BattleCities.UI
{
    /// <summary>The Inventory typography shared by TV detail sidebars.</summary>
    internal static class TvSidebarStyle
    {
        public const float HeaderHeight=42f;
        public static bool LargerText(MainMenuPlatform platform)=>platform==MainMenuPlatform.Android||platform==MainMenuPlatform.AndroidLandscape||platform==MainMenuPlatform.Psg1;
        public static float HeadingSize(bool larger)=>larger?32f:24f;
        public static float BodySize(bool larger)=>larger?28f:20f;
        public static float CountSize(bool larger)=>larger?36f:28f;
        public static void Apply(TMP_Text text,float size,ArcadeTextStyles styles)
        {
            text.fontSize=text.fontSizeMax=size;
            styles.Apply(text,ArcadeTextTreatment.PrizeAmount);
        }
        public static void PlaceHeaderIcon(UnityEngine.UI.Image image,Rect visibleBounds)
        {
            if(!image||!image.sprite)return;
            var parent=(RectTransform)image.transform.parent;
            var slot=new Rect(parent.rect.width*.035f,parent.rect.height*.10f,parent.rect.width*.18f,parent.rect.height*.80f);
            float aspect=image.sprite.rect.width/image.sprite.rect.height;
            float width=Mathf.Min(slot.width/visibleBounds.width,slot.height*aspect/visibleBounds.height),height=width/aspect;
            float left=slot.x+(slot.width-width*visibleBounds.width)*.5f-width*visibleBounds.x;
            float top=slot.y+(slot.height-height*visibleBounds.height)*.5f-height*(1-visibleBounds.y-visibleBounds.height);
            MainMenuScene.Place(image.rectTransform,left,top,width,height);
            image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;
        }
        // Alpha bounds of the supplied 1254-square crate, independent of import resolution.
        public static readonly Rect InventoryCrateBounds=new Rect(222f/1254f,223f/1254f,846f/1254f,740f/1254f);
    }
}
