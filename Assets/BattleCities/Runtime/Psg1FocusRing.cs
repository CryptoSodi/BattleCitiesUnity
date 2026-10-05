using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities
{
    /// <summary>Reveals controller-focused controls without drawing additional indicators.</summary>
    public sealed class Psg1FocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        void Awake() => HideLegacyRing();
        void OnEnable() => HideLegacyRing();
        void HideLegacyRing()
        {
            var ring = transform.Find("Controller focus");
            if (ring) ring.gameObject.SetActive(false);
        }
        public void OnSelect(BaseEventData data)
        {
            HideLegacyRing();
            Psg1UiNavigation.Reveal(GetComponent<Selectable>());
        }
        public void OnDeselect(BaseEventData data) => HideLegacyRing();
    }
}
