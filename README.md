# SuperFill — build pack for Claude Code

SuperFill (working name, rename freely) is an AutoCAD / Civil 3D plugin that fills closed areas with realistic paving patterns — bågsatt smågatsten, circle-laid rings, bonds, edge bands along curbs — and with tiled or scattered blocks, images and wipeouts. Compared to Express Tools SUPERHATCH:

- joints adapt so every course fits, and edge stones are really cut (no hidden clipping),
- patterns can be nested (medallion → rings → bågsatt field → edge band),
- output is light (repeating units become shared block definitions),
- everything it creates can be exploded with plain `EXPLODE` and edited.

## What's in this pack

| File | Purpose |
|---|---|
| `CLAUDE.md` | Read automatically by Claude Code: rules, commands, workflow |
| `docs/SPEC.md` | What to build: features, commands, requirements, glossary |
| `docs/ARCHITECTURE.md` | Projects, modules, key types, data flow, dependencies |
| `docs/ALGORITHMS.md` | Fitting, generators, clipping, sliver rule, dedup — the math |
| `docs/DWG_OUTPUT.md` | Exact DWG structure, persistence, regen, explode/edit behavior |
| `docs/PRESETS.md` | Preset JSON schema and example presets |
| `docs/TESTING.md` | Test strategy, invariants, sample boundaries, perf budgets, manual checklist |
| `docs/ROADMAP.md` | Milestones with tasks and acceptance criteria |
| `docs/DECISIONS.md` | Architecture decision records (why things are the way they are) |

## Before you start — fill in the owner TODOs

Search the docs for `TODO(owner)`. The important ones:

1. **AutoCAD / Civil 3D version(s)** to support. The docs assume 2025 or newer (.NET 8). If you need 2024 or older (.NET Framework 4.8), say so before Milestone 0 — it changes the project setup.
2. **AutoCAD install path** (for references and `accoreconsole.exe`).
3. **Drawing units** (assumed meters) and coordinate system (assumed SWEREF 99 with large coordinates).
4. **Layer standard** — the preset layer names (`SF-STEN`, `SF-STEN-KAP`, …) are placeholders.
5. **Stone sizes and joint tolerances** — all preset values in `docs/PRESETS.md` are examples. Replace them with your standard.
6. **Shared preset/block folder** on the network so colleagues use the same presets.

## Kickoff

1. Create an empty folder (ideally a git repo) and copy this pack into it, keeping the structure.
2. Fill in the owner TODOs (or tell Claude Code the answers in the first message).
3. Install: Visual Studio 2022 or the .NET 8 SDK, and AutoCAD/Civil 3D on the same machine.
4. Open Claude Code in the folder and paste:

```
Read CLAUDE.md and every file in docs/ fully. Then:
1. Summarize the architecture and the Milestone 0 plan in a few bullets.
2. List anything ambiguous or contradictory in the docs, and any TODO(owner) you need answered now.
3. After I answer, start Milestone 0. Work milestone by milestone, keep docs/PROGRESS.md updated,
   and stop at the end of each milestone so I can test in AutoCAD.
```

## How testing in AutoCAD works

Claude Code can't click around in AutoCAD. Everything geometric is built and tested in a pure .NET library and rendered to PNG/SVG so Claude Code can look at the results itself. AutoCAD-side behavior is tested headless with `accoreconsole.exe` scripts, and the few GUI things (palette, preview) are on a manual checklist in `docs/TESTING.md` that you run and report back on.
