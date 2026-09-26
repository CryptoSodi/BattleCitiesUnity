using System;
using BattleCities.Core;

namespace BattleCities.Multiplayer
{
    public static class BattleLaunchOptions
    {
        public static BattleMode Mode { get; private set; } = BattleMode.Offline;
        public static bool HasModeFlag { get; private set; }
        public static string Error { get; private set; }

        public static void Read(string[] arguments)
        {
            Mode = BattleMode.Offline;
            HasModeFlag = false;
            Error = null;
            for (int i = 1; i < arguments.Length; i++)
            {
                if (!string.Equals(arguments[i], "--mode", StringComparison.OrdinalIgnoreCase)) continue;
                if (HasModeFlag || i + 1 >= arguments.Length)
                {
                    Error = "Use one launch flag: --mode offline, --mode coop or --mode pvp.";
                    return;
                }
                HasModeFlag = true;
                var value = arguments[++i];
                if (string.Equals(value, "offline", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.Offline;
                else if (string.Equals(value, "coop", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.Coop;
                else if (string.Equals(value, "pvp", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.Versus;
                else
                {
                    Error = "Unknown mode '" + value + "'. Use --mode offline, --mode coop or --mode pvp.";
                    return;
                }
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() => Read(Environment.GetCommandLineArgs());
    }
}
