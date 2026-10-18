using System;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class DevelopmentOverlayChecks
    {
        public static void Run()
        {
            int checks = 0;
            foreach (var url in new[] { "https://test.battlecities.com", "https://test.battlecities.com/game?mode=coop", "https://TEST.battlecities.com:443/" })
            {
                if (!DevelopmentOverlays.IsTestSite(url)) throw new Exception("Test overlays missing: " + url);
                checks++;
            }
            foreach (var url in new[] { null, "", "https://magic.battlecities.com", "https://play.battlecities.com", "https://battlecities.com", "https://test.battlecities.com.evil.org", "https://play.battlecities.com/?test.battlecities.com", "http://test.battlecities.com", "https://test.battlecities.com:444", "https://user@test.battlecities.com" })
            {
                if (DevelopmentOverlays.IsTestSite(url)) throw new Exception("Overlays leaked to: " + url);
                checks++;
            }
            Debug.Log("Development overlay checks passed: " + checks);
        }
    }
}
