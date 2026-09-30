using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities
{
    /// <summary>Visible focus independent of artwork, tint transitions, and selected tank state.</summary>
    public sealed class Psg1FocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        RectTransform ring;
        void Awake()
        {
            ring = new GameObject("Controller focus", typeof(RectTransform)).GetComponent<RectTransform>();
            ring.SetParent(transform, false); ring.anchorMin = Vector2.zero; ring.anchorMax = Vector2.one;
            ring.offsetMin = new Vector2(2, 2); ring.offsetMax = new Vector2(-2, -2);
            Edge("Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
            Edge("Bottom", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 3));
            Edge("Left", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(3, 0));
            Edge("Right", Vector2.right, Vector2.one, new Vector2(-3, 0), Vector2.zero);
        }
        void Edge(string name, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(ring, false); image.color = new Color(1, .85f, .15f); image.raycastTarget = false;
            var rect = image.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = insetMin; rect.offsetMax = insetMax;
        }
        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();
        void Refresh()
        {
            bool focused = RuntimePlatformInfo.IsPsg1 && EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject &&
                Psg1UiNavigation.Available(GetComponent<Selectable>());
            if (ring) { ring.gameObject.SetActive(focused); if (focused) ring.SetAsLastSibling(); }
        }
        public void OnSelect(BaseEventData data) { Refresh(); Psg1UiNavigation.Reveal(GetComponent<Selectable>()); }
        public void OnDeselect(BaseEventData data) => Refresh();
        void OnDestroy() { if (ring) Destroy(ring.gameObject); }
    }
}
