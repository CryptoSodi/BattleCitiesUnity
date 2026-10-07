using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BattleCities.LevelEditor
{
    public sealed class LevelEditorSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler, IScrollHandler
    {
        public LevelEditorDocument Document;
        private Vector2 lastCell = new Vector2(float.NaN, float.NaN);
        private int undoGroup, pointerId;
        private bool gesture, selecting;
        public void OnPointerDown(PointerEventData e)
        {
            if (!Document || e.button == PointerEventData.InputButton.Middle || !MapPoint(e.position, out var point)) return;
            FinishGesture(); gesture = true; pointerId = e.pointerId;
            selecting = Document.Tool == "Select" && e.button == PointerEventData.InputButton.Left;
            if (selecting) { Document.BeginMove(point); return; }
#if UNITY_EDITOR
            UnityEditor.Undo.IncrementCurrentGroup(); undoGroup = UnityEditor.Undo.GetCurrentGroup(); UnityEditor.Undo.SetCurrentGroupName("Paint level stroke");
#endif
            lastCell = new Vector2(float.NaN, float.NaN); Apply(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (!gesture || e.pointerId != pointerId) return;
            if (selecting) { if (MapPoint(e.position, out var point, true)) Document.DragMove(point); }
            else Apply(e);
        }
        public void OnPointerUp(PointerEventData e) { if (gesture && e.pointerId == pointerId) FinishGesture(); }
        public void OnEndDrag(PointerEventData e) { if (gesture && e.pointerId == pointerId) FinishGesture(); }
        private void FinishGesture(bool cancel = false)
        {
            if (!gesture) return;
            if (selecting) { if (Document) Document.EndMove(cancel); }
#if UNITY_EDITOR
            else UnityEditor.Undo.CollapseUndoOperations(undoGroup);
#endif
            gesture = selecting = false;
        }
        private void Update()
        {
            if (selecting && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) FinishGesture(true);
        }
        private void OnDisable() => FinishGesture();
        private void OnApplicationFocus(bool focus) { if (!focus) FinishGesture(); }
        public void OnScroll(PointerEventData e)
        {
            if (!gesture && Document && Document.PreviewCamera) Document.PreviewCamera.orthographicSize = Mathf.Clamp(Document.PreviewCamera.orthographicSize * (1 - e.scrollDelta.y * .08f), 2, 35);
        }
        public bool MapPoint(Vector2 screen, out Vector2 map, bool allowOutside = false)
        {
            map = default;
            var rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!Document || !Document.PreviewCamera || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out var local) || (!allowOutside && !rect.rect.Contains(local))) return false;
            Vector2 uv = new Vector2((local.x - rect.rect.xMin) / rect.rect.width, (local.y - rect.rect.yMin) / rect.rect.height);
            var ray = Document.PreviewCamera.ViewportPointToRay(uv);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return false;
            var point = ray.GetPoint(distance); map = new Vector2(point.x * 64, -point.z * 64); return true;
        }
        private void Apply(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Middle || !MapPoint(e.position, out var p)) return;
            var cell = new Vector2(Mathf.Floor(p.x / Document.Snap), Mathf.Floor(p.y / Document.Snap));
            if (cell == lastCell && Document.Tool != "Select") return;
            lastCell = cell; Document.ApplyBrush(p.x, p.y, e.button == PointerEventData.InputButton.Right);
        }
    }
}
