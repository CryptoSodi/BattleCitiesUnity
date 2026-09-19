# Wing drone — first review model

Authored from the user-supplied yellow quad-fan reference. Rear and underside geometry are inferred from the single image. This is a review asset, not yet performance-tested in Battle Cities.

- `wing-drone.glb`: 60,328 triangles, 17 mesh primitives, 9 PBR materials, 3 punctual lights.
- `wing-drone.blend`: editable model, with animation tracks muted for a neutral editing pose. Unmute Hover and RotorSpin in the NLA editor to animate.
- `build.py`: reproducible Blender generator; all geometry is newly authored.
- `index.html`: Three.js review with orbit, top view, daylight/night, fan and light toggles.
- `validation.json`: glTF validation results; zero errors and zero warnings.

Four wing arms attach four open ducted fans. Each rotor has its own pivot. `RotorSpin` is a one-second counter-rotating loop; the preview plays it at 5× speed. `Hover` is an independent four-second gentle bob/roll loop. Both are embedded in the GLB.

Meters, Y up, forward +Z. DroneRoot is the vehicle placement origin. HoverBody owns the visual motion; gameplay should use a separate simple collider. The model spans roughly 1.83 m across its fans. The preview floor is lowered to show it hovering.

Materials are standard metallic/roughness PBR with geometric markings and details. No reference-photo lighting is baked, and no raster texture maps are used in this version. Armor highlights and shadows remain dynamic; only lamps and vents emit light. Import KHR_lights_punctual or recreate those lights in the game, tuning intensity for the game's exposure.

Preserve the rotor pivots when batching. A lower-detail mesh and in-game draw-call/performance testing remain recommended before spawning many drones.
