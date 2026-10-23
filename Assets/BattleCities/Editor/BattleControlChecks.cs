using System;
using System.Linq;
using System.Reflection;
using BattleCities.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BattleCities.Editor
{
    public static class BattleControlChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            int checks = 0;
            void Check(bool valid, string message) { if (!valid) throw new Exception("BATTLE CONTROLS: " + message); checks++; }
            var root = new GameObject("Isolated gameplay controls"); root.SetActive(false);
            InputActionMap actions = null;
            Keyboard keyboard = null;
            const string playerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
            var settings = InputSystem.settings;
            bool originalPlayerUpdates = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", Private).Invoke(settings, new object[] { playerUpdates });
            try
            {
                Check(RuntimePlatformInfo.UsesTouchGameplayFor(GameRuntimePlatform.WebGl,true),"mobile browser gets Android touch controls");
                Check(!RuntimePlatformInfo.UsesTouchGameplayFor(GameRuntimePlatform.WebGl,false),"desktop browser keeps keyboard controls");
                Check(RuntimePlatformInfo.UsesTouchGameplayFor(GameRuntimePlatform.Android,false),"native Android retains touch controls");
                Check(!RuntimePlatformInfo.UsesTouchGameplayFor(GameRuntimePlatform.Psg1,true),"PSG1 retains physical controls");
                Check(RuntimePlatformInfo.PowerupUiFor(GameRuntimePlatform.WebGl,true)==PowerupUiMode.ExternalController,"mobile browser has tappable HUD power-ups");
                Check(RuntimePlatformInfo.PowerupUiFor(GameRuntimePlatform.WebGl,false)==PowerupUiMode.WebTopLeft,"desktop browser keeps its HUD layout");
                var game = root.AddComponent<BattleGame>();
                typeof(BattleGame).GetMethod("ConfigureGameplayInput", Private).Invoke(game, null);
                actions = (InputActionMap)typeof(BattleGame).GetField("input", Private).GetValue(game);
                foreach (string path in new[] { "<Keyboard>/p", "<Keyboard>/r", "<Keyboard>/c", "<Gamepad>/start", "<Gamepad>/select" })
                    Check(!actions.bindings.Any(b => b.path == path), "removed shortcut " + path);
                foreach (string name in new[] { "Fire", "SecondaryFire", "SelectSecondary", "Drive0", "Turret0", "Powerup1" })
                    Check(actions.FindAction(name) != null, "retained combat action " + name);
                keyboard = InputSystem.AddDevice<Keyboard>();
                settings.SetInternalFeatureFlag(playerUpdates, true);
                actions.Enable();
                for (int slot = 1; slot <= 4; slot++)
                {
                    var key = (Key)((int)Key.Digit1 + slot - 1);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    InputSystem.Update();
                    for (int action = 1; action <= 4; action++)
                        Check(actions.FindAction("Powerup" + action).WasPressedThisFrame() == (slot == action),
                            "physical number key " + slot + " activates only slot " + slot);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                }
                actions.Disable();
                Check(!BattleGamepadBindings.HelpFor(false).Contains("SWITCH") && !BattleGamepadBindings.Help.Contains("PAUSE"), "single-special help omits unavailable controls");
                Check(BattleGamepadBindings.HelpFor(true).Contains("HOLD L1: SWITCH SPECIAL"), "future multi-special help advertises switching");
                var gesture = new GamepadSpecialGesture();
                Check(gesture.Sample(true, true, false, .1f, true) == 0, "L1 press defers deployment");
                Check(gesture.Sample(false, false, true, 0, true) == 1, "L1 tap deploys on release");
                Check(gesture.Sample(true, true, false, .6f, true) == 0, "L1 short hold does not switch");
                Check(gesture.Sample(false, true, false, .4f, true) == 2, "L1 switches at one second");
                Check(gesture.Sample(false, true, false, 2, true) == 0, "L1 continued hold does not repeat switch");
                Check(gesture.Sample(false, false, true, 0, true) == 0, "L1 switch release never deploys");
                Check(gesture.Sample(true, true, false, 2, false) == 0, "single-special tank cannot switch on hold");
                Check(gesture.Sample(false, false, true, 0, false) == 1, "single-special hold still deploys on release");
                gesture.Sample(true, true, false, .4f, true); gesture.Reset();
                Check(gesture.Sample(false, false, true, 0, true) == 0, "cancelled L1 cannot leak deployment");
                Psg1GameplayChecks.RunBindings();
                var map = JsonConvert.DeserializeObject<MapData>(Resources.Load<TextAsset>("Maps/01").text);
                var expected = new[] { SecondaryAttack.Mine, SecondaryAttack.PatrolDrone, SecondaryAttack.GroundTurret, SecondaryAttack.LandDrone };
                var simulationSetter = typeof(BattleGame).GetProperty("Simulation").GetSetMethod(true);
                for (int tier = 0; tier < 4; tier++)
                {
                    var sim = new BattleSimulation(map, playerTier: tier) { Freeze = 999, DisableEnemyFire = true };
                    for(int i=0;i<122;i++) sim.Step(default);
                    simulationSetter.Invoke(game, new object[] { sim });
                    var initial = (SecondaryAttack)typeof(BattleGame).GetProperty("InitialSpecial", Private).GetValue(game);
                    Check(initial == expected[tier], "tank-specific initial special " + tier);
                    sim.EquippedSecondary = initial;
                    Check(!game.CanSwapSpecials, "single-special tank hides Swap " + tier);
                    typeof(BattleGame).GetMethod("CycleSecondary", Private).Invoke(game, null);
                    Check(sim.EquippedSecondary == initial, "single-special tank cannot switch " + tier);
                    // Replay runs already carry the selected secondary; no new replay schema needed.
                    var recorded = new BattleSimulation(map, playerTier: tier);
                    var recorder = new ReplayRecorder(recorded, map, "fixed-special-check");
                    simulationSetter.Invoke(game, new object[] { recorded });
                    var recordingField=typeof(BattleGame).GetField("replayRecorder", Private);
                    recordingField.SetValue(game, recorder);
                    typeof(BattleGame).GetMethod("StepRecorded", Private).Invoke(game, new object[] { default(Command), null });
                    recordingField.SetValue(game, null);
                    Check(recorded.EquippedSecondary == expected[tier], "first live tick equips the tank special " + tier);
                    recorder.Finish("aborted");
                    var replay = new ReplayPlayer(recorder.Data);
                    while(!replay.Complete && replay.Error == null) replay.Step();
                    Check(replay.Complete && replay.Error == null, "selected special survives recording/playback " + tier + ": " + replay.Error);
                    simulationSetter.Invoke(game, new object[] { sim });
                }
                var controls = root.AddComponent<BattleTouchControls>(); controls.Initialize(game);
                var touch = root.GetComponentsInChildren<BattleTouchControl>(true);
                var fire = (RectTransform)touch.Single(c => c.name == "Fire").transform;
                var move = (RectTransform)touch.Single(c => c.name == "Movement").transform;
                var specials = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b=>b.name.StartsWith("Special ")).ToArray();
                Check(fire.GetComponent<CanvasGroup>().alpha==0&&move.GetComponent<CanvasGroup>().alpha==0,"floating sticks stay hidden before touch");
                Check(root.GetComponentsInChildren<BattleTouchZone>(true).Length==2,"left and right touch zones are present");
                Check(specials.Count(b=>b.gameObject.activeSelf)==1, "single-special tank shows one icon");
                Check(!touch.Any(c=>c.name=="Switch Special"), "Android has no Swap text button");
                Check(!root.GetComponentsInChildren<RectTransform>(true).Any(r => new[] { "Pause", "Resume", "Restart", "Match State" }.Contains(r.name)), "no touch pause/restart menu");
                var bunker = typeof(BattleGame).GetField("bunkerSpecials", Private);
                bunker.SetValue(game, new[] { SecondaryAttack.LandDrone, SecondaryAttack.GroundTurret });
                Check(game.CanSwapSpecials, "future multi-special tank enables swapping");
                typeof(BattleTouchControls).GetMethod("Refresh", Private).Invoke(controls, null);
                Check(specials.Count(b=>b.gameObject.activeSelf)==2, "multi-special tank shows separate ability buttons");
                typeof(BattleGame).GetMethod("CycleSecondary", Private).Invoke(game, null);
                Check(game.Simulation.EquippedSecondary == SecondaryAttack.GroundTurret, "switch chooses another owned special");
                typeof(BattleGame).GetMethod("CycleSecondary", Private).Invoke(game, null);
                Check(game.Simulation.EquippedSecondary == SecondaryAttack.LandDrone, "switch wraps through owned abilities only");
                Check(!BattleSpecialAbilities.CanSwap(new[] { SecondaryAttack.Mine, SecondaryAttack.None, SecondaryAttack.Mine }), "duplicates/empty slots do not unlock Swap");
                Check(!BattleSpecialAbilities.CanSwap(null) && BattleSpecialAbilities.First(null) == SecondaryAttack.None, "empty ability list is safe");
                typeof(BattleGame).GetMethod("OnApplicationFocus", Private).Invoke(game, new object[] { false });
                typeof(BattleGame).GetMethod("OnApplicationPause", Private).Invoke(game, new object[] { true });
                Check(!game.Paused, "focus/suspend clears input without leaving an unresumable pause");
                Debug.Log("BATTLE CONTROL CHECKS PASSED: " + checks);
            }
            finally { actions?.Dispose(); if(keyboard != null) InputSystem.RemoveDevice(keyboard); settings.SetInternalFeatureFlag(playerUpdates, originalPlayerUpdates); UnityEngine.Object.DestroyImmediate(root); }
            MobileGameplayChecks.RunBatch();
        }
    }
}
