using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>A soft elliptical contact shadow shared by tank cards and trophy artwork.</summary>
    [RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
    public sealed class TankGroundShadow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments=40,rings=8;
            var rect=rectTransform.rect;
            var center=rect.center;
            mesh.AddVert(new Vector3(center.x,center.y,0f),color,Vector2.zero);
            for(int ring=1;ring<=rings;ring++)
            {
                float radius=(float)ring/rings;
                var tint=color;
                tint.a*=Mathf.Pow(1f-radius*radius,2f);
                for(int segment=0;segment<segments;segment++)
                {
                    float angle=segment*Mathf.PI*2f/segments;
                    mesh.AddVert(new Vector3(center.x+Mathf.Cos(angle)*rect.width*.5f*radius,
                        center.y+Mathf.Sin(angle)*rect.height*.5f*radius,0f),tint,Vector2.zero);
                }
            }
            for(int segment=0;segment<segments;segment++)
            {
                int next=(segment+1)%segments;
                mesh.AddTriangle(0,1+segment,1+next);
                for(int ring=1;ring<rings;ring++)
                {
                    int inner=1+(ring-1)*segments,outer=inner+segments;
                    mesh.AddTriangle(inner+segment,outer+segment,outer+next);
                    mesh.AddTriangle(inner+segment,outer+next,inner+next);
                }
            }
        }
    }
}
