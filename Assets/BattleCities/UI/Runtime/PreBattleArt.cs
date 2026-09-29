using UnityEngine;

namespace BattleCities.UI
{
    public sealed class PreBattleArt : ScriptableObject
    {
        public Texture2D[] tanks;
        public Texture2D powerups;
        public Sprite continueActive;
        public Sprite continueInactive;
        public Sprite backButton;
        public Sprite tankTitlePanel;
        public Sprite headerBackdrop;
        public Sprite statusPanel;
        public Sprite fuelCan;
        public Sprite buttonFuelCan;
        public Sprite tankCostButton;
        public Sprite tankCostButtonSelected;
        public Sprite tankCostButtonLocked;
        public Sprite tankCardSelected;
        public Sprite tankCardAvailable;
        public Sprite tankCardUnavailable;
        public Sprite classifiedTank;
    }
}
