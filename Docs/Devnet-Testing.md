# Devnet test deployment

Game and same-origin API: https://test.battlecities.com/

This deployment uses Solana **Devnet**, a fresh PostgreSQL database, its own
session/payment records, and a separate API service. Production defaults remain
Mainnet. Only a WebGL game launched on the exact hostname `test.battlecities.com`
selects Devnet and `https://test.battlecities.com` as its API. Every other hostname
(including localhost, preview sites and lookalike subdomains), Android and other
native players use Mainnet and `https://api.battlecities.com`. The build profile,
query parameters and saved scene URL cannot enable Devnet. Payment quotes must
match the selected network. Editor defaults are live, with API fixture overrides
available for local verification. Mainnet payout and BATC drop workers stay disabled.
SKR checkout stays unavailable until a Devnet test mint is configured.

Enable Phantom's Testnet Mode and select **Solana Devnet**. Fund the wallet with
test SOL from https://faucet.solana.com/. Wallet sign-in is message signing;
transactions, balances and verification in the test Shop use Devnet.

## Build and deploy

Open the Unity project. Set `BATTLECITIES_TEST_SSH_KEY` to the path of the existing
Oracle SSH key, then run `Build-Test-Web.bat`. Alternatively:

```powershell
powershell -NoProfile -File Tools/Publishing/Deploy-TestWeb.ps1 -SshKey C:/path/to/your/existing-key
```

The script uses the current game version. It saves the Editor's version and
WebGL defines, builds with runtime hostname-based network selection, restores
those settings even if building fails, prepares the viewport/branding and a Devnet
label visible only on the exact test hostname,
uploads a checksum-verified archive, switches the test site's release directory,
and verifies the live metadata. It prints the local site and archive paths under
`Builds/TestWeb/<timestamp>/`. Add `-BuildOnly` to skip deployment.

Both production and test build configuration remove the obsolete
`BATTLECITIES_DEVNET` define. The test launcher refuses to reuse older builds
without a hostname-selection receipt. Test builds do not publish a stable GitHub game release or
replace the distributed APK. Test web archives and server credentials are ignored.

## Server

Cloudflare proxies the test subdomain to the existing Oracle server. Caddy serves
`/opt/battlecities-test/web/current` and proxies `/api/*` to the isolated service
on `127.0.0.1:3003`. The test PostgreSQL database is `battlecities_devnet_test`,
owned by a separate unprivileged database role. Its service secrets remain in
the server's protected configuration; production configuration/data are not copied.

The test API source is on `codex/devnet-test-site` in BattleCities-API. The test
deployment tooling is on the matching Unity branch. `install-test-api.py` installs
a checksum-verified API source archive, initializes the empty test schema, migrates
only that database, and starts `battlecities-test-api.service`. Future installs
reuse that database and retain test inventory. Web updates are independent.

API setup creates no real wallet payments or Mainnet transfers. Confirm the
catalog's `currency.sol.network` and web `release-info.json` are both `devnet`.
