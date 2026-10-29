# SuperFill — Roadmap

Work top to bottom. Each milestone ends with: all tests green, PNG renders reviewed, PROGRESS.md updated, and a short list of what the owner should test in AutoCAD. Don't start the next milestone until the owner confirms.

## M0 — Foundation

- [ ] Solution and projects as in ARCHITECTURE §2; target frameworks per owner's AutoCAD version (default net8.0 / net8.0-windows).
- [ ] AutoCAD references (Copy Local off) for Acad / Acad.UI; `Plugin.cs` with a `SFHELLO` command.
- [ ] Core: `Vec2`, `Transform2`, `Segment2`, `Arc2`, `Loop`, `Region`, `Tolerances`, `DeterministicRng`, `SeedDeriver`.
- [ ] `DebugScene` + CLI `render` (PNG via SkiaSharp + SVG) for a boundary JSON; sample boundaries from TESTING §4.
- [ ] Test project with one passing test; `tools/run-integration.ps1` skeleton that runs `SFHELLO` in accoreconsole.
- [ ] Create `docs/PROGRESS.md` from the template below.

**Accept:** `dotnet test` green; `sfcli render` produces PNGs of all sample boundaries; `SFHELLO` works in AutoCAD and in accoreconsole.

## M1 — Core geometry, fitter, first pattern

- [ ] Region preparation with Clipper2, `PolyRegion`, `EdgeGrid` (ALGORITHMS §1).
- [ ] `CourseFitter` + property tests (§3).
- [ ] `LatticeGenerator` with stack and running bond (§5), super-cells, `UnitDef`/`PlacementNode`.
- [ ] `Resolver`: classify, `StoneCutter`, sliver rule (`merge`, `drop`, `keep`), flags (§11).
- [ ] Preset loader/validator for `lattice` + `sv.halvforband.marksten-210x140`.
- [ ] Scenario tests + invariants I1–I5, I7 on all sample boundaries; snapshot hashes.

**Accept:** running bond renders correctly on all samples including holes and arcs; invariants pass.

## M2 — AutoCAD MVP (command line)

- [ ] `BoundaryAdapter` (polylines with bulges, circles, `TraceBoundary` pick point, holes), `UnitsAdapter`.
- [ ] `Planner` with a trivial cost model (everything raw), `EmitPlan`, `DwgEmitter`, layers, container block, local frame.
- [ ] `FillStore` (DWG_OUTPUT §3), `-SFILL`, `-SFEDIT`, `SFREGEN`, `SFINFO`, `SFTESTDUMP`.
- [ ] Integration scripts: create, regen idempotent, explode check, AUDIT, `sweref-far`.

**Accept:** owner can make a running-bond fill from the command line, edit it, regenerate it, explode it.

## M3 — Lightweight output

- [ ] Real cost model and blockify/inline decisions (§12); unit naming; nested units.
- [ ] Copy-on-write regeneration, copy detection, purge of old units (DWG_OUTPUT §5).
- [ ] CLI `bench` + `naive-baseline`; integration size measurement; cost-model calibration script.
- [ ] Record measured numbers in PROGRESS.md; adjust budgets with the owner if needed.

**Accept:** size and entity budgets (TESTING §5) met for running bond on `big-1000m2`.

## M4 — Ring generator and joint-line mode

- [ ] Course model (`CoursePath`, runs, stone construction `rect`/`wedge`, gap check) (§2).
- [ ] `RingGenerator` with medallion, fit-to-outer-circle, staggering, sector units (§4, §6).
- [ ] Joint-line output mode for course patterns and lattices (§13).
- [ ] Sliver `balance` policy.
- [ ] Preset `sv.ring.smagatsten-90`.

**Accept:** `rondell-r6` looks like the reference image (rings, staggered joints, medallion); both output modes pass invariants.

## M5 — Bågsatt

- [ ] `SegmentalArcGenerator` (§7): scallops, seams, odd-course shift, units (scallop × K courses) and sub-units.
- [ ] Axis auto/pick, origin pick (command-line prompts).
- [ ] Preset `sv.bagsatt.smagatsten-90`; scenarios on `rect-20x10`, `street-curve`, `plaza-holes`.

**Accept:** bågsatt renders correctly incl. seams and edge cuts; 1 000 m² meets time and size budgets.

## M6 — Composition, bands, obstacles

- [ ] `Composer` with priorities and separating joints (§10).
- [ ] `BandGenerator` along edge and along path, corners, holes (§8).
- [ ] Hole clearance; composite presets with `same:` references; `sv.band.storgatsten-2rader`, `sv.rondell.smagatsten`.

**Accept:** Rondell composite works end to end from the command line on `rondell-r6` and `plaza-holes`.

## M7 — SUPERHATCH parity: tile, scatter, images, xrefs, wipeouts

- [ ] `SourceAdapter` (blocks → `SourceGeometry`), selection → `SF-SRC-*` block, library import.
- [ ] `TileGenerator` (grid/brick/hex, jitter, weighted sources), edge rules incl. `cut` for block content.
- [ ] `ScatterGenerator` with periodic Poisson tile.
- [ ] Images (single/tiled, clip, keyhole), xrefs, wipeouts, background hatch (DWG_OUTPUT §8).

**Accept:** everything SUPERHATCH does works, with cut edges for blocks and documented exceptions for images/xrefs.

## M8 — Palette and live preview

- [ ] `SuperFill.Acad.UI`: PaletteSet + WPF, preset browser (three locations), parameter editor generated from the schema, boundary/center/axis picking buttons.
- [ ] `PreviewService` with debounced background generation and transient graphics; `SFAPPLY`.
- [ ] `SFILL` / `SFEDIT` palette versions; "Save as preset".

**Accept:** owner completes the manual checklist items for the palette in AutoCAD and Civil 3D.

## M9 — Associativity, quantities, deployment

- [ ] `AssociationService` (DWG_OUTPUT §6), rigid-move detection.
- [ ] `SFQTY` (command line, CSV, optional table), `SFEXPLODE`, `SFPRESETS`.
- [ ] Autoloader bundle (`PackageContents.xml`) for AutoCAD and Civil 3D, installable from the shared network folder; shared preset path setting.

**Accept:** full manual checklist passes; a colleague can install from the share and use the shared presets.

## M10 — More patterns and polish

- [ ] Herringbone 90°/45°, basket weave.
- [ ] Random-length courses, flagstone, fan (solfjäder).
- [ ] Interlocked bågsatt seams; surround bands around holes.
- [ ] Swedish UI strings.

---

## PROGRESS.md template (Claude Code creates `docs/PROGRESS.md` from this)

```markdown
# Progress

## Current milestone
M? — name — status

## Done (latest first)
- YYYY-MM-DD: …

## In progress / next step
- …

## Open questions for the owner
- …

## Known issues / flagged for later
- …

## Measured numbers (benchmarks, DWG sizes, calibration)
| Date | Scenario | Time | Entities | Size | Notes |
```
