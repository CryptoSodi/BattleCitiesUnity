using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed class RankingRowFocus : MonoBehaviour,ISelectHandler
    {
        TankRosterScroll scroll;
        public void Configure(TankRosterScroll owner){scroll=owner;}
        public void OnSelect(BaseEventData data){if(scroll)scroll.Reveal(transform as RectTransform);}
    }
}
