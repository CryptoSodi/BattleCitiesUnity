using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Bounded cosmetic simulation. Never creates colliders or consumes gameplay RNG.
    public sealed class BattleDebris : MonoBehaviour
    {
        sealed class Fragment
        {
            public Mesh Mesh; public Material[] Materials;
            public Vector3 Position,Velocity,Spin,Scale,Center;
            public Quaternion Rotation;
            public float Age,Life,Radius;
        }
        readonly List<Fragment> fragments=new List<Fragment>();
        readonly System.Random random=new System.Random(917);
        Mesh chunk;
        Material brick,steel,roof,plaster;
        public int ActiveCount=>fragments.Count;
        const int Limit=160;
        void Awake()
        {
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk=source.GetComponent<MeshFilter>().sharedMesh;
            source.SetActive(false);Destroy(source);
            brick=new Material(Shader.Find("Universal Render Pipeline/Lit"));brick.color=new Color(.84f,.31f,.055f);brick.SetFloat("_Smoothness",.18f);
            steel=new Material(brick);steel.color=new Color(.32f,.36f,.4f);steel.SetFloat("_Metallic",.7f);
            roof=new Material(brick);roof.color=new Color(.08f,.27f,.72f);
            plaster=new Material(brick);plaster.color=new Color(.66f,.61f,.45f);
        }
        float R()=> (float)random.NextDouble();
        void Launch(Mesh mesh,Material[] materials,Vector3 position,Quaternion rotation,Vector3 scale,float strength)
        {
            if(fragments.Count>=Limit)return;
            fragments.Add(new Fragment{Mesh=mesh,Materials=materials,Position=position,Rotation=rotation,Scale=scale,Center=mesh.bounds.center,
                Velocity=new Vector3((R()-.5f)*strength,1.4f+R()*strength,(R()-.5f)*strength),Spin=new Vector3(R()-.5f,R()-.5f,R()-.5f)*600,
                Life=2.8f+R()*1.7f,Radius=Mathf.Clamp(Vector3.Scale(mesh.bounds.extents,scale).magnitude*.4f,.025f,.16f)});
        }
        public void Wall(Vector3 position,bool metal)
        {
            for(int i=0;i<3;i++)Launch(chunk,new[]{metal?steel:brick},position+new Vector3((R()-.5f)*.18f,0,(R()-.5f)*.18f),Quaternion.Euler(R()*180,R()*180,R()*180),new Vector3(.06f+R()*.06f,.045f+R()*.04f,.05f+R()*.07f),2.4f);
        }
        public void Building(Vector3 position)
        {
            for(int i=0;i<6;i++)Launch(chunk,new[]{i%2==0?roof:plaster},position,Quaternion.Euler(R()*180,R()*180,R()*180),new Vector3(.07f+R()*.07f,.035f+R()*.04f,.07f+R()*.08f),2.8f);
        }
        public void Impact(Vector3 position,Vector3 direction,bool metal,bool power)
        {
            for(int i=0;i<(power?7:3);i++)
            {
                if(fragments.Count>=Limit)break;
                Launch(chunk,new[]{metal?steel:brick},position,Quaternion.Euler(R()*180,R()*180,R()*180),new Vector3(.035f+R()*.025f,.025f+R()*.018f,.06f+R()*.045f),power?3.2f:1.8f);
                var f=fragments[fragments.Count-1];f.Velocity-=direction*(1+R()*1.5f);f.Life=.75f+R()*.65f;
            }
        }
        public void Tank(GameObject model,Vector3 center)
        {
            if(!model)return;int count=0;
            foreach(var mesh in model.GetComponentsInChildren<MeshFilter>())
            {
                // Real barrel, turret and road wheels retain the tank's materials.
                if(mesh.name!="Turret"&&mesh.name!="Barrel"&&!mesh.name.StartsWith("WheelLeft")&&!mesh.name.StartsWith("WheelRight"))continue;
                var renderer=mesh.GetComponent<MeshRenderer>();if(!renderer)continue;
                Launch(mesh.sharedMesh,renderer.sharedMaterials,mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center),mesh.transform.rotation,mesh.transform.lossyScale,3.4f);
                if(++count>=10)break;
            }
            for(int i=0;i<5;i++)Launch(chunk,new[]{steel},center,Quaternion.identity,Vector3.one*(.06f+R()*.06f),4);
        }
        public void Clear()=>fragments.Clear();
        public void Tick(float dt,Camera camera=null,int layer=0)
        {
            // Small substeps make the bounce stable even when a frame is slow.
            for(int i=fragments.Count-1;i>=0;i--)
            {
                var f=fragments[i];f.Age+=dt;if(f.Age>=f.Life){fragments.RemoveAt(i);continue;}
                float remaining=dt;
                while(remaining>0){float step=Mathf.Min(remaining,1f/60);remaining-=step;f.Velocity.y-=9.8f*step;f.Position+=f.Velocity*step;
                    if(f.Position.y<f.Radius){f.Position.y=f.Radius;f.Velocity.y=Mathf.Abs(f.Velocity.y)*.3f;f.Velocity.x*=.7f;f.Velocity.z*=.7f;f.Spin*=.65f;if(f.Velocity.y<.25f)f.Velocity.y=0;}
                    f.Rotation=Quaternion.Normalize(Quaternion.Euler(f.Spin*step)*f.Rotation);
                }
                float shrink=Mathf.Clamp01((f.Life-f.Age)/.65f);
                var matrix=Matrix4x4.TRS(f.Position,f.Rotation,f.Scale*shrink)*Matrix4x4.Translate(-f.Center);
                for(int sub=0;sub<f.Mesh.subMeshCount&&sub<f.Materials.Length;sub++)Graphics.DrawMesh(f.Mesh,matrix,f.Materials[sub],layer,camera,sub,null,ShadowCastingMode.On,true);
            }
        }
        void OnDestroy(){Destroy(brick);Destroy(steel);Destroy(roof);Destroy(plaster);}
    }
}

