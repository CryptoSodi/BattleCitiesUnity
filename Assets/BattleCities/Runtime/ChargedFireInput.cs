using System;

namespace BattleCities.Core
{
    // Render-frame input is separate from the fixed-step combat simulation.
    public sealed class ChargedFireInput
    {
        public const float ChargeDuration = .65f;
        public float ChargeSeconds { get; private set; }
        public bool IsCharging { get; private set; }
        public float Progress => Math.Min(1, ChargeSeconds / ChargeDuration);
        public bool Ready => IsCharging && ChargeSeconds >= ChargeDuration;

        // null = no shot, false = quick shot, true = charged shot.
        public bool? Tick(bool pressed, bool released, bool held, float dt)
        {
            if (pressed) { IsCharging = true; ChargeSeconds = 0; }
            if (!IsCharging) return null;
            if (!pressed) ChargeSeconds = Math.Min(ChargeDuration, ChargeSeconds + Math.Max(0, dt));
            if (released)
            {
                bool power = Ready;
                Reset();
                return power;
            }
            // Losing the button without a release event cancels, rather than firing.
            if (!held) Reset();
            return null;
        }

        public void Reset() { IsCharging = false; ChargeSeconds = 0; }
    }
}
