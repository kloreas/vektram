# /server — Nakama Backend

Nakama self-hosted game server. Match handlers import `/sim` to run the
authoritative simulation.

## Responsibilities

- Match lifecycle: create, join, start, tick, end
- Authoritative simulation execution per shot (via `/sim`)
- State snapshot broadcast to all connected players
- Input validation and rejection
- Economy transactions (currency, items)
- Leaderboard and rank updates
- Session management and auth

## Running Locally

```bash
docker compose -f ../infra/docker-compose.dev.yml up
```

See `/infra/README.md` for prerequisites.
