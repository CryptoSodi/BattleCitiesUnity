using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
#if UNITY_ANDROID || UNITY_EDITOR
using Solana.Unity.SDK;
using Solana.Unity.Rpc.Models;
#endif
using UnityEngine;

namespace BattleCities.UI
{
    internal sealed class WalletAccount
    {
        internal string address, publicKeyBase64;
        internal byte[] PublicKey => Convert.FromBase64String(publicKeyBase64);
    }

    internal sealed class WalletAdapterException : Exception
    {
        internal string Code { get; }
        internal WalletAdapterException(string code, string message) : base(message) { Code = code; }
    }

    internal interface ISeekerGameWallet
    {
        Task<bool> IsAvailable(CancellationToken token);
        Task<WalletAccount> Account(CancellationToken token);
        Task<SeekerMessageProof> SignMessage(string message, CancellationToken token);
        Task<SeekerSignInProof> LoginWithSignIn(JObject input, CancellationToken token);
        Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token);
        Task<byte[]> SignAndSend(byte[] transaction, CancellationToken token);
        bool SupportsSignAndSend { get; }
        Task Disconnect();
    }

    internal sealed class SeekerMessageProof
    {
        internal string address;
        internal byte[] signature, payload;
    }

    internal sealed class SeekerSignInProof
    {
        internal string address, signedMessage, signature, signatureType;
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

    // All native Android devices use this provider; WebGL uses its existing wallet integration.
    internal static class SeekerGameWallet
    {
        internal static bool AllowMockWalletCompatibility => MainMenuApiClient.IsTestNetwork;
        internal static ISeekerGameWallet Client { get; private set; } = new SdkClient();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Older SDK versions could have left a plaintext authorization token.
            PlayerPrefs.DeleteKey("solana_sdk.mwa.auth_token");
            PlayerPrefs.DeleteKey("authToken");
            PlayerPrefs.Save();
#endif
            Client = new SdkClient();
        }
#if UNITY_EDITOR
        internal static void SetClientForChecks(ISeekerGameWallet value) => Client = value ?? new SdkClient();
        internal static IMwaAuthCache NewSessionCacheForChecks() => new SessionAuthCache();
#endif
        internal static void ValidateAccount(WalletAccount account, string expected = null)
        {
            if (account == null || account.PublicKey.Length != 32 ||
                CheckoutWalletSigner.Base58(account.PublicKey, 0, 32) != account.address ||
                (expected != null && expected != account.address))
                throw new SeekerCheckoutException("account-mismatch");
        }
#if UNITY_ANDROID || UNITY_EDITOR
        internal static void ValidateReauthorizedAccount(AuthorizationResult authorization, string expected)
        {
            byte[] key;
            try { key = authorization?.PublicKey; }
            catch (FormatException) { throw new SeekerCheckoutException("account-mismatch"); }
            if (key == null || key.Length != 32 || CheckoutWalletSigner.Base58(key, 0, key.Length) != expected)
                throw new SeekerCheckoutException("account-mismatch");
        }
#endif
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
        internal static async Task<byte[]> SendQuotedTransaction(byte[] transaction, string wallet, CancellationToken token, DateTimeOffset expiry, Action beforeSend)
        {
            ValidateAccount(await Client.Account(token), wallet);
            token.ThrowIfCancellationRequested();
            if (expiry <= DateTimeOffset.UtcNow) throw new SeekerCheckoutException("quote-expired");
            // Persist the recovery record before invoking a wallet that can broadcast.
            beforeSend();
            return await Client.SignAndSend(transaction, token);
        }
        internal static async Task<(byte[] submitted, byte[] signed)> CheckoutQuotedTransaction(
            byte[] transaction, string wallet, CancellationToken token, DateTimeOffset expiry, Action beforeSend, Action unsupported = null)
        {
            ValidateAccount(await Client.Account(token), wallet);
            token.ThrowIfCancellationRequested();
            if (expiry <= DateTimeOffset.UtcNow) throw new SeekerCheckoutException("quote-expired");
            if (Client.SupportsSignAndSend)
            {
                try { return (await SendQuotedTransaction(transaction, wallet, token, expiry, beforeSend), null); }
                catch (WalletAdapterException error) when (error.Code == "UNSUPPORTED_OPERATION" && unsupported != null)
                {
                    token.ThrowIfCancellationRequested();
                    unsupported();
                }
            }
            return (null, await SignQuotedTransaction(transaction, wallet, token, expiry));
        }
        internal static bool NeedsReconnect(Exception error)
        {
            if (error is SeekerCheckoutException checkout && checkout.Reason == "account-mismatch") return true;
            return error is WalletAdapterException sdk && (sdk.Code == "AUTHORIZATION_DECLINED" || sdk.Code == "NOT_CONNECTED"
                || sdk.Code == "NO_WALLET_FOUND" || sdk.Code == "ACCOUNT_CHANGED" || sdk.Code == "INVALID_RESPONSE");
        }
        internal static bool DefinitelyNotSubmitted(Exception error)
        {
            if (error is SeekerCheckoutException checkout && (checkout.Reason == "account-mismatch" || checkout.Reason == "quote-expired")) return true;
            return error is WalletAdapterException sdk && (sdk.Code == "AUTHORIZATION_DECLINED" || sdk.Code == "NOT_CONNECTED"
                || sdk.Code == "NO_WALLET_FOUND" || sdk.Code == "UNSUPPORTED_OPERATION");
        }
        internal static async Task ClearRejectedAuthorization(Exception error)
        {
            // Only invalidate a cached grant when authorization/account was rejected;
            // timeouts and cancellation can happen after a wallet has submitted.
            if (error is WalletAdapterException sdk && (sdk.Code == "AUTHORIZATION_DECLINED" || sdk.Code == "NOT_CONNECTED" || sdk.Code == "ACCOUNT_CHANGED" || sdk.Code == "INVALID_RESPONSE")
                || error is SeekerCheckoutException checkout && checkout.Reason == "account-mismatch")
                await DisconnectQuietly();
        }
        internal static async Task DisconnectQuietly()
        {
            try { await Client.Disconnect(); }
            catch { Debug.LogWarning("Seeker wallet authorization could not be cleared. Game sign-out is independent."); }
        }

#if UNITY_ANDROID || UNITY_EDITOR
        sealed class SdkClient : ISeekerGameWallet
        {
            SolanaWalletAdapter adapter;
            readonly SessionAuthCache authCache = new SessionAuthCache();
            public bool SupportsSignAndSend => true;
            void Initialize()
            {
                if (adapter != null) return;
                var options = new SolanaWalletAdapterOptions { solanaMobileWalletAdapterOptions = new SolanaMobileWalletAdapterOptions {
                    name = "Battle Cities", identityUri = "https://battlecities.com", iconUri = "/images/battle-cities-wallet-bc-20261015.png", keepConnectionAlive = true
                } };
                adapter = new SolanaWalletAdapter(options, MainMenuApiClient.IsTestNetwork ? RpcCluster.DevNet : RpcCluster.MainNet,
                    authCache: authCache, walletSelectionCache: RuntimePlatformInfo.IsPsg1 ? new Psg1WalletSelectionCache() : null);
            }
            public Task<bool> IsAvailable(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(true); }
            public async Task<WalletAccount> Account(CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Initialize();
                var account = adapter.Account ?? await adapter.Login();
                token.ThrowIfCancellationRequested();
                if (account?.PublicKey == null) throw new WalletAdapterException("NOT_CONNECTED", "No wallet account");
                return new WalletAccount { address = account.PublicKey.ToString(), publicKeyBase64 = Convert.ToBase64String(account.PublicKey.KeyBytes) };
            }
            public async Task<SeekerMessageProof> SignMessage(string message, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                byte[] payload;
                try { payload = await adapter.SignMessage(System.Text.Encoding.UTF8.GetBytes(message)); }
                catch (MwaRpcException error) when (error.Code == MwaErrorCodes.AuthorizationFailed)
                { throw new WalletAdapterException("AUTHORIZATION_DECLINED", "Wallet authorization rejected"); }
                token.ThrowIfCancellationRequested();
                if (payload == null || payload.Length < 64) throw new WalletAdapterException("INVALID_RESPONSE", "Invalid signed message");
                return new SeekerMessageProof { address = adapter.Account.PublicKey.ToString(), signature = payload.Skip(payload.Length - 64).ToArray(), payload = payload };
            }
            public async Task<SeekerSignInProof> LoginWithSignIn(JObject input, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Initialize();
                var payload = new SignInPayload {
                    Domain = (string)input["domain"], Statement = (string)input["statement"],
                    Uri = (string)input["uri"], Version = (string)input["version"],
                    Nonce = (string)input["nonce"], IssuedAt = (string)input["issuedAt"],
                    ExpirationTime = (string)input["expirationTime"]
                };
                var result = await adapter.LoginWithSignIn(payload);
                token.ThrowIfCancellationRequested();
                var address = result.account?.PublicKey?.ToString();
                if (string.IsNullOrEmpty(address) || result.signInResult == null || result.signInResult.Address != address)
                    throw new WalletAdapterException("INVALID_RESPONSE", "Wallet returned an invalid sign-in account");
                return new SeekerSignInProof { address = address, signedMessage = result.signInResult.SignedMessage,
                    signature = result.signInResult.Signature, signatureType = result.signInResult.SignatureType };
            }
            public async Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token)
            {
                return await CheckoutInWallet(transaction, false, token);
            }
            public async Task<byte[]> SignAndSend(byte[] transaction, CancellationToken token)
            {
                return await CheckoutInWallet(transaction, true, token);
            }
            async Task<byte[]> CheckoutInWallet(byte[] transaction, bool send, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var account = adapter?.Account?.PublicKey?.ToString();
                var cached = await authCache.GetForAccount(account);
                if (string.IsNullOrEmpty(cached)) throw new WalletAdapterException("NOT_CONNECTED", "Wallet authorization is unavailable");
                string package = RuntimePlatformInfo.IsPsg1 ? "ag.jup.jupiter.android" : await new PlayerPrefsMwaWalletSelectionCache().GetSelectedWalletPackage();
                string cluster = MainMenuApiClient.IsTestNetwork ? "devnet" : "mainnet-beta";
                string chain = MainMenuApiClient.IsTestNetwork ? "solana:devnet" : "solana:mainnet";
                string cacheNetwork = MainMenuApiClient.IsTestNetwork ? "devnet" : "mainnet";
                AuthorizationResult authorization = null;
                SignedResult signed = null;
                SignAndSendResult submitted = null;
                bool checkedAccount = false;
                try
                {
                    using (var scenario = new LocalAssociationScenario(package))
                    {
                        var actions = new List<Func<IAdapterOperations, Task>> {
                            async client => {
                                authorization = await client.Reauthorize(new Uri("https://battlecities.com"),
                                    new Uri("/images/battle-cities-wallet-bc-20261015.png", UriKind.Relative),
                                    "Battle Cities", cached, cluster, chain);
                                ValidateReauthorizedAccount(authorization, account);
                                checkedAccount = true;
                            },
                            async client => {
                                if (send) submitted = await client.SignAndSendTransactions(new[] { transaction },
                                    new SignAndSendTransactionsOptions { Commitment = "confirmed", SkipPreflight = false, MaxRetries = 0 });
                                else signed = await client.SignTransactions(new[] { transaction });
                            }
                        };
                        var result = await scenario.StartAndExecute(actions, token);
                        if (!result.WasSuccessful)
                        {
                            if (result.Error?.Code == MwaErrorCodes.AuthorizationFailed)
                                throw new WalletAdapterException("AUTHORIZATION_DECLINED", "Wallet authorization rejected");
                            if (send && result.Error?.Code == MwaErrorCodes.MethodNotFound && checkedAccount)
                                throw new WalletAdapterException("UNSUPPORTED_OPERATION", "Wallet does not support sign and send");
                            throw new WalletAdapterException("REQUEST_DECLINED", "Wallet request did not complete");
                        }
                    }
                    var bytes = send ? submitted?.SignatureBytes : signed?.SignedPayloadsBytes;
                    if (bytes == null || bytes.Count != 1 || bytes[0] == null || (send && bytes[0].Length != 64))
                        throw new WalletAdapterException("INVALID_RESPONSE", "Wallet returned an invalid response");
                    return bytes[0];
                }
                finally
                {
                    // A wallet may rotate its token even when the player cancels signing.
                    if (checkedAccount && !string.IsNullOrEmpty(authorization?.AuthToken))
                        await authCache.SetForAccount(authorization.AuthToken, account, cacheNetwork);
                }
            }
            public async Task Disconnect() { if (adapter != null) await adapter.DisconnectWallet(); else await authCache.Clear(); }

            // The PSG1 sign-in button promises Jupiter; target it through the SDK's MWA adapter.
            sealed class Psg1WalletSelectionCache : IMwaWalletSelectionCache
            {
                public Task<string> GetSelectedWalletPackage() => Task.FromResult("ag.jup.jupiter.android");
                public Task SetSelectedWalletPackage(string packageName) => Task.CompletedTask;
                public Task ClearSelectedWalletPackage() => Task.CompletedTask;
            }
        }

        // MWA authorization is valid only for this running app process.
        sealed class SessionAuthCache : IMwaAuthCache
        {
            const string PublicKeyPreference = "solana_sdk.mwa.public_key";
            string memoryValue;
            static string Network => MainMenuApiClient.IsTestNetwork ? "devnet" : "mainnet";
            public Task<string> Get() => GetForAccount(PlayerPrefs.GetString(PublicKeyPreference, ""));
            internal Task<string> GetForAccount(string wallet)
            {
                if (string.IsNullOrEmpty(wallet) || PlayerPrefs.GetString(PublicKeyPreference, "") != wallet) return Task.FromResult<string>(null);
                try
                {
                    var saved = JObject.Parse(memoryValue ?? "");
                    return Task.FromResult((string)saved["wallet"] == wallet && (string)saved["network"] == Network ? (string)saved["token"] : null);
                }
                catch (Exception) { return Task.FromResult<string>(null); }
            }
            public Task Set(string token) => SetForAccount(token, PlayerPrefs.GetString(PublicKeyPreference, ""), Network);
            internal Task SetForAccount(string token, string wallet, string network)
            {
                if (string.IsNullOrEmpty(wallet) || string.IsNullOrEmpty(token) || network != Network ||
                    PlayerPrefs.GetString(PublicKeyPreference, "") != wallet) return Task.CompletedTask;
                memoryValue = new JObject { ["wallet"] = wallet, ["network"] = network, ["token"] = token }.ToString();
                return Task.CompletedTask;
            }
            public Task Clear()
            {
                memoryValue = null;
                return Task.CompletedTask;
            }
        }
#else
        sealed class SdkClient : ISeekerGameWallet
        {
            public bool SupportsSignAndSend => false;
            public Task<bool> IsAvailable(CancellationToken token) => Task.FromResult(false);
            public Task<WalletAccount> Account(CancellationToken token) => throw new PlatformNotSupportedException();
            public Task<SeekerMessageProof> SignMessage(string message, CancellationToken token) => throw new PlatformNotSupportedException();
            public Task<SeekerSignInProof> LoginWithSignIn(JObject input, CancellationToken token) => throw new PlatformNotSupportedException();
            public Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token) => throw new PlatformNotSupportedException();
            public Task<byte[]> SignAndSend(byte[] transaction, CancellationToken token) => throw new PlatformNotSupportedException();
            public Task Disconnect() => Task.CompletedTask;
        }
#endif
    }
}
