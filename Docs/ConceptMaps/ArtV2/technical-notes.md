# Technical modeling notes — replacement environment v2

## Scope and source authority

This pack replaces appearance only for the new environment objects in Canal Crossroads, Timber Ambush and Ironworks Yard. Preserve the copied Design-v1.md placements, footprints and current runtime behavior. Do not rework the existing brick, steel, water, tanks or eagle. Do not enable conveyor movement, introduce bridge destruction behavior or change collision while modeling.

Authority order: (1) existing layout/runtime contract and Design-v1.md; (2) numeric footprint and pivot data in manifest.json; (3) reviewed family sheets; (4) STYLE-TARGET.png for material/shape language. Generated dimension marks and perspective drawings are illustrative. If a drawing suggests an overhang or excess height, trim the mesh to the numeric bounds.

## Units, bounds and orientation

- One logical cell = 64 logical units = 1 Unity world unit. Footprints are X width × Z depth.
- Map +x is world +X; map +y is world -Z. North is world +Z. Canonical south/front-facing building doors face -Z.
- Object placement for map rectangle (x,y,w,h): world center (x+w/2,0,-(y+h/2)). Ordinary solid props/buildings/forest kits use a bottom-center origin at ground Y=0.
- Include ALL visible geometry in footprint bounds: roof edges, foliage, lamps, pipes, doorsteps, foundations and trim. Use roughly 0.05–0.10 unit internal clearance. There must be no overhang into a neighboring walkable cell.
- Collision regions remain the existing authored rectangles. Do not infer a walkable hole from a porch, loading-door recess, gaps in tree crowns or gaps between props.
- No anisotropic scaling of cabins, roof curves, tree crowns, sandbags or barrels to fit different footprints. Use the named footprint variant, recompose components, or rotate a whole compatible kit.
- Top-view panels are roof/plan references. Some generated panels retain a slight camera tilt; numeric footprint and orthographic camera checks remain mandatory in modeling.

## Ground-contact exceptions

Buildings, trees and ordinary props span Y=0 to the manifest height limit. For bridges, belts and shallow ground effects, the origin remains at the existing travel/ground plane rather than below a raised platform.

| Asset family | Suggested visual vertical bounds | Modeling requirement |
|---|---|---|
| Concrete bridge | -0.15 to +0.03 | Deck level at Y=0; supports below; no high parapet or raised end curb. |
| Timber bridge | -0.10 to +0.02 | Plank tops at travel plane; underside beams contained. |
| Inactive conveyors | -0.04 to +0.02 | Ground-flush belt; both ends open, side trim very low. |
| Mud | -0.01 to +0.015 | Shallow patch/decal; no raised rectangular slab. |
| Rubble | 0 to +0.12 | Low visual debris only; preserve traversable behavior. |

Do not move the tank travel plane or water art to accommodate a model. Heights are art targets; existing scene grounding is authoritative.

## Silhouette and detail hierarchy

1. Large shape: curved vault, gable, asymmetrical shed, paired mill roof or stepped factory roof. The roof silhouette must already distinguish each building at small top-down size.
2. Secondary shape: one contained service recess/low roof vent/skylight band/short chimney. Avoid tall towers.
3. Accent: warm window, optional non-emissive utility lamp shape, silver vent, navy recess and small orange/blue trim.
4. Surface detail: broad seams, simple bevels and very limited wear. Never rely on flat stripes on a cuboid to suggest the building form.

Keep roof thickness, bevel scale and window trim consistent. Windows/doors remain modeled or textured surface details, not new entry points. Doors stay closed. Do not add signs, coins, pickups, visible saw blades, external log chutes, cranes over lanes or new gameplay props.

## Color and material targets

**Saved user lighting rule — overrides the illustrative lanterns in the images:** only windows may use warm emissive materials. Do not add real-time lights or emissive exterior lanterns, door lamps, vents, trim, hazard markings or roof hardware. Exterior lamp shapes, if retained, are non-emissive painted detail; they may also be omitted. The selected style target does not need to be regenerated for this difference. Keep any window emission modest and avoid bloom that reduces readability.

| Material role | Approximate base color | Treatment |
|---|---|---|
| Cream building body | #E8DBBA | Warm matte toy stone/plaster, broad panel joints |
| Main blue roofs | #117EDC | Clean painted metal, moderate smooth highlights |
| Blue shade/recess | #173C66 | Broad readable shadow values |
| Coral roofs/orange trim | #EB633C | Warm contrasting shape, restrained gloss |
| Timber | #985D32 | Broad bark/plank facets, no noisy grain |
| Silver hardware | #A7B9C4 | Brushed/chamfered, restrained specular |
| Painted gold accents / window warmth | #F3BF35 | Window-only warm emission; all external fixtures and gold trim non-emissive |
| Forest greens | #246F3B / #3B9345 / #68AB4E | 2–3 crown tones, consistent layered conifers |
| Mud/earth | #765037 / #A0794F | Soft wet/dry contrast |
| Fuel barrel red | #D93C30 | Clear hazard color, dark steel bands |

Use shared materials/atlas where practical. Lighting is illustrative upper-left; avoid baking directional cast shadows into albedo. Keep AO subtle enough that top views do not collapse into dark blocks.

## Family construction notes

- Harbor warehouse blue: actual faceted barrel-vault roof, broad loading opening, contained service nook. Coral warehouse: a genuinely different pitched-roof profile.
- Harbor store 2×3: long north–south volume, narrow facade and inset awning. Do not stretch the 3×2 warehouse.
- Ranger cabin: gable with small chimney and inset entrance. Logging hut: lower offset shed profile. Sawmill: two connected pitched volumes, self-contained loading recess.
- Factory workshop: stepped roof/skylight rhythm. Factory store: longer barrel-roof store. Utility workshop: low offset service volume.
- Concrete/timber bridge width and length are fixed at 2×3 / 1×3. Decks remain visibly continuous from north to south. No tall railings or posts encroaching on the path. A 1-unit tank collider must remain supported across the full timber rectangle.
- Log/sandbag/barrel models are ONE 1×1 cell each. A log region 2×1 or 3×1 uses independent repeated visual modules. Do not combine a full region into one gameplay entity. Exactly one barrel per barrel cell.
- North and south conveyors share geometry. Author one canonical north belt, then use a 180-degree Y rotation for the south variant OR bake the orientation into a variant, never both. Yellow chevrons must match the direction in the existing data. These are inactive/static assets.
- Mud and rubble have irregular visual edges inside rectangular reserved effect footprints. Ground decals/very shallow geometry may have transparency; no extra raised plate or dense blocking mound.
- Tree islands are solid static scenery, distinct from existing pass-through jungle/bush art. Reuse a small family of layered low-poly conifers, stagger crown size/height, and fill tiny gaps with low earth/rock detail. No regular spheres-on-a-board arrangement. All canopy remains inside the rectangle.

## Suggested modeling budgets

These are starting art-budget targets, not changes to engine limits: buildings roughly 1.5k–4k triangles; small props 150–600; tree islands 600–2.5k depending on footprint; ground patches 100–500; bridges/belts 400–1.5k. Favor shared materials over unique high-resolution textures per instance. Preserve visual quality at the actual gameplay camera before optimizing further.

## Acceptance checks for the modeling/integration owner

- Check XZ mesh bounds against the exact footprint, including every child mesh.
- Render directly overhead and at the real gameplay camera; keep roof/forest silhouettes distinct.
- Check scene size using 1 unit = 1 tile. Do not rescale the map.
- Confirm bridge endpoints and conveyor ends are visually unobstructed. Confirm the narrow bridge supports a tank footprint without clipping against decorative rails.
- Compare existing brick/steel/water/tank/eagle next to the new models; they must remain untouched.
- Keep collision, triggers, instance IDs, coordinates and current inactive mechanics unchanged.
- Render before/after previews of the same three layouts for acceptance.
- This handoff contains concept images and specs only. It does not certify any mesh topology, import settings, rendering performance or engine integration.

## Final reference corrections

- Ground assets use sheets/ground-patches-refined.png. The earlier ground-patches.png is retained only as superseded provenance. Mud grooves are shallow color/normal detail; sparse rubble stays below 0.12 units.
- Forest 2x4 requires SIX trees. The three-quarter view shows this count; the generated top illustration resolves only five crowns. Reuse six contained tree components and validate the final top view.
- All generated top diagrams are schematic art references. Numeric footprints, vertical bounds, tree counts and existing runtime behavior take precedence over visual inconsistencies.
