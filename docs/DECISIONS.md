# SuperFill — Architecture Decision Records

Status values: Proposed | Accepted | Superseded. Change a decision by adding a new ADR that supersedes the old one.

---

## ADR-001: Managed .NET plugin generating standard entities

**Status:** Accepted · **Date:** 2026-10-29

**Context.** A fill could be a custom entity (ObjectARX C++, true associativity, own display) or ordinary AutoCAD objects created by a .NET plugin. Drawings are shared with colleagues and consultants who won't have the plugin.

**Decision.** C# .NET plugin that only creates standard entities. Settings live in an extension dictionary.

**Options.** *ObjectARX custom entity:* real associativity and grips, but proxy objects for anyone without the plugin, C++ build per AutoCAD release, much slower to develop. *.NET + standard entities:* works everywhere, degrades gracefully, fast to build; associativity must be emulated with events.

**Consequences.** Anyone can open, plot and edit the output. Associativity is event-based (ADR-007) and only active where the plugin is loaded.

---

## ADR-002: Output must be explodable — no MInsert, no clipping, no arrays

**Status:** Accepted · **Date:** 2026-10-29

**Context.** The owner requires explodable and editable output. `MINSERT` blocks can't be exploded; exploding an XCLIP'ed block brings back the hidden geometry; associative arrays and dynamic blocks behave poorly when exploded and edited.

**Decision.** Only plain block references to explodable, uniformly scaled blocks, plus raw geometry. Edge stones are really cut.

**Consequences.** Lightness must come from shared unit definitions (ADR-005), not from MInsert. Real cutting needs a robust polygon library (ADR-004). Images and xrefs are documented exceptions.

---

## ADR-003: Pure Core library, rendered to images for review

**Status:** Accepted · **Date:** 2026-10-29

**Context.** Development is done largely by Claude Code, which can't operate the AutoCAD GUI. Geometry bugs are easiest to find visually.

**Decision.** All logic in `SuperFill.Core` without AutoCAD references, including the cost model and emit planning. A CLI renders `DebugScene` to PNG/SVG. The AutoCAD layer only adapts input and writes the `EmitPlan`. Headless integration via `accoreconsole`.

**Consequences.** Most functionality is testable on any machine. Requires converting block content into Core primitives (`SourceGeometry`), with `SourceEntityP` for entities Core can't handle.

---

## ADR-004: Clipper2 for polygon booleans and offsets

**Status:** Accepted · **Date:** 2026-10-29

**Context.** Cutting stones, composing regions, offsets for bands and hole clearances need fast, robust booleans. AutoCAD's own Region/Boolean operations are slow, need the AutoCAD runtime, and are fragile at scale.

**Decision.** Clipper2 (C#) with integer coordinates at `ClipperResolution` (0.01 mm). Arcs are discretized for booleans with `ChordError`; generator output keeps true arcs (bulges) where it isn't cut.

**Consequences.** Cut edges along curved boundaries are polylines with small segments (bounded by `ChordError`). Acceptable for plans.

---

## ADR-005: Placement tree with keyed units and a cost model

**Status:** Accepted · **Date:** 2026-10-29

**Context.** Paving fills contain tens of thousands of stones. Most patterns repeat (lattices, bågsatt scallops, ring stones, periodic scatter tiles).

**Decision.** Generators emit a hierarchy of units (`UnitDef`) with stable keys and placements. The resolver keeps fully-inside units as inserts and only descends where the boundary crosses. A cost model decides per unit whether a block definition pays off.

**Consequences.** Entity count scales with area/unit size + perimeter instead of stone count. Generators must produce correct keys (Debug builds verify with geometry hashes).

---

## ADR-006: Settings stored as versioned JSON in the container's extension dictionary

**Status:** Accepted · **Date:** 2026-10-29

**Decision.** Fully resolved spec and a boundary snapshot (container-local coordinates) as chunked JSON Xrecords, with a schema version (DWG_OUTPUT §3).

**Consequences.** Regeneration never needs the preset file; move/rotate/copy of the container keeps fills valid; older plugins refuse to edit fills from newer schemas.

---

## ADR-007: Associativity via events and deferred regeneration

**Status:** Accepted · **Date:** 2026-10-29

**Decision.** Track boundary ObjectIds; on modification enqueue the fill; regenerate at `CommandEnded`. Skip when the boundary is unchanged in container-local coordinates (rigid move together with the fill). Copies are never associative.

**Consequences.** Regeneration is a separate undo step. No persistent reactors to maintain; works only where the plugin is loaded.

---

## ADR-008: Presets as JSON in three locations, lengths in mm

**Status:** Accepted · **Date:** 2026-10-29

**Decision.** Built-in, shared (network) and user preset folders with id-based override; mm and degrees; validation on load (PRESETS).

**Consequences.** The company standard is maintained in one shared folder; users can still experiment locally.

---

## ADR-009: Target AutoCAD 2025+ (.NET 8) by default

**Status:** Proposed · **Date:** 2026-10-29 · **TODO(owner):** confirm versions.

**Context.** AutoCAD/Civil 3D 2025 and later run on .NET 8; 2024 and earlier use .NET Framework 4.8.

**Decision (proposed).** `net8.0` for Core/CLI/tests and `net8.0-windows` for the AutoCAD projects. If older versions are needed: Core → `netstandard2.0` and multi-target the AutoCAD projects (new ADR).

---

## ADR-010: Separate AutoCAD projects for core-console and UI

**Status:** Accepted · **Date:** 2026-10-29

**Decision.** `SuperFill.Acad` references only AcDbMgd/AcCoreMgd so it runs in `accoreconsole`; everything needing AcMgd (palette, transient preview) lives in `SuperFill.Acad.UI`.

**Consequences.** Every feature has a command-line path that integration tests can drive; the UI is a thin layer on top.
