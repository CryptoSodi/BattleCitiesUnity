# Ground Four-Direction Defense Turret

Ground emplacement based on the approved yellow Battle Cities reference.

## Runtime hierarchy

- `GroundTurret_Root`: placement root at ground center.
- `Base_Static`: stationary foundation and armor ring.
- `TurretYaw`: rotate around local Y in the exported Y-up GLB.
- `GunPitch`: cannon elevation and recoil pivot.
- `MuzzleFlash`: projectile/effect spawn reference.
- `Reload_1_Green` through `Reload_4_Green`: loaded-round indicators.
- `Reload_1_Red` through `Reload_4_Red`: spent/reloading indicators.
- `AttackRange_Light`: forward amber spotlight parented to `TurretYaw`.

The exported asset uses meters, Y up, and +Z forward.

## Four-direction aiming

The turret supports exactly four headings: 0, 90, 180, and 270 degrees. Quantize the requested heading before assigning `TurretYaw.localRotation`. Do not use continuous target-facing rotation. The `TurretCardinal4Way` clip demonstrates the four positions.

## Illuminated armor-nut reload indicators

The four circular armor nuts around the turret ring double as reload indicators. Immediately after firing, show all four red indicator nodes and hide all four green nodes. As reload progresses, restore the green nodes in order from 1 to 4. The embedded `ReloadCycle` clip demonstrates this sequence. Runtime code may drive the named red/green pairs directly.

## Attack-range light

`AttackRange_Light` is a 30-degree amber spot light with a 5 m source range. It rotates with the turret and aims slightly downward. Unity may tune its intensity and range for the gameplay grid while preserving its hierarchy and direction.

## Materials and gameplay

All armor and metal use dynamically lit metallic/roughness PBR materials. Emission is restricted to indicator lenses, warning lenses, vents, and the muzzle flash. Keep gameplay colliders separate from the visual geometry. Recommended proxies are a 1.15 m radius cylinder for the base and a simple box for the rotating turret.

Muzzle flash, projectiles, smoke, impacts, sound, target detection, cardinal snapping, and reload state authority belong in runtime gameplay code.
