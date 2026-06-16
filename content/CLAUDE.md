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

## Enforced by Export Pipeline (planned)

- Cosmetic stat check (fails build if a cosmetic has stat fields)
- Schema validation against JSON Schema definitions
- Referential integrity (weapon references valid tier IDs, etc.)
