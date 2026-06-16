# /tools — Tooling & Scripts

Build utilities, codegen, and CI helper scripts.

## Planned Contents

```
tools/
  proto-gen.sh          Runs protoc, generates C# + Go stubs from /proto
  content-export.sh     Exports /content data → game + server artefacts
  copy-sim-dll.sh       Builds Sim.dll and copies it to game/Assets/Plugins/Sim/
  ci/                   CI-specific helper scripts
```

## copy-sim-dll.sh (planned)

```bash
#!/usr/bin/env bash
dotnet build sim/Sim/Sim.csproj -c Release -o sim/out
mkdir -p game/Assets/Plugins/Sim
cp sim/out/Sim.dll game/Assets/Plugins/Sim/
cp sim/out/Sim.pdb game/Assets/Plugins/Sim/
```
