using System;
using System.Linq;
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
        internal static async Task<JObject> Authenticate(ISeekerGameWallet wallet, ISeekerLoginApi api, string attempt, CancellationToken token, Action<string> stage = null)
        {
            stage?.Invoke("restore_or_connect");
            var account = await wallet.Account(token);
            SeekerGameWallet.ValidateAccount(account);
            string address = account.address;
            stage?.Invoke("request_challenge");
            var challenge = await api.Request("PUT", "/api/session", new JObject { { "walletAddress", address } }, token);
            string nonce = (string)challenge?["nonce"], message = (string)challenge?["message"];
            if (!Regex.IsMatch(nonce ?? "", "\\A[a-fA-F0-9]{32}\\z") || string.IsNullOrEmpty(message) || message.Length > 4096)
                throw new InvalidOperationException("The server returned an invalid sign-in challenge.");
            stage?.Invoke("sign_message");
            var proof = await wallet.SignMessage(message, token);
            token.ThrowIfCancellationRequested();
            SeekerGameWallet.ValidateAccount(await wallet.Account(token), address);
            if (proof?.address != address || proof.signature == null || proof.signature.Length != 64 ||
                !proof.signature.Any(value => value != 0) || proof.payload == null ||
                // The pinned wrapper returns MWA's signed payload: message followed by signature.
                !proof.payload.SequenceEqual(new UTF8Encoding(false, true).GetBytes(message).Concat(proof.signature)))
                throw new InvalidOperationException("The wallet returned an invalid sign-in proof.");
            stage?.Invoke("verify_session");
            var session = await api.Request("POST", "/api/session", new JObject {
                { "provider", "wallet" }, { "walletAddress", address }, { "nonce", nonce },
                { "message", message }, { "signature", Convert.ToBase64String(proof.signature) }
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
