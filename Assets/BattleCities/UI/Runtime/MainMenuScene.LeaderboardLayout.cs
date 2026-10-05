using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        void LayoutLeaderboardEdges()
        {
            if(!leaderboard)return;
            var roundTitle=leaderboard.Find("Round status/Heading")?.GetComponent<Text>();
            if(roundTitle)
            {
                // Use the spare space above the countdown for a larger round heading.
                HudBounds(roundTitle.rectTransform,.22f,.58f,.97f,.92f);
                roundTitle.fontSize=roundTitle.resizeTextMaxSize=28;
                roundTitle.resizeTextMinSize=14;
                roundTitle.resizeTextForBestFit=true;
                roundTitle.alignment=TextAnchor.MiddleLeft;
                roundTitle.horizontalOverflow=HorizontalWrapMode.Wrap;
                roundTitle.verticalOverflow=VerticalWrapMode.Truncate;
            }
            var frame=leaderboard.GetComponent<Image>();
            // The inner opening begins 128 source pixels from navigation-container's edge.
            float inset=frame&&frame.sprite?128f/Mathf.Max(.01f,frame.pixelsPerUnit*frame.pixelsPerUnitMultiplier):18f;
            float side=Mathf.Min(.1f,inset/Mathf.Max(1f,leaderboard.rect.width));
            float edge=Mathf.Min(.1f,inset/Mathf.Max(1f,leaderboard.rect.height));
            var heading=leaderboard.Find("Heading") as RectTransform;
            var footer=leaderboard.Find("Footer") as RectTransform;
            FitJoinedPanel(heading,side,edge,true);
            FitJoinedPanel(footer,side,edge,false);
            ApplyJoinedLeaderboardGlass(heading,true,ref rankingHeadingBackdropMaterial);
            ApplyJoinedLeaderboardGlass(footer,false,ref rankingFooterBackdropMaterial);
            var trophy=heading?heading.Find("Trophy")?.GetComponent<Image>():null;
            if(trophy)
            {
                var position=trophy.rectTransform.anchoredPosition;position.x=18f;
                trophy.rectTransform.anchoredPosition=position;
            }
            ApplyTrophyGroundShadow(trophy);
            ApplyTrophyGroundShadow(leaderboard.Find("Scores/Empty state trophy")?.GetComponent<Image>());
            if(!footer)return;
            var coin=footer.Find("Reward Coin") as RectTransform;
            if(!coin)return;
            float left=coin.anchoredPosition.x+coin.rect.width+8f;
            foreach(var name in new[]{"Label","Subtitle"})
            {
                var text=footer.Find(name)?.GetComponent<Text>();
                if(!text)continue;
                var rect=text.rectTransform;
                rect.anchorMin=new Vector2(0f,rect.anchorMin.y);
                rect.anchorMax=new Vector2(1f,rect.anchorMax.y);
                rect.offsetMin=new Vector2(left,rect.offsetMin.y);
                rect.offsetMax=new Vector2(-10f,rect.offsetMax.y);
                text.alignment=TextAnchor.MiddleLeft;
            }
        }

        void ApplyJoinedLeaderboardGlass(RectTransform panel,bool joinTop,ref Material material)
        {
            if(!panel||panel.rect.height<=0)return;
            var viewport=panel.Find("Glass viewport") as RectTransform;
            if(!viewport)
            {
                var go=new GameObject("Glass viewport",typeof(RectTransform),typeof(RectMask2D));
                go.layer=panel.gameObject.layer;viewport=(RectTransform)go.transform;viewport.SetParent(panel,false);
            }
            viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
            viewport.offsetMin=viewport.offsetMax=Vector2.zero;viewport.localScale=Vector3.one;
            viewport.SetSiblingIndex(1);
            const float border=2.2f;
            float radius=Mathf.Max(1f,Mathf.Min(32f,panel.rect.height*.45f)-border);
            var paper=EnsurePanelImage(viewport,"Paper").rectTransform;
            paper.anchorMin=Vector2.zero;paper.anchorMax=Vector2.one;
            // Extend the joined side beyond the clipping viewport so that edge stays square.
            paper.offsetMin=new Vector2(border,joinTop?border:-radius);
            paper.offsetMax=new Vector2(-border,joinTop?radius:-border);
            var backdrop=transform.Find("World backdrop") as RectTransform;
            var arena=backdrop?backdrop.GetComponent<Image>():null;
            var television=mainFrame?mainFrame.Find("TV Background Viewport/TV Background")?.GetComponent<Image>():null;
            ApplyPaperBackdrop(paper,backdrop,arena?arena.sprite:null,television?television.material:null,
                ref material,radius);
        }

        static void ApplyTrophyGroundShadow(Image trophy)
        {
            if(!trophy||!trophy.sprite)return;
            var artwork=trophy.rectTransform;
            var shadow=artwork.parent.Find(artwork.name+" ground shadow") as RectTransform;
            if(!shadow)
            {
                var go=new GameObject(artwork.name+" ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));
                go.layer=artwork.gameObject.layer;
                shadow=go.GetComponent<RectTransform>();shadow.SetParent(artwork.parent,false);
            }
            // Match the fitted sprite, including any space left by preserveAspect.
            var drawn=trophy.GetPixelAdjustedRect();
            if(trophy.preserveAspect)
            {
                float aspect=trophy.sprite.rect.width/trophy.sprite.rect.height;
                if(drawn.width>drawn.height*aspect)
                {
                    float width=drawn.height*aspect;
                    drawn.x+=(drawn.width-width)*artwork.pivot.x;drawn.width=width;
                }
                else
                {
                    float height=drawn.width/aspect;
                    drawn.y+=(drawn.height-height)*artwork.pivot.y;drawn.height=height;
                }
            }
            shadow.anchorMin=shadow.anchorMax=new Vector2(.5f,.5f);
            shadow.pivot=new Vector2(.5f,.5f);
            shadow.sizeDelta=new Vector2(drawn.width*.72f,drawn.height*.12f);
            shadow.localScale=artwork.localScale;shadow.localRotation=artwork.localRotation;
            shadow.localPosition=artwork.localPosition+artwork.localRotation*Vector3.Scale(
                new Vector3(drawn.center.x,drawn.yMin+drawn.height*.012f,0f),artwork.localScale);
            var graphic=shadow.GetComponent<TankGroundShadow>();
            graphic.color=new Color32(35,25,13,112);graphic.raycastTarget=false;
            shadow.gameObject.SetActive(artwork.gameObject.activeSelf);
            // Move to the end first so the final index is immediately behind the trophy.
            shadow.SetAsLastSibling();shadow.SetSiblingIndex(artwork.GetSiblingIndex());
        }

        static void FitJoinedPanel(RectTransform panel,float side,float edge,bool joinTop)
        {
            if(!panel)return;
            panel.anchorMin=new Vector2(side,joinTop?panel.anchorMin.y:edge);
            panel.anchorMax=new Vector2(1f-side,joinTop?1f-edge:panel.anchorMax.y);
            panel.offsetMin=panel.offsetMax=Vector2.zero;
            var image=panel.GetComponent<Image>();
            if(image)image.enabled=false;
            var surface=panel.Find("Joined surface") as RectTransform;
            if(!surface)
            {
                var go=new GameObject("Joined surface",typeof(RectTransform),typeof(JoinedLeaderboardPanel));
                go.layer=panel.gameObject.layer;
                surface=go.GetComponent<RectTransform>();surface.SetParent(panel,false);
            }
            surface.anchorMin=Vector2.zero;surface.anchorMax=Vector2.one;
            surface.offsetMin=surface.offsetMax=Vector2.zero;
            surface.localScale=Vector3.one;
            surface.SetAsFirstSibling();
            var shape=surface.GetComponent<JoinedLeaderboardPanel>();
            shape.JoinTop=joinTop;shape.raycastTarget=false;
        }
    }
}
