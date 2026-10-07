# Concept map art v2

These authored models replace the first-pass environment placeholders in Canal Crossroads, Timber Ambush, and Ironworks Yard. The map templates keep their original footprints, placements, rotations, damage overrides, and bridge flags.

## Sources

- Concept artwork: **Generate modern Battle City concept**, chat `01a094c4-7348-79f3-9b5b-13a6013430db`.
- Blender modeling, texturing, GLB export and validation: **3D ASSET MAKER**, chat `01a0e132-e8a6-72b2-aa87-35bded5fd4ae`.
- Concept archive: `Docs/ConceptMaps/ArtV2/`, including generated-image provenance, dimensions and nine family sheets.
- Editable Blender sources and generation scripts: `C:/repos/Battle Cities Game/blender assets/stage-environments/2026-10-07-replacement-v2/source/`.
- Source pack manifest and glTF validation are archived alongside this file. The full source pack remains in its versioned modeling folder.

The authoring chats produced the work at the user's request. The selected visual direction uses shaped blue roofs, cream walls, timber details, restrained orange factory trim, and warm windows. Source concepts are appearance references; the numeric manifest is authoritative for dimensions. The source pack contains emissive windows. The later building-damage update adds warm window lights in Unity, as requested by the user.

## Unity integration

`Assets/BattleCities/Art/ConceptMapArtV2/Models/` contains self-contained GLB models imported through the project's existing glTFast package. Each mesh has one material slot. Equivalent imported materials are consolidated by shader properties and texture content, not by their names. Shared material instances are in `SharedMaterials/`.

`bindings.json` maps 22 asset IDs to 21 unique meshes. Southbound conveyor geometry is the northbound mesh rotated 180 degrees. Existing map placements already set that rotation, so their `draft_conveyor` key is retained and not rotated twice.

New building family prefabs are in `GameplayPrefabs/`. Existing prop and forest prefab GUIDs remain in `ConceptMapPlaceholders/` for compatibility, but their visual children now contain the authored models. Legacy blue/coral building keys also remain valid. `unity-import-report.json` lists the exact prefab, model, renderer count, triangle count and materials used by each binding.

One large tile is 64 logical units, corresponding to one Unity unit. Models have bottom-center pivots, +Y up and +Z north. Building footprints are authored separately for each size. Bridge decks and conveyor surfaces remain close to ground level to match the current tank movement system.

The explicit `ConceptMapArtIntegration.Apply()` editor utility validates all imports before replacing visuals. `UpgradeDocument()` updates a currently open concept draft by source ID while preserving custom positions and rejecting custom footprint/model substitutions. Nothing regenerates the UI or map on scene entry.

## Scope

Existing gameplay brick, steel, jungle/water surfaces, tanks and eagle assets remain in use. Buildings now use localized destruction and window lighting; see `Docs/BuildingDamage.md`. Forest islands remain static cover. Logs, sandbags and barrels retain their existing hit points. New models do not add the proposed barrel explosions, conveyor drift, mud slowdown or bridge collapse. See the parent map README for those behavior notes and validation coverage.
