# SuperFill — Product Specification

Status: Draft v1 · Owner: TODO(owner)

## 1. Purpose

Civil engineers drawing hardscape plans (markbeläggning) need area fills that look like real laid paving: bågsatt smågatsten, circle-laid rondells, bonds, edge bands along curbs, with joints that adapt and stones cut at edges and around manholes. Express Tools SUPERHATCH only tiles one block or image on a grid, hides the overflow with a spatial-filter clip (the hidden geometry comes back on explode), and produces heavy drawings. SuperFill replaces it.

## 2. Goals

- **G1 Easy:** one command with a palette, presets and live preview. Full SUPERHATCH parity (block, image, xref, wipeout) plus pattern generators.
- **G2 Light:** periodic patterns are stored as shared block definitions. Target ≥ 10× fewer entities than naive per-stone output for areas ≥ 100 m² (see TESTING budgets).
- **G3 Explodable:** `EXPLODE` → unit blocks + cut stones → plain geometry.
- **G4 Editable:** parametric (reopen settings, regenerate) and manual (after explode, edit stones like any geometry).
- **G5 Realistic:** adaptive joints within tolerances, true cut stones, sliver avoidance, staggered joints, patterns within patterns.

## 3. Non-goals (v1)

- 3D / draping on surfaces; Civil 3D surface or corridor integration.
- Output as `.pat` hatch patterns or dynamic blocks.
- AutoCAD LT (no .NET support).
- Cutting the content of xrefs or raster images.

## 4. Environment

- AutoCAD and Civil 3D. **TODO(owner): versions.** Default assumption: 2025+ (.NET 8).
- Drawing units usually meters. Coordinates in SWEREF 99 (northing around 6 000 000 m), so all block definitions use local coordinates near the origin (DWG_OUTPUT §2).
- The plugin bundle and the preset library are deployed from a shared network folder so colleagues use the same standards. **TODO(owner): path.**

## 5. Concepts

- **Fill:** one container block reference covering one boundary, plus its stored settings.
- **Boundary:** one or more outer loops plus holes. Loops consist of line and arc segments (polylines with bulges, circles); splines and ellipses are approximated.
- **Region layer:** a part of a fill with its own pattern (medallion, rings, band, field). A fill is a priority-ordered stack of region layers.
- **Generator:** produces stones or symbol placements for a region layer.
- **Course (rad):** a row of stones along a path.
- **Unit:** a repeating group of stones/symbols stored once as a block (ALGORITHMS §12).
- **Joint (fog):** gap between stones, with nominal/min/max width; course joints (between courses) are separate parameters.
- **Cut stone (kapad sten), sliver (flisa):** see glossary.

## 6. Functional requirements

### 6.1 Sources (SUPERHATCH parity and more)

- **R-SRC-1 Generated stones:** rectangle/square or wedge (trapezoid) stones defined by size in the preset. This is the main case and needs no block.
- **R-SRC-2 Block:** any block in the drawing, or a block from a library DWG referenced by a preset (imported automatically if missing).
- **R-SRC-3 Selected objects:** converted on the fly into a named source block `SF-SRC-{name}`.
- **R-SRC-4 Image:** an image definition, placed once (stretched/clipped to the region) or tiled. Images are clipped with the image clip boundary and are **not explodable** (documented exception).
- **R-SRC-5 Xref:** tiled, edge rule limited to Keep/Drop (xref content can't be cut). Xref instances are **not explodable** (AutoCAD limitation; documented exception).
- **R-SRC-6 Wipeout:** a mask of the region, optionally placed under the fill.
- **R-SRC-7 Mixed sources:** a weighted list of blocks for Tile and Scatter (e.g., three tree symbols).

### 6.2 Generators

| Generator | Swedish term | Key parameters | Milestone |
|---|---|---|---|
| Lattice: stack, running bond, herringbone 90°/45°, basket weave | rakt förband, halvförband, fiskben, korgflätning | stone size, joints, angle, origin, offset | M1 / M10 |
| Ring | cirkelsättning | center, medallion radius, stone, joints, stone shape, stagger | M4 |
| Segmental arc | bågsättning (bågsatt smågatsten) | axis, chord, rise, bulge direction, stone, joints | M5 |
| Band | kantband / kantrad | number of courses, stone, joints, along edge or along path | M6 |
| Tile (classic SUPERHATCH) | — | source(s), spacing, grid/brick/hex, angle, jitter | M7 |
| Scatter | — | source(s), min spacing, jitter, seed | M7 |
| Irregular: random-length courses, flagstone | oregelbundet förband, kullerstens-/hällmönster | length range, stagger, cell size | M10 |
| Fan / scale | solfjädersmönster | scale radius, courses per scale | M10 |

### 6.3 Adaptive joints

- **R-FIT-1** Every course holds a whole number of stones; joints are redistributed so they stay within `[jMin, jMax]`.
- **R-FIT-2** If that's impossible: change the stone count; then use the stone length tolerance (if the preset allows); then insert one cut stone placed by policy (`end`, `middle`, `seeded`, `seam`).
- **R-FIT-3** Joints in adjacent courses/rings never line up closer than `minJointOffset` (staggering, förband).
- **R-FIT-4** Optional per-stone length variation for natural stone, seeded and reproducible.

### 6.4 Composition (pattern within pattern)

- **R-CMP-1** A fill is an ordered list of region layers; higher priority is carved out first. A separating joint is kept between layers.
- **R-CMP-2** Layer shapes: `boundary` (whole area), `circle(center, radius)`, `edgeBand(width)`, `pathBand(path, width, side)`, `pickedLoop`, `remainder`.
- **R-CMP-3** Holes: picked closed objects (brunnar, trädgropar, stolpar) with a clearance; optional surround band around a hole (M10).

### 6.5 Edges

- **R-EDGE-1** Edge rule per source: `cut` | `keepIfCenterInside` | `keepIfFullyInside` | `drop`. Defaults: `cut` for stones, `keepIfCenterInside` for blocks.
- **R-EDGE-2** `cut` produces real trimmed geometry: closed polylines for stones; trimmed lines/arcs/polylines/hatches for block content. Text, attributes and unsupported entities in a cut block instance are kept if their anchor point is inside, else dropped.
- **R-EDGE-3** Sliver rule: a cut piece below `minFraction` of the stone area or narrower than `minWidth` is handled by `balance` (default for course patterns: the neighbor is cut too so both pieces are equal), `merge`, `drop` or `keep`. All slivers are flagged in quantities and optionally drawn on a check layer.

### 6.6 Output

- **R-OUT-1** One container block reference per fill. Content: unit block references + raw geometry (DWG_OUTPUT §1).
- **R-OUT-2** Drawing modes: `stoneOutlines` (each stone a closed polyline) and `jointLines` (one curve per course separator plus one segment per joint; lighter).
- **R-OUT-3** Forbidden objects: see CLAUDE.md rule 1.
- **R-OUT-4** Every created block: `Explodable = true`, `BlockScaling = Uniform`.
- **R-OUT-5** Entities on preset layers, color/linetype ByLayer. Layers are created if missing.
- **R-OUT-6** Optional per layer: background hatch (solid or ANSI) of the region, drawn boundary, wipeout under the fill.

### 6.7 Editing

- **R-EDIT-1** `SFEDIT` reopens the settings; Apply regenerates in place (same container insert handle).
- **R-EDIT-2** Associative boundary: editing the boundary objects regenerates the fill after the command ends. Deleting them keeps the fill and drops the association.
- **R-EDIT-3** Move/rotate/mirror/copy of the container works; copies are independent (copy-on-write of definitions, DWG_OUTPUT §5).
- **R-EDIT-4** After explode the result is plain geometry without plugin metadata; it can't be regenerated.
- **R-EDIT-5** Origin, angle and phase can be re-picked in `SFEDIT`.

### 6.8 Commands

| Command | Purpose |
|---|---|
| `SFILL` | Create a fill with the palette and live preview |
| `-SFILL` | Create a fill from the command line (scriptable, used by integration tests) |
| `SFEDIT` / `-SFEDIT` | Edit a fill's settings and regenerate |
| `SFREGEN` | Regenerate selected fills, or `All` |
| `SFQTY` | Quantities report (command line + CSV, optional AutoCAD table) |
| `SFEXPLODE` | Fully explode a fill to plain geometry, strip metadata, purge its blocks |
| `SFINFO` | Show a fill's preset, layers, counts and size estimate |
| `SFPRESETS` | Reload the preset library and report validation errors; open the folder |

All commands work identically in AutoCAD and Civil 3D. Each command is one undo step.

### 6.9 Quantities (mängdning)

- **R-QTY-1** Per region layer: area, full stones, cut stones, slivers flagged, total count; per source block: instance counts.
- **R-QTY-2** CSV export; optional AutoCAD Table.

### 6.10 Presets

- **R-PRE-1** JSON preset library with three locations (built-in, shared network folder, user) and override precedence user > shared > built-in (PRESETS §1).
- **R-PRE-2** All lengths in presets are in millimeters; converted using the drawing's `INSUNITS`.
- **R-PRE-3** Composite presets (e.g., "Rondell") combine pattern presets into region layers.
- **R-PRE-4** Presets are validated on load; errors are reported with file and field.

## 7. Non-functional requirements

- **Performance and size:** budgets in TESTING §5.
- **Robustness:** boundaries with arcs, many vertices, holes touching the outer loop, and very large coordinates. Invalid input (open loops, self-intersections that can't be repaired) gives a clear message, never a crash.
- **Undo:** one undo step per command.
- **No data loss:** regeneration never modifies or deletes objects outside the fill's own definitions.
- **Localization:** UI in English first, strings in resource files so Swedish can be added.

## 8. Release acceptance criteria (v1)

1. All presets in PRESETS §6 generate correctly on all sample boundaries (TESTING §4) with all invariants passing.
2. A bågsatt fill of 1 000 m² meets the time and size budgets.
3. `EXPLODE` twice on any fill (except image/xref sources) leaves only Polyline/Line/Arc/Circle/Hatch/Wipeout plus clones of source-block entities.
4. Editing the boundary regenerates the fill; copying a fill and editing the copy doesn't change the original.
5. A composite "Rondell" (medallion + rings + bågsatt field + edge band) works end to end from the palette.
6. The manual checklist in TESTING §7 passes in both AutoCAD and Civil 3D.

## 9. Glossary

| Swedish | English | Meaning |
|---|---|---|
| markbeläggning | paving, hardscape | surface finish of streets, squares, walkways |
| gatsten | setts | natural stone blocks used for paving |
| smågatsten | small setts | small, roughly cube-shaped granite setts |
| storgatsten | large setts | larger, rectangular granite setts |
| marksten | concrete pavers | |
| bågsättning, bågsatt | segmental arc laying | courses laid in repeating shallow arcs |
| solfjädersmönster | fan pattern | scale-shaped fans of arcs |
| cirkelsättning | circle laying | concentric rings around a center |
| medaljong | medallion | centerpiece of a circular pattern |
| förband | bond | how joints are offset between courses |
| halvförband | half / running bond | joints offset by half a stone |
| fiskbensmönster | herringbone | |
| korgflätning | basket weave | |
| rad | course | one row of stones |
| fog | joint | gap between stones |
| kapad sten | cut stone | stone trimmed to fit an edge |
| flisa | sliver | cut piece too small to lay |
| kantband, kantrad | edge band | courses following an edge |
| kantstöd | curb | |
| ränndal | gutter, drainage channel | |
| brunn | manhole, gully | |
| trädgrop | tree pit | |
| mängdning | quantity takeoff | |

## 10. Open questions

- TODO(owner): supported AutoCAD/Civil 3D versions.
- TODO(owner): layer standard and colors.
- TODO(owner): standard stone sizes and joint tolerances per material.
- TODO(owner): should quantities go into an AutoCAD Table by default, or CSV only?
- TODO(owner): product name and command prefix (default `SF`).
