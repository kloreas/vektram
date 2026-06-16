# Architecture Overview

## Three-Tier System

```
┌─────────────────────────────────────────────────────────────────────────┐
│  TIER 1 — CLIENT  (Unity 6.3 LTS 2D · iOS + Android)                   │
│                                                                         │
│  Rendering · UI · Audio · Animation · Input collection                  │
│  Local sim playback (Sim.dll, predictive / visual only)                 │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │  WebSocket · Protobuf
                               │  ↑ inputs (angle, power, charge, item)
                               │  ↓ state snapshots + event stream
┌──────────────────────────────▼──────────────────────────────────────────┐
│  TIER 2 — MATCH SERVER  (Nakama match handler + /sim)                   │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  /sim  (Sim.dll — netstandard2.1)                                 │  │
│  │  Projectile physics · Wind · Gravity · Terrain collision          │  │
│  │  Damage formulas · Effective stats · Turn order · Shot clock      │  │
│  │  Seeded RNG · Terrain deformation math                            │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  • Validates every client input; rejects + logs invalid messages        │
│  • Calls sim → receives authoritative result                            │
│  • Broadcasts state snapshot to all players in the match                │
│  • Enforces turn order and shot-clock countdown                         │
│  • Reports match result to Tier 3 on completion                         │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │  Internal / gRPC
┌──────────────────────────────▼──────────────────────────────────────────┐
│  TIER 3 — METAGAME BACKEND  (Nakama)                                    │
│                                                                         │
│  Auth & sessions · Player profiles · Economy (currency, item ownership) │
│  Matchmaking & lobby · Leaderboards & rank · Content catalogue delivery │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow: A Single Shot

```
Player aims and fires
        │
        ▼
Client sends: { playerId, angle, power, chargeMs, itemSlot? }
        │
        ▼  WebSocket · Protobuf
MATCH SERVER
  1. Validate input (correct turn, values in range, item owned)
  2. sim.Simulate(shotInput, currentMatchState) → ShotResult
  3. Apply ShotResult to authoritative match state
  4. Broadcast to all players:
       { trajectory[], impactPoint, damage, newHp[], terrainDelta,
         nextTurnPlayerId, remainingMs }
        │
        ▼
Client receives authoritative state snapshot
Client replays trajectory using Sim.dll (same result, visual only)
Client renders explosion, HP change, terrain deformation
Client transitions to next turn
```

---

## Shared Simulation Core

`/sim` is the architectural linchpin:

- `netstandard2.1` class library — **zero Unity or platform dependencies**.
- The match server (Tier 2) imports it to run the **authoritative** simulation.
- The Unity client (Tier 1) imports the same compiled `Sim.dll` for **predictive
  playback** and trajectory preview. Local results are visual only; the authoritative
  server result always wins.
- Tested with xUnit — no Unity install or live server required.
- All functions are **pure and deterministic** given the same inputs.

---

## Content Pipeline

```
/content  (canonical YAML / JSON authored by designers)
    │
    ▼  tools/content-export.sh  (not yet implemented)
    ├── C# constants → compiled into /sim and /game at build time
    └── server data bundle → deployed alongside Nakama
```

Cosmetics are tagged `visual_only: true` in the schema. The export pipeline fails
the build if a cosmetic entry contains any stat field.

---

## Key Contracts

| Boundary | Transport | Format |
|----------|-----------|--------|
| Client ↔ Match Server | WebSocket | Protobuf (schemas in `/proto`) |
| Match Server ↔ Metagame | Internal call / gRPC | Protobuf |
| Content pipeline → Server / Client | File at deploy time | JSON / binary bundle |
| `/sim` → Server + Client | Compiled DLL | netstandard2.1 assembly |

---

## Authority Boundary (summary)

| Value | Owner | Client trusted? |
|-------|-------|----------------|
| Damage dealt | Server (`/sim`) | No |
| Effective stats | Server (`/sim`) | No |
| RNG seed & outcomes | Server | No |
| Currency balance | Tier 3 (Nakama) | No |
| Item ownership | Tier 3 (Nakama) | No |
| Match result | Server | No |
| Turn order | Server | No |
| Visual presentation | Client | N/A |
| Input intent | Client | (validated by server) |
