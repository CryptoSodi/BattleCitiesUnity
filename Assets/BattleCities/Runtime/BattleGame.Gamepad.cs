using BattleCities.Core;
using UnityEngine;
using BattleCities.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    public static class BattleGamepadBindings
    {
        public const string Help = "D-PAD / LEFT STICK: DRIVE    RIGHT STICK: AIM\nR1: FIRE / HOLD + RELEASE    L1: TAP TO DEPLOY SPECIAL\nA / B / X / Y: USE POWER-UP 1 / 2 / 3 / 4";
        public static string HelpFor(bool canSwap) => Help + (canSwap ? "\nHOLD L1: SWITCH SPECIAL" : "");

        public static void Configure(InputActionMap map)
        {
            string[] directions = { "up", "right", "down", "left" };
            for (int i = 0; i < 4; i++) map.FindAction("Drive" + i, true).AddBinding("<Gamepad>/dpad/" + directions[i]);
            map.AddAction("StickMove", InputActionType.Value, "<Gamepad>/leftStick");
            map.AddAction("StickAim", InputActionType.Value, "<Gamepad>/rightStick");
            // PSG1 uses Nintendo face labels: A=east, B=south, X=north, Y=west.
            map.FindAction("Fire", true).AddBinding("<Gamepad>/rightShoulder");
            map.AddAction("GamepadSpecial", InputActionType.Button, "<Gamepad>/leftShoulder");
            string[] buttons = { "buttonEast", "buttonSouth", "buttonNorth", "buttonWest" };
            for (int i = 0; i < buttons.Length; i++)
                map.FindAction("Powerup" + (i + 1), true).AddBinding("<Gamepad>/" + buttons[i]);
        }

        public static Facing? Direction(Vector2 value)
        {
            if (value.sqrMagnitude < .35f * .35f) return null;
            return Mathf.Abs(value.x) > Mathf.Abs(value.y)
                ? value.x > 0 ? Facing.Right : Facing.Left
                : value.y > 0 ? Facing.Up : Facing.Down;
        }
    }

    // A tap deploys on release; a hold switches once and must never also deploy.
    public struct GamepadSpecialGesture
    {
        bool tracking, switched;
        float elapsed;
        public void Reset() { tracking = switched = false; elapsed = 0; }
        public int Sample(bool pressed, bool held, bool released, float dt, bool canSwap)
        {
            if (pressed) { tracking = true; switched = false; elapsed = 0; }
            if (!tracking) return 0;
            if (held) elapsed += Mathf.Max(0, dt);
            if (!switched && canSwap && elapsed >= 1f)
            { switched = true; return 2; }
            if (!released) return 0;
            bool deploy = !switched;
            Reset();
            return deploy ? 1 : 0;
        }
    }

    public sealed partial class BattleGame
    {
        private InputAction stickMove, stickAim, gamepadSpecial;
        private GamepadSpecialGesture specialGesture;
        private int selectedPowerup, gamepadBlockedThroughFrame = -1;
        public int SelectedPowerupSlot => selectedPowerup;
        private bool Psg1 => RuntimePlatformInfo.IsPsg1;
        private bool BlockCombat => IsReplaying || showDebug || Time.frameCount <= gamepadBlockedThroughFrame;

        private void ConfigureGamepadBindings()
        {
            BattleGamepadBindings.Configure(input);
            stickMove = input.FindAction("StickMove"); stickAim = input.FindAction("StickAim");
            gamepadSpecial = input.FindAction("GamepadSpecial");
        }

        private Facing? PlayerMove() => Latest(moveKeys, moveOrder) ?? BattleGamepadBindings.Direction(stickMove.ReadValue<Vector2>());
        private Facing? DirectionalAim() => Latest(aimKeys, aimOrder) ?? BattleGamepadBindings.Direction(stickAim.ReadValue<Vector2>()) ?? (touchControls ? touchControls.TurretAim : null);

        private void BlockControllerTransition()
        {
            CancelTouchGameplay();
            gamepadBlockedThroughFrame = Time.frameCount + 1;
        }

        private void UpdateGamepadControls()
        {
            if(IsReplaying)return;
            bool lobby = TouchLobbyVisible;
            if (lobby) { BlockControllerTransition(); return; }
            if (Psg1 && showDebug)
            {
                if (slots[1].WasPressedThisFrame()) showDebug = false;
                BlockControllerTransition(); return;
            }
            bool ended = Simulation.Lost || Simulation.Won;
            if (paused || ended || BlockCombat || consumePending || !ReplayReady || !Simulation.CanAcceptPlayerFire)
            { specialGesture.Reset(); return; }
            int gesture = specialGesture.Sample(gamepadSpecial.WasPressedThisFrame(), gamepadSpecial.IsPressed(),
                gamepadSpecial.WasReleasedThisFrame(), Time.unscaledDeltaTime, CanSwapSpecials);
            if (gesture == 1) secondaryQueued = true;
            else if (gesture == 2) CycleSecondary();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].WasPressedThisFrame()) selectedPowerup = i;
        }

        private void GamepadDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad) || (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed && change != InputDeviceChange.Disabled)) return;
            BlockControllerTransition();
        }

        private void DrawPsg1Controls()
        {
            if (!Psg1 || TouchLobbyVisible || showDebug) return;
            if (paused || Simulation.Lost || Simulation.Won) return;
            var centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(18, Mathf.RoundToInt(Screen.width / 60f)), wordWrap = true };
            DrawControllerPanel(new Rect(8, Screen.height - 66, Screen.width - 16, 58));
            GUI.Label(new Rect(16, Screen.height - 64, Screen.width - 32, 54),
                "R1 FIRE / CHARGE    L1 TAP DEPLOY" + (CanSwapSpecials ? " / HOLD SWITCH" : "") + "\nA / B / X / Y USE POWER-UP 1 / 2 / 3 / 4", centered);
        }

        private static void DrawControllerPanel(Rect rect)
        {
            var prior = GUI.color;
            GUI.color = new Color(.02f, .06f, .1f, .97f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prior;
        }
    }
}
