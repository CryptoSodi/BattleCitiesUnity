using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleCities.Core;
using BattleCities.Multiplayer;
using BattleCities.UI;
using PlaySolanaSdk;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        static Keyboard keyboard;
        static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        static double nextTick;
        static int checks;
        static string coverage;
        static Selectable Selected => EventSystem.current && EventSystem.current.currentSelectedGameObject ?
            EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>() : null;

        [MenuItem("Battle Cities/Validate All PSG1 Screen Navigation")]
        public static void Run()
            => Begin(Screens(), "login, home, dialogs, tank grid, lobby/keypad and uninterrupted battle controls");

        public static void RunResults()
            => Begin(ResultScreens(), "full-screen results, next stage, game over, final stage and return to menu");

        [MenuItem("Battle Cities/Validate PSG1 Main Menu Shortcuts")]
        public static void RunMenuShortcuts()
            => Begin(MenuShortcuts(), "Start, Select, L1, R1, page focus and return focus");

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
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            pad = null;keyboard=null; Result = message; Debug.Log(message);
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        static IEnumerator Tap(AndroidKeyCode key)
        { yield return new PSG1StateController().WithButton(key); yield return new PSG1StateController(); }
        static IEnumerator TapRightTrigger()
        { yield return new PSG1StateController().WithAxis(AndroidAxis.Gas,1); yield return new PSG1StateController(); }
        static IEnumerator TapRightShoulder()
        { yield return Tap(AndroidKeyCode.ButtonR1); }
        static IEnumerator TapKey(Key key)
        { InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null; }

        static IEnumerator MenuShortcuts()
        {
            yield return Scene("MainMenu");
            var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuScene>();
            Check(menu&&Selected==menu.StartButton,"home starts on Start");
            var legend=menu.Content.Find("PSG1 Controls/Extended PSG1 hints");
            Check(legend&&legend.Find("Guide artwork/Dpad icon")&&legend.Find("Guide artwork/L1 icon")&&legend.Find("Guide artwork/R1 icon")&&legend.Find("Guide artwork/X icon")&&legend.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("SETTINGS"))&&legend.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("SELECT TANK")),"home legend includes controller artwork and menu shortcuts");
            yield return Tap(AndroidKeyCode.ButtonStart);
            var preBattle=menu.GetComponent<PreBattleScreen>();
            Check(preBattle&&preBattle.IsOpen,"Start opens tank selection");
            var tank=Selected.navigation.selectOnRight;
            yield return Direction(3);Check(Selected==tank,"D-pad focuses another tank without activating it");
            yield return Direction(0);Check(Selected&&Selected.name=="Back","D-pad up reaches tank selection header");
            yield return Direction(1);Check(Selected==tank,"D-pad down restores the last focused tank");
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(!preBattle.IsOpen&&Selected==menu.StartButton,"Back restores Start");
            yield return TapRightTrigger();Check(!menu.IsShopOpen,"R2 does not change the main menu page");
            yield return TapRightShoulder();
            Check(menu.IsShopOpen&&Selected&&Selected.transform.IsChildOf(menu.Content.Find("Main Display/Shop screen")),"R1 opens Shop and focuses its contents");
            var shopRoot=menu.Content.Find("Main Display/Shop screen");
            var filter=shopRoot.GetComponentsInChildren<Button>().First(b=>b.name=="FUEL"&&b.interactable);
            EventSystem.current.SetSelectedGameObject(filter.gameObject);yield return null;
            var rightOfFilter=filter.navigation.selectOnRight;
            yield return Direction(3);Check(Selected==rightOfFilter,"D-pad right moves through Shop filters");
            yield return Direction(2);Check(Selected==filter,"D-pad left returns to the same filter");
            var header=filter.navigation.selectOnUp;
            yield return Direction(0);Check(Selected==header,"D-pad up enters the Shop header");
            var nextHeader=header.navigation.selectOnRight;
            yield return Direction(3);Check(Selected==nextHeader,"D-pad moves to another header option");
            yield return Direction(1);Check(Selected==filter,"D-pad down restores the last focused filter, not the active filter or matching column");
            var belowFilter=filter.navigation.selectOnDown;
            Check(Psg1UiNavigation.Available(belowFilter),"Shop filter has a reachable control below");
            yield return Direction(1);Check(Selected==belowFilter,"D-pad down enters the Shop content");
            yield return Direction(0);Check(Selected==filter,"D-pad up returns to the same Shop filter");
            var shop=menu.GetComponent<ShopScreen>();
            typeof(ShopScreen).GetMethod("ShowNotice",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shop,new object[]{"TEST","Return-focus check"});
            yield return null;yield return Tap(AndroidKeyCode.ButtonA);
            Check(Selected==filter,"Back from a Shop notice restores its originating control");
            yield return Tap(AndroidKeyCode.ButtonSelect);
            Check(menu.IsSettingsOpen,"Select opens Settings from Shop");
            var settingOrigin=Selected;var settingNext=settingOrigin.navigation.selectOnDown;
            if(Psg1UiNavigation.Available(settingNext)&&settingNext!=settingOrigin){
                yield return Direction(1);Check(Selected==settingNext,"D-pad down moves within Settings");
                yield return Direction(0);Check(Selected==settingOrigin,"D-pad up returns within Settings");
            }
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(menu.IsShopOpen&&Selected==filter,"Back from Settings restores the exact Shop control");
            yield return Tap(AndroidKeyCode.ButtonSelect);Check(menu.IsSettingsOpen,"Settings reopens from Shop");
            yield return TapRightShoulder();
            Check(menu.IsRankingOpen&&Selected&&Selected.transform.IsChildOf(menu.Content.Find("Main Display/Ranking screen")),"R1 moves from Settings to Rankings and focuses it");
            var ranking=menu.GetComponent<RankingScreen>();ranking.TogglePicker();yield return null;
            Check(ranking.IsPickerOpen,"Ranking period picker opens");yield return Tap(AndroidKeyCode.ButtonA);
            Check(!ranking.IsPickerOpen&&Selected&&Selected.name=="Period picker","Back from the period picker returns to its opening control");
            yield return TapRightShoulder();Check(menu.IsOperationsOpen&&!menu.IsSocialsOpen,"R1 opens Quarters");
            var nextQuarterCard=Selected.navigation.selectOnRight;
            if(Psg1UiNavigation.Available(nextQuarterCard)&&nextQuarterCard.GetComponent("OperationsCardFocus"))
            {
                yield return Direction(3);Check(Selected==nextQuarterCard,"Quarters focuses another card");
                yield return Direction(0);Check(Selected==nextQuarterCard.navigation.selectOnUp,"Quarters enters its header");
                yield return Direction(1);Check(Selected==nextQuarterCard,"Quarters restores the last focused card");
            }
            yield return TapRightShoulder();Check(menu.IsSocialsOpen,"R1 opens Socials");
            yield return TapRightShoulder();Check(!menu.IsOperationsOpen&&Selected==menu.StartButton,"R1 wraps to Play");
            yield return Tap(AndroidKeyCode.ButtonL1);Check(menu.IsSocialsOpen,"L1 moves to previous page");
            yield return TapRightShoulder();yield return TapRightShoulder();
            Check(menu.IsShopOpen&&Selected==filter,"returning to Shop restores the selected filter");
            yield return Tap(AndroidKeyCode.ButtonA);Check(!menu.IsShopOpen&&Selected==menu.Tabs[1],"Back restores the Shop menu option");
            yield return Tap(AndroidKeyCode.ButtonSelect);Check(menu.IsSettingsOpen,"Select opens Settings from home");
            yield return Tap(AndroidKeyCode.ButtonStart);Check(preBattle.IsOpen,"Start opens tank selection from Settings");
            yield return Tap(AndroidKeyCode.ButtonA);Check(!preBattle.IsOpen,"Back leaves tank selection");
            // PSG1 face labels are Nintendo-style: its X is the north face button.
            yield return Tap(AndroidKeyCode.ButtonY);var profileOrigin=Selected;
            Check(menu.IsPlayerProfileOpen&&profileOrigin,"PSG1 X opens Profile with a selected control");
            yield return Tap(AndroidKeyCode.ButtonSelect);Check(menu.IsSettingsOpen,"Select opens Settings from Profile");
            yield return Tap(AndroidKeyCode.ButtonA);
            Check(menu.IsPlayerProfileOpen&&Selected==profileOrigin,"Back restores the Profile control that had focus");
            yield return Tap(AndroidKeyCode.ButtonA);Check(!menu.IsPlayerProfileOpen,"Back leaves Profile");
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);pad=null;
            keyboard=InputSystem.AddDevice<Keyboard>();yield return null;
            yield return TapKey(Key.Space);
            Check(menu.IsShopOpen&&!preBattle.IsOpen,"keyboard Space mirrors R1 without submitting Start");
            yield return TapKey(Key.E);
            Check(!menu.IsShopOpen&&Selected==menu.StartButton,"keyboard E mirrors L1 without a connected gamepad");
            yield return TapKey(Key.Minus);Check(menu.IsSettingsOpen,"keyboard minus mirrors Select and opens Settings");
            yield return TapKey(Key.Escape);Check(!menu.IsSettingsOpen&&Selected==menu.StartButton,"keyboard Escape closes Settings");
            yield return TapKey(Key.Equals);Check(preBattle.IsOpen,"keyboard equals mirrors Start and opens tank selection");
            yield return TapKey(Key.Escape);Check(!preBattle.IsOpen&&Selected==menu.StartButton,"keyboard Escape returns after equals");
            yield return TapKey(Key.X);Check(menu.IsPlayerProfileOpen,"keyboard X opens Profile");
            yield return TapKey(Key.Escape);Check(!menu.IsPlayerProfileOpen&&Selected==menu.StartButton,"keyboard Escape returns from Profile");
            yield return TapKey(Key.Enter);Check(preBattle.IsOpen,"keyboard Enter still selects Start");
            yield return TapKey(Key.Escape);Check(!preBattle.IsOpen&&Selected==menu.StartButton,"keyboard Escape returns from tank selection");
        }
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
        static IEnumerator ResultReady()
        {
            float until=Time.unscaledTime+.4f;while(Time.unscaledTime<until)yield return null;
            var results=UnityEngine.Object.FindAnyObjectByType<BattleResultsScreen>();
            Check(results,"Full-screen result screen is visible");
            Check(Selected&&Selected.name=="Continue","Continue receives initial focus");
            Reachable(results.GetComponentsInChildren<Selectable>());
        }
        static IEnumerator ResultScreens()
        {
            yield return Scene("BattleCity");
            var game=UnityEngine.Object.FindAnyObjectByType<BattleGame>();
            game.LoadStage(1);ResultFrame(game,true);yield return ResultReady();
            yield return Tap(AndroidKeyCode.ButtonB);
            Check(game.Stage==2&&!game.Simulation.Won,"Stage clear A starts next stage");
            ResultFrame(game,false);yield return ResultReady();
            yield return Tap(AndroidKeyCode.ButtonB);yield return null;
            Check(SceneManager.GetActiveScene().name=="MainMenu","Defeat Continue returns to menu");
            yield return Scene("BattleCity");game=UnityEngine.Object.FindAnyObjectByType<BattleGame>();
            game.LoadStage(35);ResultFrame(game,true);yield return ResultReady();
            yield return Tap(AndroidKeyCode.ButtonB);yield return null;
            Check(SceneManager.GetActiveScene().name=="MainMenu","Final-stage Continue returns to menu without stage 36");
            yield return Scene("BattleCity");game=UnityEngine.Object.FindAnyObjectByType<BattleGame>();
            ResultFrame(game,true);yield return ResultReady();
            yield return Tap(AndroidKeyCode.ButtonA);yield return null;
            Check(SceneManager.GetActiveScene().name=="MainMenu","Results B returns to menu");
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

            Result = "Checking removed battle pause controls";
            yield return Scene("BattleCity");
            var game = UnityEngine.Object.FindFirstObjectByType<BattleGame>();
            yield return Tap(AndroidKeyCode.ButtonStart); Check(!game.Paused, "Start no longer pauses battle");
            yield return Tap(AndroidKeyCode.ButtonSelect); Check(!game.Paused, "Select no longer opens a pause/help menu");
            Check(!game.CanSwapSpecials, "Single-special tank does not offer Swap");
            yield return Scene("MainMenu");
            Check(UnityEngine.Device.Screen.width == 1240 && UnityEngine.Device.Screen.height == 1080, "PSG1 orientation unchanged");
        }
    }
}
