using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        ReferralScreen referrals;
        bool IsReferralOpen => referrals && referrals.IsOpen;
        void EnsureReferrals()
        {
            if (!mainFrame || !apiClient || !theme) return;
            if (!referrals) referrals = GetComponent<ReferralScreen>();
            if (!referrals) referrals = gameObject.AddComponent<ReferralScreen>();
            if (!referrals.IsConfigured) referrals.Configure(theme, apiClient, mainFrame);
        }
        public void OpenReferralEntry() { EnsureReferrals(); if (referrals) referrals.OpenEntry(); }
        public void OpenReferralShare() { EnsureReferrals(); if (referrals) referrals.OpenShare(); }
    }
}
