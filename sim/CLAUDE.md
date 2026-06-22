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
| `CombatantStats` | `readonly record struct` | **Effective/final** stats the damage formula consumes (it never knows their origin; #4 equipment assembles them). 3-arg ctor `MaxHp, DamageModifier, Defense` preserved; neutral-default `init` props: `Attack`, `SunderArmor`, `BaseGuard`, `CritChance`, `CritMultiplier`, `Dodge`, `Element`, `ElementPower`, `ElementResist` |
| `Combatant` | `readonly record struct` | Position (on terrain surface), Hp, Stats; `IsDefeated` when Hp ≤ 0 |
| `CombatantEntry` | `readonly record struct` | Roster slot: Combatant + TeamId + IAgent |
| `Weapon` | `readonly record struct` | ProjectileSpeed, BaseDamage, BlastRadius — data only |
| `FireAction` | `readonly record struct` | AngleDegrees, Speed (separate from weapon for charge mechanics), Weapon |
| `MatchOptions` | `readonly record struct` | FriendlyFire (ally blast damage on/off), SelfDamage (self blast damage on/off); both default true |
| `MatchState` | `readonly record struct` | Self, Allies (living teammates), Enemies (living opponents), Terrain, Environment, TurnNumber |
| `DamageInputs` | `readonly record struct` | Resolved inputs to one damage calc: impact/target positions, BaseDamage/BlastRadius, attacker+defender stats, `CombatTuning`, `ModeMultiplier`, `ElementAdvantage`, `IsCrit`, `IsMiss`. RNG + table lookups are resolved by the caller so the formula stays pure. |
| `DamageResult` | `readonly record struct` | FinalDamage + breakdown for UI/replay: IsMiss, IsCrit, Falloff, GuardReduce, DefenceReduce, AttackFactor, ElementBonus, ModeMultiplier |
| `CombatRules` | `readonly record struct` | Match combat config from `/content`: `Tuning`, `ModeMultiplier` (room/mode seam for #5), `Elements` (nullable `ElementTable`). `CombatRules.Default` = engine fallback. |
| `CombatantTurnResult` | `readonly record struct` | DamageReceived, HpBefore, HpAfter (+ `IsCrit`, `IsMiss` init props, default false) for one combatant in one turn |
| `TurnEvent` | `readonly record struct` | Full turn record: actor index, action, impact point, CombatantResults list (indexed by roster position); custom Equals for element-wise list comparison |
| `MatchOutcome` | `enum` | Team0Wins, Team1Wins, Draw (all teams KO in same turn), MaxTurnsReached |
| `MatchResult` | `readonly struct` | Outcome, WinningTeamId (nullable int), TurnCount, Log |
| `IAgent` | interface | `ChooseAction(MatchState)` — implemented by human adapters and AI bots |
| `ITurnOrderPolicy` | interface | `NextActor(livingIndices)` — seam for swappable turn scheduling |
| `RoundRobinTurnOrderPolicy` | `sealed class` | Default policy: teams alternate, living members cycle within each team in roster order |
| `IMatchSimulator` | interface | `Run(IReadOnlyList<CombatantEntry>, MatchOptions, ..., seed, CombatRules? rules = null)` — deterministic, seeded |
| `DamageCalculator` | `static class` | **Pure** `Compute(in DamageInputs) → DamageResult`. DDTank-shaped formula: attackFactor × baseScale × falloff × (1−guardReduce) × (1−defenceReduce) × DamageModifier × ModeMultiplier × crit, + element bonus, floored at 0. Divisors/caps/curves come from `CombatTuning` (data). No RNG, no table lookups inside. |
| `MatchController` | `sealed class` | **Steppable, agent-agnostic turn driver.** Ctor takes combatants + teamIds (no agents) + optional `CombatRules? rules`. Owns a `SimRandom(seed)` for crit/miss rolls — **rolls are drawn only when `Dodge>0` / `CritChance>0`, so neutral-stat matches consume no RNG and stay bit-for-bit deterministic.** Does the seeded rolls + element-advantage lookup, then calls the pure `DamageCalculator`. API: `IsOver`, `CurrentActorIndex`, `CurrentState`, `ResolveTurn(FireAction)`, `Result`. |
| `MatchSimulator` | `sealed class` | Thin agent-driven loop over `MatchController`; uses `RoundRobinTurnOrderPolicy`. |

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
| `ItemCategory` | `enum` | `BallGrant`, `Consumable` (open; equipment categories = #4). |
| `ItemEffectKind` | `enum` | `GrantBall`, `RestoreHp` (open; stat/buff + equip effects = #4). |
| `ItemEffect` | `readonly record struct` | Data-defined effect: `Kind` + `BallId` (GrantBall) / `Amount` (RestoreHp). No item logic in C#. |
| `ItemDefinition` | `readonly record struct` | One item: `Id`, `DisplayName`, `Category`, `MaxStack`, `Effect`. |
| `ItemCatalog` | `sealed class` | Immutable id→`ItemDefinition` registry. `FromJson`, `Get`/`TryGet`, `Ids`, `Count`. Validates shape, unique ids, `maxStack≥1`, effect fields. **`ValidateBallReferences(BallCatalog)`** = optional fail-fast cross-catalog check (resolution-time `Get` also throws on dangling refs). |
| `ItemDataException` | `sealed class` | Clear error for malformed/invalid/missing item data; messages name id/field/index. |

Canonical data: `content/data/{balls,combat,elements,items}.json` (+ matching `content/schema/*.schema.json`).

### Items (runtime state — `Sim.Items`, not authored content)

Server-authoritative player-held state and the pure seams that resolve a data-defined
`ItemEffect`. The **client never mutates** inventory; it displays. `Sim.Items` has **no
`Sim.Match` dependency** (the heal seam works on plain doubles).

| Type | Kind | Purpose |
|------|------|---------|
| `Inventory` | `sealed class` (immutable, value-equality) | id→count. Pure ops: `Add`, `Remove`, `Consume`, `CountOf`, `Contains`, `Entries` (ordinal order), `StackCount`; `Inventory.Empty`. Never stores zero/negative counts; every op returns a new instance. |
| `ItemUseOutcome` | `readonly record struct` | `Success` + resulting `Inventory` (reduced on success, unchanged on failure). |
| `ItemEffects` | `static class` | Pure resolution seams: `ResolveGrantedBall(BallCatalog, ItemEffect)` → `BallDefinition`; `ResolveRestoredHp(currentHp, maxHp, amount)` → clamped HP (no overheal). |

**#3/#4 boundary:** #3 = item data + catalog + inventory + effect-resolution seams. #4 = equipment categories, the base+equip+rune+costume+buff modifier-stack that **assembles** effective `CombatantStats`, and wiring item/equip use into the live turn flow. Items here describe effects as **data**; they are not yet applied inside `MatchController`.

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
                  MatchOptions, MatchState, DamageInputs, DamageResult, CombatRules,
                  CombatantTurnResult, TurnEvent, MatchOutcome, MatchResult,
                  IAgent, ITurnOrderPolicy, RoundRobinTurnOrderPolicy,
                  IMatchSimulator, DamageCalculator, MatchController, MatchSimulator
    Content/      ShellType, BallDefinition, BallCatalog, BallDataException,
                  Element, CombatTuning, ElementTable, CombatDataException,
                  ItemCategory, ItemEffectKind, ItemEffect, ItemDefinition,
                  ItemCatalog, ItemDataException
    Items/        Inventory, ItemUseOutcome, ItemEffects
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
                  ScriptedAgent (test-only IAgent helper)
    Content/      BallCatalogTests, BallsContentFileTests (validates shipped balls.json),
                  CombatTuningTests (incl. combat.json == CombatTuning.Default drift-lock),
                  ElementTableTests, ItemCatalogTests,
                  ItemsContentFileTests (validates shipped items.json + ball refs)
    Items/        InventoryTests, ItemEffectsTests
    Ai/           BotAgentTests (8)
  Sim.sln          (161 tests green)
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
