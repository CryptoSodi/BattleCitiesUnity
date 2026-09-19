using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Independent cosmetic weather; never reads or changes gameplay state.
    public sealed class BattleWeather : MonoBehaviour
    {
        public bool Cycle=true,Rain=true,Clouds=true;
        [Range(0,1)] public float TimeOfDay=.32f;
        [Range(45,600)] public float CycleSeconds=180;
        [Range(0,1)] public float RainIntensity=0;
        public float Darkness {get;private set;}
        public int RainCount=>Rain?drops.Length:0;
        readonly Vector3[] drops=new Vector3[420];
        readonly Vector3[] vertices=new Vector3[840];
        readonly int[] indices=new int[840];
        readonly Vector2[] cloudSeeds=new Vector2[7];
        readonly System.Random random=new System.Random(4417);
        Mesh rainMesh,cloudMesh;
        Material rainMaterial,cloudMaterial;
        Texture2D cloudTexture;
        MaterialPropertyBlock tint;
        float age,width=13,depth=13;
        float R()=>(float)random.NextDouble();
        void Awake()
        {
            rainMesh=new Mesh{name="Weather rain streaks"};rainMesh.MarkDynamic();
            for(int i=0;i<indices.Length;i++)indices[i]=i;
            for(int i=0;i<drops.Length;i++)drops[i]=new Vector3(R(),R()*5,R());
            for(int i=0;i<cloudSeeds.Length;i++)cloudSeeds[i]=new Vector2(R(),R());
            cloudTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);cloudTexture.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                Vector2 p=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f);
                float a=0;
                for(int k=0;k<5;k++){Vector2 c=new Vector2((k-2)*.24f,Mathf.Sin(k*2)*.13f);float d=Vector2.Distance(p,c);a=Mathf.Max(a,Mathf.SmoothStep(0,1,Mathf.Clamp01((.52f-d)/.3f)));}
                cloudTexture.SetPixel(x,y,new Color(1,1,1,a));
            }
            cloudTexture.Apply();rainMaterial=Transparent();rainMaterial.SetColor("_BaseColor",new Color(.7f,.85f,1,.45f));
            cloudMaterial=Transparent();cloudMaterial.SetTexture("_BaseMap",cloudTexture);
            cloudMesh=new Mesh();cloudMesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f)};cloudMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};cloudMesh.triangles=new[]{0,2,1,0,3,2};cloudMesh.RecalculateBounds();tint=new MaterialPropertyBlock();
        }
        Material Transparent()
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;return m;
        }
        public void Tick(float dt,float stageWidth,float stageDepth,Light sun,ReflectionProbe probe)
        {
            width=stageWidth;depth=stageDepth;age+=dt;
            if(Cycle)TimeOfDay=Mathf.Repeat(TimeOfDay+dt/Mathf.Max(45,CycleSeconds),1);
            float altitude=Mathf.Sin((TimeOfDay-.25f)*Mathf.PI*2);
            float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.38f,altitude));Darkness=1-daylight;
            float storm=Rain?Mathf.Lerp(1,.68f,RainIntensity):1;
            sun.intensity=Mathf.Lerp(.045f,2.1f,daylight)*storm;
            sun.color=Color.Lerp(new Color(.45f,.58f,1),Color.Lerp(new Color(1,.58f,.3f),new Color(1,.96f,.85f),Mathf.Clamp01(altitude*2)),daylight);
            sun.transform.rotation=Quaternion.Euler(Mathf.Lerp(20,70,Mathf.Clamp01(altitude)),-35,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.025f,.04f,.08f),new Color(.6f,.72f,.85f),daylight)*storm;
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.018f,.026f,.045f),new Color(.3f,.38f,.44f),daylight)*storm;
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.012f,.014f,.02f),new Color(.2f,.18f,.14f),daylight);
            if(probe)probe.intensity=Mathf.Lerp(.035f,.75f,daylight)*storm;
            if(Rain&&RainIntensity>0)
            {
                int count=Mathf.RoundToInt(drops.Length*RainIntensity);
                for(int i=0;i<count;i++){var p=drops[i];p.y-=dt*7;p.x=Mathf.Repeat(p.x+dt*.025f,1);if(p.y<0)p.y+=5;drops[i]=p;var v=new Vector3(p.x*width,p.y,-p.z*depth);vertices[i*2]=v;vertices[i*2+1]=v+new Vector3(-.035f,.22f,.015f);}
                rainMesh.vertices=vertices;rainMesh.SetIndices(indices,0,count*2,MeshTopology.Lines,0);rainMesh.bounds=new Bounds(new Vector3(width/2,2.5f,-depth/2),new Vector3(width+2,7,depth+2));
                Graphics.DrawMesh(rainMesh,Matrix4x4.identity,rainMaterial,0,null,0,null,false,false,false);
            }
            if(Clouds)for(int i=0;i<cloudSeeds.Length;i++)
            {
                var seed=cloudSeeds[i];float x=Mathf.Repeat(seed.x*(width+8)+age*.17f,width+8)-4;float z=-seed.y*depth;
                tint.SetColor("_BaseColor",Color.Lerp(new Color(.22f,.28f,.4f,.13f),new Color(.94f,.97f,1,.2f),daylight));
                Graphics.DrawMesh(cloudMesh,Matrix4x4.TRS(new Vector3(x,2.8f,z),Quaternion.Euler(0,i*37,0),new Vector3(4.5f,1,2.5f)),cloudMaterial,0,null,0,tint,false,false,false);
                tint.SetColor("_BaseColor",new Color(.08f,.11f,.16f,.1f*daylight));
                Graphics.DrawMesh(cloudMesh,Matrix4x4.TRS(new Vector3(x+.6f,.012f,z-.4f),Quaternion.Euler(0,i*37,0),new Vector3(4.8f,1,2.8f)),cloudMaterial,0,null,0,tint,false,false,false);
            }
        }
        void OnDestroy(){Destroy(rainMesh);Destroy(cloudMesh);Destroy(rainMaterial);Destroy(cloudMaterial);Destroy(cloudTexture);}
    }
}
