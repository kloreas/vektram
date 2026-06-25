# /server — Backend

Two cooperating tiers, per ADR-0001 (single rule source):

- **Nakama (Go/Lua/TS)** — self-hosted game server owning matchmaking, sessions,
  transport, presence, economy, and leaderboards. It does **not** run the simulation;
  Nakama's runtime can only load Go/Lua/TS server-side and cannot import our
  `netstandard2.1` `Sim.dll`. It **delegates** every authoritative simulation step to the
  match service.
- **.NET match service** (`server/Vektram.MatchHost` and its successor) — the only
  component that imports `/sim`. The authoritative simulation runs in exactly one place:
  here. Nakama RPCs into it.

## Responsibilities

### Nakama tier
- Match lifecycle: create, join, start, tick, end
- State snapshot broadcast to all connected players
- Input validation and rejection
- Economy transactions (currency, items)
- Leaderboard and rank updates
- Session management and auth
- Delegating each authoritative simulation step to the .NET match service

### .NET match service tier
- Authoritative simulation execution per shot (the sole importer of `/sim`)

## Running Locally

```bash
docker compose -f ../infra/docker-compose.dev.yml up
```

See `/infra/README.md` for prerequisites.
