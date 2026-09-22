using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class LoginButtonState : MonoBehaviour, ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        [SerializeField] private Image focusRing;
        [Header("Button states")]
        [SerializeField] private Color inactiveTint = new Color(.9f,.9f,.9f,1);
        [SerializeField] private Color activeTint = Color.white;
        [SerializeField] private Color pressedTint = new Color(.78f,.78f,.78f,1);
        [SerializeField] private Color disabledTint = new Color(.42f,.42f,.42f,.8f);
        [SerializeField, Range(1,1.05f)] private float activeScale = 1.015f;
        [SerializeField, Range(.9f,1)] private float pressedScale = .98f;
        private Button button;
        private Image art;
        private Vector3 restScale;
        private bool hovered, selected, pressed, lastInteractable;
        private float submitUntil;
        public void Configure(Image ring) { focusRing=ring; Cache(); Refresh(); }
        private void Cache()
        {
            if (button) return;
            button=GetComponent<Button>(); art=GetComponent<Image>(); restScale=transform.localScale;
            button.transition=Selectable.Transition.None;
        }
        private void Awake() => Cache();
        private void OnEnable()
        {
            Cache(); hovered=pressed=false; submitUntil=0;
            selected=EventSystem.current && EventSystem.current.currentSelectedGameObject==gameObject;
            Refresh();
        }
        private void OnDisable()
        {
            hovered=selected=pressed=false; submitUntil=0;
            if (art) art.color=inactiveTint;
            if (focusRing) focusRing.enabled=false;
            if (button) transform.localScale=restScale;
        }
        private void Update()
        {
            if (lastInteractable!=button.IsInteractable()) Refresh();
            if (submitUntil>0 && Time.unscaledTime>=submitUntil) { submitUntil=0; Refresh(); }
        }
        public void OnSelect(BaseEventData e) { selected=true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected=false; pressed=false; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hovered=true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered=pressed=false; Refresh(); }
        public void OnPointerDown(PointerEventData e)
        { if(e.button==PointerEventData.InputButton.Left && button.IsInteractable()) { pressed=true; Refresh(); } }
        public void OnPointerUp(PointerEventData e)
        { if(e.button==PointerEventData.InputButton.Left) { pressed=false; Refresh(); } }
        public void OnSubmit(BaseEventData e)
        { if(button.IsInteractable()) { submitUntil=Time.unscaledTime+.12f; Refresh(); } }
        private void Refresh()
        {
            if(!button || !art) return;
            lastInteractable=button.IsInteractable();
            bool active=lastInteractable && (hovered||selected);
            bool down=lastInteractable && (pressed||submitUntil>Time.unscaledTime);
            art.color=!lastInteractable?disabledTint:down?pressedTint:active?activeTint:inactiveTint;
            if(focusRing) focusRing.enabled=active&&!down;
            transform.localScale=restScale*(down?pressedScale:active?activeScale:1);
        }
    }
}
