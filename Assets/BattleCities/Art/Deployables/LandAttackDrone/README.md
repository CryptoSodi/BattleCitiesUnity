# Land Attack Drone

## Deliverables
- land-attack-drone.glb: exported PBR model with Drive and Idle clips.
- land-attack-drone.blend: editable Blender source.
- build.py: rebuild with Blender 5.2 in background mode.
- index.html: true-scale preview with wheel animation, recolouring, views and lighting.
- gameplay.json: proposed gameplay defaults, not embedded gameplay implementation.
- validation.json: measured dimensions, rendered triangles and validation results.

Created from the supplied Land Attack Drone concept. Geometry was authored
procedurally to match the wedge body, oversized tires, graphite bumper, amber
target lens, cream speed markings, rear cooling vents and chunky bevels.
This is an interpretation of the reference, not a scan or photograph projection.

## Scale and rendering
Meters, Y up, +Z forward. Root at ground level within 1 mm.
Measured width 0.787 m, length 0.852 m, height 0.398 m.
Instantiate at scale 1. It is smaller than the existing 1 m wide heavy tank.
43,528 rendered triangles, 25 mesh primitives, 11 materials, approximately 2.2 MB GLB.
Static pieces are grouped by material within each moving part.
These are review-asset statistics, not a target-device performance benchmark.

Standard lit glTF PBR materials. No image textures or baked scene illumination.
Yellow armor, shaded armor, graphite, rubber, steel, cream markings, orange panels
and emitting lenses/vents remain separately controllable.
Only genuine lights and cooling vents emit. Surface highlights remain dynamic.
For team colours change Armor_Yellow and Armor_Shade per instance, preserving
rubber, metal, markings and indicators. The preview demonstrates yellow/blue/red.

## Hierarchy and animation
LandDrone_Root is the placement root.
BodySuspension contains armor, bumper, lights and body-mounted details.
Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR are independent axle pivots.
Wheel pivots stay stationary during body suspension movement.
ImpactSocket is at the enclosed front impact bumper.
TargetSensor is in front of the amber lens.
TargetLens_Light is a small amber point light; intensity is runtime configurable.

Drive loops all four wheels and provides a small suspension movement.
Idle provides subtle body motion. Stop the previous clip before switching.
Each Drive cycle is one wheel revolution over approximately 1.03 seconds.
In gameplay, drive wheel angle from signed distance / 0.163 m rolling radius
(or adjust Drive playback speed accordingly). Do not spin wheels while stationary.
The preview spins wheels in place; it does not implement autonomous pursuit.

## Gameplay handoff
Implement this as one active land drone per player.
Deploy at a valid nearby unoccupied ground location.
Acquire the nearest visible hostile tank with a reachable route.
Use passable-ground pathfinding with obstacle checks and dynamic replanning;
prefer lower-threat routes when threat data is available.
Never traverse water, steel walls, brick walls, bushes or any other blocked tile.
Accelerate toward 1.5 times player speed. Suggested detection range 9 tiles,
lifetime 30 seconds, health equivalent to two enemy bullet hits.
Direct impact should inflict very high damage using the existing damage scale;
this value is intentionally not invented in gameplay.json.
Suggested splash radius is 1.25 tiles.

On contact with an enemy tank, detonate exactly once and apply hostile-only damage.
Never damage the player, friendly entities or friendly base.
Enemy bullets can destroy it before arrival.
Expire when its lifetime ends or when no reachable target can be found within the
game's configurable acquisition/repath timeout. Do not allow indefinite pursuit.
Keep authority for targets, health, damage and expiry in gameplay logic.
Use simple movement/hit colliders separate from visual wheels and body geometry.
Explosion effects, sounds and damage are runtime effects, not model animations.

## Verification and remaining work
Exported GLB validates with zero errors and zero warnings; informational notices
only concern unused UVs and intentional socket nodes.
Three.js checks verified four animated wheel pivots and no page errors.
Reviewed front/rear/side/top, actual scale beside the heavy tank,
day/night/moving spotlight and a team-colour variant.
Gameplay AI, Unity integration, collision tests, damage balance, target-device
profiling and visual approval remain outside this model review.
