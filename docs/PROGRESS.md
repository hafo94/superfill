# Progress

## Current milestone
M0 — Foundation — code complete; waiting for owner to verify SFHELLO in AutoCAD / accoreconsole.

## Done (latest first)
- 2026-10-29: M0. Solution with `SuperFill.Core`, `.Cli`, `.Acad`, `.Acad.UI`, `Core.Tests` (25 tests green).
  Core: `Vec2`, `Transform2`, `Curve2`/`Segment2`/`Arc2`, `Loop`, `Face`, `Region`, `Tolerances`, `Discretizer`,
  `DeterministicRng`, `SeedDeriver`, `DebugScene` + `SvgWriter`, `BoundaryJson`.
  CLI `render --boundary <file|dir> --out <path>` → PNG (SkiaSharp) + SVG + stats.json; all 8 sample boundaries rendered and inspected.
  Acad: `Plugin` + `SFHELLO` (builds against AutoCAD.NET 25.0.0). `tools/run-integration.ps1` + `tests/integration/hello.scr` skeleton.
- Bug found and fixed while rendering `street-curve`: `Arc2.FromBulge` put the centre on the wrong side for negative bulges (now covered by tests).

## In progress / next step
- Owner: confirm M0 (see "Owner test list" below), then start M1 (Clipper2 `PolyRegion`, `EdgeGrid`, `CourseFitter`, `LatticeGenerator`, Resolver).

## Owner test list for M0
1. `NETLOAD` `src/SuperFill.Acad/bin/Debug/net8.0-windows/SuperFill.Acad.dll`, run `SFHELLO` → "SFHELLO: SuperFill is alive…".
2. Supply `samples/dwg/blank.dwg`, then run `pwsh tools/run-integration.ps1 -AccoreConsole "<path>\accoreconsole.exe"` → `hello.scr PASS`.
3. Skim `out/*.png` (generate with the render command in CLAUDE.md) and confirm the sample boundaries match what you expect.

## Open questions for the owner
- AutoCAD / Civil 3D versions (ADR-009 still Proposed). Autodesk's NuGet `AutoCAD.NET` 25.0.0/25.1.0 support net8.0; 25.1.1 and 26.x only list net10.0. Referenced 25.0.0 (AutoCAD 2025). Tell me if you need 2026/2027 or ≤2024.
- `accoreconsole.exe` path and a `samples/dwg/blank.dwg`.
- `sliver-trap.json`: TESTING §4 says "1.003 × stone pitch wide". Interpreted as 10.003 m × 5 m (100 pitches of 0.1 m plus 3 mm). Confirm or give the intended size.
- `street-curve.json`: 20 m straight + 20 m centreline arc (R 40 m, 0.5 rad) + 20 m straight, 8 m wide (area 480 m²). Spec says "60 m long with a 40 m radius bend"; confirm.
- `l-shape-arc.json`, `plaza-holes.json` dimensions were invented (spec gives shape only).

## Known issues / flagged for later
- `Region.Normalize` assigns all holes to the first outer loop; proper containment assignment comes with Clipper2 region prep (M1).
- Build runs on Linux with `EnableWindowsTargeting`; the AutoCAD projects have not been loaded in AutoCAD.
- SkiaSharp pinned to 2.88.9 (4.x failed to load its native library on Linux).

## Measured numbers (benchmarks, DWG sizes, calibration)
| Date | Scenario | Time | Entities | Size | Notes |
|---|---|---|---|---|---|
