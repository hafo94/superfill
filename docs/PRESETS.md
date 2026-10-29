# SuperFill — Presets

> **All numeric values in this document are examples.** TODO(owner): replace stone sizes, joints, chord/rise and layer names with your company standard before shipping presets.

## 1. Library locations and precedence

| Location | Path | Purpose |
|---|---|---|
| Built-in | `<bundle>/Contents/presets/` | Ships with the plugin |
| Shared | TODO(owner), e.g. `\\server\cad\SuperFill\presets\` (configurable) | Company standard, shared with colleagues |
| User | `%APPDATA%\SuperFill\presets\` | Personal presets and overrides |

Presets are identified by `id`. If the same `id` exists in several locations: user > shared > built-in. Block libraries referenced by presets live in a `blocks/` folder next to the presets and are resolved relative to the preset's own location. Missing blocks are imported into the drawing automatically from the library DWG.

`SFPRESETS` reloads all locations and prints validation errors as `file: field: message`. Invalid presets are skipped, never half-loaded.

## 2. Units and conversion

All lengths are in **millimeters**; angles in **degrees**. `PresetResolver` converts to drawing units using `INSUNITS` (meters → × 0.001). The resolved values (in drawing units) are what gets stored in the fill (DWG_OUTPUT §3).

## 3. Pattern preset schema

```jsonc
{
  "schema": "superfill.preset/1",
  "id": "sv.bagsatt.smagatsten-90",           // unique, lowercase, dot-separated
  "version": 1,                                // bump when values change
  "name": "Bågsatt smågatsten 90",             // UI label
  "description": "Segmental arcs, small granite setts",
  "kind": "pattern",                           // pattern | composite
  "generator": "segmentalArc",                 // lattice | ring | segmentalArc | band | tile | scatter
                                               // (M10: randomCourses | flagstone | fan)
  "stone": {
    "shape": "rect",                           // rect | wedge | auto
    "length": 90, "width": 90,
    "lengthTolerance": 0,                      // ± mm the fitter may change stone length
    "variation": 0.0                           // 0..0.5 natural length variation (seeded)
  },
  "joints": {
    "nominal": 10, "min": 6, "max": 15,        // joints between stones in a course
    "course": 10, "courseMin": 6, "courseMax": 15, // joints between courses
    "minJointOffset": 30                       // stagger requirement between adjacent courses
  },
  "params": { },                               // generator-specific, §4
  "edges": {
    "rule": "cut",                             // cut | keepIfCenterInside | keepIfFullyInside | drop
    "cutPolicy": "seam",                       // where the fitter puts a forced cut stone: end | middle | seeded | seam
    "sliver": { "policy": "balance", "minFraction": 0.33, "minWidth": 30 } // balance | merge | drop | keep
  },
  "output": {
    "mode": "stoneOutlines",                   // stoneOutlines | jointLines
    "drawBoundary": false,
    "background": null,                        // or { "type": "solid" | "pattern", "pattern": "ANSI31", "scale": 1 }
    "wipeout": false,
    "layers": {                                // role → layer; TODO(owner): your layer standard
      "stone":     { "name": "SF-STEN",     "color": 8 },
      "cut":       { "name": "SF-STEN-KAP", "color": 9 },
      "joint":     { "name": "SF-FOG",      "color": 8 },
      "boundary":  { "name": "SF-GRANS",    "color": 7 },
      "check":     { "name": "SF-KONTROLL", "color": 1 },
      "container": null                        // null = current layer
    }
  },
  "units": { "targetUnitStones": 200 },
  "seed": 1
}
```

Every field except `schema`, `id`, `kind`, `generator` and the generator's required `params` has a default; the validator fills defaults and reports the resolved preset in `SFINFO`.

## 4. Generator parameters

### lattice
| Param | Type | Default | Notes |
|---|---|---|---|
| `bond` | `stack \| running \| herringbone90 \| herringbone45 \| basket` | `running` | herringbone/basket require `length = 2·width + joint` (validator checks within 0.5 mm) |
| `runningOffset` | 0..1 | 0.5 | only for `running` |
| `angle` | deg \| `"auto"` \| `"pick"` | `"auto"` | auto = longest boundary edge |
| `origin` | `"auto"` \| `"pick"` \| `[x, y]` | `"auto"` | auto = boundary extents min |

### ring
| Param | Type | Default | Notes |
|---|---|---|---|
| `center` | `"pick"` \| `"centroid"` | `"pick"` | |
| `medallionRadius` | mm | 400 | 0 = no medallion; stones start at the smallest valid radius |
| `medallion` | `null` \| `{ "block": "...", "library": "..." }` \| `{ "circle": true, "hatch": "SOLID" }` | `null` | |
| `maxRadius` | mm \| `"toBoundary"` | `"toBoundary"` | |
| `fitToOuterCircle` | bool | true | only applies if the outer edge is a concentric circle |
| `startAngle` | deg \| `"auto"` | `"auto"` | phase of ring 0; later rings use staggering |

### segmentalArc (bågsatt)
| Param | Type | Default | Notes |
|---|---|---|---|
| `axis` | deg \| `"auto"` \| `"pick"` | `"auto"` | laying direction (along the street) |
| `chord` | mm | 1500 | arc width across the street (example value) |
| `rise` | mm | 250 | arc height (example value); `0 < rise ≤ chord/2` |
| `bulge` | `forward \| backward` | `forward` | |
| `unitCourses` | even int \| `"auto"` | `"auto"` | courses per unit (auto from `targetUnitStones`) |
| `origin` | `"auto"` \| `"pick"` \| `[x, y]` | `"auto"` | where a seam starts |

### band
| Param | Type | Default | Notes |
|---|---|---|---|
| `courses` | int ≥ 1 | 2 | |
| `along` | `edge \| path` | `edge` | `path` asks for a polyline |
| `side` | `inside \| left \| right \| center` | `inside` | `left/right/center` for paths |
| `edgeJoint` | mm | joints.nominal | gap against the curb/edge |
| `cornerAngle` | deg | 20 | turning angle that counts as a corner |
| `includeHoles` | bool | false | bands around holes too |

### tile (classic SUPERHATCH)
| Param | Type | Default | Notes |
|---|---|---|---|
| `sources` | list of `{ "block" \| "image" \| "xref": name, "library"?, "weight"? }` | required | images: add `"mode": "single" \| "tiled"` |
| `spacing` | `"extents"` \| `[dx, dy]` mm | `"extents"` | |
| `gap` | mm | 0 | added to extents |
| `layout` | `grid \| brick \| hex` | `grid` | |
| `angle`, `origin` | as lattice | | |
| `anchor` | `basePoint \| center` | `center` | |
| `rotationJitter` | deg | 0 | ± per instance |
| `scaleJitter` | 0..0.5 | 0 | ± per instance |
| edge rule default | | `keepIfCenterInside` | |

### scatter
| Param | Type | Default | Notes |
|---|---|---|---|
| `sources` | as tile | required | |
| `minSpacing` | mm | required | Poisson-disk distance |
| `tileSize` | mm \| `"auto"` | `"auto"` | periodic tile, auto = 20 × minSpacing |
| `rotationJitter`, `scaleJitter` | as tile | 180, 0.1 | |

## 5. Composite preset schema

```jsonc
{
  "schema": "superfill.preset/1",
  "id": "sv.rondell.smagatsten",
  "version": 1,
  "name": "Rondell: medaljong + ringar + bågsatt fält + kantband",
  "kind": "composite",
  "separatingJoint": 10,
  "layers": [
    { "name": "Kantband", "priority": 100,
      "shape": { "type": "edgeBand", "width": "auto" },
      "pattern": "sv.band.storgatsten-2rader" },
    { "name": "Ringar", "priority": 80,
      "shape": { "type": "circle", "center": "pick", "radius": 3000 },
      "pattern": "sv.ring.smagatsten-90",
      "overrides": { "params": { "center": "same:shape" } } },
    { "name": "Fält", "priority": 0,
      "shape": { "type": "remainder" },
      "pattern": "sv.bagsatt.smagatsten-90" }
  ]
}
```

- `shape.type`: `boundary | circle | edgeBand | pathBand | pickedLoop | remainder` (ALGORITHMS §10).
- `pattern`: a preset id or an inline pattern preset (without `schema`/`id`).
- `overrides`: deep-merged into the referenced pattern preset.
- `"same:shape"` / `"same:<layerName>"` reuse a picked point so the user picks the center only once.

## 6. Built-in presets for v1 (example values)

| id | Generator | Key values | Notes |
|---|---|---|---|
| `sv.bagsatt.smagatsten-90` | segmentalArc | stone 90×90, joint 10 (6–15), chord 1500, rise 250 | |
| `sv.ring.smagatsten-90` | ring | stone 90×90 `auto`, joint 10 (6–15), medallion 400 | like the reference image |
| `sv.halvforband.marksten-210x140` | lattice running | 210×140, joint 3 (2–5) | |
| `sv.fiskben.marksten-200x98` | lattice herringbone90 | 200×98, joint 4 | satisfies `l = 2w + j` |
| `sv.band.storgatsten-2rader` | band | 220×140, joint 15 (10–20), 2 courses | |
| `sv.rondell.smagatsten` | composite | §5 | |
| `generic.tile.block` | tile | block picked at runtime, grid, center anchor | SUPERHATCH parity |
| `generic.scatter.trad` | scatter | sources from `blocks/vegetation.dwg`, minSpacing 4000 | |

Example full preset (`presets/sv.ring.smagatsten-90.json`):

```json
{
  "schema": "superfill.preset/1",
  "id": "sv.ring.smagatsten-90",
  "version": 1,
  "name": "Cirkelsatt smågatsten 90",
  "kind": "pattern",
  "generator": "ring",
  "stone": { "shape": "auto", "length": 90, "width": 90 },
  "joints": { "nominal": 10, "min": 6, "max": 15, "course": 10, "courseMin": 6, "courseMax": 15, "minJointOffset": 30 },
  "params": { "center": "pick", "medallionRadius": 400, "medallion": { "circle": true, "hatch": null },
              "maxRadius": "toBoundary", "fitToOuterCircle": true, "startAngle": "auto" },
  "edges": { "rule": "cut", "cutPolicy": "seeded", "sliver": { "policy": "balance", "minFraction": 0.33, "minWidth": 30 } },
  "output": { "mode": "stoneOutlines" },
  "seed": 1
}
```

## 7. Validation rules (`PresetValidator`)

- Unknown fields → warning; wrong types/ranges → error.
- `min ≤ nominal ≤ max` for every joint triple; all sizes > 0.
- Herringbone/basket: `|length − (2·width + joint)| ≤ 0.5 mm`.
- segmentalArc: `0 < rise ≤ chord/2`; warn if `rise/chord ∉ [0.05, 0.5]`.
- Composite: referenced presets exist; no cycles; at least one layer; exactly one `remainder` at most.
- Sources: block/image names non-empty; library files exist (warning if not reachable, e.g., network down).

## 8. Overrides in the UI

The palette shows the resolved preset as editable fields. Changed fields are stored as overrides; the fill stores the fully resolved spec plus `presetId` and `presetVersion`. "Save as preset" writes a new user preset from the current settings.
