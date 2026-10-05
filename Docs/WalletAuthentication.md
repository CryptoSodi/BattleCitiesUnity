# Wallet authentication

The existing Login and main-menu wallet buttons select their provider at runtime:

| Build/device | Provider |
| --- | --- |
| WebGL | Phantom browser extension (`window.phantom.solana`) |
| Seeker / other Android devices | Solana Mobile Wallet Adapter wallet chooser |
| PSG1 | Jupiter Wallet (`ag.jup.jupiter.android`) over Mobile Wallet Adapter |
| Unity Editor / desktop standalone | Clear unavailable message; guest play remains available |

`RuntimePlatformInfo.IsPsg1` selects the PSG1 route. For a dedicated PSG1 build,
set `BATTLE_CITIES_PSG1`; the existing device-identification fallback also works.
The device simulator previews the layout but cannot open a real Android wallet.
Mobile buttons display **CONNECT WALLET** or **CONNECT JUPITER** instead of the
web button's baked Phantom artwork. PSG1 **B** cancels a pending attempt after
returning to the game.

## Verification and session lifecycle

1. Connect/authorize the wallet and get a Solana public key.
2. `PUT /api/session` with `{ walletAddress }` to get the server's message and nonce.
3. Ask the wallet to sign those exact UTF-8 bytes. This is a message signature,
   not a transaction or payment.
4. `POST /api/session` with `{ provider: "wallet", walletAddress, nonce, message,
   signature }`, using a base64-encoded detached Ed25519 signature.
5. Require a verified wallet session and fetch `/api/player`. Its wallet address
   must equal the account that signed the challenge before Unity enters the menu.

The existing API performs signature verification and challenge expiry/reuse
checks. No backend changes are required. The default API is
`https://api.battlecities.com`; native wallet login requires HTTPS.

Web requests include cookies, and the web hosting origin must be allowed by the
API's existing credentialed CORS policy. The wallet result carries an attempt ID;
cancelled, timed-out, disabled-scene and superseded attempts cannot authenticate
the UI later. Phantom account changes during signing also fail the attempt.

Android runs the complete MWA/signature/API sequence on a native worker thread,
because Unity pauses while another app is in front. It closes the MWA association
after signing and passes results back to Unity's main thread. Only the
`battlecity_session` cookie is retained in memory, scoped to the API origin, so
authenticated API requests continue across scenes. It is never written to
PlayerPrefs. Relaunching the native game requires signing in again. Private keys
and seed phrases never enter the game.

## Android build dependency

`Assets/Plugins/Android/BattleCitiesWallet.androidlib` is a Gradle library plugin
with its own manifest, package-visibility queries and consumer keep rules for
Unity's JNI bridge. It resolves the official
`com.solanamobile:mobile-wallet-adapter-clientlib:2.1.1` from Maven Central.
This version supports the project's Android API 36 / Unity 6.6 toolchain;
MWA 2.2.0 requires API 37. The plugin uses no Google services.
Keep AndroidX enabled in Gradle (Unity's current generated configuration already does).

## Checks

Run the web bridge regression suite from the project root:

```shell
node --test Tests/WebGL/wallet-auth.test.cjs
```

The suite uses ephemeral test keys and a mocked API/provider to check exact-message
signatures, matching accounts, rejection, cancellation, timeout, invalid challenges,
duplicate requests and rejected sessions. It never opens or signs with a real wallet.

Before shipping, test rebuilt WebGL and Android players with real wallets:

- Web: open the HTTPS build with Phantom installed; connect, approve the message,
  confirm the wallet player reaches the menu, and verify authenticated inventory.
- Seeker: connect through the wallet chooser, reject once and retry, then approve.
  Confirm the session survives the Login-to-MainMenu transition.
- PSG1: confirm **CONNECT JUPITER** opens Jupiter and controller focus returns to
  the game after approval or rejection. Confirm other wallet apps are not selected.
- On both Android devices: close the wallet without approving, lose/recover network,
  background and resume the game, and retry after the timeout. No failed attempt
  should enter the menu as an authenticated wallet.

## References

- [Phantom direct integration](https://docs.phantom.com/solana/integrating-phantom)
- [Solana Mobile Android integration](https://github.com/solana-mobile/mobile-wallet-adapter/blob/main/android/docs/integration_guide.md)
- [Mobile Wallet Adapter protocol](https://github.com/solana-mobile/mobile-wallet-adapter/blob/main/spec/spec.md)
- [Unity Android library plugins](https://docs.unity3d.com/Manual/android-library-plugin-create.html)
