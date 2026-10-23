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
        private readonly ShotAimLatch shotAim = new ShotAimLatch();
        private int chargingTankId;

        private Facing? PlayerAim()
        {
            return shotAim.Resolve(DirectionalAim());
        }

        private void ResetPrimaryFire()
        {
            primaryCharge.Reset();
            shotAim.Reset();
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
            bool pressed = fire.WasPressedThisFrame() || (mouseEnabled && chaseFire.WasPressedThisFrame());
            bool released = fire.WasReleasedThisFrame() || (mouseEnabled && chaseFire.WasReleasedThisFrame());
            bool held = fire.IsPressed() || (mouseEnabled && chaseFire.IsPressed());
            if(pressed) shotAim.Reset();
            var aim = DirectionalAim();
            if(pressed || primaryCharge.IsCharging) shotAim.ObserveAim(aim);
            primaryCharge.Sample(pressed, released, held, dt, Simulation.CanFire(player));
            if(released && !held)
            {
                var touchAim = touchControls ? touchControls.TakeReleasedTurretAim() : null;
                if(primaryCharge.HasPendingShot) shotAim.Release(aim, touchAim, PlayerMove(), player.Aim);
            }
            if(!primaryCharge.IsCharging && !primaryCharge.HasPendingShot) shotAim.Reset();
        }

        private void QueuePrimaryCommand(ref Command command)
        {
            command.Fire = false;
            command.PowerShot = false;
            command.Aim = PlayerAim();
            var player = Simulation.Player;
            if (player == null || player.Id != chargingTankId) { ResetPrimaryFire(); command.Aim = PlayerAim(); return; }
            if (!primaryCharge.TryTakeShot(Simulation.CanFire(player), out var powerShot))
            {
                if(!primaryCharge.IsCharging && !primaryCharge.HasPendingShot) { shotAim.Reset(); command.Aim = PlayerAim(); }
                return;
            }
            command.Fire = true;
            command.PowerShot = powerShot;
            // Keep this command's release direction through simulation.Fire. Only
            // later commands may fall back to the tank's movement direction.
            shotAim.Reset();
        }
    }
}
