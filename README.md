# Vektram

Turn-based artillery PvP mobile game — iOS + Android.
Unity 6.3 LTS 2D client · Server-authoritative simulation · Nakama backend.

## Repository Layout

| Path | Purpose |
|------|---------|
| [`/game`](game/README.md) | Unity 6.3 LTS 2D client |
| `/sim` | Shared C# simulation core — netstandard2.1, zero Unity dependencies |
| `/proto` | Protobuf schemas — shared data contracts |
| `/server` | Nakama modules + auxiliary services |
| `/content` | Canonical game-data source + export tooling |
| `/infra` | Docker / Kubernetes / Terraform |
| `/tools` | Codegen, content pipeline, CI scripts |
| `/docs` | Design docs + ADRs |

## Key Documents

- [Architecture Overview](docs/architecture.md)
- [ADR-0001 — Server-Authoritative Model & Shared Sim Core](docs/adr/ADR-0001-server-authoritative-shared-sim.md)

## Quick Start

**Run sim tests**
```bash
dotnet test sim/Sim.sln
```

**Start Nakama dev stack**
```bash
docker compose -f infra/docker-compose.dev.yml up
```

**Unity client** — see [game/README.md](game/README.md) for Unity Hub project-creation steps.
