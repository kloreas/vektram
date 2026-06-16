# /game — Unity Client

This directory is intentionally empty. Create the Unity project here via Unity Hub.

---

## 1. Prerequisites

- **Unity Hub 3.x+**
- **Unity 6.3 LTS** — install via Unity Hub → **Installs → Add → 6.3 LTS**
  Required modules (check during install):
  - Android Build Support (includes OpenJDK + Android SDK & NDK)
  - iOS Build Support
- Recommended: Rider or VS 2022 IDE integration module

---

## 2. Create the Project

1. Open Unity Hub.
2. **Projects → New Project**.
3. Template: **2D (URP)**.
4. Unity version: **6.3 LTS**.
5. Project name: `game`.
6. Location: `<repo-root>/` — Unity Hub will create the `game/` subfolder.
   > If Hub creates `game/game/` by mistake, move the inner contents up one level
   > so that `game/Assets/` sits directly under the repo root.
7. Click **Create project** and let the Editor open and compile once before proceeding.

---

## 3. Verify Layout After Creation

```
game/
  Assets/
  Packages/
  ProjectSettings/
  UserSettings/      ← gitignored
  Library/           ← gitignored
```

---

## 4. Assembly Definition Layout

Assembly definitions (`.asmdef`) enforce hard module boundaries at Unity compile time.
Create them via **Assets → Create → Assembly Definition**.

```
game/Assets/
  Runtime/
    Vektram.Client.Core.asmdef      ← main game logic; references Sim.dll
    Vektram.Client.UI.asmdef        ← UI only; references Core
    Vektram.Client.Audio.asmdef     ← audio only; references Core
  Tests/
    Vektram.Client.Tests.asmdef     ← Unity Test Framework; references Core
```

### Example: `Vektram.Client.Core.asmdef`

```json
{
    "name": "Vektram.Client.Core",
    "rootNamespace": "Vektram.Client",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Sim.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> `overrideReferences: true` + `precompiledReferences` is the correct way to
> reference a pre-built DLL without auto-reference magic.

---

## 5. Referencing /sim in Unity

`/sim` builds as `Sim.dll` (netstandard2.1). Unity 6 supports netstandard2.1 DLLs natively.

### Step-by-step

```bash
# From the repo root:
dotnet build sim/Sim/Sim.csproj -c Release -o sim/out
```

Then copy `sim/out/Sim.dll` (and optionally `Sim.pdb`) to:

```
game/Assets/Plugins/Sim/Sim.dll
game/Assets/Plugins/Sim/Sim.pdb
```

In the Unity Inspector for `Sim.dll`:
- **Select All Platforms** ✓ (or configure per-platform as needed)
- Leave **Editor** checked unless you want to exclude the DLL from in-Editor runs.

The `tools/copy-sim-dll.sh` script (planned) will automate these two steps.
Wire it as a pre-build step once the content pipeline is established.

---

## 6. Initial URP 2D Setup

After the project opens in the Editor:

1. **Edit → Project Settings → Graphics** — confirm the URP Renderer asset is assigned.
2. **Edit → Project Settings → Player**:
   - iOS: Bundle Identifier → `com.vektram.game`
   - Android: Package Name → `com.vektram.game`
3. **Edit → Project Settings → Quality** — remove all except one quality level for mobile.
4. **Edit → Project Settings → Physics 2D** — all gameplay physics runs in `/sim`;
   disable Unity Physics 2D simulation on gameplay objects to avoid conflicts.

---

## 7. Client / Sim Boundary — Quick Reference

| Belongs in `/sim` | Belongs in `/game` |
|-------------------|--------------------|
| Projectile trajectory math | Trajectory line renderer |
| Damage formula | HP bar animation |
| Wind calculation | Wind-indicator UI |
| Turn order logic | Turn banner + timer UI |
| Terrain deformation math | Terrain mesh update |
| Shot-clock tick | Shot-clock countdown display |
| Seeded RNG | Particle effects, cosmetic VFX |

**Rule:** If a file imports `UnityEngine` and decides "what is the result?", the
logic belongs in `/sim`. If it decides "how do we display the result?", it stays
in `/game`.
