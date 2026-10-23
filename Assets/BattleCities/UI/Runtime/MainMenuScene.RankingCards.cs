using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        readonly List<RankingRowCard> homeRankCards=new List<RankingRowCard>();
        RectTransform homeCardsRoot,homeRankSeparator;
        RankingRowCard homeYourRank;
        MainMenuApiClient.RoundSnapshot homeCardData;
        static bool HasPersonalRank(MainMenuApiClient.RankingRow row)=>row!=null&&row.rank>0&&!row.restricted&&!row.guest;
        RankingRowCard CreateHomeCard(string title)
        {
            var existing=homeCardsRoot.Find(title);
            var go=existing?existing.gameObject:new GameObject(title,typeof(RectTransform),typeof(Image),typeof(RankingRowCard));go.layer=scoresPanel.gameObject.layer;
            go.transform.SetParent(homeCardsRoot,false);return go.GetComponent<RankingRowCard>();
        }
        void RenderHomeRankCards(MainMenuApiClient.RoundSnapshot value)
        {
            homeCardData=value;if(!scoresPanel)return;
            int count=Mathf.Min(10,value?.rows?.Length??0);
            if(count==0){if(homeCardsRoot)homeCardsRoot.gameObject.SetActive(false);return;}
            if(!homeCardsRoot){
                var existing=scoresPanel.Find("Ranking cards");
                var go=existing?existing.gameObject:new GameObject("Ranking cards",typeof(RectTransform),typeof(Image));go.layer=scoresPanel.gameObject.layer;homeCardsRoot=(RectTransform)go.transform;homeCardsRoot.SetParent(scoresPanel,false);
                homeCardsRoot.anchorMin=Vector2.zero;homeCardsRoot.anchorMax=Vector2.one;homeCardsRoot.offsetMin=homeCardsRoot.offsetMax=Vector2.zero;
                var surface=go.GetComponent<Image>();surface.sprite=null;surface.type=Image.Type.Simple;surface.color=new Color32(3,40,79,255);surface.raycastTarget=false;
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
            bool personal=HasPersonalRank(value.currentPlayer);
            homeYourRank.gameObject.SetActive(personal);homeRankSeparator.gameObject.SetActive(personal);
            if(personal)homeYourRank.Configure(value.currentPlayer,0,true,theme);
            LayoutHomeRankCards();
        }
        void LayoutHomeRankCards()
        {
            if(!homeCardsRoot||!homeCardsRoot.gameObject.activeSelf||homeCardData==null)return;
            int count=Mathf.Min(10,homeCardData.rows?.Length??0);if(count==0)return;
            float w=scoresPanel.rect.width,h=scoresPanel.rect.height,pad=4,gap=Mathf.Clamp(h*.0065f,3,6);
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
            var columns=leaderboard.Find("Columns");if(columns){
                foreach(var text in columns.GetComponentsInChildren<Text>()){
                    float left,width;
                    if(text.text=="#"){left=.025f;width=.10f;}
                    else if(text.text=="PLAYER"){left=(pad+RankingRowCard.NameStart(w-pad*2))/Mathf.Max(1,w);width=.63f-left;}
                    else if(text.text=="SCORE"){left=.63f;width=.22f;}
                    else if(text.text=="MATCHES"){left=.86f;width=.13f;}
                    else continue;
                    var r=text.rectTransform;r.anchorMin=new Vector2(left,r.anchorMin.y);r.anchorMax=new Vector2(left+width,r.anchorMax.y);
                    r.offsetMin=new Vector2(0,r.offsetMin.y);r.offsetMax=new Vector2(0,r.offsetMax.y);text.alignment=text.text=="PLAYER"?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
                }
            }
        }
    }
}
