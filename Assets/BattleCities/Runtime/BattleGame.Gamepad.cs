using BattleCities.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    public static class BattleGamepadBindings
    {
        public const string Help = "D-PAD / LEFT STICK: DRIVE    RIGHT STICK: AIM\nA: FIRE / HOLD + RELEASE    B: DEPLOY SPECIAL    Y: SWITCH SPECIAL\nL1 / R1: SELECT POWER-UP    X: USE POWER-UP    START: PAUSE";

        public static void Configure(InputActionMap map)
        {
            string[] directions = { "up", "right", "down", "left" };
            for (int i = 0; i < 4; i++) map.FindAction("Drive" + i, true).AddBinding("<Gamepad>/dpad/" + directions[i]);
            map.AddAction("StickMove", InputActionType.Value, "<Gamepad>/leftStick");
            map.AddAction("StickAim", InputActionType.Value, "<Gamepad>/rightStick");
            // PSG1 uses Nintendo face labels: A=east, B=south, X=north, Y=west.
            map.FindAction("Fire", true).AddBinding("<Gamepad>/buttonEast");
            map.FindAction("SecondaryFire", true).AddBinding("<Gamepad>/buttonSouth");
            map.FindAction("SelectSecondary", true).AddBinding("<Gamepad>/buttonWest");
            map.FindAction("Pause", true).AddBinding("<Gamepad>/start");
            map.AddAction("PreviousPowerup", InputActionType.Button, "<Gamepad>/leftShoulder");
            map.AddAction("NextPowerup", InputActionType.Button, "<Gamepad>/rightShoulder");
            map.AddAction("UsePowerup", InputActionType.Button, "<Gamepad>/buttonNorth");
            map.AddAction("ControllerHelp", InputActionType.Button, "<Gamepad>/select");
        }

        public static Facing? Direction(Vector2 value)
        {
            if (value.sqrMagnitude < .35f * .35f) return null;
            return Mathf.Abs(value.x) > Mathf.Abs(value.y)
                ? value.x > 0 ? Facing.Right : Facing.Left
                : value.y > 0 ? Facing.Up : Facing.Down;
        }
    }

    public sealed partial class BattleGame
    {
        private InputAction stickMove, stickAim, previousPowerup, nextPowerup, usePowerup, controllerHelp;
        private int selectedPowerup, pauseChoice, gamepadBlockedThroughFrame = -1;
        private bool gamepadHelp;
        private int pauseStickDirection;
        public int SelectedPowerupSlot => selectedPowerup;
        private bool Psg1 => RuntimePlatformInfo.IsPsg1;
        private bool BlockCombat => showDebug || Time.frameCount <= gamepadBlockedThroughFrame;

        private void ConfigureGamepadBindings()
        {
            BattleGamepadBindings.Configure(input);
            stickMove = input.FindAction("StickMove"); stickAim = input.FindAction("StickAim");
            previousPowerup = input.FindAction("PreviousPowerup"); nextPowerup = input.FindAction("NextPowerup");
            usePowerup = input.FindAction("UsePowerup"); controllerHelp = input.FindAction("ControllerHelp");
        }

        private Facing? PlayerMove() => Latest(moveKeys, moveOrder) ?? BattleGamepadBindings.Direction(stickMove.ReadValue<Vector2>());
        private Facing? DirectionalAim() => Latest(aimKeys, aimOrder) ?? BattleGamepadBindings.Direction(stickAim.ReadValue<Vector2>());

        private void BlockControllerTransition()
        {
            CancelTouchGameplay();
            pauseStickDirection = 0;
            gamepadBlockedThroughFrame = Time.frameCount + 1;
        }

        private void UpdateGamepadControls()
        {
            bool lobby = TouchLobbyVisible;
            if (lobby) { BlockControllerTransition(); return; }
            if (Psg1 && showDebug)
            {
                if (secondaryFire.WasPressedThisFrame() || pause.WasPressedThisFrame()) showDebug = false;
                BlockControllerTransition(); return;
            }
            if (pause.WasPressedThisFrame())
            {
                paused = !paused; gamepadHelp = false; pauseChoice = 0; BlockControllerTransition();
            }
            if (controllerHelp.WasPressedThisFrame())
            {
                gamepadHelp = !gamepadHelp; paused = gamepadHelp; pauseChoice = 0; BlockControllerTransition();
            }
            bool ended = Simulation.Lost || Simulation.Won;
            if (Psg1 && (paused || ended))
            {
                if (Time.frameCount <= gamepadBlockedThroughFrame) return;
                var options = GamepadPauseOptions();
                float stickY = stickMove.ReadValue<Vector2>().y;
                int direction = stickY > .5f ? 1 : stickY < -.5f ? -1 : 0;
                bool up = moveKeys[0].WasPressedThisFrame() || (direction == 1 && pauseStickDirection != 1);
                bool down = moveKeys[2].WasPressedThisFrame() || (direction == -1 && pauseStickDirection != -1);
                pauseStickDirection = direction;
                if (up) pauseChoice = (pauseChoice + options.Length - 1) % options.Length;
                if (down) pauseChoice = (pauseChoice + 1) % options.Length;
                pauseChoice = Mathf.Clamp(pauseChoice, 0, options.Length - 1);
                if (fire.WasPressedThisFrame()) ActivatePauseChoice(options[pauseChoice]);
                else if (secondaryFire.WasPressedThisFrame())
                {
                    if (ended && !IsOnline) ActivatePauseChoice("MAIN MENU");
                    else { paused = false; gamepadHelp = false; BlockControllerTransition(); }
                }
                return;
            }
            if (paused || ended || BlockCombat) return;
            if (previousPowerup.WasPressedThisFrame()) selectedPowerup = (selectedPowerup + 3) % 4;
            if (nextPowerup.WasPressedThisFrame()) selectedPowerup = (selectedPowerup + 1) % 4;
            if (usePowerup.WasPressedThisFrame()) UsePowerupSlot(selectedPowerup);
        }

        private string[] GamepadPauseOptions()
        {
            if (IsOnline) return new[] { "RESUME", "ONLINE LOBBY" };
            if (Simulation.Won && Stage < 35) return new[] { "NEXT STAGE", "RESTART", "MAIN MENU" };
            if (Simulation.Won || Simulation.Lost) return new[] { "RESTART", "MAIN MENU" };
            return new[] { "RESUME", "RESTART", "ONLINE LOBBY", "DEBUG", "MAIN MENU" };
        }

        private void ActivatePauseChoice(string choice)
        {
            BlockControllerTransition(); gamepadHelp = false;
            switch (choice)
            {
                case "RESUME": paused = false; break;
                case "RESTART": LoadStage(Stage); break;
                case "NEXT STAGE": LoadStage(Stage + 1); break;
                case "ONLINE LOBBY": if (Multiplayer.BattleSession.Instance) Multiplayer.BattleSession.Instance.Lobby.Show(); break;
                case "DEBUG": showDebug = true; paused = true; break;
                case "MAIN MENU": SceneManager.LoadScene("MainMenu"); break;
            }
        }

        private void GamepadDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad) || (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed && change != InputDeviceChange.Disabled)) return;
            BlockControllerTransition();
            if (!IsOnline) paused = true;
        }

        private void DrawPsg1Controls()
        {
            if (!Psg1 || TouchLobbyVisible || showDebug) return;
            bool ended = Simulation.Lost || Simulation.Won;
            var centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(18, Mathf.RoundToInt(Screen.width / 60f)), wordWrap = true };
            if (!paused && !ended)
            {
                DrawControllerPanel(new Rect(8, Screen.height - 66, Screen.width - 16, 58));
                GUI.Label(new Rect(16, Screen.height - 64, Screen.width - 32, 54), "A FIRE / CHARGE    B DEPLOY    Y SWITCH\nL1 / R1 SLOT    X USE    START PAUSE    SELECT HELP", centered);
                return;
            }
            var options = GamepadPauseOptions();
            float height = 200 + options.Length * 60;
            var area = new Rect(Screen.width * .12f, (Screen.height - height) * .5f, Screen.width * .76f, height);
            DrawControllerPanel(area);
            GUILayout.BeginArea(new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24));
            var title = new GUIStyle(centered) { fontSize = 30, fontStyle = FontStyle.Bold };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            GUILayout.Label(ended ? Simulation.Won ? "STAGE CLEAR" : "GAME OVER" : "PAUSED", title);
            GUILayout.Label(BattleGamepadBindings.Help, centered);
            GUILayout.Space(12);
            for (int i = 0; i < options.Length; i++)
            {
                var prior = GUI.backgroundColor; if (i == pauseChoice) GUI.backgroundColor = new Color(1f, .75f, .1f);
                if (GUILayout.Button((i == pauseChoice ? "> " : "") + options[i], button, GUILayout.Height(56))) ActivatePauseChoice(options[i]);
                GUI.backgroundColor = prior;
            }
            GUILayout.Label("D-PAD: CHOOSE    A: SELECT    B: BACK", centered);
            GUILayout.EndArea();
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
