# Multiplayer modes

| Mode | Native flag | Browser query | Total players |
| --- | --- | --- | --- |
| Single-player | `--mode offline` | `?mode=offline` (or no query) | 1 |
| Co-op | `--mode coop` | `?mode=coop` | 2 |
| 2v2 Brawl | `--mode 2v2` | `?mode=2v2` | 4 |
| Capture the Flag 1v1 | `--mode ctf1v1` | `?mode=ctf1v1` | 2 |
| Capture the Flag 2v2 | `--mode ctf2v2` | `?mode=ctf2v2` | 4 |

`ctf` remains an alias for 2v2 CTF. The old elimination PvP launch option is removed; its simulation identifier remains for historical recordings. All players must use a compatible build (`battlecities-8`). Co-op and CTF 1v1 reject a third player. Matchmaking separates the modes; the test website also uses a separate Photon app-version pool.

Open the login screen, sign in as a guest and press Start. Matching and room creation are automatic. Browser examples:

- https://play.battlecities.com/?mode=coop
- https://play.battlecities.com/?mode=2v2
- https://play.battlecities.com/?mode=ctf1v1
- https://play.battlecities.com/?mode=ctf2v2

The same queries work on https://test.battlecities.com/. Only that exact hostname selects the test API and Solana Devnet. Other hosts use the production network. The base URL continues to launch single-player.

## Team rules

Brawl uses two opposing eagle bases with two human players and allied AI per team. Yellow is P1/P3; green is P2/P4. Destroy the enemy eagle to win, or eliminate the opposing human team.

CTF uses protected eagles and unlimited human respawns. First to three captures wins. Drive over the opposing flag, then return to your own flag position while your flag is home. Destroyed/disconnected carriers drop their flag; defenders return it on contact. Dropped flags automatically return after 30 seconds. Allied AI targets enemies; only humans carry flags.

## Runtime and verification

Host migration uses compressed simulation checkpoints; disconnected players have a 30-second slot reservation. Movement prediction replays unacknowledged movement against authoritative snapshots. World state is split across network objects to stay below Fusion's per-object size limit.

Simulation checks pass for flag rules, deterministic checkpoint restoration and local replay playback. Windows native smoke tests passed for four-player arena replication, two-player co-op, and CTF 1v1; both two-player modes rejected an extra client. Physical-device, adverse-network and live host-migration tests remain pending. Team and CTF recordings are local until their formats are supported by the replay backend.

WebGL explicitly enables Fusion client/server modes. Browser hosts must keep their tab active; background-tab suspension can interrupt the match. Production/test deployment does not by itself certify cross-browser multiplayer behavior. Prefer a native or dedicated host for sustained matches.

Dedicated-server build entry point: `BattleCities.MultiplayerReleaseBuild.LinuxServer`. A Windows development server can use `--dedicated --mode 2v2`; clients can request dedicated-only matchmaking with `--dedicated-only`. Linux build support and adequate server memory are prerequisites. Backend-authenticated ranked rewards are not enabled by this deployment.
