using System;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BattleCities.Editor
{
    public static class MobileGameplayChecks
    {
        public static string LastBattleResult { get; private set; } = "Not run";

        // Feed real Touchscreen events through the EventSystem and into the live simulation.
        public static void RunBattle()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            if (!Application.isPlaying || !game || game.IsOnline || game.Simulation.Player == null)
                throw new InvalidOperationException("Open an offline Android match before running the live touch check.");
            var buttons = game.GetComponentsInChildren<BattleTouchControl>();
            var move = buttons.Single(c => c.name == "Movement");
            var fire = buttons.Single(c => c.name == "Fire");
            var special = buttons.Single(c => c.name == "Special");
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            var sim = game.Simulation;
            var player = sim.Player;
            Vector2 start = new Vector2(player.X, player.Y);
            int shots = 0, mines = sim.Mines.Count;
            bool power = false;
            Action<ShotState> fired = shot => { if (shot.Owner == player.Id) { shots++; power = shot.PowerShot; } };
            sim.ShotFired += fired;
            game.Paused = false;
            sim.EquippedSecondary = SecondaryAttack.Mine;
            LastBattleResult = "Running";
            int step = 0;
            double next = EditorApplication.timeSinceStartup + .2;
            EditorApplication.CallbackFunction tick = null;
            void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, BattleTouchControl control, Vector2 offset)
            {
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, control.transform.position) + offset;
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = position });
            }
            void Finish(string message)
            {
                EditorApplication.update -= tick;
                sim.ShotFired -= fired;
                foreach (var control in buttons) if (control) control.CancelInput();
                InputSystem.RemoveDevice(touchscreen);
                LastBattleResult = message;
            }
            tick = () =>
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + .25;
                try
                {
                    if (!game) throw new InvalidOperationException("Match closed during verification");
                    switch (step++)
                    {
                        case 0:
                            Touch(101, UnityEngine.InputSystem.TouchPhase.Began, move, Vector2.up * 90);
                            Touch(202, UnityEngine.InputSystem.TouchPhase.Began, fire, Vector2.zero);
                            next = EditorApplication.timeSinceStartup + 1.4;
                            break;
                        case 1:
                            if (Vector2.Distance(start, new Vector2(player.X, player.Y)) < 1 || !fire.Held || shots != 0)
                                throw new InvalidOperationException("Movement/fire check: distance=" + Vector2.Distance(start, new Vector2(player.X, player.Y)) + ", fireHeld=" + fire.Held + ", shots=" + shots + ", touchEnabled=" + touchscreen.enabled + ", paused=" + game.Paused);
                            Touch(101, UnityEngine.InputSystem.TouchPhase.Ended, move, Vector2.up * 90);
                            Touch(202, UnityEngine.InputSystem.TouchPhase.Ended, fire, Vector2.zero);
                            break;
                        case 2:
                            if (shots != 1 || !power) throw new InvalidOperationException("Charged release did not fire exactly one power shot");
                            Touch(303, UnityEngine.InputSystem.TouchPhase.Began, special, Vector2.zero);
                            break;
                        case 3:
                            Touch(303, UnityEngine.InputSystem.TouchPhase.Ended, special, Vector2.zero);
                            if (sim.Mines.Count != mines + 1) throw new InvalidOperationException("Special button did not deploy a mine");
                            break;
                        case 4:
                            if (move.Held || fire.Held || shots != 1) throw new InvalidOperationException("Touch release left input active");
                            Finish("PASS: real Touchscreen events moved the tank while charging, released exactly one power shot, deployed a mine, and cleared held input.");
                            break;
                    }
                }
                catch (Exception error) { Finish("FAIL: " + error.Message); }
            };
            EditorApplication.update += tick;
        }

        [MenuItem("Battle Cities/Validate Mobile Touch Input")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run touch input checks in Play mode so the Input System processes player events.");
            BattleTouchDevice.Register();
            var root = new GameObject("Touch input check", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("Touch input check events", typeof(EventSystem));
            var map = new InputActionMap("Touch check");
            var movement = map.AddAction("Move", InputActionType.Value, "<BattleTouchDevice>/move");
            movement.expectedControlType = "Vector2";
            var firing = map.AddAction("Fire", InputActionType.Button, "<BattleTouchDevice>/fire");
            var specialAction = map.AddAction("Special", InputActionType.Button, "<BattleTouchDevice>/special");
            var swapAction = map.AddAction("Swap", InputActionType.Button, "<BattleTouchDevice>/switchSpecial");
            var slotActions = Enumerable.Range(1, 4).Select(i => map.AddAction("Slot" + i, InputActionType.Button, "<BattleTouchDevice>/slot" + i)).ToArray();
            int originalDevices = InputSystem.devices.Count(d => d is BattleTouchDevice);
            int checks = 0;
            void Check(bool valid, string message)
            { if (!valid) throw new InvalidOperationException("MOBILE INPUT FAIL: " + message); checks++; }
            BattleTouchControl Control(string name, bool stick = false)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(root.transform, false);
                ((RectTransform)go.transform).sizeDelta = new Vector2(200, 200);
                RectTransform thumb = null;
                if (stick) { thumb = new GameObject("Thumb", typeof(RectTransform)).GetComponent<RectTransform>(); thumb.SetParent(go.transform, false); }
                var control = go.AddComponent<BattleTouchControl>(); control.Configure(name, thumb); return control;
            }
            try
            {
                var move = Control("move", true); var fire = Control("fire"); var special = Control("special"); var swap = Control("switchSpecial");
                var slots = Enumerable.Range(1, 4).Select(i => Control("slot" + i)).ToArray();
                map.Enable();
                Canvas.ForceUpdateCanvases();
                var left = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 101 };
                var right = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 202 };
                var third = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 303 };
                left.position = RectTransformUtility.WorldToScreenPoint(null, move.transform.position) + Vector2.right * 70;
                move.OnPointerDown(left); fire.OnPointerDown(right); InputSystem.Update();
                Check(movement.ReadValue<Vector2>().x > .9f && firing.IsPressed(), "two fingers move and fire simultaneously (move=" + movement.ReadValue<Vector2>() + ", fire=" + firing.ReadValue<float>() + ", owners=" + move.Held + "/" + fire.Held + ")");
                fire.OnPointerDown(third); fire.OnPointerUp(third); InputSystem.Update();
                Check(firing.IsPressed(), "another finger cannot steal or release an owned control");
                fire.OnPointerUp(right); InputSystem.Update();
                Check(!firing.IsPressed() && movement.ReadValue<Vector2>().x > .9f, "releasing fire preserves movement");
                left.position = RectTransformUtility.WorldToScreenPoint(null, move.transform.position) + new Vector2(30, 70);
                move.OnDrag(left); InputSystem.Update();
                Check(movement.ReadValue<Vector2>().y > .9f && Mathf.Abs(movement.ReadValue<Vector2>().x) < .01f, "drag turns into exactly one cardinal direction");
                move.OnPointerUp(left); InputSystem.Update();
                Check(movement.ReadValue<Vector2>() == Vector2.zero, "releasing movement stops the tank");

                var charge = new ChargedFireInput();
                fire.OnPointerDown(right); fire.OnPointerUp(right); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                Check(charge.TryTakeShot(true, out bool power) && !power, "tap within one input update produces a normal shot");
                Check(!charge.TryTakeShot(true, out _), "tap cannot produce duplicate shots");
                fire.OnPointerDown(right); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                InputSystem.Update();
                charge.Sample(false, false, firing.IsPressed(), ChargedFireInput.ChargeDuration, true);
                Check(charge.Ready && !charge.TryTakeShot(true, out _), "hold charges without firing");
                fire.OnPointerUp(right); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                Check(charge.TryTakeShot(true, out power) && power, "releasing a full charge produces a special shot");

                special.OnPointerDown(right); InputSystem.Update();
                Check(specialAction.WasPressedThisFrame(), "special attack reaches its action");
                InputSystem.Update(); Check(!specialAction.WasPressedThisFrame(), "holding special does not redeploy");
                special.OnPointerUp(right); swap.OnPointerDown(third); InputSystem.Update();
                Check(swapAction.WasPressedThisFrame(), "special selection reaches its action"); swap.OnPointerUp(third);
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i].OnPointerDown(right); InputSystem.Update();
                    Check(slotActions[i].WasPressedThisFrame(), "power-up slot " + (i + 1) + " reaches the correct action");
                    slots[i].OnPointerUp(right); InputSystem.Update();
                    slots[i].Interactable = false; slots[i].OnPointerDown(right); InputSystem.Update();
                    Check(!slotActions[i].IsPressed(), "unavailable power-up slot " + (i + 1) + " cannot activate");
                }
                fire.OnPointerDown(right); InputSystem.Update(); fire.Interactable = false; InputSystem.Update();
                Check(!firing.IsPressed() && !fire.Held, "pausing cancels held fire");
                fire.Interactable = true; fire.OnPointerDown(right); InputSystem.Update();
                fire.CancelInput(); charge.Reset(); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                Check(!charge.TryTakeShot(true, out _), "focus cancellation does not fire on release");
                UnityEngine.Object.DestroyImmediate(root); InputSystem.Update();
                Check(InputSystem.devices.Count(d => d is BattleTouchDevice) == originalDevices, "controls remove their virtual device on teardown");
                Debug.Log("MOBILE INPUT PASS: " + checks + " checks (multi-touch, ownership, drag, tap, charge, special, slots, cancellation, cleanup).");
            }
            finally
            {
                map.Dispose();
                if (root) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }
    }
}
