# SuperFill — Algorithms

All math here is implemented in `SuperFill.Core`. Formulas are first-order guides; the implementation must verify results geometrically (TESTING §3 invariants) rather than trust the formulas.

## 0. Conventions

- **Units:** Core works in drawing units in the fill's local frame. Presets are in mm and converted once by `PresetResolver` using the units factor.
- **Angles:** radians, counter-clockwise positive. Bulge = tan(sweep/4), positive = CCW (AutoCAD convention).
- **Orientation:** outer loops CCW, holes CW after normalization.
- **Randomness:** `DeterministicRng` (xoshiro256**, seeded via SplitMix64). Derive child seeds with `SeedDeriver.Derive(seed, layerIndex, i, j, …)` so a random choice depends only on *where* it is (lattice index, course index), not on generation order. Editing a boundary must not reshuffle the rest of the pattern.
- **Default tolerances** (`Tolerances`, in mm, converted to drawing units):

| Name | Default | Use |
|---|---|---|
| `Epsilon` | 0.001 mm | geometric equality |
| `ChordError` | 0.5 mm | arc/spline discretization for clipping and offsets |
| `ClipperResolution` | 0.01 mm | integer grid for Clipper2 (`long = round(x / res)`) |
| `HashResolution` | 0.01 mm | rounding before geometry hashing |
| `FullInsideFraction` | 0.9999 | intersection-area ratio counted as "fully inside" |

## 1. Region preparation

1. Convert every loop to `Curve2` segments (lines and arcs). Splines/ellipses arrive pre-discretized from the adapter.
2. Discretize arcs for Clipper2: segment count `n = ceil(|sweep| / (2·acos(1 − e/R)))` with `e = ChordError` (guard: if `R ≤ e`, use `n = max(4, ceil(|sweep|/(π/8)))`).
3. Build `Paths64`; `Union` outer loops (non-zero fill) and `Difference` holes inflated by their clearance (`ClipperOffset`, round joins). This also repairs touching/overlapping input. If the union is empty or changes area by more than 0.1 %, report "invalid boundary" with the offending location.
4. Build `EdgeGrid`: a uniform grid (cell size ≈ 2 × typical stone length, at most ~1 M cells) mapping each cell to the region edges crossing it. Used for broad-phase classification and point-in-region with cached cell states (inside / outside / mixed).

## 2. Course model (shared by ring, bågsatt, band, irregular courses)

A **course** is the band between two separator curves (`lower`, `upper`), shrunk by half the course joint on each side, plus a **centerline** used for measuring and fitting.

- Separators and centerlines are `SmoothRun`s: `LineRun`, `ArcRun` or `PolylineRun` (arc-length parameterized polyline; normals at vertices averaged so stones turn smoothly).
- A course path may consist of several runs separated by **corners** (turning angle ≥ `cornerAngle`, default 20°). Each run is fitted separately.
- **Joint line** at centerline position `c`: the line through `P(c)` in direction `N(c)` (normal to the centerline; radial on arcs). At corners and seams, the joint line is given explicitly (bisector or seam line).
- **Stone construction** between joint centers `c_k` and `c_{k+1}` with joint widths `j_k`, `j_{k+1}`:
  - `wedge`: shift the two joint lines by `j/2` toward the stone (parallel shift along the centerline tangent), intersect them with the lower and upper course edges → 4 corners. Edges along arcs are written with bulges (exact and still one polyline). Result: constant-width joints, trapezoid stones on curves.
  - `rect`: a rectangle of length = chord from `P(c_k + j_k/2)` to `P(c_{k+1} − j_{k+1}/2)` and width `w`, centered on the centerline chord. Joints become wedge-shaped on curves (see §3 gap check).
  - The course edges are the true band edges, so where courses narrow (bågsatt near the seams) stones narrow with them.

## 3. Course fitter (`CourseFitter`)

Input: effective length `L`, nominal stone length `s`, nominal joint `j`, bounds `jMin…jMax`, stone length tolerance `sTol` (0 = none), end condition, cut policy, optional curvature info for the gap check.

Joint count by end condition: `closed` → `n`; `halfJointEnds` (fixed ends at seams/corners, half a joint at each end) → `n`; `stoneToEnd` → `n − 1`.

```
pitch = s + j
n0 = max(1, round(L / pitch))
for n in [n0, n0−1, n0+1, n0−2, n0+2] where n ≥ 1:
    j' = (L − n·s) / jointCount(n)
    if jMin ≤ j' ≤ jMax and gapCheck(n, j'): candidate(score = |j' − j|)
if any candidate: return best (ties → smaller |n − n0|, then smaller n)

if sTol > 0:                        // stone flex (natural stone tolerance)
    for n near n0: s' = clamp((L − jointCount(n)·j) / n, s − sTol, s + sTol); j' = (L − n·s') / jointCount(n)
    accept best valid as above

// cut stone fallback: n full stones + 1 cut stone, nominal joints
choose n so the remainder c = L − n·s − jointCount(n+1)·j lies in [cMin, s)
cMin = sliver.minFraction · s  (if c ≥ s − Epsilon it is a full stone → re-run with n+1)
place the cut stone by policy: end | middle | seeded | seam; status = CutStoneInserted
```

**Gap check for `rect` stones on a curve** (centerline radius `R`, stone width `w`): neighbouring end faces rotate by `Δθ ≈ (s + j)/R`, so the joint is narrower on the inside and wider on the outside:

```
innerGap ≈ j − (w/2)·Δθ   must be ≥ jMin
outerGap ≈ j + (w/2)·Δθ   must be ≤ jMax
```

This also gives the minimum radius for `rect`: `R ≥ (w/2)(s + j)/(j − jMin)`. Below it, shape `auto` switches the course to `wedge`; shape `rect` fits anyway and flags `JointOutOfRange`. Verify the real gaps from the built polygons in tests.

**Natural variation** (`variation = v > 0`): draw lengths `l_i ~ U[s(1−v), s(1+v)]` from the course's derived seed, then `j' = (L − Σl_i)/jointCount`; if out of bounds, adjust `n` and redraw from the same seed stream.

Properties (tested): Σ stone lengths + Σ joints = `L` within `Epsilon`; all joints within bounds unless status says otherwise; deterministic.

## 4. Staggering (`Stagger`, R-FIT-3)

Adjacent courses must not have joints closer than `minJointOffset` (default `s/3`), measured along the shared separator.

- **Rings:** joints of ring `k` sit at angles `φ_k + i·2π/n_k`. Evaluate 32 evenly spaced phases in `[0, 2π/n_k)`; pick the one maximizing the minimum arc distance (at the separator radius) to ring `k−1`'s joints. Ties → smallest `φ`. If the best is below `minJointOffset`, flag `JointAlignment`.
- **Bågsatt:** odd courses are shifted by half a pitch along the centerline (their seam stones become cut stones). This is why units contain an even number of courses.
- **Band:** each course run is fitted independently; if a run violates the offset, try `n ± 1`, else flag.
- **Lattice bonds:** handled by the lattice definition itself.

## 5. Lattice generator (bonds, tile, periodic tiles)

A lattice has basis vectors `a1`, `a2`, origin `o`, rotation `α`, and **cell content** (stones or symbols in cell coordinates). Positions: `o + R(α)(i·a1 + j·a2)`.

Bond cells, with pitch `p = w + j` (joint included) and stones inset by `j/2` from their cell rectangles:

| Bond | Requirement | a1 | a2 | Cell content (cell rectangles before inset) |
|---|---|---|---|---|
| Stack (rakt) | — | `(l+j, 0)` | `(0, p)` | `[0,l+j]×[0,p]` |
| Running (halvförband), offset `f` (default ½) | — | `(l+j, 0)` | `(f·(l+j), p)` | `[0,l+j]×[0,p]` |
| Herringbone 90° (fiskben) | `l = 2w + j` | `(p, p)` | `(2p, −2p)` | H: `[0,2p]×[0,p]`, V: `[0,p]×[p,3p]` |
| Herringbone 45° | as above | as 90°, plus 45° extra rotation | | |
| Basket weave (korgflätning) | `l = 2w + j` | `(2p, 2p)` | `(2p, −2p)` | two horizontal stones in `[0,2p]×[0,2p]`, two vertical stones in `[2p,4p]×[0,2p]` |

Verify each with the overlap/gap invariants before trusting it.

**Super-cells:** group `m × k` lattice cells into one unit so a unit holds about `targetUnitStones` (default 200) stones. Hierarchy: super-cell → row of cells → stones. Roots cover the layer's bounding box (in lattice coordinates, via the inverse basis) plus one super-cell margin.

**Tile (classic SUPERHATCH):** cell content = one source instance. Default spacing = source extents + gap. Layouts: grid `a1=(dx,0), a2=(0,dy)`; brick `a2=(dx/2, dy)`; hex `a1=(d,0), a2=(d/2, d·√3/2)`. Per-instance rotation/scale jitter and weighted source choice use `SeedDeriver(seed, i, j)`. Instances are plain block references (each has its own transform, so variation costs nothing). Super-cells are only used without per-instance variation, or with a small number of pre-built variant super-cells chosen by hash.

## 6. Ring generator (cirkelsättning)

Parameters: center `C`, medallion radius `r0`, stone `s × w`, joint `j` (+bounds), course joint `jc`, shape `rect | wedge | auto`, `maxRadius` (default: farthest point of the layer region from `C`), `fitToOuterCircle`.

1. Ring `k` centerline radius `R_k = r0 + jc + w/2 + k·(w + jc)`; continue while `R_k − w/2 < maxRadius`.
2. If `fitToOuterCircle` and the layer's outer edge is a circle concentric with `C` (within `ChordError`): choose ring count `m` and adjust `jc` within its bounds so the last ring's outer edge plus the edge joint lands exactly on that circle.
3. Per ring: closed fit with `L = 2πR_k` (§3), then phase by §4.
4. Stones are built with the course model (§2): separators are circles at `R_k ± (w + jc)/2`.
5. **Units:** every stone in a ring is identical, so the stone is one unit placed `n_k` times by rotation. Optional **sector units**: if `n_k` has a divisor `d` giving `n_k/d` stones per sector in `[8, 64]`, make a sector unit inserted `d` times. The planner decides (§12).
6. **Medallion:** either a generated circle (optional hatch) or a source block placed at `C`, uniformly scaled to fit `r0`.

## 7. Segmental arc generator (bågsättning / bågsatt smågatsten)

Local frame: `v` = laying direction (along the street), `u` = across. Default `v` = direction of the longest boundary segment; can be picked as two points. Parameters: chord `B` (across, along `u`), rise `h` (`0 < h ≤ B/2`), bulge `forward | backward`, stone `s × w`, joint `j`, course joint `jc`, origin.

Geometry (forward bulge; mirror `v` for backward):

```
R  = (B²/4 + h²) / (2h)          arc radius
φ  = asin(B / (2R))              half-angle
La = 2Rφ                         arc length of one scallop
p  = w + jc                      course pitch along v
Scallop k, separator j: arc with center (k·B + B/2, j·p − (R − h)) in (u,v),
points (k·B + B/2 + R·sin t, j·p − (R − h) + R·cos t), t ∈ [−φ, φ]
→ endpoints at v = j·p on the seam lines u = k·B, apex at v = j·p + h.
```

- Course `j` of scallop `k` is the band between separators `j` and `j+1` (shrunk by `jc/2`); its centerline is separator `j` shifted by `p/2` along `v`. Because separators are translations (not concentric), courses get narrower toward the seams (perpendicular width `≈ p·cos θ`, θ = tangent angle). The course model handles this automatically.
- **Seams:** the lines `u = k·B`. Each scallop's stones are clipped to the strip `[k·B + j/2, (k+1)·B − j/2]`, which produces the diagonal joints where arcs meet.
- **Fitting:** even courses use `halfJointEnds` over the centerline length between the seams; odd courses use the same joint pitch shifted by half a pitch (§4) with their end stones cut by the seams.
- **Units:** all scallops and all even (resp. odd) courses are identical translations. Unit = one scallop × `K` courses (`K` even, chosen near `targetUnitStones`). Sub-unit = one course of one scallop. Root lattice: `(B, 0)` and `(0, K·p)` in `(u, v)`. Cover the layer bounds plus one unit margin (arcs extend `h` beyond the chord line).
- Validation: `h/B` outside `[0.05, 0.5]` → warning; stones narrower than `minWidth` near seams are cut stones and follow the sliver rule.
- Later (M10): "interlocked seam" option where seam ownership alternates per course instead of a straight seam cut.

## 8. Band generator (kantband)

- **Along the layer edge:** separator `k` = inward offset of the boundary by `e + k·(w + jc)` (edge joint `e`), computed with `ClipperOffset` (miter joins, miter limit 4) on the discretized loop, then wrapped as `PolylineRun`s split at corners. Holes get bands too unless disabled.
- **Along a path:** separators are offsets of a selected open polyline on one side or centered; ends are butt.
- Each run is fitted with `halfJointEnds`; at corners the joint line is the corner bisector, so the two runs meet in a miter.
- Band region (for composition): `boundary − offset(boundary, −(courses·(w + jc) − jc + e))`.
- Bands are generally not periodic: emit stones as raw geometry (units only if runs repeat exactly, e.g., long straight runs split into identical chunks — optional).

## 9. Scatter and irregular generators

- **Scatter (Poisson disk):** generate one **periodic** Poisson-disk tile (Bridson, `k = 30`, distances measured with wrap-around) of size `T × T` (default `T = 20 × spacing`), seeded. Repeat it on a lattice → units (super light, and editing the boundary doesn't reshuffle). Per-point rotation/scale/source choice from `SeedDeriver(seed, tileIndex, pointIndex)`.
- **Random-length courses (M10):** straight courses whose stone lengths are drawn from `[lMin, lMax]` and fitted to a period `W` (stone-to-end over a wrapped course, so each course is `W`-periodic). Each course gets its own phase so no vertical seam appears; reject/redraw if a joint lands within `minJointOffset` of a joint in the course below. Unit = `W` × `K` courses; a stone belongs to the unit where it starts.
- **Flagstone (M10):** periodic Poisson tile → Voronoi cells (half-plane clipping against neighbours found via the grid) → 2 Lloyd iterations → inset by `j/2` with `ClipperOffset` → periodic unit.
- **Fan / solfjäder (M10):** scales on a lattice; each scale is a set of concentric course bands around the scale center, bounded by the outer arcs of the neighbouring scales; reuse the course model.

## 10. Composition (`Composer`)

```
available = boundary region (outer ∖ holes with clearance)
for layer in layers ordered by priority (high → low):
    shape = evaluate(layer.shape, available, boundary)   // circle, edgeBand, pathBand, pickedLoop, remainder
    area  = shape ∩ available
    layerRegion[layer] = offset(area, −separatingJoint/2)
    available = available ∖ area
```

- `circle.radius = "toBoundary"` → distance from the center to the farthest vertex of `available`.
- `edgeBand.width = "auto"` → from the band pattern's course count, stone width and joints.
- Empty layers are skipped with a warning.

## 11. Resolver (classification, cutting, slivers)

Walk the placement tree of each layer:

```
resolve(node):
    hull = node.unit.Hull transformed by node.Xf
    state = classify(hull)                      // EdgeGrid broad phase, then exact
    inside  → keep node as an insert (count TotalStones)
    outside → drop
    crossing:
        children → resolve each child
        stones   → for each stone: classify; inside → raw full stone; crossing → StoneCutter; outside → drop
```

- `classify`: if the hull's bbox touches only grid cells with no region edges, use the cached cell state. Otherwise test hull edges against region edges in those cells; no intersections → point-in-region of one hull vertex; any intersection → crossing.
- `StoneCutter`: Clipper2 `Intersect(stone, region)`; each resulting polygon is a separate cut stone. Ratio ≥ `FullInsideFraction` → treat as full.
- **Sliver rule** on each cut piece: sliver if `area < minFraction · stoneArea` or `2·area/perimeter < minWidth`.
  - `balance` (course patterns): take the neighbouring stone in the same course on the inside, compute the combined length `T = neighbour + j + piece` along the centerline, and re-cut both at the midpoint so each is `(T − j)/2`. Result: two cut stones, no sliver.
  - `merge`: union the piece with the neighbour sharing the longest joint edge (closing the joint gap) → one larger cut stone.
  - `drop` / `keep`: as named. Every sliver is flagged (`Flags`, check layer).
- **Symbols** (tile/scatter): edge rule on the instance hull / center. `cut` → transform the source geometry, clip curves with `CurveClipper` (line/arc vs region edges, keep pieces whose midpoint is inside), clip hatch loops with Clipper2, keep/drop other entities by anchor (`SourceEntityP`).

## 12. Units, dedup and the cost model (`Planner`)

- **UnitKey:** produced by the generator from canonical inputs (generator kind, rounded parameters, course parity, stone shape, style). In Debug builds the planner also hashes each unit's actual geometry (vertices rounded to `HashResolution`) and asserts that equal keys mean equal geometry.
- **Cost model** (`CostModel`, estimates of bytes in the DWG): `polyline = cPoly + cVtx·vertices`, `line/arc/circle = cCurve`, `insert = cIns`, `blockDef = cDef + content`. Decide bottom-up for every unit used `u` times (after resolve, including nested uses):

```
inlineCost(U) = Σ cost of U's content (children already decided)
blockify(U)   = u ≥ 2  and  cDef + inlineCost(U) + u·cIns  <  u·inlineCost(U)
```

  Inlined units have their content transformed into the parent. Initial constants are placeholders; Milestone 3 calibrates them by measuring DWG size growth for 10 000 entities of each kind (TESTING §5).
- **Deterministic naming:** blocks are numbered by first use in a deterministic traversal (layer order, root order, child order): `U01`, `U02`, …

## 13. Joint-line mode

Stones are always computed internally; joint-line mode changes what is emitted.

- **Course patterns:** each unit emits, for each of its courses, the **lower separator** (Circle, Arc or Polyline) and one **joint segment** per joint spanning the course band including the course joints. Bågsatt units also own the seam on their lower-`u` side. This ownership rule means adjacent instances never duplicate a line.
- **Lattice patterns:** once per cell, inflate every stone by `j/2` (the cells then tile), collect edges, merge overlapping collinear segments (group by line, union intervals), and keep only edges not lying on the cell's upper/right lattice boundary.
- **Crossing units:** resolve in the stone model (with the sliver rule), then derive joint lines from the resulting stones: separators clipped to the region plus one segment per stone end.
- `drawBoundary` defaults to `true` in joint-line mode (otherwise edge stones look open).

## 14. Quantities

From the resolved tree: per layer, area (region area), full stones (`TotalStones` of kept nodes + raw full stones), cut stones, slivers (by policy outcome), and per-source instance counts. Exact, no sampling.

## 15. Performance notes

- Keep per-stone work allocation-free: struct arrays, reused buffers, no LINQ in hot paths.
- Precompute unit hulls in unit coordinates; transform only the hull for classification.
- Parallelize per layer and per root-node chunk with `Parallel.For`, writing results into index-addressed arrays so the merged output is deterministic.
- Clipper2 only for crossing stones; most stones never touch it.
