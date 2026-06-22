# DDTank Source Map — Revision Basis for Vektram

**Source location:** `/ddtank/` (git-ignored; not built into Vektram)  
**Purpose:** This document maps every system in the DDTank reference source so that each Vektram revision
sprint can start from full knowledge of what's there rather than re-reading raw C# each time.

---

## 1. Overview

### Directory / project structure

```
ddtank/
  Game.Logic.sln          — single solution
  Game.Logic/             — the entire server-side game logic library
    AbstractGame.cs       — base class: room-type, game-type, hard-level enums
    BaseGame.cs           — turn queue, action list, wind/map management
    PVPGame.cs            — player-vs-player match: red/blue team construction
    PVEGame.cs            — player-vs-environment: boss/NPC game loop
    CampBattlePVEGame.cs  — guild/camp PVE variant
    Actions/              — sequential action objects (turn driver)
    AI/                   — bot brain + PVE game-control
    Bussiness/Managers/   — BattleBonusMgr (post-match rewards, DB-backed)
    Cmd/                  — command handlers (Fire, Move, Shoot, PropUse …)
    Effects/              — status-effect objects (freeze, DoT, guard, seal …)
    ElementSkills/        — elemental passive/active skills
    ElfSkills/            — "elf" passive skill layer
    GuardCoreEffects/     — "guard core" equip-level passive effects
    HorseEffects/         — mount equip effects
    MarkSkills/           — "mark" passive skills (on-kill bonuses)
    PetEffects/           — pet elemental/active effects
    PetSealSkills/        — pet seal skill objects
    PetSkills/            — pet active/passive/special skill objects
    Phy/
      Actions/            — physics-layer actions (BombAction, PetAction)
      Maps/               — Map + Tile (destructible terrain bitmap)
      Maths/              — EulerVector (projectile integrator)
      Object/             — Physics, BombObject, SimpleBomb, Living, Player …
    Protocol/             — eFightPackageType enum
    Spells/               — spell system (SpellMgr, spell handlers)
    BallMgr.cs            — shell/weapon template manager (DB + tile files)
    BallConfigMgr.cs      — per-config ball overrides
    DropInfoMgr.cs        — item drop quota tracker
    DropInventory.cs      — in-match drop-item inventory
    PropItemMgr.cs        — consumable item templates (DB-backed)
    WindMgr.cs            — wind-value image renderer (captcha-style display)
    MapMgr.cs             — map template manager
    eRoomType.cs          — 40+ room-type enum values
    eGameType.cs          — game type enum
    eEffectType.cs        — status-effect type enum
    AddMoneyType.cs       — currency-add reason enum
```

### Language / framework

- **C# (.NET Framework / .NET Standard)** — decompiled from a compiled DLL (IL-level comments like
  `// Token: 0x…` confirm this is decompiled bytecode, not hand-written source).
- No Unity dependency anywhere in `Game.Logic`; this is a pure server library.
- External dependencies visible in `using` statements: `SqlDataProvider.Data` (ORM layer),
  `Bussiness` / `Bussiness.Managers` (business logic layer), `log4net`, `Newtonsoft.Json`,
  `System.Drawing` (for terrain bitmaps and wind-display images), `EntityDatabase.PlayerModels`.
- The client side of the original game is **not present** in the dump — only server logic is here.

### High-level organisation

The architecture is a single-threaded per-match game loop driven by a sequential `ArrayList` of
`IAction` objects (BaseGame). A PVP or PVE game creates `Player` / `SimpleBoss` / `SimpleNpc`
objects, places them on a `Map`, then processes turns by dispatching actions. Projectile
simulation (`SimpleBomb.StartMoving`) runs synchronously inside each `Shoot` action, logging a
`List<BombAction>` event stream that is replayed on the client.

---

## 2. System-by-System Map

---

### 2.1 Combat / Damage

#### Where it lives
- `Phy/Object/SimpleBomb.cs` — `MakeDamage()`, `MakePetDamage()`, `MakeElementDamage()`,
  `BombImp()` (detonation dispatcher)
- `Effects/` — ~40 status-effect classes (DoT, freeze, seal, guard, critical, etc.)
- `Phy/Object/Living.cs` — `TakeDamage()`, `MakeCriticalDamage()`, stat fields

#### What it does — the real formula

**Standard damage (PVP/PVE, `MakeDamage`):**

```
guardReduce   = (BaseGuard_target − SunderArmor_attacker) / 12 000
                capped at 0.60
defenceReduce = Defence_target / 80 000
                capped at 0.90   (PeakBattle room: / 18 000)

damage = BaseDamage × (0.6 + Attack × 8e-5)
         × (1 − guardReduce)
         × CurrentDamagePlus × CurrentShootMinus
         × (1 − defenceReduce)
         × (1 + BaseDamage / 10 000)

# Match room variant:
damage = BaseDamage × Attack × 0.0001
         × CurrentDamagePlus × CurrentShootMinus
         × (1 − defenceReduce) / 3

# PeakBattle room variant:
damage = BaseDamage × Attack × 0.001
         × CurrentDamagePlus × CurrentShootMinus
         × (1 − defenceReduce)
```

**Falloff with distance inside blast radius:**
```
damage *= 1 − (distance / radius) / 4
```

**Element layer** (added on top, per attacker emblem type):
```
elementDamage = AttackerFireAttack − TargetFireDefence   (or Wind/Land/Water/Light/Dark)
magicDamage   = MagicAttack × 0.8 − MagicDefence × 0.5  (floor 0)
damage       += magicDamage + elementDamage
```

**Final output:**
```
finalDamage = (int)(damage × ExtraDamage) × (100 + CulturalAdd) / 100
```

**Pet damage (`MakePetDamage`)** uses a separate DR formula (sigmoid):
```
DR  = 0.95 × (BaseGuard − 3×Grade) / (500 + BaseGuard − 3×Grade)
DR2 = 0.95 × max(0, Defence − Lucky) / (600 + max(0, Defence − Lucky))
damage = BaseDamage × (1 + Attack × 0.002) × (1 − DR − DR2 + DR×DR2)
```

**Critical hits:** separate `MakeCriticalDamage` call; result added to base damage for reporting.

**Dungeon room bonuses:** Dungeon / AcademyDungeon / SpecialActivityDungeon rooms add
`Speed × 200 + MagicAttack × 4 + elementDamage × 4` to damage.

**Miss chance:** `rand.Next(100) < target.miss` → damage = 0, "Miss" displayed.

**Special bomb types** branched in `BombImp`:
- `FORZEN` — freeze effect via `IceFronzeEffect`
- `FLY` — launches/moves the owner player
- `CURE` — healing formula: `SecondWeapon.Property7 × 1.1^StrengthenLevel × countBonus`
- `WORLDCUP`, `CATCHINSECT` — event/minigame modes

#### What Vektram currently has
- Deterministic projectile physics (Euler integration, gravity + wind + air resistance).
- Blast-radius damage: single flat number, no stat-based scaling, no DR formula, no element layer,
  no per-room variants, no miss/crit system.

#### How the revision will use it
**Upgrade existing system.** Port the full DDTank damage formula (guard/defence DR + element layer
+ crit) into `/sim` as pure stateless functions. Stats come from data records in `/content`; the
server resolves final numbers and sends authoritative damage events. No formula logic in the
Unity client.

---

### 2.2 Projectile Physics (Angle / Power / Trajectory)

#### Where it lives
- `Phy/Maths/EulerVector.cs` — the integrator
- `Phy/Object/BombObject.cs` — per-step integration + collision walkthrough
- `Phy/Object/SimpleBomb.cs` — `StartMoving()` loop (dt = 0.04 s)
- `Phy/Object/Living.cs` — `GetShootForceAndAngle()`, `ComputeVx/Vy/DX()`
- `Phy/Maps/Map.cs` — `gravity`, `airResistance`, `wind`, `windRate` per-map params

#### What it does — the real formula

**EulerVector** represents one axis: position `x0`, velocity `x1`, acceleration `x2`.

**Integration step** (`ComputeOneEulerStep`, called at dt = 0.04 s):
```
x2 = (force − airResistance × velocity) / mass
x1 += x2 × dt
x0 += x1 × dt
```
Where `force` is wind force on X axis and gravity force on Y axis.

**Per-bomb parameters** (from `BallInfo` DB record):
- `Mass`, `Weight` (gravity multiplier), `Wind` (wind multiplier), `DragIndex` (air resistance multiplier)
- Effective forces: `arf = map.airResistance × DragIndex`, `gf = map.gravity × Weight × Mass`,
  `wf = map.wind × Wind × map.windRate`

**Collision walkthrough** (`BombObject.MoveTo`): steps along the straight line from current to
predicted position in increments of 3 px (whichever axis is dominant); at each step checks
rectangle collision with objects and terrain.

**Bot aiming** (`Living.GetShootForceAndAngle`): given a target (x, y), iterates over flight times
`t ∈ [botMinTime, 4.0]` in steps of 0.6 s, solving for initial velocity analytically:
```
vx = (dx − (wf/mass) × t²/2) / t + (arf/mass) × dx × 0.7
vy = (dy − (gf/mass) × t²/2) / t + (arf/mass) × dy × 1.3

force = √(vx² + vy²)   (used if < 2000 and vy < 0 and vx in direction of target)
angle = atan(vy / vx) in degrees
```
The first `t` that yields a valid (force < 2000, correct direction) solution is used.
The 0.7 / 1.3 asymmetry on the drag correction is a tuning fudge.

**Wind display** (`WindMgr`): renders a 9-digit numeric wind value as a tiny bitmap image
(CAPTCHA-style) so cheats cannot read it trivially. Not relevant to Vektram.

#### What Vektram currently has
- Identical Euler integration (dt configurable), gravity + wind + air resistance.
- Bot aiming via `BotAgent` (analytical solve with a trial-time loop).
- Already well-matched to the DDTank model.

#### How the revision will use it
**Calibrate / upgrade.** Adopt DDTank's exact per-ball `BallInfo` physics parameters (Mass,
Weight, Wind, DragIndex) from the DB schema. Add the `0.7 / 1.3` drag correction asymmetry to
the bot solver if testing shows it improves accuracy. Map gravity and air resistance values to
a `/content` data file per map.

---

### 2.3 Item / Inventory

#### Where it lives
- `PropItemMgr.cs` — `ItemTemplateInfo` keyed by ID, loaded from DB
- `BallMgr.cs` — `BallInfo` (shell/weapon) templates + collision tile files
- `BallConfigMgr.cs` — per-config ball parameter overrides
- `DropInfoMgr.cs`, `DropInventory.cs` — in-match drop quota + pickup tracking
- `Cmd/PropUseCommand.cs` — in-match item-use command
- `Cmd/PickCommand.cs` — ground-item pickup command
- `Cmd/SecondWeaponCommand.cs` — secondary-weapon activation
- `Effects/AddBombEquipEffect.cs`, `AddGuardEquipEffect.cs`, etc. — equip stat modifiers
- `Player.cs` — `m_weapon`, `m_DeputyWeapon`, `m_Healstone`, equip/pet slot init

#### What it does
- Items split into: **shells** (`BallInfo` — physics params + blast radius + special type),
  **consumable props** (`ItemTemplateInfo` — in-match use items with effect type+value),
  **equipment** (weapon/armor slots that feed into stat calculation at match start),
  **secondary weapon** (separate slot: `SecondWeapon.Template.Property7` drives heal-gun output),
  **pet** (attached to player, supplies `PetEffects` stat modifiers).
- `BallInfo` key fields: `ID`, `Mass`, `Weight` (gravity factor), `Wind`, `DragIndex`,
  `Power` (base damage), `Radii` (blast radius), `BombType` enum (Normal/Frozen/Fly/Cure/…),
  `IsSpecial()` flag (skips terrain dig).
- Equipment effects apply at `Player.Reset()` via `InitEqupedEffect()` which walks an equip-effect
  list and calls `Start(player)` on each `AbstractEffect`.
- In-match pickups: `DropInventory` tracks dropped items by ID; `PickCommand` fires pickup;
  items are consumables applied immediately.

#### What Vektram currently has
Nothing — no item/inventory system exists.

#### How the revision will use it
**Build new from DDTank's design.** Implement in two layers:
1. `/content` data files: `balls.json` (shell physics + blast params), `items.json`
   (consumable templates), `equipment.json` (stat modifiers by slot/tier).
2. `/sim` stat pipeline: at match start each player's stat block is assembled from base stats +
   equipment modifiers — pure data transform, no item code in sim. Items used in-match
   arrive as typed events (`ItemUseEvent`) resolved on the server.
This is the largest missing surface. Priority: shell physics params first (unblocks damage
calibration), then consumable props, then equipment stat pipeline.

---

### 2.4 Room / Matchmaking

#### Where it lives
- `eRoomType.cs` — 40+ room type enum values
- `BaseGame.cs` — generic room/game construction (map, player list, turn queue)
- `PVPGame.cs` — red/blue team construction (up to 4 sub-lists per side → SurvivalMode)
- `PVEGame.cs` — PVE game construction (boss + NPC placement)
- `AbstractGame.cs` — base room state: id, roomType, gameType, hardLevel

#### What it does
- Rooms are typed by `eRoomType` (Match, Freedom, Dungeon, PeakBattle, SurvivalMode, etc.).
  Each type drives stat scaling overrides applied at `Player.Reset()`.
- `PVPGame` accepts up to 8 `List<IGamePlayer>` sub-groups (4 red, 4 blue) allowing up to N×4
  players per side. SurvivalMode increments `m_redTeamIndex` by 2 for each sub-group (separate
  teams in a free-for-all variant).
- Turn order is managed by `m_turnQueue` (a `List<TurnedLiving>`) in `BaseGame`.
- No matchmaking logic is present in this library — that lives in the server layer (Nakama /
  auxiliary services in Vektram's `/server`).

#### What Vektram currently has
- `MatchController` — steppable, agent-agnostic turn driver; supports N-vs-N.
- No room-type system; no stat scaling by room type.

#### How the revision will use it
**Add room-type layer.** Define a `RoomConfig` record in `/content` with per-type stat scaling
tables (matching the DDTank overrides). `MatchController` receives a `RoomConfig` and applies
scaling at player initialisation. Room type also gates which items and modes are active.
Matchmaking (player joining, room creation) stays in `/server` (Nakama).

---

### 2.5 Economy

#### Where it lives
- `Bussiness/Managers/BattleBonusMgr.cs` — post-match reward DB table (`BattleBonusInfo`)
- `AddMoneyType.cs` — enum of reasons currency can be added
- `eBattleRemoveMoneyType.cs` — enum of reasons currency is removed in battle
- `DropInfoMgr.cs` — per-template drop quota (max drops per day / session)
- `Player.cs` — `TotalDameLiving`, `TotalDamagePlayer`, `TotalCure`, `TotalAllScore`, etc.

#### What it does
- Post-match rewards come from a `BattleBonusInfo` table keyed by type (Win/Loss/MVP/etc.),
  loaded from the production DB. The library only manages in-match stat tracking
  (`TotalDameLiving`, kills, etc.); the reward grant itself happens in the server layer.
- In-match economy: `DropInventory` controls what items drop and at what quantity (quota-limited
  by `DropInfoMgr`). Currency awarded for kills / objectives is tracked by enum reason codes.
- No detailed XP curves or shop pricing are visible in this dump (those live in the DB/server).

#### What Vektram currently has
Nothing.

#### How the revision will use it
**Build new, DDTank-informed.** Model post-match reward types as a `/content` data table
(reward type → currency / item grant). The server resolves grants after match results arrive.
Drop quota logic can be ported to Nakama server-side Lua/JS. The in-match tracking fields
(`TotalDameLiving`, etc.) map to the `MatchController`'s result output.

---

### 2.6 Progression

#### Where it lives
- `Player.cs` — `Grade` (level), `StrengthEnchance` (all-stat flat bonus per-level),
  stat-scaling overrides per room type, `evolutionGrade` (pet evolution tier)
- `eLevelLimits.cs` — level cap enum values
- `PetMgr.cs` — `FindFightProperty(evolutionGrade)` → pet stat block by evolution tier

#### What it does
- Player level (`Grade`) feeds directly into:
  - Per-room stat floor formulas (e.g. AcademyDungeon: `BaseDamage = 3500 + baseDamage×0.6`)
  - `StrengthEnchance` adds identically to Attack / Defence / Agility / Lucky
  - Pet DR formula: `BaseGuard − 3×Grade` in the denominator
- `evolutionGrade` selects a `_petFightProperty` stat block (blood/attack bonus).
- No XP tables or level-up logic are visible in this dump.

#### What Vektram currently has
Nothing.

#### How the revision will use it
**Build new.** Define a `/content/progression.json` with level-to-stat tables and XP curves,
informed by the DDTank per-level `StrengthEnchance` pattern. Level feeds into the damage formula
(already present in the `Grade` denominator). Pet evolution grades map to a lookup table in
`/content/pets.json`.

---

### 2.7 AI / Bot Behaviour

#### Where it lives
- `AI/ABrain.cs` — abstract bot brain base
- `AI/Npc/SimpleBrain.cs` — concrete NPC brain
- `AI/Game/SimplePVEGameControl.cs` — game-level PVE AI controller
- `AI/Mission/SimpleMissionControl.cs` — mission-level AI controller
- `AI/AMissionControl.cs`, `AI/APVEGameControl.cs` — abstract bases
- `Actions/BotShootAction.cs` — executes one bot shot
- `Phy/Object/Living.cs` — `GetShootForceAndAngle()`, `ShootPoint()`

#### What it does
- **Bot shooting:** `BotShootAction.ExecuteImp` calls `GetShootForceAndAngle` (analytical
  solver, see §2.2) to recompute force + angle toward the target, then calls `ShootImp`.
- **Controlled / invoked shells:** `SimpleBomb.StartMoving` has three homing modes:
  - `invoked` → continuously steers toward the nearest enemy (full search radius 10 000 px).
  - `controlled` (falling phase, vY > 0) → steers toward nearest enemy within 150 px.
  - `invoked && controlled` → searches radius 50 000 px.
  In homing mode, velocity is replaced with a normalized direction vector and forces set to 0.
- **NPC brain** (`SimpleBrain`) and **PVE controller** (`SimplePVEGameControl`) control boss and
  NPC turn decisions; the full decision logic is inside these classes (not fully read here).

#### What Vektram currently has
- `BotAgent` — deterministic analytical solver (trial-time loop, same algorithm as DDTank).
- No homing/controlled shell logic.
- No NPC/boss brain.

#### How the revision will use it
**Upgrade existing bot + add homing.** The `BotAgent` solver already matches. Add:
1. Homing shell support: a `ControlledBomb` variant in `/sim` that applies velocity-redirect
   events (fits the existing action-stream model).
2. Port `SimpleBrain` decision logic into a Vektram `NpcBrain` in `/sim` for deterministic PVE.

---

## 3. Dependency Graph (revision order)

```
BallInfo physics params (§2.2 calibration)
    └─ requires: /content/balls.json schema
           └─ unblocks: damage formula port (§2.1)
                  └─ requires: stat pipeline (§2.3 equipment layer)
                         └─ unblocks: progression formula integration (§2.6)

Room-type config (§2.4)
    └─ unblocks: per-room stat scaling, economy reward types (§2.5)

All of the above unblock:
    Economy / post-match rewards (§2.5)
    AI: homing shells + NPC brain (§2.7)
```

Tight couplings in the source to watch:
- **Damage ↔ Stats ↔ Equipment** — `MakeDamage` reads 12+ stat fields from `Living`. Any
  equipment-layer port must produce those fields before the damage formula is wired in.
- **Damage ↔ RoomType** — room type selects formula variant and stat floor. Port both together.
- **Bot aiming ↔ BallInfo physics** — solver uses `Mass`, `Weight`, `Wind`, `DragIndex` from
  `BallInfo`. Bot won't aim correctly until ball params are in `/content`.
- **Economy ↔ Match results** — reward grants depend on kill/damage/cure totals recorded during
  match; those tracking fields must exist before rewards are wired.

---

## 4. Revision Priority Plan

| Priority | System | Why first | Vektram landing |
|----------|--------|-----------|-----------------|
| **1** | Shell/ball physics params (`/content/balls.json`) | Unblocks damage formula calibration and correct bot aiming; currently Vektram uses placeholder constants | `/content` data file + loader in `/sim` |
| **2** | Damage formula — guard/defence DR + crit | High-value formula upgrade to an existing system; makes combat feel correct immediately | Pure functions in `/sim/Combat/DamageCalc.cs`; server resolves, client displays only |
| **3** | Item/inventory — consumable props + BallInfo types | Completely absent; needed before any match feels complete; consumables interact with existing turn flow | `/content/items.json` + `/sim` item-event handler; server-authoritative activation |
| **4** | Equipment stat pipeline | Prerequisite for full damage formula (stats must come from somewhere); unlocks progression too | `/content/equipment.json` + stat assembly in `/sim` at match start |
| **5** | Room-type config + stat scaling | Unlocks game-mode variety (PVP/ranked/dungeon) and makes test matches comparable to DDTank balance | `/content/rooms.json` + `RoomConfig` consumed by `MatchController` |
| **6** | Progression — XP tables + level stat bonuses | Cannot balance without it but depends on stat pipeline being in place | `/content/progression.json` + level-up hook in `/server` |
| **7** | Economy — post-match rewards | Depends on match result output (already tracked); reward table is simple once progression exists | `/content/rewards.json` + Nakama post-match handler |
| **8** | AI: homing shells + NPC brain | Bot quality upgrade; depends on BallInfo params being available; lower priority than core combat | `/sim` `ControlledBomb` variant + `NpcBrain` |

**Rationale for weighting:**
- Items (rank 3) are prioritised above equipment (rank 4) because consumable props require only
  a minimal stat surface to function, and their absence is the most visible gap in a playable
  match.
- Economy (rank 7) ranks below progression because reward amounts are meaningless without a
  level curve to calibrate against.
- AI homing (rank 8) is last because current `BotAgent` is already functional; homing is a
  polish upgrade, not a missing foundation.
