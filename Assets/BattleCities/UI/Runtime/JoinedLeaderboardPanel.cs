using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>A dark leaderboard panel with one open, square edge against its parent rim.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JoinedLeaderboardPanel : MaskableGraphic
    {
        [SerializeField] bool joinTop;
        readonly List<Vector2> outline=new List<Vector2>();

        public bool JoinTop
        {
            get=>joinTop;
            set { if(joinTop==value)return;joinTop=value;SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var area=GetPixelAdjustedRect();
            if(area.width<=0f||area.height<=0f)return;
            float radius=Mathf.Min(32f,area.height*.45f);
            AddShape(mesh,area,radius,new Color32(3,34,56,255));
            AddShape(mesh,Inset(area,.9f),Mathf.Max(0f,radius-.9f),new Color32(17,139,174,255));
            AddShape(mesh,Inset(area,2.2f),Mathf.Max(0f,radius-2.2f),new Color32(6,62,104,255));
        }

        Rect Inset(Rect area,float amount)
        {
            // Fill all the way to the joined edge, without a border across the seam.
            return new Rect(area.xMin+amount,area.yMin+(joinTop?amount:0f),
                area.width-amount*2f,area.height-amount);
        }

        void AddShape(VertexHelper mesh,Rect area,float radius,Color tint)
        {
            if(area.width<=0f||area.height<=0f)return;
            radius=Mathf.Min(radius,Mathf.Min(area.width,area.height)*.5f);
            outline.Clear();
            Corner(new Vector2(area.xMax,area.yMin),radius,270f,!joinTop);
            Corner(new Vector2(area.xMax,area.yMax),radius,0f,joinTop);
            Corner(new Vector2(area.xMin,area.yMax),radius,90f,joinTop);
            Corner(new Vector2(area.xMin,area.yMin),radius,180f,!joinTop);
            int start=mesh.currentVertCount;
            AddVertex(mesh,area.center,tint);
            foreach(var point in outline)AddVertex(mesh,point,tint);
            for(int i=0;i<outline.Count;i++)
                mesh.AddTriangle(start,start+1+i,start+1+(i+1)%outline.Count);
        }

        void Corner(Vector2 corner,float radius,float angle,bool square)
        {
            if(square||radius<=0f){outline.Add(corner);return;}
            var center=corner;
            center.x+=angle<90f||angle>=270f?-radius:radius;
            center.y+=angle<180f?-radius:radius;
            for(int i=0;i<=8;i++)
            {
                float radians=(angle+i*90f/8f)*Mathf.Deg2Rad;
                outline.Add(center+new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*radius);
            }
        }

        void AddVertex(VertexHelper mesh,Vector2 point,Color tint)
        {
            var vertex=UIVertex.simpleVert;
            vertex.position=point;vertex.color=tint*color;
            mesh.AddVert(vertex);
        }
    }
}
