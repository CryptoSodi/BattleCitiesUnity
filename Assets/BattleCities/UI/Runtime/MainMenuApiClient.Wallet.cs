using System;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuApiClient
    {
        private string walletAttemptId;
        private double walletDeadline;
        private MobileWalletLogin mobileWallet;
        public bool IsWalletLoginPending => !string.IsNullOrEmpty(walletAttemptId);
        private string ApiOrigin => new Uri(baseUrl).GetLeftPart(UriPartial.Authority);

        private void Update()
        {
            if (IsWalletLoginPending && Time.realtimeSinceStartupAsDouble >= walletDeadline)
            {
                CancelWalletLogin(false);
                NotifyStatus("WALLET LOGIN FAILED", "Wallet sign-in timed out. Please try again.");
            }
        }

        public void CancelWalletLogin() => CancelWalletLogin(true);

        private void CancelWalletLogin(bool notify)
        {
            if (!IsWalletLoginPending) return;
            var attempt = walletAttemptId;
            walletAttemptId = null;
            BattleCitiesWalletBridge.Cancel(attempt);
            if (mobileWallet) mobileWallet.Cancel();
            if (notify) NotifyStatus("WALLET LOGIN FAILED", "Wallet sign-in cancelled.");
        }
    }
}
