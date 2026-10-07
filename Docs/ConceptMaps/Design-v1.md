# Battle Cities: detailed map specifications

Design version 1 — 7 October 2026. These proposed 25 × 25 layouts translate the three concept images into exact gameplay placements. Coordinate data below takes precedence over the illustrative images; decorative props are not traced pixel-for-pixel. New mechanics are proposals, not existing features.

## Shared coordinate system

- Field: **25 × 25 cells**, **1600 × 1600 logical units**. A cell is 64 × 64 logical units and corresponds to 1 × 1 Unity world units in the current editor. This is a chosen concept-map size; it does not change project defaults.
- Origin: north-west/top-left `(0,0)`. `x` grows right; `y` grows down. Valid cells are 0–24. North is decreasing `y`.
- Rectangles use `(x, y, width, height)` with exclusive right/bottom edges. Example `(4,11,2,3)` covers x=4–5 and y=11–13.
- Route waypoints and attack approaches are **tank top-left anchors**, not tank centers; each anchor must fit the full 1 × 1 tank footprint.
- Logical coordinates: multiply every cell coordinate/dimension by 64. Unity ground position: `(x,0,-y)`; place a centered prefab at `(x+w/2,0,-(y+h/2))`.
- All unspecified cells are the map default traversable ground. Boundary outside the field is impassable. Bridge surfaces override water only within their rectangles.
- Tanks occupy 1 × 1 cell. Aim for 2-cell main lanes; a 1-cell opening is the minimum tank fit. Fine positioning uses half cells (32 logical units), compatible with the editor snap.
- Height is visual, not a second movement layer. Keep roofs/canopies inside blocked footprints. No gameplay lanes underneath scenery.

## Shared base and spawns

| Element | Cell rectangle / facing | Logical rectangle |
|---|---|---|
| HQ | `(11.5,23.5,2,1.5)` | `(736,1504,128,96)` |
| P1 (player) | (9,24,1,1), north | (576,1536,64,64) |
| E1 (enemy) | (2,0,1,1), south | (128,0,64,64) |
| E2 (enemy) | (12,0,1,1), south | (768,0,64,64) |
| E3 (enemy) | (22,0,1,1), south | (1408,0,64,64) |

The exact existing base size is retained. The simplified ASCII map marks every touched base cell as Q; the rectangle is authoritative. Enemy exits must remain clear for the first two cells south. The base has the three HQ-* brick guards listed on each map. Attack approach tank anchors are `(10,21)`, `(12,21)`, `(14,21)`; reaching those anchors does not mean entering the blocked HQ pocket. Only one player spawn is specified, matching the current editor validation.

## Legend

`.` default ground; `#` brick; `S` steel; `H` solid building; `T` solid tree island; `O` rock; `~` water; `=` permanent bridge; `w` breakable bridge; `L` logs; `s` sandbags; `r` rubble; `m` mud; `^`/`v` north/south conveyor; `F` fuel barrel; `Q` base; `P` player; `E` enemy. Fractional rectangles are conservatively shown in every touched cell.

## Canal Crossroads

- Ground: slate road. Difficulty target: medium; crossing control.
- Purpose: Three canal crossings, two permanently safe alternatives, brick-separated approaches.
- Required scenario: Keep both permanent crossings open even after the timber bridge is lost.
- Concept: [Canal Crossroads](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/canal-crossroads.png)

### Coordinate sketch

```text
   0000000000111111111122222
   0123456789012345678901234
00 ..E.........E.........E..
01 .........................
02 ......HHH.......HHH......
03 ...##.HHH.#...#.HHH.##...
04 ..........#...#..........
05 HH........#.S.#........HH
06 HH.#..HHH.......HHH..#.HH
07 HH.#.SHHH.......HHHS.#.HH
08 ...#......#...#......#...
09 ....s..####...####.......
10 .........................
11 ~~~~==~~~~~~w~~~~~~==~~~~
12 ~~~~==~~~~~~w~~~~~~==~~~~
13 ~~~~==~~~~~~w~~~~~~==~~~~
14 .........................
15 .......###.....###..s....
16 ......HHH...S...HHH......
17 ...#..HHH.......HHH..#...
18 ...#.......#.#.......#...
19 HH.#.S.....#.#.....S.#.HH
20 HH.....###.....###.....HH
21 HH.....................HH
22 ..........#####..........
23 ..........#QQQ#..........
24 .........P#QQQ#..........
```

### Exact element placement

| ID | Type | x | y | width | height | Detail |
|---|---|---:|---:|---:|---:|---|
| W01 | water | 0 | 11 | 25 | 3 | Main east-west canal. |
| BR01 | bridge_permanent | 4 | 11 | 2 | 3 | West concrete crossing. |
| BR02 | bridge_breakable | 12 | 11 | 1 | 3 | Narrow optional timber shortcut; entry-bank landing anchors (12,10) and (12,14). |
| BR03 | bridge_permanent | 19 | 11 | 2 | 3 | East concrete crossing. |
| H01 | building | 6 | 2 | 3 | 2 | North-west warehouse, blue roof. |
| H02 | building | 16 | 2 | 3 | 2 | North-east warehouse, coral roof. |
| H03 | building | 0 | 5 | 2 | 3 | West harbor stores. |
| H04 | building | 23 | 5 | 2 | 3 | East harbor stores. |
| H05 | building | 6 | 6 | 3 | 2 | Canal court west. |
| H06 | building | 16 | 6 | 3 | 2 | Canal court east. |
| H07 | building | 6 | 16 | 3 | 2 | South-west depot. |
| H08 | building | 16 | 16 | 3 | 2 | South-east depot. |
| H09 | building | 0 | 19 | 2 | 3 | Lower west houses. |
| H10 | building | 23 | 19 | 2 | 3 | Lower east houses. |
| B01 | brick | 3 | 3 | 2 | 1 |  |
| B02 | brick | 10 | 3 | 1 | 3 |  |
| B03 | brick | 14 | 3 | 1 | 3 |  |
| B04 | brick | 20 | 3 | 2 | 1 |  |
| B05 | brick | 3 | 6 | 1 | 3 |  |
| B06 | brick | 21 | 6 | 1 | 3 |  |
| B07 | brick | 7 | 9 | 3 | 1 |  |
| B08 | brick | 15 | 9 | 3 | 1 |  |
| B09 | brick | 10 | 8 | 1 | 2 |  |
| B10 | brick | 14 | 8 | 1 | 2 |  |
| B11 | brick | 7 | 15 | 3 | 1 |  |
| B12 | brick | 15 | 15 | 3 | 1 |  |
| B13 | brick | 3 | 17 | 1 | 3 |  |
| B14 | brick | 21 | 17 | 1 | 3 |  |
| B15 | brick | 7 | 20 | 3 | 1 |  |
| B16 | brick | 15 | 20 | 3 | 1 |  |
| B17 | brick | 11 | 18 | 1 | 2 |  |
| B18 | brick | 13 | 18 | 1 | 2 |  |
| S01 | steel | 12 | 5 | 1 | 1 | Blocks direct central shot lane. |
| S02 | steel | 5 | 7 | 1 | 1 |  |
| S03 | steel | 19 | 7 | 1 | 1 |  |
| S04 | steel | 12 | 16 | 1 | 1 | Forces a turn before base approach. |
| S05 | steel | 5 | 19 | 1 | 1 |  |
| S06 | steel | 19 | 19 | 1 | 1 |  |
| SB01 | sandbag | 4 | 9 | 1 | 1 | Half of west bridge approach remains open. |
| SB02 | sandbag | 20 | 15 | 1 | 1 | Half of east bridge approach remains open. |
| HQ-N | brick | 10.5 | 22.5 | 4 | 0.5 | Destructible north screen; protects HQ from direct vertical fire. |
| HQ-W | brick | 10.5 | 23 | 0.5 | 2 | Left side wall; extends to field boundary. |
| HQ-E | brick | 14 | 23 | 0.5 | 2 | Right side wall; extends to field boundary. |

### Routes and decorative zones

- West permanent route: (2,0) → (2,10) → (5,10) → (5,15) → (4,15) → (4,21) → (10,21)
- East permanent route: (22,0) → (22,9) → (19,9) → (19,16) → (20,16) → (20,21) → (14,21)
- Optional timber crossing: (12,10) → (12,14) — Only the straight crossing segment; north/south feeder routes turn around steel S01/S04.
- Decorative rectangle (0,10,4,1): Bollards and mooring details inside the bank footprint; no collider beyond bank.
- Decorative rectangle (21,10,4,1): Matching dock accents.

Validation: **passed**. All four spawn anchors can reach all three HQ approach anchors with intact brick. Tested states: intact, timber_removed, west_only, east_only.

## Timber Ambush

- Ground: dry tan dirt. Difficulty target: medium; optional breaches and slow shortcuts.
- Purpose: Woodland islands and logging roads, with no opaque canopy over traversable lanes.
- Required scenario: Map must remain navigable with all log barricades intact and after all are removed.
- Concept: [Timber Ambush](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/timber-ambush.png)

### Coordinate sketch

```text
   0000000000111111111122222
   0123456789012345678901234
00 ..E.........E.........E..
01 .....TTT.........TTT.....
02 .....TTT.........TTT.....
03 TT......HHLL.LLHH......TT
04 TT.#....HH.....HH....#.TT
05 TT.#........S........#.TT
06 ...#.TT...........TT.#...
07 .....TT.##.....##.TT.....
08 .....TT...........TT.....
09 ....mmm.LL.....LL.mmm....
10 ....mmmS..#...#..Smmm....
11 ....HH..rr#...#rr..HH....
12 ....HH.............HH....
13 ........TT.LLL.TT........
14 ........TT.....TT........
15 ...##...TT.....TT...##...
16 TT.........HHH.........TT
17 TT.........HHH.........TT
18 TT..LL.##.......##.LL..TT
19 TT..........S..........TT
20 .....TT...#...#...TT.....
21 .....TT...........TT.....
22 ..........#####..........
23 ..........#QQQ#..........
24 .........P#QQQ#..........
```

### Exact element placement

| ID | Type | x | y | width | height | Detail |
|---|---|---:|---:|---:|---:|---|
| T01 | tree_block | 0 | 3 | 2 | 3 |  |
| T02 | tree_block | 23 | 3 | 2 | 3 |  |
| T03 | tree_block | 5 | 1 | 3 | 2 |  |
| T04 | tree_block | 17 | 1 | 3 | 2 |  |
| T05 | tree_block | 5 | 6 | 2 | 3 |  |
| T06 | tree_block | 18 | 6 | 2 | 3 |  |
| T07 | tree_block | 8 | 13 | 2 | 3 |  |
| T08 | tree_block | 15 | 13 | 2 | 3 |  |
| T09 | tree_block | 0 | 16 | 2 | 4 |  |
| T10 | tree_block | 23 | 16 | 2 | 4 |  |
| T11 | tree_block | 5 | 20 | 2 | 2 |  |
| T12 | tree_block | 18 | 20 | 2 | 2 |  |
| H01 | building | 8 | 3 | 2 | 2 | Ranger cabin. |
| H02 | building | 15 | 3 | 2 | 2 | Ranger cabin. |
| H03 | building | 4 | 11 | 2 | 2 | West logging hut. |
| H04 | building | 19 | 11 | 2 | 2 | East logging hut. |
| H05 | building | 11 | 16 | 3 | 2 | Central sawmill. |
| B01 | brick | 3 | 4 | 1 | 3 |  |
| B02 | brick | 21 | 4 | 1 | 3 |  |
| B03 | brick | 8 | 7 | 2 | 1 |  |
| B04 | brick | 15 | 7 | 2 | 1 |  |
| B05 | brick | 10 | 10 | 1 | 2 |  |
| B06 | brick | 14 | 10 | 1 | 2 |  |
| B07 | brick | 3 | 15 | 2 | 1 |  |
| B08 | brick | 20 | 15 | 2 | 1 |  |
| B09 | brick | 7 | 18 | 2 | 1 |  |
| B10 | brick | 16 | 18 | 2 | 1 |  |
| B11 | brick | 10 | 20 | 1 | 1 |  |
| B12 | brick | 14 | 20 | 1 | 1 |  |
| S01 | steel | 12 | 5 | 1 | 1 |  |
| S02 | steel | 7 | 10 | 1 | 1 |  |
| S03 | steel | 17 | 10 | 1 | 1 |  |
| S04 | steel | 12 | 19 | 1 | 1 |  |
| L01 | log | 10 | 3 | 2 | 1 | Optional upper-left breach. |
| L02 | log | 13 | 3 | 2 | 1 | Optional upper-right breach. |
| L03 | log | 8 | 9 | 2 | 1 |  |
| L04 | log | 15 | 9 | 2 | 1 |  |
| L05 | log | 11 | 13 | 3 | 1 | Central log screen, independent cells. |
| L06 | log | 4 | 18 | 2 | 1 |  |
| L07 | log | 19 | 18 | 2 | 1 |  |
| M01 | mud | 4 | 9 | 3 | 2 | West shortcut. |
| M02 | mud | 18 | 9 | 3 | 2 | East shortcut. |
| R01 | rubble | 8 | 11 | 2 | 1 |  |
| R02 | rubble | 15 | 11 | 2 | 1 |  |
| HQ-N | brick | 10.5 | 22.5 | 4 | 0.5 | Destructible north screen; protects HQ from direct vertical fire. |
| HQ-W | brick | 10.5 | 23 | 0.5 | 2 | Left side wall; extends to field boundary. |
| HQ-E | brick | 14 | 23 | 0.5 | 2 | Right side wall; extends to field boundary. |

### Routes and decorative zones

- Dry west flank: (2,0) → (2,22) → (9,22) → (9,21) → (10,21)
- Dry east flank: (22,0) → (22,22) → (15,22) → (15,21) → (14,21)
- Decorative rectangle (5,1,3,2): Conifers and cut stumps inside T03; keep canopy inside collider footprint.
- Decorative rectangle (17,1,3,2): Conifers inside T04.
- Decorative rectangle (11,16,3,2): Blue sawmill roof; do not overhang the roads.

Validation: **passed**. All four spawn anchors can reach all three HQ approach anchors with intact brick. Tested states: intact, logs_removed.

## Ironworks Yard

- Ground: grey factory concrete. Difficulty target: medium-high; directional movement and local hazards.
- Purpose: Factory courts, optional through-conveyors and barrel-assisted cover breaches.
- Required scenario: Both belt end cells stay open; normal-ground routes remain available without using either belt.
- Concept: [Ironworks Yard](C:/repos/BattleCity/art_exports/concepts/stages/2026-10-07/ironworks-yard.png)

### Coordinate sketch

```text
   0000000000111111111122222
   0123456789012345678901234
00 ..E.........E.........E..
01 .........................
02 .....HHH........HHH......
03 ...#.HHH.##...##HHH..#...
04 ...#.................#...
05 ...#........S........#...
06 HH...HH..........v.....HH
07 HH...HH..#F......v.....HH
08 HH.....^.#F......v.#F..HH
09 .......^.#.##....v.#F....
10 ...S...^.................
11 .....##^......FF.HH..S...
12 ............##.#.HH......
13 ....rr.........#.........
14 ..................rr.....
15 ...#.................#...
16 ...#.HHH...S....HHH..#...
17 ...#.HHH........HHH..#...
18 .............S...........
19 ........##.....##........
20 HH.....................HH
21 HH.....................HH
22 HH........#####........HH
23 ..........#QQQ#..........
24 .........P#QQQ#..........
```

### Exact element placement

| ID | Type | x | y | width | height | Detail |
|---|---|---:|---:|---:|---:|---|
| H01 | building | 5 | 2 | 3 | 2 | North-west factory. |
| H02 | building | 16 | 2 | 3 | 2 | North-east factory. |
| H03 | building | 0 | 6 | 2 | 3 | West stores. |
| H04 | building | 23 | 6 | 2 | 3 | East stores. |
| H05 | building | 5 | 6 | 2 | 2 | West conveyor-side machinery. |
| H06 | building | 17 | 11 | 2 | 2 | East conveyor-side machinery. |
| H07 | building | 5 | 16 | 3 | 2 | Lower west depot. |
| H08 | building | 16 | 16 | 3 | 2 | Lower east depot. |
| H09 | building | 0 | 20 | 2 | 3 |  |
| H10 | building | 23 | 20 | 2 | 3 |  |
| B01 | brick | 3 | 3 | 1 | 3 |  |
| B02 | brick | 21 | 3 | 1 | 3 |  |
| B03 | brick | 9 | 3 | 2 | 1 |  |
| B04 | brick | 14 | 3 | 2 | 1 |  |
| B05 | brick | 9 | 7 | 1 | 3 |  |
| B06 | brick | 19 | 8 | 1 | 2 |  |
| B07 | brick | 5 | 11 | 2 | 1 |  |
| B08 | brick | 15 | 12 | 1 | 2 |  |
| B09 | brick | 11 | 9 | 2 | 1 |  |
| B10 | brick | 12 | 12 | 2 | 1 |  |
| B11 | brick | 3 | 15 | 1 | 3 |  |
| B12 | brick | 21 | 15 | 1 | 3 |  |
| B13 | brick | 8 | 19 | 2 | 1 |  |
| B14 | brick | 15 | 19 | 2 | 1 |  |
| S01 | steel | 12 | 5 | 1 | 1 |  |
| S02 | steel | 3 | 10 | 1 | 1 |  |
| S03 | steel | 21 | 11 | 1 | 1 |  |
| S04 | steel | 11 | 16 | 1 | 1 |  |
| S05 | steel | 13 | 18 | 1 | 1 |  |
| C01 | conveyor | 7 | 8 | 1 | 4 | West belt; clear end cells (7,7) and (7,12). Direction: north. |
| C02 | conveyor | 17 | 6 | 1 | 4 | East belt; clear end cells (17,5) and (17,10). Direction: south. |
| F01 | barrel | 10 | 7 | 1 | 1 |  |
| F02 | barrel | 10 | 8 | 1 | 1 |  |
| F03 | barrel | 20 | 8 | 1 | 1 |  |
| F04 | barrel | 20 | 9 | 1 | 1 |  |
| F05 | barrel | 14 | 11 | 1 | 1 |  |
| F06 | barrel | 15 | 11 | 1 | 1 |  |
| R01 | rubble | 4 | 13 | 2 | 1 |  |
| R02 | rubble | 18 | 14 | 2 | 1 |  |
| HQ-N | brick | 10.5 | 22.5 | 4 | 0.5 | Destructible north screen; protects HQ from direct vertical fire. |
| HQ-W | brick | 10.5 | 23 | 0.5 | 2 | Left side wall; extends to field boundary. |
| HQ-E | brick | 14 | 23 | 0.5 | 2 | Right side wall; extends to field boundary. |

### Routes and decorative zones

- West normal-ground flank: (2,0) → (2,21) → (10,21)
- East normal-ground flank: (22,0) → (22,21) → (14,21)
- Decorative rectangle (5,2,3,2): Blue roofs and orange gantry inside H01; no beam crossing the street.
- Decorative rectangle (16,2,3,2): Freight crates inside H02.

Validation: **passed**. All four spawn anchors can reach all three HQ approach anchors with intact brick. Tested states: intact, barrels_removed.

## Terrain behavior and starting tuning

Numeric values below are starting tuning targets for prototypes; they are not measured balance results. A standard-shell hit means the baseline normal shell damage event.

- **brick**: movement: blocked while intact shots: Existing destructible brick rules; visual rectangles must be subdivided to engine brick units.
- **steel**: movement: blocked shots: Existing steel rules; do not override current weapon penetration behavior.
- **building**: movement: blocked shots: Blocks shots; static indestructible collision footprint for this proposal.
- **rock**: movement: blocked shots: Blocks shots; static indestructible collision footprint.
- **tree_block**: movement: blocked shots: Blocks shots as a static scenery island; not the existing pass-through jungle/bush tile.
- **water**: movement: blocked unless an intact bridge covers the entire tank footprint over water shots: Pass over water; no water collision for bullets.
- **bridge_permanent**: movement: passable surface over water shots: No interaction; bridge remains intact.
- **bridge_breakable**: movement: passable until fully destroyed shots: Proposed durability 3 standard-shell hits. Final hit starts a 1.5s flashing collapse warning; whole bridge then becomes water. occupants: At collapse, move any tank still on the deck to its recorded entry-bank landing anchor; if occupied, defer collapse until that landing area clears. Never drown or trap a tank silently.
- **log**: movement: blocked until removed shots: 2 standard-shell hits destroy a single 1x1 log cell; a long log region is a row of independent cells, not one global object.
- **sandbag**: movement: Tank nose contact triggers a 0.6s crush; tank remains outside the blocker until it is removed. shots: 1 standard-shell hit removes one cell; shots stop on the hit cell.
- **rubble**: movement: 0.70x normal tank speed shots: Pass through; no bullet-speed change.
- **mud**: movement: 0.65x normal tank speed shots: Pass through; no bullet-speed change. Uses existing mud visual theme; movement modifier is proposed.
- **conveyor**: movement: Adds world-direction drift at 0.30x the tank normal forward speed, even without throttle; normal collision still applies. shots: No interaction. Floor remains traversable when switched off. direction: Per-element property; visual arrows match it.
- **barrel**: movement: blocked until explosion shots: First hit arms a 0.6s flash fuse, then one explosion with 1.5-cell radius and one standard-shell damage event per target; brick may be destroyed, steel/static scenery is unchanged. Same damage rule for both tank teams. chain: A blast arms nearby barrels rather than detonating them instantly. Every barrel can explode once.

## Build order and test checklist

1. Lay down default ground, water and bridge rectangles; then static blockers and brick/steel; then modifiers and interactable props; finally HQ/spawns and decoration. Keep terrain data separate from decorative meshes.
2. First prototype logs, rubble and barrels; keep conveyors inactive and the timber bridge permanent until their mechanics are implemented. This preserves the designed routes.
3. Verify the half-cell path results in Unity using the actual tank collider, then playtest enemy steering, passing space, projectile sightlines, barrels, bridge occupancy at collapse and conveyor entry/exit.
4. Preserve existing mode rules, enemy roster, drops, rewards and win/lose behavior. No new wave count, economy or progression is specified by this map proposal.
5. Co-op spawn placement, runtime destructible state replication and balance tuning remain separate implementation work; do not assume this document updates those systems.

## Verification and source facts

- Automated checks: bounds, unintended terrain overlap, spawn footprints and south exits, full-tank four-direction reachability, explicit waypoint routes, conveyor endpoints and barrel distance from HQ/spawns.
- Canal tests include the center bridge removed, only the west permanent bridge available, and only the east permanent bridge available.
- Static reachability excludes combat, live AI, moving tanks and timing. Do not treat the concept images as exact validated tilemaps.
- Source facts checked: `src/config.ts` has a 64-unit large tile and a 128×96 base; `Assets/BattleCities/LevelEditor/LevelEditorDocument.cs` exposes 4–40-cell dimensions, 16/32/64 snap, 64×64 spawn footprints, 128×96 base footprints and exactly one player spawn; `LevelEditorGrid.cs` converts logical positions to world positions using x/64 and -y/64.
- JSON is a documented **design schema**, not a file to import directly into the current level editor. New terrain requires engine support and a deliberate conversion.
