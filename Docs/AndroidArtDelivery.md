# Android artwork delivery

Signed Android builds use an APK-pinned, SHA-256-verified `game-art` bundle.
The APK retains the startup splash, the font needed for download status, code,
and non-image runtime data. The login, main-menu, and battle scenes retain their
names and build indices, but their authored scene roots are captured together in
remote prefabs. Keeping the roots together preserves references between them.
The login scene shell displays the local splash while it checks screen updates,
downloads or verifies the bundle, and loads the captured login roots. Subsequent
scene shells instantiate their roots synchronously from the already loaded bundle.

Image-bearing Battle Cities `Resources` roots are moved into the bundle during
the Android build. Runtime consumers use `RemoteGameArt.Load`, which still falls
back to `Resources` in the Editor and in self-contained or Web builds. The build
restores staged source assets even on failure, and the recovery journal supports
an interrupted Editor process. `Battle Cities/Content/Recover interrupted build`
restores a recorded interrupted operation; do not delete the staging folder or
journal manually when either is present.

The APK build refuses to succeed if any project texture other than the splash
is still packed locally, or if a non-startup OTA texture appears in the APK.
Engine-generated resources and the startup font remain local. The launcher uploads
the resulting immutable bundle and verifies its size and hash before installing or
publishing the APK. A cold install needs network access to fetch the bundle; a
verified cached bundle can be reused offline.

`RemoteSceneCaptureChecks.Run` checks scene hierarchy and serialized references,
the local startup shell, and the image-bearing Resources plan without building a
player. `RemoteArtDependencyAudit.Run` writes a scene/Resources texture dependency
report under `Builds/RemoteArtChecks`. Before release, build and inspect an Android
APK, verify its public bundle, and test cold download, cached/offline startup,
login, main menu, battle, wallet checkout, PSG1 controls, and online multiplayer.
