# Smaller Android APKs with remote artwork

This is the first stage of content delivery. It moves seven artwork collections
(SettingsArt, PlayerProfileArt, OperationsArt, LoadoutArt, ShopArt, PreBattleArt,
BattleResultsArt), battle models, the environment palette, replay rig, land drone,
terrain materials, and their textures/dependencies into one Android AssetBundle. All game
code, scenes, maps, login, fonts and required startup content remain local.
Assets still referenced by local content remain in the APK; actual savings must
be measured from builds. Source PNG/GLB sizes do not predict APK savings.

## Local build

Close Unity. Use the existing signed Android build script with an HTTPS base URL:

```powershell
Tools/Publishing/Build-SignedAndroid.ps1 -Version 1.0.1 -VersionCode 2 -RemoteArtBaseUrl https://assets.battlecities.com/android/art
```

The URL above is the production R2 location, provisioned on 2026-10-17.
Use the next valid version/code for a real release. Reusing a version/code is only
for local comparisons. The output directory contains `battlecities.apk`, signing
verification, and `RemoteArt/content-manifest.json` plus a SHA256-named `.bundle`.
Only the SHA256-named bundle needs hosting. The manifest is embedded in the APK.
Keep the bundle and manifest together with that APK's release records.

Omit `-RemoteArtBaseUrl` to build the ordinary self-contained APK. Web and Editor
workflows retain their current local artwork. No new package or Unity runtime is
required. Remote artwork builds currently require the closed-editor batch path.

The build temporarily moves the content roots outside Resources using
AssetDatabase (preserving GUIDs), builds the bundle, then builds the APK with an
embedded immutable manifest. It passes the bundle manifest into the player build
to preserve remotely referenced types. A finally/disposal path restores the assets
and removes the generated manifest. A build report check rejects duplication of
the remote content roots and all GLB/GLTF/FBX/OBJ source models in the player.

A generated RemoteBattleModels catalog captures the authored BattleCity model
references. A scene build processor clears those references only in the player
build copy; BattleGame restores them from the loaded catalog before initialization.
The authored scene remains unchanged. The replay rig retains its own references
inside the bundle. Ordinary builds continue using local scene and Resources assets.

If Unity or the machine crashes during staging, do not build or play until running
`Battle Cities > Content > Recover interrupted build`, or the batch execute method
`BattleCities.Editor.RemoteGameArtBuild.Recover`. The journal is
`Builds/RemoteArtRecovery.json`. Do not delete it or the staging folder manually.

## First launch and cache

Before session restoration or sign-in, the opaque startup screen shows download,
verification and preload progress with a percentage and bar. Login stays hidden
until the complete artwork and model pack has been verified and preloaded.
Failures retry on startup, without using Connect Wallet as a download action.
The downloader waits while Android reports no reachable network interface, then
attempts HTTPS. Connection/temporary HTTP failures show a connection retry message;
verification failures show a distinct content message. Retries back off from 3 to
30 seconds. Diagnostics preserve Unity's result, HTTP code, native error and bytes
received; HTTP 0 means no HTTP response was received, not proof of a host outage.
Downloads use HTTPS, disallow redirects, enforce expected
size and SHA256, and stage into a partial file before promoting a complete cache.
The same APK reuses verified cached content on later launches, including without
asset-host access. Login and online game services still have their own network needs.
Clearing app data or an OS/user cache removal requires downloading again.

First download requires connectivity and enough disk space for the bundle.
The downloader does not support byte-range resume yet; completed downloads are
reused, but an interrupted bundle download restarts. Existing hashed bundles are
kept so APK rollback can reuse its content. Cache cleanup can be added later.

## R2 production hosting (2026-10-17)

Production bucket: `battlecities-game-assets` (Standard storage).
Custom domain: `assets.battlecities.com`, ownership and TLS active; minimum TLS 1.2.
First verified hosted object:
`android/art/a8e6bdf233f793a3fa2c4a45d423d1df5cc64d588bf6e3a447144815cd6f0934.bundle`
It matches the manifest from `Builds/Android/1.0.1-v2/20261016-095133`.
Public HTTPS download verified: HTTP 200, 66,628,993 bytes, exact SHA256 match.
Only this release's bundle has been uploaded; older comparison APKs may refer to
unhosted hashes. Upload each release's exact bundle before distributing its APK.

Use `cf r2 objects put <key> --bucket-name battlecities-game-assets
--content-type application/octet-stream --file <bundle-path>` for uploads.
Retain every bundle referenced by a released APK. Never overwrite a hash-named file.
Verify the public download's size and SHA256 against `content-manifest.json`.
The current cf object-upload command does not expose Cache-Control metadata.
CDN caching remains pending: the current OAuth grant cannot access zone rulesets
(HTTP 403), and the public response currently reports `cf-cache-status: DYNAMIC`.
Hosting and HTTPS downloads work without that optimization.

Android first-download, interrupted-download and retry tests against this live host
remain pending. Previous device tests used a manually preloaded bundle.
The manifest remains embedded in the APK; this does not enable live content revisions.

## Browser delivery diagnostics

Open **https://assets.battlecities.com/health.html** on the computer or phone
experiencing the problem. This standalone page is hosted in the same production
R2 bucket. Its script and styles are included in the HTML so checks can still run
if later resource requests fail after the page opens. It does not launch Unity.

Quick checks request fresh, cache-busted responses for the small host probe, the
diagnostic catalog, 64 KiB from the exact configured APK bundle, and the Android
screen-update pointer. The sample's SHA256 and Content-Range total are checked
against values generated from the locally verified bundle. The optional full
download checks every byte and the full SHA256; it can be cancelled.

Use **Repeat quick checks every 15 seconds** to capture intermittent failures.
**Copy report** includes the last 20 runs, browser, online hint, UTC timestamps,
HTTP status, response/download timings, bytes, and available Cloudflare headers.
The online hint is not proof this hostname is reachable. Browser failures without
an HTTP response must not be labelled conclusively as DNS or a server outage.
Browser and native Android networking can behave differently.

If DNS prevents the page itself from opening, retain the browser's exact error
code and time. This page cannot diagnose a failure that prevents its HTML loading.
The bare asset-domain root intentionally has no homepage.

After hosting a new APK's immutable bundle, update the diagnostic target:

    Tools/Publishing/Publish-AssetHealth.ps1 -ArtManifestPath <build>/RemoteArt/content-manifest.json -Publish

Without -Publish, this prepares the config, probe and standalone HTML locally.
The script verifies the full local bundle against the APK manifest before deriving
the sample hash. It uploads only health.html, health/config.json, health/ping.json
and the small bundled font through cf. No game build, signed screen pointer,
existing asset, DNS or CORS configuration is changed. The page displays its
configured APK version/code; it does not claim to check every historical APK.

Browser regression checks: Tools/Publishing/Test-AssetHealth.cjs (Playwright).
Use --live to check the deployed page and perform one full download.

## Future updates

This phase reduces APK size. Its content is pinned to each APK, so it does not yet
switch existing installations to a new artwork revision over the air. A later
versioned catalog can enable compatible content updates. New C# features require
a separate code hot-update architecture; this work does not install HybridCLR.

## Checks

Run `BattleCities.Editor.RemoteGameArtChecks.Run` in batch Unity. It checks valid,
missing, truncated and corrupt cache files, manifest validation, engine compatibility,
and ordinary local resource fallback. Results are in `Builds/RemoteArtChecks`.
These checks do not substitute for a real Android first-download test.

### Delivery investigation (2026-10-20)

Android 1.0.7 / version code 7 logs showed repeated immediate download failures
with HTTP 0. Screen update requests also failed during that startup. That build
discarded the transport error text, so DNS, TLS and other connection causes cannot
be distinguished retrospectively. Its connected device subsequently resolved and
reached the asset domain. R2 domain ownership/TLS were active. A public download of
`439dd29263d9c5e213c6a8471a3d54d6cab7b8d73a88c9a18fa49d36df49fc31.bundle`
returned HTTP 200, exactly 66,628,406 bytes and the expected SHA256; first byte was
received in 0.55 seconds and transfer finished in 24 seconds from the workstation.
This confirms the current object is available; it does not reproduce or explain
the phone's earlier transport failure. Improved native diagnostics require a new
APK when a build is requested. No hosting configuration was changed.

The subsequent signed diagnostic APK 1.0.7 / version code 8 reproduced the delay
and logged `ConnectionError, HTTP 0, received 0 bytes. Cannot resolve destination host`
for both screen updates and the artwork request. This identifies DNS resolution
as the failing stage; the cause of the resolver failure is still under investigation.
The same phone's shell curl could resolve the HTTPS domain and receive HTTP 200.
The application later recovered and began transferring the new bundle. Do not
describe the diagnostic/backoff changes as a DNS fix or bypass HTTPS verification.
The v8 bundle is
`cbab26dc6d28d30080d3da33dcb784ed405dc841e209e0c5fde65a10ba814f3d.bundle`,
66,629,393 bytes, uploaded and verified against its embedded manifest.

During the next build on 2026-10-21, workstation curl also failed to resolve the
asset domain. `Resolve-DnsName` through the default resolver returned
`RCODE_SERVER_FAILURE`; querying `1.1.1.1` returned the expected Cloudflare addresses
`172.67.219.12` and `104.21.86.121`. This reproduces a resolver-path failure outside
Unity too; it does not establish which upstream component caused it. No device or
system DNS setting was changed. A temporary curl `--resolve` can verify a hosted
object with normal hostname/TLS checks while investigating DNS; never disable TLS
verification or pin these CDN IPs into the app.

## Local model extraction result (2026-10-16)

Signed comparison APK: 142,546,391 bytes original; 126,958,799 bytes artwork-only;
101,213,643 bytes with models (29.00% below original). Combined content bundle:
87,147,158 bytes. Build directory: Builds/Android/1.0.1-v2/20261016-075729.
The first complete APK plus content download is larger than the original APK;
this change reduces the initial APK size, not total downloaded bytes.

Validated: no 3D source model or remote root in the player report, 21 model
binding/local fallback/scene preservation checks, 12 cache/configuration checks,
APK signature, alignment, certificate identity, and release configuration.
R2 upload and Android first-download/rendering tests remain pending.


## Android ASTC experiment (2026-10-16)

Tested 197 PNG texture imports: UI ASTC 4x4, backgrounds/other color textures
ASTC 6x6. Resolutions preserved; embedded GLB textures, launcher icons, editor
previews and data textures excluded. All 33 content checks passed.

Before: APK 78,333,387 bytes; bundle 66,630,303 bytes.
ASTC test: APK 83,774,589 bytes; bundle 72,791,685 bytes.
Combined download grew by 11,602,584 bytes, despite the logo imported texture
shrinking from about 24 MB to 6 MB. Previous Android import settings restored
because this task prioritizes download size. No device visual test performed.
Test artifacts: Builds/Android/1.0.1-v2/20261016-083633.
Metadata backups and comparison: Builds/TextureCompression.

## Runtime shader retention (2026-10-16)

Device testing reached BattleGame.Awake after successful wallet login, then
failed with ArgumentNullException for Shader.Find("Universal Render Pipeline/Lit").
Moving model references out of the player removed the implicit shader dependency.
RuntimeShaderBuild now ensures a texture-free, instancing-enabled BattleLit
material remains in Resources/RuntimeShaders (outside the remote Terrain folder).
Detailed player builds verify both material and shader appear in packed assets.
Do not remove this material during future APK size reductions.

## OTA screen releases

Android 1.0.4 adds a separate signed screen-definition channel. See [OTA screens](OTAScreens.md). The large art/model bundle remains APK-pinned; compatible screen text/layout/PNG changes use small independent files and do not rebuild the APK.
