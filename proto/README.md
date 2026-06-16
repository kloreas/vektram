# /proto — Protobuf Schemas

Shared data contracts between the Unity client and the Nakama backend.
All client ↔ server messages are defined here.

## Planned Structure

```
proto/
  match/      Shot inputs, state snapshots, event streams
  economy/    Currency, item, shop messages
  lobby/      Matchmaking, room creation
  common/     Shared types (player, item ref, etc.)
  gen/        Generated stubs (gitignored — produced by tools/proto-gen.sh)
```

## Codegen

```bash
tools/proto-gen.sh   # not yet implemented
```

Generates C# stubs into `/sim` or `/game/Assets/Proto/` and Go stubs into `/server/proto/`.
