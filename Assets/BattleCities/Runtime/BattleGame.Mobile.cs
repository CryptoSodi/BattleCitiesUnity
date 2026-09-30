using UnityEngine;
using UnityEngine.InputSystem;
using MobileScreen = UnityEngine.Device.Screen;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private BattleTouchControls touchControls;
        internal bool HasTouchControls => touchControls;
        internal bool TouchActionsAvailable => Simulation != null && Simulation.CanAcceptPlayerFire && !paused &&
            !consumePending && !showDebug && !TouchLobbyVisible && (!IsOnline || Simulation.MatchStarted);
        internal bool TouchLobbyVisible => Multiplayer.BattleSession.Instance && Multiplayer.BattleSession.Instance.Lobby.Visible;
        internal float TouchChargeProgress => primaryCharge.Progress;
        internal bool TouchConsumePending => consumePending;
        internal string TouchSlotType(int slot) => economy ? economy.SlotType(slot) : null;
        internal int TouchSlotCount(int slot) => economy ? economy.SlotCount(slot) : 0;
        internal bool TouchSlotAvailable(int slot) => TouchActionsAvailable && !IsOnline && economy &&
            economy.Authenticated && economy.SlotCount(slot) > 0;

        private void ConfigureTouchBindings()
        {
            BattleTouchDevice.Register();
            string[] directions = { "up", "right", "down", "left" };
            for (int i = 0; i < moveKeys.Length; i++) moveKeys[i].AddBinding("<BattleTouchDevice>/move/" + directions[i]);
            fire.AddBinding("<BattleTouchDevice>/fire");
            secondaryFire.AddBinding("<BattleTouchDevice>/special");
            secondarySelect.AddBinding("<BattleTouchDevice>/switchSpecial");
            for (int i = 0; i < slots.Length; i++) slots[i].AddBinding("<BattleTouchDevice>/slot" + (i + 1));
        }

        private void CreateTouchControls()
        {
            if (RuntimePlatformInfo.Current != GameRuntimePlatform.Android || touchControls) return;
            touchControls = gameObject.AddComponent<BattleTouchControls>();
            touchControls.Initialize(this);
        }

        internal void CancelTouchGameplay()
        {
            if (touchControls) touchControls.CancelInput();
            ResetPrimaryFire();
            secondaryQueued = false;
        }

        private void OnApplicationPause(bool suspended)
        {
            if (!suspended) return;
            if (!IsOnline) paused = true;
            CancelTouchGameplay();
        }

        private void OnDisable() { InputSystem.onDeviceChange -= GamepadDeviceChanged; ReleaseDebugController(); CancelTouchGameplay(); input?.Disable(); }
        private void OnEnable() { InputSystem.onDeviceChange += GamepadDeviceChanged; input?.Enable(); }
    }

    public static class MobileBattleOrientation
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartInPortrait()
        {
            ShowMenu();
        }

        public static ScreenOrientation OrientationFor(GameRuntimePlatform platform, bool battle, ScreenOrientation current)
        {
            // PSG1's natural (Portrait) orientation is its native 1240 x 1080 screen.
            if (platform == GameRuntimePlatform.Psg1) return ScreenOrientation.Portrait;
            if (platform == GameRuntimePlatform.Android) return battle ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            return current;
        }

        public static void ShowBattle() => Apply(true);
        public static void ShowMenu() => Apply(false);
        private static void Apply(bool battle)
        {
            var platform = RuntimePlatformInfo.Current;
            if (platform != GameRuntimePlatform.Android && platform != GameRuntimePlatform.Psg1) return;
            MobileScreen.orientation = OrientationFor(platform, battle, MobileScreen.orientation);
        }
    }
}
