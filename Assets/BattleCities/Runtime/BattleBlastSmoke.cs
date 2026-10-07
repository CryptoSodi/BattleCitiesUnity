using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Separate cosmetic clock/RNG: adding smoke never changes the approved impact fragments.
    public sealed class BattleBlastSmoke : MonoBehaviour
    {
        sealed class Plume { public Vector3 Position; public float Scale, Age; public int Burst; }
        readonly List<Plume> plumes=new List<Plume>();
        readonly System.Random random=new System.Random(6803);
        ParticleSystem smoke;
        public int ActiveParticles=>smoke?smoke.particleCount:0;
        public int ActivePlumes=>plumes.Count;
        float R()=>(float)random.NextDouble();

        void Awake()
        {
            smoke=gameObject.AddComponent<ParticleSystem>();
            smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;main.playOnAwake=false;main.loop=false;main.maxParticles=128;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=-.025f;
            var emission=smoke.emission;emission.enabled=false;
            var shape=smoke.shape;shape.enabled=false;
            var size=smoke.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.6f),new Keyframe(.4f,1.2f),new Keyframe(1,1.7f)));
            var color=smoke.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(new Color(.65f,.54f,.45f),0),new GradientColorKey(Color.white,.2f),new GradientColorKey(new Color(.8f,.84f,.9f),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.55f,.08f),new GradientAlphaKey(.38f,.55f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var rotation=smoke.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-.18f,.18f);
            var renderer=smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=Resources.Load<Material>("CombatVfx/BattleSmoke");
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        public void Burst(Vector3 position,float scale)
        {
            if(plumes.Count>=16)return;
            position.y=Mathf.Max(.12f,position.y*.4f);
            plumes.Add(new Plume{Position=position,Scale=Mathf.Clamp(scale,.65f,1.6f)});
        }

        public void Tick(float dt)
        {
            // Short substeps keep delayed puffs and their upward motion consistent at low FPS.
            float remaining=dt;
            while(remaining>0)
            {
                float step=Mathf.Min(remaining,.05f);remaining-=step;
                for(int i=plumes.Count-1;i>=0;i--)
                {
                    var p=plumes[i];p.Age+=step;
                    while(p.Burst<5&&p.Age>=.07f+p.Burst*.15f)
                    {
                        for(int n=0;n<3;n++)
                        {
                            var particle=new ParticleSystem.EmitParams{
                                position=p.Position+new Vector3((R()-.5f)*.28f,.03f,(R()-.5f)*.28f)*p.Scale,
                                velocity=new Vector3(.05f+R()*.12f,.65f+R()*.5f,(R()-.5f)*.10f),
                                startLifetime=2.4f+R()*1.1f,startSize=(.32f+R()*.17f)*p.Scale,
                                rotation=R()*360,startColor=Color.white,randomSeed=(uint)random.Next(1,int.MaxValue)};
                            smoke.Emit(particle,1);
                        }
                        p.Burst++;
                    }
                    if(p.Burst==5)plumes.RemoveAt(i);
                }
                smoke.Simulate(step,false,false,false);
            }
        }

        public void Clear(){plumes.Clear();if(smoke)smoke.Clear();}
    }
}
