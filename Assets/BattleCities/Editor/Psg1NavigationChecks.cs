using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using BattleCities.Multiplayer;
using BattleCities.UI;
using PlaySolanaSdk;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Android.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    /// <summary>Controller-only navigation checks. Never logs in, spends fuel, or connects to a room.</summary>
    public static class Psg1NavigationChecks
    {
        public static string Result { get; private set; } = "Not run";
        static PSG1 pad;
        static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        static double nextTick;
        static int checks;
        static string coverage;
        static Selectable Selected => EventSystem.current && EventSystem.current.currentSelectedGameObject ?
            EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>() : null;

        [MenuItem("Battle Cities/Validate All PSG1 Screen Navigation")]
        public static void Run()
            => Begin(Screens(), "login, home, dialogs, tank grid, lobby/keypad, debug, pause and return to menu");

        public static void RunResults()
            => Begin(ResultScreens(), "stage clear, next stage, restart, game over, final stage and return to menu");

        static void Begin(IEnumerator routine, string description)
        {
            if (!Application.isPlaying || !RuntimePlatformInfo.IsPsg1 || routines.Count > 0)
                throw new InvalidOperationException("Start Play mode with the PSG1 simulator and no navigation check running.");
            pad = InputSystem.AddDevice<PSG1>(); checks = 0; coverage = description; Result = "Running " + description;
            routines.Push(routine); nextTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .15;
            try
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Play mode stopped");
                while (routines.Count > 0)
                {
                    var routine = routines.Peek();
                    if (!routine.MoveNext()) { routines.Pop(); continue; }
                    if (routine.Current is IEnumerator child) { routines.Push(child); continue; }
                    if (routine.Current is PSG1StateController state) InputSystem.QueueStateEvent(pad, state);
                    return;
                }
                Finish("PASS: " + checks + " PSG1 screen checks; " + coverage + ".");
            }
            catch (Exception error) { Finish("FAIL (" + Result + "): " + error); }
        }
        static void Finish(string message)
        {
            EditorApplication.update -= Tick; routines.Clear();
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null; Result = message; Debug.Log(message);
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        static IEnumerator Tap(AndroidKeyCode key)
        { yield return new PSG1StateController().WithButton(key); yield return new PSG1StateController(); }
        static IEnumerator Direction(int direction)
        {
            yield return new PSG1StateController().WithAxis(direction < 2 ? AndroidAxis.HatY : AndroidAxis.HatX,
                direction == 0 || direction == 2 ? -1 : 1);
            yield return new PSG1StateController();
        }
        static Selectable[] Links(Selectable s)
        { var n = s.navigation; return new[] { n.selectOnUp, n.selectOnDown, n.selectOnLeft, n.selectOnRight }; }
        static List<int> Path(Selectable target)
        {
            var queue = new Queue<Selectable>(); var paths = new Dictionary<Selectable, List<int>>();
            Check(Selected, "No selected control"); queue.Enqueue(Selected); paths[Selected] = new List<int>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); if (current == target) return paths[current];
                var links = Links(current);
                for (int i = 0; i < links.Length; i++)
                {
                    var link = links[i]; if (!Psg1UiNavigation.Available(link) || paths.ContainsKey(link)) continue;
                    paths[link] = new List<int>(paths[current]) { i }; queue.Enqueue(link);
                }
            }
            throw new InvalidOperationException("Unreachable control: " + target.name + " from " + Selected.name);
        }
        static IEnumerator Go(Selectable target)
        {
            foreach (int direction in Path(target)) yield return Direction(direction);
            Check(Selected == target, "Gamepad did not reach " + target.name + "; selected " + Selected?.name);
            var indicator=target.transform.Find("Controller focus");
            Check(!indicator || !indicator.gameObject.activeSelf,
                "Unexpected focus indicator on " + target.name);
        }
        static void Reachable(IEnumerable<Selectable> items)
        { foreach (var item in items.Where(s => Psg1UiNavigation.Available(s) && s.navigation.mode != Navigation.Mode.None)) Path(item); }
        static IEnumerator Scene(string name)
        {
            var operation = SceneManager.LoadSceneAsync(name);
            while (!operation.isDone) yield return null;
            yield return null; yield return null;
        }
        static Button ButtonNamed(Transform root, string name) => root.GetComponentsInChildren<Button>().First(b => b.name == name);
        static void ResultFrame(BattleGame game, bool won)
        {
            var sim = game.Simulation;
            sim.ApplyFrame(new BattleFrame { Tick = sim.Tick, Score = sim.Score, Started = true, Won = won, Lost = !won,
                BaseAlive = sim.BaseAlive, Participants = sim.Participants.ToArray(), Tanks = sim.Tanks.ToArray(),
                Shots = sim.Shots.ToArray(), Mines = sim.Mines.ToArray(), Drones = sim.Drones.ToArray(), Turrets = sim.Turrets.ToArray(),
                LandDrones = sim.LandDrones.ToArray(), TerrainAlive = sim.Terrain.Select(w => w.Alive).ToArray(),
                ExtraWalls = sim.Terrain.Skip(sim.InitialTerrainCount).ToArray() });
        }
        static IEnumerator ResultScreens()
        {
            yield return Scene("BattleCity");
            var game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            game.LoadStage(1); ResultFrame(game, true); yield return null;
            yield return Tap(AndroidKeyCode.ButtonB);
            Check(game.Stage == 2 && !game.Simulation.Won, "Stage clear A starts next stage");
            ResultFrame(game, false); yield return null;
            yield return Tap(AndroidKeyCode.ButtonB);
            Check(game.Stage == 2 && !game.Simulation.Lost, "Game over A restarts stage");
            ResultFrame(game, false); yield return null;
            yield return Tap(AndroidKeyCode.ButtonA); yield return null;
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Game over B returns to menu");
            yield return Scene("BattleCity"); game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            game.LoadStage(35); ResultFrame(game, true); yield return null;
            yield return Direction(1); yield return Tap(AndroidKeyCode.ButtonB); yield return null;
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Final stage down/A returns to menu without stage 36");
            yield return Scene("BattleCity"); game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            ResultFrame(game, true); yield return null;
            yield return Tap(AndroidKeyCode.ButtonA); yield return null;
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Stage clear B returns to menu");
        }
        static IEnumerator Screens()
        {
            yield return Scene("Login");
            var login = UnityEngine.Object.FindFirstObjectByType<LoginScene>();
            var first = Selected as Button; Check(first && first.name == "Connect Phantom", "Login first focus");
            var loginRoot = first.transform.parent;
            Reachable(loginRoot.GetComponentsInChildren<Selectable>());
            var store = ButtonNamed(loginRoot, "Solana dApp Store");
            yield return Go(store);
            Check(string.IsNullOrEmpty(new SerializedObject(login).FindProperty("dappStoreUrl").stringValue), "Store check must not open an external URL");
            yield return Tap(AndroidKeyCode.ButtonB); // PSG1 A
            Check(loginRoot.GetComponentsInChildren<Text>().Any(t => t.text.Contains("listing is not published")), "Login A must submit");
            yield return Tap(AndroidKeyCode.ButtonA); // PSG1 B
            Check(Selected == first, "Login B returns to first control");
            first.interactable = false; yield return null;
            Check(Selected != first && Psg1UiNavigation.Available(Selected), "Disabled login control recovers focus");
            first.interactable = true;

            Result = "Running main menu and dialog checks";
            yield return Scene("MainMenu");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            Check(Selected == menu.StartButton, "Home starts focused");
            Reachable(menu.GetComponentsInChildren<Selectable>());
            yield return Go(BattleSession.Instance.Lobby.OpenButton);
            yield return Go(menu.StartButton);
            for (int i = 1; i < menu.Tabs.Length; i++)
            {
                yield return Go(menu.Tabs[i]); yield return Tap(AndroidKeyCode.ButtonB);
                Check(menu.IsModalOpen, "Tab opens its dialog: " + menu.Tabs[i].name);
                var close = Selected; yield return Direction(1);
                Check(Selected == close, "Modal contains focus");
                yield return Tap(AndroidKeyCode.ButtonA);
                Check(!menu.IsModalOpen && Selected == menu.Tabs[i], "B restores tab focus");
            }
            var settings = (Button)new SerializedObject(menu).FindProperty("settingsButton").objectReferenceValue;
            yield return Go(settings); yield return Tap(AndroidKeyCode.ButtonB);
            Check(menu.IsModalOpen, "Controller help dialog opens");
            yield return Tap(AndroidKeyCode.ButtonB);
            Check(!menu.IsModalOpen && Selected == settings, "A closes dialog and restores focus");
            EventSystem.current.SetSelectedGameObject(null); yield return null;
            Check(Selected == menu.StartButton, "Home recovers cleared focus");

            Result = "Running tank roster and lobby checks";
            yield return Tap(AndroidKeyCode.ButtonB);
            var preBattle = menu.GetComponent<PreBattleScreen>(); Check(preBattle.IsOpen, "A opens tank selection");
            var root = menu.Content.GetComponentsInChildren<RectTransform>().First(r => r.name == "Pre-battle screens");
            Reachable(root.GetComponentsInChildren<Selectable>());
            var scroll = root.GetComponentInChildren<TankRosterScroll>();
            var cards = scroll.content.GetComponentsInChildren<Button>(); Check(cards.Length == 8, "All eight cards present");
            foreach (var card in cards) yield return Go(card);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)cards[7].transform);
            Check(bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1, "Last roster card scrolls fully into view");
            yield return Go(cards[3]); yield return Tap(AndroidKeyCode.ButtonB);
            var beforeLobby = Selected;
            var lobby = BattleSession.Instance.Lobby;
            lobby.Show(); yield return null;
            var panel = lobby.GetComponentsInChildren<RectTransform>().First(t => t.name == "Room panel");
            Reachable(panel.GetComponentsInChildren<Selectable>());
            var codeButton = ButtonNamed(panel, "ENTER ROOM CODE");
            yield return Go(codeButton); yield return Tap(AndroidKeyCode.ButtonB);
            var keypad = panel.Find("Room code keypad"); Check(keypad.gameObject.activeSelf, "A opens room code keypad");
            Reachable(keypad.GetComponentsInChildren<Selectable>());
            yield return Tap(AndroidKeyCode.ButtonB); // A character
            yield return Direction(3); yield return Tap(AndroidKeyCode.ButtonB); // B character
            var input = panel.GetComponentInChildren<InputField>(true);
            Check(input.text == "AB", "Controller enters room code characters");
            yield return Go(ButtonNamed(keypad, "-")); yield return Tap(AndroidKeyCode.ButtonB);
            Check(input.text == "AB-", "Room code supports region separator");
            yield return Go(ButtonNamed(keypad, "DELETE")); yield return Tap(AndroidKeyCode.ButtonB);
            Check(input.text == "AB", "Controller deletes character");
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(lobby.Visible && !keypad.gameObject.activeSelf && Selected == codeButton && preBattle.IsOpen, "B closes only keypad");
            Reachable(panel.GetComponentsInChildren<Selectable>());
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(!lobby.Visible && preBattle.IsOpen && Selected == beforeLobby, "Lobby B restores underlying selection without closing tank page");
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(!preBattle.IsOpen && Selected == menu.StartButton, "Tank B returns home");

            Result = "Running battle pause/debug navigation checks";
            yield return Scene("BattleCity");
            var game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            yield return Tap(AndroidKeyCode.ButtonStart); Check(game.Paused, "Start pauses battle");
            for (int i = 0; i < 3; i++) yield return Direction(1);
            yield return Tap(AndroidKeyCode.ButtonB);
            var debugRoot = game.GetComponentsInChildren<Canvas>().First(c => c.name == "Touch Debug Canvas");
            Check(debugRoot.gameObject.activeInHierarchy, "Pause menu opens debug controls");
            Reachable(debugRoot.GetComponentsInChildren<Selectable>());
            var slider = debugRoot.GetComponentsInChildren<Slider>().First();
            yield return Go(slider); float value = slider.value;
            yield return Direction(3); Check(slider.value > value, "D-pad right adjusts slider");
            yield return Direction(2); Check(Mathf.Approximately(slider.value, value), "D-pad left restores slider");
            var debugButtons = debugRoot.GetComponentsInChildren<Button>();
            yield return Go(debugButtons[debugButtons.Length - 1]);
            var debugScroll = debugRoot.GetComponentInChildren<ScrollRect>();
            bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(debugScroll.viewport, (RectTransform)Selected.transform);
            Check(bounds.min.y >= debugScroll.viewport.rect.yMin - 1, "Debug scroll follows focus");
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(!debugRoot.gameObject.activeInHierarchy && game.Paused, "B returns from debug to pause");
            yield return Direction(1); yield return Tap(AndroidKeyCode.ButtonB);
            yield return null; yield return null;
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Controller returns from pause to MainMenu");
            Check(UnityEngine.Device.Screen.width == 1240 && UnityEngine.Device.Screen.height == 1080, "PSG1 orientation unchanged");
        }
    }
}
