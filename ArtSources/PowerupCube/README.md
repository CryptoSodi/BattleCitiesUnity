# Editable pickup cube source

Created in the user's 3D ASSET MAKER chat. Original authoring workspace: `C:/repos/Battle Cities Game/blender assets/powerup-cube/`.

- `powerup-cube-components.blend`: editable separate construction pieces.
- `powerup-cube.blend`: joined production mesh, optional decal surfaces and anchors.
- `build.py`: Blender 5.2 authoring/export script. Running it writes output beside the script; copy the resulting validated GLB to `Assets/BattleCities/Art/Powerups/Cube/` and run `BattleCities.Editor.PowerupCubeImport.Run`.
- `validate.cjs`: validation script from the asset-maker workspace; uses that workspace's Node dependencies.

Kept outside `Assets` to avoid Unity importing editable Blender files into application content. The runtime model's provenance, material contract and validation report are next to its GLB in the project.
