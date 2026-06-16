# ADR-0002: Numeric Representation and Simulation Conventions

**Date:** 2026-06-16
**Status:** Accepted
**Deciders:** Architecture session, Phase 1

---

## Context

The simulation core (`/sim`) must be deterministic: identical inputs must always
produce identical outputs. Before writing any physics code we must fix:

1. The numeric type for all simulation math.
2. A fixed timestep and integration method.
3. Canonical conventions for coordinates, units, and angles — recorded in one
   place (`SimConstants`) so every formula is consistent.

Two constraints shape the decision:

- **Intra-runtime reproducibility only.** Under the server-authoritative
  architecture (ADR-0001) the server's result is always final. The client
  replays the simulation for visual purposes and is corrected on receipt.
  We do not need client and server to produce bit-identical results across
  different hardware, OS, or JIT configurations.

- **No per-step heap allocation.** The sim runs per-shot on the server and
  continuously on mobile for trajectory preview. Allocating inside the inner
  physics loop causes GC pressure on constrained devices.

---

## Decision

### Numeric type — `double` (IEEE 754 double-precision)

All simulation math in `/sim` uses `double`. Conversion to `float` happens only at
the Unity rendering layer in `/game`, never inside the sim.

### Integration method — Velocity Verlet

```
x_new = x + v × dt + a × dt² / 2
v_new = v + a × dt
```

For **constant acceleration** (gravity and wind are both constants), Velocity Verlet
is analytically exact: `x_verlet(n)` equals `x_exact(n × dt)` to floating-point
precision. This means the only position error in the trajectory is floating-point
rounding, not truncation error.

Semi-implicit (symplectic) Euler was the initial choice but produces an O(dt)
accumulated positional error of ~0.59 m over a 7-second 50 m/s shot, which exceeded
the ±0.05 m analytic-comparison tolerance. Velocity Verlet eliminates this error
entirely, bringing range and apex errors to < 0.01 m.

### Fixed timestep — 1/60 second (≈ 16.67 ms)

The sim controls its own step. Callers supply no `dt` or step-count parameter;
the `IProjectileSimulator.Simulate` signature accepts only logical inputs.

Trajectory time values are computed as `tick_index × FixedTimestep` (not
accumulated with `time += dt`) to prevent floating-point drift in the time axis.

### Impact point precision — linear interpolation of y = 0 crossing

Without interpolation the impact X error is O(dt × v_x) ≈ 0.6 m at 60 Hz for a
50 m/s shot — unacceptable for a game where landing X is the primary outcome.
Linear interpolation between the last above-ground tick and the first
below-ground tick reduces the error to O(dt² × v_x) ≈ 0.01 m.
Raw ticks are preserved in `ShotResult.Trajectory` for client playback;
only `ImpactPoint`/`ImpactTime` are interpolated.

### Coordinate system

| Axis | Direction |
|------|-----------|
| +X   | Right     |
| +Y   | Up        |

### Units

| Quantity  | Unit   |
|-----------|--------|
| Distance  | metres |
| Time      | seconds |
| Speed     | m/s    |
| Wind      | m/s² (constant horizontal acceleration) |
| Angles    | degrees, counter-clockwise from +X axis (0° = right, 90° = straight up) |

### Canonical constants (`SimConstants`)

| Constant | Value | Notes |
|----------|-------|-------|
| `DefaultGravity` | 9.8 m/s² | Standard Earth gravity |
| `FixedTimestep` | 1.0/60.0 s | 60 Hz physics tick rate |
| `MaxShotDuration` | 30.0 s | Safety cap; prevents infinite loops on degenerate inputs (e.g. gravity = 0) |
| `DegToRad` | π/180 | Degrees-to-radians conversion |

### Vector type — `Vec2D` (`readonly record struct`)

No double-precision 2D vector type exists in the .NET BCL (`System.Numerics.Vector2`
uses `float`). We define `Vec2D` as a `readonly record struct` to obtain:
- Stack allocation and zero boxing — no per-step heap pressure.
- Auto-generated value equality (`==`/`!=`) — required for exact-equality determinism tests.
- Operator overloads for readable physics expressions.

---

## Consequences

### Positive

- `double` provides 15–17 significant digits. Accumulated position error at
  a 7-second 50 m/s shot is negligible (< 0.01 m after interpolation).
- Within a single runtime, IEEE 754 guarantees identical results for identical
  inputs — exactly the within-runtime reproducibility required.
- No fixed-point implementation complexity, no overflow risk, full hardware FPU
  acceleration on all target platforms (x64, ARM64 iOS/Android).
- `SimConstants` is the single change-point for all tuning parameters.
- `Vec2D` as a value type means zero per-step allocation.

### Negative / Trade-offs

- **Not cross-platform bit-identical.** Different JIT backends or FPU
  configurations (e.g. flush-to-zero, x87 vs SSE2) could theoretically yield
  different bit patterns for the same computation. In practice this is rare on
  modern 64-bit targets.
- **Impact**: The server's result is authoritative; the client's trajectory is
  visual-only. Cross-platform bit-mismatch is therefore acceptable under the
  current architecture.
- **Future risk**: If we add client-side prediction that must bit-match the server
  (e.g. to eliminate visible correction jumps), fixed-point arithmetic would be
  required. Mitigation: all physics is isolated behind `IProjectileSimulator` and
  `SimConstants`; the replacement surface is bounded.

---

## Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| `float` | 7 significant digits; visible drift in range calculations over multi-second shots; fails ±0.05 m tolerance tests. |
| Fixed-point (`long`-based Q16.16 / Q32.32) | Cross-platform bit-determinism we do not need under server-authoritative architecture. High implementation cost; overflow risk for large coordinates; no hardware FPU acceleration. |
| Variable timestep | Non-deterministic across frame rates; violates the "sim controls its own step" design invariant encoded in `IProjectileSimulator`. |
| Semi-implicit Euler | Initial choice; replaced by Velocity Verlet after observing a 0.59 m range error over a 7-second shot (O(dt) accumulated error). For constant acceleration, Velocity Verlet is strictly superior with zero additional cost. |
| `System.Numerics.Vector2` | Uses `float`; cannot be used in the sim core. |
