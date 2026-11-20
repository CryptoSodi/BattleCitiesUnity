# Cherry GitHub Pages hosting

`Prepare-CherryWebSite.ps1` prepares a separate GitHub Pages artifact with
`CNAME` set to `cherry.battlecities.com` and the Cherry verification file under
`.well-known/`. It does not alter the Play artifact or build a Unity player.

The intended repository is `CryptoSodi/BattleCitiesCherry`. GitHub Pages does
not apply Cloudflare Pages `_headers` files. The required response headers must
be set at the proxied Cloudflare hostname instead; `response-header-rules.json`
contains the hostname-scoped rule. Inspect the existing response header
ruleset and append this rule without replacing unrelated rules. Verify the
actual response after publishing.

Do not point the DNS record at GitHub Pages until the Cherry repository has
GitHub Pages enabled and its custom domain is configured. The unfinished
Cherry integration must not be presented as ready for the Cherry listing.
