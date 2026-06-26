# /content — Game Data Conventions

## Rules

- **Cosmetics are visual-only.** Every cosmetic entry must carry `visual_only: true`.
  No stat fields (`damage`, `defense`, `speed`, etc.) are permitted on cosmetic items.
  The export pipeline enforces this and fails the build on violation.
- **Never hard-code stats in C#.** All numeric tuning (weapon damage, armor values,
  tier thresholds) lives in `/content/data/`. C# code reads exported constants.
- **Schema changes require a migration entry.** Old exported artefacts must remain
  loadable by the live server for the duration of the compatibility window.
- **Designers own this directory.** Do not auto-generate files here from code.
  Data flows one way: `/content` → export → `/game` and `/server`.

## Data + Schema Convention (the reusable pattern)

Each content domain follows the same layout so `/sim` loaders stay uniform across systems
(balls, items, equipment, rooms, progression, economy):

- **Data:** `content/data/<domain>.json` — the authored values designers edit.
- **Schema:** `content/schema/<domain>.schema.json` — JSON Schema documenting every field,
  its unit, and its constraints (with each field's DDTank source meaning where applicable).
- **Versioning:** every data file carries a top-level integer `schemaVersion`. The matching
  `/sim` loader (`<Domain>Catalog.FromJson`) rejects unsupported versions. Bump it on
  breaking changes and keep old artefacts loadable for the compatibility window.
- **Loading boundary:** the host (server / Unity) reads the file; `/sim` parses the **text**
  via a pure `FromJson(string)` and never does file I/O. The parse is the single source of
  truth, so server and client cannot diverge.

Current files:

- `data/balls.json` (+ `schema/balls.schema.json`) — shell physics + blast/damage. Note the
  documented deferred item: velocity-dependent air-resistance drag is not modeled (it would
  void ADR-0002's Velocity-Verlet exactness); shell character is captured via `gravityScale` +
  `windSensitivity` instead.
- `data/combat.json` (+ `schema/combat.schema.json`) — damage formula tuning (guard/defence
  divisors + caps, falloff strength, attack floor/scale, base-damage bonus divisor). Loaded by
  `Sim.Content.CombatTuning.FromJson`. The C# fallback `CombatTuning.Default` mirrors this file
  and is **pinned to it by a test** (`combat.json == CombatTuning.Default`) so the two cannot
  drift — `/content` stays the single source of truth.
- `data/elements.json` (+ `schema/elements.schema.json`) — element set + a sparse advantage
  matrix (loaded by `Sim.Content.ElementTable.FromJson`). Unspecified pairs default to 1.0
  (pure DDTank additive); the advantage multipliers are **first-pass tuning**, to be iterated.
- `data/items.json` (+ `schema/items.schema.json`) — items and their data-defined effects
  (loaded by `Sim.Content.ItemCatalog.FromJson`). System #3 scope: `GrantBall` (references a
  `balls.json` id) and `RestoreHp` (flat amount). Item use is server-authoritative; the client
  only displays. `ItemCatalog.ValidateBallReferences(BallCatalog)` fail-fast checks `GrantBall`
  ids against `balls.json`. No `CombatTuning.Default`-style C# mirror, so no drift-lock test;
  a shipped-file test instead confirms it loads and its ball refs resolve. **Consumed live:**
  the `shell_heavy` row is genuinely used mid-match by the `server/Vektram.MatchHost` loadout+item
  demo (`MatchController.ResolveTurn(TurnAction)`), not just parsed. **Deferred (heal
  strengthen-scaling); equipment moved to its own file below.**
- `data/equipment.json` (+ `schema/equipment.schema.json`) — equipment pieces and the stat
  modifiers they grant (loaded by `Sim.Content.EquipmentCatalog.FromJson`). System #4 scope:
  three slots (`Weapon`/`Armor`/`Accessory`), one first-pass piece each. Each modifier is
  `{stat, op, value}`; `op` ∈ `Flat`/`AdditivePercent`/`MultiplicativePercent`, combined in
  that fixed order by `Sim.Stats.StatAssembler`. Provenance (source) is **stamped by the
  parser** (`Equipment` + piece id), not authored. Resolved into effective `CombatantStats` at
  match **setup** by `Sim.Stats.LoadoutResolver`, never in the turn loop. Values are **first-pass
  tuning, calibrated to `combat.json` divisors** so effects read as small percentages
  (anti-inflation, ADR-0006); a shipped-file test confirms it loads with valid stats/ops.
  **Consumed live:** the `weapon_recruit_cannon` + `armor_recruit_plate` rows are resolved through
  `LoadoutResolver` into a combatant's effective `CombatantStats` by the `server/Vektram.MatchHost`
  loadout+item demo — the modifier stack is now exercised in the product, not only in tests (the
  small per-piece deltas read as the intended anti-inflation percentages in that match).
  **Deferred:** base profile from class/level (#6), equipment-as-inventory (#7), rune trees,
  costumes (cosmetics never grant power), set bonuses, gear score.
- `data/modes.json` (+ `schema/modes.schema.json`) — game **modes** (the ruleset a match runs
  under), per ADR-0006 Decision 1 (modes are DATA, never hardcoded branches). Loaded by
  `Sim.Content.ModeCatalog.FromJson`. A "room" is the future `/server` lobby that *selects* a mode
  by id; the ruleset itself is the mode. Each mode carries `teamSizes` (team structure; empty =
  unconstrained), `modeMultiplier` (folded into the `CombatRules.ModeMultiplier` #2 seam),
  `friendlyFire`/`selfDamage` (→ `MatchOptions`), `turnOrder` (policy selection; `RoundRobin` only
  today), `maxTurns` (≤ `SimConstants.MaxTurnsPerMatch` = 200), and a `winCondition` — a
  discriminated-union-as-data (`{ kind, tiebreak? }`) evaluated each turn by the pure
  `Sim.Match.WinConditionEvaluator`, whose only switch is keyed by condition *kind* (never mode id).
  System #5 scope: two modes (`elimination` = last-team-standing, the data twin of pre-#5 behavior;
  `attrition_timed` = turn-limit tiebreak) and two real win conditions. The C# fallback
  `ModeDefinition.Default` **mirrors the `elimination` row** and is **pinned to it by a drift-lock
  test** (`elimination == ModeDefinition.Default`, like `combat.json` ↔ `CombatTuning.Default`).
  The pure `Sim.Match.ModeSetup` mapper splits a mode into the engine's primitives so the engine
  stays provenance-free. **Deferred:** per-mode item/equipment availability, economy rewards per
  room (#7), progression gating of modes (#6), mode-specific maps/terrain, dynamic per-turn
  environment (ADR-0006 Decision 2), the FFA `MatchOutcome` rename, a second turn-order policy
  (ADR-0004 agility seam), and further win conditions (hold-a-zone, survive-N-turns,
  defeat-a-boss-target — each a new `WinConditionKind` + one evaluator case; hold-a-zone also adds
  a field to `WinEvaluationContext`).

## Enforced by Export Pipeline (planned)

- Cosmetic stat check (fails build if a cosmetic has stat fields)
- Schema validation against JSON Schema definitions
- Referential integrity (weapon references valid tier IDs, etc.)
