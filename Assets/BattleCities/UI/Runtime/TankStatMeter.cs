using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>A tank rating bar with continuous and five-block display modes.</summary>
    [AddComponentMenu("Battle Cities/UI/Tank Stat Meter")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TankStatMeter : MaskableGraphic
    {
        public const int SegmentCount=5;
        [SerializeField,Range(0,SegmentCount)] int filledSegments;
        [SerializeField] bool useBlockStyle;

        public bool UseBlockStyle
        {
            get=>useBlockStyle;
            set
            {
                if(useBlockStyle==value)return;
                useBlockStyle=value;
                SetVerticesDirty();
            }
        }

        public int FilledSegments
        {
            get=>filledSegments;
            set
            {
                int next=Mathf.Clamp(value,0,SegmentCount);
                if(filledSegments==next)return;
                filledSegments=next;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area=GetPixelAdjustedRect();
            if(area.width<=0||area.height<=0)return;
            float border=Mathf.Clamp(area.height*.075f,1f,1.75f);
            float radius=Mathf.Min(area.height*.16f,3.5f);
            AddBeveledRect(mesh,area,radius,new Color32(9,32,55,255),new Color32(16,50,78,255));
            var face=new Rect(area.x+border,area.y+border,
                area.width-border*2f,area.height-border*2f);
            float innerRadius=Mathf.Max(0,radius-border);
            if(useBlockStyle)
            {
                DrawBlocks(mesh,face);
                return;
            }
            AddBeveledRect(mesh,face,innerRadius,
                new Color32(58,98,139,255),new Color32(126,165,198,255));

            float shineHeight=Mathf.Clamp(face.height*.10f,1f,2.5f);
            AddQuad(mesh,new Rect(face.x+innerRadius,face.yMax-shineHeight,
                face.width-innerRadius*2f,shineHeight),new Color32(177,207,228,255));

            float goldWidth=face.width*filledSegments/SegmentCount;
            if(filledSegments>0)
            {
                var gold=new Rect(face.x,face.y,goldWidth,face.height);
                AddBeveledRect(mesh,gold,innerRadius,
                    new Color32(237,148,0,255),new Color32(255,204,39,255));
                AddQuad(mesh,new Rect(gold.x+innerRadius,gold.yMax-shineHeight,
                    Mathf.Max(0,gold.width-innerRadius*2f),shineHeight),
                    new Color32(255,236,133,255));
            }
        }

        void DrawBlocks(VertexHelper mesh,Rect face)
        {
            float step=face.width/SegmentCount;
            float divider=Mathf.Clamp(face.height*.09f,1f,1.5f);
            float shineHeight=Mathf.Clamp(face.height*.10f,1f,2.5f);
            for(int i=0;i<SegmentCount;i++)
            {
                var cell=new Rect(face.x+i*step,face.y,step,face.height);
                bool filled=i<filledSegments;
                AddQuad(mesh,cell,filled?new Color32(237,148,0,255):new Color32(58,98,139,255),
                    filled?new Color32(255,204,39,255):new Color32(126,165,198,255));
                AddQuad(mesh,new Rect(cell.x,cell.yMax-shineHeight,cell.width,shineHeight),
                    filled?new Color32(255,236,133,255):new Color32(177,207,228,255));
                if(i>0)AddQuad(mesh,new Rect(cell.x-divider*.5f,face.y,divider,face.height),
                    new Color32(9,32,55,255));
            }
        }

        void AddBeveledRect(VertexHelper mesh,Rect rect,float radius,Color bottom,Color top,
            bool bevelLeft=true,bool bevelRight=true)
        {
            if(rect.width<=0||rect.height<=0)return;
            radius=Mathf.Clamp(radius,0,Mathf.Min(rect.width,rect.height)*.5f);
            float left=bevelLeft?radius:0f,right=bevelRight?radius:0f;
            var points=new[]
            {
                new Vector2(rect.xMin+left,rect.yMin),new Vector2(rect.xMax-right,rect.yMin),
                new Vector2(rect.xMax,rect.yMin+right),new Vector2(rect.xMax,rect.yMax-right),
                new Vector2(rect.xMax-right,rect.yMax),new Vector2(rect.xMin+left,rect.yMax),
                new Vector2(rect.xMin,rect.yMax-left),new Vector2(rect.xMin,rect.yMin+left)
            };
            int start=mesh.currentVertCount;
            AddVertex(mesh,rect.center,Color.Lerp(bottom,top,.5f));
            foreach(var point in points)
                AddVertex(mesh,point,Color.Lerp(bottom,top,(point.y-rect.yMin)/rect.height));
            for(int i=0;i<points.Length;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
        }

        void AddQuad(VertexHelper mesh,Rect rect,Color color)
        {
            AddQuad(mesh,rect,color,color);
        }

        void AddQuad(VertexHelper mesh,Rect rect,Color bottom,Color top)
        {
            if(rect.width<=0||rect.height<=0)return;
            int start=mesh.currentVertCount;
            AddVertex(mesh,new Vector2(rect.xMin,rect.yMin),bottom);
            AddVertex(mesh,new Vector2(rect.xMin,rect.yMax),top);
            AddVertex(mesh,new Vector2(rect.xMax,rect.yMax),top);
            AddVertex(mesh,new Vector2(rect.xMax,rect.yMin),bottom);
            mesh.AddTriangle(start,start+1,start+2);
            mesh.AddTriangle(start,start+2,start+3);
        }

        void AddVertex(VertexHelper mesh,Vector2 position,Color tint)
        {
            var vertex=UIVertex.simpleVert;
            vertex.position=position;
            vertex.color=tint*color;
            mesh.AddVert(vertex);
        }
    }
}
