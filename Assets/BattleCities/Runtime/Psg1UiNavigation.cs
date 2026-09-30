using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BattleCities
{
    /// <summary>Temporary PSG1 UI actions. Dispose before giving control back to another screen.</summary>
    public sealed class Psg1UiInput : IDisposable
    {
        InputSystemUIInputModule module;
        InputActionAsset actions;
        InputActionReference oldSubmit, oldCancel, oldMove, submit, cancel, move;
        public bool MatchesCurrent => module && EventSystem.current && module.gameObject == EventSystem.current.gameObject && module.submit == submit && module.move == move;
        public bool CancelPressed => cancel && cancel.action.WasPressedThisFrame();

        public Psg1UiInput()
        {
            module = EventSystem.current ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (!module) return;
            oldSubmit = module.submit; oldCancel = module.cancel; oldMove = module.move;
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("PSG1 screen");
            var a = map.AddAction("Submit", InputActionType.Button, "<Gamepad>/buttonEast");
            a.AddBinding("<Keyboard>/enter");
            var b = map.AddAction("Cancel", InputActionType.Button, "<Gamepad>/buttonSouth");
            b.AddBinding("<Keyboard>/escape");
            var navigation = map.AddAction("Move", InputActionType.Value);
            navigation.expectedControlType = "Vector2";
            navigation.AddBinding("<Gamepad>/dpad"); navigation.AddBinding("<Gamepad>/leftStick");
            navigation.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            submit = InputActionReference.Create(a); cancel = InputActionReference.Create(b); move = InputActionReference.Create(navigation);
            module.submit = submit; module.cancel = null; module.move = move;
            map.Enable();
        }

        public void Dispose()
        {
            if (module)
            {
                // A scene change may already have installed its own actions.
                if (module.submit == submit) { module.submit = oldSubmit; module.cancel = oldCancel; }
                if (module.move == move) module.move = oldMove;
            }
            if (actions) { actions.Disable(); UnityEngine.Object.Destroy(actions); }
            if (submit) UnityEngine.Object.Destroy(submit);
            if (cancel) UnityEngine.Object.Destroy(cancel);
            if (move) UnityEngine.Object.Destroy(move);
            module = null; actions = null;
        }
    }

    public static class Psg1UiNavigation
    {
        public static bool Available(Selectable item) => item && item.IsActive() && item.IsInteractable();

        // Rows explicitly connect only currently available controls; disabled host actions cannot trap focus.
        public static void Rows(params Selectable[][] source)
        {
            var rows = source.Select(row => row.Where(Available).ToArray()).Where(row => row.Length > 0).ToArray();
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                var row = rows[y]; var up = rows[(y + rows.Length - 1) % rows.Length]; var down = rows[(y + 1) % rows.Length];
                row[x].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = up[Mathf.Min(x, up.Length - 1)], selectOnDown = down[Mathf.Min(x, down.Length - 1)],
                    selectOnLeft = row[(x + row.Length - 1) % row.Length], selectOnRight = row[(x + 1) % row.Length] };
            }
        }

        public static void KeepFocus(Transform root, Selectable preferred)
        {
            if (!root || !EventSystem.current) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            var item = selected ? selected.GetComponent<Selectable>() : null;
            if (!Available(item) || !selected.transform.IsChildOf(root))
            {
                item = Available(preferred) && preferred.transform.IsChildOf(root) ? preferred :
                    root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => Available(s) && s.navigation.mode != Navigation.Mode.None);
                EventSystem.current.SetSelectedGameObject(item ? item.gameObject : null);
            }
            if (item && !item.GetComponent<Psg1FocusRing>())
            { item.gameObject.AddComponent<Psg1FocusRing>(); Reveal(item); }
        }

        public static void Reveal(Selectable item)
        {
            var scroll = item.GetComponentInParent<ScrollRect>();
            if (!scroll || !scroll.content || !scroll.viewport || !item.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)item.transform);
            var view = scroll.viewport.rect;
            float delta = bounds.max.y > view.yMax - 4 ? view.yMax - 4 - bounds.max.y :
                bounds.min.y < view.yMin + 4 ? view.yMin + 4 - bounds.min.y : 0;
            if (Mathf.Abs(delta) < .1f) return;
            scroll.StopMovement();
            float range = Mathf.Max(0, scroll.content.rect.height - view.height);
            if (range > 0) scroll.verticalNormalizedPosition -= delta / range;
        }
    }
}
