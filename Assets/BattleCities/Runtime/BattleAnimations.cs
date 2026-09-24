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
        enum PuffKind { Fire, Smoke, Tint, Ring }
        sealed class Puff {public Vector3 Position,Velocity;public float Age,Life,Size;public PuffKind Kind;public Color Color;}
        readonly List<Puff> puffs=new List<Puff>();
        readonly Stack<Puff> pool=new Stack<Puff>();
        readonly System.Random random=new System.Random(782);
        Material fire,smoke,shockwave,solid;
        Mesh quad;
        Texture2D soft,ring;
        MaterialPropertyBlock properties;
        public int ActiveParticles=>puffs.Count;
        void Awake()
        {
            soft=new Texture2D(32,32,TextureFormat.RGBA32,false);soft.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float a=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f);soft.SetPixel(x,y,new Color(1,1,1,a*a));}soft.Apply();
            fire=MakeMaterial(true);smoke=MakeMaterial(false);
            solid=MakeMaterial(false);solid.SetTexture("_BaseMap",Texture2D.whiteTexture);
            ring=new Texture2D(64,64,TextureFormat.RGBA32,false);ring.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float radius=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f;float a=Mathf.Clamp01(1-Mathf.Abs(radius-.78f)/.12f);ring.SetPixel(x,y,new Color(1,1,1,a*a));}ring.Apply();
            shockwave=MakeMaterial(true);shockwave.SetTexture("_BaseMap",ring);
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
        public void NormalShotExhaust(Vector3 position,Vector3 backward)
        {Add(position,backward*1.2f,.12f,.09f,false);}
        public void ProjectileGlow(Vector3 position,Color color,bool power,Camera camera,float scale=1)
        {
            float size=(power?.65f:.48f)*scale;
            Draw(position,camera.transform.rotation,size,HDR(color,2.2f,.75f),fire,camera);
            Draw(position,camera.transform.rotation,size*.35f,HDR(Color.Lerp(color,Color.white,.35f),2.5f,1),fire,camera);
        }
        public void ShellGlow(Vector3 position,Color color,Camera camera)
        {
            // Render the halo behind the opaque shell so depth testing preserves
            // its silhouette and material details. No white core over the mesh.
            Draw(position+camera.transform.forward*.36f,camera.transform.rotation,.85f,HDR(color,1.6f,.6f),fire,camera);
        }
        public void Trail(Vector3 from,Vector3 to,Color color,bool power,float scale=1)
        {
            float distance=Vector3.Distance(from,to);
            if(distance<.001f||distance>3)return; // Never draw a streak across a teleport.
            int count=Mathf.Clamp(Mathf.CeilToInt(distance/.065f),1,48);
            for(int i=1;i<=count;i++)AddTint(Vector3.Lerp(from,to,(float)i/count),Vector3.up*.035f,.19f,(power?.28f:.21f)*scale,color);
        }
        public void MuzzleFlash(Vector3 position,Vector3 forward,Color color,float scale=1)
        {
            AddTint(position,Vector3.zero,.085f,.72f*scale,Color.Lerp(color,Color.white,.45f));
            for(int i=0;i<5;i++)AddTint(position+forward*i*.075f*scale,forward*.8f,.075f+i*.012f,(.32f-i*.04f)*scale,color);
            for(int i=0;i<6;i++)AddTint(position,(forward*(1+R()*2)+new Vector3(R()-.5f,R()-.2f,R()-.5f))*scale,.12f+R()*.07f,.06f*scale,color);
            Add(position,Vector3.up*.45f,.3f,.22f*scale,true);
        }
        public void Impact(Vector3 position,Vector3 direction,bool power)
        {Impact(position,direction,power,new Color(1,.64f,.12f));}
        public void Impact(Vector3 position,Vector3 direction,bool power,Color color,float scale=1)
        {
            float strength=(power?1.4f:1)*scale;
            AddTint(position,Vector3.zero,.11f,1.4f*strength,Color.Lerp(color,Color.white,.7f));
            AddPuff(new Vector3(position.x,.06f,position.z),Vector3.zero,.36f,1.65f*strength,PuffKind.Ring,color);
            for(int i=0;i<12;i++)
            {
                var offset=new Vector3(R()-.5f,R()*.2f,R()-.5f)*.35f*strength;
                var velocity=(new Vector3(R()-.5f,R()*.9f,R()-.5f)*1.7f-direction*.35f)*strength;
                Add(position+offset,velocity,.32f+R()*.28f,(.48f+R()*.45f)*strength,false);
            }
            for(int i=0;i<18;i++)
            {
                var velocity=(new Vector3(R()-.5f,R()*.8f,R()-.5f)*5-direction*.7f)*strength;
                Add(position,velocity,.24f+R()*.36f,(.045f+R()*.065f)*strength,false);
            }
            for(int i=0;i<7;i++)Add(position,new Vector3((R()-.5f)*.7f,.45f+R()*.6f,(R()-.5f)*.7f),.65f+R()*.4f,(.35f+R()*.25f)*strength,true);
        }
        void Add(Vector3 p,Vector3 v,float life,float size,bool isSmoke)
        {AddPuff(p,v,life,size,isSmoke?PuffKind.Smoke:PuffKind.Fire,Color.white);}
        void AddTint(Vector3 p,Vector3 v,float life,float size,Color color)
        {AddPuff(p,v,life,size,PuffKind.Tint,color);}
        void AddPuff(Vector3 position,Vector3 velocity,float life,float size,PuffKind kind,Color color)
        {
            // Reserve capacity for impacts even with many simultaneous trails.
            if(puffs.Count>=(kind==PuffKind.Tint?650:900))return;
            var puff=pool.Count>0?pool.Pop():new Puff();
            puff.Position=position;puff.Velocity=velocity;puff.Age=0;puff.Life=life;puff.Size=size;puff.Kind=kind;puff.Color=color;puffs.Add(puff);
        }
        static Color HDR(Color color,float intensity,float alpha)=>new Color(color.r*intensity,color.g*intensity,color.b*intensity,alpha);
        public void HealthBar(Vector3 position,int health,int maximum,Color color,Camera camera)
        {
            var rotation=camera.transform.rotation;
            properties.SetColor("_BaseColor",new Color(.015f,.025f,.04f,.92f));
            Graphics.DrawMesh(quad,Matrix4x4.TRS(position,rotation,new Vector3(.76f,.075f,1)),solid,0,camera,0,properties,false,false,false);
            int filled=Mathf.Clamp(health,0,maximum);
            float step=.69f/maximum;
            for(int i=0;i<maximum;i++)
            {
                properties.SetColor("_BaseColor",i<filled?(health<=1?new Color(1,.2f,.12f):Color.Lerp(color,Color.white,.25f)):new Color(.16f,.18f,.21f));
                var p=position+camera.transform.right*(-.345f+step*(i+.5f))-camera.transform.forward*.005f;
                Graphics.DrawMesh(quad,Matrix4x4.TRS(p,rotation,new Vector3(step-.014f,.035f,1)),solid,0,camera,0,properties,false,false,false);
            }
        }
        void Draw(Vector3 position,Quaternion rotation,float size,Color color,Material material,Camera camera)
        {
            properties.SetColor("_BaseColor",color);
            Graphics.DrawMesh(quad,Matrix4x4.TRS(position,rotation,Vector3.one*size),material,0,camera,0,properties,false,false,false);
        }
        public void Clear(){foreach(var puff in puffs)pool.Push(puff);puffs.Clear();}
        public void Tick(float dt,Camera camera)
        {
            for(int i=puffs.Count-1;i>=0;i--)
            {
                var p=puffs[i];p.Age+=dt;if(p.Age>=p.Life){pool.Push(p);puffs.RemoveAt(i);continue;}
                p.Position+=p.Velocity*dt;p.Velocity*=Mathf.Exp(-dt*4);float t=p.Age/p.Life;
                bool isSmoke=p.Kind==PuffKind.Smoke,isRing=p.Kind==PuffKind.Ring;
                var color=isSmoke?new Color(.2f,.18f,.17f,Mathf.Sin(t*Mathf.PI)*.48f):
                    p.Kind==PuffKind.Tint||isRing?HDR(p.Color,2,(1-t)*(1-t)):
                    Color.Lerp(new Color(2.8f,1.8f,.38f,1),new Color(1.1f,.07f,.005f,0),t);
                float size=p.Size*(isSmoke?.7f+t*1.8f:isRing?.3f+t*1.5f:p.Kind==PuffKind.Fire?.65f+Mathf.Sin(t*Mathf.PI)*.65f:1-t*.55f);
                Draw(p.Position,isRing?Quaternion.Euler(90,0,0):camera.transform.rotation,size,color,isSmoke?smoke:isRing?shockwave:fire,camera);
            }
        }
        void OnDestroy(){Destroy(fire);Destroy(smoke);Destroy(shockwave);Destroy(solid);Destroy(quad);Destroy(soft);Destroy(ring);}
    }
}

