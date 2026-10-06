using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>The original Back button's white triangle and navy edge, fitted to a tab.</summary>
    public sealed class BackTabArrow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect=GetPixelAdjustedRect();
            float width=Mathf.Min(rect.width,rect.height*.86f),height=width/.86f;
            if(width<=0f)return;
            float edge=Mathf.Max(.7f,height*.03f),radius=Mathf.Max(.5f,height*.035f);
            Triangle(mesh,rect.center+new Vector2(.65f,-1f),width,height,radius,ArcadeTextStyles.TextShadow);
            Triangle(mesh,rect.center,width,height,radius,ArcadeTextStyles.WhiteOutline);
            Triangle(mesh,rect.center+new Vector2(edge*.5f,0),width-edge*3f,height-edge*3.5f,radius*.7f,color);
        }

        static void Triangle(VertexHelper mesh,Vector2 center,float width,float height,float radius,Color tint)
        {
            var corners=new[]{center+new Vector2(-width*.5f,0),center+new Vector2(width*.5f,height*.5f),center+new Vector2(width*.5f,-height*.5f)};
            int start=mesh.currentVertCount;mesh.AddVert(center,tint,Vector2.zero);
            const int steps=4;
            for(int i=0;i<3;i++)
            {
                var corner=corners[i];var before=corner+(corners[(i+2)%3]-corner).normalized*radius;
                var after=corner+(corners[(i+1)%3]-corner).normalized*radius;
                for(int step=0;step<=steps;step++)
                {
                    float t=step/(float)steps,u=1f-t;
                    mesh.AddVert(u*u*before+2f*u*t*corner+t*t*after,tint,Vector2.zero);
                }
            }
            int count=3*(steps+1);
            for(int i=0;i<count;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%count);
        }
    }
}
