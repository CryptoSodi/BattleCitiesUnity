using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        private void ApplyNavigationTypography()
        {
            if(tabs==null)return;
            foreach(var tab in tabs)
            {
                if(!tab)continue;
                var label=tab.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if(!label)continue;
                // Keep the larger caption below the icon and inside the plate's rim.
                var rect=label.rectTransform;
                rect.anchorMin=new Vector2(.06f,.09f);
                rect.anchorMax=new Vector2(.94f,.33f);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                label.alignment=TextAnchor.MiddleCenter;
                label.horizontalOverflow=HorizontalWrapMode.Wrap;
                label.verticalOverflow=VerticalWrapMode.Truncate;
                label.resizeTextForBestFit=true;
                label.resizeTextMinSize=12;
                float height=((RectTransform)tab.transform).rect.height;
                label.fontSize=label.resizeTextMaxSize=Mathf.Max(12,Mathf.RoundToInt(height*.32f));
                ArcadeTextStyles.ApplyNavy(label,theme?theme.HeadingFont:null);
                rect.SetAsLastSibling();
            }
        }
    }
}
