using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image focusRing;
        [SerializeField] private RectTransform artwork;
        [SerializeField] private Image stateImage;
        [SerializeField] private Sprite inactiveSkin, activeSkin;
        [SerializeField] private bool activePage;
        private bool selected, hovered;

        public void Configure(Image ring, RectTransform art) { focusRing = ring; artwork = art; Refresh(); }
        public void ConfigureSkins(Image image,Sprite inactive,Sprite active,bool isActivePage)
        { stateImage=image;inactiveSkin=inactive;activeSkin=active;activePage=isActivePage;ResetScale();Refresh(); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; ResetScale(); Refresh(); }
        public void OnPointerDown(PointerEventData e) { if (artwork && !stateImage) artwork.localScale = Vector3.one * .97f; }
        public void OnPointerUp(PointerEventData e) { ResetScale(); }
        private void OnDisable() { selected = hovered = false; ResetScale(); Refresh(); }
        private void ResetScale() { if (artwork) artwork.localScale = Vector3.one; }
        private void Refresh()
        {
            if (focusRing) focusRing.enabled = selected || hovered;
            if(stateImage&&inactiveSkin&&activeSkin)
                stateImage.sprite=activePage||selected||hovered?activeSkin:inactiveSkin;
        }
    }
}
