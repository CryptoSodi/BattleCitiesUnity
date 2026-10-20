using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace BattleCities
{
    public sealed class BattleTouchControl : OnScreenControl, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private string path;
        protected override string controlPathInternal { get => path; set => path = value; }
        private int? owner;
        private bool stick, interactable = true;
        private RectTransform thumb;
        private UnityEngine.UI.Image surface;
        private Color normalColor;
        private BattleTouchAimControl turret;
        public bool Held => owner.HasValue;
        public Vector2 LastAim { get; private set; }

        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable == value) return;
                interactable = value;
                if (!value) CancelInput();
                RefreshColor();
            }
        }

        public void Configure(string binding, RectTransform handle = null)
        {
            thumb = handle;
            stick = handle != null;
            surface = GetComponent<UnityEngine.UI.Image>();
            normalColor = surface.color;
            controlPath = "<BattleTouchDevice>/" + binding;
        }

        public void ConfigureTurret(RectTransform handle)
        {
            thumb = handle;
            turret = gameObject.AddComponent<BattleTouchAimControl>();
            turret.Configure();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!interactable || owner.HasValue || data.button != PointerEventData.InputButton.Left) return;
            owner = data.pointerId;
            if (stick) OnDrag(data); else SendValueToControl(1f);
            if (turret) OnDrag(data);
            RefreshColor();
        }

        public void OnDrag(PointerEventData data)
        {
            if ((!stick && !turret) || owner != data.pointerId || !interactable) return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var point)) return;
            float radius = rect.rect.width * .34f;
            var value = Vector2.ClampMagnitude(point / radius, 1);
            thumb.anchoredPosition = value * radius;
            // The simulation drives in four directions; never send two directions at once.
            var cardinal = value.magnitude < .2f ? Vector2.zero : Mathf.Abs(value.x) > Mathf.Abs(value.y)
                ? new Vector2(Mathf.Sign(value.x), 0) : new Vector2(0, Mathf.Sign(value.y));
            if (stick) SendValueToControl(cardinal);
            else
            {
                turret.SetAim(cardinal);
                if (cardinal != Vector2.zero) LastAim = cardinal;
            }
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (owner == data.pointerId) Release(false);
        }

        public void CancelInput()
        {
            Release(true);
        }

        private void Release(bool cancelled)
        {
            owner = null;
            if (stick) { SendValueToControl(Vector2.zero); if (thumb) thumb.anchoredPosition = Vector2.zero; }
            else SendValueToControl(0f);
            if (turret) turret.SetAim(Vector2.zero);
            if (thumb) thumb.anchoredPosition = Vector2.zero;
            if (cancelled) LastAim = Vector2.zero;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (surface) surface.color = !interactable ? new Color(normalColor.r, normalColor.g, normalColor.b, .3f)
                : Held ? new Color(1f, .72f, .12f, .95f) : normalColor;
        }

        protected override void OnDisable() { CancelInput(); base.OnDisable(); }
    }
}
