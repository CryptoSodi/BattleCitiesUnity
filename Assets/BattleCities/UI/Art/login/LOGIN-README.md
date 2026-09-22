# Login screen

Open `Assets/BattleCities/Scenes/Login.unity`. It is the first enabled scene in Build Settings.

## Editing

Expand `Login Canvas / Login Layout (edit child positions freely)` to adjust the individual RectTransforms. `LoginLayout` scales only the overall design root; it does not reset child positions. The blue container, TV frame, background and logo reuse existing main-menu assets. All six new PNGs are unmodified copies supplied by the user.

`Battle Cities > Login > Rebuild from supplied assets` deliberately resets **Login only** to the authored defaults. Do not use this command after manual changes unless you want to rebuild. Save the active scene first. No main-menu platform layouts are rebuilt.

## Authentication

- Phantom (WebGL): PUT `/api/session` obtains the nonce/message; Phantom signs that message; POST `/api/session` verifies the signature; GET `/api/player` loads the wallet player. HTTP-only session cookies remain browser-managed.
- WebGL API requests use `credentials: include`. Localhost/127.0.0.1 API hostnames are normalized to the browser hostname so development cookies remain same-site.
- Main menu reads GET `/api/session`, `/api/player`, and `/api/leaderboard/rewards`.
- Guest is local-only, intentionally not sent to the wallet-only session endpoint. The UI does not grant server authentication or ranked rewards to guests. A selected guest does not inherit a wallet session from the browser cookie.
- No Google login is imported or offered. Existing Google sessions are not accepted by this client.
- Native Editor/Android/PSG1 Phantom authentication is not implemented. The screen reports this and leaves Guest usable.
- Configure the API base URL on `Login Flow / Main Menu Api Client` and the main-menu API client for deployments. Physical devices cannot reach a developer machine through `localhost`.
- Set `Dapp Store Url` on `Login Flow / Login Scene` after a real HTTPS app listing is published. Until then the button explicitly reports that the listing is not available.

## Verification

`verify-bridge.cjs` checks credentialed requests, localhost normalization, wallet challenge/signature/player routing and rejection handling, then reads the local API's health/session/player/leaderboard endpoints. It does not require or sign with a real wallet.

`Battle Cities > Login > Test guest flow` exercises the Login-to-MainMenu transition in Play Mode and reports `LOGIN_SMOKE_PASS` or `LOGIN_SMOKE_FAIL`. A real Phantom end-to-end test still requires the user's wallet connection and signature approval in a WebGL build.
