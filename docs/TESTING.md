# SuperFill — Testing

## 1. Strategy

| Level | Where | What | Runs |
|---|---|---|---|
| Unit + property | `tests/SuperFill.Core.Tests` | geometry primitives, fitter, stagger, lattice cells, clipping, sliver rule, cost model, preset validation | every change |
| Scenario | same | every preset × every sample boundary, checked against the invariants (§3) | every change |
| Snapshot | same | geometry hash per scenario; PNG/SVG renders for visual review | every change |
| Benchmarks | `SuperFill.Cli bench` | time, entity counts, estimated size vs budgets (§5) | per milestone |
| Integration (headless AutoCAD) | `tests/integration` via `accoreconsole` | emit, persistence, regen, copy, explode, AUDIT, real DWG size | per milestone (needs AutoCAD) |
| Manual GUI | §7 checklist, run by the owner | palette, preview, grips/selection, Civil 3D | end of UI milestones |

Core is where almost all bugs live, so it gets almost all tests. Always look at the rendered PNGs after changing a generator: overlaps, gaps and wrong cuts are obvious visually.

## 2. Unit and property tests (examples)

- **Fitter** (FsCheck, random `L, s, j, jMin, jMax`): Σ lengths + Σ joints = L; joints within bounds or a fallback status; deterministic; `n` changes by at most ±2 from `round(L/(s+j))` unless a cut stone is inserted.
- **Gap check:** for random arc radii, built `rect` stones have real inner/outer gaps matching `j ∓ (w/2)Δθ` within 2 %, and `auto` switches to `wedge` below the minimum radius.
- **Lattice cells:** for every bond, generate 10 × 10 cells: no overlaps, every joint width = `j` within `Epsilon`, stone density matches `stones per cell / |det(a1, a2)|`.
- **Segmental arc:** arc endpoints lie on the seams at `v = j·p`, apex at `j·p + h`; units tile without overlap across seams and course boundaries.
- **Sliver rule:** constructed cases where a boundary cuts 5 %, 20 %, 50 % of a stone produce balance/merge/drop results as specified.
- **Clipping:** stones crossing a boundary with an arc, a concave corner and a hole produce valid cut polygons (no self-intersections, area matches Clipper2 intersection).
- **Cost model:** a unit used once is inlined; a 200-stone unit used 50 times becomes a block.
- **Serialization:** FillSpec / Region JSON round-trips exactly; chunking/reassembly of long strings.
- **Determinism:** same inputs → same geometry hash; changing one boundary vertex changes only nearby stones (compare hashes per root node).

## 3. Invariants (checked for every scenario)

- **I1 No overlap:** pairwise intersection area of output stones (flattened, spatial index for pairs) < `Epsilon²·count`.
- **I2 Containment:** area(union of stones ∖ layer region) ≈ 0.
- **I3 Joints:** every joint between neighbouring stones in a course within `[jMin, jMax]`, except stones flagged `JointOutOfRange`.
- **I4 Coverage:** stone area / layer area within the expected range for the pattern, derived per preset from stone and joint sizes (e.g., about 0.81 ± 0.03 for 90 mm setts with 10 mm joints: 90² / 100²).
- **I5 Slivers:** no unflagged cut stone below the sliver thresholds.
- **I6 Stagger:** no unflagged adjacent-course joints closer than `minJointOffset`.
- **I7 Tree consistency:** flattening the emit plan (expanding all block references) gives the same stones as the resolver's flat output.
- **I8 Allowed primitives only:** the emit plan contains only allowed primitive types; every block plan is marked explodable/uniform.

## 4. Sample boundaries

Stored in `samples/boundaries/*.json` (local coordinates, meters):

```json
{ "units": "m",
  "loops": [
    { "role": "outer", "closed": true, "vertices": [[0,0,0], [20,0,0], [20,10,0.4142], [10,15,0], [0,10,0]] },
    { "role": "hole",  "circle": { "center": [8,5], "radius": 0.4 } }
  ] }
```

Vertices are `[x, y, bulge]` (bulge applies to the segment starting at that vertex).

| File | Shape | Exercises |
|---|---|---|
| `rect-20x10.json` | rectangle | baseline, lattice alignment |
| `l-shape-arc.json` | L-shape with one arc corner | concave corner, arc cut |
| `rondell-r6.json` | circle r = 6 m | ring fit-to-outer-circle, composite rondell |
| `street-curve.json` | 8 m wide, 60 m long street with a 40 m radius bend | bågsatt along a curve, band along curbs |
| `plaza-holes.json` | 30 × 25 m polygon with 3 circular holes (manholes, tree pit) | holes, clearance, slivers |
| `sliver-trap.json` | rectangle 1.003 × stone pitch wide | forces sliver handling |
| `big-1000m2.json` | 40 × 25 m rectangle | performance budget |
| `sweref-far.json` | `rect-20x10` offset to x = 150 000, y = 6 500 000 | large coordinates (Acad integration only) |

## 5. Performance and size budgets

Initial targets (calibrate in Milestone 3 and record the real numbers in PROGRESS.md):

| Scenario | Budget |
|---|---|
| Core: bågsatt 90 mm setts on `big-1000m2` (~100 000 stones) | ≤ 1.5 s (Release, single machine) |
| Emit to DWG, same scenario | ≤ 3 s |
| Palette preview update on a 200 m² fill | ≤ 300 ms to first transient draw |
| Entities in all SF definitions vs naive per-stone output (periodic patterns ≥ 100 m²) | ≤ 10 % |
| DWG size growth vs naive output (same scenarios) | ≤ 20 % (≥ 5× smaller) |

**Naive baseline:** `-SFILL … Naive` (debug option) emits every stone as a raw polyline, no units. The CLI `naive-baseline` reports its entity count; the integration test measures its real DWG size.

**Cost-model calibration (M3):** integration script creates 10 000 entities of each kind (4-vertex polyline, line, arc, insert, block definition with N polylines) in a blank drawing, saves, and records size deltas. Results go into `CostModel` defaults.

## 6. Integration tests (accoreconsole)

- `SuperFill.Acad` references only AcDbMgd and AcCoreMgd, so it loads in `accoreconsole.exe`.
- Every command has a command-line variant usable from scripts, e.g. `-SFILL Preset sv.ring.smagatsten-90 BoundaryFile samples/boundaries/rondell-r6.json Center 6,6 Seed 1`.
- A hidden test command `SFTESTDUMP <outfile.json>` writes: entity counts by type (model space and per SF block), block definitions with `Explodable`/`BlockScaling`, fill metadata, a recursive programmatic explode check (`Entity.Explode` into a collection, verify types, no DB changes), and the geometry hash.
- `tools/run-integration.ps1`: for each `tests/integration/*.scr`: copy `samples/dwg/blank.dwg` to a temp folder, run `accoreconsole.exe /i <copy> /s <script> /l en-US`, collect the dump JSON and the saved DWG size, compare with `tests/integration/expected/*.json` (counts exact, size within tolerance).

Integration scenarios: create each built-in preset; SFEDIT change joint → regen; SFREGEN idempotent (same hash); COPY then edit the copy (original unchanged); modify an associated boundary → regen after command; rigid MOVE of fill + boundary → no regen; `sweref-far` coordinates; EXPLODE twice → allowed types only; SFEXPLODE → no SF blocks left after purge; AUDIT → no errors; WBLOCK the fill to a new file and INSERT into another drawing with an existing fill.

## 7. Manual checklist (owner runs in AutoCAD and Civil 3D)

- [ ] `NETLOAD` both DLLs; `SFILL` opens the palette; presets listed from all three locations.
- [ ] Pick point inside an area with an island → preview shows region outline and pattern within ~0.3 s.
- [ ] Change chord/rise/joint in the palette → preview updates smoothly; Escape cancels without leftovers.
- [ ] Apply → one fill; one `UNDO` removes it completely; `REDO` restores it.
- [ ] Select the fill → `SFEDIT` → palette shows its settings; change stone size → regenerates in place.
- [ ] Stretch a boundary vertex → fill regenerates after the command.
- [ ] Copy the fill, edit the copy → original unchanged.
- [ ] `EXPLODE` twice → editable stones on the right layers; `PURGE` cleans up.
- [ ] Rondell composite: pick boundary, pick center once → medallion, rings, bågsatt field, edge band.
- [ ] Image and block sources (SUPERHATCH parity) behave as documented.
- [ ] Plot preview looks right; large fill pans/zooms smoothly.
- [ ] Repeat the core steps in Civil 3D.

Report results (and screenshots for visual issues) back to Claude Code; failed items become tasks in PROGRESS.md.
