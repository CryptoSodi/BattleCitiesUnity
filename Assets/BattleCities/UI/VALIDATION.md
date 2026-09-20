# Main Menu validation — 2026-09-20

Completed:

- Unity compiled the new runtime and editor scripts and created `Assets/BattleCities/Scenes/MainMenu.unity` through the Editor API.
- The menu was added as the startup scene; `BattleCity.unity` remains in build settings.
- `MainMenuChecks.Validate()` passed scene wiring, bundled font, explicit navigation-link, platform binding and layout-bound checks across six viewport/platform combinations.
- Unity rendered Web (1600x1067), PSG1 (1240x1080), and Android (940x1672) captures. All three were visually inspected; a Start-button overlap was found and fixed, then the three captures were regenerated and inspected.
- The Play button passed the game-asset alpha report (RGBA, 1400x335, transparent pixels present, no reported problems).
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q` succeeded: 2 projects, 0 errors, 26 warnings. Warnings include existing Unity package/framework references and deprecated object-query APIs.

Pending:

- End-to-end input and gameplay-transition verification. The first gamepad test held the D-pad longer than the repeat delay; the test pulse was shortened. During the subsequent combined input/scene-transition run, Unity 6000.6.0f1 reported a native crash in `ParallelFilterChangedInstancesAndCreateScriptingArray`. There was no final completed interaction-test verdict. Input checks are now separated from scene loading.
- Unity was reopened, but is waiting at its `Scene Backup Detected` dialog. The desktop helper failed to initialize, so recovery needs the user's interaction before continuing Editor tests.
- Physical PSG1 and Android testing; no APK was built or installed during this task.

Integration limitations:

- Quarters, Shop and Socials are navigation entry points with not-yet-converted dialogs, not implemented destination pages.
- The leaderboard shows an honest unavailable state; backend, wallet and reward-service integration remain separate work.
- START is wired to load the existing gameplay scene; the post-transition runtime remains unverified after the native editor crash.
