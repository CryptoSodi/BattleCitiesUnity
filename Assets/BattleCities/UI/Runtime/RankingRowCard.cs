using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    // Shared live-data card used by the home top ten, pinned current player and full rankings.
    [ExecuteAlways]
    public sealed class RankingRowCard : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        Image background,badge,portrait,focusTint;Outline focus;
        TMP_Text rank,nameLabel,score,matches,detail;
        MainMenuApiClient.RankingRow data;RankingCardArt artwork;MenuTheme theme;
        bool yours,initialized,hovered,selected;
        public MainMenuApiClient.RankingRow Data=>data;
        public bool IsCurrentPlayer=>yours;
        public bool WideMatchesColumn {get;set;}
        public void Configure(MainMenuApiClient.RankingRow row,int index,bool current,MenuTheme skin)
        {
            data=row;yours=current;theme=skin;
            if(!initialized)Build();
            artwork=Resources.Load<RankingCardArt>("RankingCardArt");
            int visualIndex=row.rank>0?row.rank-1:index;
            var sprite=artwork?artwork.Row(row.rank,visualIndex,false):null;
            background.sprite=sprite?sprite:theme.Rounded;background.type=Image.Type.Sliced;background.raycastTarget=GetComponent<Button>();
            background.color=sprite?Color.white:row.rank==1?new Color32(255,221,99,255):row.rank==2?new Color32(226,233,240,255):row.rank==3?new Color32(255,179,112,255):visualIndex%2==0?new Color32(203,233,255,255):new Color32(255,246,224,255);
            badge.sprite=artwork?artwork.Medal(row.rank):null;
            bool medal=badge.sprite;badge.sprite=medal?badge.sprite:theme.Rounded;
            badge.type=medal?Image.Type.Simple:Image.Type.Sliced;badge.preserveAspect=medal;
            badge.color=medal?Color.white:new Color32(185,220,243,255);
            rank.text=row.rank>0?row.rank.ToString(CultureInfo.InvariantCulture):"—";
            styles.Apply(rank,medal?ArcadeTextTreatment.WhiteButton:ArcadeTextTreatment.PrizeAmount);
            nameLabel.text=current?"YOU":string.IsNullOrWhiteSpace(row.displayName)?"PLAYER":row.displayName;
            score.text=row.pointsKnown?row.totalPoints.ToString("N0",CultureInfo.InvariantCulture):"—";
            matches.text=row.matchesKnown?row.matches.ToString("N0",CultureInfo.InvariantCulture):"—";
            foreach(var text in new[]{nameLabel,score,matches})styles.Apply(text,ArcadeTextTreatment.PrizeAmount);
            detail.text="YOUR RANK";styles.Apply(detail,ArcadeTextTreatment.PrizeAmount);detail.gameObject.SetActive(current);
            var results=RemoteGameArt.Load<BattleResultsArt>("BattleResultsArt");
            int hash=0;string key=row.playerId??row.displayName??"player";foreach(char ch in key)hash=unchecked(hash*31+ch);
            var portraits=results?results.portraits:null;
            portrait.sprite=portraits!=null&&portraits.Length>0?portraits[(hash&int.MaxValue)%portraits.Length]:null;
            portrait.enabled=portrait.sprite;
            Layout();RefreshFocus();
        }
        void Build()
        {
            background=GetComponent<Image>();if(!background)background=gameObject.AddComponent<Image>();
            badge=Picture("Rank badge");portrait=Picture("Portrait");
            rank=Text("Rank");nameLabel=Text("Player");score=Text("Score");matches=Text("Matches");detail=Text("Detail");
            focusTint=Picture("Focus tint");focusTint.sprite=theme.Rounded;focusTint.type=Image.Type.Sliced;focusTint.color=new Color(0,.55f,1,.12f);focusTint.transform.SetSiblingIndex(0);
            focus=gameObject.GetComponent<Outline>();if(!focus)focus=gameObject.AddComponent<Outline>();focus.effectColor=new Color(0,.86f,1,.95f);focus.effectDistance=new Vector2(2,-2);focus.useGraphicAlpha=false;
            initialized=true;
        }
        Image Picture(string title)
        {
            var existing=transform.Find(title);var go=existing?existing.gameObject:new GameObject(title,typeof(RectTransform),typeof(Image));go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            var image=go.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;return image;
        }
        TMP_Text Text(string title)
        {
            var existing=transform.Find(title);var go=existing?existing.gameObject:new GameObject(title,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            var text=go.GetComponent<TextMeshProUGUI>();text.raycastTarget=false;text.richText=false;text.enableAutoSizing=true;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;text.alignment=TextAlignmentOptions.Center;
            return text;
        }
        public static float NameStart(float width)=>width*.31f;
        public void Layout()
        {
            if(!initialized)return;
            var rect=(RectTransform)transform;float w=rect.rect.width,h=rect.rect.height;if(w<=0||h<=0)return;
            float pad=Mathf.Clamp(h*.09f,3,7),rankWidth=w*.12f,face=Mathf.Min(h-pad*2,w*.15f),nameX=NameStart(w);
            float scoreStart=WideMatchesColumn?.60f:.63f,scoreWidth=WideMatchesColumn?.18f:.22f;
            float matchesStart=WideMatchesColumn?.79f:.86f,matchesWidth=WideMatchesColumn?.20f:.13f;
            float badgeSize=Mathf.Min(rankWidth,h-pad*2);
            MainMenuScene.Place(badge.rectTransform,pad+(rankWidth-badgeSize)*.5f,(h-badgeSize)*.5f,badgeSize,badgeSize);
            MainMenuScene.Place(rank.rectTransform,pad,pad,rankWidth,h-pad*2);
            MainMenuScene.Place(portrait.rectTransform,w*.145f+(w*.15f-face)*.5f,(h-face)*.5f,face,face);
            MainMenuScene.Place(nameLabel.rectTransform,nameX,yours?h*.10f:pad,Mathf.Max(12,w*scoreStart-nameX-7),yours?h*.52f:h-pad*2);
            MainMenuScene.Place(score.rectTransform,w*scoreStart,pad,w*scoreWidth,h-pad*2);
            MainMenuScene.Place(matches.rectTransform,w*matchesStart,pad,w*matchesWidth,h-pad*2);
            MainMenuScene.Place(detail.rectTransform,nameX,h*.58f,Mathf.Max(12,w*scoreStart-nameX-7),h*.30f);
            float size=Mathf.Clamp(h*.51f,18,30);
            foreach(var text in new[]{nameLabel,score,matches}){text.fontSize=text.fontSizeMax=size;text.fontSizeMin=Mathf.Min(14,size);}
            nameLabel.alignment=detail.alignment=TextAlignmentOptions.MidlineLeft;
            rank.fontSize=rank.fontSizeMax=Mathf.Clamp(h*.59f,20,38);rank.fontSizeMin=14;
            detail.fontSize=detail.fontSizeMax=Mathf.Clamp(h*.28f,11,18);detail.fontSizeMin=10;
            var outline=focusTint.rectTransform;outline.anchorMin=Vector2.zero;outline.anchorMax=Vector2.one;outline.offsetMin=outline.offsetMax=Vector2.zero;
            FitSkin(background,h);FitSkin(focusTint,h);if(badge.type==Image.Type.Sliced)FitSkin(badge,h*.65f);
        }
        static void FitSkin(Image image,float height)
        {
            if(!image.sprite)return;var b=image.sprite.border;float edge=Mathf.Max(Mathf.Max(b.x,b.y),Mathf.Max(b.z,b.w));
            image.pixelsPerUnitMultiplier=Mathf.Max(.01f,edge/(Mathf.Max(1,height*.19f)*image.pixelsPerUnit));
        }
        void OnRectTransformDimensionsChange(){Layout();}
        void RefreshFocus(){bool show=GetComponent<Button>()&&(hovered||selected);if(focus)focus.enabled=show;if(focusTint)focusTint.enabled=show;}
        public void OnPointerEnter(PointerEventData e){hovered=true;RefreshFocus();}
        public void OnPointerExit(PointerEventData e){hovered=false;RefreshFocus();}
        public void OnSelect(BaseEventData e){selected=true;RefreshFocus();}
        public void OnDeselect(BaseEventData e){selected=false;RefreshFocus();}
        void OnDisable(){hovered=selected=false;RefreshFocus();}
        void OnDestroy(){styles.Dispose();}
    }
}
