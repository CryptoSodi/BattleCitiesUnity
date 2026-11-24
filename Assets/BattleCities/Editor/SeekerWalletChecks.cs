using System;
using System.Linq;
using System.IO;
using System.Reflection;
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
        static JObject ValidateBridgeResponse(byte[] quoted, byte[] signed)
        {
            var go = new GameObject("Wallet checkout bridge fixture");
            go.SetActive(false);
            try
            {
                var signer = go.AddComponent<CheckoutWalletSigner>();
                var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                JObject result = null;
                typeof(CheckoutWalletSigner).GetField("attempt", fields).SetValue(signer, "fixture");
                typeof(CheckoutWalletSigner).GetField("quotedTransaction", fields).SetValue(signer, quoted);
                typeof(CheckoutWalletSigner).GetField("deadline", fields).SetValue(signer, Time.realtimeSinceStartupAsDouble + 60);
                typeof(CheckoutWalletSigner).GetField("completed", fields).SetValue(signer, (Action<JObject>)(value => result = value));
                signer.OnWalletSigned(new JObject { ["ok"] = true, ["attemptId"] = "fixture", ["signedTransaction"] = Convert.ToBase64String(signed) }.ToString());
                typeof(CheckoutWalletSigner).GetMethod("Update", fields).Invoke(signer, null);
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
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
            var revoked = new SeekerConnectException("AUTHORIZATION_DECLINED", "");
            Check(SeekerGameWallet.NeedsReconnect(revoked) && SeekerGameWallet.DefinitelyNotSubmitted(revoked), "revoked grant requires reconnect without submitting");
            await SeekerGameWallet.ClearRejectedAuthorization(revoked);
            Check(restored.disconnectCalls == 1, "revoked cached authorization is discarded");
            var missing = new SeekerConnectException("NOT_CONNECTED", "");
            Check(SeekerGameWallet.NeedsReconnect(missing) && SeekerGameWallet.DefinitelyNotSubmitted(missing), "missing grant requires reconnect without submitting");
            await SeekerGameWallet.ClearRejectedAuthorization(missing);
            Check(restored.disconnectCalls == 2, "missing authorization clears cached grant");
            var otherWallet = new SeekerCheckoutException("account-mismatch");
            Check(SeekerGameWallet.NeedsReconnect(otherWallet) && SeekerGameWallet.DefinitelyNotSubmitted(otherWallet), "different account requires reconnect");
            await SeekerGameWallet.ClearRejectedAuthorization(otherWallet);
            Check(restored.disconnectCalls == 3, "different account clears cached grant");
            foreach (var code in new[] { "TIMEOUT", "CANCELLED", "SESSION_CLOSED", "REQUEST_DECLINED" })
            {
                var ambiguous = new SeekerConnectException(code, "");
                Check(!SeekerGameWallet.DefinitelyNotSubmitted(ambiguous), code + " retains any pending payment");
                await SeekerGameWallet.ClearRejectedAuthorization(ambiguous);
            }
            Check(restored.disconnectCalls == 3, "uncertain wallet outcomes do not clear authorization");
            var invalidResponse = new SeekerConnectException("INVALID_RESPONSE", "");
            Check(SeekerGameWallet.NeedsReconnect(invalidResponse) && !SeekerGameWallet.DefinitelyNotSubmitted(invalidResponse), "invalid wallet response reconnects while preserving uncertain payments");
            await SeekerGameWallet.ClearRejectedAuthorization(invalidResponse);
            Check(restored.disconnectCalls == 4, "invalid wallet response clears cached grant");
            var wallet = new FakeWallet(); var api = new FakeApi();
            var fresh = new FakeWallet { requireDisconnect = true };
            var freshResult = await SeekerWalletLoginFlow.AuthenticateFresh(fresh, new FakeApi(), "fresh", CancellationToken.None);
            Check((bool)freshResult["ok"] && fresh.disconnectCalls == 1 && fresh.accountCalls > 0, "explicit reconnect clears cached grant before requesting account");
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
            var fixtures = JObject.Parse(File.ReadAllText("Tools/Tests/quoted-checkout-fixtures.json"));
            byte[] Fixture(string name) => Convert.FromBase64String((string)fixtures[name]);
            var transaction = Fixture("original");
            foreach (var name in new[] { "unchanged", "allowed" })
                Check(ApprovedCheckoutTransaction.Matches(transaction, Fixture(name)), "quoted transaction accepts " + name);
            foreach (var name in new[] { "changedAmount", "changedRecipient", "unsafeAssertion", "writableAccount", "extraInstruction", "changedBlockhash", "tooManyAssertions" })
                Check(!ApprovedCheckoutTransaction.Matches(transaction, Fixture(name)), "quoted transaction rejects " + name);
            Check(!ApprovedCheckoutTransaction.Matches(transaction, new byte[] {1, 2}), "truncated wallet response rejected");
            foreach (var name in new[] { "unchanged", "allowed" })
            {
                var response = ValidateBridgeResponse(transaction, Fixture(name));
                Check((bool?)response?["ok"] == true && !string.IsNullOrEmpty((string)response["signature"]), "Android bridge accepts validated " + name);
            }
            foreach (var name in new[] { "changedAmount", "changedRecipient", "unsafeAssertion", "writableAccount", "extraInstruction", "changedBlockhash", "tooManyAssertions" })
            {
                var response = ValidateBridgeResponse(transaction, Fixture(name));
                Check((bool?)response?["ok"] == false && ((string)response["error"]).Contains("No payment was submitted"), "Android bridge rejects " + name);
            }
            Check((bool?)ValidateBridgeResponse(transaction, transaction)?["ok"] == false, "Android bridge rejects an unsigned transaction");
            wallet = new FakeWallet(); SeekerGameWallet.SetClientForChecks(wallet);
            var signed = await SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None);
            Check(signed.Skip(65).SequenceEqual(transaction.Skip(65)) && signed[1] != 0, "quoted message preserved");
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, "other", CancellationToken.None), "checkout signer must match login");
            Check(wallet.transactionCalls == 1, "wrong account never receives transaction signing request");
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None, DateTimeOffset.UtcNow.AddSeconds(-1)), "expired quote rejected");
            Check(wallet.transactionCalls == 1, "expired quote never requests signing");
            bool recoverySaved = false;
            await SeekerGameWallet.SendQuotedTransaction(transaction, Address, CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1),
                () => recoverySaved = true);
            Check(recoverySaved && wallet.sendCalls == 1, "recovery record precedes wallet submission");
            await Reject(() => SeekerGameWallet.SendQuotedTransaction(transaction, "other", CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1),
                () => throw new Exception("must not save")), "wallet submission rejects a different account");
            Check(wallet.sendCalls == 1, "wrong account never receives a send request");
            wallet.changeTransaction = true;
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None), "wallet-modified transaction rejected");
            wallet.changeTransaction = false; wallet.signedOverride = Fixture("allowed");
            Check((await SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None)).SequenceEqual(wallet.signedOverride),
                "Seeker flow accepts permitted safety assertions");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerCheckoutException("transaction-changed")).Contains("differs"), "changed transaction diagnosis");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerCheckoutException("quote-expired")).Contains("expired"), "expired quote diagnosis");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerCheckoutException("account-mismatch")).Contains("does not match"), "account mismatch diagnosis");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("ACTIVITY_RECREATED", "private details")).Contains("ACTIVITY_RECREATED"), "safe SDK error code shown");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("AUTHORIZATION_DECLINED", "")).Contains("Reconnect"), "revoked authorization offers reconnect");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("NO_WALLET_FOUND", "")).Contains("No compatible wallet"), "missing wallet has specific guidance");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("UNSUPPORTED_OPERATION", "")).Contains("sign and send"), "unsupported wallet identifies required operation");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("CANCELLED", "")).Contains("Check payment"), "ambiguous cancellation requests payment check");
            Check(!CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("ACTIVITY_RECREATED", "private details")).Contains("private details"), "SDK exception details not displayed");
            Check(!CheckoutWalletSigner.DescribeSigningFailure(new SeekerConnectException("unsafe detail", "private details")).Contains("unsafe detail"), "untrusted SDK code not displayed");
            await Reject(() => { new SeekerLoginHttp("http://api.battlecities.com"); return Task.CompletedTask; }, "HTTP login origin rejected");
            await Reject(() => { new SeekerLoginHttp("https://api.battlecities.com.evil.example"); return Task.CompletedTask; }, "lookalike login origin rejected");
        }
        sealed class FakeWallet : ISeekerGameWallet
        {
            internal bool changedProof, switchAccount, changeTransaction, requireDisconnect;
            internal bool available = true;
            internal CancellationTokenSource cancel;
            internal SeekerMessageProof proof;
            internal byte[] signedOverride;
            internal int transactionCalls, accountCalls, sendCalls, disconnectCalls;
            bool switched;
            public Task<bool> IsAvailable(CancellationToken token) => Task.FromResult(available);
            WalletAccount CurrentAccount() => new WalletAccount {
                address = switched ? "other" : Address, publicKeyBase64 = Convert.ToBase64String(PublicKey) };
            public Task<WalletAccount> Account(CancellationToken token) { accountCalls++; if(requireDisconnect && disconnectCalls==0)throw new Exception("Cached grant was not cleared"); return Task.FromResult(CurrentAccount()); }
            public Task<SeekerMessageProof> SignMessage(string message, CancellationToken token)
            {
                proof = new SeekerMessageProof { address = Address, signature = Enumerable.Repeat((byte)7, 64).ToArray(),
                    payload = Encoding.UTF8.GetBytes(changedProof ? "altered" : message).Concat(Enumerable.Repeat((byte)7, 64)).ToArray() };
                switched = switchAccount; cancel?.Cancel(); return Task.FromResult(proof);
            }
            public Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token)
            { transactionCalls++; var signed = (byte[])(signedOverride ?? transaction).Clone(); if (signedOverride == null) signed[1] = 7; if (changeTransaction) signed[77]++; return Task.FromResult(signed); }
            public Task<WalletSignature> SignAndSend(byte[] transaction, CancellationToken token) { sendCalls++; return Task.FromResult<WalletSignature>(null); }
            public Task Disconnect() { disconnectCalls++; return Task.CompletedTask; }
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
