using UnityEngine;

namespace BattleCities
{
    internal static class BattleVisualLifetime
    {
        // Terrain builders also run in the editor's non-persistent map preview.
        public static void Release(Object item)
        {
            if (!item) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(item); return; }
#endif
            Object.Destroy(item);
        }
    }
}
