using TMPro;
using UnityEngine;

namespace BattleCities.UI
{
    /// <summary>Presentation time only; never advances or pauses the simulation clock.</summary>
    public sealed class BattleEndTransition : MonoBehaviour
    {
        public const float TravelSeconds = .8f;
        public const float HoldSeconds = 2;
        Canvas canvas;
        CanvasGroup group;
        TextMeshProUGUI label;
        ArcadeTextStyles styles;
        Vector2 origin;

        public void Show(Vector2 baseViewportPosition, bool won)
        {
            if (!canvas)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 150;
                group = gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                var textObject = new GameObject("Outcome", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(transform, false);
                label = textObject.GetComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 28;
                label.fontSizeMax = 72;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
                label.rectTransform.pivot = new Vector2(.5f, .5f);
                styles = new ArcadeTextStyles();
                styles.Apply(label, ArcadeTextTreatment.Gold);
            }
            origin = baseViewportPosition;
            label.text = won ? "STAGE CLEAR" : "GAME OVER";
            gameObject.SetActive(true);
            Tick(0);
        }

        public bool Tick(float elapsed)
        {
            var layout = BattleHudLayout.Current;
            canvas.scaleFactor = layout.Scale;
            var safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            var center = safe.center / layout.Scale;
            var start = new Vector2(origin.x * Screen.width, origin.y * Screen.height) / layout.Scale;
            float progress = Mathf.Clamp01(elapsed / TravelSeconds);
            float eased = 1 - Mathf.Pow(1 - progress, 3);
            var rect = label.rectTransform;
            rect.sizeDelta = new Vector2(Mathf.Min(640, safe.width / layout.Scale * .9f), 128);
            rect.anchoredPosition = Vector2.Lerp(start, center, eased);
            rect.localScale = Vector3.one * Mathf.Lerp(.65f, 1, eased);
            group.alpha = Mathf.Lerp(.4f, 1, progress);
            return elapsed >= TravelSeconds + HoldSeconds;
        }

        public void Hide() => gameObject.SetActive(false);
        void OnDestroy() => styles?.Dispose();
    }
}
