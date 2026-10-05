# Publishing the game

The Unity repository owns the public game releases and web deployment:

- Game: https://play.battlecities.com/
- APK: https://github.com/CryptoSodi/BattleCitiesUnity/releases/latest/download/BattleCities-0.1.1.apk
- Releases: https://github.com/CryptoSodi/BattleCitiesUnity/releases

The APK filename deliberately remains `BattleCities-0.1.1.apk`. Its name is a stable download identifier; the release tag identifies the actual game version.

## Publish a new Unity build

Build Web and Android APK in Unity first. Keep the web build's `index.html`, `Build`, `TemplateData`, and other generated files together. Use the existing Android signing configuration so installed copies can update.

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Publishing/Publish-Game.ps1 `
    -Version 0.1.4 -WebBuildPath Builds/WebGL -ApkPath Builds/android/BattleCities.apk
```

Sign in to the GitHub CLI with an account that can publish releases in both repositories. The script:

1. Packages the web build and APK under the ignored `Builds/Releases/<version>/Publish` folder.
2. Validates their expected entry points and writes SHA256 checksums.
3. Uploads the APK, web ZIP and checksum manifest to a draft release in the Unity repository.
4. Updates the APK at the already distributed URL, preserving its repository, tag and filename.
5. Publishes the original repository's release, which triggers the Pages deployment workflow.

Use `-PrepareOnly` to package and validate without publishing, or `-WhatIf` to prepare the artifacts and preview the publishing action. An interrupted draft publication can be retried with the same version; an already published release requires a new version.

To publish a previously packaged build, use `-WebArchivePath` instead of `-WebBuildPath`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Publishing/Publish-Game.ps1 `
    -Version 0.1.3 -WebArchivePath Builds/Releases/0.1.3/BattleCities-0.1.3-Web.zip `
    -ApkPath Builds/Releases/0.1.3/Android/BattleCities-0.1.1.apk
```

The initial release on this repository reuses the already published v0.1.3 artifacts. Later source and UI edits require a new Unity build before publication.

## Pages deployment

`.github/workflows/publish-game.yml` runs on published stable releases. It downloads the web ZIP and `SHA256SUMS.txt`, verifies the ZIP checksum, validates and extracts the Unity site, then deploys with GitHub's Pages actions.

The published game fills the browser viewport on desktop and mobile, with no Unity
footer, build title, or separate maximize button. The custom BattleCities template
uses the same `battle-cities-viewport.css` stylesheet as the Pages preparation step.
Pages also applies it to older release archives after checksum verification, so a
presentation-only correction can be deployed without rebuilding the game or
replacing historical release assets. The stylesheet filename includes its content
hash to prevent a cached older layout from being reused. Browser chrome can still
be hidden with the browser's own fullscreen command (F11 on Windows).

It can also be run from **Actions → Publish game to GitHub Pages → Run workflow**. Enter a stable release tag such as `v0.1.3`, or leave the tag empty to deploy the latest release. No Unity license or external publishing token is needed for this deployment because it uses the already built release assets and the repository's `GITHUB_TOKEN`.

APK assets stay in GitHub Releases; build binaries are not committed to the Unity source branch.

## Play domain

GitHub Pages for `CryptoSodi/BattleCitiesUnity` uses the custom domain `play.battlecities.com`. Cloudflare's `play` CNAME points to `cryptosodi.github.io` with proxying enabled. The hostname's HTTP-to-HTTPS redirect preserves the requested path and query string.

The GitHub Pages address, https://cryptosodi.github.io/BattleCitiesUnity/, also leads to this domain. Future release deployments update the game at the same public address automatically. Domain settings belong in GitHub Pages and Cloudflare; the deployment workflow does not need a CNAME file.

## Existing public link

The publishing script keeps this exact distributed URL updated by default:

https://github.com/CryptoSodi/BattleCitiesUnity-Releases/releases/download/v0.1.1/BattleCities-0.1.1.apk

Do not delete that release or change its APK filename. `-SkipLegacyApkMirror` is available only when an explicit publication should leave that compatibility link on its current build. The old web site also remains available at https://cryptosodi.github.io/BattleCitiesUnity-Releases/.
