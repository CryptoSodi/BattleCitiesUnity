using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
        RectTransform linkNotice,linkPanel;
        UnityEngine.UI.Button linkOpen,linkDone;
        TMP_Text linkHeading,linkBody,linkOpenCaption,linkDoneCaption;
        public bool IsLinkNoticeOpen=>linkNotice&&linkNotice.gameObject.activeSelf;
        void BuildLinkInstructions()
        {
            linkNotice=Panel("Browser linking",root,null);Fit(linkNotice,new Rect(0,0,1,1));linkNotice.GetComponent<UnityEngine.UI.Image>().color=new Color(0,.025f,.08f,.75f);linkNotice.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            linkPanel=Panel("Panel",linkNotice,theme.CreamPanel);linkPanel.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=2;
            linkHeading=Label("Title",linkPanel,"LINK YOUR ACCOUNT",new Rect(.05f,.04f,.90f,.15f),32,ArcadeTextTreatment.PrizeAmount);
            linkBody=Label("Instructions",linkPanel,"",new Rect(.07f,.23f,.86f,.48f),26,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);linkBody.textWrappingMode=TextWrappingModes.Normal;
            linkOpen=Button("Open browser",linkPanel,()=>OpenLink("https://battlecities.com","LINK YOUR ACCOUNT IN THE BROWSER, THEN RETURN"));Fit((RectTransform)linkOpen.transform,new Rect(.06f,.78f,.43f,.16f));
            linkOpenCaption=Label("Label",linkOpen.transform,"OPEN BROWSER",new Rect(.04f,.04f,.92f,.92f),28,ArcadeTextTreatment.PrizeAmount);
            linkDone=Button("Check status",linkPanel,()=>{HideLinkInstructions();Refresh();});Fit((RectTransform)linkDone.transform,new Rect(.51f,.78f,.43f,.16f));
            linkDoneCaption=Label("Label",linkDone.transform,"CHECK STATUS",new Rect(.04f,.04f,.92f,.92f),28,ArcadeTextTreatment.PrizeAmount);
            Link(linkOpen,linkDone,linkDone,linkDone,linkDone);Link(linkDone,linkOpen,linkOpen,linkOpen,linkOpen);linkNotice.gameObject.SetActive(false);
        }
        void ShowLinkInstructions(string provider)
        {
            linkHeading.text="LINK "+provider.ToUpperInvariant();
            linkBody.text="1. SIGN IN WITH THE SAME WALLET ON BATTLECITIES.COM.\n2. OPEN SOCIALS AND COMPLETE THE "+provider.ToUpperInvariant()+" LINK.\n3. RETURN HERE AND SELECT CHECK STATUS.";
            linkNotice.gameObject.SetActive(true);linkNotice.SetAsLastSibling();LayoutLinkInstructions();Focus(linkOpen);
        }
        void HideLinkInstructions(bool restoreFocus=true){if(linkNotice)linkNotice.gameObject.SetActive(false);if(restoreFocus)FocusFirst();}
        void LayoutLinkInstructions()
        {
            if(!linkPanel)return;float w=Mathf.Min(root.rect.width-32,680),h=Mathf.Min(root.rect.height-40,320);
            MainMenuScene.Place(linkPanel,(root.rect.width-w)*.5f,(root.rect.height-h)*.5f,w,h);
            TvTitleHeaderLayout.FitSkin(linkOpen.image,h*.16f);TvTitleHeaderLayout.FitSkin(linkDone.image,h*.16f);
        }
        void StyleLinkAction(UnityEngine.UI.Button button,TMP_Text text){var sprite=button.image.overrideSprite?button.image.overrideSprite:button.image.sprite;styles.ApplyCleanButton(text,sprite==art.tankCostButtonSelected);}
    }
}
