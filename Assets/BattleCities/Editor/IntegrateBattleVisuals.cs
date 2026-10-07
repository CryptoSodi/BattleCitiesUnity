using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    public static class IntegrateBattleVisuals
    {
        const string Root="Assets/BattleCities/Resources/CombatVfx";
        const string Quick="Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";

        [MenuItem("Battle Cities/Build Imported Combat Visuals")]
        public static void Run()
        {
            if(!AssetDatabase.IsValidFolder(Root))AssetDatabase.CreateFolder("Assets/BattleCities/Resources","CombatVfx");
            string assetPath="Assets/BattleCities/Resources/BattleVisualAssets.asset";
            var assets=AssetDatabase.LoadAssetAtPath<BattleVisualAssets>(assetPath);
            if(!assets){assets=ScriptableObject.CreateInstance<BattleVisualAssets>();AssetDatabase.CreateAsset(assets,assetPath);}
            ConfigureWater(assets);
            var smoke=AssetDatabase.LoadAssetAtPath<Material>(Root+"/BattleSmoke.mat");
            if(!smoke){smoke=new Material(Shader.Find("BattleCities/Toon Blast Smoke"));AssetDatabase.CreateAsset(smoke,Root+"/BattleSmoke.mat");}
            var spark=AssetDatabase.LoadAssetAtPath<Material>(Root+"/BattleSpark.mat");
            if(!spark){spark=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(spark,Root+"/BattleSpark.mat");}
            Transparent(spark,true);spark.SetColor("_BaseColor",new Color(3,2.1f,.8f,1));EditorUtility.SetDirty(spark);
            assets.MuzzleFlash=Adapt("vfx_MuzzleFlash_01","BattleMuzzle");
            assets.Impact=Adapt("vfx_Impact_01","BattleImpact");
            assets.Explosion=Adapt("vfx_Explosion_02","BattleExplosion");
            assets.MuzzleScale=.26f;assets.ImpactScale=.18f;assets.ExplosionScale=.55f;
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            Debug.Log("BATTLE VISUAL ASSETS: Forest water and URP Quick Effects connected.");
        }

        [MenuItem("Battle Cities/Rebuild Cartoon Water Only")]
        public static void RebuildWater()
        {
            var assets=Resources.Load<BattleVisualAssets>("BattleVisualAssets");
            if(!assets)throw new Exception("Build imported combat visuals first");
            ConfigureWater(assets);EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            Debug.Log("CARTOON WATER: rounded cyan caustics rebuilt; combat assets preserved.");
        }

        static void ConfigureWater(BattleVisualAssets assets)
        {
            var shader=Shader.Find("BattleCities/Forest Water URP");
            if(!shader)throw new Exception("Forest water URP shader has not compiled");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AurynSky/Forest Pack/Textures/Water.psd");
            if(!texture)throw new Exception("Import Forest Pack before building visuals");
            var water=AssetDatabase.LoadAssetAtPath<Material>(Root+"/ForestWater.mat");
            if(!water){water=new Material(shader);AssetDatabase.CreateAsset(water,Root+"/ForestWater.mat");}
            water.shader=shader;water.SetTexture("_BaseMap",texture);assets.WaterMaterial=water;
            water.SetTexture("_RippleMap",BuildWaterRipples());
            water.SetColor("_DeepColor",new Color(.003f,.25f,.78f,1));
            water.SetColor("_ShallowColor",new Color(.005f,.90f,1,1));
            water.SetColor("_FoamColor",new Color(1.45f,1.85f,2,1));
            EditorUtility.SetDirty(water);
        }

        static GameObject Adapt(string name,string target)
        {
            string source=Quick+name+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(!prefab)throw new Exception("Missing URP Quick Effects prefab: "+source);
            var copy=UnityEngine.Object.Instantiate(prefab);copy.name=target;copy.SetActive(false);
            try
            {
                foreach(var node in copy.GetComponentsInChildren<Transform>(true))node.gameObject.layer=0;
                // Authored art remains intact; game events and pooled lights own all behavior.
                foreach(var script in copy.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
                foreach(var collider in copy.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                foreach(var light in copy.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
                foreach(var audio in copy.GetComponentsInChildren<AudioSource>(true))UnityEngine.Object.DestroyImmediate(audio);
                foreach(var system in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;main.loop=false;main.playOnAwake=false;
                    main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.maxParticles=Mathf.Min(main.maxParticles,128);
                    // Pack emitters default to five-second durations even for one-frame flashes.
                    // Keep their authored bursts, then let the pool reclaim them when particles die.
                    var emission=system.emission;
                    if(emission.rateOverTime.constantMax==0&&emission.rateOverDistance.constantMax==0)
                    {
                        float end=.05f;
                        for(int i=0;i<emission.burstCount;i++)
                        {
                            var burst=emission.GetBurst(i);burst.cycleCount=1;emission.SetBurst(i,burst);
                            end=Mathf.Max(end,burst.time+.05f);
                        }
                        main.duration=end;
                    }
                    if(target=="BattleExplosion"&&system.name.StartsWith("Particle System"))
                    {
                        // Replace the stationary black flare layers with a separate rising plume.
                        if(main.startColor.color.maxColorComponent<.01f)emission.enabled=false;
                        else {main.startLifetime=.42f;main.startColor=new Color(.8f,.18f,.025f,1);}
                    }
                    var collision=system.collision;collision.enabled=false;
                    var lights=system.lights;lights.enabled=false;
                    var renderer=system.GetComponent<ParticleSystemRenderer>();
                    if(renderer)
                    {
                        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                        var materials=renderer.sharedMaterials;
                        for(int i=0;i<materials.Length;i++)materials[i]=ConvertMaterial(materials[i]);
                        renderer.sharedMaterials=materials;
                        if(renderer.trailMaterial)renderer.trailMaterial=ConvertMaterial(renderer.trailMaterial);
                    }
                }
                copy.SetActive(true);
                return PrefabUtility.SaveAsPrefabAsset(copy,Root+"/"+target+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }

        static Material ConvertMaterial(Material source)
        {
            if(!source)return null;
            string path=Root+"/"+source.name+"_URP.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,path);}
            var texture=source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):source.mainTexture;
            material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
            Transparent(material,false);EditorUtility.SetDirty(material);return material;
        }

        static Texture2D BuildWaterRipples()
        {
            // Distances, rather than painted white lines, retain thin antialiased edges and soft
            // cyan light around each closed cell. The expensive cell search runs only in the editor.
            const int size=512,cells=8;
            string path=Root+"/BattleWaterRipples.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture){texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true);texture.name="Cartoon white water ripples";AssetDatabase.CreateAsset(texture,path);}
            if(texture.width!=size||texture.height!=size)texture.Reinitialize(size,size,TextureFormat.RGBA32,true);
            var pixels=new Color[size*size];
            float Hash(int x,int y,float a,float b)
            {
                x=(x%cells+cells)%cells;y=(y%cells+cells)%cells;
                return Mathf.Repeat(Mathf.Sin(x*a+y*b+17.13f)*43758.5453f,1);
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=new Vector2((float)x/size*cells,(float)y/size*cells);
                // A gentle periodic warp rounds the cells without stretching them into ribbons.
                var phase=p*(Mathf.PI*2/cells);
                p+=new Vector2(Mathf.Sin(phase.y*3+phase.x)*.12f+Mathf.Cos(phase.x*2-phase.y)*.06f,
                    Mathf.Sin(phase.x*3-phase.y)*.12f+Mathf.Cos(phase.y*2+phase.x)*.06f);
                int cx=Mathf.FloorToInt(p.x),cy=Mathf.FloorToInt(p.y);
                float nearest=99,second=99,third=99;var nearestCenter=Vector2.zero;
                int nearestX=0,nearestY=0;float nearestWeight=1;
                Vector2 Center(int gx,int gy)=>new Vector2(gx+.5f+((gy%2+2)%2)*.5f+(Hash(gx,gy,127.1f,311.7f)-.5f)*.62f,
                    gy+.5f+(Hash(gx,gy,269.5f,183.3f)-.5f)*.62f);
                float Weight(int gx,int gy){float radius=.80f+Hash(gx,gy,83.2f,17.7f)*.4f;return 1/(radius*radius);}
                for(int oy=-2;oy<=2;oy++)for(int ox=-2;ox<=2;ox++)
                {
                    int gx=cx+ox,gy=cy+oy;
                    var center=Center(gx,gy);
                    float weight=Weight(gx,gy),d=(center-p).sqrMagnitude*weight;
                    if(d<nearest){third=second;second=nearest;nearest=d;nearestCenter=center;nearestX=gx;nearestY=gy;nearestWeight=weight;}
                    else if(d<second){third=second;second=d;}else third=Mathf.Min(third,d);
                }
                float edge=1;
                for(int oy=-2;oy<=2;oy++)for(int ox=-2;ox<=2;ox++)
                {
                    var center=Center(nearestX+ox,nearestY+oy);var between=center-nearestCenter;
                    if(between.sqrMagnitude<.00001f)continue;
                    // Different cell radii create curved shared boundaries rather than straight polygons.
                    float weight=Weight(nearestX+ox,nearestY+oy);
                    var gradient=(p-center)*weight-(p-nearestCenter)*nearestWeight;
                    float distance=((center-p).sqrMagnitude*weight-nearest)/Mathf.Max(.0001f,2*gradient.magnitude);
                    // Smooth intersections round each dark cell and pool light at three-way junctions.
                    float blend=Mathf.Max(.13f-Mathf.Abs(edge-distance),0)/.13f;
                    edge=Mathf.Min(edge,distance)-blend*blend*.13f*.25f;
                }
                float junction=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.28f,Mathf.Sqrt(third)-Mathf.Sqrt(nearest)));
                pixels[y*size+x]=new Color(Mathf.Clamp01(edge*2),Mathf.Clamp01(Mathf.Sqrt(nearest)*1.25f),
                    Hash(nearestX,nearestY,37.2f,93.1f),junction);
            }
            texture.SetPixels(pixels);texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Bilinear;texture.anisoLevel=4;
            texture.Apply(true,false);EditorUtility.SetDirty(texture);return texture;
        }

        static void Transparent(Material material,bool additive)
        {
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",additive?2:0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
        }
    }
}
