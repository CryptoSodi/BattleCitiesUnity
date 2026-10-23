using System;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public static class BattleSpecialAbilities
    {
        public static bool Valid(SecondaryAttack ability) => ability >= SecondaryAttack.Mine && ability <= SecondaryAttack.LandDrone;
        public static SecondaryAttack First(SecondaryAttack[] abilities)
        {
            if (abilities != null) foreach (var ability in abilities) if (Valid(ability)) return ability;
            return SecondaryAttack.None;
        }
        public static bool CanSwap(SecondaryAttack[] abilities)
        {
            var first = First(abilities);
            if (abilities != null) foreach (var ability in abilities) if (Valid(ability) && ability != first) return true;
            return false;
        }
        public static SecondaryAttack Next(SecondaryAttack[] abilities, SecondaryAttack current)
        {
            if (!CanSwap(abilities)) return First(abilities);
            int index = Array.IndexOf(abilities, current);
            for (int i = 1; i <= abilities.Length; i++)
            {
                var candidate = abilities[(index + i) % abilities.Length];
                if (Valid(candidate) && candidate != current) return candidate;
            }
            return First(abilities);
        }
    }

    public sealed partial class BattleGame
    {
        [Header("Tank special abilities (one per tank until multi-special tanks are introduced)")]
        [SerializeField] private SecondaryAttack[] vanguardSpecials = { SecondaryAttack.Mine };
        [SerializeField] private SecondaryAttack[] strikerSpecials = { SecondaryAttack.PatrolDrone };
        [SerializeField] private SecondaryAttack[] twinFangSpecials = { SecondaryAttack.GroundTurret };
        [SerializeField] private SecondaryAttack[] bunkerSpecials = { SecondaryAttack.LandDrone };

        private SecondaryAttack[] AvailableSpecials
        {
            get
            {
                int tier = IsOnline ? BattlePreparation.TankTier : Simulation?.SelectedTankTier ?? BattlePreparation.TankTier;
                return tier == 1 ? strikerSpecials : tier == 2 ? twinFangSpecials : tier == 3 ? bunkerSpecials : vanguardSpecials;
            }
        }
        public bool CanSwapSpecials => BattleSpecialAbilities.CanSwap(AvailableSpecials);
        public bool TankCanSwapSpecials(int tier) => BattleSpecialAbilities.CanSwap(tier == 1 ? strikerSpecials : tier == 2 ? twinFangSpecials : tier == 3 ? bunkerSpecials : vanguardSpecials);
        private SecondaryAttack InitialSpecial => BattleSpecialAbilities.First(AvailableSpecials);
        internal SecondaryAttack SelectedSpecial => IsOnline ? onlineSecondary
            : Simulation == null || Simulation.Tick == 0 ? InitialSpecial : Simulation.EquippedSecondary;
    }
}
