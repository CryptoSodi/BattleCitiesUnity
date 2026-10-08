# Input recording and replay

Every offline battle stage and every authoritative multiplayer-host round is recorded automatically. A recording contains simulation inputs, not video. Online replicas are presentation-only and do not create incomplete input recordings.

## Watching a battle

- Open **Player Profile → Recent Battles → WATCH**. The same public endpoint supports your own matches, other players, and guests.
- Playback stays inside the Main Menu TV. The surrounding menu remains visible and the gameplay scene is never loaded. Pause/Resume, Restart, and 0.5× / 1× / 2× / 4× speed use the shared white footer; the shared Back tab or cancel returns to the same profile page, scroll position, and selected match.
- Gameplay, pause/results menus, and profiles have no standalone REPLAYS button or replay library. Automatic recording and upload continue in the background.
- A missing recording shows **NO REPLAY**. Damaged, incompatible, or legacy browser-engine recordings show an honest error; they do not open a full-screen or external viewer.
- The TV renderer has no economy, multiplayer session, or combat input components. Playing a recording cannot consume inventory, roll new drops, redeem currency, submit a new result, or create a new recording.

The lightweight `Resources/TvReplayRig.prefab` contains only the authored `BattleGame` visual settings. Rebuild it through **Battle Cities → Rebuild TV replay renderer** after changing the BattleCity scene's renderer configuration. Layer 31 is reserved for replay visuals; camera, light, reflection, and draw-call isolation prevent those visuals from appearing outside the TV. Closing playback restores other cameras' masks, disposes its render texture, and unloads its renderer scene without switching the active menu scene.

## What is saved

`Application.persistentDataPath/Replays/<id>.json` holds an archive containing the replay and upload state. WebGL flushes the persistent filesystem to IndexedDB. The newest 20 local archives are retained; unsubmitted server-session evidence is not automatically pruned. Test archives use an editor-only directory override, not the player's saved recordings.

Each stage/round has its own recording. Finishing, restarting, changing stage, or leaving the scene finalizes it. Aborted recordings remain viewable. The maximum is 108,000 ticks (30 minutes), 10,000 input changes, 4,096 external events, or 2 MiB of serialized replay data. Hitting a recording limit marks partial evidence as truncated; incomplete evidence is never submitted as a completed competitive result. An abrupt process crash before finalization can lose the current recording.

The versioned `battlecities-unity-input-v1` format stores:

- A self-contained map, resolved dimensions, initial tank and weapon configuration, engine/build version, and RNG seed.
- Run-length encoded input commands for all four slots at 60 Hz. Movement, aim, fire, charged shot, secondary fire, secondary selection, and enemy-fire debug state are represented.
- Ordered external events before their exact simulation tick: approved inventory effects, asynchronous pickup responses, and multiplayer participant/start changes. Currency claim credentials are excluded.
- SHA256 state checkpoints at tick 0, every 60 ticks, and the final tick, including hidden timers, RNG, entity order, terrain damage, and drone path history. Divergence stops playback with the first mismatching checkpoint. Claimed outcomes are independently compared at the end.

Content and state hashing sort object keys ordinally and preserve array order. Floating point values are represented in the hash input as `"f32:<eight lowercase IEEE-754 hex digits>"`; stored simulation fields are hashed, excluding derived presentation properties. This avoids Mono/CoreCLR decimal-format differences. Payload hashes used by the API are separate server-computed hashes of the uploaded JSON. Increment `BattleReplay.SimulationVersion` whenever gameplay rules or hashing semantics change, and retain compatible verifier binaries for historical recordings.


## Checkpoint hashing performance

The checkpoint hasher streams canonical JSON through a buffered UTF-8 writer into SHA256. Cached contracts sort typed-object fields once per type; arrays keep their original order. This avoids building and sorting two complete JSON trees and allocating a complete JSON string and UTF-8 byte array for each checkpoint. Dictionaries, extension data, JTokens and custom converters retain a local canonicalization fallback.

Hash contents, the `f32:` representation, simulation version and checkpoint schedule are unchanged. Native gameplay recording captures a detached state graph synchronously at the checkpoint boundary, then hashes it on a worker. Every mutable nested entity, collection, terrain setting and route point is copied; workers never read live simulation state or call Unity APIs. Only the game thread writes checkpoint results, in capture order.

The worker queue retains at most two snapshots and processes hashes sequentially. If a recorder is driven faster than its worker, it waits for the oldest checkpoint instead of dropping evidence or growing memory without bound. Tick zero and reseeding remain synchronous. Finish drains all pending work before score caching, saving or uploading; a failed hash retries the same detached snapshot synchronously, and an unrecoverable error prevents the archive from being saved or submitted. WebGL players and the standalone verifier use synchronous hashing. Replay verification and result checks remain enabled.

`ReplayHashChecks` compares the streaming hash with an independent copy of the previous algorithm, including all 35 evolving maps, single-player/co-op/versus states, field selection, Unicode and escaping, cultures, dictionary order, converters, signed zero and non-finite floats. It also checks snapshot mutation isolation and background-recorder ordering, reseeding, queue backpressure and finalization across all three modes. It runs through both the Unity **Input replay** check and the standalone verifier's `--self-test` command. Timing numbers must be measured separately on the target device; passing compatibility checks is not evidence of a frame-rate improvement.

Android players request 60 FPS before the first scene loads. Simulation and replay timing remain fixed at 60 ticks per second; checkpoint scheduling follows simulation ticks. Actual rendering performance depends on the device and scene.

## Backend and verification boundary

The client requests `POST /api/replay-sessions` before its first simulation tick when an API/account is available. It applies the issued seed and binds the recording to the session's mode, level/content/config hashes, tick rate, build and simulator version. Failure leaves a local recording. Completed archives upload to `POST /api/replays/unity`, idempotently by session ID. A completed, debug-free single-player recording then links through `POST /api/matches/submit`; match-link failures are independently retriable.

Successful upload or replay reproduction does **not** prove fair play. Map/config choices, external power-up events, client input legitimacy and multiplayer-host authority still require trusted checks. SHA256 detects differences, not an attacker who controls both the evidence and its hashes. The API keeps competitive approval pending for review; no client route can award points or prizes by calling its own recording verified.

The API implementation lives in `C:/repos/BattleCities-API`. Its migration `032_unity_input_replays.sql`, public match WATCH retrieval, authenticated owner/admin archive retrieval, idempotence, bounded validation, and reproduction-review hooks are separate from the Unity build. This change does not deploy either repository. Roll out the migration/API and matching game build together. Until the new API is deployed, recordings remain available locally and unsupported API requests fall back without interrupting gameplay.

The public WATCH route returns replay content only. Unattached uploads, replay sessions, owner archive lists/status and upload/review metadata remain private. Rejected matches/recordings are not public. Public viewing does not mark a match approved or create rewards, and it does not add another player's recording to the viewer's upload library.

## Running the shared verifier

The verifier compiles the **same** simulation sources as the game, without a Unity dependency:

```powershell
dotnet run --project Tools/ReplayVerifier -- --self-test
dotnet run --project Tools/ReplayVerifier -- path/to/replay-or-archive.json
dotnet publish Tools/ReplayVerifier -c Release
```

It uses .NET 9 and the existing Unity Newtonsoft.Json package when available; an independent checkout restores Newtonsoft.Json 13.0.3. Exit code 0 means all checkpoints and the result reproduced, 1 means malformed/diverged evidence, and 2 means incorrect command arguments. Output is JSON with `reproduced`, `result`, `simulationVersion`, and `competitiveApproval:false`.

Integrate a deployed trusted worker with the API's internal `unityReplayStore.recordReproduction` using the stored payload hash. Apply an execution timeout and run only the pinned verifier/content version. Approval must independently establish approved map/config, session validity and event authority; neither uploads nor this standalone CLI grant rewards. Physical Android/IL2CPP/WebGL runtime portability still needs device/browser verification before production rollout.

## Validation performed

- C# deterministic round trips for single-player, co-op, versus, joins/leaves, secondary weapons, pickups, powerups and defence-wall expiry.
- Malformed input, missing checkpoints, altered setup/version, tampered checkpoint and forged result rejection.
- All 35 campaign maps through the game's Newtonsoft.Json loader, including omitted legacy dimensions.
- A full .NET-generated recording reproduced in Unity after standardizing patrol-drone arithmetic.
- Existing patrol-drone and multiplayer regression checks.
- A live 1,814-tick game recording saved and reloaded, then reproduced with all state checks matching.
- The actual C# fixture accepted by the backend replay-format validator.
- Public profile WATCH: 260 Unity assertions passed across Web, Seeker landscape and PSG1 layouts, covering anonymous cross-player playback, controller submit versus focus, missing/damaged/incompatible responses, stale-request cancellation and own-library isolation. Run **Battle Cities → Checks → Public replay WATCH (Play mode)** in the MainMenu scene.
- Public replay access and private evidence boundaries: backend suite passed 112 tests, with three platform-specific skips.

Unity menu: **Battle Cities → Checks → Input replay**. The headless fixture generator is `--fixture <path>` for backend interoperability tests. Editor UI screenshots and temporary diagnostic data are kept in the workspace's `ReplayImplementation` folder, outside the active Unity project.
