using System;
using System.Linq;
using BattleCities.Core;
using BattleCities.Multiplayer;
using PlaySolanaSdk;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Android.LowLevel;

namespace BattleCities.Editor
{
    public static class Psg1GameplayChecks
    {
        public static string LastBattleResult { get; private set; } = "Not run";

        [MenuItem("Battle Cities/Validate PSG1 Controller Bindings")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run PSG1 input checks in Play mode.");
            RunBindings();
        }

        public static void RunBindings()
        {
            var pad = InputSystem.AddDevice<PSG1>();
            var map = new InputActionMap("PSG1 verification");
            int checks = 0;
            void Check(bool valid, string message)
            { if (!valid) throw new InvalidOperationException("PSG1 FAIL: " + message); checks++; }
            void State(PSG1StateController state) { InputSystem.QueueStateEvent(pad, state); InputSystem.Update(); }
            try
            {
                for (int i = 0; i < 4; i++) map.AddAction("Drive" + i, InputActionType.Button);
                foreach (var name in new[] { "Fire", "SecondaryFire", "SelectSecondary" }) map.AddAction(name, InputActionType.Button);
                for (int i = 1; i <= 4; i++) map.AddAction("Powerup" + i, InputActionType.Button);
                BattleGamepadBindings.Configure(map); map.Enable();
                var buttons = new[] { AndroidKeyCode.ButtonB, AndroidKeyCode.ButtonA, AndroidKeyCode.ButtonX, AndroidKeyCode.ButtonY, AndroidKeyCode.ButtonL1, AndroidKeyCode.ButtonR1 };
                var actions = new[] { "Powerup1", "Powerup2", "Powerup4", "Powerup3", "GamepadSpecial", "Fire" };
                for (int i = 0; i < buttons.Length; i++)
                {
                    State(new PSG1StateController().WithButton(buttons[i]));
                    Check(map[actions[i]].WasPressedThisFrame(), actions[i] + " uses the physical PSG1 button");
                    Check(actions.Count(a => map[a].IsPressed()) == 1, "face/shoulder action is exclusive");
                    Check(!map["SecondaryFire"].IsPressed() && !map["SelectSecondary"].IsPressed(), "face/shoulder does not bypass special tap/hold");
                    State(new PSG1StateController());
                    Check(!map[actions[i]].IsPressed(), "release clears " + actions[i]);
                }
                foreach (var removed in new[] { AndroidKeyCode.ButtonStart, AndroidKeyCode.ButtonSelect })
                {
                    State(new PSG1StateController().WithButton(removed));
                    Check(!actions.Any(a => map[a].IsPressed()), "Start/Select do not pause or trigger combat");
                    State(new PSG1StateController());
                }
                for (int i = 0; i < 4; i++)
                {
                    State(new PSG1StateController().WithAxis(i % 2 == 0 ? AndroidAxis.HatY : AndroidAxis.HatX, i == 0 || i == 3 ? -1 : 1));
                    Check(map["Drive" + i].IsPressed(), "D-pad direction " + i);
                }
                State(new PSG1StateController().WithAxis(AndroidAxis.X, 1).WithAxis(AndroidAxis.Rz, -1));
                Check(BattleGamepadBindings.Direction(map["StickMove"].ReadValue<Vector2>()) == Facing.Right, "left stick drives");
                Check(BattleGamepadBindings.Direction(map["StickAim"].ReadValue<Vector2>()) == Facing.Up, "right stick aims with Android Y inversion");
                State(new PSG1StateController().WithAxis(AndroidAxis.X, .05f));
                Check(BattleGamepadBindings.Direction(map["StickMove"].ReadValue<Vector2>()) == null, "stick drift is ignored");
                Check(!map.bindings.Any(b => b.path.Contains("Trigger")), "PSG1 needs no L2/R2");
                foreach (bool battle in new[] { false, true })
                {
                    Check(MobileBattleOrientation.OrientationFor(GameRuntimePlatform.Psg1, battle, ScreenOrientation.LandscapeLeft) == ScreenOrientation.Portrait, "PSG1 native orientation in menu/match");
                    Check(MobileBattleOrientation.OrientationFor(GameRuntimePlatform.Android, battle, ScreenOrientation.Portrait) == ScreenOrientation.LandscapeLeft, "Android orientation stays landscape in menu/match");
                }
                Debug.Log("PSG1 INPUT PASS: " + checks + " checks using the installed PSG1 SDK device.");
            }
            finally { map.Dispose(); InputSystem.RemoveDevice(pad); }
        }

        // Queue hardware-format events through the live game, including UI context transitions.
        public static void RunBattle()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            if (!Application.isPlaying || !game || game.IsOnline || !RuntimePlatformInfo.IsPsg1 || game.Simulation.Player == null)
                throw new InvalidOperationException("Open an offline PSG1 match before running the live controller check.");
            var pad = InputSystem.AddDevice<PSG1>();
            var sim = game.Simulation; var player = sim.Player;
            Vector2 start = new Vector2(player.X, player.Y);
            int shots = 0, mines = sim.Mines.Count; bool power = false;
            Action<ShotState> fired = shot => { if (shot.Owner == player.Id) { shots++; power = shot.PowerShot; } };
            sim.ShotFired += fired; game.Paused = false; sim.EquippedSecondary = SecondaryAttack.Mine;
            int step = 0; double next = EditorApplication.timeSinceStartup + .3;
            LastBattleResult = "Running";
            EditorApplication.CallbackFunction tick = null;
            void State(PSG1StateController state = default) => InputSystem.QueueStateEvent(pad, state);
            void Press(AndroidKeyCode button) => State(new PSG1StateController().WithButton(button));
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
            void Finish(string message)
            {
                EditorApplication.update -= tick; sim.ShotFired -= fired;
                if (pad.added) InputSystem.RemoveDevice(pad);
                LastBattleResult = message; Debug.Log(message);
            }
            tick = () =>
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .25;
                try
                {
                    Check(game && Application.isPlaying, "Match closed during verification");
                    switch (step++)
                    {
                        case 0: State(new PSG1StateController().WithAxis(AndroidAxis.HatY, -1).WithButton(AndroidKeyCode.ButtonR1)); next += 1.4; break;
                        case 1: Check(Vector2.Distance(start, new Vector2(player.X, player.Y)) > 1 && shots == 0, "D-pad movement while charging"); State(); break;
                        case 2: Check(shots == 1 && power, "R1 release fires exactly one charged shot"); State(new PSG1StateController().WithAxis(AndroidAxis.X, 1).WithAxis(AndroidAxis.Z, 1)); break;
                        case 3: Check(player.Direction == Facing.Right && player.Aim == Facing.Right, "sticks drive and aim"); State(); break;
                        case 4: Press(AndroidKeyCode.ButtonL1); break;
                        case 5: Check(sim.Mines.Count == mines, "L1 press waits for release"); State(); break;
                        case 6: Check(sim.Mines.Count == mines + 1, "L1 tap deploys a mine"); Press(AndroidKeyCode.ButtonX); break;
                        case 7: Check(game.SelectedPowerupSlot == 3, "Y directly targets fourth slot"); State(); break;
                        case 8: Press(AndroidKeyCode.ButtonA); break;
                        case 9: Check(game.SelectedPowerupSlot == 1, "B directly targets second slot"); State(); break;
                        case 10: State(); break;
                        case 11: Press(AndroidKeyCode.ButtonStart); break;
                        case 12: Check(!game.Paused && shots == 1, "Start does not pause or fire"); State(); break;
                        case 13: Press(AndroidKeyCode.ButtonSelect); break;
                        case 14: Check(!game.Paused && shots == 1, "Select does not open a pause/help menu"); State(); BattleSession.Instance.Lobby.Show(); break;
                        case 15: Press(AndroidKeyCode.ButtonB); break;
                        case 16: Check(!BattleSession.Instance.Lobby.Visible && !game.Paused, "lobby A selects Back to Game"); State(); break;
                        case 17: Check(shots == 1, "lobby submit cannot fire"); Press(AndroidKeyCode.ButtonR1); break;
                        case 18: InputSystem.RemoveDevice(pad); break;
                        case 19: Check(!game.Paused && shots == 1, "disconnect clears firing without entering a pause menu"); Finish("PASS: PSG1 SDK events drove/aimed, charged/fired on R1, deployed on L1 release, directly targeted power-ups and handled disconnect without leaked attacks."); break;
                    }
                }
                catch (Exception error) { Finish("FAIL at step " + (step - 1) + ": " + error.Message); }
            };
            EditorApplication.update += tick;
        }
    }
}
