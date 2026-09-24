using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    [CreateAssetMenu(fileName = "TankShootingSettings", menuName = "Battle Cities/Tank Shooting Settings")]
    public sealed class TankShootingSettings : ScriptableObject
    {
        [Tooltip("Seconds between normal shots. Smaller values reload faster.")]
        [SerializeField, Min(BattleSimulation.StepSeconds)] private float normalReloadSeconds = .12f;
        [Tooltip("Normal-shot reload time after reaching upgrade tier 2 or higher.")]
        [SerializeField, Min(BattleSimulation.StepSeconds)] private float upgradedNormalReloadSeconds = .08f;

        public float NormalReloadSeconds => normalReloadSeconds;
        public float UpgradedNormalReloadSeconds => upgradedNormalReloadSeconds;
    }
}
