using System.Collections.Generic;
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
        private readonly Queue<bool> primaryShots = new Queue<bool>();
        private int chargingTankId;

        private void ResetPrimaryFire()
        {
            primaryCharge.Reset();
            primaryShots.Clear();
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
            var shot = primaryCharge.Tick(fire.WasPressedThisFrame(), fire.WasReleasedThisFrame(), fire.IsPressed(), dt);
            // Preserve short taps between fixed steps; cap the buffer to avoid long backlogs.
            if (shot.HasValue && primaryShots.Count < 4) primaryShots.Enqueue(shot.Value);
        }

        private void QueuePrimaryCommand(ref Command command)
        {
            command.Fire = false;
            command.PowerShot = false;
            var player = Simulation.Player;
            if (player == null || player.Id != chargingTankId) { ResetPrimaryFire(); return; }
            if (primaryShots.Count == 0 || !Simulation.CanFire(player)) return;
            command.Fire = true;
            command.PowerShot = primaryShots.Dequeue();
        }
    }
}
