using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleCities
{
    // Render the delivered gameplay art once, without running deployable behaviour.
    sealed class BattleSpecialIconCache
    {
        readonly Dictionary<(SecondaryAttack,int),RenderTexture> icons=new Dictionary<(SecondaryAttack,int),RenderTexture>();
        static (SecondaryAttack,int) Key(SecondaryAttack special,int ownerSlot)=>(special,Mathf.Clamp(ownerSlot,0,3));
        public Texture Cached(SecondaryAttack special,int ownerSlot=0)=>icons.TryGetValue(Key(special,ownerSlot),out var cached)&&cached&&cached.IsCreated()?cached:null;
        public Texture Get(SecondaryAttack special,int ownerSlot=0)
        {
            var iconKey=Key(special,ownerSlot);
            if(icons.TryGetValue(iconKey,out var cached))
            {
                if(cached&&cached.IsCreated())return cached;
                if(cached)BattleVisualLifetime.Release(cached);
                icons.Remove(iconKey);
            }
            var remote=RemoteGameArt.Load<RemoteBattleModels>("RemoteBattleModels");
            var prefab=Resources.Load<GameObject>("TvReplayRig");
            var fallback=prefab?prefab.GetComponent<BattleGame>():null;
            GameObject source=special==SecondaryAttack.Mine?(remote?remote.MineModel:fallback?.MineModel):
                special==SecondaryAttack.PatrolDrone?(remote?remote.DroneModel:fallback?.DroneModel):
                special==SecondaryAttack.GroundTurret?(remote?remote.GroundTurretPrefab:fallback?.GroundTurretPrefab):
                special==SecondaryAttack.LandDrone?RemoteGameArt.Load<GameObject>("Deployables/LandAttackDrone"):null;
            if(!source&&fallback)source=special==SecondaryAttack.Mine?fallback.MineModel:
                special==SecondaryAttack.PatrolDrone?fallback.DroneModel:special==SecondaryAttack.GroundTurret?fallback.GroundTurretPrefab:null;
            if(!source)return null;
            const int layer=29;
            var rig=new GameObject("Special HUD icon rig"){hideFlags=HideFlags.HideAndDontSave};
            // The isolated clone exists only for this render. Keep it near the origin:
            // far-away preview geometry can lose precision on mobile/WebGL shader paths.
            rig.SetActive(false);rig.transform.position=Vector3.zero;
            RenderTexture target=null;
            var paints=new List<Material>();
            try
            {
                var model=Object.Instantiate(source,rig.transform);
                BattleDeployableAppearance.Apply(model,ownerSlot,new Dictionary<string,Material>(),paints);
                var turret=model.GetComponent<GroundTurret>();
                if(turret)turret.SampleDeployedPreview();
                foreach(var node in model.GetComponentsInChildren<Transform>(true))node.gameObject.layer=layer;
                // The clone is inactive. Remove scripts before activation so Awake
                // cannot initialize weapons, lights or audio on a presentation model.
                foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(behaviour);
                foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                foreach(var animation in model.GetComponentsInChildren<Animation>(true))animation.enabled=false;
                foreach(var light in model.GetComponentsInChildren<Light>(true))light.enabled=false;
                foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var audio in model.GetComponentsInChildren<AudioSource>(true)){audio.playOnAwake=false;audio.enabled=false;}
                rig.SetActive(true);
                var renderers=model.GetComponentsInChildren<Renderer>();
                if(renderers.Length==0)return null;
                var bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var camera=new GameObject("Icon camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(rig.transform,false);
                camera.enabled=false;camera.orthographic=true;camera.aspect=1;camera.useOcclusionCulling=false;camera.allowHDR=false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                camera.orthographicSize=Mathf.Max(.05f,bounds.extents.magnitude)*1.05f;
                camera.transform.position=bounds.center+new Vector3(0,1.2f,-1).normalized*(bounds.extents.magnitude*4+2);
                camera.transform.LookAt(bounds.center);camera.nearClipPlane=.01f;camera.farClipPlane=bounds.extents.magnitude*8+10;
                Vector3 right=camera.transform.right,up=camera.transform.up,extents=bounds.extents;
                float horizontal=Mathf.Abs(right.x)*extents.x+Mathf.Abs(right.y)*extents.y+Mathf.Abs(right.z)*extents.z;
                float vertical=Mathf.Abs(up.x)*extents.x+Mathf.Abs(up.y)*extents.y+Mathf.Abs(up.z)*extents.z;
                camera.orthographicSize=Mathf.Max(.05f,Mathf.Max(horizontal,vertical))*1.08f;
                camera.cullingMask=1<<layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                target=new RenderTexture(256,256,16,RenderTextureFormat.ARGB32){name="Special icon "+special};target.Create();camera.targetTexture=target;
                var key=new GameObject("Icon light",typeof(Light)).GetComponent<Light>();key.transform.SetParent(rig.transform,false);
                key.type=LightType.Directional;key.transform.rotation=Quaternion.Euler(45,-35,0);key.intensity=2;key.cullingMask=1<<layer;
                var previous=RenderTexture.active;
                try
                {
                    var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                    if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);
                    else camera.Render();
                }
                finally { RenderTexture.active=previous;key.enabled=false;camera.targetTexture=null; }
                icons[iconKey]=target;return target;
            }
            catch { if(target){target.Release();BattleVisualLifetime.Release(target);}throw; }
            finally { rig.SetActive(false);BattleVisualLifetime.Release(rig);foreach(var paint in paints)BattleVisualLifetime.Release(paint); }
        }
        public void Dispose()
        {
            foreach(var target in icons.Values)if(target){target.Release();BattleVisualLifetime.Release(target);}
            icons.Clear();
        }
    }
}
