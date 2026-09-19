# Arcade mine

Reference-inspired first review model for Battle Cities. This is an authored 3D interpretation, not an exact reconstruction or a photo-projected bake.

## Files

- `mine.glb`: 10,700 rendered triangles, eight standard glTF PBR materials, embedded metal roughness texture, and `Press` / `Blink` animations.
- `mine.blend`: editable mine source.
- `ground-patch.glb`: optional separate ground decoration, 2,964 triangles. Use the game's ground instead when appropriate.
- `metal-roughness.png`: authored surface roughness; no scene lighting baked into it.
- `reference.png`: supplied design reference.
- `validation.json`: zero glTF validator errors or warnings for both GLBs; browser loaded without script errors.
- `../build_arcade_mine.py`: reproducible Blender source, including the ground patch.

## Integration

Load `mine.glb` with Three.js GLTFLoader. Units are meters, Y up, origin at ground center. The visual footprint is approximately one meter across. Preserve the imported PBR materials and texture color spaces. Enable tone mapping and cast/receive shadows; provide scene lighting and an environment appropriate to the day/night cycle. Only the red indicator uses emission. The preview renders that glow without bloom or an additional light.

Use AnimationMixer to play `Press` once when the game calls for the visual press response. It does not implement gameplay or an explosion. Keep the gameplay trigger/collider separate from the visual mesh; size it using the game's existing mine rules. The optional ground patch is a sibling at the same origin, not part of the collider.

## Surface and lighting

Yellow enamel, cream markings, and dark metal use material base colors. The center disk has a subtle roughness texture. Highlights and shadows are dynamic; there are no baked direct-light highlights, cast shadows, or unlit materials. Normal and AO maps are not supplied: bevels and notches are geometry. Fine scratches, weathering, and organic dirt from the reference are simplified. Daylight, night, and moving-spotlight preview modes are included for review. This is not a performance benchmark in the game.

The red indicator has a standard transform-based `Blink` animation: play it continuously with AnimationMixer (LoopRepeat). It alternates a red emissive cap with the underlying dark red lens, roughly 0.42 seconds on and 0.58 seconds off. Play `Press` independently by name.
