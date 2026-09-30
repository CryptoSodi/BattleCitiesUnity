using BattleCities.Core;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private Facing? PlayerAim()
        {
            var keyboardAim = DirectionalAim();
            if (!ChaseCamera || IsOnline)
            {
                chaseAim = null;
                chaseAimTankId = 0;
                return keyboardAim;
            }
            var player = Simulation.Player;
            if (player == null) return keyboardAim;
            if (chaseAimTankId != player.Id)
            {
                chaseAimTankId = player.Id;
                chaseAim = player.Aim;
            }
            if (keyboardAim.HasValue) chaseAim = keyboardAim;
            return chaseAim;
        }

        private void ResetPrimaryFire()
        {
            primaryCharge.Reset();
            chargingTankId = 0;
        }

        private void SamplePrimaryFire(float dt)
        {
            var player = Simulation.Player;
            if (paused || consumePending || BlockCombat || !Simulation.CanAcceptPlayerFire)
            {
                ResetPrimaryFire();
                return;
            }
            if (chargingTankId != player.Id) { ResetPrimaryFire(); chargingTankId = player.Id; }
            bool mouseEnabled = !HasTouchControls && ChaseCamera && !IsOnline && !showDebug && Mouse.current != null &&
                Mouse.current.position.ReadValue().y >= 150 &&
                Mouse.current.position.ReadValue().y <= Screen.height - BattleHud.TopHeightPixels;
            primaryCharge.Sample(
                fire.WasPressedThisFrame() || (mouseEnabled && chaseFire.WasPressedThisFrame()),
                fire.WasReleasedThisFrame() || (mouseEnabled && chaseFire.WasReleasedThisFrame()),
                fire.IsPressed() || (mouseEnabled && chaseFire.IsPressed()),
                dt, Simulation.CanFire(player));
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
