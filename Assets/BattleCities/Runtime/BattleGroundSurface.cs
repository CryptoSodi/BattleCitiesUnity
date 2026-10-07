using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Ground and its painted regions share the same water cutouts. Collision stays in the simulation.
    public sealed class BattleGroundSurface : MonoBehaviour
    {
        public Rect Footprint { get; private set; }
        public float Height { get; private set; }
        public Material Material { get; private set; }
        Mesh mesh;

        public static BattleGroundSurface Create(Transform parent,string name,Rect footprint,float height,
            Material material,IEnumerable<Wall> terrain)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            var ground=root.AddComponent<BattleGroundSurface>();
            ground.Footprint=footprint;ground.Height=height;ground.Material=material;
            ground.mesh=CreateMesh(footprint,height,terrain);
            root.AddComponent<MeshFilter>().sharedMesh=ground.mesh;
            var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            return ground;
        }

        public Vector2 UV(Vector2 point)=>new Vector2((Footprint.xMax-point.x)/Footprint.width,
            (Footprint.yMax-point.y)/Footprint.height);

        public static Mesh CreateMesh(Rect footprint,float height,IEnumerable<Wall> terrain)
        {
            var water=terrain.Where(t=>t.Alive&&BattleTerrain.IsBasin(t.Type)).Select(t=>new Rect(t.Bounds.X/64,
                -t.Bounds.Bottom/64,t.Bounds.W/64,t.Bounds.H/64)).Where(r=>r.Overlaps(footprint)).ToArray();
            var xs=water.SelectMany(r=>new[]{Mathf.Max(footprint.xMin,r.xMin),Mathf.Min(footprint.xMax,r.xMax)})
                .Concat(new[]{footprint.xMin,footprint.xMax}).Distinct().OrderBy(v=>v).ToArray();
            var zs=water.SelectMany(r=>new[]{Mathf.Max(footprint.yMin,r.yMin),Mathf.Min(footprint.yMax,r.yMax)})
                .Concat(new[]{footprint.yMin,footprint.yMax}).Distinct().OrderBy(v=>v).ToArray();
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            void Vertex(float x,float z)
            {
                vertices.Add(new Vector3(x,height,z));
                // Match Unity's original Plane orientation so the existing ground texture does not jump.
                uv.Add(new Vector2((footprint.xMax-x)/footprint.width,(footprint.yMax-z)/footprint.height));
            }
            for(int x=0;x<xs.Length-1;x++)for(int z=0;z<zs.Length-1;z++)
            {
                var center=new Vector2((xs[x]+xs[x+1])*.5f,(zs[z]+zs[z+1])*.5f);
                if(water.Any(r=>r.Contains(center)))continue;
                int first=vertices.Count;
                Vertex(xs[x],zs[z]);Vertex(xs[x+1],zs[z]);Vertex(xs[x+1],zs[z+1]);Vertex(xs[x],zs[z+1]);
                indices.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
            }
            var mesh=new Mesh{name="Ground with water basins"};
            if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        void OnDestroy(){BattleVisualLifetime.Release(mesh);}
    }
}
