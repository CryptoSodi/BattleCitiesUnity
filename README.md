# Battle Cities

Unity source and public releases for Battle Cities.

- [Play in your browser](https://play.battlecities.com/)
- [Download the Android APK](https://github.com/CryptoSodi/BattleCitiesUnity/releases/latest/download/battlecities.apk)
- [Browse releases](https://github.com/CryptoSodi/BattleCitiesUnity/releases)
- [Publishing instructions](Docs/Publishing.md)

Windows shortcuts in the project root:

- `Deploy-Web.bat`: build and publish the web game, including committing and pushing the source.
- `Build-Android.bat`: choose APK or AAB and build locally (`battlecities.apk` or `battlecities.aab`).
- `Commit-And-Push.bat`: commit and push all current non-ignored changes.

Open the project with the Unity version recorded in `ProjectSettings/ProjectVersion.txt`.

Web builds deploy automatically to GitHub Pages when a stable release is published. APK downloads use GitHub Releases, and the previously distributed APK URL remains supported.
