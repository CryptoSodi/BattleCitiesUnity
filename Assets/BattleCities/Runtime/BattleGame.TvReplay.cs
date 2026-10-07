using System;
using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        // Reserved for the TV's render texture; never exposed to menu/world cameras.
        public const int TvReplayLayer=31;
        bool tvReplay;
        BattleReplay tvReplayData;
        RenderTexture tvReplayTarget;
        readonly Dictionary<Camera,int> tvCameraMasks=new Dictionary<Camera,int>();
        readonly Dictionary<Light,int> tvLightMasks=new Dictionary<Light,int>();
        readonly Dictionary<ReflectionProbe,int> tvProbeMasks=new Dictionary<ReflectionProbe,int>();
        readonly List<Transform> tvTransforms=new List<Transform>();
        readonly List<Light> tvLights=new List<Light>();
        public bool IsTvReplay=>tvReplay;
        public Camera TvReplayCamera=>tvReplay?gameCamera:null;

        public static BattleGame CreateTvReplay(BattleReplay recording,RenderTexture target)
        {
            ReplayJson.Validate(recording);
            if(!target)throw new ArgumentNullException(nameof(target));
            var template=Resources.Load<GameObject>("TvReplayRig");
            if(!template||template.activeSelf)throw new InvalidOperationException("TV replay renderer is unavailable.");
            var root=Instantiate(template);root.name="TV replay renderer";
            var game=root.GetComponent<BattleGame>();
            var scene=SceneManager.CreateScene("TV replay "+Guid.NewGuid().ToString("N"));
            SceneManager.MoveGameObjectToScene(root,scene);
            game.tvReplay=true;game.tvReplayData=recording;game.tvReplayTarget=target;
            game.ChaseCamera=false;game.AutomaticCamera=true;
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!camera.transform.IsChildOf(root.transform)){game.tvCameraMasks[camera]=camera.cullingMask;camera.cullingMask&=~(1<<TvReplayLayer);}
            foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!light.transform.IsChildOf(root.transform)){game.tvLightMasks[light]=light.cullingMask;light.cullingMask&=~(1<<TvReplayLayer);}
            foreach(var probe in FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!probe.transform.IsChildOf(root.transform)){game.tvProbeMasks[probe]=probe.cullingMask;probe.cullingMask&=~(1<<TvReplayLayer);}
            try
            {
                root.SetActive(true);
                if(game.ReplayPlayback==null)throw new InvalidOperationException("TV replay could not be initialized.");
                return game;
            }
            catch {game.ReleaseTvReplay();throw;}
        }
        void IsolateTvReplayVisuals()
        {
            GetComponentsInChildren(true,tvTransforms);foreach(var child in tvTransforms)child.gameObject.layer=TvReplayLayer;
            GetComponentsInChildren(true,tvLights);foreach(var light in tvLights)light.cullingMask=1<<TvReplayLayer;
        }
        void RestoreTvReplayMasks()
        {
            foreach(var pair in tvCameraMasks)if(pair.Key)pair.Key.cullingMask=pair.Value;
            foreach(var pair in tvLightMasks)if(pair.Key)pair.Key.cullingMask=pair.Value;
            foreach(var pair in tvProbeMasks)if(pair.Key)pair.Key.cullingMask=pair.Value;
            tvCameraMasks.Clear();tvLightMasks.Clear();tvProbeMasks.Clear();
        }
        public void ReleaseTvReplay()
        {
            if(!tvReplay)return;
            RestoreTvReplayMasks();gameObject.SetActive(false);
            if(gameCamera)gameCamera.targetTexture=null;
            var scene=gameObject.scene;
            if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            else Destroy(gameObject);
        }
    }
}