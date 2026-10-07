using System.Collections.Generic;
using UnityEngine;

namespace BattleCities
{
    // Clips the authored textured mesh, rather than substituting cubes for damaged art.
    // Coordinates are world-space so resized and rotated map objects match collision cells.
    internal static class BuildingMeshGeometry
    {
        internal struct Vertex
        {
            public Vector3 P, N;
            public Vector2 UV;
            public Vertex(Vector3 p, Vector3 n, Vector2 uv) { P=p;N=n;UV=uv; }
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex(Vector3.Lerp(a.P,b.P,t),Vector3.Lerp(a.N,b.N,t).normalized,Vector2.Lerp(a.UV,b.UV,t));
        }
        internal struct Triangle
        {
            public Vertex A,B,C;
            public int Material;
            public Triangle(Vertex a,Vertex b,Vertex c,int material){A=a;B=b;C=c;Material=material;}
        }
        struct Edge { public Vector3 A,B; public Edge(Vector3 a,Vector3 b){A=a;B=b;} }
        const float Epsilon=.00002f;
        internal static List<Triangle> Clip(List<Triangle> source, Rect bounds)
        {
            var result=source;
            result=Plane(result,Vector3.right,bounds.xMin);
            result=Plane(result,Vector3.left,-bounds.xMax);
            result=Plane(result,Vector3.forward,bounds.yMin);
            return Plane(result,Vector3.back,-bounds.yMax);
        }
        static List<Triangle> Plane(List<Triangle> source,Vector3 inward,float distance)
        {
            var result=new List<Triangle>();var edges=new List<Edge>();
            var polygon=new List<Vertex>(5);var intersections=new List<Vector3>(2);
            foreach(var triangle in source)
            {
                polygon.Clear();intersections.Clear();
                var input=new[]{triangle.A,triangle.B,triangle.C};
                for(int i=0;i<3;i++)
                {
                    var a=input[i];var b=input[(i+1)%3];
                    float da=Vector3.Dot(a.P,inward)-distance,db=Vector3.Dot(b.P,inward)-distance;
                    bool ai=da>=-Epsilon,bi=db>=-Epsilon;
                    if(ai)polygon.Add(a);
                    if(ai==bi)continue;
                    var v=Vertex.Lerp(a,b,Mathf.Clamp01(da/(da-db)));
                    v.P+=inward*(distance-Vector3.Dot(v.P,inward));
                    polygon.Add(v);intersections.Add(v.P);
                }
                for(int i=1;i+1<polygon.Count;i++)
                    if(Vector3.Cross(polygon[i].P-polygon[0].P,polygon[i+1].P-polygon[0].P).sqrMagnitude>1e-12f)
                        result.Add(new Triangle(polygon[0],polygon[i],polygon[i+1],triangle.Material));
                if(intersections.Count==2&&(intersections[0]-intersections[1]).sqrMagnitude>1e-10f)
                    edges.Add(new Edge(intersections[0],intersections[1]));
            }
            Cap(result,edges,-inward);
            return result;
        }
        static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<1e-8f;
        static void Cap(List<Triangle> result,List<Edge> edges,Vector3 normal)
        {
            while(edges.Count>0)
            {
                var edge=edges[edges.Count-1];edges.RemoveAt(edges.Count-1);
                var loop=new List<Vector3>{edge.A,edge.B};bool closed=false;
                for(int attempts=0;attempts<4096;attempts++)
                {
                    var end=loop[loop.Count-1];
                    if(Near(end,loop[0])){closed=true;loop.RemoveAt(loop.Count-1);break;}
                    int next=edges.FindIndex(e=>Near(e.A,end)||Near(e.B,end));
                    if(next<0)break;
                    var e=edges[next];edges.RemoveAt(next);loop.Add(Near(e.A,end)?e.B:e.A);
                }
                if(!closed||loop.Count<3)continue;
                // Ear clipping also handles concave roof profiles and disconnected mesh parts.
                Vector3 axis=Vector3.Cross(normal,Vector3.up);if(axis.sqrMagnitude<.1f)axis=Vector3.right;
                axis.Normalize();Vector3 up=Vector3.Cross(normal,axis);
                var points=new List<Vector2>();foreach(var p in loop)points.Add(new Vector2(Vector3.Dot(p,axis),Vector3.Dot(p,up)));
                float area=0;for(int i=0;i<points.Count;i++)area+=Cross(points[i],points[(i+1)%points.Count]);
                if(area<0){loop.Reverse();points.Reverse();}
                for(int attempts=0;loop.Count>=3&&attempts<4096;attempts++)
                {
                    bool found=false;
                    for(int i=0;i<loop.Count;i++)
                    {
                        int prev=(i+loop.Count-1)%loop.Count,next=(i+1)%loop.Count;
                        var a=points[prev];var b=points[i];var c=points[next];
                        if(Cross(b-a,c-b)<=1e-9f)continue;
                        bool contains=false;
                        for(int j=0;j<points.Count;j++)if(j!=prev&&j!=i&&j!=next&&Inside(points[j],a,b,c)){contains=true;break;}
                        if(contains)continue;
                        result.Add(new Triangle(new Vertex(loop[prev],normal,Vector2.zero),new Vertex(loop[i],normal,Vector2.zero),new Vertex(loop[next],normal,Vector2.zero),-1));
                        loop.RemoveAt(i);points.RemoveAt(i);found=true;break;
                    }
                    if(!found)break;
                }
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(b-a,p-a)>1e-8f&&Cross(c-b,p-b)>1e-8f&&Cross(a-c,p-c)>1e-8f;
        internal static float RoofHeight(List<Triangle> triangles,float x,float z)
        {
            float top=float.NegativeInfinity;var p=new Vector2(x,z);
            foreach(var t in triangles)
            {
                if(t.Material<0)continue;
                var a=new Vector2(t.A.P.x,t.A.P.z);var b=new Vector2(t.B.P.x,t.B.P.z);var c=new Vector2(t.C.P.x,t.C.P.z);
                float area=Cross(b-a,c-a);if(Mathf.Abs(area)<1e-8f)continue;
                float u=Cross(p-a,c-a)/area,v=Cross(b-a,p-a)/area;
                if(u<-.001f||v<-.001f||u+v>1.001f)continue;
                top=Mathf.Max(top,t.A.P.y+u*(t.B.P.y-t.A.P.y)+v*(t.C.P.y-t.A.P.y));
            }
            return top;
        }
    }
}
