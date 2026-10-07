using System;
using BattleCities.LevelEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.EditorTools
{
    public static class LevelEditorMovementChecks
    {
        static int checks;
        static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("LEVEL MOVE: " + label); }
        [MenuItem("Battle Cities/Level Editor/Validate drag movement")]
        public static void Run()
        {
            checks = 0;
            var selection = Selection.activeObject;
            var root = new GameObject("Isolated movement checks") { hideFlags = HideFlags.HideAndDontSave };
            var doc = root.AddComponent<LevelEditorDocument>(); doc.TrackUndo = false; doc.BuildPreviews = false;
            doc.MapRoot = new GameObject("MapRoot").transform; doc.MapRoot.SetParent(root.transform, false);
            var element = doc.Create(LevelElementKind.Terrain, "steel", 128, 128, 64, 32);
            element.OverrideDamage = true; element.Damage.hitPoints = 7;
            var other = doc.Create(LevelElementKind.Terrain, "water", 512, 512, 64, 64);
            try
            {
                doc.SetTool("Select"); doc.Snap = 32;
                Check(doc.BeginMove(new Vector2(139,145)), "pick a map element with an offset grab point");
                Check(!doc.DragMove(new Vector2(139,145)) && element.X==128 && element.Y==128, "stationary pointer does not move the element");
                doc.DragMove(new Vector2(236,243)); doc.EndMove();
                Check(element.X==224 && element.Y==224, "pointer movement retains grab offset and snaps to 32 units");
                Check(element.Size==new Vector2(64,32) && element.Tile=="steel" && element.OverrideDamage && element.Damage.hitPoints==7, "move preserves dimensions, type and damage settings");
                Check(other.X==512 && other.Y==512 && doc.Elements.Length==2, "move does not replace other elements");
                string beforeCancel = doc.ToJson();
                doc.BeginMove(new Vector2(235,241));doc.DragMove(new Vector2(-900,-900));
                Check(element.X==0 && element.Y==0, "clamp north and west map edges");
                doc.DragMove(new Vector2(99999,99999));
                Check(element.Bounds.Right==doc.WidthTiles*64 && element.Bounds.Bottom==doc.HeightTiles*64, "clamp complete footprint at south and east edges");
                doc.EndMove(true);
                Check(doc.ToJson()==beforeCancel && !doc.IsMoving, "cancel restores the exact saved document");
                Check(!doc.BeginMove(new Vector2(400,400)) && !doc.DragMove(new Vector2(520,520)), "dragging empty space cannot pick up another item mid-drag");

                doc.TrackUndo = true;
                doc.BeginMove(new Vector2(235,241));doc.DragMove(new Vector2(267,273));doc.DragMove(new Vector2(299,305));doc.DragMove(new Vector2(331,337));doc.EndMove();
                string moved = doc.ToJson();
                Undo.PerformUndo();Check(doc.ToJson()==beforeCancel, "one Undo restores an entire drag");
                Undo.PerformRedo();Check(doc.ToJson()==moved, "Redo restores the moved position");
                Undo.ClearUndo(element.transform);doc.TrackUndo=false;

                var cameraRoot=new GameObject("Map camera",typeof(Camera));cameraRoot.transform.SetParent(root.transform,false);
                doc.PreviewCamera=cameraRoot.GetComponent<Camera>();doc.PreviewCamera.enabled=false;doc.PreviewCamera.orthographic=true;doc.PreviewCamera.aspect=1;doc.FrameMap();
                var canvasRoot=new GameObject("Test canvas",typeof(RectTransform),typeof(Canvas));canvasRoot.transform.SetParent(root.transform,false);canvasRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var viewport=new GameObject("Map viewport",typeof(RectTransform),typeof(LevelEditorSurface));viewport.transform.SetParent(canvasRoot.transform,false);
                var rect=(RectTransform)viewport.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(500,500);
                var surface=viewport.GetComponent<LevelEditorSurface>();surface.Document=doc;Canvas.ForceUpdateCanvases();
                Vector2 Screen(float x,float y)
                {
                    var uv=doc.PreviewCamera.WorldToViewportPoint(new Vector3(x/64,0,-y/64));
                    var local=new Vector3(rect.rect.xMin+uv.x*rect.rect.width,rect.rect.yMin+uv.y*rect.rect.height,0);
                    return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(local));
                }
                float startX=element.X,startY=element.Y;
                var pointer=new PointerEventData(EventSystem.current){pointerId=17,button=PointerEventData.InputButton.Left,position=Screen(startX+11,startY+17)};
                surface.OnPointerDown(pointer);
                pointer.position=Screen(startX+77,startY+50);surface.OnDrag(pointer);surface.OnPointerUp(pointer);
                Check(element.X==startX+64 && element.Y==startY+32 && !doc.IsMoving, "Game-view pointer down/drag/up moves the selected item");
                pointer.position=Screen(element.X+11,element.Y+17);surface.OnPointerDown(pointer);
                pointer.position=Screen(99999,99999);surface.OnDrag(pointer);surface.OnEndDrag(pointer);
                Check(element.Bounds.Right==doc.WidthTiles*64 && element.Bounds.Bottom==doc.HeightTiles*64 && !doc.IsMoving, "dragging and releasing outside the viewport clamps and ends the gesture");
                string saved=doc.ToJson();doc.Import(saved);Check(doc.ToJson()==saved, "moved positions survive JSON save and reopen");
                Debug.Log("[BattleCities] PASS: " + checks + " movement checks, including Game-view pointer gestures, snapping, bounds, properties, cancellation, Undo/Redo and save/reopen.");
            }
            finally
            {
                if(element)Undo.ClearUndo(element.transform);
                Selection.activeObject=selection;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
