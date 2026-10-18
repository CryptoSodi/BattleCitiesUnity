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

After successful sign-in, the existing login status shows download progress.
The menu opens only when the complete artwork and model pack has been verified and loaded.
A failure keeps login visible; Connect Wallet retries the download without asking
the wallet to sign again. Downloads use HTTPS, disallow redirects, enforce expected
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
