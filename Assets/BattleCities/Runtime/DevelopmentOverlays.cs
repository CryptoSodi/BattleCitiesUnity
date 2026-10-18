using System;
using UnityEngine;

namespace BattleCities
{
    /// <summary>Diagnostics are available in the editor and on the dedicated test site.</summary>
    public static class DevelopmentOverlays
    {
        public static bool Visible
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_WEBGL
                return IsTestSite(Application.absoluteURL);
#else
                return false;
#endif
            }
        }

        // Magic uses the test API too, but must not display development controls.
        public static bool IsTestSite(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
            && string.Equals(uri.Host, "test.battlecities.com", StringComparison.OrdinalIgnoreCase);
    }
}
