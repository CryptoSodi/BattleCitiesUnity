using UnityEngine;

namespace BattleCities.UI
{
    public sealed class WebFullscreenArt : ScriptableObject
    {
        public Sprite expandBlue,expandGold,collapseBlue,collapseGold;
        public Sprite ForState(bool fullscreen,bool focused) => fullscreen
            ?(focused?collapseGold:collapseBlue):(focused?expandGold:expandBlue);
    }
}
