using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex = BattleCities.BuildingMeshGeometry.Vertex;
using Triangle = BattleCities.BuildingMeshGeometry.Triangle;

namespace BattleCities
{
    [DisallowMultipleComponent]
    public sealed class BuildingDamageView : MonoBehaviour
    {
        IReadOnlyList<Wall> sections;
        MeshRenderer[] originals;
        bool[] originalEnabled;
        readonly List<Material> sourceMaterials=new List<Material>();
        readonly List<Material> ownedMaterials=new List<Material>();
        List<Triangle>[] geometry;
        Mesh damagedMesh;
        MeshRenderer damagedRenderer;
        Light[] lights;
        Wall[] lightOwners;
        int[] lastHealth;
        bool[] lastAlive;
        MapBuildingSettings settings;
        public IReadOnlyList<Light> WindowLights => lights ?? Array.Empty<Light>();
        public Mesh DamageMesh => damagedMesh;
        public int AliveSections => sections?.Count(w=>w.Alive) ?? 0;

        public void Bind(EnvironmentPiece piece,IReadOnlyList<Wall> states)
        {
            sections=states;settings=states[0].BuildingSettings??new MapBuildingSettings();
            originals=piece.IntactVisual.GetComponentsInChildren<MeshRenderer>(true);
            originalEnabled=originals.Select(r=>r.enabled).ToArray();
            lastHealth=Enumerable.Repeat(-1,states.Count).ToArray();lastAlive=new bool[states.Count];
            if(piece.DestroyedVisual)piece.DestroyedVisual.SetActive(false);
            CreateLights();RefreshVisual();
        }
        void CreateLights()
        {
            lights=new Light[2];lightOwners=new Wall[2];
            var localBounds=new Bounds(Vector3.zero,Vector3.zero);
            foreach(var r in originals)
            {
                var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)localBounds.Encapsulate(transform.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
            }
            for(int i=0;i<2;i++)
            {
                float z=i==0?localBounds.min.z-.10f:localBounds.max.z+.10f;
                var go=new GameObject(i==0?"Front window light":"Rear window light");go.transform.SetParent(transform,false);
                go.transform.localPosition=new Vector3(localBounds.center.x,Mathf.Max(.2f,localBounds.max.y*.56f),z);
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.62f,.25f);
                light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;
                light.range=Mathf.Clamp(settings.lightRange,.1f,8);light.intensity=Mathf.Clamp(settings.lightIntensity,0,8);
                lights[i]=light;
                var p=go.transform.position;
                lightOwners[i]=sections.OrderBy(w=>DistanceTo(w.Bounds,p.x*64,-p.z*64)).First();
            }
        }
        static float DistanceTo(Box b,float x,float y)
        {float dx=Mathf.Max(b.X-x,0,x-b.Right),dy=Mathf.Max(b.Y-y,0,y-b.Bottom);return dx*dx+dy*dy;}
        public void RefreshVisual()
        {
            if(sections==null)return;
            bool changed=false,damaged=false;
            for(int i=0;i<sections.Count;i++)
            {
                var w=sections[i];changed|=lastHealth[i]!=w.Health||lastAlive[i]!=w.Alive;
                lastHealth[i]=w.Health;lastAlive[i]=w.Alive;
                damaged|=!w.Alive||w.Health<Math.Max(1,w.Damage.hitPoints);
            }
            if(!changed)return;
            for(int i=0;i<lights.Length;i++)
            {
                var w=lightOwners[i];lights[i].enabled=settings.lights&&w.Alive;
                lights[i].intensity=Mathf.Clamp(settings.lightIntensity,0,8)*Mathf.Clamp01((float)w.Health/Math.Max(1,w.Damage.hitPoints));
            }
            if(damaged)
            {
                if(geometry==null)PrepareGeometry();
                RebuildMesh();
            }
            for(int i=0;i<originals.Length;i++)originals[i].enabled=!damaged&&originalEnabled[i];
            if(damagedRenderer)damagedRenderer.gameObject.SetActive(damaged);
        }
        void PrepareGeometry()
        {
            var source=new List<Triangle>();
            foreach(var renderer in originals)
            {
                if(!renderer.enabled)continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var mesh=filter.sharedMesh;
                if(!mesh.isReadable)throw new InvalidOperationException("Building mesh needs Read/Write enabled: "+mesh.name);
                var positions=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                var matrix=filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                Vertex V(int index)=>new Vertex(matrix.MultiplyPoint3x4(positions[index]),normalMatrix.MultiplyVector(normals.Length==positions.Length?normals[index]:Vector3.up).normalized,uv.Length==positions.Length?uv[index]:Vector2.zero);
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var material=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                    int slot=sourceMaterials.IndexOf(material);if(slot<0){slot=sourceMaterials.Count;sourceMaterials.Add(material);}
                    var indices=mesh.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3)source.Add(new Triangle(V(indices[i]),V(indices[i+1]),V(indices[i+2]),slot));
                }
            }
            geometry=new List<Triangle>[sections.Count];
            for(int i=0;i<sections.Count;i++)
            {
                var b=sections[i].Bounds;
                geometry[i]=BuildingMeshGeometry.Clip(source,new Rect(b.X/64,-b.Bottom/64,b.W/64,b.H/64));
            }
            var materials=new List<Material>(sourceMaterials);
            foreach(float tint in new[]{.63f,.34f})foreach(var sourceMaterial in sourceMaterials)
            {
                var mat=new Material(sourceMaterial){name=sourceMaterial.name+" damaged "+tint,hideFlags=HideFlags.HideAndDontSave};
                string color=mat.HasProperty("baseColorFactor")?"baseColorFactor":mat.HasProperty("_BaseColor")?"_BaseColor":"_Color";
                if(mat.HasProperty(color)){var c=mat.GetColor(color);mat.SetColor(color,new Color(c.r*tint,c.g*tint,c.b*tint,c.a));}
                foreach(string emission in new[]{"emissiveFactor","_EmissionColor"})if(mat.HasProperty(emission))mat.SetColor(emission,mat.GetColor(emission)*tint);
                ownedMaterials.Add(mat);materials.Add(mat);
            }
            var interior=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Broken masonry and roof cracks",hideFlags=HideFlags.HideAndDontSave};
            interior.color=new Color(.16f,.13f,.105f);interior.SetFloat("_Smoothness",.05f);ownedMaterials.Add(interior);materials.Add(interior);
            var rubble=new Material(interior){name="Collapsed masonry"};rubble.color=new Color(.38f,.35f,.3f);ownedMaterials.Add(rubble);materials.Add(rubble);
            var go=new GameObject("Localized building damage");go.transform.SetParent(transform,false);
            damagedMesh=new Mesh{name="Building sections",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};damagedMesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh=damagedMesh;
            damagedRenderer=go.AddComponent<MeshRenderer>();damagedRenderer.sharedMaterials=materials.ToArray();
        }
        void RebuildMesh()
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
            int interior=sourceMaterials.Count*3;
            var indices=Enumerable.Range(0,interior+2).Select(_=>new List<int>()).ToArray();
            var localNormal=transform.localToWorldMatrix.transpose;
            void Add(Triangle t,int slot)
            {
                foreach(var v in new[]{t.A,t.B,t.C}){indices[slot].Add(positions.Count);positions.Add(transform.InverseTransformPoint(v.P));normals.Add(localNormal.MultiplyVector(v.N).normalized);uv.Add(v.UV);}
            }
            for(int i=0;i<sections.Count;i++)
            {
                var wall=sections[i];var b=wall.Bounds;
                if(!wall.Alive)
                {
                    // Low, scattered debris leaves a readable open passage, without a tile marker.
                    for(int chip=0;chip<3;chip++)
                    {
                        float u=.23f+chip*.25f,v=.25f+((i+chip*3)%5)*.105f;
                        var center=new Vector3((b.X+u*b.W)/64,.018f,-(b.Y+v*b.H)/64);
                        float radius=Mathf.Min(b.W,b.H)/64*.085f;
                        var a=new Vertex(center+new Vector3(-radius,0,-radius),Vector3.up,Vector2.zero);
                        var c=new Vertex(center+new Vector3(radius,0,-radius*.4f),Vector3.up,Vector2.zero);
                        var q=new Vertex(center+new Vector3(0,0,radius),Vector3.up,Vector2.zero);
                        var tip=new Vertex(center+new Vector3(0,.01f,0),Vector3.up,Vector2.zero);
                        Add(new Triangle(a,tip,c,-1),interior+1);Add(new Triangle(c,tip,q,-1),interior+1);Add(new Triangle(q,tip,a,-1),interior+1);
                    }
                    continue;
                }
                int maximum=Math.Max(1,wall.Damage.hitPoints);
                int damage=wall.Health>=maximum?0:wall.Health*2>maximum?1:2;
                foreach(var t in geometry[i])Add(t,t.Material<0?interior:t.Material+damage*sourceMaterials.Count);
                if(damage==0)continue;
                // Thin zigzags follow the actual roof surface, including curved and stepped roofs.
                var points=new[]{new Vector2(.16f,.28f),new Vector2(.40f,.45f),new Vector2(.30f,.67f),new Vector2(.64f,.83f)};
                for(int j=1;j<points.Length;j++)
                {
                    var a=new Vector2((b.X+points[j-1].x*b.W)/64,-(b.Y+points[j-1].y*b.H)/64);
                    var c=new Vector2((b.X+points[j].x*b.W)/64,-(b.Y+points[j].y*b.H)/64);
                    var d=(c-a).normalized;var offset=new Vector2(-d.y,d.x)*.008f;
                    var v=new Vertex[4];bool valid=true;var coords=new[]{a-offset,a+offset,c+offset,c-offset};
                    for(int n=0;n<4;n++){var p=coords[n];float y=BuildingMeshGeometry.RoofHeight(geometry[i],p.x,p.y);valid&=!float.IsNegativeInfinity(y);v[n]=new Vertex(new Vector3(p.x,y+.006f,p.y),Vector3.up,Vector2.zero);}
                    if(valid){Add(new Triangle(v[0],v[1],v[2],-1),interior);Add(new Triangle(v[0],v[2],v[3],-1),interior);}
                }
            }
            damagedMesh.Clear();damagedMesh.SetVertices(positions);damagedMesh.SetNormals(normals);damagedMesh.SetUVs(0,uv);damagedMesh.subMeshCount=indices.Length;
            for(int i=0;i<indices.Length;i++)damagedMesh.SetTriangles(indices[i],i,false);
            damagedMesh.RecalculateBounds();damagedMesh.RecalculateTangents();
        }
        void OnDestroy()
        {
            Release(damagedMesh);foreach(var mat in ownedMaterials)Release(mat);
        }
        static void Release(UnityEngine.Object value)
        {if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
