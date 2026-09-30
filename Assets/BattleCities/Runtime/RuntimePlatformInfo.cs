using System;
using UnityEngine;
using SimulatedScreen = UnityEngine.Device.Screen;
using SimulatedSystemInfo = UnityEngine.Device.SystemInfo;

namespace BattleCities
{
    public enum GameRuntimePlatform
    {
        Desktop,
        Android,
        Psg1,
        WebGl,
        Other
    }

    public enum PowerupUiMode
    {
        GameOverlay,
        WebTopLeft,
        MergedTopHud,
        ExternalController
    }

    /// <summary>
    /// Resolves the runtime once for every scene. Platform-specific UI scenes
    /// and presenters must consume this class instead of duplicating platform
    /// checks, so Android, PSG1 and WebGL use one consistent classification.
    /// Add BATTLE_CITIES_PSG1 to the PSG1 build's Scripting Define Symbols for
    /// guaranteed identification; device properties provide the fallback.
    /// </summary>
    public static class RuntimePlatformInfo
    {
        private static readonly GameRuntimePlatform detectedPlatform;
        public static GameRuntimePlatform Current
        {
            get
            {
#if UNITY_EDITOR
                if (IsPsg1Simulator()) return GameRuntimePlatform.Psg1;
                if (SimulatedSystemInfo.operatingSystem.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0)
                    return GameRuntimePlatform.Android;
#endif
                return detectedPlatform;
            }
        }
        public static string DeviceIdentity { get; }

        public static bool IsWeb => Current == GameRuntimePlatform.WebGl;
        public static bool IsAndroid => Current == GameRuntimePlatform.Android || IsPsg1;
        public static bool IsPsg1 => Current == GameRuntimePlatform.Psg1;
        public static PowerupUiMode PowerupUi => PowerupUiFor(Current);

        public static PowerupUiMode PowerupUiFor(GameRuntimePlatform platform)
        {
            if (platform == GameRuntimePlatform.WebGl) return PowerupUiMode.WebTopLeft;
            if (platform == GameRuntimePlatform.Psg1) return PowerupUiMode.MergedTopHud;
            if (platform == GameRuntimePlatform.Android) return PowerupUiMode.ExternalController;
            return PowerupUiMode.GameOverlay;
        }

        public static bool IsPsg1SimulatorSignature(string operatingSystem, int width, int height)
        {
            bool android=!string.IsNullOrEmpty(operatingSystem)&&operatingSystem.IndexOf("android",StringComparison.OrdinalIgnoreCase)>=0;
            bool psg1Resolution=(width==1240&&height==1080)||(width==1080&&height==1240);
            return android&&psg1Resolution;
        }

#if UNITY_EDITOR
        private static bool IsPsg1Simulator()
        {
            return IsPsg1SimulatorSignature(SimulatedSystemInfo.operatingSystem,SimulatedScreen.width,SimulatedScreen.height);
        }
#endif

        static RuntimePlatformInfo()
        {
            DeviceIdentity = BuildDeviceIdentity();

#if UNITY_WEBGL && !UNITY_EDITOR
            detectedPlatform = GameRuntimePlatform.WebGl;
#elif UNITY_ANDROID && !UNITY_EDITOR
#if BATTLE_CITIES_PSG1
            detectedPlatform = GameRuntimePlatform.Psg1;
#else
            detectedPlatform = LooksLikePsg1(DeviceIdentity)
                ? GameRuntimePlatform.Psg1
                : GameRuntimePlatform.Android;
#endif
#elif UNITY_STANDALONE || UNITY_EDITOR
            detectedPlatform = GameRuntimePlatform.Desktop;
#else
            detectedPlatform = GameRuntimePlatform.Other;
#endif
        }

        private static bool LooksLikePsg1(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return false;
            string value = identity.ToLowerInvariant();
            return value.Contains("psg1") ||
                   value.Contains("ps01") ||
                   value.Contains("play solana") ||
                   value.Contains("playsolana") ||
                   value.Contains("echos");
        }

        private static string BuildDeviceIdentity()
        {
            string identity = SystemInfo.deviceModel + " | " +
                              SystemInfo.deviceName + " | " +
                              SystemInfo.operatingSystem;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    identity += " | " + build.GetStatic<string>("MANUFACTURER") +
                                " | " + build.GetStatic<string>("MODEL") +
                                " | " + build.GetStatic<string>("DEVICE") +
                                " | " + build.GetStatic<string>("PRODUCT") +
                                " | " + build.GetStatic<string>("FINGERPRINT");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read Android build identity: " + exception.Message);
            }
#endif
            return identity;
        }
    }
}
