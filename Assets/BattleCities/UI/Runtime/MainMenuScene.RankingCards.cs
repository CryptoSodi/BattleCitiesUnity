using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        readonly List<RankingRowCard> homeRankCards=new List<RankingRowCard>();
        RectTransform homeCardsRoot,homeRankSeparator,homeStandingsSurface;
        RankingRowCard homeYourRank;
        MainMenuApiClient.RoundSnapshot homeCardData;
        static bool HasPersonalRank(MainMenuApiClient.RankingRow row)=>row!=null&&row.rank>0&&!row.restricted&&!row.guest;
        RankingRowCard CreateHomeCard(string title)
        {
            var existing=homeCardsRoot.Find(title);
            var go=existing?existing.gameObject:new GameObject(title,typeof(RectTransform),typeof(Image),typeof(RankingRowCard));go.layer=scoresPanel.gameObject.layer;
            go.transform.SetParent(homeCardsRoot,false);var card=go.GetComponent<RankingRowCard>();card.WideMatchesColumn=true;return card;
        }
        void RenderHomeRankCards(MainMenuApiClient.RoundSnapshot value)
        {
            homeCardData=value;if(!scoresPanel)return;
            int count=Mathf.Min(10,value?.rows?.Length??0);
            SetHomeRankBackdrop(count>0);
            if(count==0){if(homeCardsRoot)homeCardsRoot.gameObject.SetActive(false);return;}
            if(!homeCardsRoot){
                var existing=scoresPanel.Find("Ranking cards");
                var go=existing?existing.gameObject:new GameObject("Ranking cards",typeof(RectTransform),typeof(Image));go.layer=scoresPanel.gameObject.layer;homeCardsRoot=(RectTransform)go.transform;homeCardsRoot.SetParent(scoresPanel,false);
                homeCardsRoot.anchorMin=Vector2.zero;homeCardsRoot.anchorMax=Vector2.one;homeCardsRoot.offsetMin=homeCardsRoot.offsetMax=Vector2.zero;
                var surface=go.GetComponent<Image>();surface.enabled=false;surface.raycastTarget=false;
                var existingSeparator=homeCardsRoot.Find("Your rank separator");
                var separator=existingSeparator?existingSeparator.gameObject:new GameObject("Your rank separator",typeof(RectTransform),typeof(Image));separator.layer=go.layer;homeRankSeparator=(RectTransform)separator.transform;homeRankSeparator.SetParent(homeCardsRoot,false);
                var line=separator.GetComponent<Image>();line.sprite=theme.Rounded;line.color=new Color32(0,211,255,255);line.raycastTarget=false;
                homeYourRank=CreateHomeCard("Your rank card");
            }
            homeCardsRoot.gameObject.SetActive(true);homeCardsRoot.SetAsLastSibling();
            foreach(Transform old in scoresPanel)if(old.name.StartsWith("Live "))old.gameObject.SetActive(false);
            while(homeRankCards.Count<count)homeRankCards.Add(CreateHomeCard("Rank card "+(homeRankCards.Count+1)));
            for(int i=0;i<homeRankCards.Count;i++){
                homeRankCards[i].gameObject.SetActive(i<count);
                if(i<count)homeRankCards[i].Configure(value.rows[i],i,false,theme);
            }
            var current=RankingPageData.ResolveCurrentPlayer(value.rows,value.currentPlayer);
            bool personal=HasPersonalRank(current);
            homeYourRank.gameObject.SetActive(personal);homeRankSeparator.gameObject.SetActive(personal);
            if(personal)homeYourRank.Configure(current,Mathf.Max(0,current.rank-1),true,theme);
            LayoutHomeRankCards();
        }
        void LayoutHomeRankCards()
        {
            if(!homeCardsRoot||!homeCardsRoot.gameObject.activeSelf||homeCardData==null)return;
            int count=Mathf.Min(10,homeCardData.rows?.Length??0);if(count==0)return;
            SetHomeRankBackdrop(true);
            float inset=Mathf.Clamp(scoresPanel.rect.width*.018f,5f,10f);
            homeCardsRoot.offsetMin=new Vector2(inset,5f);homeCardsRoot.offsetMax=new Vector2(-inset,-5f);
            float w=homeCardsRoot.rect.width,h=homeCardsRoot.rect.height,pad=4,gap=Mathf.Clamp(h*.0065f,3,6);
            bool personal=homeYourRank.gameObject.activeSelf;
            float youHeight=personal?Mathf.Clamp(h*.10f,40,58):0,reserve=personal?youHeight+14:0;
            float weights=count;for(int i=0;i<count;i++)if(homeCardData.rows[i].rank<=3)weights+=.12f;
            float normal=Mathf.Max(20,(h-pad*2-reserve-gap*(count-1))/weights),top=pad;
            for(int i=0;i<count;i++){
                float size=normal*(homeCardData.rows[i].rank<=3?1.12f:1);
                Place((RectTransform)homeRankCards[i].transform,pad,top,w-pad*2,size);homeRankCards[i].Layout();top+=size+gap;
            }
            if(personal){
                Place(homeRankSeparator,pad+3,h-pad-youHeight-9,w-pad*2-6,2);
                Place((RectTransform)homeYourRank.transform,pad,h-pad-youHeight,w-pad*2,youHeight);homeYourRank.Layout();
            }
            LayoutHomeRankHeadings();
        }
        void LayoutHomeRankHeadings()
        {
            var panel=leaderboard?leaderboard.Find("Scores") as RectTransform:null;
            var columns=leaderboard?leaderboard.Find("Columns"):null;
            if(!panel||!columns)return;
            float inset=Mathf.Clamp(panel.rect.width*.018f,5f,10f),w=panel.rect.width-inset*2,pad=4;
            var labels=columns.GetComponentsInChildren<Text>();
            float sharedSize=20;
            foreach(var text in labels){
                    float left,width;
                    if(text.text=="#"){left=.025f;width=.10f;}
                    else if(text.text=="PLAYER"){left=(pad+RankingRowCard.NameStart(w-pad*2))/Mathf.Max(1,w);width=.60f-left;}
                    else if(text.text=="SCORE"){left=.60f;width=.18f;}
                    else if(text.text=="MATCHES"){left=.79f;width=.20f;}
                    else continue;
                    float fullWidth=Mathf.Max(1f,panel.rect.width);
                    left=(inset+left*w)/fullWidth;width=width*w/fullWidth;
                    var r=text.rectTransform;r.anchorMin=new Vector2(left,r.anchorMin.y);r.anchorMax=new Vector2(left+width,r.anchorMax.y);
                    r.offsetMin=new Vector2(0,r.offsetMin.y);r.offsetMax=new Vector2(0,r.offsetMax.y);text.alignment=text.text=="PLAYER"?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
                    text.resizeTextForBestFit=false;text.fontSize=20;
                    text.horizontalOverflow=HorizontalWrapMode.Overflow;
                    sharedSize=Mathf.Min(sharedSize,20*Mathf.Max(1,r.rect.width-4)/Mathf.Max(1,text.preferredWidth));
            }
            int size=Mathf.Clamp(Mathf.FloorToInt(sharedSize),14,20);
            foreach(var text in labels)text.fontSize=size;
        }
        void LayoutHomeStandingsSurface()
        {
            var columns=leaderboard?leaderboard.Find("Columns") as RectTransform:null;
            var scores=leaderboard?leaderboard.Find("Scores") as RectTransform:null;
            if(!columns||!scores)return;
            if(!homeStandingsSurface){
                var existing=leaderboard.Find("Standings surface");
                var go=existing?existing.gameObject:new GameObject("Standings surface",typeof(RectTransform),typeof(Image));
                go.layer=leaderboard.gameObject.layer;homeStandingsSurface=(RectTransform)go.transform;homeStandingsSurface.SetParent(leaderboard,false);
            }
            homeStandingsSurface.SetAsFirstSibling();
            var image=homeStandingsSurface.GetComponent<Image>();image.sprite=theme.Rounded;image.type=Image.Type.Sliced;
            image.color=new Color32(3,40,79,255);image.raycastTarget=false;
            if(image.sprite)TvTitleHeaderLayout.FitSkin(image,6f,1f);
            var low=new Vector2(float.MaxValue,float.MaxValue);var high=new Vector2(float.MinValue,float.MinValue);
            var corners=new Vector3[4];
            foreach(var region in new[]{columns,scores}){
                region.GetWorldCorners(corners);
                foreach(var corner in corners){var point=(Vector2)leaderboard.InverseTransformPoint(corner);low=Vector2.Min(low,point);high=Vector2.Max(high,point);}
            }
            // Widen only the backing surface; row/header insets are independent.
            var timer=leaderboard.Find("Round status") as RectTransform;
            if(timer){
                timer.GetWorldCorners(corners);
                float timerBottom=leaderboard.InverseTransformPoint(corners[0]).y;
                high.y=Mathf.Min(high.y,timerBottom-Mathf.Max(8f,leaderboard.rect.height*.01f));
            }
            homeStandingsSurface.anchorMin=homeStandingsSurface.anchorMax=homeStandingsSurface.pivot=new Vector2(.5f,.5f);
            homeStandingsSurface.sizeDelta=high-low;homeStandingsSurface.localPosition=(low+high)*.5f;
            // Keep the existing header bindings and labels inside the shared surface.
            var oldHeader=columns.GetComponent<Image>();if(oldHeader)oldHeader.enabled=false;
            var oldBackground=columns.Find("Background")?.GetComponent<Image>();if(oldBackground)oldBackground.enabled=false;
        }
        void SetHomeRankBackdrop(bool populated)
        {
            // Keep the stencil graphic enabled; only hide the old glass/fog layers.
            // Layout refresh recreates their styling before laying out these rows.
            foreach(var name in new[]{"TV Background","TV White Fog"}){
                var layer=scoresPanel.Find(name);if(layer)layer.gameObject.SetActive(!populated);
            }
        }
    }
}
