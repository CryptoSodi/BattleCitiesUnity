using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace BattleCities
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BattleTouchState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('B', 'C', 'T', 'H');
        [InputControl(name = "move", layout = "Stick")]
        public Vector2 move;
        [InputControl(name = "fire", layout = "Button", bit = 0)]
        [InputControl(name = "special", layout = "Button", bit = 1)]
        [InputControl(name = "switchSpecial", layout = "Button", bit = 2)]
        [InputControl(name = "slot1", layout = "Button", bit = 3)]
        [InputControl(name = "slot2", layout = "Button", bit = 4)]
        [InputControl(name = "slot3", layout = "Button", bit = 5)]
        [InputControl(name = "slot4", layout = "Button", bit = 6)]
        public uint buttons;
    }

    // A separate device keeps touch controls from becoming the current physical gamepad.
    [InputControlLayout(stateType = typeof(BattleTouchState), displayName = "Battle touch controls")]
    public sealed class BattleTouchDevice : InputDevice
    {
        public static void Register()
        {
            // Registering an existing layout rebuilds its live devices during scene changes.
            if (!InputSystem.ListLayouts().Contains(nameof(BattleTouchDevice)))
                InputSystem.RegisterLayout<BattleTouchDevice>();
        }
    }
}
