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
                    Error = "Use one launch flag: --mode offline, --mode coop, --mode 2v2, --mode ctf or --mode ctf1v1.";
                    return;
                }
                HasModeFlag = true;
                var value = arguments[++i];
                if (string.Equals(value, "offline", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.Offline;
                else if (string.Equals(value, "coop", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.Coop;
                else if ((string.Equals(value, "ctf", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "ctf2v2", StringComparison.OrdinalIgnoreCase))) Mode = BattleMode.CaptureFlag;
                else if (string.Equals(value, "2v2", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.TeamBattle;
                else if (string.Equals(value, "ctf1v1", StringComparison.OrdinalIgnoreCase)) Mode = BattleMode.CaptureFlagDuel;
                else
                {
                    Error = "Unknown mode '" + value + "'. Use --mode offline, --mode coop, --mode 2v2, --mode ctf or --mode ctf1v1.";
                    return;
                }
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ReadWebUrl(UnityEngine.Application.absoluteURL);
#else
            Read(Environment.GetCommandLineArgs());
#endif
        }

        public static void ReadWebUrl(string url)
        {
            var arguments=new System.Collections.Generic.List<string>{"web"};
            if(Uri.TryCreate(url,UriKind.Absolute,out var uri))
                foreach(var item in uri.Query.TrimStart('?').Split('&'))
                {
                    var pair=item.Split(new[]{'='},2);
                    if(pair.Length!=2||!string.Equals(Uri.UnescapeDataString(pair[0]),"mode",StringComparison.OrdinalIgnoreCase))continue;
                    arguments.Add("--mode");arguments.Add(Uri.UnescapeDataString(pair[1]));
                }
            Read(arguments.ToArray());
        }
    }
}
