using UnityEngine;
using UnityEngine.EventSystems;
namespace BattleCities
{
    // The zone owns the pointer's event stream, even if it crosses the screen midpoint.
    public sealed class BattleTouchZone : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler,IInitializePotentialDragHandler
    {
        BattleTouchControl control;
        public void Configure(BattleTouchControl target){control=target;}
        public void OnInitializePotentialDrag(PointerEventData data){data.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData data){if(control)control.OnPointerDown(data);}
        public void OnDrag(PointerEventData data){if(control)control.OnDrag(data);}
        public void OnPointerUp(PointerEventData data){if(control)control.OnPointerUp(data);}
    }
}
