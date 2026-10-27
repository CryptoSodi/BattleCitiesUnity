# Battle Cities Builder

Double-click **Build-Launcher.bat** in the Unity project root, or **Start.bat** in this folder.
This is a Windows terminal application. It reuses the project's existing Unity, signing, SSH and GitHub publishing tools.

## Web

1. Choose Web.
2. Increment, keep, or manually enter the game version.
3. Choose build-only, localhost, Play, Test, Magic, or all three websites. Enter multiple numbers to combine individual destinations, including localhost.
4. Review the selected steps and choose Build/run, Preview only, or Back.

One WebGL build serves all destinations. Play selects mainnet and the production API. Test and Magic select devnet and the test API. Localhost also selects the test API.
Production publication creates a **web-only** GitHub release and waits for Pages and the live version check. It never uploads/carries an APK or changes the latest Android download. Existing release tags are not overwritten.

Localhost binds only to this computer, opens the browser, and serves the exact new output. It refuses an occupied port instead of showing an older build. The server continues after the menu closes; its PID, port, output root and identity are in that run's `localhost-process.json`. Stop that PID when finished, or change `localhostPort` for another server. It cannot be accessed from your phone over Wi-Fi.

## Android

1. Choose Android APK and the game version.
2. Choose build-only, phone installation, GitHub publication, PSG1 draft upload, or all three.
3. Enter the Android build/version code (next local code is suggested). With installation selected, choose the connected authorized device.
4. Review and run.

Game version (such as `1.0.19`) is separate from Android's integer version code (such as `18`). Android updates must not downgrade an installed version code. Published Android tags must have a new version/code combination. Check the store console's highest version code before a store release; the launcher cannot see codes uploaded outside this repository.

The launcher builds the signed mainnet ARM64 APK using the existing Battle Cities identity. It uploads the exact hash-named OTA artwork to the existing R2 bucket and downloads it again to verify every byte **before** installing or distributing the APK. Build-only still hosts this required artwork so the APK can actually start; it does not publish an APK.

Close Unity before an Android OTA build; the existing artwork staging/signing pipeline requires a closed Editor. Web builds can use an open, connected Editor or batch mode.

The terminal shows the local APK path/file link. Selecting GitHub also returns its version-specific download URL. Phone installation uses `adb install -r`, verifies the installed version, and launches the game without uninstalling it or changing network settings.

PSG1 uploads use PlayGate's official MCP API: read the current draft, obtain a short-lived upload form, upload and checksum-verify the APK, then attach it and require successful automatic analysis. This updates the **draft only**, preserving other dashboard metadata/builds. It never submits for review, accepts terms, signs a wallet message, or uploads signing keys. If the agreement/wallet/account needs attention, resolve it in PlayGate and rerun.

## Configuration and credentials

Choose Settings in the menu. Local preferences are saved in ignored `settings.local.json`; `settings.example.json` lists supported fields. No passwords or tokens belong in either file.

- Unity CLI and the Editor version in `ProjectVersion.txt`, with WebGL/Android modules and a valid Unity license.
- Git/Git LFS, GitHub CLI (`gh auth login`), Python 3.11+ and Cloudflare `cf` CLI authenticated for the existing artwork bucket.
- Existing Android signing files under `D:/keys`, accessed by the same Windows account that created their DPAPI credentials. Credentials still enter the signer through stdin, never command arguments or project files.
- Test/Magic deployment uses `BATTLECITIES_TEST_SSH_KEY`, a local `sshKey` override, or the existing key in Downloads. Existing SSH host-key checks stay enabled.
- `PLAYSOLANA_MCP_TOKEN` must exist in the process or Windows **user** environment, as already configured for Codex. It is sent only to `https://mcp.playsolana.com/mcp`; it is never saved to logs/configuration or sent to the CDN.

The Python HTTPS helpers try normal DNS first and use Cloudflare DNS-over-HTTPS only if hostname resolution fails. They retain hostname/certificate validation and never change Windows/phone DNS. This is confined to this launcher; it does not modify the game's downloader.

## Logs and failures

Runs live in ignored `Builds/Launcher/<timestamp-id>/`: `plan.json`, `launcher.log`, and `result.json` record the artifact, completed steps and URLs. A failure stops subsequent destinations and preserves the completed steps and build. No force-pushes or automatic release overwrites occur. Inspect the receipt before retrying a partially published job. Automatic resume is intentionally not implemented: a new run is a new build; use the recorded publishing scripts/artifact paths for a targeted retry.

Publishing commits and pushes **all non-ignored project changes**, just like the existing release scripts. Build-only and localhost do not commit or push. One launcher run is allowed at a time, and existing build locks prevent competing Unity builds.

## Command-line use and verification

```powershell
.\Build-Launcher.bat -CheckOnly
.\Build-Launcher.bat -PlanPath C:\path\plan.json -DryRun
.\Build-Launcher.bat -PlanPath C:\path\plan.json
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/BuildLauncher/Test-Launcher.ps1
python -m unittest discover -s Tools/BuildLauncher/tests -v
```

Example plan (version/code are examples; choose the next actual values):

```json
{"kind":"web","version":"1.0.19","androidCode":0,"serial":"","targets":["play","test","magic"]}
```

```json
{"kind":"android","version":"1.0.19","androidCode":18,"serial":"YOUR_ADB_SERIAL","targets":["install","github","psg1"]}
```

`-DryRun` validates and lists steps without building, changing settings, committing, uploading or contacting publishing services. `-CheckOnly` checks local tools/settings only. Tests use mock build/publish adapters and temporary files; they do not release an APK or website.
