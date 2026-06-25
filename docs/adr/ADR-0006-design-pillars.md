# ADR-0006: Design Pillars — Data-Driven Modes, Dynamic Environment, Anti-Inflation Balance

**Date:** 2026-06-25
**Status:** Accepted
**Deciders:** Architecture session, Design-pillar lock

---

## Context

[ADR-0005](ADR-0005-revision-philosophy.md) set the standing disposition for the whole
revision: faithful DDTank skeleton, modern 2026 core, behavior-as-data. [ADR-0004](ADR-0004-team-based-match-model.md)
generalised the match engine to N-vs-N and left explicit seams — a swappable
`ITurnOrderPolicy`, decoupled `MatchOptions` (`FriendlyFire` / `SelfDamage`), and a
team-aware `MatchState`. The combat-rules surface is already data-driven: `CombatRules`
carries `Tuning`, a `ModeMultiplier` scalar (the room/mode seam for system #5), and an
optional `ElementTable`, all supplied by the host from `/content`.

Three product-direction decisions have now been locked that constrain every future system.
They are not implementation tasks — they are standing design pillars. This ADR records them
once so each future sprint inherits them rather than re-litigating them.

Relevant current state these decisions build on:

- **`WorldEnvironment`** (`Sim.Projectile`) is today a `readonly record struct (Gravity, WindX)`
  passed into the simulation **once per round** and held constant for the whole match.
- **`MatchOptions`** carries per-match boolean rules; **`CombatRules`** carries the
  data-driven damage/mode/element configuration. Together these are the existing
  data-driven combat-rules surface a mode definition will extend.
- The **room system (#5)** in the revision plan is the future layer that selects which
  mode/config a match runs under.

---

## Decision

### Decision 1 — Data-driven game modes (no hardcoded mode logic)

Vektram will ship **many distinct, original game modes** — varied win conditions, team
sizes, match durations, and special rules. Every mode **must be defined as DATA in
`/content`**, following the established schema + `<Domain>Catalog.FromJson` loader
convention, **never as a branching code path** in `/sim` or the server.

**A new mode is new data, not new code.**

- This is the direct application of ADR-0005's non-goal of replicating DDTank's per-room
  *formula-shape switching*. We already reduced that to a scalar `ModeMultiplier` on
  `CombatRules`; modes extend the same data surface rather than forking the engine.
- It builds on the existing seams: `MatchOptions` (per-match boolean rules) and
  `CombatRules` (`Tuning` / `ModeMultiplier` / `Elements`) are the data the engine already
  consumes "without knowing how it was assembled." A mode definition is the next data record
  the future **room system (#5)** selects and hands to the match.
- Modes apply to **both offline (vs bots) and online play.** They are a property of the
  match configuration, not of the network transport — the same mode data drives a local
  bot match and a server-authoritative online match.

**Design hook (flagged, not built this session):** win-condition *variety* (last-team-standing,
score threshold, timed, objective, survival, …) will require a **data-driven win-condition
model**. Today the engine hardcodes the all-enemies-defeated / draw / max-turns outcome
logic from ADR-0004. When modes are actually built, the win condition must move into the
mode data + a small evaluator seam in the match controller, mirroring how `ModeMultiplier`
replaced per-room formula switching. **Do not build this now** — this ADR only records that
the win-condition surface is the place that must become data, so it is not retrofitted under
pressure later.

### Decision 2 — Dynamic (time-varying) environment

The match environment — wind today, weather later — **may change DURING a match**, not only
be a constant fixed at match start. Environment becomes part of match **state that can
evolve per turn** (e.g. wind shifts on turn 3; a storm reverses it mid-match), rather than a
single fixed input.

This is recorded as an **accepted direction** with a concrete seam, not a built feature.

- **Rationale (ADR-0005 "modern feel"):** a time-varying environment adds a tactical layer
  and deliberately defeats memorized fixed angle/power play — a returning player still
  recognises the game, but rote solutions stop working.
- **The seam:** today `WorldEnvironment` is constant and passed into the simulation once.
  The future change is to let the **match controller advance or replace the
  `WorldEnvironment` per turn via a data-driven rule** (the rule itself authored in
  `/content`, consistent with Decision 1 — environment evolution is a mode property). Each
  turn's shot still runs against a single constant environment; only the *value handed in
  between turns* changes.
- **Scope:** a small, deliberate future `/sim` extension. **Not built this session.**

**This does NOT reopen [ADR-0002](ADR-0002-numeric-representation-and-sim-conventions.md).**
The integrator (Velocity Verlet) and fixed timestep (1/60 s) are unchanged. Within any
single turn's flight the acceleration is still constant, so Verlet stays analytically exact;
only the per-turn environment values vary. The dynamic-environment direction lives entirely
above the integrator, in match-state evolution — it touches no numeric-representation or
timestep decision.

### Decision 3 — Balance philosophy (anti-inflation, readable depth)

Standing balance pillars that constrain **every** future stat, damage, progression, and
economy system:

- **No numeric inflation.** Damage and stats stay in small, human-readable ranges (think
  50 / 100, not millions). A player should be able to roughly reason about the numbers in
  their head. The shipped tuning already lives here (base damages ~75–140, HP ~100); future
  systems hold that scale rather than escalating it.
- **Deep-but-simple.** A few clear mechanics with a high skill ceiling — not many shallow,
  overlapping ones. Depth comes from mastery of simple rules, not from rule count.
- **Variety over vertical grind.** Power and progression come primarily from **collecting
  and building different weapons / characters / loadouts**, not from leveling a single
  number ever upward. Horizontal breadth is the progression fantasy; raw stat-stacking is not.
- **Cosmetics never grant power.** Power comes from a separate, fair equipment system.
  This already exists as Non-Negotiable Ground Rule #3 (cosmetics are visual-only, enforced
  by the content schema + export pipeline) — see root [CLAUDE.md](../../CLAUDE.md). Restated
  here as a balance pillar so the constraint is visible from the design side, not only the
  content-schema side.

These are **design constraints**, not features. Future systems must respect them:
**#4 equipment / stat pipeline**, **#6 progression**, and **#7 economy** each have to fit
inside these ranges and this "variety, not inflation" shape rather than reaching for bigger
numbers or pay-for-power.

---

## Consequences

### Positive

- **One lens for modes, environment, and balance.** Like ADR-0005, these pillars are
  settled once. New modes, the dynamic-environment work, and every numeric system start from
  agreed direction instead of per-sprint debate.
- **Engine stays lean.** Decision 1 keeps mode variety out of the engine as branching code;
  the match engine keeps consuming data it does not have to understand the provenance of.
- **Seams are named before they are needed.** The data-driven win-condition model
  (Decision 1) and the per-turn environment advance (Decision 2) are identified as the exact
  insertion points, so neither is retrofitted under pressure when modes ship.
- **Balance is defended early.** Recording anti-inflation now, before #4/#6/#7 exist, stops
  number-creep from being designed in and then being expensive to walk back.

### Negative / Trade-offs

- **Discipline cost on modes.** Forbidding mode-specific code paths means more up-front
  schema design (a mode/win-condition data model) than a quick `if (mode == X)` branch.
  Accepted: it is the same trade ADR-0005 already took for formula shapes.
- **Dynamic environment widens match state.** Making environment per-turn state (rather than
  a constant input) adds a small amount of state-evolution logic and replay/serialization
  surface. Contained: it sits above the integrator and changes no ADR-0002 decision.
- **Balance pillars are judgment, not a formula.** "Readable range" and "deep-but-simple"
  are dispositions, not mechanical checks. Each future system must justify that it fits —
  this ADR sets the default, not a decision procedure (same caveat as ADR-0005).

---

## References

- [ADR-0002](ADR-0002-numeric-representation-and-sim-conventions.md) — Numeric Representation
  & Simulation Conventions (explicitly **not** reopened by Decision 2).
- [ADR-0004](ADR-0004-team-based-match-model.md) — Team-Based Match Model (the
  `MatchOptions` / turn-order / win-condition surface Decisions 1 build on).
- [ADR-0005](ADR-0005-revision-philosophy.md) — Revision Philosophy (the `ModeMultiplier` /
  data-not-code disposition Decision 1 extends).
- Root [CLAUDE.md](../../CLAUDE.md) — Non-Negotiable Ground Rule #3 (cosmetics never grant
  power), restated as a balance pillar in Decision 3.
