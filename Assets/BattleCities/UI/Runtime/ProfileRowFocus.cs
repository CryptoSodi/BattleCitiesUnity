using UnityEngine;
using UnityEngine.EventSystems;
namespace BattleCities.UI
{
    public sealed class ProfileRowFocus : MonoBehaviour,ISelectHandler,IDeselectHandler,IPointerEnterHandler,IPointerExitHandler
    {
        TankRosterScroll scroll;CardSelectionHighlight highlight;bool hover,selected;
        public void Configure(TankRosterScroll owner,CardSelectionHighlight visual){scroll=owner;highlight=visual;hover=selected=false;Refresh();}
        void Refresh(){if(highlight)highlight.SetState(false,hover||selected);}
        public void OnSelect(BaseEventData e){selected=true;if(scroll)scroll.Reveal((RectTransform)transform);Refresh();}
        public void OnDeselect(BaseEventData e){selected=false;Refresh();}
        public void OnPointerEnter(PointerEventData e){hover=true;Refresh();}
        public void OnPointerExit(PointerEventData e){hover=false;Refresh();}
    }
}
