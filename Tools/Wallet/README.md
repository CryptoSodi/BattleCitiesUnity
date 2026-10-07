# Browser wallet checkout bundle

Run `npm ci` then `npm run build` from this folder to regenerate the committed
`Assets/StreamingAssets/BattleCitiesCheckout/wallet.js`. It loads locally from
StreamingAssets and does not depend on a CDN. Dependencies are pinned in the lockfile.

The helper asks Phantom to sign the quoted transaction. It does not broadcast.
Unity stores the signed transaction before submitting it to the authenticated API.
Private keys remain in the wallet.
