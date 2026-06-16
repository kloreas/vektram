# /server — Nakama Backend Conventions

## Authority Rules

- The server is the single source of truth for all game state (see root `CLAUDE.md`).
- **Never trust client-provided damage, stats, RNG outcomes, currency, or match results.**
- Validate every client input. Silently reject and log any message that fails
  validation (type checks, range checks, turn-ownership checks).
- All economy mutations (award currency, grant item) happen here — never on the client.

## Sim Integration

- Import `Sim.dll` (compiled from `/sim`) to run the authoritative simulation.
- Do not reimplement any formula from `/sim` in server code. If logic is missing,
  add it to `/sim` under a test, then call it here.

## Error Handling

- Match handler panics must not crash the Nakama process. Recover and terminate
  the match cleanly, logging the full state at the point of failure.
- Economy operations must be transactional. Partial writes are bugs.
