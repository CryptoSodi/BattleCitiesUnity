using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Draws the transparent golden Phantom emblem with its original proportions.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class LoginWalletIcon : MaskableGraphic
    {
        [SerializeField] private Sprite source;
        public override Texture mainTexture => source ? source.texture : base.mainTexture;

        public void Configure(Sprite artwork)
        {
            source = artwork;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!source) return;
            var bounds = GetPixelAdjustedRect();
            var size = source.rect.size;
            float scale = Mathf.Min(bounds.width / Mathf.Max(1, size.x), bounds.height / Mathf.Max(1, size.y));
            var half = size * (scale * .5f);
            var min = bounds.center - half;
            var max = bounds.center + half;
            var uv = DataUtility.GetOuterUV(source);
            mesh.AddVert(new Vector3(min.x, min.y), color, new Vector2(uv.x, uv.y));
            mesh.AddVert(new Vector3(min.x, max.y), color, new Vector2(uv.x, uv.w));
            mesh.AddVert(new Vector3(max.x, max.y), color, new Vector2(uv.z, uv.w));
            mesh.AddVert(new Vector3(max.x, min.y), color, new Vector2(uv.z, uv.y));
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
        }
    }
}
