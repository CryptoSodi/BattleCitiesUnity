using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BattleCities.UI;
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
            var authorization = new AuthorizationResult { Accounts = new System.Collections.Generic.List<AuthorizationResult.AuthorizationResultAccounts> {
                new AuthorizationResult.AuthorizationResultAccounts { Address = Convert.ToBase64String(PublicKey) } } };
            SeekerGameWallet.ValidateReauthorizedAccount(authorization, Address);
            Check(true, "matching reauthorized account is accepted");
            await Reject(() => { SeekerGameWallet.ValidateReauthorizedAccount(authorization, "other"); return Task.CompletedTask; },
                "changed wallet account is rejected before checkout");
            authorization.Accounts[0].Address = "invalid base64";
            await Reject(() => { SeekerGameWallet.ValidateReauthorizedAccount(authorization, Address); return Task.CompletedTask; },
                "invalid reauthorized account is rejected");
            const string sdkPublicKey = "solana_sdk.mwa.public_key";
            string oldKey = PlayerPrefs.GetString(sdkPublicKey, null);
            try
            {
                PlayerPrefs.SetString(sdkPublicKey, Address);
                var sessionCache = SeekerGameWallet.NewSessionCacheForChecks();
                await sessionCache.Set("test-token");
                Check(await sessionCache.Get() == "test-token", "authorization is available during the current process");
                Check(await SeekerGameWallet.NewSessionCacheForChecks().Get() == null, "a new app session cannot restore wallet authorization");
                PlayerPrefs.SetString(sdkPublicKey, "other");
                Check(await sessionCache.Get() == null, "authorization cannot be used for another account");
                PlayerPrefs.SetString(sdkPublicKey, Address);
                await sessionCache.Clear();
                Check(await sessionCache.Get() == null, "clearing authorization disconnects the wallet");
            }
            finally
            {
                if (oldKey == null) PlayerPrefs.DeleteKey(sdkPublicKey); else PlayerPrefs.SetString(sdkPublicKey, oldKey);
            }
            string paymentKey = "battlecities.test.deferred." + Guid.NewGuid();
            try
            {
                var receipt = new JObject { ["wallet"] = Address, ["api"] = "https://example.invalid", ["quoteToken"] = "quote-1", ["walletSubmitted"] = true };
                var second = (JObject)receipt.DeepClone(); second["quoteToken"] = "quote-2";
                Check(DeferredShopPayments.Add(paymentKey, Address, "https://example.invalid", receipt), "unknown receipt can be saved");
                Check(DeferredShopPayments.Add(paymentKey, Address, "https://example.invalid", receipt), "saving the same receipt twice is idempotent");
                Check(DeferredShopPayments.Add(paymentKey, Address, "https://example.invalid", second), "multiple receipts are retained");
                Check(DeferredShopPayments.Read(paymentKey, Address, "https://example.invalid").Count == 2, "all receipts survive a reload");
                Check(DeferredShopPayments.Read(paymentKey, "other", "https://example.invalid") == null, "receipts cannot be read for another wallet");
                Check(!DeferredShopPayments.Resolved(new JObject { ["ok"] = false, ["pending"] = true }), "unknown status retains receipt");
                Check(DeferredShopPayments.Resolved(new JObject { ["ok"] = true, ["account"] = new JObject() }), "verified payment resolves receipt");
                Check(DeferredShopPayments.Resolved(new JObject { ["terminal"] = true }), "terminal failure resolves receipt");
                Check(DeferredShopPayments.Remove(paymentKey, Address, "https://example.invalid", "quote-1") &&
                    DeferredShopPayments.Read(paymentKey, Address, "https://example.invalid").Count == 1, "resolving one payment preserves the other");
                PlayerPrefs.SetString(paymentKey, "invalid receipt data");
                Check(!DeferredShopPayments.Add(paymentKey, Address, "https://example.invalid", receipt) && PlayerPrefs.GetString(paymentKey) == "invalid receipt data",
                    "invalid saved data is never overwritten");
            }
            finally { PlayerPrefs.DeleteKey(paymentKey); }
            var restored = new FakeWallet(); SeekerGameWallet.SetClientForChecks(restored);
            var revoked = new WalletAdapterException("AUTHORIZATION_DECLINED", "");
            Check(SeekerGameWallet.NeedsReconnect(revoked) && SeekerGameWallet.DefinitelyNotSubmitted(revoked), "revoked grant requires reconnect without submitting");
            await SeekerGameWallet.ClearRejectedAuthorization(revoked);
            Check(restored.disconnectCalls == 1, "revoked cached authorization is discarded");
            var missing = new WalletAdapterException("NOT_CONNECTED", "");
            Check(SeekerGameWallet.NeedsReconnect(missing) && SeekerGameWallet.DefinitelyNotSubmitted(missing), "missing grant requires reconnect without submitting");
            await SeekerGameWallet.ClearRejectedAuthorization(missing);
            Check(restored.disconnectCalls == 2, "missing authorization clears cached grant");
            var otherWallet = new SeekerCheckoutException("account-mismatch");
            Check(SeekerGameWallet.NeedsReconnect(otherWallet) && SeekerGameWallet.DefinitelyNotSubmitted(otherWallet), "different account requires reconnect");
            await SeekerGameWallet.ClearRejectedAuthorization(otherWallet);
            Check(restored.disconnectCalls == 3, "different account clears cached grant");
            foreach (var code in new[] { "TIMEOUT", "CANCELLED", "SESSION_CLOSED", "REQUEST_DECLINED" })
            {
                var ambiguous = new WalletAdapterException(code, "");
                Check(!SeekerGameWallet.DefinitelyNotSubmitted(ambiguous), code + " retains any pending payment");
                await SeekerGameWallet.ClearRejectedAuthorization(ambiguous);
            }
            Check(restored.disconnectCalls == 3, "uncertain wallet outcomes do not clear authorization");
            var invalidResponse = new WalletAdapterException("INVALID_RESPONSE", "");
            Check(SeekerGameWallet.NeedsReconnect(invalidResponse) && !SeekerGameWallet.DefinitelyNotSubmitted(invalidResponse), "invalid wallet response reconnects while preserving uncertain payments");
            await SeekerGameWallet.ClearRejectedAuthorization(invalidResponse);
            Check(restored.disconnectCalls == 4, "invalid wallet response clears cached grant");
            var wallet = new FakeWallet(); var api = new FakeApi();
            var fresh = new FakeWallet { requireDisconnect = true };
            var freshResult = await SeekerWalletLoginFlow.AuthenticateFresh(fresh, new FakeApi(), "fresh", CancellationToken.None);
            Check((bool)freshResult["ok"] && fresh.disconnectCalls == 1 && fresh.accountCalls > 0, "explicit reconnect clears cached grant before requesting account");
            var result = await SeekerWalletLoginFlow.Authenticate(wallet, api, "attempt", CancellationToken.None);
            Check((bool)result["ok"] && (string)result["attemptId"] == "attempt", "verified login result");
            Check(api.calls == "PUT POST GET " && (string)api.posted["nonce"] == api.nonce && wallet.signInCalls == 1, "SIWS challenge precedes a single wallet sign-in");
            Check((string)api.posted["signature"] == wallet.signInProof.signature, "SIWS signature reaches verification");
            Check((string)result["sessionCookie"] == api.Cookie && (long)result["sessionExpiresAt"] == api.ExpiresAt, "actual server session preserved");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { nonce = "bad" }, "x", CancellationToken.None), "malformed nonce rejected");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { statement = "" }, "x", CancellationToken.None), "unexpected challenge statement rejected");
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, new FakeApi { domain = "evil.example" }, "x", CancellationToken.None), "unexpected challenge domain rejected");
            api = new FakeApi(); wallet = new FakeWallet { changedProof = true };
            await Reject(() => SeekerWalletLoginFlow.Authenticate(wallet, api, "x", CancellationToken.None), "malformed signed message rejected");
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
            var sdkTransaction = Solana.Unity.Rpc.Models.Transaction.Deserialize(transaction);
            Check(sdkTransaction.Serialize().SequenceEqual(transaction), "Solana Unity SDK preserves the quoted transaction bytes");
            var sdkProbe = new SdkSigningProbe(PublicKey, Fixture("unchanged"));
            await sdkProbe.Login();
            var sdkSigned = await sdkProbe.SignTransaction(Solana.Unity.Rpc.Models.Transaction.Deserialize(transaction));
            Check(sdkProbe.called, "SDK sign-only entry point accepts a public-key wallet");
            Check(ApprovedCheckoutTransaction.Matches(transaction, sdkSigned.Serialize()), "SDK sign-only output preserves the approved transaction");
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
            wallet.SupportsSignAndSend = false;
            wallet.signedOverride = null;
            int sendsBeforeFallback = wallet.sendCalls;
            int signsBeforeFallback = wallet.transactionCalls;
            bool fallbackSavedRecovery = false;
            var fallback = await SeekerGameWallet.CheckoutQuotedTransaction(transaction, Address, CancellationToken.None,
                DateTimeOffset.UtcNow.AddMinutes(1), () => fallbackSavedRecovery = true);
            Check(fallback.signed != null && fallback.submitted == null && wallet.sendCalls == sendsBeforeFallback
                && wallet.transactionCalls == signsBeforeFallback + 1 && !fallbackSavedRecovery,
                "sign-only wallet uses validated signed transaction without wallet submission");
            wallet.changeTransaction = true;
            await Reject(() => SeekerGameWallet.CheckoutQuotedTransaction(transaction, Address, CancellationToken.None,
                DateTimeOffset.UtcNow.AddMinutes(1), () => fallbackSavedRecovery = true), "sign-only wallet cannot alter quote");
            wallet.changeTransaction = false;
            wallet.SupportsSignAndSend = true;
            wallet.sendError = new WalletAdapterException("TIMEOUT", "");
            signsBeforeFallback = wallet.transactionCalls;
            bool ambiguousRecovery = false;
            await Reject(() => SeekerGameWallet.CheckoutQuotedTransaction(transaction, Address, CancellationToken.None,
                DateTimeOffset.UtcNow.AddMinutes(1), () => ambiguousRecovery = true), "ambiguous send failure propagates");
            Check(ambiguousRecovery && wallet.transactionCalls == signsBeforeFallback,
                "uncertain wallet submission never triggers sign-only fallback");
            wallet.sendError = new WalletAdapterException("UNSUPPORTED_OPERATION", "");
            bool clearedUnsupported = false;
            var unsupportedFallback = await SeekerGameWallet.CheckoutQuotedTransaction(transaction, Address, CancellationToken.None,
                DateTimeOffset.UtcNow.AddMinutes(1), () => { }, () => clearedUnsupported = true);
            Check(clearedUnsupported && unsupportedFallback.signed != null,
                "only explicit unsupported method clears the send marker and falls back to signing");
            wallet.sendError = null;
            wallet.changeTransaction = true;
            await Reject(() => SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None), "wallet-modified transaction rejected");
            wallet.changeTransaction = false; wallet.signedOverride = Fixture("allowed");
            Check((await SeekerGameWallet.SignQuotedTransaction(transaction, Address, CancellationToken.None)).SequenceEqual(wallet.signedOverride),
                "Seeker flow accepts permitted safety assertions");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerCheckoutException("transaction-changed")).Contains("differs"), "changed transaction diagnosis");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerCheckoutException("quote-expired")).Contains("expired"), "expired quote diagnosis");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new SeekerCheckoutException("account-mismatch")).Contains("account changed"), "account mismatch diagnosis");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("ACTIVITY_RECREATED", "private details")).Contains("ACTIVITY_RECREATED"), "safe SDK error code shown");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("AUTHORIZATION_DECLINED", "")).Contains("Reconnect"), "revoked authorization offers reconnect");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("NO_WALLET_FOUND", "")).Contains("No compatible wallet"), "missing wallet has specific guidance");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("UNSUPPORTED_OPERATION", "")).Contains("another wallet"), "unsupported wallet offers alternative");
            Check(CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("CANCELLED", "")).Contains("Check payment"), "ambiguous cancellation requests payment check");
            Check(!CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("ACTIVITY_RECREATED", "private details")).Contains("private details"), "SDK exception details not displayed");
            Check(!CheckoutWalletSigner.DescribeSigningFailure(new WalletAdapterException("unsafe detail", "private details")).Contains("unsafe detail"), "untrusted SDK code not displayed");
            await Reject(() => { new SeekerLoginHttp("http://api.battlecities.com"); return Task.CompletedTask; }, "HTTP login origin rejected");
            await Reject(() => { new SeekerLoginHttp("https://api.battlecities.com.evil.example"); return Task.CompletedTask; }, "lookalike login origin rejected");
        }
        sealed class SdkSigningProbe : Solana.Unity.SDK.WalletBase
        {
            readonly Solana.Unity.Wallet.Account account;
            readonly byte[] signed;
            internal bool called;
            internal SdkSigningProbe(byte[] key, byte[] signedTransaction) { account = new Solana.Unity.Wallet.Account(string.Empty, new Solana.Unity.Wallet.PublicKey(key)); signed = signedTransaction; }
            protected override Task<Solana.Unity.Wallet.Account> _Login(string password = null) => Task.FromResult(account);
            protected override Task<Solana.Unity.Wallet.Account> _CreateAccount(string mnemonic = null, string password = null) => throw new NotImplementedException();
            protected override Task<Solana.Unity.Rpc.Models.Transaction> _SignTransaction(Solana.Unity.Rpc.Models.Transaction transaction) { called = true; return Task.FromResult(Solana.Unity.Rpc.Models.Transaction.Deserialize(signed)); }
            protected override Task<Solana.Unity.Rpc.Models.Transaction[]> _SignAllTransactions(Solana.Unity.Rpc.Models.Transaction[] transactions) => throw new NotImplementedException();
            public override Task<byte[]> SignMessage(byte[] message) => throw new NotImplementedException();
        }
        sealed class FakeWallet : ISeekerGameWallet
        {
            public bool SupportsSignAndSend { get; set; } = true;
            internal bool changedProof, switchAccount, changeTransaction, requireDisconnect;
            internal bool available = true;
            internal CancellationTokenSource cancel;
            internal SeekerMessageProof proof;
            internal SeekerSignInProof signInProof;
            internal Exception sendError;
            internal byte[] signedOverride;
            internal int transactionCalls, accountCalls, sendCalls, disconnectCalls, signInCalls;
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
            public Task<SeekerSignInProof> LoginWithSignIn(JObject input, CancellationToken token)
            {
                if (requireDisconnect && disconnectCalls == 0) throw new Exception("Cached grant was not cleared");
                signInCalls++;
                signInProof = new SeekerSignInProof { address = Address,
                    signedMessage = changedProof ? "!invalid!" : Convert.ToBase64String(Encoding.UTF8.GetBytes("signed SIWS message")),
                    signature = Convert.ToBase64String(Enumerable.Repeat((byte)7, 64).ToArray()), signatureType = "ed25519" };
                switched = switchAccount; cancel?.Cancel(); return Task.FromResult(signInProof);
            }
            public Task<byte[]> SignTransaction(byte[] transaction, CancellationToken token)
            { transactionCalls++; var signed = (byte[])(signedOverride ?? transaction).Clone(); if (signedOverride == null) signed[1] = 7; if (changeTransaction) signed[77]++; return Task.FromResult(signed); }
            public Task<byte[]> SignAndSend(byte[] transaction, CancellationToken token) { sendCalls++; if(sendError!=null)throw sendError; return Task.FromResult<byte[]>(null); }
            public Task Disconnect() { disconnectCalls++; return Task.CompletedTask; }
        }
        sealed class FakeApi : ISeekerLoginApi
        {
            internal string nonce = new string('a', 32), statement = "Sign in to Battle Cities.", domain = "battlecities.com", calls = "", cookie = "battlecity_session=sess-test";
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
                if (method == "PUT") return Task.FromResult(new JObject { { "method", "siws" }, { "input", new JObject {
                    { "domain", domain }, { "uri", "https://battlecities.com" }, { "statement", statement }, { "version", "1" }, { "nonce", nonce },
                    { "issuedAt", DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O") }, { "expirationTime", DateTimeOffset.UtcNow.AddMinutes(4).ToString("O") } } } });
                if (method == "POST") posted = body;
                return Task.FromResult(new JObject { { "authenticated", authenticated }, { "provider", "wallet" },
                    { "player", new JObject { { "provider", "wallet" }, { "walletAddress", playerAddress } } } });
            }
        }
    }
}
