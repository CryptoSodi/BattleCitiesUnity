using System;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BattleCities.UI
{
    internal interface ISeekerLoginApi
    {
        string Origin { get; }
        string Cookie { get; }
        long ExpiresAt { get; }
        Task<JObject> Request(string method, string path, JObject body, CancellationToken token);
    }
    internal static class SeekerWalletLoginFlow
    {
        internal static async Task<JObject> AuthenticateFresh(ISeekerGameWallet wallet, ISeekerLoginApi api, string attempt, CancellationToken token, Action<string> stage = null)
        {
            token.ThrowIfCancellationRequested();
            stage?.Invoke("clear_cached_authorization");
            await wallet.Disconnect();
            token.ThrowIfCancellationRequested();
            return await Authenticate(wallet, api, attempt, token, stage);
        }
        internal static async Task<JObject> Authenticate(ISeekerGameWallet wallet, ISeekerLoginApi api, string attempt, CancellationToken token, Action<string> stage = null)
        {
            stage?.Invoke("request_challenge");
            var challenge = await api.Request("PUT", "/api/session", new JObject { { "method", "siws" } }, token);
            var input = challenge?["input"] as JObject;
            string nonce = (string)input?["nonce"];
            if ((string)challenge?["method"] != "siws" || input == null ||
                (string)input["domain"] != "battlecities.com" || (string)input["uri"] != "https://battlecities.com" ||
                (string)input["statement"] != "Sign in to Battle Cities." || (string)input["version"] != "1" ||
                !Regex.IsMatch(nonce ?? "", "\\A[a-fA-F0-9]{32}\\z") ||
                !DateTimeOffset.TryParse((string)input["issuedAt"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var issued) ||
                !DateTimeOffset.TryParse((string)input["expirationTime"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry) ||
                expiry <= issued || expiry - issued > TimeSpan.FromMinutes(5) || expiry <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("The server returned an invalid sign-in challenge.");
            stage?.Invoke("wallet_sign_in");
            var proof = await wallet.LoginWithSignIn(input, token);
            token.ThrowIfCancellationRequested();
            string address = proof?.address;
            SeekerGameWallet.ValidateAccount(await wallet.Account(token), address);
            byte[] signedMessage, signature;
            try { signedMessage = Convert.FromBase64String(proof?.signedMessage ?? ""); signature = Convert.FromBase64String(proof?.signature ?? ""); }
            catch (FormatException) { throw new InvalidOperationException("The wallet returned an invalid sign-in proof."); }
            if (string.IsNullOrEmpty(address) || signedMessage.Length == 0 || signedMessage.Length > 4096 ||
                signature.Length != 64 || !signature.Any(value => value != 0) ||
                (!string.IsNullOrEmpty(proof.signatureType) && proof.signatureType != "ed25519"))
                throw new InvalidOperationException("The wallet returned an invalid sign-in proof.");
            stage?.Invoke("verify_session");
            var session = await api.Request("POST", "/api/session", new JObject {
                { "provider", "wallet" }, { "method", "siws" }, { "walletAddress", address }, { "nonce", nonce },
                { "signedMessage", proof.signedMessage }, { "signature", proof.signature }, { "signatureType", proof.signatureType ?? "ed25519" }
            }, token);
            if ((bool?)session?["authenticated"] != true || (string)session?["provider"] != "wallet")
                throw new InvalidOperationException("The API did not establish a wallet session.");
            stage?.Invoke("load_player");
            var result = await api.Request("GET", "/api/player", null, token);
            var player = result?["player"] as JObject;
            token.ThrowIfCancellationRequested();
            if ((bool?)result?["authenticated"] != true || (string)player?["provider"] != "wallet" ||
                (string)player?["walletAddress"] != address ||
                !new SavedWalletSession { origin = api.Origin, cookie = api.Cookie,
                    wallet = address, expiresAt = api.ExpiresAt }.IsValid(api.Origin))
                throw new InvalidOperationException("The verified wallet session could not load its player.");
            stage?.Invoke("complete");
            return new JObject { { "ok", true }, { "attemptId", attempt }, { "player", player },
                { "sessionCookie", api.Cookie }, { "sessionExpiresAt", api.ExpiresAt } };
        }
    }
}
