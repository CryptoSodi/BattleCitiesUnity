using UnityEngine;

namespace BattleCities
{
    internal static class AndroidFrameRate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android otherwise defaults to 30 FPS. Simulation/replay ticks remain fixed.
            Application.targetFrameRate = 60;
#endif
        }
    }
}
