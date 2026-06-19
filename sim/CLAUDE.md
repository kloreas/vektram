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
| `FireCommand` | `readonly record struct` | Shot inputs: origin, angle, speed, seed |
| `WorldEnvironment` | `readonly record struct` | Gravity + wind for a round |
| `TrajectoryPoint` | `readonly record struct` | Position, velocity, time at one tick |
| `ShotResult` | `readonly struct` | Trajectory array + interpolated impact |
| `IProjectileSimulator` | interface | Contract for server authority and client preview |
| `ProjectileSimulator` | class | Concrete Velocity Verlet implementation |
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
| `MatchSimulator` | class | Team-based turn engine; uses `RoundRobinTurnOrderPolicy`; win condition: last team with living members |

## Project Structure

```
sim/
  Sim/
    Core/         Vec2D, SimConstants
    Projectile/   FireCommand, WorldEnvironment, TrajectoryPoint,
                  ShotResult, IProjectileSimulator, ProjectileSimulator
    Terrain/      ITerrainQuery, FlatTerrain
    Match/        CombatantStats, Combatant, CombatantEntry, Weapon, FireAction,
                  MatchOptions, MatchState, CombatantTurnResult, TurnEvent,
                  MatchOutcome, MatchResult,
                  IAgent, ITurnOrderPolicy, RoundRobinTurnOrderPolicy,
                  IMatchSimulator, DamageCalculator, MatchSimulator
  Sim.Tests/
    Projectile/   ProjectileSimulatorTests (10 tests)
    Terrain/      ProjectileTerrainTests (9 cases, 3 via Theory)
    Match/        DamageCalculatorTests (7), MatchSimulatorTests (11 — 1v1 regression),
                  TeamMatchSimulatorTests (11 — team-specific),
                  ScriptedAgent (test-only IAgent helper)
  Sim.sln
```

## What Belongs Here

- Projectile trajectory math (angle, power, drag, air resistance)
- Wind and gravity modifiers
- Terrain collision detection and deformation math
- Damage formulas and effective-stat calculations
- Turn order and shot-clock logic
- Seeded, reproducible RNG utilities

## What Does NOT Belong Here

- Rendering, UI, audio, animation
- Network I/O or serialization
- File I/O or any platform API
- `UnityEngine.*` types
- Match lifecycle orchestration (that lives in `/server`)
