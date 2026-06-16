# ADR-0001: Server-Authoritative Simulation & Shared Simulation Core

**Date:** 2026-06-16
**Status:** Accepted
**Deciders:** Initial architecture session

---

## Context

We are building a competitive, real-money-adjacent turn-based artillery PvP mobile game.
Players fire projectiles at each other over the internet. Two foundational design
questions must be settled before any code is written:

1. **Who runs the combat simulation?**
   Options: client-authoritative, P2P with consensus, or server-authoritative.

2. **How do we share physics and game-rule logic?**
   The server must run the canonical simulation. The client benefits from running
   the same logic for trajectory preview and visual playback. Using separate
   codebases risks rule divergence — a silent bug category with no obvious
   detection mechanism.

---

## Decision

### Decision 1 — Server-Authoritative Simulation

The dedicated match server is the **single source of truth** for all simulation
outcomes. It:

- Holds the RNG seed; clients never receive it.
- Runs the full combat simulation on every shot.
- Validates every client input; silently rejects invalid or out-of-sequence inputs.
- Broadcasts authoritative state snapshots and structured event streams to all
  connected players.
- Enforces turn order and the shot clock.

The client:

- Sends only inputs: aim angle, power, charge duration, optional item activation.
- Receives authoritative state and replays the simulation locally for smooth visuals,
  using the same shared sim library.
- **Never uses a locally computed result to alter game state.**

Anything the client cannot forge: damage values, effective stats, currency balance,
item ownership, match outcome, turn order, and RNG outcomes.

### Decision 2 — Shared C# Simulation Core (`/sim`)

A single `netstandard2.1` C# class library (`/sim/Sim`) contains all combat
mathematics:

- Projectile physics (trajectory, drag, air resistance)
- Environmental modifiers (wind, gravity)
- Terrain collision and deformation math
- Damage formulas and effective-stat calculations
- Turn order and shot-clock logic
- Seeded, reproducible RNG utilities

This library:

- Has **zero Unity dependencies** — `dotnet build` succeeds without Unity installed.
- Is consumed by the **Nakama match handler** to run the authoritative simulation.
- Is compiled to a `netstandard2.1` DLL and consumed by the **Unity client** for
  local trajectory preview and visual playback.
- Has a comprehensive **xUnit test suite** that can run in CI without a game server
  or Unity install.
- Exposes only **pure functions** (same inputs → same outputs, no side effects,
  no I/O).
- All randomness accepts an explicit `uint seed` parameter — no unseeded
  `System.Random()` calls.

---

## Consequences

### Positive

- **Cheat resistance.** The client cannot report false damage or fabricate stats.
  The worst attack surface is a disconnect or sending malformed inputs, both of
  which the server handles gracefully.
- **Single rule source.** Physics tuning and balance changes live in one place and
  propagate automatically to both the authoritative server and the client preview.
- **Testability.** Every formula is unit-testable with plain `dotnet test` — no
  Unity, no live server, no network required. This is the primary quality gate.
- **Replay fidelity.** Replays are fully reproducible: store the input stream,
  replay it through the authoritative sim, get the identical match.
- **Auditability.** Disputes and suspected exploits can be investigated by
  replaying stored input logs.

### Negative / Trade-offs

- **Shot latency.** Every shot must round-trip to the server before its result is
  canonical. Mitigated by client-side predictive playback (same sim, unconfirmed)
  and authoritative correction on receipt. Acceptable for an artillery game with a
  slow turn cadence.
- **Server CPU cost.** The server does real simulation work per shot. For artillery
  cadence this is cheap, but must be profiled under load before launch.
- **Deployment coupling.** Updating sim rules requires a coordinated server + client
  deploy to keep the shared DLL in sync. A versioned protocol or compatibility window
  must be designed before the game reaches a live playerbase.
- **Discipline requirement.** Having two execution contexts (server authority +
  client playback) means the sim boundary must be actively enforced. The rules in
  `CLAUDE.md` and the zero-Unity-deps constraint are the enforcement mechanism.

---

## Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| Client-authoritative | Trivially cheat-able. Unacceptable for a competitive game with real stakes. |
| P2P with consensus | Complex desync handling; still vulnerable to cheating if one client is modified; poor NAT traversal on mobile. |
| Duplicate sim code per platform | Inevitable rule divergence as balance changes are made; double maintenance burden; no single test suite. |
| Unity-only simulation (MonoBehaviour) | Blocks server-side headless execution without a Unity license; couples the server build to the Unity Editor lifecycle. |
