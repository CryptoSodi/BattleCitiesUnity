using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Responsive roster with focus-driven scrolling.</summary>
    [ExecuteAlways]
    public sealed class TankRosterScroll : ScrollRect
    {
        internal const float CardAspect=.85f;
        [SerializeField,Min(.1f)] float cardAspectRatio=CardAspect;
        public float CardAspectRatio { get=>cardAspectRatio; set=>cardAspectRatio=Mathf.Max(.1f,value); }

        public override void SetLayoutHorizontal()
        {
            base.SetLayoutHorizontal();
            if(!content||!viewport)return;
            var grid=content.GetComponent<GridLayoutGroup>();
            if(!grid)return;
            int columns=Mathf.Max(1,grid.constraintCount);
            float width=Mathf.Max(1f,(viewport.rect.width-grid.padding.horizontal-grid.spacing.x*(columns-1))/columns);
            var size=new Vector2(width,width/Mathf.Max(.1f,cardAspectRatio));
            if((grid.cellSize-size).sqrMagnitude>.01f)grid.cellSize=size;
        }

        public void Reveal(RectTransform card)
        {
            if(!card||!content||!viewport||!card.IsChildOf(content))return;
            Canvas.ForceUpdateCanvases();
            StopMovement();
            float range=Mathf.Max(0,content.rect.height-viewport.rect.height);
            if(range<=.01f){verticalNormalizedPosition=1f;return;}
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport,card);
            const float inset=4f;
            float top=viewport.rect.yMax-inset,bottom=viewport.rect.yMin+inset;
            float delta=0;
            if(bounds.size.y>top-bottom||bounds.max.y>top)delta=top-bounds.max.y;
            else if(bounds.min.y<bottom)delta=bottom-bounds.min.y;
            if(Mathf.Abs(delta)>.01f)
                verticalNormalizedPosition=1f-Mathf.Clamp(content.anchoredPosition.y+delta,0,range)/range;
        }
    }
}
