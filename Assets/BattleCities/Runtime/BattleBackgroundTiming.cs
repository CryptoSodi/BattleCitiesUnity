using System;

namespace BattleCities
{
    public static class BattleBackgroundTiming
    {
        // Fixed simulation steps remain unchanged; only presentation delta is clamped.
        public const int MaxStepsPerFrame=240;
        public static float Elapsed(ref double previous,double now)
        {
            float elapsed=(float)Math.Max(0,now-previous);previous=now;return elapsed;
        }
    }
}
