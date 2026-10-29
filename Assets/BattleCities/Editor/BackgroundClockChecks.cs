using System;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class BackgroundClockChecks
    {
        public static void Run()
        {
            double previous=100;
            void Check(bool ok,string message){if(!ok)throw new Exception("BACKGROUND CLOCK: "+message);}
            Check(Math.Abs(BattleBackgroundTiming.Elapsed(ref previous,101)-1)<.0001f,"throttled frame retains a full second instead of 0.1 seconds");
            Check(Math.Abs(BattleBackgroundTiming.Elapsed(ref previous,101.016)-.016)<.0001f,"normal frames keep normal cadence");
            Check(Math.Abs(BattleBackgroundTiming.Elapsed(ref previous,161.016)-60)<.0001f,"suspended callbacks retain elapsed time for bounded catch-up");
            Check(BattleBackgroundTiming.Elapsed(ref previous,160)==0,"clock discontinuity cannot reverse simulation");
            Check(BattleBackgroundTiming.MaxStepsPerFrame==240,"catch-up budget bounds one frame of work");
            BattleControlChecks.Run();
            Debug.Log("BACKGROUND CLOCK PASS: throttled frames, normal frames, suspension gap, nonnegative clock and bounded catch-up; focus input cancellation checks passed.");
        }
    }
}
