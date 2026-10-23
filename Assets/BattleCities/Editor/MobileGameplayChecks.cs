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
            var special = game.GetComponentsInChildren<UnityEngine.UI.Button>().Single(c => c.name == "Special Mine");
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
            void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Component control, Vector2 offset)
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
                            Touch(101, UnityEngine.InputSystem.TouchPhase.Began, move, Vector2.zero);
                            break;
                        case 1:
                            Touch(101, UnityEngine.InputSystem.TouchPhase.Moved, move, Vector2.up * 90);
                            Touch(202, UnityEngine.InputSystem.TouchPhase.Began, fire, Vector2.zero);
                            next = EditorApplication.timeSinceStartup + 1.4;
                            break;
                        case 2:
                            if (Vector2.Distance(start, new Vector2(player.X, player.Y)) < 1 || !fire.Held || shots != 0)
                                throw new InvalidOperationException("Movement/fire check: distance=" + Vector2.Distance(start, new Vector2(player.X, player.Y)) + ", fireHeld=" + fire.Held + ", shots=" + shots + ", touchEnabled=" + touchscreen.enabled + ", paused=" + game.Paused);
                            Touch(101, UnityEngine.InputSystem.TouchPhase.Ended, move, Vector2.up * 90);
                            Touch(202, UnityEngine.InputSystem.TouchPhase.Ended, fire, Vector2.zero);
                            break;
                        case 3:
                            if (shots != 1 || !power) throw new InvalidOperationException("Charged release did not fire exactly one power shot");
                            Touch(303, UnityEngine.InputSystem.TouchPhase.Began, special, Vector2.zero);
                            break;
                        case 4:
                            Touch(303, UnityEngine.InputSystem.TouchPhase.Ended, special, Vector2.zero);
                            break;
                        case 5:
                            if (sim.Mines.Count != mines + 1) throw new InvalidOperationException("Special button did not deploy a mine");
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
            RunChecks();
        }

        public static void RunBatch()
        {
            var settings=InputSystem.settings;
            const string feature="RUN_PLAYER_UPDATES_IN_EDIT_MODE";
            bool original=(bool)typeof(InputSettings).GetMethod("IsFeatureEnabled",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(settings,new object[]{feature});
            var activeInstances=typeof(UnityEngine.InputSystem.OnScreen.OnScreenControl).GetField("s_nbActiveInstances",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var originalInstances=activeInstances.GetValue(null);
            try
            {
                settings.SetInternalFeatureFlag(feature,true);
                RunChecks();
            }
            finally { settings.SetInternalFeatureFlag(feature,original);activeInstances.SetValue(null,originalInstances); }
        }

        static void RunChecks()
        {
            BattleTouchDevice.Register();
            var root = new GameObject("Touch input check", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("Touch input check events", typeof(EventSystem));
            var map = new InputActionMap("Touch check");
            var movement = map.AddAction("Move", InputActionType.Value, "<BattleTouchDevice>/move");
            movement.expectedControlType = "Vector2";
            var firing = map.AddAction("Fire", InputActionType.Button, "<BattleTouchDevice>/fire");
            var aiming = map.AddAction("Aim", InputActionType.Value, "<BattleTouchDevice>/aim");
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
                var fireThumb=new GameObject("Turret Thumb",typeof(RectTransform)).GetComponent<RectTransform>();fireThumb.SetParent(fire.transform,false);
                fire.ConfigureTurret(fireThumb);
                var slots = Enumerable.Range(1, 4).Select(i => Control("slot" + i)).ToArray();
                map.Enable();
                Canvas.ForceUpdateCanvases();
                var left = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 101 };
                var right = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 202 };
                var third = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 303 };
                Vector2 fireCenter=RectTransformUtility.WorldToScreenPoint(null,fire.transform.position);
                right.position=fireCenter;
                left.position = RectTransformUtility.WorldToScreenPoint(null, move.transform.position) + Vector2.right * 70;
                move.OnPointerDown(left); fire.OnPointerDown(right); InputSystem.Update();
                Check(movement.ReadValue<Vector2>().x > .9f && firing.IsPressed(), "two fingers move and fire simultaneously (move=" + movement.ReadValue<Vector2>() + ", fire=" + firing.ReadValue<float>() + ", owners=" + move.Held + "/" + fire.Held + ")");
                right.position=fireCenter+Vector2.up*70;fire.OnDrag(right);InputSystem.Update();
                Check(aiming.ReadValue<Vector2>().y>.9f&&movement.ReadValue<Vector2>().x>.9f&&firing.IsPressed(),"right stick aims up while moving right and holding fire");
                third.position=fireCenter+Vector2.left*70;fire.OnDrag(third);InputSystem.Update();
                Check(aiming.ReadValue<Vector2>().y>.9f,"another finger cannot steal turret aim");
                fire.OnPointerDown(third); fire.OnPointerUp(third); InputSystem.Update();
                Check(firing.IsPressed(), "another finger cannot steal or release an owned control");
                fire.OnPointerUp(right); InputSystem.Update();
                Check(!firing.IsPressed() && movement.ReadValue<Vector2>().x > .9f, "releasing fire preserves movement");
                Check(aiming.ReadValue<Vector2>()==Vector2.zero&&fire.LastAim==Vector2.up&&fireThumb.anchoredPosition==Vector2.zero,"release centers stick, clears held aim and retains shot direction");
                Check(fire.TakeReleasedAim()==Vector2.up&&fire.TakeReleasedAim()==Vector2.zero,"release direction is consumed only once");
                left.position = RectTransformUtility.WorldToScreenPoint(null, move.transform.position) + new Vector2(30, 70);
                move.OnDrag(left); InputSystem.Update();
                Check(movement.ReadValue<Vector2>().y > .9f && Mathf.Abs(movement.ReadValue<Vector2>().x) < .01f, "drag turns into exactly one cardinal direction");
                move.OnPointerUp(left); InputSystem.Update();
                Check(movement.ReadValue<Vector2>() == Vector2.zero, "releasing movement stops the tank");

                var charge = new ChargedFireInput();
                right.position=fireCenter;
                fire.OnPointerDown(right); fire.OnPointerUp(right); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                Check(charge.TryTakeShot(true, out bool power) && !power, "tap within one input update produces a normal shot");
                Check(!charge.TryTakeShot(true, out _), "tap cannot produce duplicate shots");
                fire.OnPointerDown(right); InputSystem.Update();
                Check(fire.LastAim==Vector2.zero,"new centered fire press does not reuse the previous shot's aim");
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                right.position=fireCenter+Vector2.left*70;fire.OnDrag(right);InputSystem.Update();
                Check(firing.IsPressed()&&aiming.ReadValue<Vector2>().x<-.9f,"turret drag does not interrupt held charge");
                InputSystem.Update();
                charge.Sample(false, false, firing.IsPressed(), ChargedFireInput.ChargeDuration, true);
                Check(charge.Ready && !charge.TryTakeShot(true, out _), "hold charges without firing");
                fire.OnPointerUp(right); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                Check(charge.TryTakeShot(true, out power) && power, "releasing a full charge produces a special shot");
                Check(fire.LastAim==Vector2.left&&aiming.ReadValue<Vector2>()==Vector2.zero,"charged release retains final turret direction");
                InputSystem.Update();right.position=fireCenter+Vector2.right*70;
                fire.OnPointerDown(right);fire.OnPointerUp(right);InputSystem.Update();
                Check(firing.WasPressedThisFrame()&&firing.WasReleasedThisFrame()&&fire.LastAim==Vector2.right,"quick aim and release within one frame preserves final aim");
                right.position=fireCenter;

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
                Check(!firing.IsPressed() && !fire.Held&&aiming.ReadValue<Vector2>()==Vector2.zero&&fire.LastAim==Vector2.zero, "pausing cancels held fire and remembered aim");
                fire.Interactable = true; fire.OnPointerDown(right); InputSystem.Update();
                fire.CancelInput(); charge.Reset(); InputSystem.Update();
                charge.Sample(firing.WasPressedThisFrame(), firing.WasReleasedThisFrame(), firing.IsPressed(), .01f, true);
                Check(!charge.TryTakeShot(true, out _), "focus cancellation does not fire on release");
                move.ConfigureFloating();fire.ConfigureFloating();
                var moveZone=new GameObject("Left zone",typeof(BattleTouchZone)).GetComponent<BattleTouchZone>();moveZone.transform.SetParent(root.transform,false);moveZone.Configure(move);
                var fireZone=new GameObject("Right zone",typeof(BattleTouchZone)).GetComponent<BattleTouchZone>();fireZone.transform.SetParent(root.transform,false);fireZone.Configure(fire);
                Check(move.GetComponent<CanvasGroup>().alpha==0&&fire.GetComponent<CanvasGroup>().alpha==0,"floating controls are hidden until touched");
                left.position=new Vector2(160,180);right.position=new Vector2(640,210);
                moveZone.OnPointerDown(left);fireZone.OnPointerDown(right);InputSystem.Update();
                Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,move.transform.position),left.position)<.1f,"movement origin follows left touch");
                Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,fire.transform.position),right.position)<.1f,"fire origin follows right touch");
                Check(movement.ReadValue<Vector2>()==Vector2.zero&&firing.IsPressed(),"touch origin starts neutral movement and held fire");
                left.position+=Vector2.right*70;right.position+=Vector2.up*70;
                moveZone.OnDrag(left);fireZone.OnDrag(right);InputSystem.Update();
                Check(movement.ReadValue<Vector2>().x>.9f&&aiming.ReadValue<Vector2>().y>.9f&&firing.IsPressed(),"floating zones move and aim/fire concurrently");
                var fireOrigin=((RectTransform)fire.transform).anchoredPosition;
                fireZone.OnPointerDown(third);fireZone.OnPointerUp(third);InputSystem.Update();
                Check(fire.Held&&((RectTransform)fire.transform).anchoredPosition==fireOrigin,"second finger cannot move or release floating origin");
                left.position=new Vector2(900,180);moveZone.OnDrag(left);InputSystem.Update();
                Check(movement.ReadValue<Vector2>().x>.9f&&fire.Held,"crossing midpoint keeps original movement ownership");
                fireZone.OnPointerUp(right);moveZone.OnPointerUp(left);InputSystem.Update();
                Check(!fire.Held&&!move.Held&&fire.GetComponent<CanvasGroup>().alpha==0&&move.GetComponent<CanvasGroup>().alpha==0,"release hides both floating controls");
                Check(fire.TakeReleasedAim()==Vector2.up,"floating fire release retains selected turret aim");
                left.position=new Vector2(230,300);moveZone.OnPointerDown(left);InputSystem.Update();
                Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,move.transform.position),left.position)<.1f,"next touch chooses a fresh movement origin");
                move.CancelInput();InputSystem.Update();
                // In batch Edit mode Unity does not run player MonoBehaviour disable callbacks.
                // Invoke those same callbacks for this fixture; Play mode uses normal lifecycle cleanup.
                if(!Application.isPlaying)foreach(var control in root.GetComponentsInChildren<UnityEngine.InputSystem.OnScreen.OnScreenControl>())control.GetType().GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(control,null);
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
