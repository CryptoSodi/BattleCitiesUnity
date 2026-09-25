using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        [Header("Player tank shooting")]
        [Tooltip("Reload settings for the selected tank. Empty uses the default tank.")]
        [SerializeField] private TankShootingSettings selectedTankShootingSettings;

        // A tank selector can set this before LoadStage. Each stage snapshots the
        // settings so cooldowns remain simulation state, never shared asset state.
        public TankShootingSettings SelectedTankShootingSettings
        {
            get
            {
                if (!selectedTankShootingSettings)
                    selectedTankShootingSettings = Resources.Load<TankShootingSettings>("Tanks/DefaultTankShooting");
                return selectedTankShootingSettings;
            }
            set => selectedTankShootingSettings = value;
        }

        private readonly ChargedFireInput primaryCharge = new ChargedFireInput();
        private int chargingTankId;

        private void ResetPrimaryFire()
        {
            primaryCharge.Reset();
            chargingTankId = 0;
        }

        private void SamplePrimaryFire(float dt)
        {
            var player = Simulation.Player;
            if (paused || consumePending || !Simulation.CanAcceptPlayerFire)
            {
                ResetPrimaryFire();
                return;
            }
            if (chargingTankId != player.Id) { ResetPrimaryFire(); chargingTankId = player.Id; }
            primaryCharge.Sample(fire.WasPressedThisFrame(), fire.WasReleasedThisFrame(), fire.IsPressed(), dt, Simulation.CanFire(player));
        }

        private void QueuePrimaryCommand(ref Command command)
        {
            command.Fire = false;
            command.PowerShot = false;
            var player = Simulation.Player;
            if (player == null || player.Id != chargingTankId) { ResetPrimaryFire(); return; }
            if (!primaryCharge.TryTakeShot(Simulation.CanFire(player), out var powerShot)) return;
            command.Fire = true;
            command.PowerShot = powerShot;
        }
    }
}
