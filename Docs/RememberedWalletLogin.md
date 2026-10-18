# Remembered wallet sign-in

Android remembers the last verified wallet game session after one successful
sign-in with this version. Startup decrypts the saved session, validates its
expiration and exact production API origin, then requests GET /api/session and
GET /api/player. Both server identities must agree with the remembered wallet
before entering Home and loading the required content bundle.

The session cookie and public wallet address are encrypted using an AES-256-GCM
key in Android Keystore. The atomic file is kept in getNoBackupFilesDir. Wallet
private keys, MWA authorizations and payment-signing permission are not stored.
Keystore work runs off the Unity frame thread. Cookies are never logged or put
in PlayerPrefs, browser URLs, or the content bucket. HTTP redirects are disabled.
Session expiration follows the API cookie Max-Age, capped at its current 30 days.
Expired/revoked/invalid sessions require manual sign-in. Temporary network/server
failures preserve saved credentials but do not grant authenticated access.
Removing app data/uninstalling requires sign-in again. First use after upgrading
from an older APK requires a wallet sign-in to create the saved record.

Game authentication and the current-run purchase connection are separate. A
restored session can play and load its account; Shop shows CONNECT WALLET until
an explicit wallet login in that app run. New quotes require that connection.
Signing still opens MWA and requires the same wallet and unchanged transaction.
Existing signed payment receipts can be checked without a new payment signature.
Confirmed server logout clears the encrypted session and purchase connection;
failed logout retains the existing session and offers retry.

Web uses the existing HTTP-only browser cookie and now checks it on the Login
screen automatically. Guest/signed-out entry suppresses automatic wallet login.
Desktop does not persist native credentials.

Startup hides the authored login Canvas before its first frame and shows the
game logo with loading status while checking the session and preparing content.
A verified session goes directly to Home. Failed/missing restoration reveals
the existing login controls and controller focus; content download failures
also reveal those controls for retry.

Validation: BattleCities.Editor.SavedSessionChecks.Run uses isolated storage and
HTTP fixtures for valid restore, missing/malformed/expired credentials, origin
and account mismatch, denied sessions, outages, Shop connection state and logout.
Results are written to Builds/SavedSessionChecks/result.txt. A real Android
sign-in, force-stop/reopen, Shop reconnect and logout/reopen remain device checks.

Verified build: 1.0.3 / code 4, Builds/Android/1.0.3-v4/20261017-200008.
APK: 78,861,959 bytes; signature, alignment, Firebase identity and runtime shader
retention passed. All 50 saved-session fixture checks passed. The APK contains
WalletSessionStore and its embedded bundle URL matches the public R2 object.
The 66,629,978-byte hosted bundle was downloaded and its SHA256 verified as
80f881da5b9f327c4e0c6d517254826e0a9b20d9a6e2795a25ce53b03e81b314.
No Android device was connected during this change; real wallet/reopen validation
is still pending.
