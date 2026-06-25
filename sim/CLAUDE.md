# /sim — Simulation Core

Shared C# class library (`netstandard2.1`). Referenced by the Nakama match server
and the Unity client. This is the single source of truth for all combat rules.

## Hard Rules

- **Zero Unity dependencies.** `dotnet build sim/Sim.sln` must succeed without
  Unity installed. If you see `UnityEngine` anywhere in this directory, it is a bug.
- **All randomness takes an explicit `uint seed` parameter.** No `new System.Random()`
  without a caller-supplied seed. Determinism is mandatory.
- **All public sim functions must be pure.** Same inputs → same outputs. No side
  effects, no I/O, no static mutable state.
- **Use `double` for simulation math.** The server runs at `double` precision.
  The Unity client converts to `float` only at the rendering layer in `/game`.
- **Host does file I/O; sim parses text.** `/sim` never opens files or touches platform
  APIs. Content (e.g. `balls.json`) is read by the host (match server / Unity content
  pipeline), which hands the **string** to a `*Catalog.FromJson` parser here. The parse
  is the single source of truth, so server and client cannot diverge. (The "no
  serialization" rule below means no **wire/network** serialization of sim state — that
  lives in `/proto`; static content parsing of caller-supplied text is the standard pattern.)

## Running Tests

```bash
dotnet test sim/Sim.sln
```

## Simulation Conventions (see ADR-0002)

| Convention | Value |
|------------|-------|
| Coordinate system | +X right, +Y up |
| Distance / time | metres / seconds |
| Angles | degrees, CCW from +X (0° = right, 90° = up) |
| Gravity | 9.8 m/s², applied as −Y acceleration |
| Wind | m/s² constant horizontal acceleration (+X = rightward) |
| Fixed timestep | 1/60 s (60 Hz) |
| Integration | Velocity Verlet (exact for constant acceleration) |
| Impact precision | Linear interpolation of terrain-surface crossing (sub-step) |

## Key Types

### Core / Projectile / Terrain

| Type | Kind | Purpose |
|------|------|---------|
| `Vec2D` | `readonly record struct` | Double-precision 2D vector with value equality |
| `SimConstants` | `static class` | All canonical numeric constants (incl. `MaxTurnsPerMatch = 200`) |
| `SimRandom` | `sealed class` | Deterministic xorshift64 RNG; splitmix64 seed init; `NextDouble()` → [0,1). Use instead of `System.Random` for cross-runtime reproducibility. |
| `FireCommand` | `readonly record struct` | Shot inputs: origin, angle, speed, seed |
| `WorldEnvironment` | `readonly record struct` | Gravity + wind for a round |
| `TrajectoryPoint` | `readonly record struct` | Position, velocity, time at one tick |
| `ShotResult` | `readonly struct` | Trajectory array + interpolated impact |
| `ShellPhysics` | `readonly record struct` | Per-shell `GravityScale` + `WindSensitivity` (dimensionless multipliers on the round's gravity/wind). `ShellPhysics.Neutral` = (1,1) → bit-identical to the no-shell path. Keeps per-shot acceleration **constant** so Verlet stays exact. **Deferred:** velocity-dependent air-resistance drag (DDTank `DragIndex`) is intentionally not modeled — it would void ADR-0002's exactness and needs its own ADR amendment. |
| `IProjectileSimulator` | interface | Contract for server authority and client preview; `Simulate(...)` has a 3-arg overload and a 4-arg overload taking `ShellPhysics` |
| `ProjectileSimulator` | class | Concrete Velocity Verlet implementation; 3-arg `Simulate` delegates to 4-arg with `ShellPhysics.Neutral` |
| `ITerrainQuery` | interface | Read-only heightfield: GetHeight(x) — no overhangs/caves/walls |
| `FlatTerrain` | class | Constant-height terrain; `FlatTerrain.Ground` is the y = 0 baseline |

### Match

| Type | Kind | Purpose |
|------|------|---------|
| `CombatantStats` | `readonly record struct` | **Effective/final** stats the damage formula consumes (it never knows their origin; the #4 `Sim.Stats` stack assembles them). 3-arg ctor `MaxHp, DamageModifier, Defense` preserved; neutral-default `init` props: `Attack`, `SunderArmor`, `BaseGuard`, `CritChance`, `CritMultiplier`, `Dodge`, `Element`, `ElementPower`, `ElementResist`. Shape **unchanged by #4** — modifiers target these existing channels. |
| `Combatant` | `readonly record struct` | Position (on terrain surface), Hp, Stats; `IsDefeated` when Hp ≤ 0 |
| `CombatantEntry` | `readonly record struct` | Roster slot: Combatant + TeamId + IAgent |
| `Weapon` | `readonly record struct` | ProjectileSpeed, BaseDamage, BlastRadius — data only |
| `FireAction` | `readonly record struct` | AngleDegrees, Speed (separate from weapon for charge mechanics), Weapon |
| `TurnAction` | `readonly record struct` | A turn's submission: `Fire` (FireAction) + optional `ItemId` (string?). `ItemId == null` = fire-only (bit-identical to the FireAction path). The seam by which an actor uses one item **before** its shot. |
| `TurnItemUse` | `readonly record struct` | Logged on `TurnEvent.ItemUse` (nullable): `ItemId`, `Kind` (`ItemEffectKind?`, null when id unknown), `Applied`, `HpRestored`, `GrantedBallId`. A rejected use is logged with `Applied=false`. |
| `MatchOptions` | `readonly record struct` | FriendlyFire (ally blast damage on/off), SelfDamage (self blast damage on/off); both default true |
| `MatchState` | `readonly record struct` | Self, Allies (living teammates), Enemies (living opponents), Terrain, Environment, TurnNumber |
| `DamageInputs` | `readonly record struct` | Resolved inputs to one damage calc: impact/target positions, BaseDamage/BlastRadius, attacker+defender stats, `CombatTuning`, `ModeMultiplier`, `ElementAdvantage`, `IsCrit`, `IsMiss`. RNG + table lookups are resolved by the caller so the formula stays pure. |
| `DamageResult` | `readonly record struct` | FinalDamage + breakdown for UI/replay: IsMiss, IsCrit, Falloff, GuardReduce, DefenceReduce, AttackFactor, ElementBonus, ModeMultiplier |
| `CombatRules` | `readonly record struct` | Match combat config from `/content`: `Tuning`, `ModeMultiplier` (room/mode seam for #5), `Elements` (nullable `ElementTable`). `CombatRules.Default` = engine fallback. |
| `CombatantTurnResult` | `readonly record struct` | DamageReceived, HpBefore, HpAfter (+ `IsCrit`, `IsMiss` init props, default false) for one combatant in one turn |
| `TurnEvent` | `readonly record struct` | Full turn record: actor index, action, impact point, CombatantResults list (indexed by roster position), nullable `ItemUse` (init prop); custom Equals for element-wise list comparison (now also compares `ItemUse`) |
| `MatchOutcome` | `enum` | Team0Wins, Team1Wins, Draw (all teams KO in same turn), MaxTurnsReached. **Unchanged by #5.** Team0/1Wins are 2-team-shaped; `WinningTeamId` is the general N-team carrier. The FFA rename is deferred (lands with the first FFA mode). |
| `MatchResult` | `readonly struct` | Outcome, WinningTeamId (nullable int), TurnCount, Log |
| `MatchModeRules` | `readonly record struct` | The engine-facing, provenance-free slice of a mode: `WinCondition`, `MaxTurns`, `TurnOrder`. Parallel to `MatchOptions`/`CombatRules`. `Default` = (LastTeamStanding, 200, RoundRobin) = pre-#5 behavior. Produced by `ModeSetup`. |
| `TeamStanding` | `readonly record struct` | Per-team snapshot the evaluator reads: `TeamId`, `AliveCount`, `TotalHpRemaining`, `TotalDamageDealt`. Built each turn from cheap running tallies (pure sums, no RNG). |
| `WinEvaluationContext` | `readonly record struct` | Post-turn match-progress snapshot: `TurnNumber`, `MaxTurns`, per-team `Standings`. **Extension boundary:** a condition needing new state (hold-a-zone) adds a field here + one evaluator case; conditions reading existing progress (survive-N, defeat-boss) need no change. |
| `WinEvaluation` | `readonly record struct` | Evaluator result: `IsFinished` + `Outcome` + `WinningTeamId`. `Continue` / `Finished(...)` factories. |
| `WinConditionEvaluator` | `static class` | **Pure** `Evaluate(in WinConditionDefinition, in WinEvaluationContext) → WinEvaluation`. The ONLY switch keyed by condition KIND (never mode id), so many modes reuse a few cases. `LastTeamStanding` (= pre-#5 logic, bit-for-bit) + `TurnLimitTiebreak` (argmax over alive teams; tie → Draw). No RNG/IO. `OutcomeForWinner` preserves the `0→Team0Wins/else→Team1Wins` carry-forward verbatim (deliberate; FFA rename deferred). |
| `ModeSetup` | `static class` | **Pure** mapper splitting a `ModeDefinition` into the engine's primitives: `ToMatchOptions` / `ToCombatRules` (folds `ModeMultiplier`) / `ToModeRules`, plus `Resolve(...)` → all three. Lives in `/sim` so server and client derive identical config. |
| `IAgent` | interface | `ChooseAction(MatchState)` — implemented by human adapters and AI bots |
| `ITurnOrderPolicy` | interface | `NextActor(livingIndices)` — seam for swappable turn scheduling |
| `RoundRobinTurnOrderPolicy` | `sealed class` | Default policy: teams alternate, living members cycle within each team in roster order |
| `IMatchSimulator` | interface | `Run(IReadOnlyList<CombatantEntry>, MatchOptions, ..., seed, CombatRules? rules = null, MatchModeRules? modeRules = null)` — deterministic, seeded |
| `DamageCalculator` | `static class` | **Pure** `Compute(in DamageInputs) → DamageResult`. DDTank-shaped formula: attackFactor × baseScale × falloff × (1−guardReduce) × (1−defenceReduce) × DamageModifier × ModeMultiplier × crit, + element bonus, floored at 0. Divisors/caps/curves come from `CombatTuning` (data). No RNG, no table lookups inside. |
| `MatchController` | `sealed class` | **Steppable, agent-agnostic turn driver.** Ctor takes combatants + teamIds (no agents) + optional `CombatRules? rules` + optional item deps (`inventories`, `itemCatalog`, `ballCatalog`; all null ⇒ no-items mode = pre-item behavior) + optional `MatchModeRules? modeRules` (null ⇒ `MatchModeRules.Default` = pre-#5 last-team-standing / 200-cap / round-robin). **#5:** selects the turn-order policy from `modeRules.TurnOrder`, keeps a per-team damage-dealt tally (pure sums), and ends the match by calling the pure `WinConditionEvaluator` on a per-turn `WinEvaluationContext` instead of the old inlined team-wipe check — bit-for-bit identical under the default mode (no RNG drawn by the tally). Owns a `SimRandom(seed)` for crit/miss rolls — **rolls are drawn only when `Dodge>0` / `CritChance>0`, so neutral-stat matches consume no RNG and stay bit-for-bit deterministic.** Does the seeded rolls + element-advantage lookup, then calls the pure `DamageCalculator`. **Item use** (system #3) is server-authoritative + deterministic (no RNG): `ResolveTurn(TurnAction)` optionally consumes one item before the shot — `RestoreHp` heals the actor (clamped, pre-shot, so a self-blast hits post-heal HP); `GrantBall` swaps the shell so **both** trajectory (ball `ShellPhysics` via 4-arg `Simulate`) **and** damage (ball `BaseDamage`/`BlastRadius`) come from the same ball that turn. Unavailable/unknown items are **rejected cleanly** (no effect, inventory unchanged, turn fires fire-only, logged `Applied=false`); no mid-match throw. API: `IsOver`, `CurrentActorIndex`, `CurrentState`, `ResolveTurn(FireAction)` (delegates to fire-only `TurnAction`), `ResolveTurn(TurnAction)`, `InventoryOf(int)`, `Result`. |
| `MatchSimulator` | `sealed class` | Thin agent-driven loop over `MatchController`; forwards optional `modeRules` (turn-order policy now chosen by the controller from the mode). |

### AI

| Type | Kind | Purpose |
|------|------|---------|
| `BotDifficulty` | `readonly record struct` | Tuning knobs: `SearchBudget`, `AimNoiseDegrees`, `WindCompensationFactor`. Presets: `Easy`, `Medium`, `Hard`. Canonical values → /content eventually. |
| `BotAgent` | `sealed class : IAgent` | Grid-searches launch angles, scores by X-distance to nearest enemy, applies aim noise via `SimRandom`. Constructor takes `IProjectileSimulator`, `Weapon`, `BotDifficulty`, `uint seed`. |

### Content

Data-driven content loaded from `/content` (authored JSON). This is the reusable pattern
for later systems (items, equipment, rooms, progression, economy): an immutable record
model + a `<Domain>Catalog` with a **pure `FromJson(string)`** parser + a
`<Domain>DataException`. Hosts read the file; `/sim` parses the text.

| Type | Kind | Purpose |
|------|------|---------|
| `ShellType` | `enum` | Shell behaviour tag (DDTank `BombType`): `Standard`, `Heavy`, `Light`. Special behaviours (frozen/cure/fly) added with later systems. |
| `BallDefinition` | `readonly record struct` | One shell type: `Id`, `DisplayName`, `Type`, `Physics` (`ShellPhysics`), `BlastRadius`, `BaseDamage`, `ProjectileSpeed`. Superset of `Weapon`'s physics fields. **Future seam:** `Weapon` will later be sourced from a `BallDefinition`. |
| `BallCatalog` | `sealed class` | Immutable id→`BallDefinition` registry. `FromJson(string)` (pure, non-reflective `JsonDocument` parse, IL2CPP-safe), `Get`/`TryGet`, `Ids`, `Count`. Validates schema version, required fields, value ranges, duplicate ids. |
| `BallDataException` | `sealed class` | Clear error for malformed/invalid/missing ball data; messages name the id/field/index. |
| `Element` | `enum` | Damage element (DDTank emblem set): `None, Fire, Water, Wind, Land, Light, Dark`. |
| `CombatTuning` | `readonly record struct` | Damage formula divisors/caps/curves (guard/defence divisors + caps, falloffStrength, attackFloor/Scale, baseDamageBonusDivisor). `FromJson(string)`. `CombatTuning.Default` **mirrors `combat.json`** and is **pinned to it by a drift-lock test**. |
| `ElementTable` | `sealed class` | Data-driven element advantage matrix. `FromJson(string)`, `Advantage(attacker, defender)` (unspecified pairs → 1.0 = pure DDTank additive). `ElementTable.Neutral` = all 1.0. |
| `CombatDataException` | `sealed class` | Clear error for malformed/invalid combat tuning or element data. |
| `ItemCategory` | `enum` | `BallGrant`, `Consumable`, `Equipment` (the last is a #4 zero-cost ownership tag — equipment stat data lives in `EquipmentCatalog`, not an `ItemEffect`; nothing authors an item of this category yet). |
| `ItemEffectKind` | `enum` | `GrantBall`, `RestoreHp` (unchanged by #4 — equipment grants stats through the modifier stack, not a use-effect; stat/buff effects still open). |
| `ItemEffect` | `readonly record struct` | Data-defined effect: `Kind` + `BallId` (GrantBall) / `Amount` (RestoreHp). No item logic in C#. |
| `ItemDefinition` | `readonly record struct` | One item: `Id`, `DisplayName`, `Category`, `MaxStack`, `Effect`. |
| `ItemCatalog` | `sealed class` | Immutable id→`ItemDefinition` registry. `FromJson`, `Get`/`TryGet`, `Ids`, `Count`. Validates shape, unique ids, `maxStack≥1`, effect fields. **`ValidateBallReferences(BallCatalog)`** = optional fail-fast cross-catalog check (resolution-time `Get` also throws on dangling refs). |
| `ItemDataException` | `sealed class` | Clear error for malformed/invalid/missing item data; messages name id/field/index. |
| `EquipmentSlot` | `enum` | First-pass body slots: `Weapon`, `Armor`, `Accessory`. More slots (mount/pet/second weapon) added later. |
| `EquipmentDefinition` | `readonly record struct` | One equipment piece: `Id`, `DisplayName`, `Slot`, `Modifiers` (`IReadOnlyList<StatModifier>`). **Custom element-wise equality** over `Modifiers` (like `TurnEvent`) so same text → value-equal. Grants stats via the stack, not a use-effect. |
| `EquipmentCatalog` | `sealed class` | Immutable id→`EquipmentDefinition` registry. `FromJson` (pure, non-reflective `JsonDocument`, IL2CPP-safe), `Get`/`TryGet`, `Ids`, `Count`. Validates schema version, required/empty fields, unique ids, known `slot`/`stat`/`op`, numeric `value`. **Stamps** each modifier's `Source` = (`Equipment`, piece id) — provenance is derived, not authored. |
| `EquipmentDataException` | `sealed class` | Clear error for malformed/invalid/missing equipment data; messages name id/field/index. |
| `TurnOrderPolicyKind` | `enum` | Data-driven SELECTION of an `ITurnOrderPolicy`. `RoundRobin` only today; the ADR-0004 agility policy lands as a new value + one mapping case. |
| `WinConditionKind` | `enum` | The win-condition evaluator family: `LastTeamStanding`, `TurnLimitTiebreak`. The ONE enum a mode's win condition dispatches on (never mode id). |
| `TiebreakMetric` | `enum` | `TotalHpRemaining`, `TotalDamageDealt` — decides a `TurnLimitTiebreak` at the turn cap. |
| `WinConditionDefinition` | `readonly record struct` | Discriminated-union-as-data: `Kind` + optional `Tiebreak`. `LastTeamStanding` static = the pre-#5 case. The JSON shape (`{kind, tiebreak?}`) lets a future kind add its own params. |
| `ModeDefinition` | `readonly record struct` | One **mode** (the ruleset a match runs under): `Id`, `DisplayName`, `TeamSizes` (empty = unconstrained), `ModeMultiplier`, `FriendlyFire`, `SelfDamage`, `TurnOrder`, `MaxTurns`, `WinCondition`. **Custom element-wise equality** over `TeamSizes` (like `EquipmentDefinition`). `Default` mirrors the `elimination` row (drift-locked) and reproduces pre-#5 behavior. `AcceptsRoster(teamIds)` = validate-only structure check. |
| `ModeCatalog` | `sealed class` | Immutable id→`ModeDefinition` registry. `FromJson` (pure, non-reflective `JsonDocument`, IL2CPP-safe), `Get`/`TryGet`/`Ids`/`Count`. Validates schema version, required/empty fields, unique ids, known `turnOrder`/`winCondition.kind`/`tiebreak`, `modeMultiplier > 0`, `maxTurns ∈ 1..MaxTurnsPerMatch`, tiebreak required for `TurnLimitTiebreak` / forbidden for `LastTeamStanding`. |
| `ModeDataException` | `sealed class` | Clear error for malformed/invalid/missing mode data; messages name id/field/index. |

Canonical data: `content/data/{balls,combat,elements,items,equipment,modes}.json` (+ matching `content/schema/*.schema.json`).

### Stats (modifier stack — `Sim.Stats`, system #4)

The layer that **assembles** effective `CombatantStats` from a base block + source-tagged
modifiers — pure, deterministic, resolved at **match setup** (not in the turn loop), so the
match/damage code keeps consuming finished stats and stays untouched.

| Type | Kind | Purpose |
|------|------|---------|
| `StatKind` | `enum` | The 11 numeric channels of `CombatantStats` (MaxHp…ElementResist). `Element` is absent — it's assigned, not stacked. Contiguous from 0; `StatAssembler.ChannelCount` pins the count (guard test). |
| `ModifierOp` | `enum` | `Flat`, `AdditivePercent`, `MultiplicativePercent` — combined in that fixed order. |
| `ModifierSourceType` | `enum` | `Base`, `Equipment`, `Rune`, `Costume`, `Buff`. Tags provenance so a source is auditable/removable. Equipment is the only fully-wired source in #4; Rune/Costume/Buff are seams. |
| `ModifierSource` | `readonly record struct` | `Type` + `SourceId`. Dropping all modifiers with one `SourceId` and re-assembling reproduces the without-that-source result (unequip / expiring-buff seam). |
| `StatModifier` | `readonly record struct` | `Stat` + `Op` + `Value` + `Source`. Percent ops take a fraction (`0.10` = 10%). |
| `StatAssembler` | `static class` | **Pure** `Assemble(CombatantStats base, IReadOnlyList<StatModifier>) → CombatantStats`. Per channel: `(base + ΣFlat) × (1 + ΣAdd%) × Π(1+Mult%)`. Accumulates in caller order (deterministic; Σ/Π order-independent for exact operands). Clamps: CritChance/Dodge → [0,1], MaxHp floored > 0, rest floored at 0. Element passed through. No RNG/IO/Unity. |
| `Loadout` | `readonly record struct` | `BaseStats` + `EquippedIds` + `Runes` (modifier seam) + `CosmeticId` (display-only, zero power). `Loadout.Bare(base)` = no gear. Gather order: equipped ids → each piece's modifiers → runes. |
| `LoadoutResolver` | `static class` | **Pure** `Resolve(in Loadout, EquipmentCatalog) → CombatantStats`. Gathers modifiers in fixed order, calls `StatAssembler`. Costume ignored (cosmetic pillar). Unknown id → `EquipmentDataException`. Called by host/controller setup, never in the turn loop. |

### Items (runtime state — `Sim.Items`, not authored content)

Server-authoritative player-held state and the pure seams that resolve a data-defined
`ItemEffect`. The **client never mutates** inventory; it displays. `Sim.Items` has **no
`Sim.Match` dependency** (the heal seam works on plain doubles).

| Type | Kind | Purpose |
|------|------|---------|
| `Inventory` | `sealed class` (immutable, value-equality) | id→count. Pure ops: `Add`, `Remove`, `Consume`, `CountOf`, `Contains`, `Entries` (ordinal order), `StackCount`; `Inventory.Empty`. Never stores zero/negative counts; every op returns a new instance. |
| `ItemUseOutcome` | `readonly record struct` | `Success` + resulting `Inventory` (reduced on success, unchanged on failure). |
| `ItemEffects` | `static class` | Pure resolution seams: `ResolveGrantedBall(BallCatalog, ItemEffect)` → `BallDefinition`; `ResolveRestoredHp(currentHp, maxHp, amount)` → clamped HP (no overheal). |

**#3/#4 boundary:** #3 (complete) = item data + catalog + inventory + effect-resolution seams **+ item use wired into the live turn** (`MatchController.ResolveTurn(TurnAction)` applies `GrantBall`/`RestoreHp` via the pure `ItemEffects` seams; inventory is controller-side server-authoritative state and never leaks into the pure `DamageCalculator`). **#4 (done)** = the `Sim.Stats` modifier stack + `EquipmentCatalog`/`equipment.json` + `Loadout`/`LoadoutResolver` that **assemble** effective `CombatantStats` at match setup. `CombatantStats` shape was **unchanged** (zero new fields) so the formula/controller/simulator are untouched and the prior 173 stayed green. **Deferred from #4** (each re-attaches at a named seam): base profile from class/level → progression #6 (`Loadout.BaseStats`); equipment-as-ownable-inventory → economy #7 (`ItemCategory.Equipment`); full rune trees → next slice (`Loadout.Runes` + a future `RuneCatalog`); set bonuses → post-gather source in `LoadoutResolver`; gear score → pure read over resolved modifiers (display only); element-granting weapons + mid-match buff re-assembly → noted seams. Bots stay fire-only; bot item-use/loadouts are a future knob.

**#5 boundary (room/mode config, done):** modes are **data** (`modes.json` → `ModeCatalog`/`ModeDefinition`), per ADR-0006 Decision 1. A "room" is the future `/server` lobby that *selects* a mode by id; the ruleset **is** the mode. The pure `ModeSetup` mapper splits a `ModeDefinition` into the engine's existing provenance-free primitives (`MatchOptions` / `CombatRules` / new `MatchModeRules`), so the engine never special-cases a mode. The **win condition is data**: `WinConditionDefinition` (discriminated-union-as-data) is evaluated each turn by the pure `WinConditionEvaluator`, whose **only switch is keyed by condition KIND, never by mode id** — so many modes reuse a few evaluators (`LastTeamStanding` + `TurnLimitTiebreak` shipped). `MatchController` gained one optional `MatchModeRules? modeRules` param; null ⇒ `Default`, reproducing the pre-#5 last-team-standing / 200-cap / round-robin path **bit-for-bit** (the standings tally is pure summation, so no RNG is drawn — the prior 213 stayed green). `MatchOutcome`/`MatchResult` were **unchanged** (the `0→Team0Wins/else→Team1Wins` map is a deliberate carry-forward). **Deferred** (each re-attaches at a named seam): per-mode item/equipment availability + per-type stat scaling/floors → progression #6; economy rewards per room → #7; mode-specific maps/terrain + dynamic per-turn environment → ADR-0006 Decision 2; matchmaking/lobbies → `/server`; the FFA `MatchOutcome` rename → first FFA mode (`WinningTeamId` already carries it); a second turn-order policy → ADR-0004 agility seam (`TurnOrderPolicyKind`); further win conditions (hold-a-zone, survive-N-turns, defeat-a-boss-target) → a new `WinConditionKind` + one evaluator case (hold-a-zone also adds a field to `WinEvaluationContext`; defeat-a-boss is already expressible as last-team-standing with the boss as its own team id).

**Damage model (system #2, ADR-0005):** the formula *shape* is adopted from DDTank's `MakeDamage` (multiplicative guard/defence DR with caps, attack scaling with a floor, a forgiving distance falloff, an additive element layer + modern advantage multiplier, a crit multiplier). Divisors/curves live in `/content`, not C#. **Deliberately not replicated:** integer truncation of final damage, per-room formula-*shape* switching (a scalar `ModeMultiplier` is used instead; rooms #5 supply it), and client-trusted rolls (all crit/miss rolls are server-side seeded `SimRandom`). **Deferred:** separate magic layer, `ExtraDamage`/`CulturalAdd` buff multipliers, and pet sigmoid DR.

## Project Structure

```
sim/
  Sim/
    Core/         Vec2D, SimConstants
    Projectile/   FireCommand, WorldEnvironment, TrajectoryPoint, ShellPhysics,
                  ShotResult, IProjectileSimulator, ProjectileSimulator
    Terrain/      ITerrainQuery, FlatTerrain
    Match/        CombatantStats, Combatant, CombatantEntry, Weapon, FireAction,
                  TurnAction, TurnItemUse, MatchOptions, MatchState, DamageInputs,
                  DamageResult, CombatRules, CombatantTurnResult, TurnEvent,
                  MatchOutcome, MatchResult, IAgent, ITurnOrderPolicy,
                  RoundRobinTurnOrderPolicy, IMatchSimulator, DamageCalculator,
                  MatchController, MatchSimulator,
                  MatchModeRules, TeamStanding, WinEvaluationContext,
                  WinEvaluation, WinConditionEvaluator, ModeSetup
    Content/      ShellType, BallDefinition, BallCatalog, BallDataException,
                  Element, CombatTuning, ElementTable, CombatDataException,
                  ItemCategory, ItemEffectKind, ItemEffect, ItemDefinition,
                  ItemCatalog, ItemDataException, EquipmentSlot,
                  EquipmentDefinition, EquipmentCatalog, EquipmentDataException,
                  TurnOrderPolicyKind, WinConditionKind, TiebreakMetric,
                  WinConditionDefinition, ModeDefinition, ModeCatalog, ModeDataException
    Items/        Inventory, ItemUseOutcome, ItemEffects
    Stats/        StatKind, ModifierOp, ModifierSourceType, ModifierSource,
                  StatModifier, StatAssembler, Loadout, LoadoutResolver
    Ai/           BotDifficulty, BotAgent
  Sim.Tests/
    Projectile/   ProjectileSimulatorTests (10), ShellPhysicsTests (6 — incl.
                  exact-equality no-regression of Neutral vs 3-arg path)
    Terrain/      ProjectileTerrainTests (9 cases, 3 via Theory)
    Match/        DamageCalculatorTests (18 — new DR/element/crit/mode model),
                  MatchSimulatorTests (11 — 1v1 regression),
                  TeamMatchSimulatorTests (11 — team-specific),
                  MatchControllerTests (6 — steppable driver),
                  MatchControllerCombatTests (5 — crit/miss/element/mode in-match),
                  MatchControllerItemTests (12 — heal/grant-ball/reject/determinism
                  + heal-before-self-blast + same-ball trajectory&damage),
                  WinConditionEvaluatorTests (11 — both conditions + boss-as-team case),
                  ModeSetupTests (5 — mode → engine primitives),
                  MatchControllerModeTests (8 — null==explicit-default bit-for-bit,
                  tiebreak at cap, mode MaxTurns, boss-as-team, determinism, sim forwarding),
                  ScriptedAgent (test-only IAgent helper)
    Content/      BallCatalogTests, BallsContentFileTests (validates shipped balls.json),
                  CombatTuningTests (incl. combat.json == CombatTuning.Default drift-lock),
                  ElementTableTests, ItemCatalogTests,
                  ItemsContentFileTests (validates shipped items.json + ball refs),
                  EquipmentCatalogTests (14 — parse/validation/equality),
                  EquipmentContentFileTests (validates shipped equipment.json),
                  ModeCatalogTests (19 — parse/validation/equality/AcceptsRoster),
                  ModesContentFileTests (2 — shipped modes.json + elimination==Default drift-lock)
    Items/        InventoryTests, ItemEffectsTests
    Stats/        StatAssemblerTests (16 — order/determinism/clamp/source-removal/
                  rune+equip stack/Element passthrough/ChannelCount guard),
                  LoadoutResolverTests (9 — assembly seam, cosmetic-zero-power,
                  unknown-id throw, feeds DamageCalculator)
    Ai/           BotAgentTests (8)
  Sim.sln          (258 tests green)
```

## What Belongs Here

- Projectile trajectory math (angle, power; per-shell gravity/wind via `ShellPhysics`)
- Wind and gravity modifiers
- Terrain collision detection and deformation math
- Damage formulas and effective-stat calculations
- Turn order and shot-clock logic
- Seeded, reproducible RNG utilities
- Parsing caller-supplied content text into immutable typed catalogs (`*Catalog.FromJson`)

> **Deferred:** velocity-dependent air-resistance drag (DDTank `DragIndex`). It makes
> per-shot acceleration non-constant and would void ADR-0002's Velocity-Verlet exactness
> guarantee. Adding it is a future task gated on an ADR-0002 amendment + integrator
> re-validation — do not smuggle it into the constant-acceleration path.

## What Does NOT Belong Here

- Rendering, UI, audio, animation
- Wire/network serialization of sim state (that lives in `/proto`). Parsing static
  content text into immutable types is allowed (see Hard Rules).
- File I/O or any platform API (the host reads files; `/sim` parses the supplied text)
- `UnityEngine.*` types
- Match lifecycle orchestration (that lives in `/server`)
