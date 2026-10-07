using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        void SetupBattlePostProcessing()
        {
            gameCamera.allowHDR=true;
            var cameraData=gameCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing=true;
            cameraData.volumeLayerMask|=1;
            cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var profile=Resources.Load<VolumeProfile>("BattleLook");
            if(!profile)return;
            var root=new GameObject("Battle color and bloom");root.transform.SetParent(transform,false);
            var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=profile;
            sun.shadowBias=.035f;sun.shadowNormalBias=.16f;sun.shadowNearPlane=.1f;sun.shadowStrength=.85f;
        }
    }
}
