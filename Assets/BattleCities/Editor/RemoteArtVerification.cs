using UnityEngine;

namespace BattleCities.Editor
{
    public static class RemoteArtVerification
    {
        // Editor-only verification; does not build or launch a player.
        public static void Run()
        {
            RemoteSceneCaptureChecks.Run();
            RemoteModelChecks.Run();
            Debug.Log("REMOTE ART VERIFICATION PASSED");
        }
    }
}
