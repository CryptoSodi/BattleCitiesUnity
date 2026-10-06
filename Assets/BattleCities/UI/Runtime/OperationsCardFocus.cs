using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed class OperationsCardFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        OperationsScreen screen;
        int index;
        public void Configure(OperationsScreen owner, int item) { screen=owner;index=item; }
        public void OnSelect(BaseEventData data) { if(screen)screen.FocusCard(index,true,false); }
        public void OnDeselect(BaseEventData data) { if(screen)screen.FocusCard(index,false,false); }
        public void OnPointerEnter(PointerEventData data) { if(screen)screen.FocusCard(index,true,true); }
        public void OnPointerExit(PointerEventData data) { if(screen)screen.FocusCard(index,false,true); }
    }
}
