# Standard keyboard and PSG1 controls

The main game supports keyboard controls equivalent to PSG1. No test mode, URL flag, controller or PSG1 device is required. Keyboard support does not change wallet, network, token or device identity.

| PSG1 control | Keyboard | Behavior |
| --- | --- | --- |
| Left stick / D-pad | W A S D | Drive up, left, down, right |
| Right stick | Arrow keys | Aim turret independently |
| R1 | Space | Tap and release to fire; hold 1 second and release for a power shot |
| L1 | E | Tap and release to deploy; hold 1 second to switch if multiple specials are available |
| A | 1 | Consume equipped power-up slot 1 |
| B | 2 | Consume equipped power-up slot 2 |
| X | 3 | Consume equipped power-up slot 3 |
| Y | 4 | Consume equipped power-up slot 4 |

Q remains the existing direct switch shortcut for tanks with multiple specials. Use E to test the actual PSG1 tap/hold interaction. Current tanks each have one special, so holding E will not switch; releasing deploys it. Normal charge, cooldown, stock and server checks still apply. Without turret input the turret follows movement, after firing in the released aim direction.

These controls are enabled by default in the main game and in Unity Play mode. Focus the Game view when using Unity.

Existing builds need rebuilding to include these changes. No query parameter or launch argument is needed.

Menu focus navigation is standard on login, home, dialogs, Shop, Rankings, settings, tank/loadout selection, lobby/keypad, the controls guide and results. Use arrows or WASD to navigate, Enter for A/select and Escape for B/back. Focus highlights and automatic scrolling use the PSG1 navigation code. Number keys are face-button equivalents during gameplay, not menu navigation. Keyboard support leaves the current screen layout, wallet and currency unchanged; use the PSG1 Device Simulator profile when also testing the console layout.
