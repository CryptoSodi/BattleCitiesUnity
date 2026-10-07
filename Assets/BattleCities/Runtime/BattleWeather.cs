using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Cosmetic weather advances on the same presentation clock as combat, including pause.
    public sealed class BattleWeather : MonoBehaviour
    {
        public bool Cycle=true,Rain=true,Clouds=true;
        [Range(0,1)] public float TimeOfDay=.32f;
        [Range(45,600)] public float CycleSeconds=180;
        [Range(0,1)] public float RainIntensity=0;
        public float Darkness {get;private set;}
        public int RainCount {get;private set;}
        public int SplashCount {get;private set;}
        public int WaterRippleCount {get;private set;}
        public float PresentationAge=>age;
        public const int MaxDrops=900,MaxSplashes=160;
        const int CloudLayers=6;
        readonly Vector3[] drops=new Vector3[MaxDrops];
        readonly float[] dropSeeds=new float[MaxDrops];
        readonly Vector4[] cloudSeeds=new Vector4[CloudLayers];
        readonly MeshRenderer[] clouds=new MeshRenderer[CloudLayers],shadows=new MeshRenderer[CloudLayers];
        readonly Splash[] splashes=new Splash[MaxSplashes];
        readonly System.Random random=new System.Random(4417);
        struct Splash {public Vector3 Position;public float Age,Life;public bool Water,Alive;}
        sealed class Quads
        {
            public Mesh Mesh;
            public Vector3[] Positions;
            public Color[] Colors;
            public Vector2[] Data;
            public Quads(int count,string name)
            {
                Positions=new Vector3[count*4];Colors=new Color[count*4];Data=new Vector2[count*4];
                var uv=new Vector2[count*4];var indices=new int[count*6];
                for(int i=0;i<count;i++)
                {
                    int v=i*4,t=i*6;uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;
                    indices[t]=v;indices[t+1]=v+2;indices[t+2]=v+1;indices[t+3]=v;indices[t+4]=v+3;indices[t+5]=v+2;
                }
                Mesh=new Mesh{name=name};Mesh.MarkDynamic();Mesh.vertices=Positions;Mesh.uv=uv;Mesh.triangles=indices;
            }
            public void Set(int index,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Vector2 data)
            {
                int v=index*4;Positions[v]=a;Positions[v+1]=b;Positions[v+2]=c;Positions[v+3]=d;
                for(int i=0;i<4;i++){Colors[v+i]=color;Data[v+i]=data;}
            }
            public void Upload(Bounds bounds){Mesh.vertices=Positions;Mesh.colors=Colors;Mesh.uv2=Data;Mesh.bounds=bounds;}
        }
        Quads rainQuads,splashQuads;
        Mesh cloudMesh;
        Material rainMaterial,splashMaterial,cloudMaterial;
        MeshRenderer rainRenderer,splashRenderer;
        MaterialPropertyBlock tint;
        Transform visuals;
        Camera view;
        float age,width=13,depth=13;
        float[] surfaceHeights;
        bool[] surfaceWater;
        int gridWidth,gridDepth,nextSplash;
        float R()=>(float)random.NextDouble();

        void Awake()
        {
            visuals=new GameObject("Weather visuals").transform;visuals.SetParent(transform,false);
            rainQuads=new Quads(MaxDrops,"Soft rain ribbons");splashQuads=new Quads(MaxSplashes,"Rain splashes and water rings");
            rainMaterial=new Material(Resources.Load<Shader>("BattleWeatherParticles")){name="Rain streaks"};
            splashMaterial=new Material(rainMaterial){name="Rain landing rings"};splashMaterial.SetFloat("_Splash",1);
            cloudMaterial=new Material(Resources.Load<Shader>("BattleClouds")){name="Soft shaded cartoon clouds"};
            rainRenderer=Renderer("Falling rain",rainQuads.Mesh,rainMaterial);
            splashRenderer=Renderer("Rain landings",splashQuads.Mesh,splashMaterial);
            rainRenderer.enabled=false;splashRenderer.enabled=false;
            cloudMesh=new Mesh{name="Weather cloud quad"};
            cloudMesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f)};
            cloudMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};cloudMesh.triangles=new[]{0,2,1,0,3,2};cloudMesh.RecalculateBounds();
            for(int i=0;i<MaxDrops;i++){drops[i]=new Vector3(R(),R()*5.5f,R());dropSeeds[i]=R();}
            for(int i=0;i<CloudLayers;i++)
            {
                cloudSeeds[i]=new Vector4(R(),R(),R(),R());
                clouds[i]=Renderer("Drifting cloud "+i,cloudMesh,cloudMaterial);
                shadows[i]=Renderer("Cloud ground shadow "+i,cloudMesh,cloudMaterial);
            }
            tint=new MaterialPropertyBlock();
        }
        MeshRenderer Renderer(string name,Mesh mesh,Material material)
        {
            var root=new GameObject(name);root.transform.SetParent(visuals,false);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;return renderer;
        }

        // Camera looks towards +Z: -X/+Z is top-left, +X/+Z is top-right.
        public static Vector3 DirectionToSun(float time)
        {
            float phase=Mathf.Repeat(time-.25f,1)*2;
            float progress=phase<=1?phase:2-phase;
            float elevation=Mathf.Lerp(8,phase<=1?72:50,Mathf.Sin(progress*Mathf.PI))*Mathf.Deg2Rad;
            float azimuth=Mathf.Lerp(-60,60,progress)*Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(azimuth)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Cos(azimuth)*Mathf.Cos(elevation));
        }

        public void ConfigureSurfaces(IEnumerable<Wall> terrain,float stageWidth,float stageDepth)
        {
            width=stageWidth;depth=stageDepth;gridWidth=Mathf.CeilToInt(width*4);gridDepth=Mathf.CeilToInt(depth*4);
            int count=gridWidth*gridDepth;
            if(surfaceHeights==null||surfaceHeights.Length!=count){surfaceHeights=new float[count];surfaceWater=new bool[count];}
            for(int i=0;i<count;i++){surfaceHeights[i]=-.012f;surfaceWater[i]=false;}
            foreach(var wall in terrain)
            {
                if(!wall.Alive)continue;
                bool wet=BattleTerrain.HasWaterRipples(wall.Type);
                bool surface=BattleTerrain.IsSurface(wall.Type);if(!surface&&!wall.StopsBullet&&wall.Type!="jungle")continue;
                float level=surface?BattleGame.TerrainSurfaceHeight(wall.Type)+.006f:wall.Type=="jungle"?.62f:wall.Brick?.43f:.52f;
                var b=wall.Bounds;
                int minX=Mathf.Clamp(Mathf.FloorToInt(b.X/16),0,gridWidth),maxX=Mathf.Clamp(Mathf.CeilToInt(b.Right/16),0,gridWidth);
                int minZ=Mathf.Clamp(Mathf.FloorToInt(b.Y/16),0,gridDepth),maxZ=Mathf.Clamp(Mathf.CeilToInt(b.Bottom/16),0,gridDepth);
                for(int z=minZ;z<maxZ;z++)for(int x=minX;x<maxX;x++){int index=z*gridWidth+x;surfaceHeights[index]=level;surfaceWater[index]=wet;}
            }
        }
        void Surface(Vector3 p,out float height,out bool wet)
        {
            height=-.012f;wet=false;if(surfaceHeights==null)return;
            int x=Mathf.Clamp((int)(p.x*4),0,gridWidth-1),z=Mathf.Clamp((int)(-p.z*4),0,gridDepth-1);
            int i=z*gridWidth+x;height=surfaceHeights[i];wet=surfaceWater[i];
        }
        public void ClearPrecipitation()
        {
            for(int i=0;i<MaxSplashes;i++)splashes[i].Alive=false;
            SplashCount=WaterRippleCount=0;nextSplash=0;if(splashRenderer)splashRenderer.enabled=false;
        }

        public bool ApplyGlobalLighting=true;
        public void Tick(float dt,float stageWidth,float stageDepth,Light sun,ReflectionProbe probe)
        {
            dt=Mathf.Max(0,dt);width=stageWidth;depth=stageDepth;age+=dt;
            if(Cycle)TimeOfDay=Mathf.Repeat(TimeOfDay+dt/Mathf.Max(45,CycleSeconds),1);
            float altitude=Mathf.Sin((TimeOfDay-.25f)*Mathf.PI*2);
            float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.38f,altitude));Darkness=1-daylight;
            float rain=Rain?Mathf.Clamp01(RainIntensity):0,storm=Mathf.Lerp(1,.68f,rain);
            var direction=DirectionToSun(TimeOfDay);
            sun.intensity=Mathf.Lerp(.40f,1.25f,daylight)*storm;
            sun.color=Color.Lerp(new Color(.65f,.75f,1),Color.Lerp(new Color(1,.58f,.3f),new Color(1,.96f,.85f),Mathf.Clamp01(altitude*2)),daylight);
            sun.color=Color.Lerp(sun.color,new Color(.72f,.82f,.94f),rain*.45f);
            sun.transform.rotation=Quaternion.LookRotation(-direction,Vector3.up);sun.shadowStrength=Mathf.Lerp(.88f,.50f,rain);
            if(ApplyGlobalLighting){RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.26f,.32f,.45f),new Color(.48f,.58f,.70f),daylight)*storm;
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.13f,.18f,.28f),new Color(.26f,.32f,.37f),daylight)*storm;
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.08f,.09f,.13f),new Color(.17f,.16f,.13f),daylight);}
            if(probe)probe.intensity=Mathf.Lerp(.22f,.75f,daylight)*storm;
            UpdateClouds(direction,daylight,rain);RainCount=Mathf.RoundToInt(MaxDrops*rain);
            if(!Rain)ClearPrecipitation();
            float remaining=dt;while(remaining>0){float step=Mathf.Min(.05f,remaining);AdvanceRain(step,rain);remaining-=step;}
            RenderRain(daylight,rain);
        }
        void UpdateClouds(Vector3 sunDirection,float daylight,float rain)
        {
            for(int i=0;i<CloudLayers;i++)
            {
                clouds[i].enabled=shadows[i].enabled=Clouds;if(!Clouds)continue;
                var seed=cloudSeeds[i];float height=i%2==0?3.2f:4.6f;
                float speed=(.12f+seed.z*.10f)*(1+rain*.5f);
                float x=Mathf.Repeat(seed.x*(width+9)+age*speed,width+9)-4.5f;
                float z=-Mathf.Repeat(seed.y*(depth+6)+age*speed*.32f,depth+6)+3;
                var position=new Vector3(x,height,z);var scale=new Vector3(3.8f+seed.z*1.8f,1,2.3f+seed.w*.8f);
                var rotation=Quaternion.Euler(0,seed.w*65-25,0);
                clouds[i].transform.SetPositionAndRotation(position,rotation);clouds[i].transform.localScale=scale;
                tint.Clear();
                var top=Color.Lerp(new Color(.30f,.40f,.57f),new Color(.94f,.98f,1),daylight);
                top=Color.Lerp(top,new Color(.43f,.51f,.62f),rain*.62f);top.a=Mathf.Lerp(.23f,.30f,rain)*(i%2==0?1:.65f);
                tint.SetColor("_Tint",top);tint.SetFloat("_Shadow",0);tint.SetVector("_SunDirection",Quaternion.Inverse(rotation)*sunDirection);
                tint.SetVector("_StageBounds",new Vector4(0,-depth,width,0));clouds[i].SetPropertyBlock(tint);
                var offset=Vector3.ClampMagnitude(-new Vector3(sunDirection.x,0,sunDirection.z)*height/Mathf.Max(.35f,sunDirection.y),6);
                shadows[i].transform.SetPositionAndRotation(new Vector3(x+offset.x,.006f,z+offset.z),rotation);shadows[i].transform.localScale=scale*1.08f;
                tint.SetColor("_Tint",new Color(.055f,.09f,.15f,Mathf.Lerp(.09f,.19f,daylight)*(1+rain*.25f)));
                tint.SetFloat("_Shadow",1);shadows[i].SetPropertyBlock(tint);
            }
        }
        void AdvanceRain(float dt,float intensity)
        {
            for(int i=0;i<MaxSplashes;i++)if(splashes[i].Alive)
            {splashes[i].Age+=dt;if(splashes[i].Age>=splashes[i].Life)splashes[i].Alive=false;}
            for(int i=0;i<RainCount;i++)
            {
                var p=drops[i];float seed=dropSeeds[i];
                p.x=Mathf.Repeat(p.x+dt*(1.1f+intensity)/Mathf.Max(1,width),1);
                p.z=Mathf.Repeat(p.z+dt*.22f/Mathf.Max(1,depth),1);p.y-=dt*(7.5f+seed*3);
                var world=new Vector3(p.x*width,p.y,-p.z*depth);Surface(world,out float floor,out bool wet);
                if(p.y<=floor)
                {
                    // Sample landings so water rings have time to expand before the pool is reused.
                    if(i%3==0)
                    {
                        splashes[nextSplash]=new Splash{Position=new Vector3(world.x,floor+.009f,world.z),Water=wet,Alive=true,Life=wet?.52f:.24f};
                        nextSplash=(nextSplash+1)%MaxSplashes;
                    }
                    p.y=5.2f+seed*.6f;
                }
                drops[i]=p;
            }
        }
        void RenderRain(float daylight,float intensity)
        {
            if(!view)view=GetComponentInChildren<Camera>();
            var velocity=new Vector3(1.1f+intensity,-9,-.22f).normalized;
            var side=Vector3.Cross(velocity,view?view.transform.forward:Vector3.forward).normalized;
            var bounds=new Bounds(new Vector3(width/2,2.8f,-depth/2),new Vector3(width+2,8,depth+2));rainRenderer.enabled=RainCount>0;
            if(rainRenderer.enabled)
            {
                for(int i=0;i<MaxDrops;i++)
                {
                    float seed=dropSeeds[i];var p=drops[i];var head=new Vector3(p.x*width,p.y,-p.z*depth);
                    var tail=head-velocity*(.24f+seed*.24f);var half=side*(.008f+seed*.004f);
                    var color=Color.Lerp(new Color(.43f,.65f,.95f),new Color(.76f,.91f,1),daylight);color.a=i<RainCount?(.28f+seed*.34f):0;
                    rainQuads.Set(i,head-half,head+half,tail+half,tail-half,color,Vector2.zero);
                }
                rainQuads.Upload(bounds);
            }
            SplashCount=WaterRippleCount=0;
            for(int i=0;i<MaxSplashes;i++)
            {
                var s=splashes[i];float progress=s.Alive?s.Age/s.Life:1;
                float radius=Mathf.Lerp(.015f,s.Water?.16f:.072f,progress);var right=Vector3.right*radius;var forward=Vector3.forward*radius;
                var color=Color.Lerp(new Color(.32f,.57f,.83f),new Color(.68f,.90f,1),daylight);
                color.a=s.Alive?Mathf.Pow(1-progress,1.3f)*(s.Water?.46f:.42f):0;
                splashQuads.Set(i,s.Position-right-forward,s.Position+right-forward,s.Position+right+forward,s.Position-right+forward,color,new Vector2(s.Water?1:0,progress));
                if(s.Alive){SplashCount++;if(s.Water)WaterRippleCount++;}
            }
            splashRenderer.enabled=SplashCount>0;if(splashRenderer.enabled)splashQuads.Upload(bounds);
        }
        void OnDestroy()
        {
            if(visuals)Destroy(visuals.gameObject);if(rainQuads!=null)Destroy(rainQuads.Mesh);if(splashQuads!=null)Destroy(splashQuads.Mesh);
            Destroy(cloudMesh);Destroy(rainMaterial);Destroy(splashMaterial);Destroy(cloudMaterial);
        }
    }
}
