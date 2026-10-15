# Android identity fix: PKG-007

Date: 15 October 2026.

The Android Player Settings had no default application icon and empty adaptive icon slots. The plugin manifests and Gradle files did not override the launcher icon. An APK built from those settings therefore used Unity's default artwork.

## Branding and settings

- Reused the approved BC winged crest with the yellow C. The exact original PNG is retained as `Assets/BattleCities/Branding/AndroidIcons/official-bc-crest.png`.
- Derived the icon files by resizing, centring, compositing on the existing navy brand colour, and applying a round mask. No logo was generated or redesigned for this fix.
- Assigned the default application icon and all six adaptive foreground/background slots through Unity's Player Settings APIs.
- Unity 6000.6.0f1 marks the old Legacy and Round icon APIs as unsupported compile-time errors. The Editor post-export hook therefore supplies all six legacy/round density PNGs and the round application manifest reference in the generated launcher module. Android 26+ uses the same adaptive artwork for both icon references.
- Adaptive slot 0 is background and slot 1 is foreground in this installed Unity version. The export hook also checks that the actual generated foreground has visible and transparent pixels.
- Changed the display name from `BattleCitiesUnity` to `Battle Cities`, explicitly confirmed by the owner.
- Kept package `sol.battle.cities`, ARM64/IL2CPP, existing keystore and alias, and existing signing certificate.
- Bumped Android release version from 1.0.0 / code 1 to 1.0.1 / code 2.
- No gameplay, networking, scene, UI or wallet implementation was changed.

## Files

- `ProjectSettings/ProjectSettings.asset`: display name, version, default icon and adaptive icon assignments.
- `Assets/BattleCities/Branding/AndroidIcons/`: approved source, master exports, six density variants for legacy/round/adaptive layers, provenance and Unity import metadata.
- `Assets/BattleCities/Editor/AndroidApplicationIcons.cs`: configuration menu, Android-only build validation and generated legacy/round resources. Missing/unofficial assignments fail the build instead of silently falling back.

Run **Battle Cities > Android > Configure Official Application Icons** to reapply the settings. The utility does not alter package ID or signing settings. The existing signed Android build script remains unchanged.

## Build and APK verification

Built using the installed Unity 6000.6.0f1 Android environment and existing `tools/Publishing/Build-SignedAndroid.ps1 -Version 1.0.1 -VersionCode 2` workflow.

Final artifact: `Builds/Android/1.0.1-v2/20261015-084341/battlecities.apk`.

- Unity export succeeded with zero build errors; Gradle release build and APK signing succeeded.
- Android SDK `aapt` confirms package `sol.battle.cities`, label `Battle Cities`, version 1.0.1 and code 2.
- Manifest application icon and round-icon references resolve to the official packaged resources.
- Both adaptive XML resources resolve to the intended foreground and background.
- Extracted and decoded every packaged PNG for all six densities: all 24 images match the approved derived artwork, including foreground transparency. Resource obfuscation filenames were resolved through the compiled resource table.
- `apksigner` and `zipalign` verification passed. The signer matches the pre-existing Battle Cities certificate, SHA-256 `d3673cb75cdf37466d9ced1d1ffd23def10034ed8be2f08451204cf79f8f88a4`.
- APK SHA-256: `1091318c730706b8a51ba908179c8139afdf3078d16c4ec05943e784de70154e`.
- Detailed local reports: `release-verification.json`, `signature-verification.txt`, `apk-metadata.txt`, and `icon-verification/icon-verification.json` beside the APK.

The APK was installed successfully on the attached Samsung SM-S918B. Visual launcher/Settings checks are recorded separately after the device is unlocked. This device is not a Solana Seeker; no Seeker-specific installed-device verification is claimed.

Reference: [Unity Android Player icon settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-PlayerSettingsAndroid.html). The installed 6000.6 APIs and actual APK resources were used to resolve differences from older Unity documentation.
