namespace BattleCities.UI
{
    public sealed partial class PreBattleScreen
    {
        BattleGuideScreen battleGuide;
        bool acceptingGuide;
        bool TryShowBattleGuide()
        {
            if (acceptingGuide || !BattleGuideScreen.ShouldShow) return false;
            if (!battleGuide)
            {
                var panel = new UnityEngine.GameObject("Battle station", typeof(UnityEngine.RectTransform));
                panel.transform.SetParent(root, false);
                var bounds = (UnityEngine.RectTransform)panel.transform;
                bounds.anchorMin = UnityEngine.Vector2.zero; bounds.anchorMax = UnityEngine.Vector2.one;
                bounds.offsetMin = bounds.offsetMax = UnityEngine.Vector2.zero;
                battleGuide = panel.AddComponent<BattleGuideScreen>();
                battleGuide.Configure(theme, art);
            }
            int generation = loadoutGeneration;
            battleGuide.Open(selected, () =>
            {
                if (!this || !IsLoadout || !IsOpen || generation != loadoutGeneration) return;
                acceptingGuide = true;
                try { Continue(); } finally { acceptingGuide = false; }
            });
            return true;
        }
    }
}
