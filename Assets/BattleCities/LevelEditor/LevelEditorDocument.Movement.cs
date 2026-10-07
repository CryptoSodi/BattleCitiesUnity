using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BattleCities.LevelEditor
{
    public sealed partial class LevelEditorDocument
    {
        [NonSerialized] LevelElement movingElement;
        [NonSerialized] Vector2 moveGrabOffset;
        [NonSerialized] Vector3 moveStart;
        [NonSerialized] bool moveChanged;
        [NonSerialized] int moveUndoGroup = -1;
        public bool IsMoving => movingElement;

        public bool BeginMove(Vector2 point)
        {
            EndMove();
            if (Tool != "Select") return false;
            if (point.x < 0 || point.y < 0 || point.x >= WidthTiles*64 || point.y >= HeightTiles*64) return false;
            Select(At(point.x, point.y));
            if (!Selected) { Status("Select: click and drag an item to move it."); return false; }
            if (Selected.Size.x > WidthTiles*64 || Selected.Size.y > HeightTiles*64) { Status("Resize this item to fit the map before moving it."); return false; }
            movingElement = Selected;
            moveGrabOffset = point - new Vector2(Selected.X, Selected.Y);
            moveStart = Selected.transform.localPosition;
            moveChanged = false; moveUndoGroup = -1;
            Status("Drag " + Selected.Tile + " to move. Escape cancels.");
            return true;
        }
        public bool DragMove(Vector2 point)
        {
            if (!movingElement || Tool != "Select") return false;
            int grid = Snap == 16 || Snap == 32 ? Snap : 64;
            Vector2 corner = point - moveGrabOffset;
            float x = Mathf.Clamp(Mathf.Round(corner.x/grid)*grid, 0, Mathf.Max(0, WidthTiles*64-movingElement.Size.x));
            float y = Mathf.Clamp(Mathf.Round(corner.y/grid)*grid, 0, Mathf.Max(0, HeightTiles*64-movingElement.Size.y));
            var target = new Vector3((x+movingElement.Size.x*.5f)/64, moveStart.y, -(y+movingElement.Size.y*.5f)/64);
            if ((movingElement.transform.localPosition-target).sqrMagnitude < .000001f) return false;
            if (!moveChanged)
            {
#if UNITY_EDITOR
                if (TrackUndo)
                {
                    Undo.IncrementCurrentGroup(); moveUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Move level element");
                    Undo.RegisterCompleteObjectUndo(movingElement.transform, "Move level element");
                }
#endif
                moveChanged = true;
            }
            movingElement.transform.localPosition = target;
#if UNITY_EDITOR
            if (TrackUndo) EditorUtility.SetDirty(movingElement.transform);
#endif
            Dirty(); RefreshSelection();
            Status("Move " + movingElement.Tile + " / " + Mathf.RoundToInt(x) + ", " + Mathf.RoundToInt(y) + " / Escape cancels");
            return true;
        }
        public void EndMove(bool cancel = false)
        {
            if (!movingElement) { moveUndoGroup = -1; moveChanged = false; return; }
            if (cancel && moveChanged) { movingElement.transform.localPosition = moveStart; Dirty(); RefreshSelection(); }
#if UNITY_EDITOR
            if (moveUndoGroup >= 0) { Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(moveUndoGroup); }
#endif
            if (moveChanged) Status(cancel ? "Move cancelled." : "Moved " + movingElement.Tile + ". Undo restores its previous position.");
            movingElement = null; moveChanged = false; moveUndoGroup = -1;
        }
        private void OnDisable() => EndMove();
    }
}
