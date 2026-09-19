using System;
using System.Linq;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    [DisallowMultipleComponent]
    public sealed class GroundTurret : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField, Min(.5f)] float attackRange=3;
        [SerializeField, Min(1)] int damage=1;
        [SerializeField, Min(.05f)] float fireCooldown=1.2f;
        [SerializeField, Min(.05f)] float reloadDuration=4.033333f;
        [SerializeField, Min(0)] float cardinalTurnDuration=.18f;
        [SerializeField, Min(1)] int health=4;
        [SerializeField] float muzzleDistance=.86f;
        [SerializeField] LayerMask targetLayers=~0;
        [Header("Authored one-shot clips")]
        [SerializeField] GameObject animationRoot;
        [SerializeField] AnimationClip deployClip,undeployClip,gunFireClip,reloadClip;
        [Header("Separate gameplay colliders")]
        [SerializeField] BoxCollider blockingCollider,clearanceCollider,groundCollider;
        [Header("Attack light")]
        [SerializeField] bool attackLightEnabled=true;
        [SerializeField, Min(.1f)] float attackLightRange=5;
        [SerializeField, Min(0)] float attackLightIntensity=54;
        [SerializeField] Color attackLightColor=new Color(1,.47f,.08f);
        [SerializeField, Range(1,179)] float attackLightOuterCone=30;
        [Header("Reload indicators")]
        [SerializeField] Color readyIndicatorColor=new Color(.08f,1,.18f);
        [SerializeField] Color reloadIndicatorColor=new Color(1,.035f,.015f);
        [Header("Audio")]
        [SerializeField] AudioClip fireSound;
        [SerializeField, Range(0,1)] float fireVolume=.8f;

        Transform turretYaw,muzzleFlash;
        Transform[] green=new Transform[4],red=new Transform[4];
        Light rangeLight,muzzleLight;
        Light[] lights;
        AudioSource audioSource;
        MaterialPropertyBlock indicatorBlock;
        bool initialized;
        int lastFireSequence;
        float yaw;
        public float AttackRange=>attackRange;
        public int Damage=>damage;
        public float FireCooldown=>fireCooldown;
        public float ReloadDuration=>reloadDuration;
        public float CardinalTurnDuration=>cardinalTurnDuration;
        public float DeployDuration=>deployClip?deployClip.length:3.033333f;
        public int Health=>health;
        public float MuzzleDistance=>muzzleDistance;
        public LayerMask TargetLayers=>targetLayers;
        public Vector3 MuzzlePosition=>muzzleFlash?muzzleFlash.position:transform.position;

        void Awake(){Initialize();}
        public void Initialize()
        {
            if(initialized)return;
            if(!animationRoot||!deployClip||!undeployClip||!gunFireClip||!reloadClip)
                throw new InvalidOperationException("GroundTurret requires the armored-shell model and all four runtime clips.");
            // Sampling one selected transition at authoritative time stops the opposite
            // action immediately and holds its final pose without an independent clock.
            foreach(var animator in animationRoot.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var animation in animationRoot.GetComponentsInChildren<Animation>(true)){animation.Stop();animation.enabled=false;}
            turretYaw=Find("TurretYaw");muzzleFlash=Find("MuzzleFlash");
            for(int i=0;i<4;i++){green[i]=Find("Reload_"+(i+1)+"_Green");red[i]=Find("Reload_"+(i+1)+"_Red");}
            if(!turretYaw||!muzzleFlash||green.Any(t=>!t)||red.Any(t=>!t)||!blockingCollider||!clearanceCollider||!groundCollider)
                throw new InvalidOperationException("GroundTurret hierarchy or collider references are incomplete.");
            rangeLight=Find("AttackRange_Light").GetComponentInChildren<Light>(true);
            if(!rangeLight)rangeLight=Find("AttackRange_Light").gameObject.AddComponent<Light>();
            rangeLight.type=LightType.Spot;rangeLight.range=attackLightRange;rangeLight.intensity=attackLightIntensity;
            rangeLight.color=attackLightColor;rangeLight.spotAngle=attackLightOuterCone;rangeLight.innerSpotAngle=attackLightOuterCone*.58f;
            rangeLight.shadows=LightShadows.Soft;
            lights=animationRoot.GetComponentsInChildren<Light>(true);
            muzzleLight=lights.FirstOrDefault(l=>l.name=="MuzzleFlash_Light");
            audioSource=GetComponent<AudioSource>();if(!audioSource)audioSource=gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake=false;audioSource.spatialBlend=1;
            indicatorBlock=new MaterialPropertyBlock();
            initialized=true;
        }
        Transform Find(string name)=>animationRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);

        public void Tick(TurretState state,float dt)
        {
            Initialize();
            bool active=state.Active;
            bool transitioning=state.Phase==TurretPhase.Deploying||state.Phase==TurretPhase.Retracting;
            var clip=state.Phase==TurretPhase.Deploying||state.Phase==TurretPhase.Deployed?deployClip:undeployClip;
            float progress=transitioning?Mathf.Clamp01(state.TransitionTime/DeployDuration):1;
            // Cancel any partial recoil before applying a deployment pose.
            gunFireClip.SampleAnimation(animationRoot,gunFireClip.length);
            clip.SampleAnimation(animationRoot,progress*clip.length);

            // No root offset or bounds normalization: lower housing stays underground.
            blockingCollider.enabled=state.Alive&&state.Phase==TurretPhase.Deployed;
            clearanceCollider.enabled=state.Alive&&transitioning;
            groundCollider.enabled=state.Alive&&state.Phase==TurretPhase.Stowed;
            foreach(var light in lights)light.enabled=active;
            rangeLight.enabled=active&&attackLightEnabled;
            if(active)
            {
                float target=(int)state.Heading*90;
                yaw=state.TurnRemaining<=0?target:Mathf.LerpAngle(yaw,target,Mathf.Clamp01(dt/Mathf.Max(dt,state.TurnRemaining)));
                if(state.ShotAge<gunFireClip.length)gunFireClip.SampleAnimation(animationRoot,Mathf.Max(0,state.ShotAge));
            }
            // Deployment clips never gain authority over runtime cardinal aiming.
            turretYaw.localRotation=Quaternion.Euler(0,yaw,0);
            if(muzzleLight)muzzleLight.enabled=active&&state.ShotAge<.12f;
            if(state.FireSequence!=lastFireSequence)
            {
                lastFireSequence=state.FireSequence;
                if(active&&state.ShotAge<.2f&&fireSound)audioSource.PlayOneShot(fireSound,fireVolume);
            }
            float reloadProgress=1-Mathf.Clamp01(state.ReloadRemaining/Mathf.Max(.001f,reloadDuration));
            reloadClip.SampleAnimation(animationRoot,reloadProgress*reloadClip.length);
            int ready=state.ReloadRemaining<=0?4:Mathf.Min(3,Mathf.FloorToInt(reloadProgress*4));
            for(int i=0;i<4;i++)
            {
                bool loaded=i<ready;
                green[i].gameObject.SetActive(loaded);red[i].gameObject.SetActive(!loaded);
                // Only the chosen mesh is visible; authored clip scale keys cannot
                // leave the opposite color tiny but still rendered.
                (loaded?green[i]:red[i]).localScale=Vector3.one;
                Tint(green[i],readyIndicatorColor);Tint(red[i],reloadIndicatorColor);
            }
        }
        void Tint(Transform node,Color color)
        {
            foreach(var renderer in node.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(indicatorBlock);
                indicatorBlock.SetColor("_BaseColor",color);indicatorBlock.SetColor("_EmissionColor",color*3);
                renderer.SetPropertyBlock(indicatorBlock);indicatorBlock.Clear();
            }
        }
    }
}
