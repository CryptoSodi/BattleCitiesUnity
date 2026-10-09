using UnityEngine;

namespace BattleCities.UI
{
    public sealed class BattleResultsArt : ScriptableObject
    {
        public MenuTheme theme;
        public PreBattleArt panels;
        public Sprite medal, victory, defeat, share, report, enemy, players, shells;
        public Sprite[] portraits = new Sprite[4];
        public Sprite[] tanks = new Sprite[4];
    }
}
