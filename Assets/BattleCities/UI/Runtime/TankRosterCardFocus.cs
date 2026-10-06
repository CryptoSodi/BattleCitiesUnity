using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    /// <summary>Reveals and highlights focus without changing the deployed tank.</summary>
    public sealed class TankRosterCardFocus : MonoBehaviour,ISelectHandler,IDeselectHandler,IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField] TankRosterScroll roster;
        CardSelectionHighlight highlight;
        bool keyboardFocused,hovered;

        public void Configure(TankRosterScroll scroll)
        {
            roster=scroll;
            highlight=CardSelectionHighlight.Ensure(GetComponent<UnityEngine.UI.Image>());
            keyboardFocused=EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            keyboardFocused=true;Refresh();
            if(roster)roster.Reveal((RectTransform)transform);
        }

        public void OnDeselect(BaseEventData eventData){keyboardFocused=false;Refresh();}
        public void OnPointerEnter(PointerEventData eventData){hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData eventData){hovered=false;Refresh();}
        void OnDisable(){keyboardFocused=hovered=false;Refresh();}
        void Refresh(){if(highlight)highlight.SetFocused(keyboardFocused||hovered);}
    }
}
