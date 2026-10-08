using UnityEngine;

namespace BattleCities.UI
{
    /// <summary>Shows the current battlefield through the same frosted surface as the main-menu TV.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask))]
    public sealed class LoginGlassSurface : MonoBehaviour
    {
        [SerializeField] private Sprite roundedMask;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private UnityEngine.UI.Image[] backdropSources;
        [SerializeField, Min(.1f)] private float cornerRadius = 8f;

        private RectTransform opening;
        private UnityEngine.UI.Image maskImage, backdropImage, fallbackFog;
        private UnityEngine.UI.Mask openingMask;
        private Material surfaceMaterial, appliedTemplate, renderingMaterial;
        private readonly Vector3[] corners = new Vector3[4];
        private bool refreshing;

        public void Configure(Sprite rounded, Material template, UnityEngine.UI.Image[] sources, float radius)
        {
            roundedMask = rounded;
            materialTemplate = template;
            backdropSources = sources;
            cornerRadius = Mathf.Max(.1f, radius);
            Refresh();
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases += Refresh;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= Refresh;
            ReleaseMaterial();
        }

        private void OnDestroy() => ReleaseMaterial();

        public void Refresh()
        {
            if (!isActiveAndEnabled || refreshing || !roundedMask) return;
            refreshing = true;
            try
            {
                EnsureLayers();
                var source = ActiveBackdrop();
                bool hasSource = source && source.sprite;
                backdropImage.enabled = hasSource;
                EnsureMaterial();
                fallbackFog.color = new Color(1f, 1f, 1f, hasSource && surfaceMaterial ? 0f : .6f);
                if (!hasSource) return;

                // Mirror the whole background in this opening's coordinate space. Its
                // visible crop then stays continuous with the world outside the frame.
                source.rectTransform.GetWorldCorners(corners);
                Vector3 bottomLeft = opening.InverseTransformPoint(corners[0]);
                Vector3 topRight = opening.InverseTransformPoint(corners[2]);
                var rect = backdropImage.rectTransform;
                var size = new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
                var position = new Vector2((bottomLeft.x + topRight.x) * .5f - opening.rect.center.x,
                    (bottomLeft.y + topRight.y) * .5f - opening.rect.center.y);
                if (size.x <= 0f || size.y <= 0f) return;
                if (rect.sizeDelta != size) rect.sizeDelta = size;
                if (rect.anchoredPosition != position) rect.anchoredPosition = position;
                if (backdropImage.sprite != source.sprite) backdropImage.sprite = source.sprite;
                if (!surfaceMaterial) return;

                opening.GetWorldCorners(corners);
                Vector3 glassMin = rect.InverseTransformPoint(corners[0]);
                Vector3 glassMax = rect.InverseTransformPoint(corners[2]);
                var glassBounds = new Vector4(glassMin.x, glassMin.y,
                    glassMax.x - glassMin.x, glassMax.y - glassMin.y);
                Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(source.sprite);
                Rect bounds = rect.rect;
                var glassUv = new Vector4(
                    Mathf.Lerp(uv.x, uv.z, (glassMin.x - bounds.xMin) / bounds.width),
                    Mathf.Lerp(uv.y, uv.w, (glassMin.y - bounds.yMin) / bounds.height),
                    (glassMax.x - glassMin.x) / bounds.width * (uv.z - uv.x),
                    (glassMax.y - glassMin.y) / bounds.height * (uv.w - uv.y));

                bool changed = SetBounds(surfaceMaterial, glassBounds, glassUv);
                // A stencil Mask owns a derived material. It must have the same bounds
                // after a resize, platform switch, or a fresh Canvas stencil rebuild.
                var actualMaterial = backdropImage.materialForRendering;
                changed |= renderingMaterial != actualMaterial;
                renderingMaterial = actualMaterial;
                if (actualMaterial && actualMaterial != surfaceMaterial)
                    changed |= SetBounds(actualMaterial, glassBounds, glassUv);
                if (changed) backdropImage.SetMaterialDirty();
            }
            finally { refreshing = false; }
        }

        private UnityEngine.UI.Image ActiveBackdrop()
        {
            if (backdropSources == null) return null;
            foreach (var candidate in backdropSources)
                if (candidate && candidate.enabled && candidate.gameObject.activeInHierarchy && candidate.sprite)
                    return candidate;
            return null;
        }

        private void EnsureLayers()
        {
            if (!opening) opening = (RectTransform)transform;
            if (!maskImage) maskImage = GetComponent<UnityEngine.UI.Image>();
            if (!openingMask) openingMask = GetComponent<UnityEngine.UI.Mask>();
            maskImage.enabled = true;
            maskImage.sprite = roundedMask;
            maskImage.type = UnityEngine.UI.Image.Type.Sliced;
            maskImage.fillCenter = true;
            maskImage.preserveAspect = false;
            maskImage.material = null;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            maskImage.pixelsPerUnitMultiplier = 32f / (Mathf.Max(.1f, cornerRadius) * maskImage.pixelsPerUnit);
            openingMask.enabled = true;
            openingMask.showMaskGraphic = false;

            if (!backdropImage) backdropImage = Layer("TV Background");
            var rect = backdropImage.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            if (rect.GetSiblingIndex() != 0) rect.SetAsFirstSibling();
            backdropImage.type = UnityEngine.UI.Image.Type.Simple;
            backdropImage.preserveAspect = false;
            backdropImage.color = Color.white;

            if (!fallbackFog) fallbackFog = Layer("TV White Fog");
            var fogRect = fallbackFog.rectTransform;
            fogRect.anchorMin = Vector2.zero;
            fogRect.anchorMax = Vector2.one;
            fogRect.offsetMin = fogRect.offsetMax = Vector2.zero;
            fogRect.localScale = Vector3.one;
            if (fogRect.GetSiblingIndex() != 1) fogRect.SetSiblingIndex(1);
        }

        private UnityEngine.UI.Image Layer(string childName)
        {
            var child = transform.Find(childName);
            if (!child)
            {
                var childObject = new GameObject(childName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }
            var image = child.GetComponent<UnityEngine.UI.Image>();
            if (!image) image = child.gameObject.AddComponent<UnityEngine.UI.Image>();
            child.gameObject.layer = gameObject.layer;
            child.gameObject.SetActive(true);
            image.raycastTarget = false;
            image.maskable = true;
            return image;
        }

        private void EnsureMaterial()
        {
            if (appliedTemplate != materialTemplate || !surfaceMaterial)
            {
                ReleaseMaterial();
                if (materialTemplate)
                {
                    surfaceMaterial = new Material(materialTemplate)
                    {
                        name = "Login Glass (" + gameObject.name + ")",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    appliedTemplate = materialTemplate;
                }
            }
            if (backdropImage.material != surfaceMaterial) backdropImage.material = surfaceMaterial;
        }

        private bool SetBounds(Material material, Vector4 bounds, Vector4 uv)
        {
            if (!material.HasProperty("_GlassRect")) return false;
            bool changed = material.GetVector("_GlassRect") != bounds ||
                material.GetVector("_GlassUVRect") != uv || material.GetFloat("_CornerRadius") != cornerRadius;
            if (!changed) return false;
            material.SetVector("_GlassRect", bounds);
            material.SetVector("_GlassUVRect", uv);
            material.SetFloat("_CornerRadius", cornerRadius);
            return true;
        }

        private void ReleaseMaterial()
        {
            if (backdropImage) backdropImage.material = null;
            if (surfaceMaterial)
            {
                if (Application.isPlaying) Destroy(surfaceMaterial);
                else DestroyImmediate(surfaceMaterial);
            }
            surfaceMaterial = appliedTemplate = renderingMaterial = null;
        }
    }
}
