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
| `CombatantStats` | `readonly record struct` | MaxHp, DamageModifier (attacker multiplier), Defense (flat reduction) |
| `Combatant` | `readonly record struct` | Position (on terrain surface), Hp, Stats; `IsDefeated` when Hp ≤ 0 |
| `CombatantEntry` | `readonly record struct` | Roster slot: Combatant + TeamId + IAgent |
| `Weapon` | `readonly record struct` | ProjectileSpeed, BaseDamage, BlastRadius — data only |
| `FireAction` | `readonly record struct` | AngleDegrees, Speed (separate from weapon for charge mechanics), Weapon |
| `MatchOptions` | `readonly record struct` | FriendlyFire (ally blast damage on/off), SelfDamage (self blast damage on/off); both default true |
| `MatchState` | `readonly record struct` | Self, Allies (living teammates), Enemies (living opponents), Terrain, Environment, TurnNumber |
| `CombatantTurnResult` | `readonly record struct` | DamageReceived, HpBefore, HpAfter for one combatant in one turn |
| `TurnEvent` | `readonly record struct` | Full turn record: actor index, action, impact point, CombatantResults list (indexed by roster position); custom Equals for element-wise list comparison |
| `MatchOutcome` | `enum` | Team0Wins, Team1Wins, Draw (all teams KO in same turn), MaxTurnsReached |
| `MatchResult` | `readonly struct` | Outcome, WinningTeamId (nullable int), TurnCount, Log |
| `IAgent` | interface | `ChooseAction(MatchState)` — implemented by human adapters and AI bots |
| `ITurnOrderPolicy` | interface | `NextActor(livingIndices)` — seam for swappable turn scheduling |
| `RoundRobinTurnOrderPolicy` | `sealed class` | Default policy: teams alternate, living members cycle within each team in roster order |
| `IMatchSimulator` | interface | `Run(IReadOnlyList<CombatantEntry>, MatchOptions, ...)` — deterministic, seeded |
| `DamageCalculator` | `static class` | Pure blast damage: linear falloff × DamageModifier − Defense, clamped ≥ 0 |
| `MatchController` | `sealed class` | **Steppable, agent-agnostic turn driver.** Constructor takes combatants + teamIds separately (no agents). API: `IsOver`, `CurrentActorIndex`, `CurrentState`, `ResolveTurn(FireAction)` (throws `InvalidOperationException` if called after `IsOver`), `Result` (throws if not yet over). `MatchSimulator.Run` is a thin loop over this. |
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

Canonical data: `content/data/balls.json` (+ `content/schema/balls.schema.json`).

## Project Structure

```
sim/
  Sim/
    Core/         Vec2D, SimConstants
    Projectile/   FireCommand, WorldEnvironment, TrajectoryPoint, ShellPhysics,
                  ShotResult, IProjectileSimulator, ProjectileSimulator
    Terrain/      ITerrainQuery, FlatTerrain
    Match/        CombatantStats, Combatant, CombatantEntry, Weapon, FireAction,
                  MatchOptions, MatchState, CombatantTurnResult, TurnEvent,
                  MatchOutcome, MatchResult,
                  IAgent, ITurnOrderPolicy, RoundRobinTurnOrderPolicy,
                  IMatchSimulator, DamageCalculator, MatchController, MatchSimulator
    Content/      ShellType, BallDefinition, BallCatalog, BallDataException
    Ai/           BotDifficulty, BotAgent
  Sim.Tests/
    Projectile/   ProjectileSimulatorTests (10 tests), ShellPhysicsTests (6 — incl.
                  exact-equality no-regression of Neutral vs 3-arg path)
    Terrain/      ProjectileTerrainTests (9 cases, 3 via Theory)
    Match/        DamageCalculatorTests (7), MatchSimulatorTests (11 — 1v1 regression),
                  TeamMatchSimulatorTests (11 — team-specific),
                  MatchControllerTests (6 — steppable driver),
                  ScriptedAgent (test-only IAgent helper)
    Content/      BallCatalogTests (parse/validation/determinism),
                  BallsContentFileTests (validates shipped balls.json)
    Ai/           BotAgentTests (8 tests)
  Sim.sln          (87 tests green)
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
