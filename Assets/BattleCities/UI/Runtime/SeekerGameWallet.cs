using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Community.SeekerConnect;
using UnityEngine;

namespace BattleCities.UI
{
    internal interface ISeekerGameWallet
    {
        Task<bool> IsAvailable(CancellationToken token);
        Task<WalletAccount> Account(CancellationToken token);
        Task<SeekerMessageProof> SignMessage(string message, CancellationToken token);
        Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token);
        Task<WalletAccount> Restore();
        Task Disconnect();
    }

    internal sealed class SeekerMessageProof
    {
        internal string address;
        internal byte[] signature, payload;
    }

    // Only native Android routes into this provider. Web Phantom and PSG1 Jupiter stay independent.
    internal static class SeekerGameWallet
    {
        internal static bool AllowMockWalletCompatibility => MainMenuApiClient.IsTestNetwork;
        internal static ISeekerGameWallet Client { get; private set; } = new SdkClient();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Client = new SdkClient();
        internal static bool SupportsWallet(WalletAvailability availability)
            => availability == WalletAvailability.Certified || availability == WalletAvailability.Other;
#if UNITY_EDITOR
        internal static void SetClientForChecks(ISeekerGameWallet value) => Client = value ?? new SdkClient();
#endif
        internal static void ValidateAccount(WalletAccount account, string expected = null)
        {
            if (account == null || account.PublicKey.Length != 32 ||
                CheckoutWalletSigner.Base58(account.PublicKey, 0, 32) != account.address ||
                (expected != null && expected != account.address))
                throw new InvalidOperationException("Select the wallet used to sign in to Battle Cities.");
        }
        internal static async Task<byte[]> SignQuotedTransaction(byte[] transaction, string wallet, CancellationToken token, DateTimeOffset? expiry = null)
        {
            var original = (byte[])transaction.Clone();
            ValidateAccount(await Client.Account(token), wallet);
            if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("The checkout price expired. Request a fresh price before signing.");
            var signed = await Client.SignTransaction(transaction, token);
            token.ThrowIfCancellationRequested();
            if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("The checkout price expired. Request a fresh price before retrying.");
            ValidateAccount(await Client.Account(token), wallet);
            if (signed == null || signed.Length != original.Length || signed[0] != 1 ||
                !signed.Skip(65).SequenceEqual(original.Skip(65)))
                throw new InvalidOperationException("The wallet changed the quoted transaction. Request a fresh price and try again.");
            return signed;
        }
        internal static async Task<bool> RestoreQuietly(string expectedWallet)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expectedWallet) || !await Client.IsAvailable(CancellationToken.None)) return false;
                var account = await Client.Restore();
                ValidateAccount(account, expectedWallet);
                return true;
            }
            catch { return false; /* A missing/mismatched authorization cannot enable purchases. */ }
        }
        internal static async Task DisconnectQuietly()
        {
            try { await Client.Disconnect(); }
            catch { Debug.LogWarning("Seeker wallet authorization could not be cleared. Game sign-out is independent."); }
        }

        sealed class SdkClient : ISeekerGameWallet
        {
            Task initialization;
            async Task Initialize()
            {
                if (initialization == null)
                    initialization = SeekerConnectManager.InitializeAsync(new SeekerConnectConfig {
                        appName = "Battle Cities", identityUri = "https://battlecities.com",
                        iconPath = "images/battle-cities-wallet-bc-20261015.png",
                        chain = MainMenuApiClient.WalletChain, allowGenericMwa = true,
                        allowMwaTesting = AllowMockWalletCompatibility, timeoutMs = 120000
                    });
                try { await initialization; }
                catch { initialization = null; throw; }
            }
            public async Task<bool> IsAvailable(CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); await Initialize(); token.ThrowIfCancellationRequested();
                var availability = await SeekerConnectManager.GetWalletAvailabilityAsync();
                Debug.Log("[BattleCitiesWallet] availability=" + availability + " chain=" + MainMenuApiClient.WalletChain);
                return SupportsWallet(availability);
            }
            public Task<WalletAccount> Account(CancellationToken token) => GetAccount(token, true);
            public Task<WalletAccount> Restore() => GetAccount(CancellationToken.None, false);
            internal async Task<WalletAccount> GetAccount(CancellationToken token, bool connect)
            {
                await Initialize(); token.ThrowIfCancellationRequested();
                var session = SeekerConnectManager.Session ?? await SeekerConnectManager.RestoreSessionAsync(token);
                if (session == null && connect) session = await SeekerConnectManager.ConnectAsync(token);
                if (session != null && session.chain != MainMenuApiClient.WalletChain) throw new InvalidOperationException("The wallet connected to the wrong network.");
                return session?.Account;
            }
            public async Task<SeekerMessageProof> SignMessage(string message, CancellationToken token)
            {
                var proof = await SeekerConnectManager.SignMessageAsync(message, token);
                return new SeekerMessageProof { address = proof.Address, signature = proof.Signature, payload = proof.SignedPayload };
            }
            public Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token)
                => SeekerConnectManager.SignTransactionAsync(transaction, token);
            public async Task Disconnect()
            {
                if (initialization != null) { await initialization; await SeekerConnectManager.DisconnectAsync(); }
            }
        }
    }
}
