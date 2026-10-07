# Concept map drafts

Open the saved LevelEditor scene and use **MAPS** in its top bar, or Unity's **Battle Cities > Level Editor > Concept maps** menu. Choose Canal Crossroads, Timber Ambush or Ironworks Yard. Each opens as a copy; use Save As for changes. The previous draft is automatically backed up under `Library/LevelEditorMapBackups` and opening is undoable. The original templates are in `Assets/BattleCities/LevelDrafts/ConceptMaps` and are separate from the campaign.

All three maps are 25 x 25 large tiles (1600 x 1600 logical units). Source coordinates, half-cell HQ screens and four spawn anchors follow [Design v1](Design-v1.md). The existing base still supplies its normal inner guards. The stage 01 enemy roster and drop flags are copied unchanged; map difficulty descriptions are design targets, not balance results.

Select or drag an item in the map, or select it under `LevelEditor/MapRoot`. The hierarchy names retain source IDs such as H01, BR02 and HQ-N. Inspect `Source Id`, `Design Notes`, size, rotation, bridge surface and damage properties in the Unity Inspector. Notes and IDs survive save/open. Log regions are independent one-cell elements named L01.1, L01.2, and so on.

## Replacement assets and behavior

The maps now use authored concept-v2 buildings, forest islands, bridges, logs, sandbags, barrels, mud, rubble and conveyors. The two requested art chats produced nine concept sheets and 21 unique 3D models covering 22 asset IDs. See [Art v2](ArtV2/README.md) for the concept sheets, editable-source location and import details. Existing brick, steel, water, tanks and eagle use their gameplay assets. New building families are under `Assets/BattleCities/Art/ConceptMapArtV2/GameplayPrefabs`; existing prop wrappers retain their GUIDs in `ConceptMapPlaceholders` with their visuals replaced. Each prefab keeps its gameplay role and fixed footprint.

| Element | Current prototype |
| --- | --- |
| Buildings | Destructible in 32-unit sections; 3 HP per section by default. Warm window lights dim with damage. |
| Tree islands | Solid, bullet-blocking and invulnerable. |
| Concrete and timber bridges | Permanent and passable. The Bridge Surface flag removes water within the deck footprint; moving, resizing or deleting the deck updates the water automatically. |
| Logs | Each 1 x 1 cell has two hit points and blocks tanks/shots until destroyed. |
| Sandbags | One-hit destructible cover; contact crush is pending. |
| Fuel barrels | One-hit destructible cover; fuse, explosion and chain reaction are pending. |
| Mud and rubble | Passable authored surfaces; proposed speed modifiers are pending. |
| Conveyors | Passable inactive belts with north/south arrows; drift is pending. |

The prototype does not implement the proposed timber collapse, occupant rescue, hazard replication or co-op spawn changes. Run **Test Level** for the existing local playtest flow. These maps are editor drafts, not new campaign slots.

See [Building damage and lighting](../BuildingDamage.md) for section behavior, saved lighting controls, and validation.

## Verification

Use **Battle Cities > Level Editor > Validate concept maps** to check exact source placements, bounds, original roster/drop flags, spawn exits, save/open metadata, damage configuration, visual footprint containment, the supplied waypoint routes, and all four spawns reaching all three HQ approaches with the simulation's full tank footprint. Scenarios include intact maps, timber removed, west bridge only, east bridge only, logs removed, barrels removed and avoiding conveyors. Bridge relocation/removal is checked separately. Static reachability does not validate live enemy steering, combat timing or future mechanics.

Top-down captures from the shared gameplay renderer are in `Previews/`.

Verified after the art-v2 import in Unity on 7 October 2026: all 718 concept-map assertions and the existing editor/campaign checks passed. All three top-down previews and the live editor were visually inspected. The imported assets have supported materials and stay inside their assigned footprints; equivalent materials are shared. A before/after comparison confirmed that the current authoring draft retained its geometry, spawns, damage and other gameplay values. The earlier movement and two-hit-log checks remain unchanged. The assertion count is lower than the placeholder version because each authored asset has one renderer rather than many primitive renderers.
