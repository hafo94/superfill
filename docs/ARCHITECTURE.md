# SuperFill — Architecture

## 1. Overview

```
 User ──► SuperFill.Acad.UI  (palette, live preview, preset browser)
                │  FillSpec (Core type)
                ▼
 Scripts ─► SuperFill.Acad   (commands, boundary/source adapters, emitter, persistence, associativity)
 (accoreconsole)│  Region + FillSpec + SourceLibrary (Core types)
                ▼
          SuperFill.Core     (pure .NET, no AutoCAD)
            Presets → Composer → Generators → Resolver (classify/cut/sliver) → Planner (dedup/cost) → EmitPlan + Quantities
                ▲
 SuperFill.Cli ─┘            (render PNG/SVG, benchmarks, preset validation)
```

The key idea: **Core decides everything** (geometry, which blocks to create, what goes where). The AutoCAD layer converts inputs into Core types and writes the resulting `EmitPlan` into the database. That keeps almost all logic testable without AutoCAD and lets the CLI render results to images for review.

## 2. Solution layout

```
SuperFill.sln
CLAUDE.md
docs/                      specs (this folder) + PROGRESS.md
presets/                   built-in presets (*.json) + blocks/*.dwg library
samples/boundaries/        sample boundary JSON files (TESTING §4)
samples/dwg/               blank.dwg and test drawings for integration tests
src/
  SuperFill.Core/          net8.0, no AutoCAD references
    Geometry/              Vec2, Transform2, Segment2, Arc2, Loop, Face, Region, Discretizer, Tolerances
    Geometry/Index/        EdgeGrid (broad phase), PolyRegion (Clipper2 wrapper)
    Rng/                   DeterministicRng (xoshiro256**), SeedDeriver
    Courses/               CoursePath, SmoothRun, CourseFitter, StoneBuilder, Stagger
    Generators/            IPatternGenerator, LatticeGenerator, RingGenerator, SegmentalArcGenerator,
                           BandGenerator, TileGenerator, ScatterGenerator, (later) Irregular*, Fan*
    Composition/           RegionLayerSpec, Composer
    Resolve/               Resolver, Classifier, StoneCutter, CurveClipper, SliverRule, Flags
    Units/                 UnitKey, UnitDef, PlacementNode, UnitLibrary
    Planning/              Planner, CostModel, EmitPlan, DrawPrimitive types
    Sources/               SourceGeometry, SourceLibrary (block content as Core primitives)
    Quantities/            QuantityReport
    Presets/               PresetModel, PresetLoader, PresetValidator, PresetResolver
    Serialization/         JSON for FillSpec, Region, EmitPlan (stable, versioned)
    Debug/                 DebugScene (lines, polygons, colors, labels) — rendered by Cli
    FillEngine.cs          the pipeline entry point
  SuperFill.Acad/          net8.0-windows, refs AcDbMgd + AcCoreMgd only (runs in accoreconsole)
    Commands/              -SFILL, -SFEDIT, SFREGEN, SFQTY, SFEXPLODE, SFINFO, SFPRESETS, test commands
    Adapters/              BoundaryAdapter, SourceAdapter, UnitsAdapter
    Emit/                  DwgEmitter, LayerService, BlockNaming
    Persist/               FillStore (extension dictionary), FillRecord
    Regen/                 FillRegenerator (copy-on-write)
    Assoc/                 AssociationService (events → queue → regen at CommandEnded)
    Diagnostics/           Log (file + editor), TestDump (JSON report for integration tests)
    Plugin.cs              IExtensionApplication
  SuperFill.Acad.UI/       net8.0-windows, refs AcMgd (palette needs full AutoCAD)
    Palette/               PaletteHost (PaletteSet + WPF), FillViewModel, ParameterEditor
    Preview/               PreviewService (TransientManager)
    Commands/              SFILL, SFEDIT (palette versions), internal SFAPPLY
  SuperFill.Cli/           net8.0 console: render, bench, validate-presets, naive-baseline
tests/
  SuperFill.Core.Tests/    xUnit + FsCheck: unit, property, scenario, snapshot tests
  integration/             accoreconsole scripts (*.scr) + expected reports
tools/
  run-integration.ps1
bundle/SuperFill.bundle/   PackageContents.xml + Contents/ (deployment, M9)
```

**TODO(owner):** if AutoCAD 2024 or older must be supported, Core becomes `netstandard2.0` and Acad projects multi-target `net48;net8.0-windows` (record an ADR). Default is 2025+ only.

## 3. Core types (sketch — refine during implementation)

```csharp
// Geometry (all in drawing units, local fill coordinates)
public readonly record struct Vec2(double X, double Y);
public readonly record struct Transform2(double A, double B, double C, double D, double Tx, double Ty); // rigid + uniform scale (+ mirror)
public abstract record Curve2;                       // Segment2(Vec2 A, Vec2 B) | Arc2(Vec2 Center, double R, double Start, double Sweep)
public sealed record Loop(IReadOnlyList<Curve2> Curves);          // closed
public sealed record Face(Loop Outer, IReadOnlyList<Loop> Holes);
public sealed record Region(IReadOnlyList<Face> Faces);           // exact geometry
public sealed class PolyRegion { /* discretized Paths64 + EdgeGrid; Contains(p), Classify(hull), Intersect(poly) */ }

// Courses
public sealed class CoursePath { IReadOnlyList<SmoothRun> Runs; bool Closed; }
public abstract class SmoothRun { double Length; Vec2 PointAt(double s); Vec2 NormalAt(double s); } // LineRun, ArcRun, PolylineRun
public sealed record FitResult(double[] JointCenters, double[] StoneLengths, int CutStoneIndex, FitStatus Status);

// Units and placements (ALGORITHMS §12)
public readonly record struct UnitKey(ulong Hash);
public sealed class UnitDef {
    public UnitKey Key; public IReadOnlyList<PlacementNode> Children; public IReadOnlyList<StoneGeom> Stones;
    public IReadOnlyList<DrawPrimitive> JointLines; public ConvexPolygon Hull; public int TotalStones;
}
public readonly record struct PlacementNode(UnitKey Unit, Transform2 Xf);
public sealed record StoneGeom(Polygon Outline, StoneInfo Info);    // Info: course index, flags, style key
public sealed class PatternOutput { UnitLibrary Library; IReadOnlyList<PlacementNode> Roots; IReadOnlyList<SymbolPlacement> Symbols; }

// Generators
public interface IPatternGenerator {
    string Kind { get; }
    PatternOutput Generate(PatternContext ctx, CancellationToken ct); // covers the layer's bounding area + margin, unclipped
}

// Resolve → Plan
public sealed class LayerResult { /* used units + root inserts (fully inside), raw stones (full + cut), symbols, flags, quantities */ }
public sealed class EmitPlan { IReadOnlyList<BlockPlan> Blocks; BlockPlan Container; IReadOnlyList<LayerPlan> Layers; }
public abstract record DrawPrimitive;  // PolylineP(vertices, bulges, closed, style) | LineP | ArcP | CircleP | HatchP(loops, pattern, style)
                                       // BlockRefP(blockKey, Xf) | SourceRefP(sourceBlock, Xf) | SourceEntityP(sourceBlock, entityIndex, Xf)
                                       // ImageP(imageKey, Xf, clipPolygon) | WipeoutP(polygon)

// Entry point
public sealed class FillEngine {
    public FillResult Run(FillSpec spec, Region boundaryLocal, SourceLibrary sources, CancellationToken ct);
    // FillResult = EmitPlan + QuantityReport + Flags + DebugScene (optional) + geometry hash
}
```

## 4. Pipeline

1. **Acad: gather input.** Boundary objects or a pick point (`Editor.TraceBoundary`) → `Region` in world coordinates. Source blocks → `SourceGeometry` (lines, arcs, circles, polylines with bulges, hatch loops, anchors of text/unsupported entities). Preset + user overrides → `FillSpec` (resolved, mm converted to drawing units).
2. **Acad: local frame.** Origin `O` = lower-left of the boundary extents rounded down to a whole unit. Boundary → local coordinates (DWG_OUTPUT §2).
3. **Core: compose.** `Composer` turns region layer specs into one `PolyRegion` per layer (ALGORITHMS §10).
4. **Core: generate.** Per layer, the generator produces a `PatternOutput` (unit library + root placements) covering the layer's bounds, without clipping.
5. **Core: resolve.** `Resolver` walks the placement tree against the layer region: fully inside → keep as insert; crossing → descend into children; leaf stones crossing → cut; outside → drop. Sliver rule, edge rules and flags apply here (ALGORITHMS §11).
6. **Core: plan.** `Planner` applies the cost model: each used unit becomes a block or is inlined into its parent; produces `EmitPlan` with deterministic block keys, plus quantities.
7. **Acad: emit.** `DwgEmitter` creates layers, block definitions and the container in one transaction; `FillStore` writes the extension dictionary; `AssociationService` registers boundary objects.

## 5. AutoCAD layer responsibilities

- **Commands:** thin. Parse input, call adapters, `FillEngine`, `DwgEmitter`, `FillStore`. Each command is one undo group.
- **BoundaryAdapter:** Polyline (with bulges) / Circle / Arc chains / Ellipse / Spline (discretized with `Tolerances.ChordError`) / Region / existing Hatch loops → `Region`. Validates closure and orientation.
- **SourceAdapter:** walks a BlockTableRecord recursively (nested blocks flattened with transforms) into `SourceGeometry`; keeps an index per entity so `SourceEntityP` can clone the original entity for anything Core can't cut.
- **UnitsAdapter:** `INSUNITS` → millimeters-to-drawing-units factor. Unitless drawings: assume meters and warn.
- **DwgEmitter:** writes an `EmitPlan`. Never decides geometry. Sets `Explodable`, `BlockScaling.Uniform`, layers, ByLayer properties.
- **FillStore / FillRegenerator / AssociationService:** see DWG_OUTPUT §3–§6.

## 6. Threading, transactions, UI

- Core is thread-safe and side-effect free; `FillEngine.Run` takes a `CancellationToken`.
- **Palette preview:** parameter change → debounce 250 ms → `Task.Run(FillEngine.Run)` with cancellation of the previous run → marshal result to the UI thread → `PreviewService` draws transient graphics (region outline + unit hulls always; full stones only inside the current view and up to a cap, e.g., 20 000).
- **Palette Apply:** stores the pending spec in an in-memory slot and calls `SendStringToExecute("_.SFAPPLY ")`, so the database work runs in command context with a proper undo group.
- All database work: main thread, `using var tr = db.TransactionManager.StartTransaction()`, `DocumentLock` when not in a command context.
- Errors: Core throws `FillException` with a user-facing message key; commands catch, print to the editor, log details to `%LOCALAPPDATA%\SuperFill\log.txt`, and abort the transaction.

## 7. Approved dependencies

| Package | Used in | Why |
|---|---|---|
| Clipper2 | Core | Robust polygon booleans and offsetting (integer coordinates) |
| AutoCAD .NET API (Autodesk NuGet `AutoCAD.NET.Core` / `AutoCAD.NET.Model` / `AutoCAD.NET`, or DLLs from the ObjectARX SDK) | Acad, Acad.UI | Must match the target AutoCAD version; reference only, never copy to output (`Private=false` / `ExcludeAssets=runtime`) |
| xUnit, FsCheck.Xunit | Core.Tests | Unit and property tests |
| SkiaSharp | Cli | PNG rendering of `DebugScene` |
| BenchmarkDotNet (optional) | Cli | Micro-benchmarks |
| CommunityToolkit.Mvvm (optional) | Acad.UI | MVVM boilerplate for the palette |

`System.Text.Json` (built in) for all JSON. Anything else: ask the owner first.

## 8. Adding a generator (checklist)

1. Preset schema: add the generator's parameter block (PRESETS §4) and validation.
2. Implement `IPatternGenerator` producing units + roots with stable `UnitKey`s; provide joint-line primitives per unit.
3. Add scenario tests over all sample boundaries + invariants (TESTING §3).
4. Add at least one built-in preset and a CLI render to the snapshot set.
5. Document the algorithm in ALGORITHMS.md.
