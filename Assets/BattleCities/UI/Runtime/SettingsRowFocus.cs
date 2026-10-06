using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed class SettingsRowFocus : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        CardSelectionHighlight highlight;bool hovered;
        public bool Focused=>hovered||(EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject);
        public void Configure(CardSelectionHighlight visual){highlight=visual;hovered=false;Refresh();}
        void Refresh(){if(highlight)highlight.SetState(false,Focused);}
        public void OnPointerEnter(PointerEventData e){hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData e){hovered=false;Refresh();}
        public void OnSelect(BaseEventData e){Refresh();}
        public void OnDeselect(BaseEventData e){if(highlight)highlight.SetState(false,hovered);}
        void OnDisable(){hovered=false;if(highlight)highlight.SetState(false,false);}
    }
}
