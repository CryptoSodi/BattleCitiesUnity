# Level editor â€” first version

Open **Battle Cities > Level Editor > Open editor**, or open `Assets/BattleCities/Scenes/LevelEditor.unity`.

## Authoring

- The saved `EditorCanvas` hierarchy contains the header, toolbar, scrollable palette, map viewport, properties panel, and footer. Adjust RectTransforms, text, and artwork outside Play mode. Opening the editor does not rebuild those controls.
- `LevelEditor/MapRoot` contains editable `LevelElement` objects. Select an element to edit its fields in Unity's Inspector; move it with the Transform tool. Size uses map units: 64 units is one tile. Keep the element root rotation at zero and scale at one; use Size and Rotation fields for supported props.
- Select `LevelEditor` to edit the name, dimensions, visual theme stage, and enemy roster (tiers a/b/c/d and drop flags).
- In **Scene view**, use the toolbar and the document Inspector to choose a brush. Paint/Erase drag with the left mouse button; Select lets you click and drag an item to move it. Unity's scene navigation remains available with Alt. Frame map restores a top-down view.
- Press **Play** to use the saved Canvas in **Game view**. Select a palette item and drag across the map. Right-click erases, wheel zooms, FIT MAP resets the view, and SNAP cycles 64/32/16 units. Undo/Redo groups a paint drag into one operation. In **Select**, click and drag an existing item to move it; the grab point is retained, its position snaps to the current Large/Small/Fine grid, and its whole footprint stays inside the map. Each move is one Undo operation. Escape restores the starting position. Moving preserves size, type, rotation, and damage settings, and does not erase other items. A saved terrain region moves as one item; the cyan outline shows its complete footprint.
- Click a top-down element thumbnail to select its brush and show its properties on the right. The palette contains brick, steel, jungle, Water, Tree, Building, player spawn, enemy spawn, and eagle base. **Water** groups the approved **Water / Lava / Ice / Grease** surface types. Choose a type on the right to paint it; selecting a placed surface lets you change that patch's type without changing its footprint. Place one player spawn and one base, and at least one enemy spawn. Existing drafts retain older ground, prop, light, and terrain entries for compatibility.
- **Tree** works like Water: choose the TREE thumbnail, then choose **CLASSIC A / CLASSIC B** (the original trees), or an existing **GROVE 2 x 2 / 2 x 3 / 3 x 2 / 2 x 4** model on the right. Paint to place the chosen model at its normal footprint, snapped to the current grid. Select a placed tree and choose a type to replace its model in place; position, footprint, rotation, source notes and damage settings are preserved. Tree damage overrides remain editable in the Unity Inspector. The last chosen tree type is remembered when returning to the Tree brush.
- **Building** works like Water and Tree: choose BUILDING, then select an existing model in the right-hand **TYPE** tab and paint it. There are 11 distinct models, including warehouses, cabins, workshops and the two original city buildings. Select a placed building to change its type in place or open **DAMAGE** to adjust section health, invulnerability and shot susceptibility. Model changes preserve position, size, rotation, source notes, damage and lighting. New buildings use the chosen model's footprint, 3 HP per section and default warm lights. Lights remain adjustable in the Unity Inspector.
- Set **Map Size** width and height on the right, then choose **Apply**. Dimensions use large tiles and can be rectangular, from 4 to 40 tiles per side. Resizing preserves positions. Shrinking is rejected if it would cut off existing elements; move or erase them first. Sizes persist in the scene, JSON drafts, recovery, and playtests.
- **Small** is the default paint size: 32 x 32 units, so four small tiles form one 64 x 64 large tile. **Large** paints 64 x 64; **Fine** paints 16 x 16. The grid shows the current subdivision. Terrain painting and erasing affect only that footprint; spawns and the base keep their gameplay sizes.
- Editor actions use flat square-cornered controls. A gold tile marks the active brush/tool, and a cyan border marks hover or keyboard focus. Thumbnail artwork reuses the game models and terrain materials.

## Damage properties

Select brick, steel, or a bullet-blocking prop and enable **Override Damage**. Set Hit Points, Invulnerable, Normal Shots, and Power Shots + Splash. These are actual simulation properties, not preview-only settings.

- Normal shots remove one HP; charged shots remove three HP, including their wall splash.
- Brick health applies per 16-unit simulation fragment. Steel uses 32-unit fragments. Each prop has one health pool.
- Invulnerable blocks both attack types. Turning off an attack checkbox ignores that type. Turning off Override Damage restores the existing material/role rules.
- Water, ground, jungle terrain, spawns, standalone lights, and the eagle base do not expose custom health in this version. Enemy health still follows its tank tier.

## Save and test

**OPEN** imports a JSON map as an editable copy. **SAVE** creates a JSON draft; **SAVE AS** creates another copy. Drafts normally belong in `Assets/BattleCities/LevelDrafts`. Existing files are replaced atomically with a `.bak` copy. Campaign maps are not automatically overwritten or added to the campaign.

**VALIDATE** checks dimensions, bounds, required spawns/base, spawn clearance, terrain keys, prop roles, and damage settings. It rejects incompatible overlapping surface types. It does not yet evaluate complete path reachability or difficulty.

**TEST LEVEL** validates, saves the authoring scene, and opens the existing BattleCity gameplay scene using the draft snapshot. This is a local sandbox: economy and multiplayer are disabled. Stop Play to return to the editor. The configured Preview Stage controls existing stage-specific visual defaults.

Map edits made in authoring Play mode are restored when you stop and written to `Library/BattleCitiesLevelEditorRecovery.json`. **Battle Cities > Level Editor > Recover last Play-mode draft** restores that snapshot. Keep named JSON saves for durable copies. Canvas layout changes made in Play mode follow Unity's normal temporary behavior; author those outside Play mode.

## Preview and scope

The orthographic top-down map preview calls the same gameplay builders for ground, road markings, props, brick fragments, 32-unit steel blocks, foliage, joined surfaces, and recessed shorelines. It uses the existing game models/materials and BattleLook profile; surface animation runs in authoring Play mode. Preview Stage selects the same ground theme used during Test Level. Combat and dynamic weather run during Test Level.

Generated geometry lives under `LevelEditor/Game appearance (preview)` and is not saved as authored map content. Continue editing the `LevelElement` objects under `MapRoot`. The preview rebuilds after painting, moving elements, resizing the map, changing a surface type, and Undo/Redo. The Canvas remains a persistent, editable hierarchy.

This version is an internal desktop Unity tool, excluded from the game's build scene list. Campaign ordering, multi-level management, player-facing editing, advanced wave timing, path analysis, and custom base/enemy health remain future work.

## Verification

Run **Battle Cities > Level Editor > Validate editor and damage rules**. Checks use a separate hidden document and cover all 35 existing campaign map roundtrips, rectangular resizing and shrink protection, four-small-tiles placement, all four Water variants, surface save/reopen, painting/erasing rectangle remainders, damage preservation, spawn validation, real projectile collision, normal/power immunity, prop health, and the existing shooting/health regressions.

Run **Battle Cities > Level Editor > Validate tree family** for tree painting, the six actual models, selected-type changes, natural placement footprints, snap, overlap protection, property preservation, save/open, gameplay export and Undo/Redo. All 56 checks passed; the saved palette and six type buttons were also exercised in authoring Play mode.

Run **Battle Cities > Level Editor > Validate building family** for the 11 building models, brush placement, selected-model changes, section defaults, damage/light preservation, legacy aliases, save/open and Undo/Redo. All 114 building-family checks passed, along with 56 tree checks and the existing editor/campaign regression checks.

## Concept map library

Use **MAPS** in the editor title bar, or **Battle Cities > Level Editor > Concept maps**, to open Canal Crossroads, Timber Ambush or Ironworks Yard. These 25 x 25 templates open as editable copies and the previous draft is backed up automatically. Save As keeps your own version. Source IDs and design notes are on each element in the Inspector.

See [Concept map notes](ConceptMaps/README.md) for replacement models, supported behavior and pending mechanics, [the supplied specification](ConceptMaps/Design-v1.md) for exact placements, and `ConceptMaps/Previews` for rendered previews. **Validate concept maps** checks source placements and full tank routes without changing the current draft.
