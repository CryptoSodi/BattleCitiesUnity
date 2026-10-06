using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed class ScanlineGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            for(float y=r.yMin;y<r.yMax;y+=4)
            {
                int i=vh.currentVertCount;
                vh.AddVert(new Vector3(r.xMin,y),color,Vector2.zero);vh.AddVert(new Vector3(r.xMin,Mathf.Min(y+1,r.yMax)),color,Vector2.zero);
                vh.AddVert(new Vector3(r.xMax,Mathf.Min(y+1,r.yMax)),color,Vector2.zero);vh.AddVert(new Vector3(r.xMax,y),color,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
        }
    }
}
