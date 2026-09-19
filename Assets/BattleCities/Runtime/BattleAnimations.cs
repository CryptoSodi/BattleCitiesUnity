using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BattleCities.Core;

namespace BattleCities
{
    // Presentation only: no physics, simulation RNG, or gameplay transforms are changed.
    public sealed class TankAnimation : MonoBehaviour
    {
        Transform barrel;
        Transform[] wheels;
        float[] radii;
        SkinnedMeshRenderer[] tracks;
        Vector2 lastPosition;
        bool hasPosition;
        float treadPhase;
        Vector3 barrelHome;
        float recoil, phase;
        public void Initialize()
        {
            var nodes=GetComponentsInChildren<Transform>(true);
            barrel=nodes.FirstOrDefault(t=>t.name=="BarrelPivot");
            if(barrel)barrelHome=barrel.localPosition;
            wheels=nodes.Where(t=>t.name.StartsWith("WheelLeft")||t.name.StartsWith("WheelRight")).ToArray();
            radii=wheels.Select(w=>{var mesh=w.GetComponent<MeshFilter>();return mesh?Mathf.Max(.01f,mesh.sharedMesh.bounds.extents.y):.12f;}).ToArray();
            tracks=GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh.GetBlendShapeIndex("TreadAdvance")>=0).ToArray();
        }
        public void Fire(){recoil=1;}
        public void Tick(TankState tank,float dt)
        {
            phase+=dt*(tank.Moving?22:0);recoil=Mathf.MoveTowards(recoil,0,dt*7);
            if(barrel)barrel.localPosition=barrelHome+Vector3.back*(.09f*recoil);
            transform.localPosition=new Vector3(0,tank.Moving?Mathf.Sin(phase)*.009f:0,0);
            var position=new Vector2(tank.X,tank.Y);
            float distance=hasPosition?Vector2.Distance(position,lastPosition)/64f:0;
            lastPosition=position;hasPosition=true;
            // Ignore respawn/teleport discontinuities. Pausing cannot advance the belt.
            if(dt>0&&distance>0&&distance<1)
            {
                float localDistance=distance/Mathf.Max(.01f,transform.localScale.x);
                for(int i=0;i<wheels.Length;i++)wheels[i].Rotate(Vector3.right,localDistance/radii[i]*Mathf.Rad2Deg,Space.Self);
                treadPhase=Mathf.Repeat(treadPhase-localDistance/.1f,1);
                foreach(var track in tracks)
                {
                    int index=track.sharedMesh.GetBlendShapeIndex("TreadAdvance");
                    // glTFast preserves normalized (1.0) frame weights; do not assume 100.
                    float fullWeight=track.sharedMesh.GetBlendShapeFrameWeight(index,track.sharedMesh.GetBlendShapeFrameCount(index)-1);
                    track.SetBlendShapeWeight(index,treadPhase*fullWeight);
                }
            }
        }
    }

    public sealed class BattleAnimations : MonoBehaviour
    {
        sealed class Puff {public Vector3 Position,Velocity;public float Age,Life,Size;public bool Smoke;}
        readonly List<Puff> puffs=new List<Puff>();
        readonly System.Random random=new System.Random(782);
        Material fire,smoke;
        Mesh quad;
        Texture2D soft;
        MaterialPropertyBlock properties;
        public int ActiveParticles=>puffs.Count;
        void Awake()
        {
            soft=new Texture2D(32,32,TextureFormat.RGBA32,false);soft.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float a=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f);soft.SetPixel(x,y,new Color(1,1,1,a*a));}soft.Apply();
            fire=MakeMaterial(true);smoke=MakeMaterial(false);
            quad=new Mesh();quad.vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)};quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,1,2,0,2,3};quad.RecalculateBounds();properties=new MaterialPropertyBlock();
        }
        Material MakeMaterial(bool additive)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetTexture("_BaseMap",soft);m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",additive?1:10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;return m;
        }
        float R()=> (float)random.NextDouble();
        public void Burst(Vector3 position,float size)
        {
            for(int i=0;i<18;i++)Add(position,new Vector3(R()-.5f,R()*.9f,R()-.5f)*size*3,.25f+R()*.55f,size*(.18f+R()*.3f),i>8);
        }
        public void Flame(Vector3 position,Vector3 backward,bool power)
        {Add(position,backward*(power?1.8f:1.2f),.12f,power?.16f:.09f,false);}
        public void Impact(Vector3 position,Vector3 direction,bool power)
        {
            float strength=power?1.4f:1;
            Add(position,Vector3.zero,.12f,.42f*strength,false);
            for(int i=0;i<10;i++)
            {
                var velocity=(new Vector3(R()-.5f,R()*.65f,R()-.5f)*2.8f-direction*.7f)*strength;
                Add(position,velocity,.16f+R()*.18f,(.035f+R()*.055f)*strength,false);
            }
            for(int i=0;i<3;i++)Add(position,new Vector3((R()-.5f)*.3f,.3f+R()*.3f,(R()-.5f)*.3f),.35f+R()*.2f,.16f*strength,true);
        }
        void Add(Vector3 p,Vector3 v,float life,float size,bool isSmoke)
        {if(puffs.Count>=320)return;puffs.Add(new Puff{Position=p,Velocity=v,Life=life,Size=size,Smoke=isSmoke});}
        public void Clear(){puffs.Clear();}
        public void Tick(float dt,Camera camera)
        {
            for(int i=puffs.Count-1;i>=0;i--)
            {
                var p=puffs[i];p.Age+=dt;if(p.Age>=p.Life){puffs.RemoveAt(i);continue;}
                p.Position+=p.Velocity*dt;p.Velocity*=Mathf.Exp(-dt*4);float t=p.Age/p.Life;
                var color=p.Smoke?new Color(.24f,.22f,.2f,(1-t)*.45f):Color.Lerp(new Color(1,1,.55f,1),new Color(1,.16f,.01f,0),t);
                properties.SetColor("_BaseColor",color);float size=p.Size*(p.Smoke?1+t*2:1-t*.6f);
                Graphics.DrawMesh(quad,Matrix4x4.TRS(p.Position,camera.transform.rotation,Vector3.one*size),p.Smoke?smoke:fire,0,camera,0,properties,false,false,false);
            }
        }
        void OnDestroy(){Destroy(fire);Destroy(smoke);Destroy(quad);Destroy(soft);}
    }
}

