# /server — Backend Conventions

## Topology

Two tiers (ADR-0001, single rule source):

- **Nakama (Go/Lua/TS)** owns matchmaking, sessions, transport, presence, economy, and
  leaderboards. It cannot import our `netstandard2.1` `Sim.dll` (its runtime is Go/Lua/TS
  only), so it **never runs the simulation** — it delegates each authoritative step to the
  .NET match service.
- **The .NET match service** (`server/Vektram.MatchHost` and its successor) is the **only**
  component that imports `/sim`. The authoritative simulation runs in exactly one place:
  this service.

## Authority Rules

- The server is the single source of truth for all game state (see root `CLAUDE.md`).
- **Never trust client-provided damage, stats, RNG outcomes, currency, or match results.**
- Validate every client input. Silently reject and log any message that fails
  validation (type checks, range checks, turn-ownership checks).
- All economy mutations (award currency, grant item) happen here — never on the client.

## Sim Integration

- The authoritative simulation runs in exactly one place — the .NET match service, which
  imports `/sim`. Nakama delegates to it and must not reimplement any sim logic.
- Do not reimplement any formula from `/sim` in server code. If logic is missing,
  add it to `/sim` under a test, then call it from the .NET match service.

## Error Handling

- A failing match must not crash the Nakama process or the .NET match service. Recover and
  terminate the match cleanly, logging the full state at the point of failure.
- Economy operations must be transactional. Partial writes are bugs.
