using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // One continuous bank per joined shoreline, including holes and concave junctions.
    public sealed class BattleWaterBanks : MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        public int LoopCount { get; private set; }
        public int SegmentCount { get; private set; }
        struct Edge {public Vector2 A,B;public Edge(Vector2 a,Vector2 b){A=a;B=b;}}
        struct Section {public Vector2[] Points;public Vector2 Land;}
        sealed class Geometry
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Vector2> UV=new List<Vector2>();
            public readonly List<Color> Colors=new List<Color>();
            public readonly List<int> Indices=new List<int>();
        }
        static readonly float[] Insets={0,.025f,.043f,.085f};
        static readonly float[] Rounds={0,.035f,.065f,.10f};
        static readonly float[] Levels={0,.008f,-.035f,-.22f};
        static readonly float[] Shades={1,1.02f,.80f,.43f};
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Vector2 Left(Vector2 p)=>new Vector2(-p.y,p.x);

        public void Build(List<Vector4> outline,BattleGroundSurface[] ground)
        {
            if(ground==null||ground.Length==0)return;
            var edges=outline.Select(e=>new Edge(new Vector2(e.x,e.y),new Vector2(e.z,e.w))).ToList();
            var groups=new Dictionary<BattleGroundSurface,Geometry>();
            while(edges.Count>0)
            {
                var edge=edges[0];edges.RemoveAt(0);
                var path=new List<Vector2>{edge.A};var start=edge.A;var previous=edge.A;var current=edge.B;
                int remaining=edges.Count+1;
                while(current!=start&&remaining-->0)
                {
                    path.Add(current);
                    var direction=(current-previous).normalized;int best=-1;float angle=-4;
                    for(int i=0;i<edges.Count;i++)if(edges[i].A==current)
                    {
                        var next=(edges[i].B-current).normalized;
                        float turn=Mathf.Atan2(Cross(direction,next),Vector2.Dot(direction,next));
                        if(turn>angle){best=i;angle=turn;}
                    }
                    if(best<0)throw new System.InvalidOperationException("Water shoreline is not closed.");
                    previous=current;current=edges[best].B;edges.RemoveAt(best);
                }
                if(current!=start)throw new System.InvalidOperationException("Water shoreline did not close.");
                // Remove tile divisions so each corner belongs to the outline, not to individual tiles.
                for(int i=path.Count-1;i>=0;i--)
                {
                    var a=path[(i+path.Count-1)%path.Count];var b=path[i];var c=path[(i+1)%path.Count];
                    if(Mathf.Abs(Cross(b-a,c-b))<.00001f)path.RemoveAt(i);
                }
                var sections=new List<Section>();
                for(int i=0;i<path.Count;i++)
                {
                    var p=path[i];var incoming=(p-path[(i+path.Count-1)%path.Count]).normalized;
                    var outgoing=(path[(i+1)%path.Count]-p).normalized;
                    float turn=Mathf.Sign(Cross(incoming,outgoing));
                    // All maps have >= .5-unit channels; cap corner rounding for smaller custom shapes too.
                    float available=Mathf.Min(Vector2.Distance(p,path[(i+path.Count-1)%path.Count]),
                        Vector2.Distance(p,path[(i+1)%path.Count]))*.20f;
                    for(int step=0;step<=4;step++)
                    {
                        var points=new Vector2[4];
                        for(int ring=0;ring<4;ring++)
                        {
                            float radius=Mathf.Min(Rounds[ring],available);
                            var corner=p+(Left(incoming)+Left(outgoing))*Insets[ring];
                            var center=corner-incoming*radius+outgoing*radius;
                            float theta=turn*Mathf.PI*.5f*step/4;
                            var from=-outgoing*radius;
                            points[ring]=center+new Vector2(from.x*Mathf.Cos(theta)-from.y*Mathf.Sin(theta),
                                from.x*Mathf.Sin(theta)+from.y*Mathf.Cos(theta));
                        }
                        sections.Add(new Section{Points=points,Land=p-(Left(incoming)+Left(outgoing))*.004f});
                    }
                }
                for(int i=0;i<sections.Count;i++)
                {
                    var a=sections[i];var b=sections[(i+1)%sections.Count];
                    int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a.Points[0],b.Points[0])*8));
                    for(int step=0;step<steps;step++)
                    {
                        float t0=(float)step/steps,t1=(float)(step+1)/steps;
                        var sample=Vector2.Lerp(a.Land,b.Land,(t0+t1)*.5f);
                        var surface=ground.Where(g=>g.Footprint.Contains(sample)).OrderByDescending(g=>g.Height).FirstOrDefault()??ground[0];
                        if(!groups.TryGetValue(surface,out var data)){data=new Geometry();groups.Add(surface,data);}
                        for(int ring=0;ring<3;ring++)
                        {
                            int first=data.Vertices.Count;
                            Add(Vector2.Lerp(a.Points[ring],b.Points[ring],t0),ring);
                            Add(Vector2.Lerp(a.Points[ring],b.Points[ring],t1),ring);
                            Add(Vector2.Lerp(a.Points[ring+1],b.Points[ring+1],t1),ring+1);
                            Add(Vector2.Lerp(a.Points[ring+1],b.Points[ring+1],t0),ring+1);
                            Triangle(first,first+2,first+1);Triangle(first,first+3,first+2);
                            void Add(Vector2 p,int profile)
                            {
                                data.Vertices.Add(new Vector3(p.x,surface.Height+Levels[profile],p.y));
                                data.UV.Add(surface.UV(p));float shade=Shades[profile];
                                data.Colors.Add(new Color(shade,shade,shade,profile/3f));
                            }
                            void Triangle(int x,int y,int z)
                            {
                                if(Vector3.Cross(data.Vertices[y]-data.Vertices[x],data.Vertices[z]-data.Vertices[x]).sqrMagnitude<1e-12f)return;
                                data.Indices.Add(x);data.Indices.Add(y);data.Indices.Add(z);
                            }
                        }
                        SegmentCount++;
                    }
                }
                LoopCount++;
            }
            var shader=Resources.Load<Shader>("BattleWaterBank");
            foreach(var group in groups)
            {
                var data=group.Value;var source=group.Key.Material;
                var material=new Material(shader){name="Water bank - "+source.name};materials.Add(material);
                material.SetTexture("_BaseMap",source.mainTexture);material.SetColor("_BaseColor",source.color);
                material.SetTextureScale("_BaseMap",source.mainTextureScale);material.SetTextureOffset("_BaseMap",source.mainTextureOffset);
                var mesh=new Mesh{name="Rounded recessed shoreline"};meshes.Add(mesh);
                if(data.Vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(data.Vertices);mesh.SetUVs(0,data.UV);mesh.SetColors(data.Colors);mesh.SetTriangles(data.Indices,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                var root=new GameObject("Shaded basin bank");root.transform.SetParent(transform,false);
                root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        void OnDestroy(){foreach(var mesh in meshes)BattleVisualLifetime.Release(mesh);foreach(var material in materials)BattleVisualLifetime.Release(material);}
    }
}
