# Building damage and lighting

Buildings use independent 32 x 32 map-unit sections (four per large tile), with **3 HP per section by default**. The first hits crack and darken only the struck area. The last hit removes that section's roof and wall geometry, scatters debris, and removes its movement/bullet obstruction. A full tank requires a 64-unit-wide cleared passage. Power shots damage nearby sections within their existing blast radius.

This applies to all nine concept building families, their legacy aliases, and the two existing city building prefabs. Forest islands and other environment objects keep their existing rules. The three concept templates have been updated, and the current authoring draft is preserved.

Each building has warm front/rear window lights. A light dims with its section's health and turns off when that section is destroyed. They use no real-time shadows; the original window emission remains part of the model. Default light intensity is 1 and range is 2.2 Unity units.

Select a building in the editor to adjust **Override Damage**, **Hit Points**, **Invulnerable**, **Normal Shots**, and **Power Shots**. Building HP is per section. In the Unity Inspector, **Override Building Lighting** and **Building Lighting** control the placed building's lights, intensity (0–8), and range (0.1–8). These settings survive JSON save/open and local playtests. Disabling an override restores the building defaults.

The simulation owns section health and collision. The renderer clips the existing textured model to the same world-space sections, including resized and rotated buildings, and caps exposed cut surfaces. Intact models retain their original renderer. Damage geometry is prepared on the first hit and combined into one renderer per building; temporary meshes/materials are released when the map closes. Debris is cosmetic and does not block movement.

Multiplayer snapshots include partial terrain health as well as alive/dead state, so a replica can restore cracked and destroyed sections on joining. The focused snapshot test passes; this change was not tested in a live multi-client network session.

Validation on 7 October 2026: **127 building checks**, **718 concept-map checks**, and the existing editor/campaign checks passed. Coverage includes actual projectile contact, normal and power damage, selective immunity, tank-sized breaches, light dimming, all 13 building prefab keys, rotated/resized models, save/open, and replica restoration. A running local playtest also confirmed three real projectiles remove the targeted section and its collision. The draft layout, terrain, roster, and non-building behavior were compared with the backup and preserved.

Unity visual review: [damage stages and lights](ConceptMaps/Previews/building-damage-and-lights.png). Columns show intact, hit, a removed corner, and a breach; the upper row is oblique and the lower row is top-down.

Run **Battle Cities > Level Editor > Validate building damage and lights** for focused checks. `BuildingDamageSetup.Apply()` is an explicit one-time migration utility; it never runs automatically on scene entry. Backups are under `Library/BuildingDamageBackups`.
