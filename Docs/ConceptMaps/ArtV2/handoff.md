# Replacement environment assets v2

**Complete: 22 asset references across 9 selected family sheets, mapped to 63 authored regions / 71 visual modules.**

Start with [STYLE-TARGET.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/STYLE-TARGET.png), then the family image linked from [manifest.json](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/manifest.json). [PACK-OVERVIEW.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/PACK-OVERVIEW.png) previews all nine families.

## Required lighting override

**Warm emission is allowed on WINDOWS ONLY. No real-time lights and no emissive exterior lanterns.** Any lamps shown in the images are non-emissive painted shapes or may be omitted.

## Ready family sheets

| Family | Image | Assets | Status |
|---|---|---:|---|
| Harbor buildings | [harbor-buildings.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/harbor-buildings.png) | 3 | Ready |
| Logging buildings | [logging-buildings.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/logging-buildings.png) | 3 | Ready |
| Industrial buildings | [industrial-buildings.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/industrial-buildings.png) | 3 | Ready |
| Bridges | [bridges.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/bridges.png) | 2 | Ready |
| Logs, sandbags and barrel | [barricades-and-barrel.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/barricades-and-barrel.png) | 3 | Ready |
| Inactive conveyors | [conveyors.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/conveyors.png) | 2 | Ready |
| Mud and rubble | [ground-patches-refined.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/ground-patches-refined.png) | 2 | Ready |
| Forest islands: small | [forest-islands-small.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/forest-islands-small.png) | 2 | Ready |
| Forest islands: wide | [forest-islands-wide.png](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/sheets/forest-islands-wide.png) | 2 | Ready |

## Modeling contract

- One map cell = 64 logical units = 1 Unity unit. All geometry, roof edges, props and tree crowns must fit inside their specified footprint.
- Use the numeric bounds in manifest.json, not generated dimension marks. Ordinary assets have a bottom-center pivot, ground Y=0, north +Z and entrance south -Z.
- Bridge travel surfaces remain at Y=0. Belts stay inactive. Mud and rubble remain shallow and traversable. Existing runtime behavior and collision take precedence.
- Preserve existing brick, steel, water, tanks and eagle art. This pack changes the appearance of the named new scenery assets only.
- Forest 2x4 requires six trees; its generated top diagram shows only five crowns. Use the specified count and validate a true orthographic render.
- Use ground-patches-refined.png. The earlier ground-patches.png is superseded and retained for provenance only.

## Exact data and placement

- [asset-index.md](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/asset-index.md): IDs, dimensions, vertical bounds and image rows.
- [manifest.json](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/manifest.json): machine-readable asset data, selected images and provenance.
- [placement-map.md](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/placement-map.md) and [placement-map.json](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/placement-map.json): every region, module, coordinate and matching asset ID.
- [technical-notes.md](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/technical-notes.md): modeling, material and validation rules.
- [references/Design-v1.md](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/references/Design-v1.md): frozen authoritative layout specification.
- [validation.json](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/replacement-assets-v2/validation.json): artifact and mapping checks.

Original stage concepts are positive style references. Copied rejected Unity previews document what to replace; do not reproduce their flat placeholders.

Concept artwork and handoff only. No Unity/modeling source was edited. Mesh topology, collision, performance and engine imports still require validation by the modeling/integration owner.
