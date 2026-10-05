using UnityEngine;

namespace BattleCities.UI
{
    [CreateAssetMenu(menuName = "Battle Cities/UI/Menu Theme")]
    public sealed class MenuTheme : ScriptableObject
    {
        public Font HeadingFont;
        public Font BodyFont;
        public Sprite Battlefield, PsgBattlefield, AndroidLandscapeBattlefield, AndroidPortraitBattlefield, Background, RewardGarden, Logo, PlayButton;
        public Sprite BlueFrame, CreamPanel, GoldPanel, SelectedPanel, DarkPanel, SilverFrame, FocusRing, Rounded;
        public Sprite[] NavigationIcons;
        public Sprite SettingsIcon, ScoreIcon, HighScoreIcon, TrophyIcon, TimerIcon;
        public Sprite[] RewardIcons;
        public Sprite[] PrizeCrates;
        public Sprite[] StepBadges;
        public Color Navy = new Color32(6, 29, 54, 255);
        public Color Gold = new Color32(255, 220, 64, 255);
        public Color Cream = new Color32(255, 246, 222, 255);
    }
}
