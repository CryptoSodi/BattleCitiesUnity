# Battle presentation pass

## Assets and ownership

- Forest – Low Poly Toon Battle Arena / Tower Defense Pack: the original `Assets/AurynSky/Forest Pack/Textures/Water.psd` supplies broad color movement beneath the cartoon ripple pattern.
- Free Quick Effects Vol. 1: imported the URP package's original Prefabs, Materials and Textures. `IntegrateBattleVisuals` creates separate game prefabs and URP particle materials in `Assets/BattleCities/Resources/CombatVfx`; original vendor files are preserved.
- Projectile Factory for Toon Projectiles by Hovl Studio is an integration pack. Its underlying prefab dependencies are absent, so it is not referenced by gameplay.

Run **Battle Cities > Build Imported Combat Visuals** in Edit mode to rebuild the adapted materials and prefabs. This overwrites only the generated CombatVfx assets and BattleVisualAssets configuration.

## Runtime behavior

- Current surface art direction follows the user's close-up cartoon pool reference: cyan water, rounded cells with blue centers, thin bright white caustics, and soft cyan light around their edges. `BuildWaterRipples` bakes a seamless 512-square distance texture using staggered cells with variable radii and smooth intersections. Curved shared boundaries replace the older stretched ribbons. The runtime shader uses two texture samples, gently drifting UVs, antialiased edges, and brightness that responds to daylight/night while retaining the blue/cyan hue.
- Water tiles form a single surface with continuous world-space texture coordinates. A shared, subdivided mesh stores distance to the complete shoreline, including concave corners and T-junctions. Shore foam appears only on exposed edges. Surface placement avoids the old ground overlap. Flow uses the game's presentation clock.
- Following the user's recessed-pool reference, water now sits at Y=-0.18, below the ground at Y=-0.025. `BattleGroundSurface` removes water footprints from both the base ground and painted path/road/meadow regions, preserving their original UV alignment. Road markings skip water.
- `BattleWaterBanks` traces the joined shoreline into closed loops and builds a narrow ground-textured lip, shaded soil faces, and rounded inner corners. Dry islands retain their inner banks; diagonally touching pools remain separate. There are no internal banks between connected water tiles. A subdued contact shadow replaces the old straight white perimeter. The bank uses one mesh/material per adjoining ground surface, is built only on stage load, and adds no colliders. Meshes and materials are released with the stage.
- Muzzle flashes and hits use Quick Effects MuzzleFlash_01 and Impact_01; heavy explosions use Explosion_02 with expanding shockwaves. Each category can use eight pooled instances, with 24 total. Imported loops, particle collisions, audio and lights are disabled; one-shot burst timing is normalized for reuse. Pool cleanup checks visible particle counts after the emission window because paused ParticleSystems can report IsAlive after their particles are gone.
- Every bullet impact emits directional sparks and small simulated debris, including nonlethal tank hits. Charged hits use larger bursts. Sparks are capped at 384 and combat flash lights at six; lights do not cast shadows.
- The user explicitly approved the fragmented bullet effect. Keep its appearance and timing intact when revising water or explosions. The smoke revision does not change its emitter settings, random sequence, or debris behavior.
- Explosions and charged impacts leave rounded gray smoke that expands, rises, drifts and fades after the short fire pulse. The previous stationary black flare layers are disabled. `BattleBlastSmoke` uses a separate random generator, a single 128-particle system, and at most 16 pending plumes. Ordinary bullet impacts do not add blast smoke.
- Particle systems advance manually on the game's pause clock. Stage changes clear transient effects. Gameplay simulation, damage and collision rules are unchanged.
- The battle camera uses the BattleLook volume: neutral tonemapping, restrained bloom, modest desaturation and FXAA. Weather lighting has lower daytime intensity and brighter blue night fill. The sun travels from the camera's top left through overhead noon to the top right. The base emblem retains more illumination at night.

## Verification (2026-10-07)

The project compiled and `BattlePresentationChecks.Run()` passed in Play mode: surviving armor impact, unchanged sparks/debris, smoke rising after fire, smoke pause retention and stage cleanup, bounded burst load, expiry, HDR/profile wiring, exact water coverage/upward triangle winding on all 12 water stages, shared T-junction shoreline distances, muzzle reuse within 0.4 seconds, and preservation of delayed explosion emitters. `ShootingHealthChecks.Run()` and `git diff --check` also passed.

Reviewed settled water rendering on stages 31 and 5, normal impact sparks, explosion progression/shockwaves, and day/night lighting. An actual simulation bullet hit a visible armored tank, reducing it from 6 to 5 HP and producing 12 sparks plus 3 debris fragments. The stage 31 water surface contains 4,464 shared vertices and renders in one draw.

The final water and smoke shaders compile without errors, and the final console check returned no errors. The curved blue/white ripples and smoke were visually reviewed in daylight and at night. Smoke remains readable after the fire disappears. Earlier development compiler diagnostics were fixed before these final checks.

The earlier editor stall was resolved by preserving/compressing its 21.9 GB generated log and restarting Unity. The scene recovery file was preserved before restart. Final preview placements were discarded by returning to Edit mode; source scenes were not modified.

No on-device/mobile performance run or player build has been performed.

## Recessed shoreline verification (2026-10-07)

`BattleWaterBasinChecks.Run()` validates ground and overlay cutouts on all 35 maps, below-ground water and bank winding on all 12 water stages, and synthetic T-junction, dry-island and diagonal-contact cases. It is included in `BattlePresentationChecks.Run()` and is also available as **Battle Cities > Validate Water Basins** in Play mode. All checks passed, as did the existing smoke, fragmentation and shooting regressions. Stage 31 has 16 closed shoreline loops, 1,600 bank segments, and one bank draw when only the base ground material is involved. The original water surface remains 4,464 shared vertices.

Reviewed the actual Game view on grassy stage 5 and the more complex connected channels in stage 31, including day and night shading. Rounded corners and shaded far banks make the depth visible while retaining the approved blue/white flow pattern. This revision does not change fire, smoke, sparks, debris, collision footprints or damage. Preview camera/lighting changes are temporary and are discarded on leaving Play mode.

## Cartoon surface refinement (2026-10-07)

The user approved basin depth and requested a closer match to the bright cyan cartoon pool surface. Only `ForestBattleWater.shader`, the water portion of `IntegrateBattleVisuals`, and its generated water material/texture changed. Ground cutouts, bank geometry and all combat assets remain intact. **Battle Cities > Rebuild Cartoon Water Only** updates the water independently, without rebuilding approved combat prefabs. `BattlePresentationChecks.Run()` passes with the revised material, including the existing basin, smoke and fragmentation checks. The final shader compiles successfully. Reviewed the actual Game view in daylight and at night; player builds and mobile performance remain untested.

## Lighting, clouds and rain refinement (2026-10-07)

- `BattleWeather.DirectionToSun` replaces the old fixed azimuth. Sunrise is at -X/+Z (screen top left), noon reaches 72 degrees elevation, and sunset is at +X/+Z (screen top right). The night fill returns continuously to the next sunrise; time wrapping does not jump. Stage-load reflection captures use the correct initial lighting.
- Six cloud layers drift at two heights with rounded silhouettes and soft volume shading. Their soft ground shadows project away from the current light direction and are clipped to the battlefield. Storms cool the light and soften direct shadows. Clouds remain translucent so combat stays readable.
- Rain uses up to 900 tapered, angled ribbon quads and a separate pool of 160 landing effects. Sampled impacts produce tiny ground splashes or expanding rings on water at its recessed height. A quarter-tile height lookup updates with terrain rebuilds, including destroyed walls. Rendering uses two reusable meshes; rain does not add physics colliders or use gameplay randomness.
- Rain intensity and cloud toggles retain the existing F1/debug controls. Clear weather remains the default. Rain, cloud drift, shadows and splashes freeze with the game's presentation clock. Stage changes clear landing effects; disabling rain clears the pool, while reducing intensity to zero lets existing landings fade.

`BattleWeatherChecks.Run()` passed in Play mode: sun screen directions, overhead noon, continuous dusk/time wrap, intensity scaling, water rings, pause retention, drift, bounded heavy rain, expiry and toggles. `BattlePresentationChecks.Run()` and `ShootingHealthChecks.Run()` also passed. The new cloud and particle shaders compile successfully. Reviewed actual Game views at sunrise, sunset and in rain. No player build or on-device performance profiling was performed.
