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

## Project Structure

```
sim/
  Sim/          netstandard2.1 class library — the sim core
  Sim.Tests/    xUnit test project (net8.0)
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
