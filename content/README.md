# /content — Game Data

Canonical source of all game content: weapons, armor, tiers, cosmetics.
This is the single place designers edit tuning values.

## Planned Structure

```
content/
  schema/     JSON Schema / Protobuf definitions for content types
  data/       Canonical YAML / JSON data files
  export/     Export scripts → C# constants + server data bundles
  tests/      Schema validation tests
```

## Export

```bash
tools/content-export.sh   # not yet implemented
```

Exported artefacts land in `/game/Assets/Data/` and `/server/data/`.
