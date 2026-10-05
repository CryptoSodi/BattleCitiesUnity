using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    /// <summary>Reveals the focused card without drawing an outline or changing the deployed tank.</summary>
    public sealed class TankRosterCardFocus : MonoBehaviour,ISelectHandler,IDeselectHandler
    {
        [SerializeField] TankRosterScroll roster;
        [SerializeField] UnityEngine.UI.Outline highlight;

        public void Configure(TankRosterScroll scroll)
        {
            roster=scroll;
            highlight=GetComponent<UnityEngine.UI.Outline>();
            if(highlight)highlight.enabled=false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if(highlight)highlight.enabled=false;
            if(roster)roster.Reveal((RectTransform)transform);
        }

        public void OnDeselect(BaseEventData eventData){if(highlight)highlight.enabled=false;}
        void OnDisable(){if(highlight)highlight.enabled=false;}
    }
}
