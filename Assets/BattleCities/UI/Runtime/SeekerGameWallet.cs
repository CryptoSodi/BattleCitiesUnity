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
        Task<WalletSignature> SignAndSend(byte[] transaction, CancellationToken token);
        Task Disconnect();
    }

    internal sealed class SeekerMessageProof
    {
        internal string address;
        internal byte[] signature, payload;
    }

    internal sealed class SeekerCheckoutException : InvalidOperationException
    {
        internal string Reason { get; }
        internal string Detail { get; }
        internal SeekerCheckoutException(string reason, string detail = null) : base(reason == "account-mismatch"
            ? "Select the wallet used to sign in to Battle Cities."
            : reason == "quote-expired" ? "The checkout price expired. Request a fresh price."
            : "The wallet changed the quoted transaction. Request a fresh price and try again.") { Reason = reason; Detail = detail; }
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
                throw new SeekerCheckoutException("account-mismatch");
        }
        internal static async Task<byte[]> SignQuotedTransaction(byte[] transaction, string wallet, CancellationToken token, DateTimeOffset? expiry = null)
        {
            var original = (byte[])transaction.Clone();
            ValidateAccount(await Client.Account(token), wallet);
            if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
                throw new SeekerCheckoutException("quote-expired");
            var signed = await Client.SignTransaction(transaction, token);
            token.ThrowIfCancellationRequested();
            if (expiry.HasValue && expiry.Value <= DateTimeOffset.UtcNow)
                throw new SeekerCheckoutException("quote-expired");
            ValidateAccount(await Client.Account(token), wallet);
            if (!ApprovedCheckoutTransaction.Matches(original, signed, out var difference))
                throw new SeekerCheckoutException("transaction-changed", difference);
            return signed;
        }
        internal static async Task<WalletSignature> SendQuotedTransaction(byte[] transaction, string wallet, CancellationToken token, DateTimeOffset expiry, Action beforeSend)
        {
            ValidateAccount(await Client.Account(token), wallet);
            token.ThrowIfCancellationRequested();
            if (expiry <= DateTimeOffset.UtcNow) throw new SeekerCheckoutException("quote-expired");
            // Persist the recovery record before invoking a wallet that can broadcast.
            beforeSend();
            return await Client.SignAndSend(transaction, token);
        }
        internal static bool NeedsReconnect(Exception error)
        {
            if (error is SeekerCheckoutException checkout && checkout.Reason == "account-mismatch") return true;
            return error is SeekerConnectException sdk && (sdk.Code == "AUTHORIZATION_DECLINED" || sdk.Code == "NOT_CONNECTED"
                || sdk.Code == "NO_WALLET_FOUND" || sdk.Code == "ACCOUNT_CHANGED" || sdk.Code == "INVALID_RESPONSE");
        }
        internal static bool DefinitelyNotSubmitted(Exception error)
        {
            if (error is SeekerCheckoutException checkout && (checkout.Reason == "account-mismatch" || checkout.Reason == "quote-expired")) return true;
            return error is SeekerConnectException sdk && (sdk.Code == "AUTHORIZATION_DECLINED" || sdk.Code == "NOT_CONNECTED"
                || sdk.Code == "NO_WALLET_FOUND" || sdk.Code == "UNSUPPORTED_OPERATION");
        }
        internal static async Task ClearRejectedAuthorization(Exception error)
        {
            // Only invalidate a cached grant when authorization/account was rejected;
            // timeouts and cancellation can happen after a wallet has submitted.
            if (error is SeekerConnectException sdk && (sdk.Code == "AUTHORIZATION_DECLINED" || sdk.Code == "NOT_CONNECTED" || sdk.Code == "ACCOUNT_CHANGED" || sdk.Code == "INVALID_RESPONSE")
                || error is SeekerCheckoutException checkout && checkout.Reason == "account-mismatch")
                await DisconnectQuietly();
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
            public Task<WalletSignature> SignAndSend(byte[] transaction, CancellationToken token)
                => SeekerConnectManager.SignAndSendTransactionAsync(transaction, token);
            public async Task Disconnect()
            {
                if (initialization != null) { await initialization; await SeekerConnectManager.DisconnectAsync(); }
            }
        }
    }
}
