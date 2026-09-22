using UnityEngine;

namespace BattleCities.UI
{
    /// <summary>Fits only the design root. All child positions remain editable in the scene.</summary>
    [ExecuteAlways]
    public sealed class LoginLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform designRoot;
        [SerializeField] private Vector2 designSize = new Vector2(760, 1260);
        [SerializeField, Range(0.8f, 1f)] private float screenCoverage = 0.96f;
        [Header("Platform preview (Auto detects PSG1 device / Simulator)")]
        [SerializeField] private MainMenuPlatform platform = MainMenuPlatform.Auto;
        [Header("PSG1 only — child RectTransforms are the layout source of truth")]
        [SerializeField] private RectTransform psg1Root;
        [SerializeField] private Vector2 psg1DesignSize = new Vector2(1240, 1080);
        [SerializeField, Range(0.8f, 1f)] private float psg1ScreenCoverage = 0.98f;
        private bool lastPsg1;
        public bool UsesPsg1 => platform == MainMenuPlatform.Psg1 || (platform == MainMenuPlatform.Auto && RuntimePlatformInfo.IsPsg1);
        public void ConfigurePsg1(RectTransform root) { psg1Root = root; Fit(); }
        public void Preview(MainMenuPlatform value) { platform = value; Fit(); }
        private void Update() { if (lastPsg1 != UsesPsg1) Fit(); }
        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void OnValidate() => Fit();
        public void Configure(RectTransform root) { designRoot = root; Fit(); }
        private void Fit()
        {
            if (!designRoot || !(transform is RectTransform viewport)) return;
            var size = viewport.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            lastPsg1 = UsesPsg1;
            bool psg = lastPsg1 && psg1Root;
            designRoot.gameObject.SetActive(!psg);
            if (psg1Root) psg1Root.gameObject.SetActive(psg);
            if (psg)
            {
                psg1Root.sizeDelta = psg1DesignSize;
                psg1Root.localScale = Vector3.one * Mathf.Min(size.x / Mathf.Max(1, psg1DesignSize.x),
                    size.y / Mathf.Max(1, psg1DesignSize.y)) * psg1ScreenCoverage;
                return;
            }
            designRoot.sizeDelta = designSize;
            float scale = Mathf.Min(size.x / Mathf.Max(1, designSize.x), size.y / Mathf.Max(1, designSize.y)) * screenCoverage;
            designRoot.localScale = Vector3.one * scale;
        }
    }
}
