# CLAUDE.md — SuperFill

AutoCAD / Civil 3D .NET plugin that fills closed areas with realistic paving patterns (bågsatt smågatsten, rings, bonds, edge bands) and with tiled/scattered blocks, images and wipeouts. Output must be **light, explodable and editable**. It replaces Express Tools SUPERHATCH.

## Read first

Before writing code, read in this order: `docs/SPEC.md` → `docs/ARCHITECTURE.md` → `docs/ALGORITHMS.md` → `docs/DWG_OUTPUT.md` → `docs/PRESETS.md` → `docs/TESTING.md` → `docs/ROADMAP.md` → `docs/DECISIONS.md`. Then continue from `docs/PROGRESS.md` (create it if missing).

## Non-negotiable rules

1. **Standard entities only.** Output may contain: BlockReference, Polyline (LWPOLYLINE), Line, Arc, Circle, Hatch, Wipeout, RasterImage, and clones of entities from user source blocks. Never: custom entities, proxies, `MInsert`, XCLIP / spatial filters, associative ARRAY, groups, dynamic blocks.
2. **Explodable.** Plain `EXPLODE` must take any fill down to editable geometry. Every BlockTableRecord we create has `Explodable = true` and `BlockScaling = Uniform`. The only documented exceptions are RasterImage and xref instances (see SPEC §6.1).
3. **Real cuts, never hidden geometry.** Edge stones are trimmed geometry. Never hide geometry with clipping, masking or off-layer tricks.
4. **Pure core.** `SuperFill.Core` has zero AutoCAD references. All geometry, fitting, generators, clipping, dedup/cost planning and quantities live there and are unit-tested. The AutoCAD layer only converts inputs, writes an `EmitPlan` to the database, and persists data.
5. **Deterministic.** Same spec + seed + boundary → identical output (identical geometry hash). Use the project's own seeded RNG (`DeterministicRng`), never `System.Random` or `Guid.NewGuid()` in Core.
6. **AutoCAD threading and transactions.** AutoCAD API calls only on the main thread, inside a transaction. Lock the document (`DocumentLock`) whenever code runs from the palette or any modeless context. Core may run on background threads.
7. **No magic numbers.** Tolerances live in `Tolerances`, cost constants in `CostModel`, everything else comes from presets.
8. **Ask first** before adding a dependency not on the approved list (ARCHITECTURE §7), or before changing anything decided in `docs/DECISIONS.md`. If a decision changes, add a new ADR rather than silently editing an old one.

## Commands

```bash
dotnet build SuperFill.sln -c Debug
dotnet test tests/SuperFill.Core.Tests

# Render a preset into a sample boundary → PNG + SVG + stats.json (look at the PNG yourself)
dotnet run --project src/SuperFill.Cli -- render --preset presets/sv.bagsatt.smagatsten-90.json \
    --boundary samples/boundaries/street-curve.json --out out/street-curve

# Performance + size benchmarks against the budgets in docs/TESTING.md
dotnet run --project src/SuperFill.Cli -c Release -- bench --all

# Headless AutoCAD integration tests (needs AutoCAD installed; TODO(owner): confirm path)
pwsh tools/run-integration.ps1 -AccoreConsole "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe"
```

Loading in AutoCAD for manual tests: `NETLOAD` → `src/SuperFill.Acad/bin/Debug/net8.0-windows/SuperFill.Acad.dll` (and `SuperFill.Acad.UI.dll` for the palette).

## How to work

- Work **one milestone at a time** from `docs/ROADMAP.md`. Stop at the end of each milestone, summarize, and list what the owner should test manually.
- For Core logic: write or extend tests first, implement, run tests, then **render the sample boundaries to PNG and inspect them**. Many bugs (overlaps, wrong joints, missing cut stones) are obvious visually and invisible to unit tests.
- You cannot drive the AutoCAD GUI. For AutoCAD-side behavior, (a) add a command-line variant (`-SFILL` etc.) and an `accoreconsole` integration script, and (b) add steps to the manual checklist in `docs/TESTING.md` and ask the owner to run them.
- At the end of every session, update `docs/PROGRESS.md`: done, in progress, next step, open questions, known issues.
- If a doc is wrong, ambiguous or contradicts another doc, say so, propose a fix, and update the doc once agreed. Docs are the source of truth; keep them current.
- Snapshot (golden) updates only when the change is intended; note why in PROGRESS.md.

## Code conventions

- C# (latest language version), nullable enabled, `TreatWarningsAsErrors` in Core and Core.Tests.
- Core geometry types are small immutable `readonly record struct`s (`Vec2`, `Transform2`, …). No AutoCAD types in Core.
- No static mutable state. Dependencies via constructors.
- One public type per file; folders mirror namespaces.
- Identifiers in English; Swedish domain terms appear in comments, preset names and UI labels (see glossary in SPEC §9). Example: the bågsatt generator is `SegmentalArcGenerator`.
- Naming in the drawing: commands `SF*`, blocks `SF-{fillId}…`, layers from presets.
- Hot loops: arrays and structs, no LINQ, no per-stone allocations of AutoCAD objects before emit.

## Domain cheat-sheet

- **Course (rad):** one row of stones along a path (line, arc, ring, offset of an edge).
- **Joint (fog):** gap between stones. Each course fits a whole number of stones by adjusting joints within `[min, max]`.
- **Cut stone (kapad sten):** stone trimmed at a boundary. **Sliver (flisa):** cut piece too small to lay; avoided by the sliver rule.
- **Unit:** a repeating group of stones (e.g. one bågsatt arc × 8 courses) stored once as a block and inserted many times.
- **Fill:** one container block reference holding everything generated for one boundary.
