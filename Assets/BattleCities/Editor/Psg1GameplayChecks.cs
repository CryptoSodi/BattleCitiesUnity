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
            var pad = InputSystem.AddDevice<PSG1>();
            var map = new InputActionMap("PSG1 verification");
            int checks = 0;
            void Check(bool valid, string message)
            { if (!valid) throw new InvalidOperationException("PSG1 FAIL: " + message); checks++; }
            void State(PSG1StateController state) { InputSystem.QueueStateEvent(pad, state); InputSystem.Update(); }
            try
            {
                for (int i = 0; i < 4; i++) map.AddAction("Drive" + i, InputActionType.Button);
                foreach (var name in new[] { "Fire", "SecondaryFire", "SelectSecondary", "Pause" }) map.AddAction(name, InputActionType.Button);
                BattleGamepadBindings.Configure(map); map.Enable();
                var buttons = new[] { AndroidKeyCode.ButtonB, AndroidKeyCode.ButtonA, AndroidKeyCode.ButtonX, AndroidKeyCode.ButtonY, AndroidKeyCode.ButtonL1, AndroidKeyCode.ButtonR1, AndroidKeyCode.ButtonStart, AndroidKeyCode.ButtonSelect };
                var actions = new[] { "Fire", "SecondaryFire", "SelectSecondary", "UsePowerup", "PreviousPowerup", "NextPowerup", "Pause", "ControllerHelp" };
                for (int i = 0; i < buttons.Length; i++)
                {
                    State(new PSG1StateController().WithButton(buttons[i]));
                    Check(map[actions[i]].WasPressedThisFrame(), actions[i] + " uses the physical PSG1 button");
                    Check(actions.Count(a => map[a].IsPressed()) == 1, "face/shoulder action is exclusive");
                    State(new PSG1StateController());
                    Check(!map[actions[i]].IsPressed(), "release clears " + actions[i]);
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
                        case 0: State(new PSG1StateController().WithAxis(AndroidAxis.HatY, -1).WithButton(AndroidKeyCode.ButtonB)); next += 1.4; break;
                        case 1: Check(Vector2.Distance(start, new Vector2(player.X, player.Y)) > 1 && shots == 0, "D-pad movement while charging"); State(); break;
                        case 2: Check(shots == 1 && power, "A release fires exactly one charged shot"); State(new PSG1StateController().WithAxis(AndroidAxis.X, 1).WithAxis(AndroidAxis.Z, 1)); break;
                        case 3: Check(player.Direction == Facing.Right && player.Aim == Facing.Right, "sticks drive and aim"); State(); break;
                        case 4: Press(AndroidKeyCode.ButtonA); break;
                        case 5: Check(sim.Mines.Count == mines + 1, "B deploys a mine"); State(); break;
                        case 6: Press(AndroidKeyCode.ButtonX); break;
                        case 7: Check(sim.EquippedSecondary == SecondaryAttack.PatrolDrone, "Y switches special"); Press(AndroidKeyCode.ButtonR1); break;
                        case 8: Check(game.SelectedPowerupSlot == 1, "R1 selects next power-up"); State(); break;
                        case 9: Press(AndroidKeyCode.ButtonL1); break;
                        case 10: Check(game.SelectedPowerupSlot == 0, "L1 selects previous power-up"); State(); break;
                        case 11: Press(AndroidKeyCode.ButtonB); break;
                        case 12: State(new PSG1StateController().WithButton(AndroidKeyCode.ButtonB).WithButton(AndroidKeyCode.ButtonStart)); break;
                        case 13: Check(game.Paused, "Start pauses"); State(); break;
                        case 14: Check(shots == 1, "pause cancels charged fire"); Press(AndroidKeyCode.ButtonA); break;
                        case 15: Check(!game.Paused && sim.Drones.Count == 0, "B resumes without deploying"); State(); break;
                        case 16: Press(AndroidKeyCode.ButtonStart); break;
                        case 17: State(); break;
                        case 18: Press(AndroidKeyCode.ButtonB); next += 1.2; break;
                        case 19: State(); break;
                        case 20: Check(!game.Paused && shots == 1, "A resume does not leak a held/released shot"); BattleSession.Instance.Lobby.Show(); break;
                        case 21: Press(AndroidKeyCode.ButtonB); break;
                        case 22: Check(!BattleSession.Instance.Lobby.Visible && !game.Paused, "lobby A selects Back to Game"); State(); break;
                        case 23: Check(shots == 1, "lobby submit cannot fire"); BattleSession.Instance.Lobby.Show(); break;
                        case 24: Press(AndroidKeyCode.ButtonA); break;
                        case 25: Check(!BattleSession.Instance.Lobby.Visible && !game.Paused && sim.Drones.Count == 0, "lobby B cancels without deploying"); State(); break;
                        case 26: Press(AndroidKeyCode.ButtonSelect); break;
                        case 27: Check(game.Paused, "Select opens help"); State(); break;
                        case 28: Press(AndroidKeyCode.ButtonSelect); break;
                        case 29: Check(!game.Paused, "Select closes help"); State(); break;
                        case 30: Press(AndroidKeyCode.ButtonB); break;
                        case 31: InputSystem.RemoveDevice(pad); break;
                        case 32: Check(game.Paused && shots == 1, "disconnect pauses and cancels firing"); Finish("PASS: PSG1 SDK events drove/aimed, charged/fired, deployed/switched special, selected power-ups, navigated pause/help/lobby, and handled disconnect without leaked attacks."); break;
                    }
                }
                catch (Exception error) { Finish("FAIL at step " + (step - 1) + ": " + error.Message); }
            };
            EditorApplication.update += tick;
        }
    }
}
