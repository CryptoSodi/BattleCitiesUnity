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
        int frames, displayedFps = -1, screenWidth, screenHeight;
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
            Layout();
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
            if (screenWidth != Screen.width || screenHeight != Screen.height) Layout();
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

        void Layout()
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            float scale = Mathf.Clamp(screenWidth / 1240f, .78f, 1.15f);
            panel.anchorMin = panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = new Vector2(-14 * scale, -BattleHud.TopHeightPixels - 162 * scale);
            panel.sizeDelta = new Vector2(142 * scale, 34 * scale);
            label.fontSize = Mathf.RoundToInt(22 * scale);
        }
    }
}
