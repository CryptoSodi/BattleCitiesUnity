using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed class SettingsControlVisual : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        Image focus,selection;TMP_Text caption;ArcadeTextStyles styles;bool active;Button button;
        public bool Hovered {get;private set;}
        public bool Focused=>button&&button.interactable&&(Hovered||(EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject));
        public void Configure(Image highlight,Image gold,TMP_Text label,ArcadeTextStyles textStyles)
        {button=GetComponent<Button>();focus=highlight;selection=gold;caption=label;styles=textStyles;Hovered=false;Refresh();}
        public void SetActive(bool value){active=value;Refresh();}
        public void Refresh(){if(focus)focus.enabled=Focused;if(selection)selection.enabled=active;styles?.ApplyCleanButton(caption,active);}
        public void OnPointerEnter(PointerEventData e){Hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData e){Hovered=false;Refresh();}
        public void OnSelect(BaseEventData e){Refresh();}
        public void OnDeselect(BaseEventData e){if(focus)focus.enabled=Hovered;}
        void OnDisable(){Hovered=false;}
    }
}
