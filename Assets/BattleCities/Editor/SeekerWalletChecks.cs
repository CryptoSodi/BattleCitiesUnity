using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BattleCities.UI;
using Community.SeekerConnect;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class SeekerWalletChecks
    {
        static int checks;
        static readonly byte[] PublicKey = Enumerable.Repeat((byte)3, 32).ToArray();
        static readonly string Address = CheckoutWalletSigner.Base58(PublicKey, 0, 32);
        static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
        static async Task Reject(Func<Task> action, string name)
        { bool rejected = false; try { await action(); } catch { rejected = true; } Check(rejected, name); }
        public static void Run()
        {
            try { RunAsync().GetAwaiter().GetResult(); Debug.Log("SEEKER WALLET CHECKS PASSED: " + checks); }
            finally { SeekerGameWallet.SetClientForChecks(null); }
        }
        public static void CompileAndroid()
        {
            Run();
            SavedSessionChecks.Run();
            var settings = new UnityEditor.Build.Player.ScriptCompilationSettings {
                group = UnityEditor.BuildTargetGroup.Android, target = UnityEditor.BuildTarget.Android
            };
            var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings,
                "Builds/Automation/seeker-wallet-player-scripts");
            if (result.assemblies == null || result.assemblies.Count == 0) throw new Exception("Android player scripts did not compile");
            Debug.Log("SEEKER ANDROID PLAYER SCRIPTS COMPILED: " + result.assemblies.Count);
        }
        static async Task RunAsync()
        {
            checks = 0;
            Check(SeekerGameWallet.SupportsWallet(WalletAvailability.Certified), "certified wallet supported");
            Check(SeekerGameWallet.SupportsWallet(WalletAvailability.Other), "ordinary Android MWA wallet supported");
            Check(!SeekerGameWallet.SupportsWallet(WalletAvailability.None), "missing wallet not claimed available");
            Check(!SeekerGameWallet.SupportsWallet(WalletAvailability.Unknown), "unknown availability not claimed supported");
            var production = new SeekerConnectConfig { appName = "Battle Cities", identityUri = "https://battlecities.com",
                chain = "solana:mainnet", allowGenericMwa = true, allowMwaTesting = false };
            production.Validate(); Check(!production.allowMwaTesting, "production generic wallets do not enable mock compatibility");
            production.allowMwaTesting = true;
            await Reject(() => { production.Validate(); return Task.CompletedTask; }, "mock compatibility remains forbidden on mainnet");
            var restored = new FakeWallet(); SeekerGameWallet.SetClientForChecks(restored);
            Check(await SeekerGameWallet.RestoreQuietly(Address), "matching restored wallet enables purchase connection");
            Check(restored.restoreCalls == 1 && restored.accountCalls == 0 && restored.transactionCalls == 0,
                "quiet restoration never opens a new wallet connection or signs a transaction");
            Check(!await SeekerGameWallet.RestoreQuietly("other"), "different restored account cannot enable purchases");
            restored.missingRestore = true;
            Check(!await SeekerGameWallet.RestoreQuietly(Address), "absent wallet authorization requires reconnect");
            restored.missingRestore = false; restored.restoreFailure = true;
            Check(!await SeekerGameWallet.RestoreQuietly(Address), "revoked authorization cannot enable purchases");
            restored.restoreFailure = false; restored.available = false;
            int restoresBefore = restored.restoreCalls;
            Check(!await SeekerGameWallet.RestoreQuietly(Address) && restored.restoreCalls == restoresBefore,
                "unavailable wallet never attempts restoration");
            var wallet = new FakeWallet(); var api = new FakeApi();
            var result = await SeekerWalletLoginFlow.Authenticate(wallet, api, "attempt", CancellationToken.None);
            Check((bool)result["ok"] && (string)result["attemptId"] == "attempt", "verified login result");
            Check(api.calls == "PUT POST GET " && api.posted["message"].ToString() == api.message, "exact server challenge and endpoint order");
            Check((string)api.posted["signature"] == Convert.ToBase64String(wallet.proof.signature), "detached signature reaches verification");
            Check((string)result["sessionCookie"] == api.Cookie && (long)result["sessionExpiresAt"] == api.ExpiresAt, "actual server session preserved");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { nonce = "bad" }, "x", CancellationToken.None), "malformed nonce rejected");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { message = "" }, "x", CancellationToken.None), "empty challenge rejected");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { message = new string('x', 4097) }, "x", CancellationToken.None), "oversized challenge rejected");
            api = new FakeApi(); wallet = new FakeWallet { changedProof = true };
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, api, "x", CancellationToken.None), "altered signed message rejected");
            Check(api.calls == "PUT ", "invalid proof never reaches server verification");
            api = new FakeApi(); wallet = new FakeWallet { switchAccount = true };
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, api, "x", CancellationToken.None), "wallet switching rejected");
            Check(api.calls == "PUT ", "account switch never submits proof");
            wallet = new FakeWallet();
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { authenticated = false }, "x", CancellationToken.None), "wallet authorization is not server authentication");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { cookie = "" }, "x", CancellationToken.None), "missing API cookie rejected");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { expiry = 1 }, "x", CancellationToken.None), "expired API cookie rejected");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { playerAddress = "other" }, "x", CancellationToken.None), "different backend player rejected");
            using (var cancellation = new CancellationTokenSource()) {
                api = new FakeApi(); wallet = new FakeWallet { cancel = cancellation };
                await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, api, "x", cancellation.Token), "cancelled signing rejected");
                Check(api.calls == "PUT ", "cancelled attempt never posts proof");
            }
            var transaction = new byte[100]; transaction[0] = 1; transaction[77] = 4;
            wallet = new FakeWallet(); SeekerGameWallet.SetClientForChecks(wallet);
            var signed = await SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None);
            Check(signed.Skip(65).SequenceEqual(transaction.Skip(65)) && signed[1] != 0, "quoted message preserved");
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, "other", CancellationToken.None), "checkout signer must match login");
            Check(wallet.transactionCalls == 1, "wrong account never receives transaction signing request");
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None, DateTimeOffset.UtcNow.AddSeconds(-1)), "expired quote rejected");
            Check(wallet.transactionCalls == 1, "expired quote never requests signing");
            wallet.changeTransaction = true;
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None), "wallet-modified transaction rejected");
            await Reject(() => { new SeekerLoginHttp("http://api.battlecities.com"); return Task.CompletedTask; }, "HTTP login origin rejected");
            await Reject(() => { new SeekerLoginHttp("https://api.battlecities.com.evil.example"); return Task.CompletedTask; }, "lookalike login origin rejected");
        }
        sealed class FakeWallet : ISeekerGameWallet
        {
            internal bool changedProof, switchAccount, changeTransaction;
            internal bool available = true, missingRestore, restoreFailure;
            internal CancellationTokenSource cancel;
            internal SeekerMessageProof proof;
            internal int transactionCalls, restoreCalls, accountCalls;
            bool switched;
            public Task<bool> IsAvailable(CancellationToken token) => Task.FromResult(available);
            WalletAccount CurrentAccount() => new WalletAccount {
                address = switched ? "other" : Address, publicKeyBase64 = Convert.ToBase64String(PublicKey) };
            public Task<WalletAccount> Account(CancellationToken token) { accountCalls++; return Task.FromResult(CurrentAccount()); }
            public Task<SeekerMessageProof> SignMessage(string message, CancellationToken token)
            {
                proof = new SeekerMessageProof { address = Address, signature = Enumerable.Repeat((byte)7, 64).ToArray(),
                    payload = Encoding.UTF8.GetBytes(changedProof ? "altered" : message).Concat(Enumerable.Repeat((byte)7, 64)).ToArray() };
                switched = switchAccount; cancel?.Cancel(); return Task.FromResult(proof);
            }
            public Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token)
            { transactionCalls++; var signed = (byte[])transaction.Clone(); signed[1] = 7; if (changeTransaction) signed[77]++; return Task.FromResult(signed); }
            public Task Disconnect() => Task.CompletedTask;
            public Task<WalletAccount> Restore()
            {
                restoreCalls++;
                if (restoreFailure) throw new InvalidOperationException("Authorization revoked");
                return Task.FromResult(missingRestore ? null : CurrentAccount());
            }
        }
        sealed class FakeApi : ISeekerLoginApi
        {
            internal string nonce = new string('a', 32), message = "Exact server challenge\nUnicode: café", calls = "", cookie = "battlecity_session=sess-test";
            internal string playerAddress = Address;
            internal long expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
            internal bool authenticated = true;
            internal JObject posted;
            public string Cookie => cookie;
            public string Origin => MainMenuApiClient.LiveApiBaseUrl;
            public long ExpiresAt => expiry;
            public Task<JObject> Request(string method, string path, JObject body, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); calls += method + " ";
                if (method == "PUT") return Task.FromResult(new JObject { { "nonce", nonce }, { "message", message } });
                if (method == "POST") posted = body;
                return Task.FromResult(new JObject { { "authenticated", authenticated }, { "provider", "wallet" },
                    { "player", new JObject { { "provider", "wallet" }, { "walletAddress", playerAddress } } } });
            }
        }
    }
}
