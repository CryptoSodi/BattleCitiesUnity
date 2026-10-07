# Terrain tiles

`MapData.terrain.regions` supports these tile keys. Coordinates and sizes use the existing map units (64 units = one tile).

| Key | Appearance | Tank behavior | Bullets |
| --- | --- | --- | --- |
| `water` | Existing cyan cartoon water, recessed banks | Blocked | Pass over |
| `lava` | Dark red crust, animated glowing molten seams | Blocked; no contact damage | Pass over |
| `muddyWater` | Opaque brown/olive water, slow currents and ripples | Blocked | Pass over |
| `ice` | Joined pale blue sheets, still cracks and reflections | Existing half-second coast after releasing drive | Pass over |
| `grease` | Dark oil with violet/teal sheen and feathered edges | Same sliding behavior as ice | Pass over |
| `quicksand` | Shallow ochre basin with soft moving sand contours | Driveable; sinks stopped tanks and resists escape | Pass over |

Stage 17 uses **ice**. It now renders as joined sheets instead of individual cream-colored cubes. Its layout and sliding rules are unchanged. Grease is an additional supported tile, not a replacement for Stage 17's ice.

## Quicksand behavior

- Sinking reaches maximum depth after 5.5 seconds stationary. The central track contact must be at least halfway over the patch; grazing a corner does not trigger it.
- Tanks survive at maximum depth, with the turret still visible and aiming/firing available.
- Hold any unobstructed driving direction to escape. Speed varies from 62% on fresh sand to 12% when fully sunk. About 3.2 seconds of actual movement loosens fully packed sand; reaching solid ground restores depth faster.
- Turning, firing, or pushing against a wall does not count as escape movement. Reversing away from a wall works.
- Tracks churn faster relative to hull travel when escaping. Lower hull and tracks sink visually; logical hitboxes remain unchanged so combat stays predictable.
- Uses the same fixed simulation steps for players, AI and multiplayer. Pausing the game pauses sinking and surface motion. No input tapping or extra button is required.
- Lava and muddy water use the existing recessed-bank geometry. Quicksand removes the ground underneath so the sinking visual is not hidden by the ground plane.

## Play the sample

Open the battle scene, enter Play mode, then select **Battle Cities > Terrain > Play terrain sample**.

Top row, left to right: water, lava, muddy water. Bottom row: ice, grease, quicksand. The player spawns just south of quicksand: drive up into it, release movement to sink, then hold a direction to pull free. Reload a campaign stage or stop Play to leave the sample. The sample does not edit any campaign map.

Authoring example:

```json
{"type":"quicksand","x":576,"y":384,"width":128,"height":160}
```

Use one terrain type for each footprint; overlapping different surface types is not supported. Adjacent regions of the same type join seamlessly. Map aliases `muddy_water`, `muddywater`, `quick_sand`, and `quickSand` normalize to the canonical keys above.

## Validation

**Battle Cities > Terrain > Validate terrain rules** checks blocked liquids, shots across basins, sinking/recovery, wall and corner contact, AI escape, deterministic movement, multiplayer movement and state serialization, slide behavior, ground cutouts, and shader compilation.

Verified in Unity: terrain rules, existing navigation checks, multiplayer rules, combat/presentation/basin checks, weather checks, and a visibly sunk tank driving free in the playable sample. Stage 17's ice was inspected from the game camera.

Known pre-existing check issue: `LaneDirectionRegressionChecks` starts tanks two map units inside its walls. Its 16 cases fail identically with the saved pre-terrain simulation and the new simulation; with clear starting positions, all 16 pass in both versions. That fixture and the existing overlap behavior were left unchanged.

Networking protocol is `battlecities-3` because tank snapshots now include sink depth and sand contact. Hosts and clients must use the same updated build. Local multiplayer rule and serialization checks are covered; a live two-device session is a separate release check.

The material clocks use the game's presentation delta, not Unity's global shader time. Materials are shared per type, surfaces are joined meshes, and resources are disposed with the game/stage.
