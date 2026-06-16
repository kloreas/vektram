# Vektram — Repo Conventions

## Non-Negotiable Ground Rules

### 1 — Server Authority Boundary

The server is the **single source of truth** for all simulation outcomes.

**Server owns:** damage, effective stats, RNG (seed never leaves server), currency,
item ownership, match results, turn order, shot-clock enforcement.

**Client sends:** inputs only — aim angle, power, charge timing, item activation.  
**Client receives:** authoritative state snapshots and event streams.  
**Client renders; it never decides.**

Never write client-side code that reads a damage or stat value from local simulation
and uses it to alter game state. Route all such decisions through the server.

### 2 — The Sim Boundary

`/sim` is a **Unity-independent** `netstandard2.1` class library.

- **No** `UnityEngine.*` or `Unity.*` namespaces inside `/sim`.
- **No** `MonoBehaviour`, coroutines, or Unity math types.
- Use `System.Numerics.Vector2/Vector3` or plain structs instead of `UnityEngine.Vector2`.
- The server imports `/sim` directly. The Unity client imports the same compiled DLL.
- If you are about to add a Unity dependency to `/sim`, stop — move the code to `/game`.

### 3 — Cosmetics Never Affect Stats

Cosmetics are purely visual. No stat multipliers, no hidden bonuses, no damage-type
overrides. The content schema enforces this; the export pipeline will fail the build
if violated.

### 4 — Data-Driven Content

Weapon stats, armor values, tier breakpoints, and item properties are **data, not code**.
Canonical source lives in `/content`. Never hard-code tuning values in C#.

---

## Directory Map

| Path | Purpose |
|------|---------|
| `/game` | Unity 6.3 LTS 2D client |
| `/sim` | Shared sim core — netstandard2.1, zero Unity deps |
| `/proto` | Protobuf schemas |
| `/server` | Nakama modules + auxiliary services |
| `/content` | Canonical game data + export tooling |
| `/infra` | Docker / Kubernetes / Terraform |
| `/tools` | Codegen, content pipeline, CI scripts |
| `/docs` | Design docs + ADRs |

---

## Build & Test Commands

```bash
# Run all sim unit tests
dotnet test sim/Sim.sln

# Build sim in Release (produces Sim.dll for Unity)
dotnet build sim/Sim/Sim.csproj -c Release -o sim/out

# Regenerate Protobuf stubs (once tooling is wired up)
tools/proto-gen.sh

# Start Nakama dev stack
docker compose -f infra/docker-compose.dev.yml up
```

Unity projects are opened via Unity Hub — do not run `dotnet build` on `/game`.

---

## C# Coding Conventions

- `<Nullable>enable</Nullable>` in every project. No `!` suppressions without a comment explaining the invariant.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in every project.
- No `public` mutable fields; use properties.
- Private fields: `_camelCase`. Public members: `PascalCase`. Interfaces: `IFoo`. Type params: `TFoo`.
- No comments explaining *what* code does — name things well instead.
- Only comment the *why*: a hidden invariant, a worked-around bug, a non-obvious constraint.
- Tests: Arrange / Act / Assert sections separated by blank lines. No magic numbers — name constants.
- Sim types should prefer value semantics (structs, immutable records) where practical.
- All sim randomness takes an explicit `uint seed` parameter — no unseeded `System.Random()`.
- Use `double` for sim math (server precision); convert to `float` only at the Unity rendering layer.

---

## ADR Index

| ID | Title |
|----|-------|
| [ADR-0001](docs/adr/ADR-0001-server-authoritative-shared-sim.md) | Server-Authoritative Model & Shared Sim Core |
