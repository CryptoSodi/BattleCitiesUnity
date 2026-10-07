using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Cosmetic only. All instances, particles and lights have a fixed upper bound.
    public sealed class BattleCombatVfx : MonoBehaviour
    {
        sealed class Effect
        {
            public GameObject Root, Prefab;
            public ParticleSystem[] Systems;
            public bool Active;
            public float Age, EmitUntil;
        }
        sealed class Flash { public Light Light; public float Age, Life, Intensity; }
        readonly List<Effect> effects = new List<Effect>();
        readonly Flash[] flashes = new Flash[6];
        readonly System.Random random = new System.Random(2971);
        BattleVisualAssets assets;
        ParticleSystem sparks;
        Material sparkMaterial;
        BattleBlastSmoke blastSmoke;
        const int EffectLimit = 24;
        public int ActiveEffects { get { int n=0; foreach(var e in effects) if(e.Active)n++; return n; } }
        public int ActiveSparks => sparks ? sparks.particleCount : 0;
        public int ActiveSmoke => blastSmoke ? blastSmoke.ActiveParticles : 0;
        float R() => (float)random.NextDouble();

        void Awake()
        {
            assets = Resources.Load<BattleVisualAssets>("BattleVisualAssets");
            var root = new GameObject("Pooled impact sparks");
            root.transform.SetParent(transform, false);
            sparks = root.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 384;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.25f; main.startLifetime = .4f; main.startSize = .035f;
            var emission = sparks.emission; emission.enabled = false;
            var shape = sparks.shape; shape.enabled = false;
            var color = sparks.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.5f,.08f),.55f),new GradientColorKey(new Color(.8f,.12f,.015f),1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.45f),new GradientAlphaKey(0,1)});
            color.color = gradient;
            var size = sparks.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,.15f));
            var sparkSource=Resources.Load<Material>("CombatVfx/BattleSpark");
            sparkMaterial = sparkSource ? new Material(sparkSource) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            sparkMaterial.SetColor("_BaseColor",new Color(3,2.1f,.8f,1));
            sparkMaterial.SetFloat("_Surface",1); sparkMaterial.SetFloat("_Blend",2);
            sparkMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            sparkMaterial.SetFloat("_DstBlend",(float)BlendMode.One);
            sparkMaterial.SetFloat("_ZWrite",0); sparkMaterial.SetFloat("_Cull",0);
            sparkMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); sparkMaterial.renderQueue=3000;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=sparkMaterial; renderer.renderMode=ParticleSystemRenderMode.Stretch;
            renderer.velocityScale=.045f; renderer.lengthScale=1.8f;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            var smokeRoot=new GameObject("Rising blast smoke");smokeRoot.transform.SetParent(transform,false);
            blastSmoke=smokeRoot.AddComponent<BattleBlastSmoke>();
            for(int i=0;i<flashes.Length;i++)
            {
                var light=new GameObject("Pooled combat light "+i).AddComponent<Light>();
                light.transform.SetParent(transform,false); light.type=LightType.Point;
                light.shadows=LightShadows.None; light.enabled=false;
                flashes[i]=new Flash{Light=light};
            }
        }

        public bool Muzzle(Vector3 position, Vector3 forward, float scale)
        {
            FlashAt(position, .08f, 1.8f*scale, 1.3f);
            return Play(assets ? assets.MuzzleFlash : null, position, Quaternion.LookRotation(forward), assets ? assets.MuzzleScale*scale : scale);
        }

        public bool Impact(Vector3 position, Vector3 direction, bool power, float scale)
        {
            var normal=-direction.normalized;
            for(int i=0;i<(power?26:12);i++)
            {
                var velocity=(normal*(1.7f+R()*2.5f)+new Vector3((R()-.5f)*4,1.5f+R()*3.2f,(R()-.5f)*4))*(power?1.25f:1);
                var p=new ParticleSystem.EmitParams{position=position,velocity=velocity,startLifetime=.2f+R()*.32f,
                    startSize=.016f+R()*.015f,startColor=new Color(1,.83f,.42f,1),randomSeed=(uint)random.Next(1,int.MaxValue)};
                sparks.Emit(p,1);
            }
            FlashAt(position+Vector3.up*.15f, power?.18f:.095f, power?5:2.4f, power?2.8f:1.55f);
            if(power)blastSmoke.Burst(position,.85f);
            return Play(assets ? (power ? assets.Explosion : assets.Impact) : null,position,
                Quaternion.LookRotation(normal.sqrMagnitude>.01f?normal:Vector3.up),assets?(power?assets.ExplosionScale*.8f:assets.ImpactScale)*Mathf.Max(.75f,scale):1);
        }

        public bool Burst(Vector3 position,float scale)
        {
            blastSmoke.Burst(position,scale);
            FlashAt(position+Vector3.up*.3f,.24f,5.5f,3.4f);
            return Play(assets?assets.Explosion:null,position,Quaternion.identity,assets?assets.ExplosionScale*scale:scale);
        }

        void FlashAt(Vector3 position,float life,float intensity,float range)
        {
            Flash flash=null;
            foreach(var item in flashes) if(!item.Light.enabled){flash=item;break;}
            if(flash==null)return;
            flash.Age=0;flash.Life=life;flash.Intensity=intensity;
            flash.Light.transform.position=position;flash.Light.range=range;
            flash.Light.color=new Color(1,.64f,.24f);flash.Light.intensity=intensity;flash.Light.enabled=true;
        }

        bool Play(GameObject prefab,Vector3 position,Quaternion rotation,float scale)
        {
            if(!prefab)return false;
            Effect effect=null;
            foreach(var e in effects)if(!e.Active&&e.Prefab==prefab){effect=e;break;}
            if(effect==null)
            {
                // A full pool drops this cosmetic burst; it cannot affect a shot or its damage.
                if(effects.Count>=EffectLimit)return true;
                int instances=0;foreach(var e in effects)if(e.Prefab==prefab)instances++;
                if(instances>=8)return true; // Reserve slots for the other two effect types.
                var root=Instantiate(prefab,transform);root.SetActive(false);
                effect=new Effect{Root=root,Prefab=prefab,Systems=root.GetComponentsInChildren<ParticleSystem>(true)};
                foreach(var system in effect.Systems)
                {
                    system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;main.playOnAwake=false;main.loop=false;
                    main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.maxParticles=Mathf.Min(main.maxParticles,128);
                    if(system.emission.enabled)
                        effect.EmitUntil=Mathf.Max(effect.EmitUntil,main.startDelay.constantMax+main.duration);
                    var collision=system.collision;collision.enabled=false;
                    var light=system.lights;light.enabled=false;
                }
                effects.Add(effect);
            }
            effect.Root.transform.SetPositionAndRotation(position,rotation);
            effect.Root.transform.localScale=Vector3.one*scale;
            effect.Root.SetActive(true);effect.Age=0;effect.Active=true;
            foreach(var system in effect.Systems){system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);system.Pause(false);}
            return true;
        }

        public void Tick(float dt)
        {
            // Manual simulation shares the game's pause clock, including imported effects.
            if(dt>0)sparks.Simulate(dt,false,false,false);
            blastSmoke.Tick(dt);
            foreach(var e in effects)
            {
                if(!e.Active)continue;
                e.Age+=dt;bool alive=false;
                foreach(var system in e.Systems)
                {
                    if(dt>0)system.Simulate(dt,false,e.Age<=dt,false);
                    // IsAlive remains true for paused emitters during manual simulation.
                    // Preserve delayed emissions, then recycle once the visible particles are gone.
                    alive|=system.particleCount>0;
                }
                if((e.Age>Mathf.Max(.15f,e.EmitUntil)&&!alive)||e.Age>4){e.Active=false;e.Root.SetActive(false);}
            }
            foreach(var flash in flashes)
            {
                if(!flash.Light.enabled)continue;
                flash.Age+=dt;float t=Mathf.Clamp01(flash.Age/flash.Life);
                flash.Light.intensity=flash.Intensity*(1-t)*(1-t);
                if(t>=1)flash.Light.enabled=false;
            }
        }

        public void Clear()
        {
            if(sparks)sparks.Clear();
            if(blastSmoke)blastSmoke.Clear();
            foreach(var e in effects){e.Active=false;e.Root.SetActive(false);}
            foreach(var flash in flashes)if(flash!=null)flash.Light.enabled=false;
        }
        void OnDestroy(){if(sparkMaterial)Destroy(sparkMaterial);}
    }
}
