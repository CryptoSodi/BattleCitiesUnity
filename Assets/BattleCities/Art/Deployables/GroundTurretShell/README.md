# Armored-shell ground turret

New standalone asset: ground-turret.glb. Editable source: ground-turret.blend.
Regenerate using Blender 5.2 with build.py. The earlier ground-turret folder is unchanged.

## Design and scale
Restores the original yellow armor blocks, projecting support feet, upper head,
gun, ammunition pod, warning lamp, and four illuminated reload nuts.
The base exterior is retained; an internal cover and staged moving armor provide
head clearance. No enlarged external cylinder or outward sliding cover.

Meters, Y up, +Z forward. Deployed shell footprint is 1.000 m wide.
The GLB includes its authoring conversion on GroundTurret_Root; instantiate the
imported asset at scale 1. Do not normalize using total height or minimum bounds:
the lower housing is intentionally underground. Place the scene root at ground Y=0.

## Animation and gameplay
Deploy and Undeploy last approximately 3.03 seconds. Retraction:
1. Cannon retracts along its axis.
2. Armor panels briefly separate to clear the head.
3. Head lowers into the shell; armor returns to its original placement.
4. Internal cover closes; the entire shell settles flush with ground.

Deployment reverses the same staged motion. The cannon retraction is stylized
axial compression of the gun assembly, rather than a physically simulated mechanism.
The transition temporarily needs approximately 1.36 m of lateral clearance.
The stowed head remains below the closed cover; no visual geometry rises above Y=0.

Preserved clips: TurretCardinal4Way, GunFire, ReloadCycle.
TurretYaw supports only 0, 90, 180 and 270 degrees for gameplay targeting.
Reload_1 through Reload_4 colored nuts provide sequential red-to-green feedback.
AttackRange_Light follows the head.

Play Deploy/Undeploy once, clamp to the endpoint, and stop the opposite action.
Do not aim or fire during transitions. Disable attack/target lighting and muzzle
effects while stowed. Stop recoil before retraction. Block the tile throughout
transitions and while deployed; enable tank crossing only when Undeploy finishes.
Before deployment, check the tile and transition clearance for vehicles.
Use a flat ground collider in the stowed state and a separate blocking collider
while deployed. Do not use the detailed animated meshes as gameplay colliders.
The GLB does not implement Unity navigation or collider state changes.

## Preview and validation
index.html compares this asset with the existing heavy tank at actual meter scale.
Tank crossing is a visual clearance demonstration, not a Unity physics test.
check-deployment.cjs checks footprint, flush ground plane, hidden head, matching
endpoints, repeated playback, and glTF validation.
check-preview.cjs checks the review UI, lighting presets and crossing demonstration.
PBR materials remain dynamically lit. No image textures or scene lighting baked.
The existing tank reference is unchanged.
