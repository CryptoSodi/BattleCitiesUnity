# Photon multiplayer

## Installed

- Project: `C:/repos/Unity/BattleCitiesUnity`, Unity 6000.6.0f1.
- SDK: official Photon Fusion 2.1.3 Stable Build 2390.
- Photon application: Battle Cities, Fusion 2, `dedb8ea5-35ab-42a2-a8af-9d5461c4d72c`.
- Architecture: one player hosts; 60 Hz authoritative simulation, 30 Hz snapshots, up to four players.
- Rooms use the same application version (`battlecities-2`) and a region encoded in their join code. Change the application version when making incompatible network schema changes.

## Play

1. The build opens the **Login** scene first. Select **Continue as Guest** to receive a new random guest ID for this login session. The ID is passed to Photon as the local user ID; it is not verified authentication.
2. Launch with `--mode coop` or `--mode pvp`. Without a mode flag, co-op is selected. An invalid mode displays an error when Start is pressed.
3. On **Main Menu**, press **START**. Fusion joins an available public room in the selected mode or creates one automatically. A guest needs no room code or create/join choice. The selected launch mode is locked when `--mode` was supplied.
4. The match starts automatically five seconds after a second player arrives, or as soon as four players are present. Rooms close when the match starts. The waiting panel shows matchmaking progress and lets a player leave.
5. **ONLINE** remains available for private rooms: the host selects a map and region, creates a room, shares its code, and manually starts the match once friends join.
6. After a result, the host can choose **REMATCH LOBBY** and start again. **LEAVE ROOM** returns to the offline game and room menu.

Keyboard controls remain WASD to drive, arrow keys to aim, Space to fire/charge, Q to change secondary weapon, and E or right mouse to deploy it. Opening the room menu suppresses local gameplay input; the online match continues for everyone else.

| Mode | Rules |
| --- | --- |
| Co-op | Defend the base and clear existing enemy waves. No friendly fire. Each player has three lives and independent respawns. |
| PvP | No enemy waves or base-loss condition. Each player has three lives; the last survivor wins. |

All 35 playable maps (01–35) have four clear, non-overlapping starting positions. Rooms close to new joins when play starts and reopen in the rematch lobby. Disconnected clients are removed; a departing host ends the room.

## Implementation

- `BattleSession`: Photon connection, room-code validation, region, scene preparation and cleanup.
- `BattleLobbyUI`: uGUI create/join, mode/map/region selection, roster, start, results, rematch and leave.
- `BattleNetworkMatch` / `BattleNetworkTypes`: input exchange and replicated participants, tanks, shots, secondary weapons, terrain, pickups and combat events.
- `BattleSimulation.Multiplayer`: player ownership, independent lives, co-op/PvP targeting, safe spawns and victory rules.
- `BattleSimulation.Replica` / `BattleGame.Multiplayer`: client presentation and the bridge to existing gameplay.
- `PhotonMultiplayerSetup`: app/configuration and registered network prefab. **Battle Cities > Multiplayer > Configure Fusion** can recreate missing generated assets.
- `Resources/RuntimeShaders`: material references retain runtime-created shaders in standalone builds.

The host owns combat and movement results. Clients submit commands rather than health, position, score or inventory. The host remains a trusted player process; this is suitable for casual matches, not a trusted ranked economy. Online pickups are session-only, and persistent reward claims and paid inventory consumption are disabled online.

## Verified on 25 September 2026

- Windows development player build: **Succeeded, 0 errors**.
- Multiplayer rule checks: independent movement, co-op friendly fire, PvP damage, per-player lives and respawn, match results, player departure, and spawn/capacity validation on all 35 playable maps.
- Existing shooting/health and mine, drone, land-drone and turret regression checks passed.
- Two standalone Photon peers in **each mode**, starting from MainMenu: room creation/join, host start, remote movement and firing, replicated tanks, terrain destruction, events and advancing simulation all passed.
- Final co-op host observed 291 simulation units of remote travel and 6 shots; PvP host observed 294 units and 6 shots. Both clients received the expected world state beyond tick 420.
- Final smoke logs contain no runtime errors or exceptions. Test clients disconnected and exited. Machine-readable reports are in `Docs/MultiplayerValidation.json`.
- Login-first development builds passed the full **Continue as Guest → Main Menu START → Photon room** flow with both `--mode coop` and `--mode pvp`. Each run received a different random guest ID, and Photon used that ID for the connection. See `Docs/LoginLaunchValidation.json`.
- Quick Match uses Fusion `AutoHostOrClient` and `FillRoom` with a mode property. A visible open room is filled first; if none matches, Fusion creates a new host room. Only matches in the configured Photon region are considered. Co-op and PvP have separate queues.
- Four simultaneous standalone clients verified this flow: two co-op peers joined the same room, two PvP peers joined another room, and both matches started without codes, room creation clicks or a host Start click. See `Docs/QuickMatchValidation.json`.

The transport smoke uses a controlled arena with cleared terrain and frozen enemies to isolate networking. Simulation regression checks cover game rules separately. These tests used separate processes on the same Windows machine through the Photon Asia region; they are not a cross-device latency benchmark or a full four-player endurance test.

## Re-run checks

- Editor menu: **Battle Cities > Multiplayer > Validate Rules**.
- Editor helper: `PhotonMultiplayerSetup.BuildWindowsSmoke()` writes a development build to `Builds/Multiplayer/BattleCities.exe`. It accepts an optional output path.
- Start two copies with `--bc-smoke host <report-directory> coop` and `--bc-smoke client <same-report-directory> coop`; use `pvp` for PvP. Use `-batchmode -nographics -logFile <log-path>` for unattended testing. This harness only exists in Editor/development builds. Each peer writes its JSON report and exits after the test.
- Include BattleCity, MainMenu and Login in normal player build scenes. Keep the project's normal entry scene when producing a release build.

## Remaining milestones

- Full client movement prediction and reconciliation. Current clients display authoritative state with visual smoothing, so input responsiveness depends on round-trip latency.
- Cross-device PC/Android/PSG1 tests, controller/touch acceptance, loss/jitter tests and four-player endurance testing. Native Android code paths are present but no Android build/device validation was performed here.
- Host migration and reconnect/resume. A lost host currently ends the room.
- WebGL needs a separate topology/build decision; this Host Mode implementation explicitly disables browser connections.
- Ranked play and persistent rewards require a trusted backend or dedicated server.

Compiler warnings remain for APIs deprecated in Unity 6.6/Fusion compatibility callbacks and existing project tooling; there are no build errors.
