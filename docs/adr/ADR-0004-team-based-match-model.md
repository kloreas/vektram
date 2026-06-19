# ADR-0004: Team-Based Match Model — N-vs-N, Swappable Turn Order, Configurable Friendly Fire

**Date:** 2026-06-19
**Status:** Accepted
**Deciders:** Architecture session, Phase 2

---

## Context

The initial match engine (`Sim.Match`) was a strict 1v1 duel. The game vision requires
1v1, 2v2, 4v4, and 6v6 formats for both PvP and PvE (player team vs enemy/boss team),
with a comfortable ceiling of 8 combatants per side. This ADR records the generalisation
made before the bot AI and Unity presentation layer were built on the 1v1 assumption —
the cheapest moment to widen the model.

---

## Decision

### 1 — Teams and the Match Roster

Each combatant belongs to a team identified by an integer id (0, 1, …). The current
requirement is two teams (0 vs 1). The identifier space is open: nothing in the data
model or engine logic assumes exactly two teams. FFA or multi-team modes are structurally
unblocked without further schema changes.

A match is constructed from a flat roster of `CombatantEntry` values, each bundling:

- `Combatant` — initial position and HP  
- `TeamId` — which team this slot belongs to  
- `IAgent` — the decision-maker (human adapter, scripted test agent, or future AI bot)

A 1v1 match is two `CombatantEntry` values with `TeamId` 0 and 1 — a clean special case
with no distinct code path.

### 2 — Win Condition

A team is *defeated* when **all** of its combatants have HP ≤ 0. After each turn's damage
is resolved:

```
aliveTeams = { teamId : ∃ combatant on that team with HP > 0 }

|aliveTeams| == 0  →  Draw   (all remaining teams eliminated in the same turn)
|aliveTeams| == 1  →  Team{x}Wins  (exactly one team survives)
|aliveTeams| ≥ 2  →  continue
```

This formulation handles the double-KO case correctly and extends to N teams without
change. A `MaxTurnsPerMatch` safety cap prevents infinite loops.

`MatchOutcome` uses `Team0Wins` / `Team1Wins` / `Draw` / `MaxTurnsReached`.
`MatchResult.WinningTeamId` (int?) carries the winning team id, or null for Draw /
MaxTurnsReached.

### 3 — Turn-Order Policy (Swappable Seam)

Turn order is delegated to `ITurnOrderPolicy`, a stateful single-method interface:

```csharp
int NextActor(IReadOnlyList<int> livingCombatantIndices);
```

The match engine creates one policy instance per match and calls `NextActor` once per
turn, passing the roster indices of still-living combatants. The policy returns the index
of the next actor.

**Default policy — `RoundRobinTurnOrderPolicy`:**  
Teams alternate; within each team, living members cycle in original roster order. Defeated
combatants are skipped automatically. For a 2v2 roster `[A(T0), B(T1), C(T0), D(T1)]`
the order is `A B C D A B C D …`. When C is defeated the pattern becomes `A B A D A B …`.

**Planned future policy — delay/agility-based ordering (Gunbound-style turn economy):**  
Each combatant earns turns according to an agility stat; faster combatants act more
frequently. This policy will likely require the `NextActor` signature to widen to include
live combatant state (agility values). That change is contained to the interface + the one
active implementation + the single call site in `MatchSimulator`. The current narrow
signature is a conscious trade-off accepted at this phase.

`MatchSimulator` instantiates `RoundRobinTurnOrderPolicy` directly inside `Run`. Policy
injection is deferred until the agility system is built, at which point the construction
site is the obvious replacement point.

### 4 — Friendly Fire and Self-Damage (Two Independent Options)

`MatchOptions` carries two independent boolean flags:

| Flag | Effect |
|------|--------|
| `FriendlyFire` | When true, allies (same-team combatants other than the shooter) within blast radius take damage. When false, allies are immune. |
| `SelfDamage` | When true, the shooter is vulnerable to their own blast. When false, the shooter is immune regardless of `FriendlyFire`. |

**Self is distinct from ally.** The `isActor` check is evaluated before `isAlly`. This
allows the "protect teammates but you can still blow yourself up" combination
(`FriendlyFire=false, SelfDamage=true`), which is the expected competitive default.

**Defaults:** both flags `true` — preserves the original 1v1 duel semantics (every
combatant in the blast radius takes damage). Casual / PvE modes opt into `FriendlyFire=false`.

**Self-damage rationale:** Self-damage from a poorly aimed shot is an intentional
skill-and-risk mechanic (the defining Gunbound / Worms property). It is on by default and
turned off only by explicit per-mode configuration.

### 5 — Team-Aware Agent View (`MatchState`)

The state presented to an agent each turn now carries:

- `Self` — the acting combatant (unchanged)
- `Allies` — living teammates on the same team, Self excluded
- `Enemies` — living combatants on all opposing teams

For 1v1: `Allies` is empty; `Enemies` has one element. No agent code needs to
special-case this.

### 6 — Turn Log (`TurnEvent`)

`CombatantResults` replaces the old `Combatant0Result` / `Combatant1Result` named
properties with `IReadOnlyList<CombatantTurnResult>` indexed by roster position. This
scales to N combatants. A combatant shielded by friendly-fire rules records 0 damage.

`TurnEvent` overrides `Equals` with element-wise list comparison so that determinism
tests (`Assert.Equal(log1[i], log2[i])`) work correctly despite the list being a
reference type.

---

## Consequences

### Positive

- **Future formats are cheap.** Adding 4v4, 6v6, PvE boss modes, or co-op requires
  only constructing the right `CombatantEntry` list and choosing appropriate `MatchOptions`.
  No engine logic changes.
- **Swappable turn policy.** The `ITurnOrderPolicy` seam makes the agility-based ordering
  a single-implementation swap with no engine rewrite.
- **FriendlyFire granularity.** Designers can independently tune self-damage and
  ally-damage per mode without code changes.
- **1v1 is a clean special case.** The original duel is two entries with team ids 0 and 1.
  All 11 original tests pass unchanged after the API update.
- **48 tests green.** 11 1v1 regression + 11 team-specific tests (2v2, FF options,
  turn order, draw, 4v4 scale, determinism) run in < 100 ms with zero warnings.

### Negative / Trade-offs

- **`MatchOptions` API is additive.** Any code calling `MatchSimulator.Run` must now
  pass a `MatchOptions` argument. This is a minor call-site change on the server and
  future client adapter.
- **Per-turn list allocations.** `MatchState.Allies`, `MatchState.Enemies`, and
  `TurnEvent.CombatantResults` allocate small lists each turn. With ≤ 16 combatants and
  ≤ 200 turns this is negligible (< 10 KB total), well within the per-turn (not per-tick)
  budget established by ADR-0002.
- **Turn-order policy is stateful.** Each match creates a fresh `RoundRobinTurnOrderPolicy`.
  This is correct but means the policy cannot be a shared singleton.

---

## Alternatives Considered

| Option | Reason Not Chosen |
|--------|------------------|
| Separate `ITeamMatchSimulator` alongside old `IMatchSimulator` | Two interfaces → maintenance burden; 1v1 is already a clean special case of the generalised API. |
| Single `FriendlyFire` flag covering both ally and self-damage | Cannot express "protect teammates but you can blow yourself up" (`FF=false, SD=true`) — a common competitive setting. Decoupled flags cost one bool and one branch. |
| Hardcode turn order in `MatchSimulator` | Blocks the agility/delay-based policy (a planned Phase 3 feature). The seam costs one interface file and one policy class. |
| `TurnEvent` with a `Dictionary<int, CombatantTurnResult>` | Unnecessary indirection; indexed array access is O(1) and roster indices are already stable. |
