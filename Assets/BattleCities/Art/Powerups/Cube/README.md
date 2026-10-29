# Battlefield power-up cube

Authored by the user's **3D ASSET MAKER** chat from their blue-and-gold cube references. Original asset workspace: `C:/repos/Battle Cities Game/blender assets/powerup-cube/`.

`powerup-cube.glb` is a unit cube with ground-center pivot, Y up. The opaque shell has 732 triangles and two materials. Its five optional placeholder decal planes are disabled in the game prefab; `BattlePickupCube` creates aspect-fitted top/side planes using the existing transparent Shop illustrations. No new collection collider is added.

The reusable `Resources/PowerupCube.prefab` normalizes the shell to 0.9 world units; its runtime body group reduces it to 54% of a map tile. The cube hovers, bobs, rotates and gently rocks, with softly pulsing gold emission and a faint colour-matched panel glow. A small warm shadowless point light illuminates nearby ground. The independently grounded gold halo has a soft radial fade, a visible shrinking/enlarging pulse within a one-tile maximum outer diameter and a rotating segmented outer ring, with six small orbiting gold sparks kept inside that footprint. Both live games and TV replays use this same world-space presentation. Shop, loadout and HUD icons are unchanged.

The prefab and its mesh/material/illustration dependencies are delivered through the pre-login Android art bundle. Runtime code needs a new application build; replacing a remote texture alone cannot add this behavior to an old APK. No build has been published as part of this change.

Editable Blender files and authoring code are preserved outside Unity's import tree in `ArtSources/PowerupCube/`. The asset-maker's GLB validation report is included here. Reimport/regenerate the prefab with `BattleCities.Editor.PowerupCubeImport.Run`; run `PowerupCubeChecks.Run` for all twelve variants, live pickup lifecycle and TV-layer checks. Rendered Editor visual previews are saved under `Builds/PowerupCubePreviews/` and do not contact the API or change player inventory.

Inset panel colours are item-specific, including ivory/white healers, icy Freeze, sand Base Defence, light-steel Speed, burgundy Wipeout and teal Zoom Out. Other blue variants retain their appropriate cobalt/navy hues. Runtime property blocks keep these colours per instance; source meshes, shared gold materials and Shop/HUD artwork are unchanged.
