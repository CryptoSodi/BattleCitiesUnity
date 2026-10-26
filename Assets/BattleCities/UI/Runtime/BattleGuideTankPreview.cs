using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BattleCities.UI
{
    /// <summary>Isolated, noninteractive view of the actual gameplay tank model.</summary>
    public sealed class BattleGuideTankPreview : MonoBehaviour
    {
        GameObject rig; Camera previewCamera; RenderTexture target; Light previewLight;
        readonly List<Material> paint = new List<Material>();
        readonly Dictionary<string,Material> paintCache = new Dictionary<string,Material>();
        const int PreviewLayer = 29;
        static int slot;
        public void Configure(int tier, bool aimUp)
        {
            Release();
            var prefab = Resources.Load<GameObject>("TvReplayRig");
            var game = prefab ? prefab.GetComponent<BattleGame>() : null;
            if (!game || game.TankModels == null || game.TankModels.Length <= tier) return;
            var origin = new Vector3(12000 + (++slot % 32) * 20, 12000, 12000);
            rig = new GameObject("Controls guide model rig") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = origin;
            var model = Instantiate(game.TankModels[tier], rig.transform);
            model.transform.localRotation = Quaternion.Euler(0, 90, 0);
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var turret = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "TurretPivot");
            if (turret && aimUp) turret.rotation = Quaternion.identity;
            var renderers = model.GetComponentsInChildren<Renderer>();
            BattleTankAppearance.Apply(model,new Core.TankState{Player=true,Tier=tier},paintCache,paint);
            var bounds = new Bounds(origin, Vector3.zero);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            target = new RenderTexture(512, 320, 16, RenderTextureFormat.ARGB32) { name = "Controls tank", antiAliasing = 1 };
            target.Create();
            var cameraObject = new GameObject("Controls model camera", typeof(Camera));
            cameraObject.transform.SetParent(rig.transform, false);
            previewCamera = cameraObject.GetComponent<Camera>(); previewCamera.enabled = false;
            previewCamera.transform.position = bounds.center + Vector3.up * 8;
            previewCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / 1.6f) * 1.08f;
            previewCamera.nearClipPlane = .01f; previewCamera.farClipPlane = 20;
            previewCamera.cullingMask = 1 << PreviewLayer; previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear; previewCamera.targetTexture = target;
            var light = new GameObject("Controls model light", typeof(Light)).GetComponent<Light>();
            previewLight=light;
            light.transform.SetParent(rig.transform, false); light.transform.rotation = Quaternion.Euler(45, -30, 0);
            light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << PreviewLayer;
            GetComponent<RawImage>().texture = target;
            RenderPreview();
        }
        public void RenderPreview()
        {
            if(!previewCamera||!previewLight)return;
            previewLight.enabled=true;
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)previewCamera.Render();
            previewLight.enabled=false;
        }
        void Release()
        {
            if (previewCamera) previewCamera.targetTexture = null;
            if (target) { target.Release(); Dispose(target); } target = null;
            if (rig) Dispose(rig); rig = null;
            foreach(var material in paint)if(material)Dispose(material);paint.Clear();paintCache.Clear();
        }
        static void Dispose(Object value) { if(Application.isPlaying)Destroy(value);else DestroyImmediate(value); }
        void OnDestroy() => Release();
    }
}
