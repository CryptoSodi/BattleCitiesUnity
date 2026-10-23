# Over-the-air game screens

Current Android APKs include the signed screen runtime. The new Web controls guide
and Web update loader require one requested Web player build; later compatible
display/artwork updates download at cold startup without rebuilding that player.

## PSG1 controls guide

`battle-guide-psg1` opens inside the TV before a PSG1 battle, using the same guarded
loadout/fuel continuation as Web. Its separate opt-out preference is
`battlecities.psg1.controls.dismissed.v1`. Seeker stays excluded.

The guide shows D-pad/left-stick movement, right-stick aim, R1 tap/release and
hold/release shooting, L1 deploy/conditional hold-switch, and A/B/X/Y direct
power-up slots. Instruction cards are informational and cannot be selected. Only Continue and the
opt-out take focus. Left/right switches footer focus; up/down scrolls instructions. A selects the opt-out or Continue; B returns to
loadout without launching or spending fuel. No pause/restart/camera controls.

Five purple controller illustrations in `Art/controls-guide/psg1/` are bundled
through `Resources/Psg1BattleGuideArt.asset`. Existing hands/shells and selected
3D tank models are reused. Nine signed PNG bindings and static copy are declared
under the PSG1-only definition in `Content/ScreenUpdates/android-v1.json`, using
the existing Android OTA channel. Artwork verifies/preloads before login. Input,
navigation, opt-out storage and launch actions remain native.

A requested APK build containing this screen is required before publishing its
OTA definition; older APKs do not recognize the new screen ID. Set the minimum app
version to that compatible APK when publishing. Editor checks create no APK or
OTA release.

Run `BattleCities.Editor.Psg1BattleGuideChecks.Run` to prepare bundled references,
validate signed Android artwork/cache fallback, check navigation and
platform/preference/launch isolation, and render native and smaller TV previews.

## Web controls guide

`battle-guide` is Web-only and opens inside the TV after START BATTLE, before the
existing loadout-save/fuel-debit flow. Continue resumes that flow once. DON'T SHOW
AGAIN is saved in browser PlayerPrefs. Seeker keeps its current flow; PSG1 uses the
separate controller guide described below.

The page reuses the existing theme/title/footer/action skins and renders the actual
selected tank model. Five generated transparent PNGs are bundled as offline
fallbacks through `Resources/BattleGuideArt` and declared in
`Content/ScreenUpdates/web-v1.json`: keycap, tap, charge, normal-shell, power-shell.
All guide copy and these illustration slots have declared OTA bindings. Live tank
render textures and native key/launch/preference behavior are never remote images.

Web uses `Resources/ScreenUpdates/web-config.json`, the same pinned public signing
key, a separate `web-ui-v1` channel, and the independent
`https://assets.battlecities.com/web/screens/v1/latest.json` pointer. Browser WebCrypto
verifies RSA-SHA256 signatures; file hashes, compatibility, sequence checks and
complete-image activation remain enforced. Failed checks retain cached/bundled UI.

After the first compatible Web build, publish a strictly increasing Web sequence:

```powershell
rtk proxy powershell -NoProfile -File Tools/Publishing/Publish-ScreenUpdate.ps1 -Platform web -Sequence 1 -MinimumAppVersion 1.0.7
```

The publisher normalizes/validates artwork, adds read-only R2 CORS rules for the
game's Web origins while preserving unrelated rules, verifies immutable uploads,
and switches the signed Web pointer last. Android publishing remains independent.
No Web OTA release or CORS change was published during implementation.

Run `BattleCities.Editor.BattleGuideChecks.Run` for responsive Editor previews and
preference/continue/platform checks, and `node Tools/Publishing/Check-WebScreenSignature.mjs`
for browser signature verification and tamper rejection. These do not build a player.

## Migrated screens

All 18 existing player-facing screen definitions now contain supported display
content, rather than empty hooks:

| Screen ID | Content controlled by its definition |
| --- | --- |
| `login` | Authored headings, descriptions, entry instructions, controller hints, optional element bounds, fixed artwork, coverage scale |
| `home` | Navigation captions, instruction copy, HUD/leaderboard labels, controller hints, optional element bounds, fixed artwork |
| `settings` | Static row copy, row order and relative heights, gaps, normalized element regions, alert button captions, fixed artwork |
| `shop` | Category captions, product display names, inventory labels, normalized regions including checkout geometry, sidebar width, card gaps, fixed artwork |
| `ranking` | Static headings, tabs and column captions, normalized regions, payout sidebar width, row height, fixed artwork |
| `profile` | Static headings, controls and column captions, normalized regions, commander/battle-row proportions, fixed artwork |
| `quarters` | Section headings and card copy, normalized regions, grid spacing, fixed artwork |
| `treasury` | Static headings and holdings/history introduction, normalized regions, grid spacing, fixed artwork |
| `manual` | Category captions, existing entry names/roles, normalized regions, grid spacing, fixed artwork |
| `manualdetail` | Name, role, lore, effect and source for every existing manual entry; normalized regions and fixed artwork |
| `socials` | Page introduction and linking controls, normalized regions, grid spacing, fixed artwork |
| `select-tank` | Heading and tank display names, specification labels, normalized regions, card gaps, fixed illustrations |
| `loadout` | Heading, instructions and power display names, normalized regions, inventory/column proportions, fixed illustrations |
| `battle-results` | Static report/table labels, report width, minimum row height, fixed illustrations |
| `replay` | Viewer heading and controls, normalized regions, viewport padding, fixed artwork |
| `lobby` | Static lobby controls and instructions, panel side inset |
| `battle-hud` | Static metric labels, font scale |
| `pause` | Pause/outcome headings and action captions, controller help, controller menu row/button height |

The existing appearance is the baseline. Platform variants inherit `all` and can
override individual properties for `web`, `seeker`, or `psg1`. Seeker includes both
Android orientations; existing responsive layout code still fits the available area.
Only Android players fetch the production channel; Editor previews use local content.

The runtime preserves action delegates, native route/slot IDs, selection, scrolling,
and existing font/button treatments. API balances, offer prices, inventory counts,
wallet/session identity, payment confirmations, task verification and match values
remain live data. The home Commander readout is the live player name, including
after Shop wallet connection and saved-session restoration. Its region can move,
but its text cannot be overridden. The old `status-bar-player-readout-label` copy
key is accepted for signed-release compatibility and ignored by the player; it
is no longer included in the authoring catalog. Notification permissions and enabled state remain native. Checkout
geometry is editable; financial confirmation wording and payment behavior stay native.

This schema updates the display of existing controls and pages. Adding a new native
action, control type, route, gameplay system, plugin or SDK still requires an APK.

## Authoring

Edit `Content/ScreenUpdates/android-v1.json`. The complete key/default list is in
`Content/ScreenUpdates/bindings.csv`. The separate resource
`Assets/BattleCities/Resources/ScreenUpdates/default-screens.json` is the initial
offline baseline; don't overwrite that baseline for routine later OTA releases.

Each screen supports:

- `copy`: compiled static display keys and replacement plain text. Entry slugs,
  object names and action identifiers remain unchanged when captions change.
- `regions`: declared normalized top-left `[x, y, width, height]` regions. A `null`
  authored-element region keeps its local responsive layout. Numbers must stay
  finite and inside the parent. Existing layout-managed rows remain layout-managed.
- `layout`: declared numeric responsive settings with compiled safe ranges.
- `art`: declared fixed Image paths relative to that screen's root, plus semantic
  `entry-<slug>`, `power-<id>` and `tank-<index>` illustration keys. These semantic
  bindings preserve per-entry artwork when cards and loadouts refresh. A `null` value
  keeps local artwork. A replacement uses an image ID and renders with aspect fit.
  Status/identity art, QR codes and live render textures are not fixed illustrations.
- `patches`/`flows`: existing object overrides and weighted Settings row order.
  Reordered rows also update controller navigation. Full binding validation precedes
  applying these overrides; missing hierarchy targets retain the local view.

Use the canonical UI guide in `Assets/BattleCities/UI/AGENTS.md`. Preserve the shared
title/footer dimensions, approved fonts, artwork proportions and focus/activation rules.
Test the relevant platform variants before publishing changed spacing or long text.

For a new illustration, add an `images` entry with `id` and a repository-relative
`source` PNG, then set a declared `art` value (or patch `image`) to that ID. The publisher
computes its hash, byte size, dimensions and hosted URL; the source path is stripped
before upload. Limits are 16 images, 4096 pixels per side, 8 MiB per compressed PNG,
and 16 million decoded pixels total. The complete JSON document is capped at 256 KiB.
Screen PNGs are independent of the large remote-art bundle pinned to an APK.

## Publishing

Close Unity and use a sequence strictly higher than the current production release:

```powershell
rtk proxy powershell -File Tools/Publishing/Publish-ScreenUpdate.ps1 -Sequence 3
```

The example sequence is not reusable once published. The command checks the signed
current release, validates the exact normalized source in Unity, uploads and publicly
verifies immutable documents/PNGs, then writes the signed `latest.json` pointer last:

`https://assets.battlecities.com/android/screens/v1/latest.json`

Publishing does not build an APK. Roll back by publishing a known-good document with
a new, higher sequence using `-DocumentPath`. Never overwrite immutable hashed files.

## Trust and fallback

The APK contains a public RSA key. The private 3072-bit signing key stays protected
by Windows DPAPI in LocalAppData/BattleCities/ContentSigning. It is never committed
or uploaded. Preserve the user profile's protection material with that key; losing
it requires an APK with a replacement trust key. Initialize-ScreenSigning.ps1 reuses
the existing local key rather than silently rotating it.

Releases bind the renderer ABI, Unity version, channel, supported app-version range
(maximum exclusive), and monotonically increasing sequence. The client verifies
the original signed payload, content hashes and limits; it rejects redirects and
stale, incompatible or tampered releases. JSON and all referenced PNGs must verify
and decode before activation. No update activates during gameplay or wallet approval.

Cached releases are reverified at startup. Offline or failed checks keep the last
valid cache, then the built-in content. Files are cached by SHA256; only the current
release's images remain in memory. Disk cleanup of old hashed files is deferred.

## Verification

Run `BattleCities.Editor.ScreenMigrationChecks.RunComplete` in batch mode or the
Editor to execute signature/cache/fallback checks, all-screen migration checks and
baseline platform previews. It does not build an APK or call production wallet,
purchase or social mutation endpoints.

- `Builds/ScreenUpdateChecks/result.txt`: update/security/fallback fixtures.
- `Builds/ScreenUpdateChecks/migrations.txt`: all-screen copy/region/layout checks,
  real UI bindings on four layouts, authored prefab reuse and route preservation.
- `Builds/ScreenUpdateChecks/Previews`: baseline rendered previews.

Android cold-start downloads, airplane-mode fallback, wallet connection and a live
battle still need device verification after the APK is built. Editor checks do not
substitute for those device checks.

## Prepared production release

`screens-2` is published to the existing R2 screen channel. Its document is 95,539
bytes, SHA256 `6a949c4f2969a3f301aac24281a58689d72fcc191d7e96413e961e54a293ce86`.
It targets Android app versions 1.0.4 through (excluding) 2.0.0 on Unity 6000.6.0f1.
The installed 1.0.3 APK does not contain this runtime; the next APK must add it once.

Local verification passed 42 update/security/image checks and 224 migration checks
across four layouts. Twenty-four baseline platform previews were rendered and the
main screens reviewed. This is Editor verification; device verification remains
for the later APK build. Downloaded PNGs are retained through scene transitions
and disposed explicitly on release replacement/reset.
