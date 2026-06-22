# /content — Game Data Conventions

## Rules

- **Cosmetics are visual-only.** Every cosmetic entry must carry `visual_only: true`.
  No stat fields (`damage`, `defense`, `speed`, etc.) are permitted on cosmetic items.
  The export pipeline enforces this and fails the build on violation.
- **Never hard-code stats in C#.** All numeric tuning (weapon damage, armor values,
  tier thresholds) lives in `/content/data/`. C# code reads exported constants.
- **Schema changes require a migration entry.** Old exported artefacts must remain
  loadable by the live server for the duration of the compatibility window.
- **Designers own this directory.** Do not auto-generate files here from code.
  Data flows one way: `/content` → export → `/game` and `/server`.

## Data + Schema Convention (the reusable pattern)

Each content domain follows the same layout so `/sim` loaders stay uniform across systems
(balls, items, equipment, rooms, progression, economy):

- **Data:** `content/data/<domain>.json` — the authored values designers edit.
- **Schema:** `content/schema/<domain>.schema.json` — JSON Schema documenting every field,
  its unit, and its constraints (with each field's DDTank source meaning where applicable).
- **Versioning:** every data file carries a top-level integer `schemaVersion`. The matching
  `/sim` loader (`<Domain>Catalog.FromJson`) rejects unsupported versions. Bump it on
  breaking changes and keep old artefacts loadable for the compatibility window.
- **Loading boundary:** the host (server / Unity) reads the file; `/sim` parses the **text**
  via a pure `FromJson(string)` and never does file I/O. The parse is the single source of
  truth, so server and client cannot diverge.

Current files:

- `data/balls.json` (+ `schema/balls.schema.json`) — shell physics + blast/damage. Note the
  documented deferred item: velocity-dependent air-resistance drag is not modeled (it would
  void ADR-0002's Velocity-Verlet exactness); shell character is captured via `gravityScale` +
  `windSensitivity` instead.
- `data/combat.json` (+ `schema/combat.schema.json`) — damage formula tuning (guard/defence
  divisors + caps, falloff strength, attack floor/scale, base-damage bonus divisor). Loaded by
  `Sim.Content.CombatTuning.FromJson`. The C# fallback `CombatTuning.Default` mirrors this file
  and is **pinned to it by a test** (`combat.json == CombatTuning.Default`) so the two cannot
  drift — `/content` stays the single source of truth.
- `data/elements.json` (+ `schema/elements.schema.json`) — element set + a sparse advantage
  matrix (loaded by `Sim.Content.ElementTable.FromJson`). Unspecified pairs default to 1.0
  (pure DDTank additive); the advantage multipliers are **first-pass tuning**, to be iterated.
- `data/items.json` (+ `schema/items.schema.json`) — items and their data-defined effects
  (loaded by `Sim.Content.ItemCatalog.FromJson`). System #3 scope: `GrantBall` (references a
  `balls.json` id) and `RestoreHp` (flat amount). Item use is server-authoritative; the client
  only displays. `ItemCatalog.ValidateBallReferences(BallCatalog)` fail-fast checks `GrantBall`
  ids against `balls.json`. No `CombatTuning.Default`-style C# mirror, so no drift-lock test;
  a shipped-file test instead confirms it loads and its ball refs resolve. **Deferred to #4:**
  equipment categories + the modifier-stack stat assembly; heal strengthen-scaling.

## Enforced by Export Pipeline (planned)

- Cosmetic stat check (fails build if a cosmetic has stat fields)
- Schema validation against JSON Schema definitions
- Referential integrity (weapon references valid tier IDs, etc.)
