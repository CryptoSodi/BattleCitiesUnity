# Native Seeker Connect integration

Battle Cities pins `com.community.seekerconnect.unity` 0.2.2 from
`CryptoSodi/solana-seeker-connect-unity` commit
`2af326d4855407537b95f217aa0a4c1bfc89176b`. The package was supplied from
`D:/repos/Solana Connect Integration in Unity/UnitySeekerConnect` and is installed
by its pinned Git URL, so another checkout does not depend on that drive path.

## Provider routing

- Native Android checks the SDK's wallet availability. Certified Seeker wallets
  and other installed MWA-compatible wallets use the SDK for login, cached
  authorization restoration and quote signing. Certified routing stays preferred;
  the official generic-MWA fallback chooses another installed wallet when needed.
- Production generic support uses `allowGenericMwa = true`, independently of
  `allowMwaTesting = false`. Mock-only signature compatibility remains disabled.
  No-wallet/unknown results retain the existing native fallback/error handling.
- PSG1 continues to use the existing Jupiter-targeted provider and never
  initializes Seeker Connect. Web continues to use its Phantom integration;
  no Seeker browser connection or relay is configured by this integration.
- Identity is `https://battlecities.com`, with the same hosted wallet icon as
  the existing native flow. Native authorization requests `solana:mainnet`,
  matching the native game's live API. No devnet testing authorization is used.

An isolated Android devnet flavor is available through
`Tools/Publishing/Build-DevnetAndroid.ps1`. It builds `sol.battle.cities.devnet`
with the launcher label **Battle Cities Devnet**, `BATTLECITIES_ANDROID_DEVNET`,
the test API and `solana:devnet` for both SDK and legacy MWA authorization.
Its optional sample-wallet compatibility stays confined to devnet. Mainnet
always uses `allowMwaTesting = false` and verifies message signatures normally.
Its debug signature and app sandbox are separate from the mainnet app, and
production Firebase/push registration is excluded. The launcher snapshots and
restores production editor settings and Firebase resources after the build.

## Authentication and payment boundaries

SDK 0.2.2 verifies Ed25519 signatures for both full signed messages and detached
64-byte responses before normalizing the result. Invalid responses and changed
accounts clear cached authorization; the next explicit Connect starts fresh.
Signing and payments are never retried automatically.

`MobileWalletLogin` selects the provider. `SeekerWalletLoginFlow` requests the
existing server nonce and exact message, checks the wrapper's signed payload
(`UTF-8 message || 64-byte signature`), and submits the detached signature to
the existing API. It then requires the server-authenticated player to match the
wallet and requires a valid server session cookie. Each attempt has an isolated
HTTP cookie jar; cancellation/late responses cannot publish a login result.
The successful game session still uses the existing encrypted Android storage.

Cached SDK authorization alone never authenticates the player. At startup the
game first verifies its saved backend session/player; only then does it restore
the optional SDK authorization without connecting a new wallet. A reconnect
still signs a fresh backend challenge. Confirmed game sign-out also clears local
SDK authorization. This does not revoke the wallet's grant.

After server session validation, a successful quiet SDK restore for that exact
wallet also restores the Shop purchase-connection flag. Missing, unavailable,
revoked or mismatched SDK authorization leaves the Shop requiring Connect Wallet
while preserving a valid game login. Restoration does not sign or submit a
transaction; each checkout still uses the existing wallet approval flow.

Checkout uses `SignTransactionAsync`, never SDK sign-and-send. It checks the
wallet before and after signing, rejects changed transaction messages and expired
quotes, and passes signed bytes through the existing game/server validation and
submission flow. It never automatically retries a payment.

## Toolchain and validation

The package's Android hook adds its native Gradle module and separate
non-exported AndroidX host activity; it does not replace the Unity renderer.
It requires Unity 6000.6.0f1 / AGP 9.0 / Gradle 9.1, API 36, Java 17 and Kotlin
2.4.10. Its backup rules exclude authorization preferences. The existing game
backend session remains in Android KeyStore-encrypted no-backup storage.

`BattleCities.Editor.SeekerWalletChecks.CompileAndroid` runs isolated login and
checkout checks and compiles Android player scripts without producing an APK.
The native consumer fixture under ignored `Builds/Automation` compiles the
existing Java MWA library against the SDK and runs the wrapper's Kotlin tests.

Jupiter Mobile is listed as MWA-compatible by Solana Mobile at
https://wallets.solanamobile.com/. Installing it on a normal Android phone can
validate the generic provider and cache restoration using the available phone.
PSG1-specific controls and device lifecycle remain outside that phone test; its
existing Jupiter-targeted routing is unchanged.

Version 0.2.1 consumer validation passed 31 game integration checks, compilation
of 47 Android player script assemblies, compilation of the legacy Java library
with the new SDK, and all 29 native SDK tests. APK/Web builds were not produced.

These checks do not establish physical Seeker approval/lifecycle behavior or
release APK manifest/R8 compatibility. Those require a subsequently authorized
APK build and device test. No on-chain payment is needed for the initial
connect, cancellation, restore and sign-message device checks.
