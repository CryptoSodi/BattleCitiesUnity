# PSG1 controls

PSG1 keeps its native 1240 × 1080 orientation in menus and matches. Physical face labels follow the installed PlaySolana SDK: A is east, B is south, X is north, and Y is west.

| Screen | Controls |
| --- | --- |
| Login, home, dialogs, tank roster, loadout, online lobby | D-pad or left stick moves focus; A selects; B goes back. |
| Home | Up from Start reaches the commander/wallet, controls and Online row. Down reaches the bottom menu tabs. |
| Tank roster | Three columns; focus scrolls cards into view. Back and Continue are part of the navigation loop. |
| Room-code keypad | A adds a letter, number or hyphen. Delete removes one character; Clear removes the code; Done or B returns to the lobby. |
| Connecting / matchmaking | B or Cancel Connection cancels a pending connection; B leaves a waiting quick-match room. |
| Gameplay | D-pad/left stick drives; right stick aims; A fires or charges while held; B deploys the special attack; Y switches special; L1/R1 selects a power-up slot; X uses it. |
| Pause and help | Start pauses/resumes; Select opens/closes help; D-pad/stick chooses an action; A selects; B resumes. |
| Victory / game over | A activates the focused Next Stage, Restart or Main Menu action. B returns to Main Menu. Final-stage victory has no Next Stage option. |
| Debug panel | Open Debug from the offline pause menu. Up/down traverses controls; left/right adjusts sliders; A toggles or activates; B returns to pause. |

Focus uses a gold outline separate from a card's selected tank artwork. Closing a modal restores its previous control. Focus recovers if the selected control is disabled, hidden, or cleared. Scroll views reveal newly focused controls.

Shop, Quarters and Socials currently open placeholder dialogs; separate destination screens have not been built. External wallet applications manage their own input after handoff.

## Verification

In Play mode with the PSG1 simulator selected:

- **Battle Cities > Validate All PSG1 Screen Navigation** runs controller-only checks across Login, MainMenu, tank selection, lobby/keypad and the battle debug/pause flow. It does not log in, spend fuel, or connect to a room.
- `BattleCities.Editor.Psg1NavigationChecks.RunResults()` checks next stage, restart, final-stage victory and return-to-menu actions using simulation result fixtures.
- **Battle Cities > Validate PSG1 Controller Bindings** verifies the installed SDK's hardware-format button and axis mappings.
- `BattleCities.Editor.Psg1GameplayChecks.RunBattle()` checks live offline movement, aiming, fire, specials, pause/lobby transitions and disconnection cleanup.

Editor checks use simulated PSG1 SDK input events. Physical-device acceptance and a live multiplayer session still require hardware/network testing.
