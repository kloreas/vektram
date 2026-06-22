# ADR-0005: Revision Philosophy — Faithful DDTank Skeleton, Modern 2026 Core

**Date:** 2026-06-22
**Status:** Accepted
**Deciders:** Architecture session, Revision kickoff

---

## Context

Vektram is a ground-up re-implementation of a DDTank-style turn-based artillery game
(the BomBom / DDTank lineage). We hold the decompiled DDTank server logic as a reference
source under `/ddtank/` (git-ignored; never built into Vektram), fully mapped in
[docs/ddtank-source-map.md](../ddtank-source-map.md) — every system, its real formulas,
what Vektram currently has, and how each revision will use it.

That source is the authoritative record of how the original game *behaves*. It is also
fifteen-year-old decompiled bytecode: a 25 Hz Euler integrator, client-trusted logic,
hard-coded tuning constants, DB-coupled game logic, and IL-level cruft. We want the
former and none of the latter.

This ADR records the standing principle that governs **every** revision sprint, so each
system is approached through the same lens rather than re-litigated each time.

---

## Decision

**Stay faithful to DDTank's skeleton and familiar feel; modernize the engine,
architecture, and quality to a 2026 standard, with measured inspiration from modern games.**

Keep DDTank's *design* and *behavior*. Do **not** replicate its legacy technical flaws.
No over-the-top reinvention — tasteful, modern, coherent.

This breaks down into three commitments held in tension:

1. **Faithful skeleton.** Preserve DDTank's design intent, the *spirit* of its formulas,
   and its recognizable moment-to-moment feel. A returning DDTank/BomBom player should
   feel at home. The source map is the canonical reference for what that behavior is.

2. **Modern core.** Re-implement on Vektram's clean foundations — deterministic,
   engine-independent `/sim` (Velocity Verlet at 1/60 s, per [ADR-0002](ADR-0002-numeric-representation-and-sim-conventions.md)),
   server-authoritative resolution ([ADR-0001](ADR-0001-server-authoritative-shared-sim.md)),
   data-driven content in `/content`, test-first development.

3. **Measured innovation.** Layer modern architecture and polish where it adds genuine
   depth or feel, without altering the nostalgic identity.

---

## What this means per system

For every system we revise:

- **Preserve the design.** Keep DDTank's design intent, the spirit of its formulas, and
  its recognizable feel. Faithful, not slavish — port the *behavior*, not the bytecode.
- **Re-implement on clean foundations.** Deterministic, engine-independent `/sim`
  (Velocity Verlet, 1/60 s — ADR-0002); server-authoritative resolution; data-driven
  content in `/content`; test-first.
- **Model behavior as data, not legacy code.** Express DDTank's formulas and constants as
  parameters/data tuned to feel right on our modern core. Do **not** copy its legacy
  integrator (25 Hz Euler) or its client-trusted logic. Where the source uses a tuning
  fudge (e.g. the bot solver's 0.7 / 1.3 drag asymmetry), treat it as a calibration value
  to validate, not gospel to transcribe.
- **Layer modern architecture where it adds depth.** Example: a
  base + equipment + rune + costume + buff modifier-stack stat system that scales toward
  Lost Ark-level gear depth later, rather than DDTank's flat per-slot stat addition.
- **Add a 2026 layer of polish/feel.** Aiming-preview arc, smooth 60 fps interpolation,
  wind/weather effects — additive polish that never changes the nostalgic identity and
  never crosses the client-authority boundary.

---

## Non-Goals

Explicitly out of scope for the revision:

- **No legacy-flaw replication.** We do not port the 25 Hz Euler integrator, DB-coupled
  game logic, `System.Drawing` wind-CAPTCHA rendering, or any decompilation artifact.
  The source map describes behavior to *match*, not code to *copy*.
- **No client-trusted logic.** Nothing the original trusted to the client (damage, stats,
  RNG, outcomes) moves to the Vektram client. The server-authority boundary
  (ADR-0001) is absolute regardless of how DDTank structured it.
- **No gratuitous reinvention.** We do not redesign systems that already work and already
  feel right just to be different. Innovation must earn its place by adding depth or feel;
  novelty for its own sake breaks the nostalgic contract with the player.
- **No hard-coded tuning.** Formula constants from DDTank land in `/content` as data, never
  baked into C# (per the Data-Driven Content ground rule).

---

## Consequences

### Positive

- **One lens for the whole revision.** Every sprint starts from the same principle; we do
  not re-argue "how faithful should we be?" per system.
- **Behavior is preserved, debt is not.** Players get the game they remember; we get a
  codebase built to 2026 standards.
- **The source map becomes a behavior spec.** `/ddtank/` and the source map are read as
  *what the game does*, then re-expressed as data + clean `/sim` code + tests.

### Negative / Trade-offs

- **Judgment calls per system.** "Spirit of the formula" and "measured innovation" are not
  mechanical rules; each revision must justify where it stays faithful and where it
  modernizes. This ADR sets the default disposition, not a decision procedure.
- **Constant temptation to copy.** Having the decompiled source on hand makes transcription
  the path of least resistance. The non-goals exist to resist that pull deliberately.

---

## References

- [docs/ddtank-source-map.md](../ddtank-source-map.md) — system-by-system map of the
  reference source, with formulas, current Vektram state, and per-system revision plan.
- [ADR-0001](ADR-0001-server-authoritative-shared-sim.md) — Server-Authoritative Model & Shared Sim Core
- [ADR-0002](ADR-0002-numeric-representation-and-sim-conventions.md) — Numeric Representation & Simulation Conventions
- [ADR-0003](ADR-0003-client-trajectory-preview-not-authoritative-playback.md) — Client Sim = Aiming Preview Only
- [ADR-0004](ADR-0004-team-based-match-model.md) — Team-Based Match Model
