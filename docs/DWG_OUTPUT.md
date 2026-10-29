# SuperFill — DWG Output, Persistence and Editing

## 1. Structure of a fill

```
Model space (or the current layout)
└─ BlockReference → "SF-7K3QX2MA"                (container; layer = preset container layer or current)
   ├─ ExtensionDictionary
   │    └─ Dictionary "SF_FILL"                  (§3)
   └─ BlockTableRecord "SF-7K3QX2MA"             (Explodable = true, BlockScaling = Uniform)
       ├─ BlockReference → "SF-7K3QX2MA-U01" × n    unit instances (fully inside)
       │    └─ BTR "SF-7K3QX2MA-U01": BlockReference → "SF-7K3QX2MA-U02" × m  +  Polylines
       ├─ Polyline (closed)      cut stones          layer e.g. SF-STEN-KAP
       ├─ Polyline (closed)      inlined full stones layer e.g. SF-STEN
       ├─ Line / Arc / Circle    joint-line mode     layer e.g. SF-FOG
       ├─ BlockReference → user source block         tile / scatter / medallion
       ├─ cloned source entities (cut symbols)       keep their own layers
       ├─ Hatch (optional background), Polyline (optional drawn boundary)
       ├─ Wipeout (optional), RasterImage (image sources)
       └─ Polyline on check layer (flagged slivers / alignment), only if enabled
```

Rules:

- **Fill id:** 8 characters base32 from a random value created in the Acad layer (Core stays deterministic); checked unique in the BlockTable. 8 characters keeps collisions negligible when drawings are inserted into each other (AutoCAD keeps the existing definition on a name clash, which would show wrong geometry).
- **Names:** container `SF-{id}`, units `SF-{id}-U{nn}` in deterministic order, source blocks created from selections `SF-SRC-{name}`.
- **Every** BTR we create: `Explodable = true`, `BlockScaling = BlockScaling.Uniform`, origin `(0,0,0)`.
- **Entities:** on preset layers (created if missing with the preset's color), color/linetype/lineweight ByLayer. Never layer 0 for generated geometry (it would land on 0 after explode). Units placed with rotation/translation only.
- Allowed entity types: CLAUDE.md rule 1. Nothing else.

## 2. Local frame

Swedish coordinates are large (SWEREF 99 northing ≈ 6 500 000 m). Block content far from the origin loses precision in graphics and bloats coordinates.

- Local origin `O` = lower-left corner of the boundary extents, rounded down to whole drawing units.
- All block definitions (container and units) are in local coordinates; the container is inserted at `O` with rotation 0 and scale 1 at creation.
- The stored boundary snapshot is in container-local coordinates, so moving, rotating, mirroring or copying the container never invalidates it.

## 3. Persistence schema (extension dictionary on the container BlockReference)

```
ExtensionDictionary
└─ "SF_FILL" (DBDictionary)
   ├─ "META"     Xrecord: (70) schemaVersion = 1
   │                      (1)  fillId
   │                      (1)  pluginVersion
   │                      (1)  ownerInsertHandle   (hex handle of the container insert at creation)
   │                      (1)  geometryHash        (from FillResult, for change detection)
   ├─ "SPEC"     Xrecord: (90) totalLength, then (1) chunks of the resolved FillSpec JSON
   ├─ "BOUNDARY" Xrecord: (90) totalLength, then (1) chunks of the boundary JSON (local coords) + hash
   └─ "ASSOC"    Xrecord: repeated pairs (1) role ["outer" | "hole" | "path" | "center"], (330) soft pointer to the entity
```

- JSON is split into chunks of at most 250 characters per `(1)` value; reassembled in order and verified against `(90)`.
- `SPEC` is the fully resolved spec (preset values + overrides + seed + units factor) plus the preset id and version for traceability. Regeneration never needs the preset file.
- Unit BTRs carry an Xrecord `SF_UNIT` in their own extension dictionary with `(1) fillId`, so purge logic never touches anything else.
- Reading: unknown `schemaVersion` → refuse to edit with a clear message ("created by a newer SuperFill").

## 4. Creation (`SFILL` / `-SFILL`)

One transaction, one undo group:

1. Resolve spec and boundary (adapters), compute `O`, run `FillEngine`.
2. Create missing layers.
3. Create unit BTRs bottom-up from the `EmitPlan`, then the container BTR.
4. Insert the container at `O`; write `SF_FILL`; register associations.
5. Print a one-line summary: stones, cut stones, slivers flagged, blocks, entities.

## 5. Regeneration (`SFEDIT` Apply, `SFREGEN`, association trigger)

```
read META, SPEC, BOUNDARY
if insert.Handle != META.ownerInsertHandle:          // this is a copy (COPY, copy/paste, INSERT of a drawing)
    assign a new fillId, set ownerInsertHandle, clear ASSOC   // copies are never associative
if the container BTR is referenced by more than one insert:
    copy-on-write: this insert gets new BTR names (new fillId)
if associative and triggered by a boundary change:
    boundary = associated entities → Region (world) → local via inverse(insert transform)
    if hash(boundary) == stored hash: stop          // rigid move together with the fill: nothing to do
run FillEngine; build new unit BTRs (new names); replace the container BTR's entities
erase this fill's old unit BTRs that are now unreferenced (check nested references too)
update SPEC / BOUNDARY / META
```

- The container insert keeps its handle and its transform, so the fill stays where the user put it.
- Regeneration only touches BTRs marked with this fill's `SF_UNIT` id and the container's own entities.

## 6. Associativity (`AssociationService`)

- On document open (and after `SFILL`), scan for containers with `SF_FILL` and build a map: boundary ObjectId → fill insert ObjectId.
- `Database.ObjectModified` / `ObjectErased`: if the id is in the map, enqueue the fill. Never modify the database inside these events.
- On `Document.CommandEnded` (and `LispEnded`), process the queue: lock the document, regenerate each queued fill in its own transaction, ignore events raised by our own regeneration (re-entrancy flag).
- Erased boundary: keep the fill, remove the association, print a note.
- The regeneration is a separate undo step after the user's command. Acceptable for v1; document it in the UI help.

## 7. Explode behavior

- **Explode once** (container): unit BlockReferences, cut stones, inlined stones, symbols etc. land in the owner space on their layers. The `SF_FILL` data lived on the container insert and is gone, so the result is no longer a fill.
- **Explode again** (units): plain Polylines/Lines/Arcs/Circles. Nested units may need another level depending on the plan depth (typically 2–3 levels total).
- Unreferenced SF definitions remain until `PURGE`.
- `SFEXPLODE` does the full recursive explode in one step, purges this fill's BTRs, and reports counts.
- Exceptions: RasterImage and xref instances stay as they are (not explodable in AutoCAD).

## 8. Images, xrefs and wipeouts

- **Image, single:** one RasterImage scaled to the layer's extents, clipped with a polygon clip boundary (`SetClipBoundary(ClipBoundaryType.Poly, …)`). Holes: use a keyhole polygon (outer loop connected to each hole by a zero-width slit).
- **Image, tiled:** RasterImages sharing one image definition on a lattice; edge tiles get their own clip polygon.
- **Xref:** BlockReferences to the xref block on a lattice; edge rule Keep/Drop only.
- **Wipeout:** created from the layer region polygon (keyhole for holes), placed first in the container so it draws underneath.

## 9. Interop notes

- `WBLOCK` / `INSERT` / copy-paste between drawings carry the container BTR, unit BTRs and the extension dictionary. Handles change, so the fill is detected as a copy on its next edit and gets new names then.
- Civil 3D: no Civil-specific objects are created; everything above applies unchanged.
- `AUDIT` must report no errors on drawings containing fills (integration test).
