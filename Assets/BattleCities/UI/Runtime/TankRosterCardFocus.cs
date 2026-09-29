using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    /// <summary>Shows keyboard/gamepad focus without changing the deployed tank.</summary>
    public sealed class TankRosterCardFocus : MonoBehaviour,ISelectHandler,IDeselectHandler
    {
        [SerializeField] TankRosterScroll roster;
        [SerializeField] UnityEngine.UI.Outline highlight;

        public void Configure(TankRosterScroll scroll)
        {
            roster=scroll;
            highlight=GetComponent<UnityEngine.UI.Outline>();
            if(!highlight)highlight=gameObject.AddComponent<UnityEngine.UI.Outline>();
            highlight.effectColor=new Color32(255,205,40,255);
            highlight.effectDistance=new Vector2(2,-2);
            highlight.useGraphicAlpha=true;
            highlight.enabled=EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if(highlight)highlight.enabled=true;
            if(roster)roster.Reveal((RectTransform)transform);
        }

        public void OnDeselect(BaseEventData eventData){if(highlight)highlight.enabled=false;}
        void OnDisable(){if(highlight)highlight.enabled=false;}
    }
}
