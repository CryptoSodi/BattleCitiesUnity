# Healers and shooter power-ups

These are source changes; no Web/APK build, content upload or API deployment was performed.

| Inventory/shop ID | Simulation type | Effect | SOL | SKR |
|---|---|---|---:|---:|
| `max-healer` | `healmax` | Restore current tank to its chassis maximum HP | 0.02 | 450 |
| `health-healer` | `heal2` | Restore 2 HP, capped at the chassis maximum | 0.01 | 450 |
| `rapid-shooter` | `rapid` | 4x firing rate for 10 simulation seconds | 0.01 | 450 |
| `rapid-shooter-long` | `rapidlong` | 4x firing rate for 60 simulation seconds | 0.02 | 900 |

The short shooter uses the existing three-shell icon from atlas cell 8. The long shooter uses the supplied blue ammo box. Healing uses the supplied gold/green cases. The supplied originals are preserved; see `UI/Art/shop/extended-powerups.source.json`. New framed images are separate textures, so old atlas UVs remain unchanged.

Healing applies to a living current player tank and cannot resurrect a destroyed tank. Shooter effects refresh rather than add durations; a shorter effect cannot shorten an active longer one. Normal reload and charged-shot recovery divide by four, stacking with the tank's Rounds property. Charge input/timing and damage are unchanged. The boost does not fire shots without player input. Expiry uses simulation ticks, pauses with simulation, and resets on tank destruction/respawn.

The twelve inventory items appear in shop/loadout and the Field Manual. HUD, touch/controller icons, world drops and replay billboards share the extended artwork lookup. `ExtendedPowerupArt.asset` is a required pre-login remote-art-pack dependency and is included by `RemoteGameArtBuild`; it is not fetched after wallet connection.

`powerupRosterVersion=1` enables twelve ordinary drop types. Requests/recordings without the field keep the legacy eight-type pool. The active shooter timer is replicated and checkpointed, and omitted from JSON when zero to preserve old replay state hashes. Photon protocol advances to `battlecities-11` because its tank network structure changed; update clients and dedicated servers together on the next explicitly requested build/release.

The companion API changes are in `C:/repos/BattleCities-API`: catalog/prices, JSON inventory normalization, authenticated idempotent consumption, ordinary drops and replay validation. No database schema change is required. Existing explicit administrator price overrides, including disabled/null SKR prices, retain priority.

MagicBlock's deployed VRF contract still maps eight outcomes. Its verified decoder is deliberately unchanged; the four additions are ordinary-drop/inventory/shop items, not new claimed on-chain outcomes. Extending that experimental drop pool requires a separately coordinated contract change.

Validation entry points: `BattleCities.Editor.ExtendedPowerupChecks.Run`, `BattleCities.Editor.ExtendedPowerupUiChecks.Run`, and the API's `npm test`.

Wipeout deals one point of standard damage to each living enemy NPC. Normal shields protect against it, allies/player tanks are unaffected, and only enemies reduced to zero HP are destroyed and award kill points. Damaging a marked carrier consumes its drop flag once through the ordinary damage path. New recordings/checkpoints use `wipeoutDamageVersion=1`; missing/zero retains archived instant-kill behavior. The Field Manual and both screen-definition sources describe the new effect. `BattleCities.Editor.WipeoutDamageChecks.Run` verifies this rule and replay compatibility.
