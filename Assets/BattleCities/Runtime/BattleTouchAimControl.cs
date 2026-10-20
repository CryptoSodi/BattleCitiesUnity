using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

namespace BattleCities
{
    // Shares the fire stick's pointer owner while sending its second input channel.
    public sealed class BattleTouchAimControl : OnScreenControl
    {
        [SerializeField] string path;
        protected override string controlPathInternal { get => path; set => path = value; }
        public void Configure() => controlPath = "<BattleTouchDevice>/aim";
        public void SetAim(Vector2 direction) => SendValueToControl(direction);
        protected override void OnDisable() { SetAim(Vector2.zero); base.OnDisable(); }
    }
}
