# Publishing the game

The Unity repository owns the public game releases and web deployment:

- Game: https://play.battlecities.com/
- APK: https://github.com/CryptoSodi/BattleCitiesUnity/releases/latest/download/battlecities.apk
- Releases: https://github.com/CryptoSodi/BattleCitiesUnity/releases

New Android outputs are named `battlecities.apk` or `battlecities.aab`. Starting with the next publication, the main APK release asset uses `battlecities.apk`. A compatibility copy named `BattleCities-0.1.1.apk` preserves the previously distributed links; the release tag identifies the actual game version.

## Windows batch files

Double-click these files in the project root. The window stays open with the result or error:

| File | Action |
| --- | --- |
| `Deploy-Web.bat` | Suggests the next release version, builds WebGL, validates the site archive, commits and pushes all non-ignored project changes, publishes a release, waits for Pages, and verifies the live domain. |
| `Build-Android.bat` | Offers APK or AAB, prompts for the version, increments Android's version code, and creates `battlecities.apk` or `battlecities.aab` locally. |
| `Commit-And-Push.bat` | Prompts for a commit message, stages all non-ignored changes, commits, and pushes the current branch to origin. |

Prerequisites: `unity`, `git`, Git LFS, and (for deployment) `gh` on PATH, a licensed installation of the Unity version in `ProjectVersion.txt`, and its WebGL/Android modules. Run `gh auth login` once for GitHub publishing. Git uses its existing credential helper. No Cloudflare login is needed for future web deployments.

Unity may be open or closed. With an open Editor, the build script stops Play Mode, switches the platform, saves the open scenes/assets, and polls the live Pipeline build. The project's `com.unity.pipeline` package must be connected. With Unity closed, it starts the matching installed Editor in batch mode. An open but unreachable Editor produces an error instead of opening a second Editor. An untitled scene must be saved first. Close the script only after it reports completion; a timeout requires checking the Editor before retrying.

Builds use fresh dated directories under `Builds/Releases/<version>/WebGL/` or `Android/`, and show their full output path. Requests, build reports, logs, and the automation lock are under ignored `Builds/Automation/`. Only one build/deploy/push launcher can run at a time.

Web deployment carries the existing released APK forward unchanged by default. It accepts the old asset name when carrying an older release forward, then stages it under the new name. To publish a newly built Android APK together with the new web release, pass its path explicitly. Publication also updates a compatibility copy at the previously distributed URL:

```powershell
.\Build-Android.bat -Version 0.1.10 -Format APK
.\Build-Android.bat -Version 0.1.10 -Format AAB
.\Deploy-Web.bat -Version 0.1.10 -ApkPath "C:\path\shown\by\the\build\battlecities.apk"
.\Commit-And-Push.bat -Message "Update gameplay and menu"
```

Double-clicking `Build-Android.bat` asks for the format first: `1`/`APK` (the default) or `2`/`AAB`. `-Format APK` or `-Format AAB` skips that choice. AAB creates an Android App Bundle for store upload; `Deploy-Web -ApkPath` accepts an APK. Both formats print their full local path, use fresh output folders, and increment Android's version code independently.

Android builds retain Unity's current package, graphics, architecture, and signing configuration. The current project uses Unity's default debug signing unless custom signing is configured in Publishing Settings. To update an installed APK, retain its signing identity. For custom signing, enter passwords in Unity before a live build; for a closed-editor build, supply `BATTLECITIES_ANDROID_KEYSTORE_PASSWORD` and `BATTLECITIES_ANDROID_KEY_ALIAS_PASSWORD` in the environment. Passwords are never written to the build request or passed as command-line arguments.

All three launchers accept `-CheckOnly`, which checks prerequisites without changing settings, building, staging, committing, pushing, or publishing:

```powershell
.\Deploy-Web.bat -CheckOnly
.\Build-Android.bat -CheckOnly
.\Build-Android.bat -Format AAB -CheckOnly
.\Commit-And-Push.bat -CheckOnly
```

For unattended use, pass `-Version` and `-Format` to Android (and optionally `-VersionCode`), `-Version` to web, or `-Message` to Git. Set `BATTLECITIES_NO_PAUSE=1` to suppress the batch window's final pause. Failures return a nonzero exit code. Git refuses detached HEAD, an unexpected origin, or a remote branch with newer/diverged commits; it does not force-push or merge automatically. Build binaries stay in ignored directories and GitHub Releases.

## Publish a new Unity build

Build Web and Android APK in Unity first. Keep the web build's `index.html`, `Build`, `TemplateData`, and other generated files together. Use the existing Android signing configuration so installed copies can update.

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Publishing/Publish-Game.ps1 `
    -Version 0.1.4 -WebBuildPath Builds/WebGL -ApkPath Builds/android/battlecities.apk
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
hash to prevent a cached older layout from being reused. Pages also replaces the
browser loading screen and favicon with the current Battle Cities logo and template
styles. The workflow downloads Git LFS assets, and preparation checks the logo's
PNG signature before deploying. Logo and style filenames are content-hashed too. Browser chrome can still
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
