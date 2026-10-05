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
        private Vector3 artworkRestScale = Vector3.one;
        private bool artworkScaleCaptured;

        public void Configure(Image ring, RectTransform art)
        {
            if (focusRing) focusRing.enabled = false;
            focusRing = ring;
            artwork = art;
            CaptureArtworkScale();
            Refresh();
        }
        public void ConfigureSkins(Image image,Sprite inactive,Sprite active,bool isActivePage)
        {
            stateImage=image;inactiveSkin=inactive;activeSkin=active;activePage=isActivePage;
            CaptureArtworkScale();
            Refresh();
        }
        private void Awake() { CaptureArtworkScale(); }
        private void OnEnable() { CaptureArtworkScale(); Refresh(); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; ResetScale(); Refresh(); }
        public void OnPointerDown(PointerEventData e)
        {
            if (artwork && !stateImage)
                artwork.localScale = artworkRestScale * .97f;
        }
        public void OnPointerUp(PointerEventData e) { ResetScale(); }
        private void OnDisable() { selected = hovered = false; ResetScale(); Refresh(); }
        private void CaptureArtworkScale()
        {
            if (!artwork || artworkScaleCaptured) return;
            artworkRestScale = artwork.localScale;
            artworkScaleCaptured = true;
        }
        private void ResetScale()
        {
            if (artwork && artworkScaleCaptured)
                artwork.localScale = artworkRestScale;
        }
        private void Refresh()
        {
            if (focusRing) focusRing.enabled = false;
            if(stateImage&&inactiveSkin&&activeSkin)
                stateImage.sprite=activePage||selected||hovered?activeSkin:inactiveSkin;
        }
    }
}
