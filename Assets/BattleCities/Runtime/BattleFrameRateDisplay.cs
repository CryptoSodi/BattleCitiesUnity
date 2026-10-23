using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities
{
    /// <summary>A lightweight, unscaled frame-rate readout below the tactical map.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BattleFrameRateDisplay : MonoBehaviour
    {
        const double SampleSeconds = .5;
        RectTransform panel;
        Text label;
        double elapsed;
        int frames, displayedFps = -1;
        Vector4 layoutKey;
        bool skipNextFrame;

        public void Initialize(Sprite background, Color color)
        {
            panel = (RectTransform)transform;
            var image = gameObject.AddComponent<Image>();
            image.sprite = background;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;

            var textObject = new GameObject("Frame rate", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(transform, false);
            label = textObject.GetComponent<Text>();
            label.text = "-- FPS";
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            UI.ArcadeTextStyles.ApplyWhite(label);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6, 2);
            rect.offsetMax = new Vector2(-6, -2);
            ApplyLayout(BattleHudLayout.Current);
        }

        void OnEnable() => ResetSample();
        void OnApplicationPause(bool paused) => ResetSample();
        void OnApplicationFocus(bool focused) => ResetSample();

        void ResetSample()
        {
            elapsed = 0;
            frames = 0;
            skipNextFrame = true;
        }

        void Update()
        {
            if (!label) return;
            if (skipNextFrame) { skipNextFrame = false; return; }
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < SampleSeconds) return;
            int fps = Mathf.RoundToInt((float)(frames / elapsed));
            if (fps != displayedFps)
            {
                displayedFps = fps;
                label.text = fps.ToString(CultureInfo.InvariantCulture) + " FPS";
            }
            elapsed = 0;
            frames = 0;
        }

        public void ApplyLayout(BattleHudLayout layout,bool androidSpecials=false)
        {
            var key=new Vector4(layout.Width,layout.Height,layout.Scale,androidSpecials?1:0);
            if(layoutKey==key)return;
            layoutKey=key;
            float scale = layout.OverlayScale;
            panel.anchorMin = panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-14 * scale, -layout.TopHeight - 162 * scale-(androidSpecials?78:0));
            panel.sizeDelta = new Vector2(142 * scale, 34 * scale);
            label.fontSize = Mathf.RoundToInt(22 * scale);
        }
    }
}
