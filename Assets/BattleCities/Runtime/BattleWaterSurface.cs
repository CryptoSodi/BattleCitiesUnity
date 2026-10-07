using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // One shared surface, with world-space UVs and foam only at exposed shores.
    public sealed class BattleWaterSurface : MonoBehaviour
    {
        Mesh mesh;
        MeshRenderer surface;
        MaterialPropertyBlock properties;
        float age;
        public int CellCount { get; private set; }
        public const float WaterHeight=-.18f;
        public void Build(IEnumerable<Wall> terrain,Material material,BattleGroundSurface[] ground=null,string type=BattleTerrain.Water,float height=WaterHeight)
        {
            var water=terrain.Where(w=>w.Alive&&w.Type==type).Select(w=>w.Bounds).ToArray();
            if(water.Length==0)return;
            var xs=water.SelectMany(b=>new[]{b.X/64,(b.X+b.W)/64}).Distinct().OrderBy(v=>v).ToArray();
            var zs=water.SelectMany(b=>new[]{-(b.Y+b.H)/64,-b.Y/64}).Distinct().OrderBy(v=>v).ToArray();
            int width=xs.Length-1,depth=zs.Length-1;
            var wet=new bool[width,depth];
            for(int x=0;x<width;x++)for(int z=0;z<depth;z++)
            {
                float px=(xs[x]+xs[x+1])*32,py=-(zs[z]+zs[z+1])*32;
                wet[x,z]=water.Any(b=>px>b.X&&px<b.X+b.W&&py>b.Y&&py<b.Y+b.H);
            }
            // Distance to the complete outline is continuous at concave corners and T junctions.
            // Per-tile edge distances create visible square changes in the shallow-water tint.
            // Directed edges keep water on the left, for closed bank loops and dry islands.
            var outline=new List<Vector4>();
            for(int x=0;x<width;x++)for(int z=0;z<depth;z++)
            {
                if(!wet[x,z])continue;
                if(x==0||!wet[x-1,z])outline.Add(new Vector4(xs[x],zs[z+1],xs[x],zs[z]));
                if(x==width-1||!wet[x+1,z])outline.Add(new Vector4(xs[x+1],zs[z],xs[x+1],zs[z+1]));
                if(z==0||!wet[x,z-1])outline.Add(new Vector4(xs[x],zs[z],xs[x+1],zs[z]));
                if(z==depth-1||!wet[x,z+1])outline.Add(new Vector4(xs[x+1],zs[z+1],xs[x],zs[z+1]));
            }
            var vertices=new List<Vector3>();var uv=new List<Vector2>();
            var shores=new List<Vector2>();var triangles=new List<int>();
            var lookup=new Dictionary<Vector2,int>();
            int Vertex(float x,float z)
            {
                var p=new Vector2(x,z);
                if(lookup.TryGetValue(p,out int index))return index;
                float distance=float.MaxValue;
                foreach(var edge in outline)
                {
                    float dx=x-Mathf.Clamp(x,Mathf.Min(edge.x,edge.z),Mathf.Max(edge.x,edge.z));
                    float dz=z-Mathf.Clamp(z,Mathf.Min(edge.y,edge.w),Mathf.Max(edge.y,edge.w));
                    distance=Mathf.Min(distance,dx*dx+dz*dz);
                }
                index=vertices.Count;lookup.Add(p,index);
                vertices.Add(new Vector3(x,height,z));uv.Add(p);shores.Add(new Vector2(Mathf.Sqrt(distance),0));
                return index;
            }
            for(int x=0;x<width;x++)for(int z=0;z<depth;z++)
            {
                if(!wet[x,z])continue;
                int nx=Mathf.CeilToInt((xs[x+1]-xs[x])*8),nz=Mathf.CeilToInt((zs[z+1]-zs[z])*8);
                for(int ix=0;ix<nx;ix++)for(int iz=0;iz<nz;iz++)
                {
                    float x0=Mathf.Lerp(xs[x],xs[x+1],(float)ix/nx),x1=Mathf.Lerp(xs[x],xs[x+1],(float)(ix+1)/nx);
                    float z0=Mathf.Lerp(zs[z],zs[z+1],(float)iz/nz),z1=Mathf.Lerp(zs[z],zs[z+1],(float)(iz+1)/nz);
                    int a=Vertex(x0,z0),b=Vertex(x1,z0),c=Vertex(x1,z1),d=Vertex(x0,z1);
                    triangles.AddRange(new[]{a,c,b,a,d,c});
                }
                CellCount++;
            }
            mesh=new Mesh{name="Joined "+type};
            if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,shores);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            surface=gameObject.AddComponent<MeshRenderer>();surface.sharedMaterial=material;
            surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=true;
            properties=new MaterialPropertyBlock();
            if(ground!=null&&ground.Length>0)gameObject.AddComponent<BattleWaterBanks>().Build(outline,ground);
        }
        public void Tick(float dt)
        {
            if(!surface)return;
            age+=dt;properties.SetFloat("_FlowTime",age);surface.SetPropertyBlock(properties);
        }
        void OnDestroy(){BattleVisualLifetime.Release(mesh);}
    }
}
