using System;

namespace BattleCities.Core
{
    // Render-frame input is separate from the fixed-step combat simulation.
    public sealed class ChargedFireInput
    {
        public const float ChargeDuration = 1f;
        public float ChargeSeconds { get; private set; }
        public bool IsCharging { get; private set; }
        public float Progress => Math.Min(1, ChargeSeconds / ChargeDuration);
        public bool Ready => IsCharging && ChargeSeconds >= ChargeDuration;
        private bool? pendingShot;

        // Keep only the released shot until the next simulation step. Normal taps
        // during reload are discarded instead of becoming an automatic-fire queue.
        public void Sample(bool pressed, bool released, bool held, float dt, bool canFire)
        {
            if (pressed) pendingShot = null;
            var shot = Tick(pressed, released, held, dt);
            if (shot.HasValue && (shot.Value || canFire)) pendingShot = shot;
        }

        public bool TryTakeShot(bool canFire, out bool powerShot)
        {
            powerShot = false;
            if (IsCharging || !pendingShot.HasValue) return false;
            if (!canFire)
            {
                if (!pendingShot.Value) pendingShot = null;
                return false;
            }
            powerShot = pendingShot.Value;
            pendingShot = null;
            return true;
        }

        // null = no shot, false = quick shot, true = charged shot.
        public bool? Tick(bool pressed, bool released, bool held, float dt)
        {
            if (pressed) { IsCharging = true; ChargeSeconds = 0; }
            if (!IsCharging) return null;
            if (!pressed) ChargeSeconds = Math.Min(ChargeDuration, ChargeSeconds + Math.Max(0, dt));
            if (released && !held)
            {
                bool power = Ready;
                Reset();
                return power;
            }
            // Losing the button without a release event cancels, rather than firing.
            if (!held) Reset();
            return null;
        }

        public void Reset() { IsCharging = false; ChargeSeconds = 0; pendingShot = null; }
    }
}
